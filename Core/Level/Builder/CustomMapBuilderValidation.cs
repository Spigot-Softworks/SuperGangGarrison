using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenGarrison.Core;

public enum CustomMapBuilderValidationSeverity
{
    Error,
    Warning,
}

public sealed record CustomMapBuilderValidationIssue(
    CustomMapBuilderValidationSeverity Severity,
    string Code,
    string Message);

public sealed record CustomMapBuilderValidationResult(
    CustomMapBuilderGameMode Mode,
    IReadOnlyList<CustomMapBuilderValidationIssue> Issues)
{
    public bool IsValid => !Issues.Any(static issue => issue.Severity == CustomMapBuilderValidationSeverity.Error);
}

public static class CustomMapBuilderValidator
{
    public static CustomMapBuilderValidationResult Validate(
        CustomMapBuilderDocument document,
        CustomMapBuilderGameMode mode = CustomMapBuilderGameMode.Free)
    {
        ArgumentNullException.ThrowIfNull(document);
        var normalized = document.NormalizeForEditing();
        var effectiveMode = mode == CustomMapBuilderGameMode.Free ? InferGameMode(normalized.Entities) : mode;
        var issues = new List<CustomMapBuilderValidationIssue>();
        ValidateRequiredSpawns(normalized.Entities, issues);
        ValidateObjectiveMode(normalized.Entities, effectiveMode, issues);
        ValidateGates(normalized.Entities, issues);
        ValidateMapSolids(normalized, issues);
        ValidateControlPointSettings(normalized, issues);
        return new CustomMapBuilderValidationResult(effectiveMode, issues);
    }

    private static void ValidateControlPointSettings(
        CustomMapBuilderDocument document,
        List<CustomMapBuilderValidationIssue> issues)
    {
        if (!ControlPointMapSettingsMetadata.ParseOverrideInitialCps(document.Metadata))
        {
            return;
        }

        foreach (var entity in document.Entities)
        {
            if (!ControlPointOwnershipResolver.IsControlPointEntity(entity.Type))
            {
                continue;
            }

            var rules = ControlPointLockDependencyMetadata.Parse(entity.Properties);
            if (!ControlPointLockDependencyMetadata.HasConflictingRules(rules))
            {
                continue;
            }

            var index = ControlPointOwnershipResolver.ResolveControlPointIndex(entity);
            AddError(
                issues,
                "cp_lock_rule_conflict",
                $"Control point #{index} cannot use the same control point and team for both Lock when and Unlock when.");
        }
    }

    public static CustomMapBuilderGameMode InferGameMode(IReadOnlyList<CustomMapBuilderEntity> entities)
    {
        for (var index = 0; index < entities.Count; index += 1)
        {
            if (MapLogicScoreTriggerMetadata.IsScoreTriggerEntityType(entities[index].Type))
            {
                return CustomMapBuilderGameMode.Scr;
            }
        }

        if (Count(entities, "GeneratorRed") > 0 || Count(entities, "GeneratorBlue") > 0)
        {
            return CustomMapBuilderGameMode.Generator;
        }

        if (Count(entities, "KothRedControlPoint") > 0 || Count(entities, "KothBlueControlPoint") > 0)
        {
            return CustomMapBuilderGameMode.DualKingOfTheHill;
        }

        if (Count(entities, "KothControlPoint") > 0)
        {
            return CustomMapBuilderGameMode.KingOfTheHill;
        }

        if (Count(entities, "ArenaControlPoint") > 0)
        {
            return CustomMapBuilderGameMode.Arena;
        }

        if (Count(entities, "redintel") > 0 || Count(entities, "blueintel") > 0)
        {
            return CustomMapBuilderGameMode.CaptureTheFlag;
        }

        var controlPoints = CustomMapBuilderEntityNormalization.CountControlPointsNormalized(entities);
        if (controlPoints > 0)
        {
            return Count(entities, "SetupGate") > 0
                ? CustomMapBuilderGameMode.AttackDefenseControlPoint
                : CustomMapBuilderGameMode.ControlPoint;
        }

        return CustomMapBuilderGameMode.Free;
    }

