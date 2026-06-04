#nullable enable

using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Client;

internal enum HostSetupServerCvarValueType
{
    Integer,
    Float,
    Boolean,
}

internal enum HostSetupCvarEditorKind
{
    Toggle,
    Stepped,
    NumericText,
}

internal sealed class HostSetupServerCvarDefinition
{
    public required string Name { get; init; }

    public required string Label { get; init; }

    public HostSetupCvarEditorKind EditorKind { get; init; }

    public HostSetupServerCvarValueType ValueType { get; init; }

    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    public double Step { get; init; } = 1d;

    public string? HostSettingKey { get; init; }
}

internal static class HostSetupServerCvarCatalog
{
    private static readonly string[] ExcludedNames =
    [
        "sv_special_abilities",
        "sv_secondaryabilities",
        "sv_secondary_abilities",
        "sv_map",
        "sv_rcon_password",
        "sv_maxplayers",
    ];

    private static readonly HostSetupServerCvarDefinition[] Definitions =
    [
        Def("sv_timelimit", "Time Limit (min)", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Integer, 1, 255, hostSettingKey: nameof(OpenGarrisonHostSettings.TimeLimitMinutes)),
        Def("sv_caplimit", "Cap Limit", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Integer, 1, 255, hostSettingKey: nameof(OpenGarrisonHostSettings.CapLimit)),
        Def("sv_respawnseconds", "Respawn Time (sec)", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Integer, 0, 255, hostSettingKey: nameof(OpenGarrisonHostSettings.RespawnSeconds)),
        Def("sv_autobalance", "Auto-balance", HostSetupCvarEditorKind.Toggle, HostSetupServerCvarValueType.Boolean, hostSettingKey: nameof(OpenGarrisonHostSettings.AutoBalanceEnabled)),
        Def("sv_specialabilities", "Special Abilities", HostSetupCvarEditorKind.Toggle, HostSetupServerCvarValueType.Boolean, hostSettingKey: nameof(OpenGarrisonHostSettings.SecondaryAbilitiesEnabled)),
        Def("sv_randomspread", "Random Spread", HostSetupCvarEditorKind.Toggle, HostSetupServerCvarValueType.Boolean, hostSettingKey: nameof(OpenGarrisonHostSettings.RandomSpreadEnabled)),
        Def("sv_sniper_aim_indicator", "Sniper Aim Indicator", HostSetupCvarEditorKind.Toggle, HostSetupServerCvarValueType.Boolean, hostSettingKey: nameof(OpenGarrisonHostSettings.SniperAimIndicatorEnabled)),
        Def("sv_competitive_readyup", "Competitive Ready-up", HostSetupCvarEditorKind.Toggle, HostSetupServerCvarValueType.Boolean, hostSettingKey: nameof(OpenGarrisonHostSettings.CompetitiveReadyUpEnabled)),
        Def("sv_competitive_setup_seconds", "Competitive Setup (sec)", HostSetupCvarEditorKind.Stepped, HostSetupServerCvarValueType.Integer, 0, 120, step: 1, hostSettingKey: nameof(OpenGarrisonHostSettings.CompetitiveSetupSeconds)),
        Def("sv_roundendff", "Round-end FF", HostSetupCvarEditorKind.Toggle, HostSetupServerCvarValueType.Boolean, hostSettingKey: nameof(OpenGarrisonHostSettings.RoundEndFriendlyFireEnabled)),
        Def("sv_bot_autofill", "Bot Autofill", HostSetupCvarEditorKind.Toggle, HostSetupServerCvarValueType.Boolean, hostSettingKey: nameof(OpenGarrisonHostSettings.BotAutofillEnabled)),
        Def("sv_bot_autofill_min_players", "Bot Autofill Min Players", HostSetupCvarEditorKind.Stepped, HostSetupServerCvarValueType.Integer, 0, 24, step: 1, hostSettingKey: nameof(OpenGarrisonHostSettings.BotAutofillMinPlayers)),
        Def("sv_bot_autofill_per_team", "Bot Autofill Per Team", HostSetupCvarEditorKind.Stepped, HostSetupServerCvarValueType.Integer, 0, 12, step: 1, hostSettingKey: nameof(OpenGarrisonHostSettings.BotAutofillPerTeam)),
        Def("sv_tickrate", "Tick Rate", HostSetupCvarEditorKind.Stepped, HostSetupServerCvarValueType.Integer, 10, 120, step: 1, hostSettingKey: nameof(OpenGarrisonHostSettings.TickRate)),
        Def("sv_player_scale", "Player Scale", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Float, PlayerEntity.MinPlayerScale, PlayerEntity.MaxPlayerScale, hostSettingKey: nameof(OpenGarrisonHostSettings.PlayerScale)),
        Def("sv_map_scale", "Map Scale", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Float, 0.25, 4, hostSettingKey: nameof(OpenGarrisonHostSettings.MapScale)),
        Def("sv_movement_speed_scale", "Movement Speed Scale", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Float, 0.1, 4, hostSettingKey: nameof(OpenGarrisonHostSettings.MovementSpeedScale)),
        Def("sv_projectile_speed_scale", "Projectile Speed Scale", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Float, 0.1, 4, hostSettingKey: nameof(OpenGarrisonHostSettings.ProjectileSpeedScale)),
        Def("sv_damage_scale", "Damage Scale", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Float, 0, 10, hostSettingKey: nameof(OpenGarrisonHostSettings.DamageScale)),
        Def("sv_gravity_scale", "Gravity Scale", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Float, 0, 4, hostSettingKey: nameof(OpenGarrisonHostSettings.GravityScale)),
        Def("sv_horizontal_speed_clamp", "Horizontal Speed Clamp", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Float, 1, 60, hostSettingKey: nameof(OpenGarrisonHostSettings.HorizontalSpeedClampPerTick)),
        Def("sv_vertical_speed_clamp", "Vertical Speed Clamp", HostSetupCvarEditorKind.NumericText, HostSetupServerCvarValueType.Float, 1, 60, hostSettingKey: nameof(OpenGarrisonHostSettings.VerticalSpeedClampPerTick)),
    ];

