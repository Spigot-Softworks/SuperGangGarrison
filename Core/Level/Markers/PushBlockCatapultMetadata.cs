using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum PushBlockDirection
{
    Up,
    Down,
    Left,
    Right,
}

public static class PushBlockMetadata
{
    public const string EntityType = "pushBlock";
    public const string DirectionPropertyKey = "direction";
    public const string SpeedPropertyKey = "speed";
    public const string UpValue = "up";
    public const string DownValue = "down";
    public const string LeftValue = "left";
    public const string RightValue = "right";
    public const float DefaultPushSpeedPerTick = 5f;
    public const string DefaultProperties = "xscale=1;yscale=1;direction=up;speed=5";

    public static bool IsPushBlockEntityType(string type) =>
        type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);

    public static PushBlockDirection ParseDirection(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, DirectionPropertyKey, UpValue)
            : UpValue;
        return ParseDirection(value);
    }

    public static PushBlockDirection ParseDirection(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            DownValue => PushBlockDirection.Down,
            LeftValue => PushBlockDirection.Left,
            RightValue => PushBlockDirection.Right,
            _ => PushBlockDirection.Up,
        };

    public static string ToDirectionPropertyValue(PushBlockDirection direction) =>
        direction switch
        {
            PushBlockDirection.Down => DownValue,
            PushBlockDirection.Left => LeftValue,
            PushBlockDirection.Right => RightValue,
            _ => UpValue,
        };

    public static string CycleDirectionPropertyValue(string? value)
    {
        return ToDirectionPropertyValue(ParseDirection(value) switch
        {
            PushBlockDirection.Up => PushBlockDirection.Right,
            PushBlockDirection.Right => PushBlockDirection.Down,
            PushBlockDirection.Down => PushBlockDirection.Left,
            _ => PushBlockDirection.Up,
        });
    }

    public static string GetDirectionDisplayLabel(string? value) =>
        ParseDirection(value) switch
        {
            PushBlockDirection.Down => "Down",
            PushBlockDirection.Left => "Left",
            PushBlockDirection.Right => "Right",
            _ => "Up",
        };

    public static float ParseSpeedPerTick(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, SpeedPropertyKey, DefaultPushSpeedPerTick.ToString(CultureInfo.InvariantCulture))
            : null;
        return ParseSpeedPerTick(value, DefaultPushSpeedPerTick);
    }

    public static float ParseSpeedPerTick(string? value, float fallback)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && float.IsFinite(parsed)
            && parsed > 0f)
        {
            return parsed;
        }

        return fallback;
    }

    public static RoomObjectMarker CreateMarker(
        float x,
        float y,
        float xScale,
        float yScale,
        IReadOnlyDictionary<string, string> properties,
        bool useCenterOrigin)
    {
        var width = 42f * NormalizeScale(xScale);
        var height = 42f * NormalizeScale(yScale);
        var (topLeftX, topLeftY) = CustomMapEntityPlacementAnchor.ToTopLeft(x, y, width, height, useCenterOrigin);
        return new RoomObjectMarker(
            ResolveRoomObjectType(ParseDirection(properties)),
            topLeftX,
            topLeftY,
            width,
            height,
            "sprite64",
            SourceName: EntityType,
            Value: ParseSpeedPerTick(properties) * LegacyMovementModel.SourceTicksPerSecond);
    }

    private static RoomObjectType ResolveRoomObjectType(PushBlockDirection direction) =>
        direction switch
        {
            PushBlockDirection.Down => RoomObjectType.MoveBoxDown,
            PushBlockDirection.Left => RoomObjectType.MoveBoxLeft,
            PushBlockDirection.Right => RoomObjectType.MoveBoxRight,
            _ => RoomObjectType.MoveBoxUp,
        };

    private static float NormalizeScale(float scale) =>
        float.IsFinite(scale) && MathF.Abs(scale) > 0f ? MathF.Abs(scale) : 1f;

    private static string ReadProperty(
        IReadOnlyDictionary<string, string> properties,
        string key,
        string fallback)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }
}

public readonly record struct CatapultConfiguration(
    float AngleDegrees,
    float Speed,
    bool RequiresJumpPress)
{
    public (float X, float Y) GetImpulse()
    {
        var radians = AngleDegrees * (MathF.PI / 180f);
        return (MathF.Cos(radians) * Speed, -MathF.Sin(radians) * Speed);
    }
}

public static class CatapultMetadata
{
    public const string EntityType = "catapult";
    public const string AnglePropertyKey = "angle";
    public const string SpeedPropertyKey = "speed";
    public const string RequiresJumpPressPropertyKey = "requiresW";
    public const float DefaultAngleDegrees = 90f;
    public const float DefaultLaunchSpeedPerTick = 10f;
    public const string DefaultProperties = "xscale=1;yscale=1;angle=90;speed=10;requiresW=false";

    public static bool IsCatapultEntityType(string type) =>
        type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);

    public static CatapultConfiguration FromProperties(IReadOnlyDictionary<string, string>? properties) => new(
        ParseAngleDegrees(properties),
        ParseSpeedPerTick(properties) * LegacyMovementModel.SourceTicksPerSecond,
        ParseRequiresJumpPress(properties));

    public static float ParseAngleDegrees(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, AnglePropertyKey, DefaultAngleDegrees.ToString(CultureInfo.InvariantCulture))
            : null;
        return ParseAngleDegrees(value);
    }

    public static float ParseAngleDegrees(string? value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && float.IsFinite(parsed))
        {
            var normalized = parsed % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        return DefaultAngleDegrees;
    }

    public static float ParseSpeedPerTick(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, SpeedPropertyKey, DefaultLaunchSpeedPerTick.ToString(CultureInfo.InvariantCulture))
            : null;
        return PushBlockMetadata.ParseSpeedPerTick(value, DefaultLaunchSpeedPerTick);
    }

    public static bool ParseRequiresJumpPress(IReadOnlyDictionary<string, string>? properties)
    {
        var value = properties is not null
            ? ReadProperty(properties, RequiresJumpPressPropertyKey, "false")
            : "false";
        return DamageTriggerMetadata.ParseBoolProperty(value);
    }

    public static string CycleRequiresJumpPressPropertyValue(string? value) =>
        DamageTriggerMetadata.ParseBoolProperty(value ?? "false") ? "false" : "true";

    public static string GetRequiresJumpPressDisplayLabel(string? value) =>
        DamageTriggerMetadata.ParseBoolProperty(value ?? "false") ? "W press" : "Enter";

    public static RoomObjectMarker CreateMarker(
        float x,
        float y,
        float xScale,
        float yScale,
        IReadOnlyDictionary<string, string> properties,
        bool useCenterOrigin)
    {
        var width = 42f * NormalizeScale(xScale);
        var height = 42f * NormalizeScale(yScale);
        var (topLeftX, topLeftY) = CustomMapEntityPlacementAnchor.ToTopLeft(x, y, width, height, useCenterOrigin);
        var configuration = FromProperties(properties);
        return new RoomObjectMarker(
            RoomObjectType.Catapult,
            topLeftX,
            topLeftY,
            width,
            height,
            "sprite64",
            SourceName: EntityType,
            Value: configuration.Speed,
            Catapult: configuration);
    }

    private static float NormalizeScale(float scale) =>
        float.IsFinite(scale) && MathF.Abs(scale) > 0f ? MathF.Abs(scale) : 1f;

    private static string ReadProperty(
        IReadOnlyDictionary<string, string> properties,
        string key,
        string fallback)
    {
        return properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
    }
}
