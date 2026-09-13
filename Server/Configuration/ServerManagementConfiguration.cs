#nullable enable

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerManagementConfiguration
{
    public const string DefaultFileName = "server-management.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int SchemaVersion { get; set; } = 1;

    public string Documentation { get; set; } =
        "Players are matched only after their friend code is verified by the SuperGanggarrison account API.";

    public List<ServerManagedPlayerConfiguration> Players { get; set; } = [];

    public static ServerManagementConfiguration LoadOrCreate(string path, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            var template = CreateTemplate();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonSerializer.Serialize(template, SerializerOptions) + Environment.NewLine);
            log?.Invoke($"[management] created configuration template: {path}");
            return template;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ServerManagementConfiguration>(File.ReadAllText(path), SerializerOptions);
            if (parsed is null) throw new JsonException("Configuration document was empty.");
            parsed.Players ??= [];
            return parsed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            log?.Invoke($"[management] could not load {path}: {ex.Message}. No configured privileges or titles will be granted.");
            return new ServerManagementConfiguration();
        }
    }

    internal static ServerManagementConfiguration CreateTemplate()
        => new()
        {
            Players =
            [
                new ServerManagedPlayerConfiguration
                {
                    Enabled = false,
                    FriendCode = "OG2-YOUR-CODE",
                    Role = "Owner",
                    Permissions = ["FullAccess"],
                    Title = new ServerManagedPlayerTitleConfiguration
                    {
                        Text = "[Owner]",
                        Color = "rainbow",
                    },
                },
            ],
        };
}

internal sealed class ServerManagedPlayerConfiguration
{
    public bool Enabled { get; set; } = true;

    public string FriendCode { get; set; } = string.Empty;

    public string Role { get; set; } = "Special";

    public List<string>? Permissions { get; set; }

    public ServerManagedPlayerTitleConfiguration? Title { get; set; }
}

internal sealed class ServerManagedPlayerTitleConfiguration
{
    public string Text { get; set; } = string.Empty;

    public string Color { get; set; } = "#FFFFFF";
}

internal readonly record struct ServerManagedPlayerGrant(
    string FriendCode,
    OpenGarrisonServerAdminPermissions Permissions,
    string TitleText,
    uint TitleColorRgb,
    bool TitleRainbow);

internal sealed class ServerManagementService
{
    private const string FriendCodePrefix = "OG2";
    private const string FriendCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly Dictionary<string, ServerManagedPlayerGrant> _grants;
    private readonly Action<string>? _log;

    public ServerManagementService(ServerManagementConfiguration configuration, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _log = log;
        _grants = BuildGrants(configuration.Players ?? []);
        _log?.Invoke($"[management] loaded {_grants.Count} enabled player entr{(_grants.Count == 1 ? "y" : "ies")}");
    }