    private static void ValidateRequiredSpawns(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        List<CustomMapBuilderValidationIssue> issues)
    {
        if (!HasBaseTeamSpawn(entities, "red"))
        {
            AddError(issues, "missing_red_spawn", "Every map needs at least one red spawn.");
        }

        if (!HasBaseTeamSpawn(entities, "blue"))
        {
            AddError(issues, "missing_blue_spawn", "Every map needs at least one blue spawn.");
        }
    }

    private static void ValidateObjectiveMode(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        CustomMapBuilderGameMode mode,
        List<CustomMapBuilderValidationIssue> issues)
    {
        switch (mode)
        {
            case CustomMapBuilderGameMode.CaptureTheFlag:
                RequireExactly(entities, issues, "redintel", 1, "ctf_red_intel", "CTF needs exactly one redintel.");
                RequireExactly(entities, issues, "blueintel", 1, "ctf_blue_intel", "CTF needs exactly one blueintel.");
                break;
            case CustomMapBuilderGameMode.ControlPoint:
                RequireControlPoints(entities, issues, minimum: 1, maximum: 5, code: "cp_points", message: "CP needs 1-5 control points.");
                RequireAtLeast(entities, issues, "CapturePoint", 1, "cp_capture_zone", "CP needs at least one CapturePoint zone.");
                break;
            case CustomMapBuilderGameMode.AttackDefenseControlPoint:
                RequireControlPoints(entities, issues, minimum: 1, maximum: 4, code: "adcp_points", message: "A/D CP needs 1-4 control points.");
                RequireAtLeast(entities, issues, "CapturePoint", 1, "adcp_capture_zone", "A/D CP needs at least one CapturePoint zone.");
                RequireAtLeast(entities, issues, "SetupGate", 1, "adcp_setup_gate", "A/D CP needs at least one SetupGate.");
                break;
            case CustomMapBuilderGameMode.KingOfTheHill:
                RequireExactly(entities, issues, "KothControlPoint", 1, "koth_point", "KOTH needs exactly one KothControlPoint.");
                RequireAtLeast(entities, issues, "CapturePoint", 1, "koth_capture_zone", "KOTH needs at least one CapturePoint zone.");
                break;
            case CustomMapBuilderGameMode.DualKingOfTheHill:
                RequireExactly(entities, issues, "KothRedControlPoint", 1, "dkoth_red_point", "DKOTH needs exactly one KothRedControlPoint.");
                RequireExactly(entities, issues, "KothBlueControlPoint", 1, "dkoth_blue_point", "DKOTH needs exactly one KothBlueControlPoint.");
                RequireAtLeast(entities, issues, "CapturePoint", 1, "dkoth_capture_zone", "DKOTH needs at least one CapturePoint zone.");
                break;
            case CustomMapBuilderGameMode.Arena:
                RequireExactly(entities, issues, "ArenaControlPoint", 1, "arena_point", "Arena needs exactly one ArenaControlPoint.");
                RequireAtLeast(entities, issues, "CapturePoint", 1, "arena_capture_zone", "Arena needs at least one CapturePoint zone.");
                break;
            case CustomMapBuilderGameMode.Generator:
                RequireExactly(entities, issues, "GeneratorRed", 1, "gen_red", "Generator mode needs exactly one GeneratorRed.");
                RequireExactly(entities, issues, "GeneratorBlue", 1, "gen_blue", "Generator mode needs exactly one GeneratorBlue.");
                break;
            case CustomMapBuilderGameMode.Scr:
                break;
        }
    }

