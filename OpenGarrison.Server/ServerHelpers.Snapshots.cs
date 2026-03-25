using System;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

internal static partial class ServerHelpers
{
    internal static SnapshotPlayerState ToSnapshotPlayerState(SimulationWorld world, byte slot, PlayerEntity player, PlayerEntity? viewer)
    {
        var isPlayableSlot = SimulationWorld.IsPlayableNetworkPlayerSlot(slot);
        var isDominatingLocalViewer = viewer is not null
            && !ReferenceEquals(player, viewer)
            && player.GetDominationKillCount(viewer.Id) > 3;
        var isDominatedByLocalViewer = viewer is not null
            && !ReferenceEquals(player, viewer)
            && viewer.GetDominationKillCount(player.Id) > 3;
        return new SnapshotPlayerState(
            slot,
            player.Id,
            player.DisplayName,
            (byte)player.Team,
            (byte)player.ClassId,
            player.IsAlive,
            isPlayableSlot && world.IsNetworkPlayerAwaitingJoin(slot),
            slot >= SimulationWorld.FirstSpectatorSlot,
            isPlayableSlot ? world.GetNetworkPlayerRespawnTicks(slot) : 0,
            player.X,
            player.Y,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            (short)player.Health,
            (short)player.MaxHealth,
            (short)player.CurrentShells,
            (short)player.MaxShells,
            (short)player.Kills,
            (short)player.Deaths,
            (short)player.Caps,
            (short)player.HealPoints,
            (short)player.ActiveDominationCount,
            isDominatingLocalViewer,
            isDominatedByLocalViewer,
            player.Metal,
            player.IsGrounded,
            player.RemainingAirJumps,
            player.IsCarryingIntel,
            player.IsSpyCloaked,
            player.SpyCloakAlpha,
            player.IsUbered,
            player.IsHeavyEating,
            player.HeavyEatTicksRemaining,
            player.IsSniperScoped,
            player.SniperChargeTicks,
            player.FacingDirectionX,
            player.AimDirectionDegrees,
            player.IsTaunting,
            player.TauntFrameIndex,
            player.IsChatBubbleVisible,
            player.ChatBubbleFrameIndex,
            player.ChatBubbleAlpha,
            player.BurnIntensity,
            player.BurnDurationSourceTicks,
            player.BurnDecayDelaySourceTicksRemaining,
            player.BurnIntensityDecayPerSourceTick,
            player.BurnedByPlayerId ?? -1,
            (byte)player.MovementState,
            player.PrimaryCooldownTicks,
            player.ReloadTicksUntilNextShell);
    }

    internal static SnapshotIntelState ToSnapshotIntelState(TeamIntelligenceState intel)
    {
        return new SnapshotIntelState(
            (byte)intel.Team,
            intel.X,
            intel.Y,
            intel.IsAtBase,
            intel.IsDropped,
            intel.ReturnTicksRemaining);
    }

    internal static SnapshotSentryState ToSnapshotSentryState(SentryEntity sentry)
    {
        return new SnapshotSentryState(
            sentry.Id,
            sentry.OwnerPlayerId,
            (byte)sentry.Team,
            sentry.X,
            sentry.Y,
            sentry.Health,
            sentry.IsBuilt,
            sentry.FacingDirectionX,
            sentry.DesiredFacingDirectionX,
            sentry.AimDirectionDegrees,
            sentry.ReloadTicksRemaining,
            sentry.AlertTicksRemaining,
            sentry.ShotTraceTicksRemaining,
            sentry.HasLanded,
            sentry.HasActiveTarget,
            sentry.CurrentTargetPlayerId ?? -1,
            sentry.LastShotTargetX,
            sentry.LastShotTargetY);
    }

    internal static SnapshotShotState ToSnapshotBulletState(ShotProjectileEntity shot)
    {
        return new SnapshotShotState(shot.Id, (byte)shot.Team, shot.OwnerId, shot.X, shot.Y, shot.VelocityX, shot.VelocityY, shot.TicksRemaining);
    }

