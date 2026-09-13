#nullable enable

using System.Text;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed record RegisteredPluginVoteKind(
    string OwnerPluginId,
    string LocalId,
    string GlobalId,
    OpenGarrisonServerVoteRegistration Registration);

/// <summary>
/// Owns the discoverable plugin vote catalog. IDs are owner-scoped, registration is
/// bounded, and entries retain object identity so stale callbacks cannot run after
/// an unload/reload replaces a registration.
/// </summary>
internal sealed class PluginVoteRegistry
{
    private const int MaxRegistrations = 64;
    private const int MaxIdLength = 48;
    private const int MaxOwnerIdLength = 80;
    private const int MaxDisplayNameBytes = 80;
    private const int MaxDescriptionBytes = 200;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly Dictionary<string, RegisteredPluginVoteKind> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<RegisteredPluginVoteKind> GetCatalog()
        => _registrations.Values
            .OrderBy(static entry => entry.Registration.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.GlobalId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool TryRegister(
        string ownerPluginId,
        OpenGarrisonServerVoteRegistration registration,
        out string error)
    {
        error = string.Empty;
        var normalizedOwnerId = ownerPluginId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedOwnerId.Length is 0 or > MaxOwnerIdLength
            || normalizedOwnerId.Any(static ch =>
                !char.IsAsciiLetterOrDigit(ch) && ch is not '.' and not '_' and not '-'))
        {
            error = $"Vote owner plugin IDs must be 1-{MaxOwnerIdLength} ASCII letters, numbers, '.', '_' or '-'.";
            return false;
        }

        ArgumentNullException.ThrowIfNull(registration);
        if (!TryNormalizeId(registration.Id, out var localId))
        {
            error = $"Vote IDs must be 1-{MaxIdLength} lowercase ASCII letters, numbers, '.', '_' or '-'.";
            return false;
        }

        var displayName = registration.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0
            || ContainsControlCharacters(displayName)
            || !TryGetUtf8ByteCount(displayName, out var displayNameBytes)
            || displayNameBytes > MaxDisplayNameBytes)
        {
            error = $"Vote display names must be non-empty, single-line text of at most {MaxDisplayNameBytes} UTF-8 bytes.";
            return false;
        }

        var description = registration.Description?.Trim() ?? string.Empty;
        if (ContainsControlCharacters(description)
            || !TryGetUtf8ByteCount(description, out var descriptionBytes)
            || descriptionBytes > MaxDescriptionBytes)
        {
            error = $"Vote descriptions must be single-line text of at most {MaxDescriptionBytes} UTF-8 bytes.";
            return false;
        }

        if (!Enum.IsDefined(registration.TargetKind))
        {
            error = "Vote target kind is not supported.";
            return false;
        }

        if (registration.Apply is null)
        {
            error = "Vote apply callback is required.";
            return false;
        }

        var globalId = BuildGlobalId(normalizedOwnerId, localId);
        if (_registrations.ContainsKey(globalId))
        {
            error = $"Vote kind \"{localId}\" is already registered by {ownerPluginId}.";
            return false;
        }

        if (_registrations.Count >= MaxRegistrations)
        {
            error = $"The server vote catalog is limited to {MaxRegistrations} plugin vote kinds.";
            return false;
        }

        var normalizedRegistration = registration with
        {
            Id = localId,
            DisplayName = displayName,
            Description = description,
        };
        _registrations.Add(globalId, new RegisteredPluginVoteKind(
            normalizedOwnerId,
            localId,
            globalId,
            normalizedRegistration));
        return true;
    }

    public bool TryGetOwned(string ownerPluginId, string localId, out RegisteredPluginVoteKind registration)
    {
        registration = null!;
        return TryNormalizeId(localId, out var normalizedId)
            && _registrations.TryGetValue(BuildGlobalId(ownerPluginId, normalizedId), out registration!);
    }

    public bool TryResolvePublic(string id, out RegisteredPluginVoteKind registration, out string error)
    {
        registration = null!;
        error = "Unknown plugin vote kind.";
        var trimmed = id?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (_registrations.TryGetValue(trimmed, out registration!))
        {
            return true;
        }

        var matches = _registrations.Values
            .Where(entry => string.Equals(entry.LocalId, trimmed, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (matches.Length == 1)
        {
            registration = matches[0];
            return true;
        }

        error = matches.Length > 1
            ? "Plugin vote ID is ambiguous; use the full owner:id shown in the vote menu."
            : $"Unknown plugin vote kind \"{trimmed}\".";
        return false;
    }

    public bool IsCurrent(RegisteredPluginVoteKind registration)
        => _registrations.TryGetValue(registration.GlobalId, out var current)
            && ReferenceEquals(current, registration);

    public bool RemoveOwner(string ownerPluginId)
    {
        var keys = _registrations
            .Where(entry => string.Equals(entry.Value.OwnerPluginId, ownerPluginId, StringComparison.OrdinalIgnoreCase))
            .Select(static entry => entry.Key)
            .ToArray();
        foreach (var key in keys)
        {
            _registrations.Remove(key);
        }

        return keys.Length > 0;
    }

    private static string BuildGlobalId(string ownerPluginId, string localId)
        => $"{ownerPluginId.Trim().ToLowerInvariant()}:{localId}";

    private static bool ContainsControlCharacters(string value)
        => value.Any(char.IsControl);

    private static bool TryGetUtf8ByteCount(string value, out int byteCount)
    {
        try
        {
            byteCount = StrictUtf8.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            byteCount = 0;
            return false;
        }
    }

    private static bool TryNormalizeId(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length is > 0 and <= MaxIdLength
            && normalized.All(static ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-');
    }
}