    public bool ApplyVerifiedIdentity(ClientSession client)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!client.HasAttachedGameplayAccount
            || !TryNormalizeFriendCode(client.VerifiedFriendCode, out var friendCode)
            || !_grants.TryGetValue(friendCode, out var grant))
        {
            return ClearManagedIdentity(client);
        }

        var changed = client.ConfiguredAdminPermissions != grant.Permissions
            || !string.Equals(client.ServerTitleText, grant.TitleText, StringComparison.Ordinal)
            || client.ServerTitleColorRgb != grant.TitleColorRgb
            || client.ServerTitleRainbow != grant.TitleRainbow;
        client.ConfiguredAdminPermissions = grant.Permissions;
        client.ServerTitleText = grant.TitleText;
        client.ServerTitleColorRgb = grant.TitleColorRgb;
        client.ServerTitleRainbow = grant.TitleRainbow;
        if (changed)
        {
            _log?.Invoke($"[management] applied verified entry friendCode={friendCode} slot={client.Slot} permissions={grant.Permissions}");
        }
        return changed;
    }

    public static bool ClearManagedIdentity(ClientSession client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var changed = client.ConfiguredAdminPermissions != OpenGarrisonServerAdminPermissions.None
            || client.ServerTitleText.Length > 0
            || client.ServerTitleColorRgb != 0
            || client.ServerTitleRainbow;
        client.ConfiguredAdminPermissions = OpenGarrisonServerAdminPermissions.None;
        client.ServerTitleText = string.Empty;
        client.ServerTitleColorRgb = 0;
        client.ServerTitleRainbow = false;
        return changed;
    }

    internal bool TryGetGrant(string? friendCode, out ServerManagedPlayerGrant grant)
    {
        grant = default;
        return TryNormalizeFriendCode(friendCode, out var normalized)
            && _grants.TryGetValue(normalized, out grant);
    }

    internal static bool TryNormalizeFriendCode(string? value, out string friendCode)
    {
        friendCode = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var compact = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character)) compact.Append(char.ToUpperInvariant(character));
        }

        var text = compact.ToString();
        if (text.StartsWith(FriendCodePrefix, StringComparison.Ordinal)) text = text[FriendCodePrefix.Length..];
        if (text.Length is not (8 or 12 or 16) || text.Any(character => !FriendCodeAlphabet.Contains(character))) return false;

        var groups = Enumerable.Range(0, text.Length / 4).Select(index => text.Substring(index * 4, 4));
        friendCode = $"{FriendCodePrefix}-{string.Join('-', groups)}";
        return true;
    }

    private Dictionary<string, ServerManagedPlayerGrant> BuildGrants(IEnumerable<ServerManagedPlayerConfiguration> entries)
    {
        var grants = new Dictionary<string, ServerManagedPlayerGrant>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry is null || !entry.Enabled) continue;
            if (!TryNormalizeFriendCode(entry.FriendCode, out var friendCode))
            {
                _log?.Invoke($"[management] ignored entry with invalid friend code: {entry.FriendCode}");
                continue;
            }
            if (grants.ContainsKey(friendCode))
            {
                _log?.Invoke($"[management] ignored duplicate entry for {friendCode}");
                continue;
            }

            var permissions = ResolvePermissions(entry, friendCode);
            var titleText = NormalizeTitleText(entry.Title?.Text);
            var rainbow = string.Equals(entry.Title?.Color?.Trim(), "rainbow", StringComparison.OrdinalIgnoreCase);
            var color = rainbow ? 0xFFFFFFu : ParseHexColor(entry.Title?.Color, friendCode);
            grants.Add(friendCode, new ServerManagedPlayerGrant(friendCode, permissions, titleText, color, rainbow));
        }
        return grants;
    }

    private OpenGarrisonServerAdminPermissions ResolvePermissions(ServerManagedPlayerConfiguration entry, string friendCode)
    {
        if (entry.Permissions is { Count: > 0 })
        {
            var permissions = OpenGarrisonServerAdminPermissions.None;
            foreach (var value in entry.Permissions)
            {
                if (!Enum.TryParse<OpenGarrisonServerAdminPermissions>(value?.Trim(), true, out var parsed)
                    || parsed == OpenGarrisonServerAdminPermissions.None)
                {
                    _log?.Invoke($"[management] ignored unknown permission '{value}' for {friendCode}");
                    continue;
                }
                permissions |= parsed;
            }
            return permissions & OpenGarrisonServerAdminPermissions.FullAccess;
        }

        return entry.Role?.Trim().ToLowerInvariant() switch
        {
            "owner" or "admin" or "administrator" => OpenGarrisonServerAdminPermissions.FullAccess,
            "moderator" or "mod" => OpenGarrisonServerAdminPermissions.ViewServerState
                | OpenGarrisonServerAdminPermissions.ManagePlayers
                | OpenGarrisonServerAdminPermissions.ManageMatch,
            _ => OpenGarrisonServerAdminPermissions.None,
        };
    }

    private string NormalizeTitleText(string? value)
    {
        var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (Encoding.UTF8.GetByteCount(text) <= Protocol.ProtocolCodec.MaxServerPlayerTitleBytes) return text;
        _log?.Invoke("[management] ignored a player title that exceeds the protocol limit.");
        return string.Empty;
    }

    private uint ParseHexColor(string? value, string friendCode)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.StartsWith('#')) text = text[1..];
        if (text.Length == 3) text = string.Concat(text.Select(character => $"{character}{character}"));
        if (text.Length == 6 && uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var color)) return color;
        if (!string.IsNullOrWhiteSpace(value)) _log?.Invoke($"[management] invalid title color '{value}' for {friendCode}; using #FFFFFF");
        return 0xFFFFFFu;
    }
}