    private static void ValidateGates(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        List<CustomMapBuilderValidationIssue> issues)
    {
        foreach (var entity in entities)
        {
            if (!IsGate(entity.Type))
            {
                continue;
            }

            if (!TryGetPositiveScale(entity, "xscale", entity.XScale)
                || !TryGetPositiveScale(entity, "yscale", entity.YScale))
            {
                AddError(issues, "invalid_gate_scale", $"{entity.Type} at {entity.X:0},{entity.Y:0} needs positive xscale and yscale.");
            }
        }
    }

    private static void ValidateMapSolids(
        CustomMapBuilderDocument document,
        List<CustomMapBuilderValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(document.WalkmaskImagePath))
        {
            if (!File.Exists(document.WalkmaskImagePath))
            {
                AddError(issues, "missing_walkmask", "Walkmask image path does not exist.");
                return;
            }

            try
            {
                using var image = Image.Load<Rgba32>(document.WalkmaskImagePath);
                for (var y = 0; y < image.Height; y += 1)
                {
                    for (var x = 0; x < image.Width; x += 1)
                    {
                        if (image[x, y].A > 0)
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddError(issues, "invalid_walkmask", $"Walkmask image could not be read: {ex.Message}");
                return;
            }

            AddError(issues, "empty_walkmask", "Walkmask image must contain at least one solid pixel.");
            return;
        }

        if (HasEmbeddedSolid(document.EmbeddedWalkmaskSection))
        {
            return;
        }

        AddError(issues, "missing_solids", "Map needs a walkmask with at least one solid tile.");
    }

    private static bool HasEmbeddedSolid(string embeddedWalkmaskSection)
    {
        var lines = embeddedWalkmaskSection
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n');
        var firstContentLine = 0;
        while (firstContentLine < lines.Length && string.IsNullOrWhiteSpace(lines[firstContentLine]))
        {
            firstContentLine += 1;
        }

        if (firstContentLine + 2 >= lines.Length)
        {
            return false;
        }

        foreach (var character in string.Concat(lines.Skip(firstContentLine + 2)))
        {
            if (character > ' ')
            {
                return true;
            }
        }

        return false;
    }

    private static void RequireControlPoints(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        List<CustomMapBuilderValidationIssue> issues,
        int minimum,
        int maximum,
        string code,
        string message)
    {
        var count = CustomMapBuilderEntityNormalization.CountControlPointsNormalized(entities);
        if (count < minimum || count > maximum)
        {
            AddError(issues, code, message);
        }
    }

    private static void RequireAtLeast(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        List<CustomMapBuilderValidationIssue> issues,
        string type,
        int required,
        string code,
        string message)
    {
        if (Count(entities, type) < required)
        {
            AddError(issues, code, message);
        }
    }

    private static void RequireExactly(
        IReadOnlyList<CustomMapBuilderEntity> entities,
        List<CustomMapBuilderValidationIssue> issues,
        string type,
        int required,
        string code,
        string message)
    {
        if (Count(entities, type) != required)
        {
            AddError(issues, code, message);
        }
    }

    private static bool HasBaseTeamSpawn(IReadOnlyList<CustomMapBuilderEntity> entities, string team)
    {
        foreach (var entity in entities)
        {
            if (CustomMapBuilderEntityNormalization.CountsAsTeamSpawn(entity, team))
            {
                return true;
            }
        }

        return false;
    }

    private static int Count(IReadOnlyList<CustomMapBuilderEntity> entities, string type)
    {
        return entities.Count(entity => entity.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGate(string type)
    {
        return type.Contains("gate", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPositiveScale(CustomMapBuilderEntity entity, string key, float fallback)
    {
        var value = fallback;
        if (entity.Properties.TryGetValue(key, out var text)
            && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
        }

        return float.IsFinite(value) && value > 0f;
    }

    private static void AddError(List<CustomMapBuilderValidationIssue> issues, string code, string message)
    {
        issues.Add(new CustomMapBuilderValidationIssue(CustomMapBuilderValidationSeverity.Error, code, message));
    }
}
