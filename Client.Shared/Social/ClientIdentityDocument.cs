#nullable enable

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public sealed class ClientIdentityDocument
{
    public const string DefaultFileName = "client-identity.json";

    private const string FriendCodePrefix = "OG2";
    private const string FriendCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string FriendCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public PlayerCardProfile PlayerCard { get; set; } = PlayerCardProfile.CreateDefault();

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool IsPersistent { get; private set; }

    public static ClientIdentityDocument LoadOrCreate(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return CreateNew(isPersistent: false);
        }

        var resolvedPath = path ?? RuntimePaths.GetUserDataPath(DefaultFileName);
        if (File.Exists(resolvedPath))
        {
            try
            {
                var document = JsonConfigurationFile.LoadOrCreate<ClientIdentityDocument>(resolvedPath);
                if (document.IsUsable())
                {
                    document.FriendCode = NormalizeFriendCodeForStorage(document.FriendCode);
                    document.PlayerCard = PlayerCardProfile.Sanitize(document.PlayerCard);
                    document.IsPersistent = true;
                    document.Save(resolvedPath);
                    return document;
                }
            }
            catch
            {
            }
        }

        var created = CreateNew(isPersistent: true);
        created.Save(resolvedPath);
        return created;
    }

    public void Save(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var resolvedPath = path ?? RuntimePaths.GetUserDataPath(DefaultFileName);
        JsonConfigurationFile.Save(resolvedPath, this);
    }

    public static bool TryNormalizeFriendCode(string? value, out string friendCode)
    {
        friendCode = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var compact = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                compact.Append(char.ToUpperInvariant(character));
            }
        }

        var text = compact.ToString();
        if (text.StartsWith(FriendCodePrefix, StringComparison.Ordinal))
        {
            text = text[FriendCodePrefix.Length..];
        }

        if (text.Length is < 8 or > 16)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (!FriendCodeAlphabet.Contains(character))
            {
                return false;
            }
        }

        friendCode = FormatFriendCode(text);
        return true;
    }

    private static ClientIdentityDocument CreateNew(bool isPersistent)
    {
        var document = new ClientIdentityDocument
        {
            ClientId = Guid.NewGuid().ToString("N"),
            ClientSecret = CreateSecret(),
            FriendCode = CreateFriendCode(),
            PlayerCard = PlayerCardProfile.CreateDefault(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsPersistent = isPersistent,
        };
        return document;
    }

    private static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string CreateFriendCode()
    {
        Span<char> code = stackalloc char[12];
        for (var index = 0; index < code.Length; index += 1)
        {
            code[index] = FriendCodeAlphabet[RandomNumberGenerator.GetInt32(FriendCodeAlphabet.Length)];
        }

        return FormatFriendCode(new string(code));
    }

    private static string FormatFriendCode(string compactSuffix)
    {
        var groups = new List<string>();
        for (var index = 0; index < compactSuffix.Length; index += 4)
        {
            var length = Math.Min(4, compactSuffix.Length - index);
            groups.Add(compactSuffix.Substring(index, length));
        }

        return $"{FriendCodePrefix}-{string.Join('-', groups)}";
    }

    private static string NormalizeFriendCodeForStorage(string value)
    {
        return TryNormalizeFriendCode(value, out var normalized) ? normalized : CreateFriendCode();
    }

    private bool IsUsable()
    {
        return !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret)
            && TryNormalizeFriendCode(FriendCode, out _);
    }
}
