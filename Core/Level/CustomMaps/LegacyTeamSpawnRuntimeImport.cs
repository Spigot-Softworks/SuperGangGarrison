using System;
using System.Globalization;

namespace OpenGarrison.Core;

/// <summary>
/// Runtime import rules for legacy GG2 team spawn entity types (redspawn, bluespawn, redspawnN, bluespawnN).
/// </summary>
public static class LegacyTeamSpawnRuntimeImport
{
    public static bool TryCreateSpawnPoint(string entityType, float x, float y, out LegacyTeamSpawnImportResult result)
    {
        result = default;
        var type = entityType.Trim();
        if (type.Equals("redspawn", StringComparison.OrdinalIgnoreCase))
        {
            result = new LegacyTeamSpawnImportResult(PlayerTeam.Red, new SpawnPoint(x, y));
            return true;
        }

        if (type.Equals("bluespawn", StringComparison.OrdinalIgnoreCase))
        {
            result = new LegacyTeamSpawnImportResult(PlayerTeam.Blue, new SpawnPoint(x, y));
            return true;
        }

        if (type.StartsWith("redspawn", StringComparison.OrdinalIgnoreCase) && type.Length > "redspawn".Length)
        {
            var objectiveIndex = ParseTrailingIndex(type, "redspawn");
            if (objectiveIndex <= 0)
            {
                return false;
            }

            result = new LegacyTeamSpawnImportResult(
                PlayerTeam.Red,
                CreateForwardSpawnPoint(x, y, objectiveIndex, objectiveIndex));
            return true;
        }

        if (type.StartsWith("bluespawn", StringComparison.OrdinalIgnoreCase) && type.Length > "bluespawn".Length)
        {
            var objectiveIndex = ParseTrailingIndex(type, "bluespawn");
            if (objectiveIndex <= 0)
            {
                return false;
            }

            result = new LegacyTeamSpawnImportResult(
                PlayerTeam.Blue,
                CreateForwardSpawnPoint(x, y, objectiveIndex, objectiveIndex));
            return true;
        }

        return false;
    }

    public static bool IsLegacyTeamSpawnType(string entityType)
    {
        var type = entityType.Trim();
        return type.Equals("redspawn", StringComparison.OrdinalIgnoreCase)
            || type.Equals("bluespawn", StringComparison.OrdinalIgnoreCase)
            || (type.StartsWith("redspawn", StringComparison.OrdinalIgnoreCase) && type.Length > "redspawn".Length)
            || (type.StartsWith("bluespawn", StringComparison.OrdinalIgnoreCase) && type.Length > "bluespawn".Length);
    }

    private static SpawnPoint CreateForwardSpawnPoint(float x, float y, int linkedControlPointIndex, int priority)
    {
        return new SpawnPoint(
            x,
            y,
            SpawnPointRole.Forward,
            linkedControlPointIndex,
            ForwardSpawnUseCondition.ObjectiveOwnedByTeam,
            ForwardSpawnPriorityMetadata.ClampPriority(priority));
    }

    private static int ParseTrailingIndex(string type, string prefix)
    {
        var suffix = type[prefix.Length..];
        return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : 0;
    }
}

public readonly record struct LegacyTeamSpawnImportResult(PlayerTeam Team, SpawnPoint SpawnPoint);
