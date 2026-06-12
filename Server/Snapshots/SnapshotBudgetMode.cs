namespace OpenGarrison.Server;

internal enum SnapshotBudgetMode
{
    Balanced,
    GameplayCriticalUntrimmed,
}

internal static class SnapshotBudgetModeParser
{
    public const string BalancedName = "Balanced";
    public const string GameplayCriticalUntrimmedName = "GameplayCriticalUntrimmed";

    public static SnapshotBudgetMode Parse(string? value, SnapshotBudgetMode fallback = SnapshotBudgetMode.GameplayCriticalUntrimmed)
    {
        return TryParse(value, out var parsed) ? parsed : fallback;
    }

    public static bool TryParse(string? value, out SnapshotBudgetMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = SnapshotBudgetMode.GameplayCriticalUntrimmed;
            return false;
        }

        var normalized = value.Trim()
            .Replace("-", string.Empty, System.StringComparison.Ordinal)
            .Replace("_", string.Empty, System.StringComparison.Ordinal)
            .Replace(" ", string.Empty, System.StringComparison.Ordinal);

        if (string.Equals(normalized, "Balanced", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Budgeted", System.StringComparison.OrdinalIgnoreCase))
        {
            mode = SnapshotBudgetMode.Balanced;
            return true;
        }

        if (string.Equals(normalized, "GameplayCriticalUntrimmed", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "GameplayCritical", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Untrimmed", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Default", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "NoTrim", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "NoTrimming", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "GmlStyle", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "OldGml", System.StringComparison.OrdinalIgnoreCase))
        {
            mode = SnapshotBudgetMode.GameplayCriticalUntrimmed;
            return true;
        }

        mode = SnapshotBudgetMode.GameplayCriticalUntrimmed;
        return false;
    }

    public static string ToConfigString(SnapshotBudgetMode mode)
    {
        return mode == SnapshotBudgetMode.GameplayCriticalUntrimmed
            ? GameplayCriticalUntrimmedName
            : BalancedName;
    }
}