    public static IReadOnlyList<HostSetupServerCvarDefinition> AdvancedDefinitions { get; } = Definitions;

    public static int BasicOptionCount => 5;

    public static bool TryGetDefinition(string name, out HostSetupServerCvarDefinition definition)
    {
        foreach (var entry in Definitions)
        {
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                definition = entry;
                return true;
            }
        }

        definition = null!;
        return false;
    }

    public static bool TryGetDefinitionByHostSettingKey(string hostSettingKey, out HostSetupServerCvarDefinition definition)
    {
        foreach (var entry in Definitions)
        {
            if (string.Equals(entry.HostSettingKey, hostSettingKey, StringComparison.Ordinal))
            {
                definition = entry;
                return true;
            }
        }

        definition = null!;
        return false;
    }

    public static string FormatDisplayValue(HostSetupServerCvarDefinition definition, string rawValue)
    {
        if (definition.EditorKind == HostSetupCvarEditorKind.Toggle)
        {
            return ParseBool(rawValue) ? "On" : "Off";
        }

        return rawValue;
    }

    public static string StepValue(HostSetupServerCvarDefinition definition, string rawValue, int direction)
    {
        if (definition.EditorKind != HostSetupCvarEditorKind.Stepped)
        {
            return rawValue;
        }

        if (definition.ValueType == HostSetupServerCvarValueType.Integer)
        {
            var current = int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int)Math.Round(definition.Minimum ?? 0);
            var next = current + (int)Math.Round(definition.Step) * direction;
            if (definition.Minimum.HasValue)
            {
                next = Math.Max((int)Math.Round(definition.Minimum.Value), next);
            }

            if (definition.Maximum.HasValue)
            {
                next = Math.Min((int)Math.Round(definition.Maximum.Value), next);
            }

            return next.ToString(CultureInfo.InvariantCulture);
        }

        var currentFloat = double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFloat)
            ? parsedFloat
            : definition.Minimum ?? 0;
        var nextFloat = currentFloat + (definition.Step * direction);
        if (definition.Minimum.HasValue)
        {
            nextFloat = Math.Max(definition.Minimum.Value, nextFloat);
        }

        if (definition.Maximum.HasValue)
        {
            nextFloat = Math.Min(definition.Maximum.Value, nextFloat);
        }

        return nextFloat.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static string ToggleValue(string rawValue)
    {
        return ParseBool(rawValue) ? "0" : "1";
    }

    public static bool IsValidInputCharacter(HostSetupServerCvarDefinition definition, char character)
    {
        if (char.IsControl(character))
        {
            return false;
        }

        return definition.ValueType switch
        {
            HostSetupServerCvarValueType.Integer => char.IsDigit(character),
            HostSetupServerCvarValueType.Float => char.IsDigit(character) || character is '.' or ',',
            _ => true,
        };
    }

    public static bool TryNormalizeInput(HostSetupServerCvarDefinition definition, string text, out string normalized)
    {
        normalized = text.Trim();
        if (normalized.Length == 0)
        {
            return false;
        }

        if (definition.ValueType == HostSetupServerCvarValueType.Float)
        {
            normalized = normalized.Replace(',', '.');
        }

        if (definition.EditorKind == HostSetupCvarEditorKind.Toggle)
        {
            normalized = ParseBool(normalized) ? "1" : "0";
            return true;
        }

        if (definition.ValueType == HostSetupServerCvarValueType.Integer)
        {
            if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            if (definition.Minimum.HasValue)
            {
                parsed = Math.Max((int)Math.Round(definition.Minimum.Value), parsed);
            }

            if (definition.Maximum.HasValue)
            {
                parsed = Math.Min((int)Math.Round(definition.Maximum.Value), parsed);
            }

            normalized = parsed.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (definition.ValueType == HostSetupServerCvarValueType.Float)
        {
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            if (definition.Minimum.HasValue)
            {
                parsed = Math.Max(definition.Minimum.Value, parsed);
            }

            if (definition.Maximum.HasValue)
            {
                parsed = Math.Min(definition.Maximum.Value, parsed);
            }

            normalized = parsed.ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }

        return true;
    }

    private static HostSetupServerCvarDefinition Def(
        string name,
        string label,
        HostSetupCvarEditorKind editorKind,
        HostSetupServerCvarValueType valueType,
        double? minimum = null,
        double? maximum = null,
        double step = 1,
        string? hostSettingKey = null)
    {
        if (Array.IndexOf(ExcludedNames, name) >= 0)
        {
            throw new InvalidOperationException($"Excluded cvar {name} should not be registered.");
        }

        return new HostSetupServerCvarDefinition
        {
            Name = name,
            Label = label,
            EditorKind = editorKind,
            ValueType = valueType,
            Minimum = minimum,
            Maximum = maximum,
            Step = step,
            HostSettingKey = hostSettingKey,
        };
    }

    private static bool ParseBool(string value)
    {
        return value.Trim() switch
        {
            "1" or "true" or "True" or "on" or "On" or "yes" or "Yes" => true,
            _ => false,
        };
    }
}
