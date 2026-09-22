using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public readonly record struct BarrierConfiguration(BarrierTargetFilters Targets)
{
    public const float WallDefaultWidth = 6f;
    public const float WallDefaultHeight = 60f;
    public const float FloorDefaultWidth = 60f;
    public const float FloorDefaultHeight = 6f;
    public const float DefaultWidth = WallDefaultWidth;
    public const float DefaultHeight = WallDefaultHeight;
    public const float MinExtent = 6f;

    public static BarrierConfiguration Default => new(BarrierTargetFilters.Default);

    public static BarrierConfiguration FromProperties(IReadOnlyDictionary<string, string>? properties)
    {
        properties = EnsureDefaultTargets(BarrierLegacyPropertyMigration.EnsureModernBarrierProperties(properties));
        return new BarrierConfiguration(BarrierTargetFilters.FromProperties(properties));
    }

    /// <summary>
    /// Supply defaults only when no target settings were authored. Explicit Allow values are intentional.
    /// </summary>
    private static IReadOnlyDictionary<string, string> EnsureDefaultTargets(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return BarrierTargetFilters.SolidWall.ToProperties();
        }

        if (BarrierLegacyPropertyMigration.UsesModernBarrierSchema(properties))
        {
            return properties;
        }

        var merged = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in BarrierTargetFilters.SolidWall.ToProperties())
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    public static bool IsFloorOrientation(IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null)
        {
            return false;
        }

        if (properties.TryGetValue("orientation", out var orientation)
            && orientation.Equals("floor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return properties.TryGetValue("axis", out var axis)
            && axis.Equals("floor", StringComparison.OrdinalIgnoreCase);
    }

    public static (float Width, float Height) ResolveDimensions(float xScale, float yScale, bool floor = false)
    {
        var baseWidth = floor ? FloorDefaultWidth : WallDefaultWidth;
        var baseHeight = floor ? FloorDefaultHeight : WallDefaultHeight;
        var width = baseWidth * MathF.Abs(xScale <= 0f ? 1f : xScale);
        var height = baseHeight * MathF.Abs(yScale <= 0f ? 1f : yScale);
        return (MathF.Max(MinExtent, width), MathF.Max(MinExtent, height));
    }

    public static RoomObjectMarker CreateMarker(
        float x,
        float y,
        float xScale,
        float yScale,
        in BarrierConfiguration configuration,
        bool floor = false)
    {
        var (width, height) = ResolveDimensions(xScale, yScale, floor);
        return new RoomObjectMarker(
            RoomObjectType.Barrier,
            x,
            y,
            width,
            height,
            "sprite45",
            ResolveDisplayTeam(configuration),
            "barrier",
            Value: 0f,
            InitialOwnership: ControlPointInitialOwnership.ModeDefault,
            Barrier: configuration);
    }

    public static PlayerTeam? ResolveDisplayTeam(in BarrierConfiguration configuration)
    {
        return ResolveDisplayTeam(configuration.Targets);
    }

    public static PlayerTeam? ResolveDisplayTeam(in BarrierTargetFilters targets)
    {
        if (TryResolveTierDisplayTeam(
                targets.RedPlayers,
                targets.BluePlayers,
                targets.Blocks(BarrierTargetKind.RedPlayers),
                targets.Blocks(BarrierTargetKind.BluePlayers),
                out var playerTeam))
        {
            return playerTeam;
        }

        if (TryResolveTierDisplayTeam(
                targets.RedShots,
                targets.BlueShots,
                targets.Blocks(BarrierTargetKind.RedShots),
                targets.Blocks(BarrierTargetKind.BlueShots),
                out var shotTeam))
        {
            return shotTeam;
        }

        if (TryResolveTierDisplayTeam(
                targets.RedIntel,
                targets.BlueIntel,
                targets.Blocks(BarrierTargetKind.RedIntel),
                targets.Blocks(BarrierTargetKind.BlueIntel),
                out var intelTeam))
        {
            return intelTeam;
        }

        return null;
    }

    public bool Blocks(BarrierTargetKind target) => Targets.Blocks(target);

    public Dictionary<string, string> ToProperties()
    {
        var properties = Targets.ToProperties();
        properties["xscale"] = "1";
        properties["yscale"] = "1";
        return properties;
    }

    private static bool TryResolveTierDisplayTeam(
        BarrierTargetFilter redSetting,
        BarrierTargetFilter blueSetting,
        bool blocksRed,
        bool blocksBlue,
        out PlayerTeam? team)
    {
        if (redSetting == blueSetting)
        {
            team = null;
            return false;
        }

        team = ResolveExclusivePassingTeam(blocksRed, blocksBlue);
        return team.HasValue;
    }

    private static PlayerTeam? ResolveExclusivePassingTeam(bool blocksRed, bool blocksBlue)
    {
        if (!blocksRed && blocksBlue)
        {
            return PlayerTeam.Red;
        }

        if (blocksRed && !blocksBlue)
        {
            return PlayerTeam.Blue;
        }

        return null;
    }
}
