using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public readonly record struct DirectionalWallConfiguration(
    DirectionalWallPassDirection PassDirection,
    DirectionalWallAffectSetting Players,
    DirectionalWallAffectSetting Projectiles)
{
    public const float DefaultWidth = BarrierConfiguration.DefaultWidth;
    public const float DefaultHeight = BarrierConfiguration.DefaultHeight;
    public const float MinExtent = BarrierConfiguration.MinExtent;

    public const string PassDirectionPropertyKey = "passDirection";
    public const string PlayersPropertyKey = "players";
    public const string ProjectilesPropertyKey = "projectiles";

    public const string PassDirectionRightValue = "right";
    public const string PassDirectionLeftValue = "left";
    public const string PassDirectionUpValue = "up";
    public const string PassDirectionDownValue = "down";
    public const string AffectValue = "affect";
    public const string IgnoreValue = "ignore";

    public static DirectionalWallConfiguration Default => new(
        DirectionalWallPassDirection.Right,
        DirectionalWallAffectSetting.Affect,
        DirectionalWallAffectSetting.Ignore);

    public static DirectionalWallConfiguration FromProperties(IReadOnlyDictionary<string, string>? properties)
    {
        properties = BarrierLegacyPropertyMigration.EnsureModernDirectionalWallProperties(properties);
        properties ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return new DirectionalWallConfiguration(
            ParsePassDirection(GetProperty(properties, PassDirectionPropertyKey, PassDirectionRightValue)),
            ParseAffect(GetProperty(properties, PlayersPropertyKey, AffectValue)),
            ParseAffect(GetProperty(properties, ProjectilesPropertyKey, IgnoreValue)));
    }

    public static RoomObjectMarker CreateMarker(
        float x,
        float y,
        float xScale,
        float yScale,
        in DirectionalWallConfiguration configuration)
    {
        var (width, height) = BarrierConfiguration.ResolveDimensions(xScale, yScale, configuration.UsesFloorShape);
        return new RoomObjectMarker(
            RoomObjectType.DirectionalWall,
            x,
            y,
            width,
            height,
            "sprite45",
            null,
            "directionalWall",
            Value: 0f,
            InitialOwnership: ControlPointInitialOwnership.ModeDefault,
            Barrier: default,
            DirectionalWall: configuration);
    }

    public bool AffectsPlayers => Players == DirectionalWallAffectSetting.Affect;

    public bool AffectsProjectiles => Projectiles == DirectionalWallAffectSetting.Affect;

    public bool UsesFloorShape => UsesFloorShapeForPassDirection(PassDirection);

    public static bool UsesFloorShapeForPassDirection(DirectionalWallPassDirection passDirection)
    {
        return passDirection is DirectionalWallPassDirection.Up or DirectionalWallPassDirection.Down;
    }

    public Dictionary<string, string> ToProperties()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PassDirectionPropertyKey] = ToPassDirectionValue(PassDirection),
            [PlayersPropertyKey] = ToAffectValue(Players),
            [ProjectilesPropertyKey] = ToAffectValue(Projectiles),
            ["xscale"] = "1",
            ["yscale"] = "1",
        };
    }

    public static string CyclePassDirectionValue(string current)
    {
        return ParsePassDirection(current) switch
        {
            DirectionalWallPassDirection.Right => PassDirectionLeftValue,
            DirectionalWallPassDirection.Left => PassDirectionUpValue,
            DirectionalWallPassDirection.Up => PassDirectionDownValue,
            _ => PassDirectionRightValue,
        };
    }

    public static string CycleAffectPropertyValue(string current)
    {
        return ParseAffect(current) == DirectionalWallAffectSetting.Affect ? IgnoreValue : AffectValue;
    }

    public static string GetPassDirectionDisplayLabel(DirectionalWallPassDirection direction)
    {
        return direction switch
        {
            DirectionalWallPassDirection.Left => "Left",
            DirectionalWallPassDirection.Up => "Up",
            DirectionalWallPassDirection.Down => "Down",
            _ => "Right",
        };
    }

    public static string GetPassDirectionDisplayLabel(string? value)
    {
        return GetPassDirectionDisplayLabel(ParsePassDirection(value));
    }

    public static string GetAffectDisplayLabel(DirectionalWallAffectSetting setting)
    {
        return setting == DirectionalWallAffectSetting.Affect ? "Affect" : "Ignore";
    }

    public static string GetAffectDisplayLabel(string? value)
    {
        return GetAffectDisplayLabel(ParseAffect(value));
    }

    private static string ToPassDirectionValue(DirectionalWallPassDirection direction)
    {
        return direction switch
        {
            DirectionalWallPassDirection.Left => PassDirectionLeftValue,
            DirectionalWallPassDirection.Up => PassDirectionUpValue,
            DirectionalWallPassDirection.Down => PassDirectionDownValue,
            _ => PassDirectionRightValue,
        };
    }

    private static string ToAffectValue(DirectionalWallAffectSetting setting)
    {
        return setting == DirectionalWallAffectSetting.Affect ? AffectValue : IgnoreValue;
    }

    private static DirectionalWallPassDirection ParsePassDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DirectionalWallPassDirection.Right;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "left" => DirectionalWallPassDirection.Left,
            "up" => DirectionalWallPassDirection.Up,
            "down" => DirectionalWallPassDirection.Down,
            _ => DirectionalWallPassDirection.Right,
        };
    }

    private static DirectionalWallAffectSetting ParseAffect(string? value)
    {
        return value?.Trim().Equals(AffectValue, StringComparison.OrdinalIgnoreCase) == true
            ? DirectionalWallAffectSetting.Affect
            : DirectionalWallAffectSetting.Ignore;
    }

    private static string GetProperty(IReadOnlyDictionary<string, string> properties, string key, string fallback)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }
}
