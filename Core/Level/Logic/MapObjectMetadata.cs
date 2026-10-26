namespace OpenGarrison.Core;

/// <summary>Mapper-facing labels. Runtime links continue to use stable entity IDs.</summary>
public static class MapObjectMetadata
{
    public const string NameKey = "objectName";
    public const string GroupsKey = "objectGroups";
    public static string Name(IReadOnlyDictionary<string, string> properties)
        => properties.TryGetValue(NameKey, out var name) ? name.Trim() : string.Empty;
    public static string Groups(IReadOnlyDictionary<string, string> properties)
        => properties.TryGetValue(GroupsKey, out var groups) ? string.Join(", ", ParseGroups(groups)) : string.Empty;
    public static string[] ParseGroups(string? value) => (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string UniqueName(string desired, IEnumerable<CustomMapBuilderEntity> entities)
    {
        if (string.IsNullOrWhiteSpace(desired)) return string.Empty;
        var names = entities.Select(entity => Name(entity.Properties)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var suffix = 2; ; suffix++)
        {
            var candidate = desired.Trim() + "_" + suffix;
            if (!names.Contains(candidate)) return candidate;
        }
    }
}
