using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private Dictionary<string, GameplayReplicatedStateEntry> ReplicatedStateEntries { get; } = new(StringComparer.Ordinal);

    public void SetDisplayName(string? displayName)
    {
        DisplayName = SanitizeDisplayName(displayName);
    }

    public IReadOnlyList<GameplayReplicatedStateEntry> GetReplicatedStateEntries()
    {
        return ReplicatedStateEntries.Values
            .OrderBy(static entry => entry.OwnerId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGetReplicatedStateInt(string ownerId, string key, out int value)
    {
        if (TryGetReplicatedState(ownerId, key, GameplayReplicatedStateValueKind.Whole, out var entry))
        {
            value = entry.IntValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetReplicatedStateFloat(string ownerId, string key, out float value)
    {
        if (TryGetReplicatedState(ownerId, key, GameplayReplicatedStateValueKind.Scalar, out var entry))
        {
            value = entry.FloatValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetReplicatedStateBool(string ownerId, string key, out bool value)
    {
        if (TryGetReplicatedState(ownerId, key, GameplayReplicatedStateValueKind.Toggle, out var entry))
        {
            value = entry.BoolValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool SetReplicatedStateInt(string ownerId, string key, int value)
    {
        return SetReplicatedState(new GameplayReplicatedStateEntry(ownerId, key, GameplayReplicatedStateValueKind.Whole, IntValue: value));
    }

    public bool SetReplicatedStateFloat(string ownerId, string key, float value)
    {
        return SetReplicatedState(new GameplayReplicatedStateEntry(ownerId, key, GameplayReplicatedStateValueKind.Scalar, FloatValue: value));
    }

    public bool SetReplicatedStateBool(string ownerId, string key, bool value)
    {
        return SetReplicatedState(new GameplayReplicatedStateEntry(ownerId, key, GameplayReplicatedStateValueKind.Toggle, BoolValue: value));
    }

    public bool ClearReplicatedState(string ownerId, string key)
    {
        return TryCreateReplicatedStateDictionaryKey(ownerId, key, out var dictionaryKey)
            && ReplicatedStateEntries.Remove(dictionaryKey);
    }

    internal void ReplaceReplicatedStateEntries(IEnumerable<GameplayReplicatedStateEntry> entries)
    {
        ReplicatedStateEntries.Clear();
        foreach (var entry in entries)
        {
            if (ReplicatedStateEntries.Count >= MaxReplicatedStateEntries)
            {
                break;
            }

            var normalizedEntry = NormalizeReplicatedStateEntry(entry);
            if (normalizedEntry is null)
            {
                continue;
            }

            ReplicatedStateEntries[CreateReplicatedStateDictionaryKey(normalizedEntry.OwnerId, normalizedEntry.Key)] = normalizedEntry;
        }
    }

    private bool SetReplicatedState(GameplayReplicatedStateEntry entry)
    {
        var normalizedEntry = NormalizeReplicatedStateEntry(entry);
        if (normalizedEntry is null)
        {
            return false;
        }

        var dictionaryKey = CreateReplicatedStateDictionaryKey(normalizedEntry.OwnerId, normalizedEntry.Key);
        if (!ReplicatedStateEntries.ContainsKey(dictionaryKey)
            && ReplicatedStateEntries.Count >= MaxReplicatedStateEntries)
        {
            return false;
        }

        ReplicatedStateEntries[dictionaryKey] = normalizedEntry;
        return true;
    }

    private bool TryGetReplicatedState(string ownerId, string key, GameplayReplicatedStateValueKind expectedKind, out GameplayReplicatedStateEntry entry)
    {
        if (TryCreateReplicatedStateDictionaryKey(ownerId, key, out var dictionaryKey)
            && ReplicatedStateEntries.TryGetValue(dictionaryKey, out entry!)
            && entry.Kind == expectedKind)
        {
            return true;
        }

        entry = null!;
        return false;
    }

    private static GameplayReplicatedStateEntry? NormalizeReplicatedStateEntry(GameplayReplicatedStateEntry entry)
    {
        if (!GameplayReplicatedStateContract.TryNormalizeIdentifier(entry.OwnerId, out var normalizedOwnerId)
            || !GameplayReplicatedStateContract.TryNormalizeIdentifier(entry.Key, out var normalizedKey))
        {
            return null;
        }

        return entry with
        {
            OwnerId = normalizedOwnerId,
            Key = normalizedKey,
        };
    }

    private static bool TryCreateReplicatedStateDictionaryKey(string ownerId, string key, out string dictionaryKey)
    {
        dictionaryKey = string.Empty;
        if (!GameplayReplicatedStateContract.TryNormalizeIdentifier(ownerId, out var normalizedOwnerId)
            || !GameplayReplicatedStateContract.TryNormalizeIdentifier(key, out var normalizedKey))
        {
            return false;
        }

        dictionaryKey = CreateReplicatedStateDictionaryKey(normalizedOwnerId, normalizedKey);
        return true;
    }

    private static string CreateReplicatedStateDictionaryKey(string ownerId, string key)
    {
        return string.Concat(ownerId, "::", key);
    }

    private static string SanitizeDisplayName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return DefaultDisplayName;
        }

        var sanitized = displayName.Replace("#", string.Empty);
        if (sanitized.Length == 0)
        {
            return DefaultDisplayName;
        }

        return sanitized.Length > MaxDisplayNameLength
            ? sanitized[..MaxDisplayNameLength]
            : sanitized;
    }
}
