using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum MapLogicGateType
{
    And,
    Or,
    Xor,
}

/// <summary>
/// Which control point ownership state must hold for a <see cref="MapLogicNodeKind.CpTrigger"/> to output true.
/// </summary>
public enum MapLogicCpTriggerOwnerRequirement
{
    Red,
    Blue,
    Neutral,
    /// <summary>True when the point is owned by either team (not neutral).</summary>
    Owned,
}

public static class MapLogicMetadata
{
    public const string LogicKeyPropertyKey = "logicKey";
    public const string LogicInputPropertyKey = "logicInput";
    public const string LogicInput1PropertyKey = "logicInput1";
    public const string LogicInput2PropertyKey = "logicInput2";
    public const string LogicSignalPropertyKey = "logicSignal";
    public const string LockedWhenLogicPropertyKey = "lockedWhenLogic";
    public const string UnlockedWhenLogicPropertyKey = "unlockedWhenLogic";
    public const string GateTypePropertyKey = "gateType";
    public const string RequiredOwnerPropertyKey = "requiredOwner";
    public const string LinkedControlPointPropertyKey = "linkObjective";
    public const string ActivatorBehaviorPropertyKey = "activatorBehavior";
    public const string ActivatorEntityPropertyKey = "activatorEntity";
    public const string ActivateOnStartPropertyKey = "activateOnStart";
    public const string CountdownSecondsPropertyKey = "countdownSeconds";
    public const string TriggerOnStartPropertyKey = "triggerOnStart";
    public const string CountdownSecondsDefaultPropertyValue = "1";
    public const string NodePriorityPropertyKey = "nodePriority";
    public const string NodePriorityDefaultPropertyValue = "0";
    public const string SignalPriorityPropertyKey = "signalPriority";
    public const int MinNodePriority = 0;
    public const int MaxNodePriority = 100;
    public const int NodePriorityStep = 1;

    public const string CpTriggerEntityType = "logicCpTrigger";
    public const string GateEntityType = "logicGate";
    public const string NotEntityType = "logicNot";
    public const string ActivatorEntityType = "logicActivator";
    public const string TimerEntityType = "logicTimer";
    public const string PlayerTriggerEntityType = PlayerTriggerMetadata.PlayerTriggerEntityType;

    public static bool IsLogicEntityType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        return type.Equals(CpTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(GateEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(NotEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(TimerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(ActivatorEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLogicOutputEntityType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        return type.Equals(CpTriggerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(GateEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(NotEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(TimerEntityType, StringComparison.OrdinalIgnoreCase)
            || type.Equals(PlayerTriggerEntityType, StringComparison.OrdinalIgnoreCase);
    }

    public static float ParseCountdownSeconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 1f;
        }

        return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0f
            ? parsed
            : 1f;
    }

    public static float ParseCountdownSeconds(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return 1f;
        }

        return properties.TryGetValue(CountdownSecondsPropertyKey, out var raw)
            ? ParseCountdownSeconds(raw)
            : 1f;
    }

    public static string ToCountdownSecondsPropertyValue(float seconds)
    {
        return Math.Max(0.001f, seconds).ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static bool ParseTriggerOnStart(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return false;
        }

        return properties.TryGetValue(TriggerOnStartPropertyKey, out var value)
            && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryParseActivatorBehavior(string? value, out MapLogicActivatorBehavior behavior)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Trim().Equals("disable", StringComparison.OrdinalIgnoreCase))
        {
            behavior = MapLogicActivatorBehavior.Disable;
            return true;
        }

        if (value.Trim().Equals("enable", StringComparison.OrdinalIgnoreCase))
        {
            behavior = MapLogicActivatorBehavior.Enable;
            return true;
        }

        behavior = default;
        return false;
    }

    public static string ToActivatorBehaviorPropertyValue(MapLogicActivatorBehavior behavior)
    {
        return behavior == MapLogicActivatorBehavior.Enable ? "enable" : "disable";
    }

    public static string CycleActivatorBehaviorPropertyValue(string current)
    {
        if (!TryParseActivatorBehavior(current, out var behavior))
        {
            behavior = MapLogicActivatorBehavior.Disable;
        }

        return ToActivatorBehaviorPropertyValue(
            behavior == MapLogicActivatorBehavior.Disable
                ? MapLogicActivatorBehavior.Enable
                : MapLogicActivatorBehavior.Disable);
    }

    public static string GetActivatorBehaviorDisplayLabel(string? value)
    {
        return TryParseActivatorBehavior(value, out var behavior)
            ? behavior == MapLogicActivatorBehavior.Enable ? "Enable" : "Disable"
            : "Disable";
    }

    public static int ClampNodePriority(int priority)
    {
        return Math.Clamp(priority, MinNodePriority, MaxNodePriority);
    }

    public static int ParseNodePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MinNodePriority;
        }

        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority)
            ? ClampNodePriority(priority)
            : MinNodePriority;
    }