    internal static SnapshotShotState ToSnapshotNeedleState(NeedleProjectileEntity shot)
    {
        return new SnapshotShotState(shot.Id, (byte)shot.Team, shot.OwnerId, shot.X, shot.Y, shot.VelocityX, shot.VelocityY, shot.TicksRemaining);
    }

    internal static SnapshotShotState ToSnapshotBubbleState(BubbleProjectileEntity bubble)
    {
        return new SnapshotShotState(bubble.Id, (byte)bubble.Team, bubble.OwnerId, bubble.X, bubble.Y, bubble.VelocityX, bubble.VelocityY, bubble.TicksRemaining);
    }

    internal static SnapshotShotState ToSnapshotBladeState(BladeProjectileEntity blade)
    {
        return new SnapshotShotState(blade.Id, (byte)blade.Team, blade.OwnerId, blade.X, blade.Y, blade.VelocityX, blade.VelocityY, blade.TicksRemaining);
    }

    internal static SnapshotShotState ToSnapshotRevolverState(RevolverProjectileEntity shot)
    {
        return new SnapshotShotState(shot.Id, (byte)shot.Team, shot.OwnerId, shot.X, shot.Y, shot.VelocityX, shot.VelocityY, shot.TicksRemaining);
    }

    internal static SnapshotRocketState ToSnapshotRocketState(RocketProjectileEntity rocket)
    {
        var passedFriendlyPlayerIds = rocket.PassedFriendlyPlayerIds.Count == 0
            ? Array.Empty<int>()
            : [.. rocket.PassedFriendlyPlayerIds.OrderBy(static id => id)];

        return new SnapshotRocketState(
            rocket.Id,
            (byte)rocket.Team,
            rocket.OwnerId,
            rocket.X,
            rocket.Y,
            rocket.PreviousX,
            rocket.PreviousY,
            rocket.DirectionRadians,
            rocket.Speed,
            rocket.TicksRemaining,
            rocket.ReducedKnockbackSourceTicksRemaining,
            rocket.ZeroKnockbackSourceTicksRemaining,
            rocket.RangeAnchorOwnerId,
            rocket.LastKnownRangeOriginX,
            rocket.LastKnownRangeOriginY,
            rocket.DistanceToTravel,
            rocket.IsFading,
            rocket.FadeSourceTicksRemaining,
            passedFriendlyPlayerIds);
    }

    internal static SnapshotFlameState ToSnapshotFlameState(FlameProjectileEntity flame)
    {
        return new SnapshotFlameState(
            flame.Id,
            (byte)flame.Team,
            flame.OwnerId,
            flame.X,
            flame.Y,
            flame.PreviousX,
            flame.PreviousY,
            flame.VelocityX,
            flame.VelocityY,
            flame.TicksRemaining,
            flame.AttachedPlayerId ?? -1,
            flame.AttachedOffsetX,
            flame.AttachedOffsetY);
    }

    internal static SnapshotMineState ToSnapshotMineState(MineProjectileEntity mine)
    {
        return new SnapshotMineState(
            mine.Id,
            (byte)mine.Team,
            mine.OwnerId,
            mine.X,
            mine.Y,
            mine.VelocityX,
            mine.VelocityY,
            mine.IsStickied,
            mine.IsDestroyed,
            mine.ExplosionDamage);
    }

    internal static SnapshotDeadBodyState ToSnapshotDeadBodyState(DeadBodyEntity deadBody)
    {
        return new SnapshotDeadBodyState(
            deadBody.Id,
            (byte)deadBody.Team,
            (byte)deadBody.ClassId,
            deadBody.X,
            deadBody.Y,
            deadBody.Width,
            deadBody.Height,
            deadBody.HorizontalSpeed,
            deadBody.VerticalSpeed,
            deadBody.FacingLeft,
            deadBody.TicksRemaining);
    }

