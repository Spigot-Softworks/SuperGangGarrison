// Captures a SimulationWorld tick as a SnapshotMessage via the world's
// SnapshotSystem (Core/Simulation/Systems/SnapshotSystem.cs). The capture
// mirrors production's SnapshotBroadcaster converter usage so the per-tick
// SHA256 hashes in Goldens/ verify the refactor is behavior-preserving.

using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core.Tests.Simulation.Replay;

internal sealed class SnapshotCapture
{
    private readonly SnapshotStringCache _stringCache = new();
    // Harness-owned fallback IDs. Production owns its own transient-event
    // counter; this counter exists only to make this standalone capture total.
    private ulong _nextFallbackEventId = 1;

    public SnapshotMessage Capture(SimulationWorld world)
    {
        var snapshots = world.Snapshots;
        var players = new List<SnapshotPlayerState>();
        var viewer = world.LocalPlayerAwaitingJoin ? null : world.LocalPlayer;
        foreach (var slot in SimulationWorld.NetworkPlayerSlots)
        {
            if (!world.NetworkPlayers.TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            players.Add(snapshots.ToSnapshotPlayerState(
                slot,
                player,
                viewer,
                value => _stringCache.GetOrAddCacheId(value),
                world.NetworkPlayers.GetNetworkPlayerPingMilliseconds(slot),
                world.NetworkPlayers.IsNetworkPlayerBot(slot)));
        }

        var visualEvents = ConvertVisualEvents(snapshots, world.DrainPendingVisualEvents(), world.Frame);
        var soundEvents = ConvertSoundEvents(snapshots, world.DrainPendingSoundEvents());
        var damageEvents = ConvertDamageEvents(snapshots, world.DrainPendingDamageEvents());
        var gibSpawnEvents = ConvertGibSpawnEvents(world.DrainPendingGibSpawnEvents());
        var rocketSpawnEvents = ConvertRocketSpawnEvents(world.DrainPendingRocketSpawnEvents());
        // Healing is presentation-side state in this protocol and has no
        // SnapshotMessage collection, but it must still be drained per tick.
        _ = world.DrainPendingHealingEvents();

        // The protocol currently has no BloodDrops member. Convert them here
        // to keep this capture aligned with the available converter surface;
        // the values cannot participate in the serialized SnapshotMessage.
        _ = world.BloodDrops
            .Select(bloodDrop => snapshots.ToSnapshotBloodDropState(bloodDrop))
            .ToArray();

        var playerGibs = world.PlayerGibs
            .Select(gib => snapshots.ToSnapshotPlayerGibState(gib))
            .ToArray();

        return new SnapshotMessage(
            Frame: (ulong)world.Frame,
            TickRate: 30,
            LevelName: world.Level.Name,
            MapAreaIndex: (byte)Math.Clamp(world.Level.MapAreaIndex, 1, byte.MaxValue),
            MapAreaCount: (byte)Math.Clamp(world.Level.MapAreaCount, 1, byte.MaxValue),
            GameMode: (byte)world.MatchRules.Mode,
            MatchPhase: (byte)world.MatchState.Phase,
            WinnerTeam: world.MatchState.WinnerTeam.HasValue ? (byte)world.MatchState.WinnerTeam.Value : (byte)0,
            TimeRemainingTicks: world.MatchState.TimeRemainingTicks,
            RedCaps: world.RedCaps,
            BlueCaps: world.BlueCaps,
            SpectatorCount: world.SpectatorCount,
            LastProcessedInputSequence: 0,
            RedIntel: snapshots.ToSnapshotIntelState(world.ObjectiveRules.RedIntel),
            BlueIntel: snapshots.ToSnapshotIntelState(world.ObjectiveRules.BlueIntel),
            Players: players.ToArray(),
            CombatTraces: world.CombatTraces
                .Select(trace => snapshots.ToSnapshotCombatTraceState(trace))
                .ToArray(),
            SniperAimIndicators: world.SniperAimIndicators
                .Select(indicator => snapshots.ToSnapshotSniperAimIndicatorState(indicator))
                .ToArray(),
            Sentries: world.Sentries
                .Select(sentry => snapshots.ToSnapshotSentryState(sentry))
                .ToArray(),
            Shots: world.Shots
                .Select(shot => snapshots.ToSnapshotBulletState(shot))
                .ToArray(),
            Bubbles: world.Bubbles
                .Select(bubble => snapshots.ToSnapshotBubbleState(bubble))
                .ToArray(),
            Blades: world.Blades
                .Select(blade => snapshots.ToSnapshotBladeState(blade))
                .ToArray(),
            Needles: world.Needles
                .Select(needle => snapshots.ToSnapshotNeedleState(needle))
                .ToArray(),
            RevolverShots: world.RevolverShots
                .Select(shot => snapshots.ToSnapshotRevolverState(shot))
                .ToArray(),
            Rockets: world.Rockets
                .Select(rocket => snapshots.ToSnapshotRocketState(rocket))
                .ToArray(),
            Flames: world.Flames
                .Select(flame => snapshots.ToSnapshotFlameState(flame))
                .ToArray(),
            Flares: world.Flares
                .Select(flare => snapshots.ToSnapshotFlareState(flare))
                .ToArray(),
            Mines: world.Mines
                .Select(mine => snapshots.ToSnapshotMineState(mine))
                .ToArray(),
            DeadBodies: world.DeadBodies
                .Select(body => snapshots.ToSnapshotDeadBodyState(body))
                .ToArray(),
            ControlPointSetupTicksRemaining: world.ControlPointSetupTicksRemaining,
            KothUnlockTicksRemaining: world.ObjectiveRules.KothUnlockTicksRemaining,
            KothRedTimerTicksRemaining: world.ObjectiveRules.KothRedTimerTicksRemaining,
            KothBlueTimerTicksRemaining: world.ObjectiveRules.KothBlueTimerTicksRemaining,
            ControlPoints: world.ControlPoints
                .Select(point => snapshots.ToSnapshotControlPointState(point))
                .ToArray(),
            Generators: world.ObjectiveRules.Generators
                .Select(generator => snapshots.ToSnapshotGeneratorState(generator))
                .ToArray(),
            LocalDeathCam: snapshots.ToSnapshotDeathCamState(
                world.GetNetworkPlayerDeathCam(SimulationWorld.LocalPlayerSlot)),
            KillFeed: world.KillFeedEntries
                .Select(entry => snapshots.ToSnapshotKillFeedEntry(entry))
                .ToArray(),
            VisualEvents: visualEvents,
            DamageEvents: damageEvents,
            SoundEvents: soundEvents,
            MapScale: world.Level.MapScale)
        {
            TimeLimitTicks = world.MatchRules.TimeLimitTicks,
            ArenaUnlockTicksRemaining = world.ArenaUnlockTicksRemaining,
            ArenaPointTeam = world.ArenaPointTeam.HasValue ? (byte)world.ArenaPointTeam.Value : (byte)0,
            ArenaCappingTeam = world.ArenaCappingTeam.HasValue ? (byte)world.ArenaCappingTeam.Value : (byte)0,
            ArenaCappingTicks = world.ArenaCappingTicks,
            ArenaCappers = world.ArenaCappers,
            ArenaRedConsecutiveWins = world.ArenaRedConsecutiveWins,
            ArenaBlueConsecutiveWins = world.ArenaBlueConsecutiveWins,
            CompetitiveReadyUpPhase = (byte)world.ReadyUp.CompetitiveReadyUpPhase,
            CompetitiveReadyUpTicksRemaining = world.ReadyUp.CompetitiveReadyUpTicksRemaining,
            CapLimit = world.MatchRules.CapLimit,
            ScoreboardPlayers = players.ToArray(),
            SentryGibs = world.SentryGibs
                .Select(gib => snapshots.ToSnapshotSentryGibState(gib))
                .ToArray(),
            JumpPads = world.JumpPads
                .Select(pad => snapshots.ToSnapshotJumpPadState(pad))
                .ToArray(),
            CivilDefenseTurrets = world.CivilDefenseTurrets
                .Select(turret => snapshots.ToSnapshotCivilDefenseTurretState(turret))
                .ToArray(),
            JumpPadGibs = world.JumpPadGibs
                .Select(gib => snapshots.ToSnapshotJumpPadGibState(gib))
                .ToArray(),
            Grenades = world.Grenades
                .Select(grenade => snapshots.ToSnapshotGrenadeState(grenade))
                .ToArray(),
            HealthPacks = snapshots.ToSnapshotHealthPackStates(
                world.HealthPacks,
                world.Level.HealthPackSpawns,
                world.Pickups.GetHealthPackSpawnRespawnTicksRemaining),
            PlayerGibs = playerGibs,
            GibSpawnEvents = gibSpawnEvents,
            RocketSpawnEvents = rocketSpawnEvents,
            EntityCollectionCompletenessFlags = SnapshotEntityCollectionCompletenessFlags.AllProjectiles,
        };
    }

    private SnapshotSoundEvent[] ConvertSoundEvents(SnapshotSystem snapshots, IReadOnlyList<WorldSoundEvent> events)
    {
        return events
            .Select(soundEvent => snapshots.ToSnapshotSoundEvent(soundEvent, NextFallbackEventId()))
            .ToArray();
    }

    private SnapshotVisualEvent[] ConvertVisualEvents(
        SnapshotSystem snapshots,
        IReadOnlyList<WorldVisualEvent> events,
        long frame)
    {
        return events
            .Select(visualEvent =>
            {
                var snapshot = snapshots.ToSnapshotVisualEvent(visualEvent, NextFallbackEventId());
                return snapshot.SourceFrame == 0
                    ? snapshot with { SourceFrame = (ulong)frame }
                    : snapshot;
            })
            .ToArray();
    }

    private SnapshotDamageEvent[] ConvertDamageEvents(SnapshotSystem snapshots, IReadOnlyList<WorldDamageEvent> events)
    {
        return events
            .Select(damageEvent => snapshots.ToSnapshotDamageEvent(damageEvent, NextFallbackEventId()))
            .ToArray();
    }

    private SnapshotGibSpawnEvent[] ConvertGibSpawnEvents(IReadOnlyList<WorldGibSpawnEvent> events)
    {
        return events
            .Select(gibSpawnEvent => new SnapshotGibSpawnEvent(
                gibSpawnEvent.SpriteName,
                gibSpawnEvent.FrameIndex,
                gibSpawnEvent.X,
                gibSpawnEvent.Y,
                gibSpawnEvent.VelocityX,
                gibSpawnEvent.VelocityY,
                gibSpawnEvent.RotationSpeedDegrees,
                gibSpawnEvent.HorizontalFriction,
                gibSpawnEvent.RotationFriction,
                gibSpawnEvent.LifetimeTicks,
                gibSpawnEvent.BloodChance,
                NextFallbackEventId()))
            .ToArray();
    }

    private SnapshotRocketSpawnEvent[] ConvertRocketSpawnEvents(IReadOnlyList<WorldRocketSpawnEvent> events)
    {
        return events
            .Select(rocketSpawnEvent => new SnapshotRocketSpawnEvent(
                rocketSpawnEvent.Id,
                rocketSpawnEvent.Team,
                rocketSpawnEvent.OwnerId,
                rocketSpawnEvent.X,
                rocketSpawnEvent.Y,
                rocketSpawnEvent.PreviousX,
                rocketSpawnEvent.PreviousY,
                rocketSpawnEvent.DirectionRadians,
                rocketSpawnEvent.Speed,
                rocketSpawnEvent.TicksRemaining,
                rocketSpawnEvent.ReducedKnockbackSourceTicksRemaining,
                rocketSpawnEvent.ZeroKnockbackSourceTicksRemaining,
                rocketSpawnEvent.RangeAnchorOwnerId,
                rocketSpawnEvent.LastKnownRangeOriginX,
                rocketSpawnEvent.LastKnownRangeOriginY,
                rocketSpawnEvent.DistanceToTravel,
                rocketSpawnEvent.IsFading,
                rocketSpawnEvent.FadeSourceTicksRemaining,
                rocketSpawnEvent.ExplodeImmediately,
                rocketSpawnEvent.IsCritical,
                NextFallbackEventId(),
                rocketSpawnEvent.PassedFriendlyPlayerIds,
                rocketSpawnEvent.CriticalDamageMultiplier,
                rocketSpawnEvent.IsBallistic,
                rocketSpawnEvent.BallisticGravityPerTick,
                rocketSpawnEvent.SuppressSmokeTrail))
            .ToArray();
    }

    private ulong NextFallbackEventId()
    {
        return _nextFallbackEventId++;
    }
}
