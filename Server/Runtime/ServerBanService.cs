using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerBanService(
    string path,
    Func<DateTimeOffset>? utcNowGetter = null,
    Action<string>? log = null)
{
    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _utcNowGetter = utcNowGetter ?? (() => DateTimeOffset.UtcNow);
    private readonly Action<string> _log = log ?? (_ => { });

    public OpenGarrisonServerBanActionResult BanIpAddress(
        IPAddress address,
        TimeSpan? duration,
        string reason,
        string bannedBy,
        string? playerName = null)
    {
        return BanNormalizedAddress(
            NormalizeAddress(address),
            NormalizeDuration(duration),
            NormalizeReason(reason),
            NormalizeActor(bannedBy),
            NormalizePlayerName(playerName));
    }

    public OpenGarrisonServerBanActionResult BanIpAddress(
        string ipAddress,
        TimeSpan? duration,
        string reason,
        string bannedBy,
        string? playerName = null)
    {
        if (!TryNormalizeAddress(ipAddress, out var normalizedAddress, out var errorMessage))
        {
            return new OpenGarrisonServerBanActionResult(
                Success: false,
                Address: string.Empty,
                ErrorMessage: errorMessage,
                IsPermanent: false,
                ExpiresUnixTimeSeconds: 0);
        }

        return BanNormalizedAddress(
            normalizedAddress,
            NormalizeDuration(duration),
            NormalizeReason(reason),
            NormalizeActor(bannedBy),
            NormalizePlayerName(playerName));
    }

    public OpenGarrisonServerAddressActionResult UnbanIpAddress(string ipAddress)
    {
        if (!TryNormalizeAddress(ipAddress, out var normalizedAddress, out var errorMessage))
        {
            return new OpenGarrisonServerAddressActionResult(false, string.Empty, errorMessage);
        }

        lock (_gate)
        {
            var now = _utcNowGetter();
            var document = LoadDocument();
            var changed = PruneExpiredEntries(document, now);
            var removed = document.Entries.RemoveAll(entry =>
                string.Equals(entry.Address, normalizedAddress, StringComparison.Ordinal)) > 0;
            if (!removed)
            {
                if (changed)
                {
                    SaveDocument(document);
                }

                return new OpenGarrisonServerAddressActionResult(false, normalizedAddress, "No active ban exists for that IP address.");
            }

            SaveDocument(document);
            _log($"[server] unbanned ip {normalizedAddress}");
            return new OpenGarrisonServerAddressActionResult(true, normalizedAddress, string.Empty);
        }
    }

    public string? GetConnectionDeniedReason(IPEndPoint remoteEndPoint)
    {
        lock (_gate)
        {
            var now = _utcNowGetter();
            var document = LoadDocument();
            var changed = PruneExpiredEntries(document, now);
            var normalizedAddress = NormalizeAddress(remoteEndPoint.Address);
            var entry = document.Entries.FirstOrDefault(candidate =>
                string.Equals(candidate.Address, normalizedAddress, StringComparison.Ordinal));
            if (changed)
            {
                SaveDocument(document);
            }

            return entry is null
                ? null
                : BuildConnectionDeniedReason(entry, now);
        }
    }

    private OpenGarrisonServerBanActionResult BanNormalizedAddress(
        string normalizedAddress,
        TimeSpan? duration,
        string reason,
        string bannedBy,
        string? playerName)
    {
        lock (_gate)
        {
            var now = _utcNowGetter();
            DateTimeOffset? expiresUtc = duration.HasValue ? now + duration.Value : null;
            var document = LoadDocument();
            PruneExpiredEntries(document, now);

            var record = new BanEntryDocument
            {
                Address = normalizedAddress,
                Reason = reason,
                BannedBy = bannedBy,
                PlayerName = playerName ?? string.Empty,
                CreatedUtc = now,
                ExpiresUtc = expiresUtc,
            };

            var existingIndex = document.Entries.FindIndex(entry =>
                string.Equals(entry.Address, normalizedAddress, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                document.Entries[existingIndex] = record;
            }
            else
            {
                document.Entries.Add(record);
            }

            document.Entries.Sort(static (left, right) => string.Compare(left.Address, right.Address, StringComparison.Ordinal));
            SaveDocument(document);

            var durationText = expiresUtc.HasValue
                ? $" until {expiresUtc.Value:yyyy-MM-dd HH:mm:ss 'UTC'}"
                : " permanently";
            var playerSuffix = string.IsNullOrWhiteSpace(playerName) ? string.Empty : $" player=\"{playerName}\"";
            _log($"[server] banned ip {normalizedAddress}{durationText}{playerSuffix} by {bannedBy}: {reason}");

            return new OpenGarrisonServerBanActionResult(
                Success: true,
                Address: normalizedAddress,
                ErrorMessage: string.Empty,
                IsPermanent: !expiresUtc.HasValue,
                ExpiresUnixTimeSeconds: expiresUtc?.ToUnixTimeSeconds() ?? 0);
        }
    }

    private BanDocument LoadDocument()
    {
        return JsonConfigurationFile.LoadOrCreate(path, static () => new BanDocument());
    }

    private void SaveDocument(BanDocument document)
    {
        JsonConfigurationFile.Save(path, document);
    }

    private static bool PruneExpiredEntries(BanDocument document, DateTimeOffset now)
    {
        return document.Entries.RemoveAll(entry =>
            entry.ExpiresUtc.HasValue
            && entry.ExpiresUtc.Value <= now) > 0;
    }

    private static string BuildConnectionDeniedReason(BanEntryDocument entry, DateTimeOffset now)
    {
        var reasonSuffix = string.IsNullOrWhiteSpace(entry.Reason)
            ? string.Empty
            : $" Reason: {entry.Reason}";
        if (!entry.ExpiresUtc.HasValue)
        {
            return "You are banned from this server." + reasonSuffix;
        }

        var expiresUtc = entry.ExpiresUtc.Value;
        var remaining = expiresUtc - now;
        if (remaining <= TimeSpan.Zero)
        {
            return "You are banned from this server." + reasonSuffix;
        }

        return $"You are banned from this server until {expiresUtc:yyyy-MM-dd HH:mm:ss 'UTC'} ({FormatRemaining(remaining)} remaining).{reasonSuffix}";
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalDays >= 1d)
        {
            return $"{Math.Ceiling(remaining.TotalDays):0} day(s)";
        }

        if (remaining.TotalHours >= 1d)
        {
            return $"{Math.Ceiling(remaining.TotalHours):0} hour(s)";
        }

        if (remaining.TotalMinutes >= 1d)
        {
            return $"{Math.Ceiling(remaining.TotalMinutes):0} minute(s)";
        }

        return $"{Math.Ceiling(Math.Max(1d, remaining.TotalSeconds)):0} second(s)";
    }

    private static TimeSpan? NormalizeDuration(TimeSpan? duration)
    {
        return !duration.HasValue || duration.Value <= TimeSpan.Zero
            ? null
            : duration.Value;
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "Banned by admin." : reason.Trim();
    }

    private static string NormalizeActor(string bannedBy)
    {
        return string.IsNullOrWhiteSpace(bannedBy) ? "server" : bannedBy.Trim();
    }

    private static string? NormalizePlayerName(string? playerName)
    {
        return string.IsNullOrWhiteSpace(playerName) ? null : playerName.Trim();
    }

    private static bool TryNormalizeAddress(string ipAddress, out string normalizedAddress, out string errorMessage)
    {
        normalizedAddress = string.Empty;
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            errorMessage = "IP address is required.";
            return false;
        }

        if (!IPAddress.TryParse(ipAddress.Trim(), out var parsedAddress))
        {
            errorMessage = "Invalid IP address.";
            return false;
        }

        normalizedAddress = NormalizeAddress(parsedAddress);
        return true;
    }

    private static string NormalizeAddress(IPAddress address)
    {
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    private sealed class BanDocument
    {
        public int SchemaVersion { get; set; } = 1;

        public List<BanEntryDocument> Entries { get; set; } = [];
    }

    private sealed class BanEntryDocument
    {
        public string Address { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string BannedBy { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset? ExpiresUtc { get; set; }
    }
}
