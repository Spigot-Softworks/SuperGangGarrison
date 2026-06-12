using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class MapLogicEntityReferenceList
{
    public const char Separator = '|';

    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var trimmed = value.Trim();
        if (trimmed.IndexOf(Separator) < 0)
        {
            return [trimmed];
        }

        var parts = trimmed.Split(Separator);
        var refs = new List<string>(parts.Length);
        for (var index = 0; index < parts.Length; index += 1)
        {
            var part = parts[index].Trim();
            if (part.Length > 0)
            {
                refs.Add(part);
            }
        }

        return refs;
    }

    public static string Format(IReadOnlyList<string> entityRefs)
    {
        if (entityRefs.Count == 0)
        {
            return string.Empty;
        }

        if (entityRefs.Count == 1)
        {
            return entityRefs[0].Trim();
        }

        return string.Join(Separator, entityRefs);
    }

    public static bool IsEmpty(string? value)
    {
        return Parse(value).Count == 0;
    }

    public static string AppendDistinct(string? value, string entityRef)
    {
        if (string.IsNullOrWhiteSpace(entityRef))
        {
            return value ?? string.Empty;
        }

        var refs = new List<string>(Parse(value));
        for (var index = 0; index < refs.Count; index += 1)
        {
            if (refs[index].Equals(entityRef, StringComparison.OrdinalIgnoreCase))
            {
                return Format(refs);
            }
        }

        refs.Add(entityRef.Trim());
        return Format(refs);
    }

    public static string AppendDistinct(string? value, IEnumerable<string> entityRefs)
    {
        var combined = value ?? string.Empty;
        foreach (var entityRef in entityRefs)
        {
            combined = AppendDistinct(combined, entityRef);
        }

        return combined;
    }

    public static string RemoveAt(string? value, int removeIndex)
    {
        var refs = new List<string>(Parse(value));
        if (removeIndex < 0 || removeIndex >= refs.Count)
        {
            return Format(refs);
        }

        refs.RemoveAt(removeIndex);
        return Format(refs);
    }

    public static string UpgradeStableRefs(
        string? value,
        IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        var refs = Parse(value);
        if (refs.Count == 0)
        {
            return string.Empty;
        }

        var upgraded = new List<string>(refs.Count);
        var changed = false;
        for (var index = 0; index < refs.Count; index += 1)
        {
            var entityRef = refs[index];
            if (!MapLogicEntityReference.TryFindBuilderEntityIndex(entities, entityRef, out var targetIndex))
            {
                upgraded.Add(entityRef);
                continue;
            }

            var stableRef = MapLogicEntityReference.FormatEntityRef(entities[targetIndex]);
            upgraded.Add(stableRef);
            if (!stableRef.Equals(entityRef, StringComparison.OrdinalIgnoreCase))
            {
                changed = true;
            }
        }

        return changed ? Format(upgraded) : value ?? string.Empty;
    }

    public static string RefreshMovedRefs(
        string? value,
        IReadOnlyList<(string OldRef, string NewRef, string MapEntityId)> movedUpdates)
    {
        var refs = Parse(value);
        if (refs.Count == 0 || movedUpdates.Count == 0)
        {
            return value ?? string.Empty;
        }

        var refreshed = new List<string>(refs.Count);
        var changed = false;
        for (var refIndex = 0; refIndex < refs.Count; refIndex += 1)
        {
            var entityRef = refs[refIndex];
            var updatedRef = entityRef;
            for (var updateIndex = 0; updateIndex < movedUpdates.Count; updateIndex += 1)
            {
                var update = movedUpdates[updateIndex];
                if (entityRef.Equals(update.OldRef, StringComparison.OrdinalIgnoreCase)
                    || entityRef.Equals(update.NewRef, StringComparison.OrdinalIgnoreCase))
                {
                    updatedRef = update.NewRef;
                    break;
                }

                if (!MapLogicEntityReference.TryParseEntityRef(
                        entityRef,
                        out _,
                        out _,
                        out _,
                        out var referencedId)
                    || string.IsNullOrWhiteSpace(referencedId)
                    || !referencedId.Equals(update.MapEntityId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                updatedRef = update.NewRef;
                break;
            }

            refreshed.Add(updatedRef);
            if (!updatedRef.Equals(entityRef, StringComparison.OrdinalIgnoreCase))
            {
                changed = true;
            }
        }

        return changed ? Format(refreshed) : value ?? string.Empty;
    }
}
