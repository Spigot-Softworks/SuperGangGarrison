using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class SnapshotSystemTests
{
    [Fact]
    public void CapturingTheSameEntityStateProducesIdenticalProtocolBytes()
    {
        var store = new EntityStore();
        var combat = new CombatSystem(store);
        var snapshots = new SnapshotSystem(store, combat);
        var shot = new ShotProjectileEntity(
            id: 7,
            team: PlayerTeam.Red,
            ownerId: 3,
            x: 12.5f,
            y: 24.25f,
            velocityX: 4f,
            velocityY: -2f,
            damagePerHit: 11f);
        store.Add(shot);

        var first = CreateSnapshot(snapshots.ToSnapshotBulletState(shot));
        var second = CreateSnapshot(snapshots.ToSnapshotBulletState(shot));

        Assert.Equal(ProtocolCodec.Serialize(first), ProtocolCodec.Serialize(second));
    }

    [Fact]
    public void DamageCaptureConvertsEachQueuedEventOnceAndDrainsCombatQueue()
    {
        var store = new EntityStore();
        var attacker = CreatePlayer(1, PlayerTeam.Red);
        var target = CreatePlayer(2, PlayerTeam.Blue);
        store.Add(attacker);
        store.Add(target);
        var combat = new CombatSystem(store);
        var snapshots = new SnapshotSystem(store, combat);

        Assert.True(combat.TryApplyPlayerDamage(target.Id, 10, attacker.Id));

        ulong nextEventId = 41;
        var captured = snapshots.DrainSnapshotDamageEvents(ref nextEventId);
        var capturedAgain = snapshots.DrainSnapshotDamageEvents(ref nextEventId);

        var damageEvent = Assert.Single(captured);
        Assert.Empty(capturedAgain);
        Assert.Equal((ulong)41, damageEvent.EventId);
        Assert.Equal(10, damageEvent.Amount);
        Assert.Equal(attacker.Id, damageEvent.AttackerPlayerId);
        Assert.Equal(target.Id, damageEvent.TargetEntityId);
        Assert.Equal((byte)DamageTargetKind.Player, damageEvent.TargetKind);
    }

    private static SnapshotMessage CreateSnapshot(SnapshotShotState shot)
    {
        return new SnapshotMessage(
            Frame: 12,
            TickRate: 30,
            LevelName: "test",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 900,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 100f, 0f, true, false, 0),
            Players: Array.Empty<SnapshotPlayerState>(),
            CombatTraces: Array.Empty<SnapshotCombatTraceState>(),
            SniperAimIndicators: Array.Empty<SnapshotSniperAimIndicatorState>(),
            Sentries: Array.Empty<SnapshotSentryState>(),
            Shots: new[] { shot },
            Bubbles: Array.Empty<SnapshotShotState>(),
            Blades: Array.Empty<SnapshotShotState>(),
            Needles: Array.Empty<SnapshotShotState>(),
            RevolverShots: Array.Empty<SnapshotShotState>(),
            Rockets: Array.Empty<SnapshotRocketState>(),
            Flames: Array.Empty<SnapshotFlameState>(),
            Flares: Array.Empty<SnapshotShotState>(),
            Mines: Array.Empty<SnapshotMineState>(),
            DeadBodies: Array.Empty<SnapshotDeadBodyState>(),
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: Array.Empty<SnapshotControlPointState>(),
            Generators: Array.Empty<SnapshotGeneratorState>(),
            LocalDeathCam: null,
            KillFeed: Array.Empty<SnapshotKillFeedEntry>(),
            VisualEvents: Array.Empty<SnapshotVisualEvent>(),
            DamageEvents: Array.Empty<SnapshotDamageEvent>(),
            SoundEvents: Array.Empty<SnapshotSoundEvent>());
    }

    private static PlayerEntity CreatePlayer(int id, PlayerTeam team)
    {
        var player = new PlayerEntity(id, CharacterClassCatalog.Scout, $"Player {id}");
        player.Spawn(team, 0f, 0f);
        return player;
    }
}