    internal static SnapshotControlPointState ToSnapshotControlPointState(ControlPointState point)
    {
        return new SnapshotControlPointState(
            (byte)point.Index,
            (byte)(point.Team.HasValue ? point.Team.Value : 0),
            (byte)(point.CappingTeam.HasValue ? point.CappingTeam.Value : 0),
            (ushort)Math.Clamp((int)MathF.Round(point.CappingTicks), 0, ushort.MaxValue),
            (ushort)Math.Clamp(point.CapTimeTicks, 0, ushort.MaxValue),
            (byte)Math.Clamp(point.Cappers, 0, byte.MaxValue),
            point.IsLocked);
    }

    internal static SnapshotGeneratorState ToSnapshotGeneratorState(GeneratorState generator)
    {
        return new SnapshotGeneratorState(
            (byte)generator.Team,
            (short)generator.Health,
            (short)generator.MaxHealth);
    }

    internal static SnapshotPlayerGibState ToSnapshotPlayerGibState(PlayerGibEntity gib)
    {
        return new SnapshotPlayerGibState(
            gib.Id,
            gib.SpriteName,
            gib.FrameIndex,
            gib.X,
            gib.Y,
            gib.VelocityX,
            gib.VelocityY,
            gib.RotationDegrees,
            gib.RotationSpeedDegrees,
            gib.TicksRemaining,
            gib.BloodChance);
    }

    internal static SnapshotBloodDropState ToSnapshotBloodDropState(BloodDropEntity bloodDrop)
    {
        return new SnapshotBloodDropState(
            bloodDrop.Id,
            bloodDrop.X,
            bloodDrop.Y,
            bloodDrop.VelocityX,
            bloodDrop.VelocityY,
            bloodDrop.IsStuck,
            bloodDrop.TicksRemaining,
            bloodDrop.Scale);
    }

    internal static SnapshotCombatTraceState ToSnapshotCombatTraceState(CombatTrace trace)
    {
        return new SnapshotCombatTraceState(
            trace.StartX,
            trace.StartY,
            trace.EndX,
            trace.EndY,
            trace.TicksRemaining,
            trace.HitCharacter,
            (byte)trace.Team,
            trace.IsSniperTracer);
    }

    internal static SnapshotSoundEvent ToSnapshotSoundEvent(WorldSoundEvent soundEvent, ulong fallbackEventId)
    {
        return new SnapshotSoundEvent(
            soundEvent.SoundName,
            soundEvent.X,
            soundEvent.Y,
            soundEvent.EventId == 0 ? fallbackEventId : soundEvent.EventId,
            soundEvent.SourceFrame);
    }

    internal static SnapshotVisualEvent ToSnapshotVisualEvent(WorldVisualEvent visualEvent, ulong fallbackEventId)
    {
        return new SnapshotVisualEvent(visualEvent.EffectName, visualEvent.X, visualEvent.Y, visualEvent.DirectionDegrees, visualEvent.Count, visualEvent.EventId == 0 ? fallbackEventId : visualEvent.EventId);
    }

    internal static SnapshotKillFeedEntry ToSnapshotKillFeedEntry(KillFeedEntry entry)
    {
        return new SnapshotKillFeedEntry(
            entry.KillerName,
            (byte)entry.KillerTeam,
            entry.WeaponSpriteName,
            entry.VictimName,
            (byte)entry.VictimTeam,
            entry.MessageText,
            entry.KillerPlayerId,
            entry.VictimPlayerId,
            (OpenGarrison.Protocol.KillFeedSpecialType)entry.SpecialType,
            entry.EventId);
    }

    internal static SnapshotDeathCamState? ToSnapshotDeathCamState(LocalDeathCamState? deathCam)
    {
        if (deathCam is null)
        {
            return null;
        }

        return new SnapshotDeathCamState(
            deathCam.FocusX,
            deathCam.FocusY,
            deathCam.KillMessage,
            deathCam.KillerName,
            deathCam.KillerTeam.HasValue ? (byte)deathCam.KillerTeam.Value : (byte)0,
            deathCam.Health,
            deathCam.MaxHealth,
            deathCam.RemainingTicks,
            deathCam.InitialTicks);
    }
}
