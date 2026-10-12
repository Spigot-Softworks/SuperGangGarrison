using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public static class MapGameModeMetadata
{
    public const string GameModePropertyKey = "gameMode";

    public const string ScrPropertyValue = "scr";
    public const string CaptureTheFlagPropertyValue = "ctf";
    public const string ControlPointPropertyValue = "cp";
    public const string AttackDefenseControlPointPropertyValue = "adcp";
    public const string KingOfTheHillPropertyValue = "koth";
    public const string DualKingOfTheHillPropertyValue = "dkoth";
    public const string ArenaPropertyValue = "arena";
    public const string GeneratorPropertyValue = "generator";
    public const string TeamDeathmatchPropertyValue = "tdm";
    public const string ConquestPropertyValue = "cq";

    public static bool IsEditableMapMetadataKey(string key)
    {
        return key.Equals(GameModePropertyKey, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToPropertyValue(GameModeKind mode)
    {
        return mode switch
        {
            GameModeKind.Conquest => ConquestPropertyValue,
            GameModeKind.Scr => ScrPropertyValue,
            GameModeKind.CaptureTheFlag => CaptureTheFlagPropertyValue,
            GameModeKind.ControlPoint => ControlPointPropertyValue,
            GameModeKind.TeamDeathmatch => TeamDeathmatchPropertyValue,
            _ => string.Empty,
        };
    }

    public static string ToPropertyValue(CustomMapBuilderGameMode mode)
    {
        return mode switch
        {
            CustomMapBuilderGameMode.Conquest => ConquestPropertyValue,
            CustomMapBuilderGameMode.Scr => ScrPropertyValue,
            CustomMapBuilderGameMode.CaptureTheFlag => CaptureTheFlagPropertyValue,
            CustomMapBuilderGameMode.ControlPoint => ControlPointPropertyValue,
            CustomMapBuilderGameMode.AttackDefenseControlPoint => AttackDefenseControlPointPropertyValue,
            CustomMapBuilderGameMode.KingOfTheHill => KingOfTheHillPropertyValue,
            CustomMapBuilderGameMode.DualKingOfTheHill => DualKingOfTheHillPropertyValue,
            CustomMapBuilderGameMode.Arena => ArenaPropertyValue,
            CustomMapBuilderGameMode.Generator => GeneratorPropertyValue,
            _ => string.Empty,
        };
    }

    public static bool TryParseGameMode(string? value, out GameModeKind mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = default;
            return false;
        }

        if (value.Equals(ConquestPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = GameModeKind.Conquest;
            return true;
        }

        if (value.Equals(ScrPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = GameModeKind.Scr;
            return true;
        }

        if (value.Equals(CaptureTheFlagPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = GameModeKind.CaptureTheFlag;
            return true;
        }

        if (value.Equals(ControlPointPropertyValue, StringComparison.OrdinalIgnoreCase)
            || value.Equals(AttackDefenseControlPointPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = GameModeKind.ControlPoint;
            return true;
        }

        if (value.Equals(TeamDeathmatchPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = GameModeKind.TeamDeathmatch;
            return true;
        }

        mode = default;
        return false;
    }

    public static bool TryParseBuilderGameMode(string? value, out CustomMapBuilderGameMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = CustomMapBuilderGameMode.Free;
            return false;
        }

        if (value.Equals(ConquestPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.Conquest;
            return true;
        }

        if (value.Equals(ScrPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.Scr;
            return true;
        }

        if (value.Equals(CaptureTheFlagPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.CaptureTheFlag;
            return true;
        }

        if (value.Equals(ControlPointPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.ControlPoint;
            return true;
        }

        if (value.Equals(AttackDefenseControlPointPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.AttackDefenseControlPoint;
            return true;
        }

        if (value.Equals(KingOfTheHillPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.KingOfTheHill;
            return true;
        }

        if (value.Equals(DualKingOfTheHillPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.DualKingOfTheHill;
            return true;
        }

        if (value.Equals(ArenaPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.Arena;
            return true;
        }

        if (value.Equals(GeneratorPropertyValue, StringComparison.OrdinalIgnoreCase))
        {
            mode = CustomMapBuilderGameMode.Generator;
            return true;
        }

        mode = CustomMapBuilderGameMode.Free;
        return false;
    }

    public static GameModeKind? TryReadGameMode(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null
            || !metadata.TryGetValue(GameModePropertyKey, out var rawValue)
            || !TryParseGameMode(rawValue, out var mode))
        {
            return null;
        }

        return mode;
    }

    public static CustomMapBuilderGameMode ResolveBuilderGameMode(
        IReadOnlyDictionary<string, string>? metadata,
        IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        return ResolveBuilderGameMode(metadata, entities, mapName: null);
    }

    public static CustomMapBuilderGameMode ResolveBuilderGameMode(
        IReadOnlyDictionary<string, string>? metadata,
        IReadOnlyList<CustomMapBuilderEntity> entities,
        string? mapName)
    {
        if (metadata is not null
            && metadata.TryGetValue(GameModePropertyKey, out var rawValue)
            && TryParseBuilderGameMode(rawValue, out var explicitMode))
        {
            return explicitMode;
        }

        return mapName?.StartsWith("cq_", StringComparison.OrdinalIgnoreCase) == true
            ? CustomMapBuilderGameMode.Conquest
            : CustomMapBuilderValidator.InferGameMode(entities);
    }
}

