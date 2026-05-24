using System;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

internal static partial class ServerHelpers
{
    private const string CoreReplicatedOwnerId = "core.player";
    private const string SoldierShotgunAvailableKey = "soldier_shotgun_available";
    private const string SoldierShotgunEquippedKey = "soldier_shotgun_equipped";
    private const string SoldierShotgunAmmoKey = "soldier_shotgun_ammo";
    private const string SoldierShotgunMaxAmmoKey = "soldier_shotgun_max_ammo";
    private const string SoldierShotgunReloadTicksKey = "soldier_shotgun_reload_ticks";
    private const string SoldierShotgunCooldownTicksKey = "soldier_shotgun_cooldown_ticks";
    private const string DemomanGrenadeLauncherAmmoKey = "demoman_gl_ammo";
    private const string DemomanGrenadeLauncherReloadTicksKey = "demoman_gl_reload_ticks";

    internal static SnapshotPlayerState ToSnapshotPlayerState(
        SimulationWorld world,
        byte slot,
        PlayerEntity player,
        PlayerEntity? viewer,
        SnapshotStringCache stringCache)
    {
        var isPlayableSlot = SimulationWorld.IsPlayableNetworkPlayerSlot(slot);
        var isAwaitingJoin = isPlayableSlot && world.IsNetworkPlayerAwaitingJoin(slot);
        var snapshotTeam = isAwaitingJoin
            ? world.GetNetworkPlayerConfiguredTeam(slot)
            : player.Team;
        var isDominatingLocalViewer = viewer is not null
            && !ReferenceEquals(player, viewer)
            && player.GetDominationKillCount(viewer.Id) > 3;
        var isDominatedByLocalViewer = viewer is not null
            && !ReferenceEquals(player, viewer)
            && viewer.GetDominationKillCount(player.Id) > 3;
        var replicatedStates = player.GetReplicatedStateEntries()
            .Select(static entry => new SnapshotReplicatedStateEntry(
                entry.OwnerId,
                entry.Key,
                entry.Kind switch
                {
                    GameplayReplicatedStateValueKind.Whole => SnapshotReplicatedStateValueKind.Whole,
                    GameplayReplicatedStateValueKind.Scalar => SnapshotReplicatedStateValueKind.Scalar,
                    _ => SnapshotReplicatedStateValueKind.Toggle,
                },
                entry.IntValue,
                entry.FloatValue,
                entry.BoolValue))
            .ToList();

        replicatedStates.AddRange(GameplayAbilityReplicatedState.CreateEntries(player)
            .Select(static entry => new SnapshotReplicatedStateEntry(
                entry.OwnerId,
                entry.Key,
                entry.Kind switch
                {
                    GameplayReplicatedStateValueKind.Whole => SnapshotReplicatedStateValueKind.Whole,
                    GameplayReplicatedStateValueKind.Scalar => SnapshotReplicatedStateValueKind.Scalar,
                    _ => SnapshotReplicatedStateValueKind.Toggle,
                },
                entry.IntValue,
                entry.FloatValue,
                entry.BoolValue)));

        if (player.ClassId == PlayerClass.Soldier)
        {
            replicatedStates.Add(new SnapshotReplicatedStateEntry(
                CoreReplicatedOwnerId,
                SoldierShotgunAvailableKey,
                SnapshotReplicatedStateValueKind.Toggle,
                0,
                0f,
                player.HasExperimentalOffhandWeapon));
            replicatedStates.Add(new SnapshotReplicatedStateEntry(
                CoreReplicatedOwnerId,
                SoldierShotgunEquippedKey,
                SnapshotReplicatedStateValueKind.Toggle,
                0,
                0f,
                player.IsExperimentalOffhandPresented));
            replicatedStates.Add(new SnapshotReplicatedStateEntry(
                CoreReplicatedOwnerId,
                SoldierShotgunAmmoKey,
                SnapshotReplicatedStateValueKind.Whole,
                player.ExperimentalOffhandCurrentShells,
                0f,
                false));
            replicatedStates.Add(new SnapshotReplicatedStateEntry(
                CoreReplicatedOwnerId,
                SoldierShotgunMaxAmmoKey,
                SnapshotReplicatedStateValueKind.Whole,
                player.ExperimentalOffhandMaxShells,
                0f,
                false));
            replicatedStates.Add(new SnapshotReplicatedStateEntry(
                CoreReplicatedOwnerId,
                SoldierShotgunReloadTicksKey,
                SnapshotReplicatedStateValueKind.Whole,
                player.ExperimentalOffhandReloadTicksUntilNextShell,
                0f,
                false));
            replicatedStates.Add(new SnapshotReplicatedStateEntry(
                CoreReplicatedOwnerId,
                SoldierShotgunCooldownTicksKey,
                SnapshotReplicatedStateValueKind.Whole,
                player.ExperimentalOffhandCooldownTicks,
                0f,
                false));
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            replicatedStates.Add(new SnapshotReplicatedStateEntry(
                CoreReplicatedOwnerId,
                DemomanGrenadeLauncherAmmoKey,
                SnapshotReplicatedStateValueKind.Whole,
                player.ExperimentalOffhandCurrentShells,
                0f,
                false));
            replicatedStates.Add(new SnapshotReplicatedStateEntry(
                CoreReplicatedOwnerId,
                DemomanGrenadeLauncherReloadTicksKey,
                SnapshotReplicatedStateValueKind.Whole,
                player.ExperimentalOffhandReloadTicksUntilNextShell,
                0f,
                false));
        }

        return new SnapshotPlayerState(
            slot,
            player.Id,
            player.DisplayName,
            (byte)snapshotTeam,
            (byte)player.ClassId,
            player.IsAlive,
            isAwaitingJoin,
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
            player.Points,
            (short)player.HealPoints,
            (short)player.ActiveDominationCount,
            isDominatingLocalViewer,
            isDominatedByLocalViewer,
            player.Metal,
            player.IsGrounded,
            player.RemainingAirJumps,
            player.IsCarryingIntel,
            player.IntelRechargeTicks,
            player.IsSpyCloaked,
            player.SpyCloakAlpha,
            player.IsSpySuperjumping,
            player.SpySuperjumpHorizontalVelocity,
            player.SpySuperjumpCooldownTicksRemaining,
            player.SpyBackstabVisualTicksRemaining,
            player.IsUbered,
            player.IsKritzCritBoosted,
            player.IsHeavyEating,
            player.HeavyEatTicksRemaining,
            player.IsSniperScoped,
            player.SniperChargeTicks,
            player.IsUsingBinoculars,
            player.BinocularsFocusX,
            player.BinocularsFocusY,
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
            player.ReloadTicksUntilNextShell,
            player.MedicNeedleCooldownTicks,
            player.MedicNeedleRefillTicks,
            player.PyroAirblastCooldownTicks,
            player.PyroFlareCooldownTicks,
            player.PyroPrimaryFuelScaled,
            player.IsPyroPrimaryRefilling,
            player.PyroFlameLoopTicksRemaining,
            player.PyroPrimaryRequiresReleaseAfterEmpty,
            player.HeavyEatCooldownTicksRemaining,
            (short)player.Assists,
            player.BadgeMask,
            player.IsMedicHealing,
            player.MedicHealTargetId ?? -1,
            player.MedicUberCharge,
            player.IsMedicUberReady,
            player.GameplayLoadoutState.ModPackId,
            player.GameplayLoadoutState.LoadoutId,
            player.GameplayLoadoutState.PrimaryItemId,
            player.GameplayLoadoutState.SecondaryItemId ?? string.Empty,
            player.GameplayLoadoutState.UtilityItemId ?? string.Empty,
            (byte)player.GameplayLoadoutState.EquippedSlot,
            player.GameplayLoadoutState.EquippedItemId,
            player.GameplayLoadoutState.AcquiredItemId ?? string.Empty,
            GameplayModPackCacheId: stringCache.GetOrAddCacheId(player.GameplayLoadoutState.ModPackId),
            GameplayLoadoutCacheId: stringCache.GetOrAddCacheId(player.GameplayLoadoutState.LoadoutId),
            GameplayPrimaryItemCacheId: stringCache.GetOrAddCacheId(player.GameplayLoadoutState.PrimaryItemId),
            GameplaySecondaryItemCacheId: stringCache.GetOrAddCacheId(player.GameplayLoadoutState.SecondaryItemId ?? string.Empty),
            GameplayUtilityItemCacheId: stringCache.GetOrAddCacheId(player.GameplayLoadoutState.UtilityItemId ?? string.Empty),
            GameplayEquippedItemCacheId: stringCache.GetOrAddCacheId(player.GameplayLoadoutState.EquippedItemId),
            GameplayAcquiredItemCacheId: stringCache.GetOrAddCacheId(player.GameplayLoadoutState.AcquiredItemId ?? string.Empty),
            ReferenceEquals(player, viewer) ? player.GetTrackedOwnedGameplayItemIds() : Array.Empty<string>(),
            replicatedStates.ToArray(),
            player.PlayerScale,
            AimWorldX: player.AimWorldX,
            AimWorldY: player.AimWorldY,
            OffhandCooldownTicks: player.ExperimentalOffhandCooldownTicks,
            OffhandReloadTicks: player.ExperimentalOffhandReloadTicksUntilNextShell,
            GibDeaths: (short)Math.Clamp(player.GibDeaths, 0, short.MaxValue),
            IsReady: world.IsNetworkPlayerReady(slot),
            GameplayClassId: player.GameplayClassId,
            GameplayClassCacheId: stringCache.GetOrAddCacheId(player.GameplayClassId));
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
            sentry.AimDirectionDegrees,
            sentry.ShotTraceTicksRemaining,
            sentry.HasLanded,
            sentry.HasActiveTarget,
            sentry.LastShotTargetX,
            sentry.LastShotTargetY);
    }

    internal static SnapshotJumpPadState ToSnapshotJumpPadState(JumpPadEntity pad)
    {
        return new SnapshotJumpPadState(
            pad.Id,
            pad.OwnerPlayerId,
            (byte)pad.Team,
            pad.X,
            pad.Y,
            pad.Health,
            pad.HasLanded,
            pad.IsBuilt);
    }

    internal static SnapshotJumpPadGibState ToSnapshotJumpPadGibState(JumpPadGibEntity jumpPadGib)
    {
        return new SnapshotJumpPadGibState(
            jumpPadGib.Id,
            (byte)jumpPadGib.Team,
            jumpPadGib.X,
            jumpPadGib.Y,
            jumpPadGib.TicksRemaining);
    }

    internal static SnapshotShotState ToSnapshotBulletState(ShotProjectileEntity shot)
    {
        return new SnapshotShotState(shot.Id, (byte)shot.Team, shot.OwnerId, shot.X, shot.Y, shot.VelocityX, shot.VelocityY, shot.TicksRemaining, shot.IsCritical);
    }

    internal static SnapshotShotState ToSnapshotNeedleState(NeedleProjectileEntity shot)
    {
        return new SnapshotShotState(shot.Id, (byte)shot.Team, shot.OwnerId, shot.X, shot.Y, shot.VelocityX, shot.VelocityY, shot.TicksRemaining, shot.IsCritical);
    }

    internal static SnapshotShotState ToSnapshotBubbleState(BubbleProjectileEntity bubble)
    {
        return new SnapshotShotState(bubble.Id, (byte)bubble.Team, bubble.OwnerId, bubble.X, bubble.Y, bubble.VelocityX, bubble.VelocityY, bubble.TicksRemaining);
    }

    internal static SnapshotShotState ToSnapshotBladeState(BladeProjectileEntity blade)
    {
        return new SnapshotShotState(blade.Id, (byte)blade.Team, blade.OwnerId, blade.X, blade.Y, blade.VelocityX, blade.VelocityY, blade.TicksRemaining, blade.IsCritical);
    }

    internal static SnapshotShotState ToSnapshotRevolverState(RevolverProjectileEntity shot)
    {
        return new SnapshotShotState(shot.Id, (byte)shot.Team, shot.OwnerId, shot.X, shot.Y, shot.VelocityX, shot.VelocityY, shot.TicksRemaining, shot.IsCritical);
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
            passedFriendlyPlayerIds,
            rocket.IsCritical);
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
            flame.AttachedOffsetY,
            flame.IsCritical);
    }

    internal static SnapshotShotState ToSnapshotFlareState(FlareProjectileEntity flare)
    {
        return new SnapshotShotState(flare.Id, (byte)flare.Team, flare.OwnerId, flare.X, flare.Y, flare.VelocityX, flare.VelocityY, flare.TicksRemaining, flare.IsCritical);
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
            mine.ExplosionDamage,
            mine.IsCritical);
    }

    internal static SnapshotGrenadeState ToSnapshotGrenadeState(GrenadeProjectileEntity grenade)
    {
        return new SnapshotGrenadeState(
            grenade.Id,
            (byte)grenade.Team,
            grenade.OwnerId,
            grenade.X,
            grenade.Y,
            grenade.PreviousX,
            grenade.PreviousY,
            grenade.VelocityX,
            grenade.VelocityY,
            grenade.FuseTicksLeft,
            grenade.IsCritical);
    }

    internal static SnapshotDeadBodyState ToSnapshotDeadBodyState(DeadBodyEntity deadBody)
    {
        return new SnapshotDeadBodyState(
            deadBody.Id,
            deadBody.SourcePlayerId,
            (byte)deadBody.Team,
            (byte)deadBody.ClassId,
            (byte)deadBody.AnimationKind,
            deadBody.X,
            deadBody.Y,
            deadBody.Width,
            deadBody.Height,
            deadBody.HorizontalSpeed,
            deadBody.VerticalSpeed,
            deadBody.FacingLeft,
            deadBody.TicksRemaining);
    }

    internal static SnapshotSentryGibState ToSnapshotSentryGibState(SentryGibEntity sentryGib)
    {
        return new SnapshotSentryGibState(
            sentryGib.Id,
            (byte)sentryGib.Team,
            sentryGib.X,
            sentryGib.Y,
            sentryGib.TicksRemaining);
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
            trace.IsSniperTracer,
            trace.IsCritical);
    }

    internal static SnapshotSniperAimIndicatorState ToSnapshotSniperAimIndicatorState(SniperAimIndicator indicator)
    {
        return new SnapshotSniperAimIndicatorState(
            indicator.SniperPlayerId,
            indicator.X,
            indicator.Y,
            (byte)indicator.Team,
            indicator.Transparency);
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

    internal static SnapshotDamageEvent ToSnapshotDamageEvent(WorldDamageEvent damageEvent, ulong fallbackEventId)
    {
        return new SnapshotDamageEvent(
            damageEvent.Amount,
            damageEvent.AttackerPlayerId,
            damageEvent.AssistedByPlayerId,
            (byte)damageEvent.TargetKind,
            damageEvent.TargetEntityId,
            damageEvent.X,
            damageEvent.Y,
            damageEvent.WasFatal,
            damageEvent.EventId == 0 ? fallbackEventId : damageEvent.EventId,
            damageEvent.SourceFrame);
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
            entry.MessageHighlightStart,
            entry.MessageHighlightLength,
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
