using System.Linq;

namespace OpenGarrison.Core;

internal static class SimpleLevelScaling
{
    public static SimpleLevel ApplyUniformScale(SimpleLevel level, float scale)
    {
        var clampedScale = float.Clamp(scale, 0.25f, 4f);
        if (MathF.Abs(clampedScale - 1f) <= 0.0001f)
        {
            return level;
        }

        return new SimpleLevel(
            level.Name,
            level.Mode,
            new WorldBounds(level.Bounds.Width * clampedScale, level.Bounds.Height * clampedScale),
            clampedScale,
            level.BackgroundAssetName,
            level.MapAreaIndex,
            level.MapAreaCount,
            Scale(level.LocalSpawn, clampedScale),
            level.RedSpawns.Select(spawn => Scale(spawn, clampedScale)).ToArray(),
            level.BlueSpawns.Select(spawn => Scale(spawn, clampedScale)).ToArray(),
            level.IntelBases.Select(marker => Scale(marker, clampedScale)).ToArray(),
            level.RoomObjects.Select(marker => Scale(marker, clampedScale)).ToArray(),
            level.FloorY * clampedScale,
            level.Solids.Select(solid => Scale(solid, clampedScale)).ToArray(),
            level.ImportedFromSource,
            level.AreaTransitionMarkers.Select(marker => Scale(marker, clampedScale)).ToArray(),
            level.UnsupportedSourceEntities.ToArray())
        {
            ControlPointSetupGatesActive = level.ControlPointSetupGatesActive,
            ForcedBlockingTeamGates = level.ForcedBlockingTeamGates,
        };
    }

    private static SpawnPoint Scale(SpawnPoint spawn, float scale) => new(spawn.X * scale, spawn.Y * scale);

    private static IntelBaseMarker Scale(IntelBaseMarker marker, float scale) => new(marker.Team, marker.X * scale, marker.Y * scale);

    private static RoomObjectMarker Scale(RoomObjectMarker marker, float scale) => new(
        marker.Type,
        marker.X * scale,
        marker.Y * scale,
        marker.Width * scale,
        marker.Height * scale,
        marker.SpriteName,
        marker.Team,
        marker.SourceName,
        marker.Value * scale);

    private static LevelSolid Scale(LevelSolid solid, float scale) => new(solid.X * scale, solid.Y * scale, solid.Width * scale, solid.Height * scale);

    private static AreaTransitionMarker Scale(AreaTransitionMarker marker, float scale) => new(marker.X * scale, marker.Y * scale, marker.Direction, marker.SourceName);
}
