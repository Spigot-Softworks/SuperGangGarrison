using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class ControlPointMapSettingsMetadata
{
    public const string OverrideInitialCpsPropertyKey = "overrideInitialCps";

    /// <summary>Legacy map metadata keys copied from old maps but not used by OpenGarrison.</summary>
    public static readonly string[] LegacyMetadataStripKeys = ["mplatform"];

    public static bool IsEditableMapMetadataKey(string key)
    {
        return key.Equals("background", StringComparison.OrdinalIgnoreCase)
            || key.Equals("void", StringComparison.OrdinalIgnoreCase)
            || key.Equals(OverrideInitialCpsPropertyKey, StringComparison.OrdinalIgnoreCase)
            || MapGameModeMetadata.IsEditableMapMetadataKey(key)
            || ScrMapSettingsMetadata.IsEditableMapMetadataKey(key);
    }

    public static void StripLegacyMetadataKeys(IDictionary<string, string> metadata)
    {
        for (var index = 0; index < LegacyMetadataStripKeys.Length; index += 1)
        {
            metadata.Remove(LegacyMetadataStripKeys[index]);
        }
    }

    public static bool ParseOverrideInitialCps(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return false;
        }

        return metadata.TryGetValue(OverrideInitialCpsPropertyKey, out var rawValue)
            && IsTruthy(rawValue);
    }

    public static string ToPropertyValue(bool enabled)
    {
        return enabled ? "true" : "false";
    }

    private static bool IsTruthy(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
