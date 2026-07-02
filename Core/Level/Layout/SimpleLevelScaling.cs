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
            level.UnsupportedSourceEntities.ToArray(),
            Scale(level.CustomMapVisuals, clampedScale),
            level.MovingPlatforms.Select(marker => Scale(marker, clampedScale)).ToArray(),
            level.ControlPointSettings,
            level.ScrSettings,
            level.ShowControlPoints,
            level.LogicGraph,
            level.LogicActivators,
            level.LogicScoreTriggers,
            level.SpritesheetPlaybackSet,
            level.HealthPackSpawns.Select(marker => Scale(marker, clampedScale)).ToArray(),
            level.BotSpawns.Select(marker => Scale(marker, clampedScale)).ToArray(),
            level.GameplayMessages.Select(marker => Scale(marker, clampedScale)).ToArray(),
            level.GameplaySounds.Select(marker => Scale(marker, clampedScale)).ToArray(),
            level.SpawnClassBehaviors.Select(marker => Scale(marker, clampedScale)).ToArray())
        {
            ControlPointSetupGatesActive = level.ControlPointSetupGatesActive,
            ForcedBlockingTeamGates = level.ForcedBlockingTeamGates,
        };
    }

    private static SpawnPoint Scale(SpawnPoint spawn, float scale) => new(
        spawn.X * scale,
        spawn.Y * scale,
        spawn.Role,
        spawn.LinkedControlPointIndex,
        spawn.UseCondition,
        spawn.Priority,
        spawn.LogicSignalNodeIndex);

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
        marker.Value * scale,
        marker.InitialOwnership,
        marker.LockRules,
        marker.CapTimeMultiplier,
        marker.IsCapTimeMultiplierCustom,
        marker.Barrier,
        marker.DirectionalWall,
        marker.TeleportZone,
        marker.PlayerTriggerZone,
        marker.CustomMapSprite,
        marker.AreaExtension,
        marker.DamageableZone,
        marker.ForegroundSprite,
        marker.Spritesheet);

    private static LevelSolid Scale(LevelSolid solid, float scale) => new(solid.X * scale, solid.Y * scale, solid.Width * scale, solid.Height * scale);

    private static AreaTransitionMarker Scale(AreaTransitionMarker marker, float scale) => new(marker.X * scale, marker.Y * scale, marker.Direction, marker.SourceName);

    private static MovingPlatformMarker Scale(MovingPlatformMarker marker, float scale) => new(
        marker.X * scale,
        marker.Y * scale,
        marker.Width * scale,
        marker.Height * scale,
        marker.TravelX * scale,
        marker.TravelY * scale,
        marker.UpSpeed * scale,
        marker.DownSpeed * scale,
        marker.TriggerMode,
        marker.ResetMovementState,
        marker.ResourceName,
        marker.SourceName);

    private static HealthPackSpawnMarker Scale(HealthPackSpawnMarker marker, float scale) => new(
        marker.X * scale,
        marker.Y * scale,
        marker.Size,
        marker.RespawnTicks);

    private static BotSpawnMarker Scale(BotSpawnMarker marker, float scale) => new(
        marker.X * scale,
        marker.Y * scale,
        marker.TriggerRef,
        marker.Team,
        marker.ClassId,
        marker.Kind,
        marker.Respawn,
        marker.RespawnMode,
        marker.NameMode,
        marker.Name,
        marker.ForceNameplate,
        marker.ForceHealthBar,
        marker.DeathTriggerRef,
        marker.DeathTriggerNodeIndex,
        marker.TriggerNodeIndex);

    private static GameplayMessageMarker Scale(GameplayMessageMarker marker, float scale) => new(
        marker.X * scale,
        marker.Y * scale,
        marker.TriggerRef,
        marker.Text,
        marker.Style,
        marker.DialogueBoxStyle,
        marker.Animation,
        marker.Font,
        marker.FontScale,
        marker.Alignment,
        marker.ChatTeam,
        marker.ScreenX * scale,
        marker.ScreenY * scale,
        marker.Width * scale,
        marker.Height * scale,
        marker.DurationSeconds,
        marker.TypingDurationSeconds,
        marker.EndMode,
        marker.InputBinding,
        marker.FreezeSimulation,
        marker.SoundName,
        marker.MusicName,
        marker.MusicCrossfade,
        marker.MusicCrossfadeSeconds,
        marker.MusicLoop,
        marker.MusicFadeAfterSeconds,
        marker.ImageResourceName,
        marker.ImageAnimation,
        marker.ImageOffsetX,
        marker.ImageOffsetY,
        marker.ImageWidth,
        marker.ImageHeight,
        marker.OnEndAction,
        marker.OnEndEffects,
        marker.OnEndSoundName,
        marker.OnEndSeconds,
        marker.OnEndMapName,
        marker.OnEndTeleportX * scale,
        marker.OnEndTeleportY * scale,
        marker.OnEndTeleportExitRef,
        marker.OnEndTriggerRef,
        marker.OnEndTriggerNodeIndex,
        marker.TriggerNodeIndex);

    private static GameplaySoundMarker Scale(GameplaySoundMarker marker, float scale) => new(
        marker.X * scale,
        marker.Y * scale,
        marker.TriggerRef,
        marker.SoundName,
        marker.Mode,
        marker.Crossfade,
        marker.CrossfadeSeconds,
        marker.TriggerNodeIndex);

    private static SpawnClassBehaviorMarker Scale(SpawnClassBehaviorMarker marker, float scale) => new(
        marker.X * scale,
        marker.Y * scale,
        marker.Team,
        marker.ForcedClass,
        marker.ManualSpawn,
        marker.SkipTeamSelect,
        marker.AllowTeamChange,
        marker.AllowClassChange);

    private static CustomMapVisualMetadata Scale(CustomMapVisualMetadata metadata, float scale)
    {
        if (ReferenceEquals(metadata, CustomMapVisualMetadata.Empty))
        {
            return metadata;
        }

        return metadata with
        {
            ImageScale = metadata.ImageScale * scale,
            ForegroundOffsetX = metadata.ForegroundOffsetX * scale,
            ForegroundOffsetY = metadata.ForegroundOffsetY * scale,
        };
    }
}