    public static int ParseNodePriority(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return MinNodePriority;
        }

        if (properties.TryGetValue(NodePriorityPropertyKey, out var nodePriority)
            && !string.IsNullOrWhiteSpace(nodePriority))
        {
            return ParseNodePriority(nodePriority);
        }

        if (properties.TryGetValue(SignalPriorityPropertyKey, out var legacyPriority))
        {
            return ParseNodePriority(legacyPriority);
        }

        return MinNodePriority;
    }

    public static void EnsureNodePriorityProperty(IDictionary<string, string> properties)
    {
        if (properties.ContainsKey(NodePriorityPropertyKey))
        {
            properties.Remove(SignalPriorityPropertyKey);
            return;
        }

        if (properties.TryGetValue(SignalPriorityPropertyKey, out var legacyPriority))
        {
            properties[NodePriorityPropertyKey] = legacyPriority;
            properties.Remove(SignalPriorityPropertyKey);
            return;
        }

        properties[NodePriorityPropertyKey] = NodePriorityDefaultPropertyValue;
    }

    public static int AdjustNodePriority(string? value, int delta)
    {
        return ClampNodePriority(ParseNodePriority(value) + delta);
    }

    public static string ToNodePriorityPropertyValue(int priority)
    {
        return ClampNodePriority(priority).ToString(CultureInfo.InvariantCulture);
    }

    public static string FormatNodePrioritySliderDisplay(string? value)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"< {ParseNodePriority(value)} >");
    }

    public static string GetNodePriorityDisplayLabel(string? value)
    {
        return ParseNodePriority(value).ToString(CultureInfo.InvariantCulture);
    }

    public static string CreateLogicKey()
    {
        return Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];
    }

    public static string EnsureLogicKey(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue(LogicKeyPropertyKey, out var existing)
            && !string.IsNullOrWhiteSpace(existing))
        {
            return existing.Trim();
        }

        return CreateLogicKey();
    }

    public static string FormatLogicRef(string logicKey)
    {
        return string.IsNullOrWhiteSpace(logicKey) ? string.Empty : $"node:{logicKey.Trim()}";
    }

    public static bool TryParseLogicRef(string? value, out string logicKey)
    {
        logicKey = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("node:", StringComparison.OrdinalIgnoreCase))
        {
            logicKey = trimmed[5..].Trim();
            return logicKey.Length > 0;
        }

        logicKey = trimmed;
        return logicKey.Length > 0;
    }

    public static bool TryParseGateType(string? value, out MapLogicGateType gateType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            gateType = MapLogicGateType.And;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "and":
                gateType = MapLogicGateType.And;
                return true;
            case "or":
                gateType = MapLogicGateType.Or;
                return true;
            case "xor":
                gateType = MapLogicGateType.Xor;
                return true;
            default:
                gateType = default;
                return false;
        }
    }

    public static string ToGateTypePropertyValue(MapLogicGateType gateType)
    {
        return gateType switch
        {
            MapLogicGateType.Or => "or",
            MapLogicGateType.Xor => "xor",
            _ => "and",
        };
    }

    public static string CycleGateTypePropertyValue(string current)
    {
        if (!TryParseGateType(current, out var gateType))
        {
            gateType = MapLogicGateType.And;
        }

        var next = (MapLogicGateType)(((int)gateType + 1) % 3);
        return ToGateTypePropertyValue(next);
    }

    public static string GetGateTypeDisplayLabel(string? value)
    {
        return TryParseGateType(value, out var gateType)
            ? gateType.ToString().ToUpperInvariant()
            : "AND";
    }

    public static bool TryParseCpTriggerOwnerRequirement(string? value, out MapLogicCpTriggerOwnerRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            requirement = MapLogicCpTriggerOwnerRequirement.Red;
            return true;
        }

        if (value.Equals("neutral", StringComparison.OrdinalIgnoreCase)
            || value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            requirement = MapLogicCpTriggerOwnerRequirement.Neutral;
            return true;
        }

        if (value.Equals("red", StringComparison.OrdinalIgnoreCase))
        {
            requirement = MapLogicCpTriggerOwnerRequirement.Red;
            return true;
        }

        if (value.Equals("blue", StringComparison.OrdinalIgnoreCase))
        {
            requirement = MapLogicCpTriggerOwnerRequirement.Blue;
            return true;
        }

        if (value.Equals("owned", StringComparison.OrdinalIgnoreCase)
            || value.Equals("notneutral", StringComparison.OrdinalIgnoreCase)
            || value.Equals("not-neutral", StringComparison.OrdinalIgnoreCase)
            || value.Equals("not_neutral", StringComparison.OrdinalIgnoreCase)
            || value.Equals("either", StringComparison.OrdinalIgnoreCase)
            || value.Equals("captured", StringComparison.OrdinalIgnoreCase))
        {
            requirement = MapLogicCpTriggerOwnerRequirement.Owned;
            return true;
        }

        requirement = default;
        return false;
    }

    public static string ToRequiredOwnerPropertyValue(MapLogicCpTriggerOwnerRequirement requirement)
    {
        return requirement switch
        {
            MapLogicCpTriggerOwnerRequirement.Blue => "blue",
            MapLogicCpTriggerOwnerRequirement.Neutral => "neutral",
            MapLogicCpTriggerOwnerRequirement.Owned => "owned",
            _ => "red",
        };
    }

    public static string CycleRequiredOwnerPropertyValue(string current)
    {
        if (!TryParseCpTriggerOwnerRequirement(current, out var requirement))
        {
            requirement = MapLogicCpTriggerOwnerRequirement.Red;
        }

        var next = (MapLogicCpTriggerOwnerRequirement)(((int)requirement + 1) % 4);
        return ToRequiredOwnerPropertyValue(next);
    }

    public static string GetRequiredOwnerDisplayLabel(string? value)
    {
        return TryParseCpTriggerOwnerRequirement(value, out var requirement)
            ? requirement switch
            {
                MapLogicCpTriggerOwnerRequirement.Blue => "Blue",
                MapLogicCpTriggerOwnerRequirement.Neutral => "Neutral",
                MapLogicCpTriggerOwnerRequirement.Owned => "Not neutral",
                _ => "Red",
            }
            : value ?? string.Empty;
    }

    public static int ParseLinkedControlPointIndex(IReadOnlyDictionary<string, string> properties)
    {
        return ForwardSpawnMetadata.ParseLinkedControlPointIndex(properties);
    }

    public static bool EvaluateCpTrigger(
        int linkedControlPointIndex,
        MapLogicCpTriggerOwnerRequirement ownerRequirement,
        IReadOnlyList<ControlPointState> controlPoints)
    {
        if (linkedControlPointIndex < ControlPointIndexMetadata.MinIndex)
        {
            return false;
        }

        if (!TryGetOwner(controlPoints, linkedControlPointIndex, out var owner))
        {
            return false;
        }

        return ownerRequirement switch
        {
            MapLogicCpTriggerOwnerRequirement.Neutral => !owner.HasValue,
            MapLogicCpTriggerOwnerRequirement.Red => owner == PlayerTeam.Red,
            MapLogicCpTriggerOwnerRequirement.Blue => owner == PlayerTeam.Blue,
            MapLogicCpTriggerOwnerRequirement.Owned => owner.HasValue,
            _ => false,
        };
    }

    public static bool EvaluateGate(MapLogicGateType gateType, bool input1, bool input2)
    {
        return gateType switch
        {
            MapLogicGateType.Or => input1 || input2,
            MapLogicGateType.Xor => input1 ^ input2,
            _ => input1 && input2,
        };
    }

    private static bool TryGetOwner(IReadOnlyList<ControlPointState> controlPoints, int controlPointIndex, out PlayerTeam? owner)
    {
        for (var index = 0; index < controlPoints.Count; index += 1)
        {
            var point = controlPoints[index];
            if (!ControlPointMarkerIndex.TryGetIndex(point.Marker, out var logicalIndex)
                || logicalIndex != controlPointIndex)
            {
                continue;
            }

            owner = point.Team;
            return true;
        }

        owner = null;
        return false;
    }
}
