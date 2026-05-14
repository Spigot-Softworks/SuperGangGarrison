using System;
using System.Collections.Generic;
using System.Text;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal static class SnapshotContributionPlanner
{
    private const int PlayerUpdatePriority = 1500;
    private const int PlayerDetailUpdatePriority = 900;
    private const int AddedPlayerUpdatePriorityBonus = 200;
    private const int LocalPlayerUpdatePriorityBonus = 300;
    private const int LocalPlayerStatusPriorityBonus = 250;
    private const int PlayerChatBubblePriority = 1125;
    private const int RemovedPlayerEstimatedBytes = 6;
    private const int SnapshotPlayerFixedBytes = 220;
    private const int SnapshotPlayerMovementBytes = 37;
    private const int SnapshotPlayerStatusBytes = 20;
    private const int SnapshotPlayerExtendedStatusBytes = 26;
    private const int SnapshotPlayerChatBubbleBytes = 10;
    private const int ProjectileSnapshotUpdateIntervalTicks = 1;
    private const int CosmeticEntityUpdateIntervalTicks = 8;
    private const float MaxEventDistanceFromFocus = 1200f;
    private const int LowFrequencyPlayerDetailRefreshIntervalTicks = 30;

    public static List<SnapshotDeltaBudgeter.Contribution> BuildContributions(
        ClientSession client,
        SnapshotMessage fullSnapshot,
        ISnapshotBaselineState? baseline,
        SimulationWorld world)
    {
        var focus = GetClientFocusPoint(client, world);
        var frame = world.Frame;
        var contributions = new List<SnapshotDeltaBudgeter.Contribution>();

        AddPlayerDelta(
            contributions,
            fullSnapshot.Players,
            baseline?.Players ?? Array.Empty<SnapshotPlayerState>(),
            PlayerUpdatePriority,
            client.Slot,
            focus,
            frame);

        AddSentryDelta(
            contributions,
            fullSnapshot.Sentries,
            baseline?.Sentries,
            priority: 1200,
            focus);
        AddEntityDelta(
            contributions,
            fullSnapshot.JumpPads,
            baseline?.JumpPads,
            priority: 1195,
            estimateUpdatedBytes: static state => 22,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.JumpPads.Add(state),
            static (builder, id) => builder.RemovedJumpPadIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.Rockets,
            baseline?.Rockets,
            priority: 1120,
            estimateUpdatedBytes: EstimateRocketBytes,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Rockets.Add(state),
            static (builder, id) => builder.RemovedRocketIds.Add(id),
            addedStateKind: SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.Flames,
            baseline?.Flames,
            priority: 1110,
            estimateUpdatedBytes: static state => 57,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Flames.Add(state),
            static (builder, id) => builder.RemovedFlameIds.Add(id),
            (state, baselineState, currentFrame, id) => ShouldSkipScheduledProjectileUpdate(state, baselineState, currentFrame, id, IsFlameMotionOnlyChange),
            frame,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.Flares,
            baseline?.Flares,
            priority: 1105,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Flares.Add(state),
            static (builder, id) => builder.RemovedFlareIds.Add(id),
            (state, baselineState, currentFrame, id) => ShouldSkipScheduledProjectileUpdate(state, baselineState, currentFrame, id, IsShotMotionOnlyChange),
            frame,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.Mines,
            baseline?.Mines,
            priority: 1100,
            estimateUpdatedBytes: static state => 31,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Mines.Add(state),
            static (builder, id) => builder.RemovedMineIds.Add(id),
            (state, baselineState, currentFrame, id) => ShouldSkipScheduledProjectileUpdate(state, baselineState, currentFrame, id, IsMineMotionOnlyChange),
            frame,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.Shots,
            baseline?.Shots,
            priority: 1080,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Shots.Add(state),
            static (builder, id) => builder.RemovedShotIds.Add(id),
            (state, baselineState, currentFrame, id) => ShouldSkipScheduledProjectileUpdate(state, baselineState, currentFrame, id, IsShotMotionOnlyChange),
            frame,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.Needles,
            baseline?.Needles,
            priority: 1070,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Needles.Add(state),
            static (builder, id) => builder.RemovedNeedleIds.Add(id),
            (state, baselineState, currentFrame, id) => ShouldSkipScheduledProjectileUpdate(state, baselineState, currentFrame, id, IsShotMotionOnlyChange),
            frame,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.RevolverShots,
            baseline?.RevolverShots,
            priority: 1060,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.RevolverShots.Add(state),
            static (builder, id) => builder.RemovedRevolverShotIds.Add(id),
            (state, baselineState, currentFrame, id) => ShouldSkipScheduledProjectileUpdate(state, baselineState, currentFrame, id, IsShotMotionOnlyChange),
            frame,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.Bubbles,
            baseline?.Bubbles,
            priority: 1050,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Bubbles.Add(state),
            static (builder, id) => builder.RemovedBubbleIds.Add(id),
            (state, baselineState, currentFrame, id) => ShouldSkipScheduledProjectileUpdate(state, baselineState, currentFrame, id, IsShotMotionOnlyChange),
            frame,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.Blades,
            baseline?.Blades,
            priority: 1040,
            estimateUpdatedBytes: static state => 29,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.Blades.Add(state),
            static (builder, id) => builder.RemovedBladeIds.Add(id),
            (state, baselineState, currentFrame, id) => ShouldSkipScheduledProjectileUpdate(state, baselineState, currentFrame, id, IsShotMotionOnlyChange),
            frame,
            SnapshotDeltaBudgeter.ContributionKind.ProjectileSpawn);
        AddEntityDelta(
            contributions,
            fullSnapshot.DeadBodies,
            baseline?.DeadBodies,
            priority: 440,
            estimateUpdatedBytes: static state => 40,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.DeadBodies.Add(state),
            static (builder, id) => builder.RemovedDeadBodyIds.Add(id));
        AddEntityDelta(
            contributions,
            fullSnapshot.SentryGibs,
            baseline?.SentryGibs,
            priority: 360,
            estimateUpdatedBytes: static state => 17,
            estimatedRemovedBytes: 4,
            focus,
            static state => state.Id,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.SentryGibs.Add(state),
            static (builder, id) => builder.RemovedSentryGibIds.Add(id),
            (state, baselineState, currentFrame, id) => ((currentFrame + id) % CosmeticEntityUpdateIntervalTicks) != 0,
            frame);
        AddPointEventContributions(
            contributions,
            fullSnapshot.SoundEvents,
            priority: 850,
            estimateBytes: EstimateSoundEventBytes,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.SoundEvents.Add(state),
            maxDistanceFromFocus: MaxEventDistanceFromFocus);
        AddPointEventContributions(
            contributions,
            fullSnapshot.VisualEvents,
            priority: 840,
            estimateBytes: EstimateVisualEventBytes,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.VisualEvents.Add(state),
            maxDistanceFromFocus: MaxEventDistanceFromFocus);
        AddPointEventContributions(
            contributions,
            fullSnapshot.DamageEvents,
            priority: 830,
            estimateBytes: static state => 42,
            focus,
            static state => state.X,
            static state => state.Y,
            static (builder, state) => builder.DamageEvents.Add(state),
            maxDistanceFromFocus: MaxEventDistanceFromFocus);
        AddOrderedContributions(
            contributions,
            fullSnapshot.KillFeed,
            priority: 1180,
            estimateBytes: EstimateKillFeedBytes,
            static (builder, entry) => builder.KillFeed.Add(entry));

        return contributions;
    }

    private static (float X, float Y) GetClientFocusPoint(ClientSession client, SimulationWorld world)
    {
        if (SimulationWorld.IsPlayableNetworkPlayerSlot(client.Slot)
            && world.TryGetNetworkPlayer(client.Slot, out var player)
            && player.IsAlive)
        {
            // When using binoculars, use midpoint between player and binoculars focus
            // This ensures entities near both the player and the viewed area are included
            if (player.IsUsingBinoculars)
            {
                return (
                    (player.X + player.BinocularsFocusX) / 2f,
                    (player.Y + player.BinocularsFocusY) / 2f);
            }
            
            return (player.X, player.Y);
        }

        var deathCam = world.GetNetworkPlayerDeathCam(client.Slot);
        if (deathCam is not null)
        {
            return (deathCam.FocusX, deathCam.FocusY);
        }

        return (world.Bounds.Width / 2f, world.Bounds.Height / 2f);
    }

    private static void AddPlayerDelta(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<SnapshotPlayerState> currentPlayers,
        IReadOnlyList<SnapshotPlayerState> baselinePlayers,
        int priority,
        byte viewerSlot,
        (float X, float Y) focus,
        long frame)
    {
        var currentBySlot = new Dictionary<int, SnapshotPlayerState>(currentPlayers.Count);
        for (var index = 0; index < currentPlayers.Count; index += 1)
        {
            var player = currentPlayers[index];
            currentBySlot[player.Slot] = player;
        }

        var baselineBySlot = new Dictionary<int, SnapshotPlayerState>(baselinePlayers.Count);
        for (var index = 0; index < baselinePlayers.Count; index += 1)
        {
            var player = baselinePlayers[index];
            baselineBySlot[player.Slot] = player;
            if (currentBySlot.ContainsKey(player.Slot))
            {
                continue;
            }

            var removedSlot = player.Slot;
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority + AddedPlayerUpdatePriorityBonus,
                0f,
                RemovedPlayerEstimatedBytes,
                builder => builder.RemovedPlayerIds.Add(removedSlot),
                SnapshotDeltaBudgeter.ContributionKind.PlayerRemoval));
        }

        for (var index = 0; index < currentPlayers.Count; index += 1)
        {
            var player = currentPlayers[index];
            var isAddedPlayer = !baselineBySlot.TryGetValue(player.Slot, out var baselinePlayer);
            if (!isAddedPlayer && EqualityComparer<SnapshotPlayerState>.Default.Equals(player, baselinePlayer))
            {
                continue;
            }

            var playerPriority = player.Slot == viewerSlot
                ? priority + LocalPlayerUpdatePriorityBonus
                : priority;
            if (isAddedPlayer)
            {
                playerPriority += AddedPlayerUpdatePriorityBonus;

                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    playerPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    EstimatePlayerBytes(player),
                    builder => builder.Players.Add(player),
                    SnapshotDeltaBudgeter.ContributionKind.PlayerFirstAppearance));
                continue;
            }

            if (IsPlayerRosterCriticalChange(player, baselinePlayer!))
            {
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    playerPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    EstimatePlayerBytes(player),
                    builder => builder.Players.Add(player),
                    SnapshotDeltaBudgeter.ContributionKind.PlayerRosterUpdate));
                continue;
            }

            if (HasPlayerMovementChanged(player, baselinePlayer!))
            {
                var movementState = ToPlayerMovementState(player);
                var movementKind = player.Slot == viewerSlot
                    ? SnapshotDeltaBudgeter.ContributionKind.LocalPlayerUpdate
                    : SnapshotDeltaBudgeter.ContributionKind.PlayerMovementUpdate;
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    playerPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    SnapshotPlayerMovementBytes,
                    builder => builder.PlayerMovementStates.Add(movementState),
                    movementKind));
            }

            if (HasPlayerStatusChanged(player, baselinePlayer!))
            {
                var statusState = ToPlayerStatusState(player);
                var statusPriority = player.Slot == viewerSlot
                    ? playerPriority + LocalPlayerStatusPriorityBonus
                    : playerPriority - 50;
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    statusPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    SnapshotPlayerStatusBytes,
                    builder => builder.PlayerStatusStates.Add(statusState),
                    player.Slot == viewerSlot
                        ? SnapshotDeltaBudgeter.ContributionKind.LocalPlayerStatusUpdate
                        : SnapshotDeltaBudgeter.ContributionKind.Optional));
            }

            if (HasPlayerExtendedStatusChanged(player, baselinePlayer!))
            {
                var extendedStatusState = ToPlayerExtendedStatusState(player);
                var extendedStatusPriority = player.Slot == viewerSlot
                    ? playerPriority + LocalPlayerStatusPriorityBonus - 20
                    : playerPriority - 25;
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    extendedStatusPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    SnapshotPlayerExtendedStatusBytes,
                    builder => builder.PlayerExtendedStatusStates.Add(extendedStatusState),
                    SnapshotDeltaBudgeter.ContributionKind.PlayerExtendedStatusUpdate));
            }

            if (HasPlayerChatBubbleChanged(player, baselinePlayer!))
            {
                var chatBubbleState = ToPlayerChatBubbleState(player);
                var chatBubblePriority = PlayerChatBubblePriority + (chatBubbleState.IsChatBubbleVisible ? 100 : 0);
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    chatBubblePriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    SnapshotPlayerChatBubbleBytes,
                    builder => builder.PlayerChatBubbleStates.Add(chatBubbleState),
                    SnapshotDeltaBudgeter.ContributionKind.PlayerChatBubbleUpdate));
            }

            if (HasPlayerImmediateDetailChanged(player, baselinePlayer!))
            {
                var detailPriority = player.Slot == viewerSlot
                    ? PlayerDetailUpdatePriority + 50
                    : PlayerDetailUpdatePriority;
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    detailPriority,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    EstimatePlayerBytes(player),
                    builder => builder.Players.Add(player)));
            }
            else if (HasPlayerLowFrequencyDetailChanged(player, baselinePlayer!)
                && ShouldSendLowFrequencyPlayerDetail(frame, player.Slot))
            {
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    PlayerDetailUpdatePriority - 120,
                    DistanceSquared(focus.X, focus.Y, player.X, player.Y),
                    EstimatePlayerBytes(player),
                    builder => builder.Players.Add(player)));
            }
        }
    }

    private static SnapshotPlayerMovementState ToPlayerMovementState(SnapshotPlayerState player)
    {
        return new SnapshotPlayerMovementState(
            player.Slot,
            player.X,
            player.Y,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.IsGrounded,
            player.RemainingAirJumps,
            player.FacingDirectionX,
            player.AimDirectionDegrees,
            player.MovementState,
            player.IsTaunting,
            player.TauntFrameIndex,
            player.BurnIntensity,
            player.GameplayEquippedSlot,
            player.PrimaryCooldownTicks,
            player.ReloadTicksUntilNextShell,
            player.OffhandCooldownTicks,
            player.OffhandReloadTicks,
            player.MedicHealTargetId,
            player.IsMedicHealing);
    }

    private static SnapshotPlayerStatusState ToPlayerStatusState(SnapshotPlayerState player)
    {
        return new SnapshotPlayerStatusState(
            player.Slot,
            player.Health,
            player.MaxHealth,
            player.Ammo,
            player.MaxAmmo,
            player.Metal,
            player.IsCarryingIntel,
            player.IntelRechargeTicks);
    }

    private static SnapshotPlayerChatBubbleState ToPlayerChatBubbleState(SnapshotPlayerState player)
    {
        return new SnapshotPlayerChatBubbleState(
            player.Slot,
            player.IsChatBubbleVisible,
            player.ChatBubbleFrameIndex,
            player.ChatBubbleAlpha);
    }

    private static SnapshotPlayerExtendedStatusState ToPlayerExtendedStatusState(SnapshotPlayerState player)
    {
        return new SnapshotPlayerExtendedStatusState(
            player.Slot,
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
            player.MedicNeedleCooldownTicks,
            player.MedicNeedleRefillTicks,
            player.PyroAirblastCooldownTicks,
            player.PyroFlareCooldownTicks,
            player.PyroPrimaryFuelScaled,
            player.IsPyroPrimaryRefilling,
            player.PyroFlameLoopTicksRemaining,
            player.PyroPrimaryRequiresReleaseAfterEmpty,
            player.HeavyEatCooldownTicksRemaining,
            player.MedicUberCharge,
            player.IsMedicUberReady);
    }

    private static bool HasPlayerMovementChanged(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        return player.X != baselinePlayer.X
            || player.Y != baselinePlayer.Y
            || player.HorizontalSpeed != baselinePlayer.HorizontalSpeed
            || player.VerticalSpeed != baselinePlayer.VerticalSpeed
            || player.IsGrounded != baselinePlayer.IsGrounded
            || player.RemainingAirJumps != baselinePlayer.RemainingAirJumps
            || player.FacingDirectionX != baselinePlayer.FacingDirectionX
            || player.AimDirectionDegrees != baselinePlayer.AimDirectionDegrees
            || player.MedicHealTargetId != baselinePlayer.MedicHealTargetId
            || player.IsMedicHealing != baselinePlayer.IsMedicHealing
            || player.MovementState != baselinePlayer.MovementState
            || player.IsTaunting != baselinePlayer.IsTaunting
            || player.TauntFrameIndex != baselinePlayer.TauntFrameIndex
            || player.BurnIntensity != baselinePlayer.BurnIntensity
            || player.GameplayEquippedSlot != baselinePlayer.GameplayEquippedSlot
            || player.PrimaryCooldownTicks != baselinePlayer.PrimaryCooldownTicks
            || player.ReloadTicksUntilNextShell != baselinePlayer.ReloadTicksUntilNextShell
            || player.OffhandCooldownTicks != baselinePlayer.OffhandCooldownTicks
            || player.OffhandReloadTicks != baselinePlayer.OffhandReloadTicks;
    }

    private static bool HasPlayerImmediateDetailChanged(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        var normalizedPlayer = NormalizePlayerForImmediateDetailComparison(player, baselinePlayer);
        return !EqualityComparer<SnapshotPlayerState>.Default.Equals(normalizedPlayer, baselinePlayer);
    }

    private static SnapshotPlayerState NormalizePlayerForImmediateDetailComparison(
        SnapshotPlayerState player,
        SnapshotPlayerState baselinePlayer)
    {
        return player with
        {
            X = baselinePlayer.X,
            Y = baselinePlayer.Y,
            HorizontalSpeed = baselinePlayer.HorizontalSpeed,
            VerticalSpeed = baselinePlayer.VerticalSpeed,
            IsGrounded = baselinePlayer.IsGrounded,
            RemainingAirJumps = baselinePlayer.RemainingAirJumps,
            FacingDirectionX = baselinePlayer.FacingDirectionX,
            AimDirectionDegrees = baselinePlayer.AimDirectionDegrees,
            MedicHealTargetId = baselinePlayer.MedicHealTargetId,
            IsMedicHealing = baselinePlayer.IsMedicHealing,
            MovementState = baselinePlayer.MovementState,
            IsTaunting = baselinePlayer.IsTaunting,
            TauntFrameIndex = baselinePlayer.TauntFrameIndex,
            BurnIntensity = baselinePlayer.BurnIntensity,
            GameplayEquippedSlot = baselinePlayer.GameplayEquippedSlot,
            PrimaryCooldownTicks = baselinePlayer.PrimaryCooldownTicks,
            ReloadTicksUntilNextShell = baselinePlayer.ReloadTicksUntilNextShell,
            OffhandCooldownTicks = baselinePlayer.OffhandCooldownTicks,
            OffhandReloadTicks = baselinePlayer.OffhandReloadTicks,
            Health = baselinePlayer.Health,
            MaxHealth = baselinePlayer.MaxHealth,
            Ammo = baselinePlayer.Ammo,
            MaxAmmo = baselinePlayer.MaxAmmo,
            Metal = baselinePlayer.Metal,
            IsCarryingIntel = baselinePlayer.IsCarryingIntel,
            IntelRechargeTicks = baselinePlayer.IntelRechargeTicks,
            IsSpyCloaked = baselinePlayer.IsSpyCloaked,
            SpyCloakAlpha = baselinePlayer.SpyCloakAlpha,
            IsSpySuperjumping = baselinePlayer.IsSpySuperjumping,
            SpySuperjumpHorizontalVelocity = baselinePlayer.SpySuperjumpHorizontalVelocity,
            SpySuperjumpCooldownTicksRemaining = baselinePlayer.SpySuperjumpCooldownTicksRemaining,
            SpyBackstabVisualTicksRemaining = baselinePlayer.SpyBackstabVisualTicksRemaining,
            IsUbered = baselinePlayer.IsUbered,
            IsKritzCritBoosted = baselinePlayer.IsKritzCritBoosted,
            IsHeavyEating = baselinePlayer.IsHeavyEating,
            HeavyEatTicksRemaining = baselinePlayer.HeavyEatTicksRemaining,
            IsSniperScoped = baselinePlayer.IsSniperScoped,
            SniperChargeTicks = baselinePlayer.SniperChargeTicks,
            MedicNeedleCooldownTicks = baselinePlayer.MedicNeedleCooldownTicks,
            MedicNeedleRefillTicks = baselinePlayer.MedicNeedleRefillTicks,
            PyroAirblastCooldownTicks = baselinePlayer.PyroAirblastCooldownTicks,
            PyroFlareCooldownTicks = baselinePlayer.PyroFlareCooldownTicks,
            PyroPrimaryFuelScaled = baselinePlayer.PyroPrimaryFuelScaled,
            IsPyroPrimaryRefilling = baselinePlayer.IsPyroPrimaryRefilling,
            PyroFlameLoopTicksRemaining = baselinePlayer.PyroFlameLoopTicksRemaining,
            PyroPrimaryRequiresReleaseAfterEmpty = baselinePlayer.PyroPrimaryRequiresReleaseAfterEmpty,
            HeavyEatCooldownTicksRemaining = baselinePlayer.HeavyEatCooldownTicksRemaining,
            MedicUberCharge = baselinePlayer.MedicUberCharge,
            IsMedicUberReady = baselinePlayer.IsMedicUberReady,
            IsChatBubbleVisible = baselinePlayer.IsChatBubbleVisible,
            ChatBubbleFrameIndex = baselinePlayer.ChatBubbleFrameIndex,
            ChatBubbleAlpha = baselinePlayer.ChatBubbleAlpha,
            Kills = baselinePlayer.Kills,
            Deaths = baselinePlayer.Deaths,
            Caps = baselinePlayer.Caps,
            Points = baselinePlayer.Points,
            HealPoints = baselinePlayer.HealPoints,
            ActiveDominationCount = baselinePlayer.ActiveDominationCount,
            IsDominatingLocalViewer = baselinePlayer.IsDominatingLocalViewer,
            IsDominatedByLocalViewer = baselinePlayer.IsDominatedByLocalViewer,
            Assists = baselinePlayer.Assists,
            BadgeMask = baselinePlayer.BadgeMask,
            OwnedGameplayItemIds = baselinePlayer.OwnedGameplayItemIds,
            GibDeaths = baselinePlayer.GibDeaths,
        };
    }

    private static bool HasPlayerLowFrequencyDetailChanged(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        return player.Kills != baselinePlayer.Kills
            || player.Deaths != baselinePlayer.Deaths
            || player.Caps != baselinePlayer.Caps
            || player.Points != baselinePlayer.Points
            || player.HealPoints != baselinePlayer.HealPoints
            || player.ActiveDominationCount != baselinePlayer.ActiveDominationCount
            || player.IsDominatingLocalViewer != baselinePlayer.IsDominatingLocalViewer
            || player.IsDominatedByLocalViewer != baselinePlayer.IsDominatedByLocalViewer
            || player.Assists != baselinePlayer.Assists
            || player.BadgeMask != baselinePlayer.BadgeMask
            || !GameplayIdListsEqual(player.OwnedGameplayItemIds, baselinePlayer.OwnedGameplayItemIds)
            || player.GibDeaths != baselinePlayer.GibDeaths;
    }

    private static bool HasPlayerExtendedStatusChanged(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        return player.IsSpyCloaked != baselinePlayer.IsSpyCloaked
            || player.SpyCloakAlpha != baselinePlayer.SpyCloakAlpha
            || player.IsSpySuperjumping != baselinePlayer.IsSpySuperjumping
            || player.SpySuperjumpHorizontalVelocity != baselinePlayer.SpySuperjumpHorizontalVelocity
            || player.SpySuperjumpCooldownTicksRemaining != baselinePlayer.SpySuperjumpCooldownTicksRemaining
            || player.SpyBackstabVisualTicksRemaining != baselinePlayer.SpyBackstabVisualTicksRemaining
            || player.IsUbered != baselinePlayer.IsUbered
            || player.IsKritzCritBoosted != baselinePlayer.IsKritzCritBoosted
            || player.IsHeavyEating != baselinePlayer.IsHeavyEating
            || player.HeavyEatTicksRemaining != baselinePlayer.HeavyEatTicksRemaining
            || player.IsSniperScoped != baselinePlayer.IsSniperScoped
            || player.SniperChargeTicks != baselinePlayer.SniperChargeTicks
            || player.MedicNeedleCooldownTicks != baselinePlayer.MedicNeedleCooldownTicks
            || player.MedicNeedleRefillTicks != baselinePlayer.MedicNeedleRefillTicks
            || player.PyroAirblastCooldownTicks != baselinePlayer.PyroAirblastCooldownTicks
            || player.PyroFlareCooldownTicks != baselinePlayer.PyroFlareCooldownTicks
            || player.PyroPrimaryFuelScaled != baselinePlayer.PyroPrimaryFuelScaled
            || player.IsPyroPrimaryRefilling != baselinePlayer.IsPyroPrimaryRefilling
            || player.PyroFlameLoopTicksRemaining != baselinePlayer.PyroFlameLoopTicksRemaining
            || player.PyroPrimaryRequiresReleaseAfterEmpty != baselinePlayer.PyroPrimaryRequiresReleaseAfterEmpty
            || player.HeavyEatCooldownTicksRemaining != baselinePlayer.HeavyEatCooldownTicksRemaining
            || player.MedicUberCharge != baselinePlayer.MedicUberCharge
            || player.IsMedicUberReady != baselinePlayer.IsMedicUberReady;
    }

    private static bool ShouldSendLowFrequencyPlayerDetail(long frame, byte slot)
    {
        return ((frame + slot) % LowFrequencyPlayerDetailRefreshIntervalTicks) == 0;
    }

    private static bool GameplayIdListsEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        var leftCount = left?.Count ?? 0;
        var rightCount = right?.Count ?? 0;
        if (leftCount != rightCount)
        {
            return false;
        }

        for (var index = 0; index < leftCount; index += 1)
        {
            if (!string.Equals(left![index], right![index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasPlayerStatusChanged(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        return player.Health != baselinePlayer.Health
            || player.MaxHealth != baselinePlayer.MaxHealth
            || player.Ammo != baselinePlayer.Ammo
            || player.MaxAmmo != baselinePlayer.MaxAmmo
            || player.Metal != baselinePlayer.Metal
            || player.IsCarryingIntel != baselinePlayer.IsCarryingIntel
            || player.IntelRechargeTicks != baselinePlayer.IntelRechargeTicks;
    }

    private static bool HasPlayerChatBubbleChanged(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        return player.IsChatBubbleVisible != baselinePlayer.IsChatBubbleVisible
            || player.ChatBubbleFrameIndex != baselinePlayer.ChatBubbleFrameIndex
            || player.ChatBubbleAlpha != baselinePlayer.ChatBubbleAlpha;
    }

    private static bool IsPlayerRosterCriticalChange(SnapshotPlayerState player, SnapshotPlayerState baselinePlayer)
    {
        return player.PlayerId != baselinePlayer.PlayerId
            || !string.Equals(player.Name, baselinePlayer.Name, StringComparison.Ordinal)
            || player.Team != baselinePlayer.Team
            || player.ClassId != baselinePlayer.ClassId
            || player.IsAlive != baselinePlayer.IsAlive
            || player.IsAwaitingJoin != baselinePlayer.IsAwaitingJoin
            || player.IsSpectator != baselinePlayer.IsSpectator
            || player.PlayerScale != baselinePlayer.PlayerScale;
    }

    private static void AddEntityDelta<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> currentStates,
        IReadOnlyList<T>? baselineStates,
        int priority,
        Func<T, int> estimateUpdatedBytes,
        int estimatedRemovedBytes,
        (float X, float Y) focus,
        Func<T, int> idSelector,
        Func<T, float> xSelector,
        Func<T, float> ySelector,
        Action<SnapshotDeltaBudgeter.Builder, T> addState,
        Action<SnapshotDeltaBudgeter.Builder, int> addRemovedId,
        Func<T, T, long, int, bool>? shouldSkipMotionOnlyUpdate = null,
        long currentWorldFrame = 0,
        SnapshotDeltaBudgeter.ContributionKind addedStateKind = SnapshotDeltaBudgeter.ContributionKind.Optional) where T : notnull
    {
        var delta = DiffEntities(currentStates, baselineStates, idSelector);
        Dictionary<int, T>? baselineById = null;
        if (baselineStates is not null)
        {
            baselineById = new Dictionary<int, T>(baselineStates.Count);
            for (var index = 0; index < baselineStates.Count; index += 1)
            {
                var state = baselineStates[index];
                baselineById[idSelector(state)] = state;
            }
        }

        for (var index = 0; index < delta.RemovedIds.Count; index += 1)
        {
            var removedId = delta.RemovedIds[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority + 100,
                DistanceSquared(focus.X, focus.Y, focus.X, focus.Y),
                estimatedRemovedBytes,
                builder => addRemovedId(builder, removedId),
                SnapshotDeltaBudgeter.ContributionKind.EntityRemoval));
        }

        for (var index = 0; index < delta.UpdatedStates.Count; index += 1)
        {
            var state = delta.UpdatedStates[index];
            T? baselineState = default;
            var isKnownEntity = baselineById is not null
                && baselineById.TryGetValue(idSelector(state), out baselineState);
            if (isKnownEntity
                && shouldSkipMotionOnlyUpdate is not null
                && baselineState is not null
                && shouldSkipMotionOnlyUpdate(state, baselineState, currentWorldFrame, idSelector(state)))
            {
                continue;
            }

            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority,
                DistanceSquared(focus.X, focus.Y, xSelector(state), ySelector(state)),
                estimateUpdatedBytes(state),
                builder => addState(builder, state),
                isKnownEntity ? SnapshotDeltaBudgeter.ContributionKind.Optional : addedStateKind));
        }
    }

    private static bool ShouldSkipScheduledProjectileUpdate<T>(
        T state,
        T baselineState,
        long currentWorldFrame,
        int entityId,
        Func<T, T, bool> isMotionOnlyChange)
    {
        if (!isMotionOnlyChange(state, baselineState))
        {
            return false;
        }

        return ((currentWorldFrame + entityId) % ProjectileSnapshotUpdateIntervalTicks) != 0;
    }

    private static bool IsShotMotionOnlyChange(SnapshotShotState state, SnapshotShotState baselineState)
    {
        return EqualityComparer<SnapshotShotState>.Default.Equals(
            state with
            {
                X = baselineState.X,
                Y = baselineState.Y,
                VelocityX = baselineState.VelocityX,
                VelocityY = baselineState.VelocityY,
                TicksRemaining = baselineState.TicksRemaining,
            },
            baselineState);
    }

    private static bool IsRocketMotionOnlyChange(SnapshotRocketState state, SnapshotRocketState baselineState)
    {
        return EqualityComparer<SnapshotRocketState>.Default.Equals(
            state with
            {
                X = baselineState.X,
                Y = baselineState.Y,
                PreviousX = baselineState.PreviousX,
                PreviousY = baselineState.PreviousY,
                DirectionRadians = baselineState.DirectionRadians,
                Speed = baselineState.Speed,
                TicksRemaining = baselineState.TicksRemaining,
                ReducedKnockbackSourceTicksRemaining = baselineState.ReducedKnockbackSourceTicksRemaining,
                ZeroKnockbackSourceTicksRemaining = baselineState.ZeroKnockbackSourceTicksRemaining,
                LastKnownRangeOriginX = baselineState.LastKnownRangeOriginX,
                LastKnownRangeOriginY = baselineState.LastKnownRangeOriginY,
                DistanceToTravel = baselineState.DistanceToTravel,
                FadeSourceTicksRemaining = baselineState.FadeSourceTicksRemaining,
            },
            baselineState);
    }

    private static bool IsFlameMotionOnlyChange(SnapshotFlameState state, SnapshotFlameState baselineState)
    {
        return EqualityComparer<SnapshotFlameState>.Default.Equals(
            state with
            {
                X = baselineState.X,
                Y = baselineState.Y,
                PreviousX = baselineState.PreviousX,
                PreviousY = baselineState.PreviousY,
                VelocityX = baselineState.VelocityX,
                VelocityY = baselineState.VelocityY,
                TicksRemaining = baselineState.TicksRemaining,
            },
            baselineState);
    }

    private static bool IsMineMotionOnlyChange(SnapshotMineState state, SnapshotMineState baselineState)
    {
        return EqualityComparer<SnapshotMineState>.Default.Equals(
            state with
            {
                X = baselineState.X,
                Y = baselineState.Y,
                VelocityX = baselineState.VelocityX,
                VelocityY = baselineState.VelocityY,
            },
            baselineState);
    }

    private static void AddPointEventContributions<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> states,
        int priority,
        Func<T, int> estimateBytes,
        (float X, float Y) focus,
        Func<T, float> xSelector,
        Func<T, float> ySelector,
        Action<SnapshotDeltaBudgeter.Builder, T> addState,
        float maxDistanceFromFocus = float.MaxValue)
    {
        var maxDistanceSquared = maxDistanceFromFocus * maxDistanceFromFocus;
        for (var index = states.Count - 1; index >= 0; index -= 1)
        {
            var state = states[index];
            var distanceSquared = DistanceSquared(focus.X, focus.Y, xSelector(state), ySelector(state));
            if (distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority - ((states.Count - 1) - index),
                distanceSquared,
                estimateBytes(state),
                builder => addState(builder, state)));
        }
    }

    private static void AddOrderedContributions<T>(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<T> states,
        int priority,
        Func<T, int> estimateBytes,
        Action<SnapshotDeltaBudgeter.Builder, T> addState)
    {
        for (var index = states.Count - 1; index >= 0; index -= 1)
        {
            var state = states[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority - ((states.Count - 1) - index),
                0f,
                estimateBytes(state),
                builder => addState(builder, state)));
        }
    }

    private static int EstimateSoundEventBytes(SnapshotSoundEvent state)
    {
        return 26 + state.SoundName.Length;
    }

    private static int EstimateVisualEventBytes(SnapshotVisualEvent state)
    {
        return 26 + state.EffectName.Length;
    }

    private static int EstimateKillFeedBytes(SnapshotKillFeedEntry entry)
    {
        return 35
            + entry.KillerName.Length
            + entry.WeaponSpriteName.Length
            + entry.VictimName.Length
            + entry.MessageText.Length;
    }

    private static int EstimatePlayerBytes(SnapshotPlayerState player)
    {
        return SnapshotPlayerFixedBytes
            + EstimateStringBytes(player.Name)
            + EstimateStringBytes(player.GameplayModPackId)
            + EstimateStringBytes(player.GameplayLoadoutId)
            + EstimateStringBytes(player.GameplayPrimaryItemId)
            + EstimateStringBytes(player.GameplaySecondaryItemId)
            + EstimateStringBytes(player.GameplayUtilityItemId)
            + EstimateStringBytes(player.GameplayEquippedItemId)
            + EstimateStringBytes(player.GameplayAcquiredItemId)
            + EstimateGameplayIdListBytes(player.OwnedGameplayItemIds)
            + EstimateReplicatedStateEntryBytes(player.ReplicatedStates);
    }

    private static int EstimateGameplayIdListBytes(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count == 0)
        {
            return 1;
        }

        var bytes = 1;
        for (var index = 0; index < ids.Count; index += 1)
        {
            bytes += EstimateStringBytes(ids[index]);
        }

        return bytes;
    }

    private static int EstimateReplicatedStateEntryBytes(IReadOnlyList<SnapshotReplicatedStateEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return 1;
        }

        var bytes = 1;
        for (var index = 0; index < entries.Count; index += 1)
        {
            var entry = entries[index];
            bytes += EstimateStringBytes(entry.OwnerId)
                + EstimateStringBytes(entry.Key)
                + 10;
        }

        return bytes;
    }

    private static int EstimateStringBytes(string value)
    {
        return 2 + Encoding.UTF8.GetByteCount(value);
    }

    private static int EstimatePlayerGibBytes(SnapshotPlayerGibState state)
    {
        return 42 + state.SpriteName.Length;
    }

    private static int EstimateRocketBytes(SnapshotRocketState state)
    {
        var passedFriendlyPlayerCount = state.PassedFriendlyPlayerIds?.Count ?? 0;
        return 68 + (passedFriendlyPlayerCount * 4);
    }

    private static EntityDelta<T> DiffEntities<T>(
        IReadOnlyList<T> currentStates,
        IReadOnlyList<T>? baselineStates,
        Func<T, int> idSelector) where T : notnull
    {
        var updatedStates = new List<T>(currentStates.Count);
        if (baselineStates is null || baselineStates.Count == 0)
        {
            updatedStates.AddRange(currentStates);
            return new EntityDelta<T>(updatedStates, []);
        }

        var currentById = new Dictionary<int, T>(currentStates.Count);
        for (var index = 0; index < currentStates.Count; index += 1)
        {
            var state = currentStates[index];
            currentById[idSelector(state)] = state;
        }

        var baselineById = new Dictionary<int, T>(baselineStates.Count);
        for (var index = 0; index < baselineStates.Count; index += 1)
        {
            var state = baselineStates[index];
            baselineById[idSelector(state)] = state;
        }

        var removedIds = new List<int>();
        foreach (var baselineState in baselineStates)
        {
            var id = idSelector(baselineState);
            if (!currentById.ContainsKey(id))
            {
                removedIds.Add(id);
            }
        }

        for (var index = 0; index < currentStates.Count; index += 1)
        {
            var state = currentStates[index];
            var id = idSelector(state);
            if (!baselineById.TryGetValue(id, out var baselineState)
                || !EqualityComparer<T>.Default.Equals(state, baselineState))
            {
                updatedStates.Add(state);
            }
        }

        return new EntityDelta<T>(updatedStates, removedIds);
    }

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static void AddSentryDelta(
        List<SnapshotDeltaBudgeter.Contribution> contributions,
        IReadOnlyList<SnapshotSentryState> currentStates,
        IReadOnlyList<SnapshotSentryState>? baselineStates,
        int priority,
        (float X, float Y) focus)
    {
        var delta = DiffEntities(currentStates, baselineStates, static state => state.Id);
        Dictionary<int, SnapshotSentryState>? baselineById = null;
        if (baselineStates is not null)
        {
            baselineById = new Dictionary<int, SnapshotSentryState>(baselineStates.Count);
            for (var index = 0; index < baselineStates.Count; index += 1)
            {
                var state = baselineStates[index];
                baselineById[state.Id] = state;
            }
        }

        // Add removed sentries
        for (var index = 0; index < delta.RemovedIds.Count; index += 1)
        {
            var removedId = delta.RemovedIds[index];
            contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                priority + 100,
                DistanceSquared(focus.X, focus.Y, focus.X, focus.Y),
                4, // Just the ID
                builder => builder.RemovedSentryIds.Add(removedId),
                SnapshotDeltaBudgeter.ContributionKind.EntityRemoval));
        }

        // Add updated/new sentries
        for (var index = 0; index < delta.UpdatedStates.Count; index += 1)
        {
            var current = delta.UpdatedStates[index];
            SnapshotSentryState? baseline = null;
            var isNew = baselineById is null || !baselineById.TryGetValue(current.Id, out baseline);

            if (isNew)
            {
                // New sentry - send full state (44 bytes)
                contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                    priority,
                    DistanceSquared(focus.X, focus.Y, current.X, current.Y),
                    44, // Full state: 4 (id) + 4 (owner) + 1 (team) + 8 (x,y) + 4 (health) + 1 (isBuilt) + 4 (facing) + 4 (aim) + 4 (shotTrace) + 1 (hasLanded) + 1 (hasTarget) + 8 (lastShot)
                    builder => builder.Sentries.Add(current)));
            }
            else
            {
                // Check if static fields changed
                var staticFieldsChanged = current.OwnerPlayerId != baseline!.OwnerPlayerId
                    || current.Team != baseline.Team
                    || current.IsBuilt != baseline.IsBuilt
                    || current.HasLanded != baseline.HasLanded;

                if (staticFieldsChanged)
                {
                    // Static fields changed - send full state (44 bytes)
                    contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                        priority,
                        DistanceSquared(focus.X, focus.Y, current.X, current.Y),
                        44,
                        builder => builder.Sentries.Add(current)));
                }
                else
                {
                    // Only dynamic fields changed - send lightweight update (39 bytes)
                    var update = new SnapshotSentryUpdateState(
                        current.Id,
                        current.X,
                        current.Y,
                        current.Health,
                        current.FacingDirectionX,
                        current.AimDirectionDegrees,
                        current.ShotTraceTicksRemaining,
                        current.HasActiveTarget,
                        current.LastShotTargetX,
                        current.LastShotTargetY);

                    contributions.Add(new SnapshotDeltaBudgeter.Contribution(
                        priority,
                        DistanceSquared(focus.X, focus.Y, current.X, current.Y),
                        39, // Lightweight update: 4 (id) + 8 (x,y) + 4 (health) + 4 (facing) + 4 (aim) + 4 (shotTrace) + 1 (hasTarget) + 8 (lastShot) + 2 (overhead)
                        builder => builder.SentryUpdateStates.Add(update)));
                }
            }
        }
    }

    private sealed record EntityDelta<T>(List<T> UpdatedStates, List<int> RemovedIds);
}
