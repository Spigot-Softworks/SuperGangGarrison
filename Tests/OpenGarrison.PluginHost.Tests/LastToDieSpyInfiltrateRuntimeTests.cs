using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieSpyInfiltrateRuntimeTests
{
    [Fact]
    public void DerivedModifiersEnableOnlyTheOwnedInfiltratePerk()
    {
        Assert.True(LastToDieDerivedModifiers.FromPerks(
            [LastToDiePerkIds.Spy.Infiltrate]).InfiltrateEnabled);
        Assert.False(LastToDieDerivedModifiers.FromPerks([]).InfiltrateEnabled);
    }

    [Fact]
    public void InteractWeaponRisingEdgeDashesExactlyTwoHundredTwentyUnits()
    {
        var world = CreateSpyWorld();
        var player = world.LocalPlayer;
        player.TeleportTo(100f, 100f);
        player.RestoreMovementProbeState(null, null, 1f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate],
            resetDynamicState: true));

        var startX = player.X;
        var pressed = default(PlayerInputSnapshot) with
        {
            InteractWeapon = true,
            AimWorldX = 1000f,
            AimWorldY = 100f,
        };
        Assert.True(world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, pressed));
        world.AdvanceOneTick();

        Assert.True(player.IsLastToDieSpyInfiltrateDashing);
        Assert.Equal(9, player.LastToDieSpyInfiltrateDashTicksRemaining);
        Assert.Equal(180, player.LastToDieSpyInfiltrateCooldownTicksRemaining);
        for (var tick = 1; tick < player.LastToDieSpyInfiltrateDurationTicks; tick += 1)
        {
            Assert.True(world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, default));
            world.AdvanceOneTick();
        }

        Assert.InRange(player.X - startX, 219.99f, 220.01f);
        Assert.True(player.IsLastToDieSpyInfiltrateDashing);
        Assert.Equal(1, player.LastToDieSpyInfiltrateDashTicksRemaining);

        world.AdvanceOneTick();

        Assert.False(player.IsLastToDieSpyInfiltrateDashing);
        Assert.Equal(0f, player.HorizontalSpeed);
    }

    [Fact]
    public void HeldInteractDoesNotRetriggerAndReleasePressDoesAfterSixSeconds()
    {
        var world = CreateSpyWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate],
            resetDynamicState: true));
        var held = default(PlayerInputSnapshot) with
        {
            InteractWeapon = true,
            AimWorldX = 1000f,
        };
        Assert.True(world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, held));
        world.AdvanceOneTick();

        for (var tick = 0; tick < 180; tick += 1)
        {
            Assert.True(world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, held));
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.LocalPlayer.LastToDieSpyInfiltrateCooldownTicksRemaining);
        Assert.False(world.LocalPlayer.IsLastToDieSpyInfiltrateDashing);

        Assert.True(world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, default));
        world.AdvanceOneTick();
        Assert.True(world.TrySetNetworkPlayerInput(SimulationWorld.LocalPlayerSlot, held));
        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsLastToDieSpyInfiltrateDashing);
        Assert.Equal(180, world.LocalPlayer.LastToDieSpyInfiltrateCooldownTicksRemaining);
    }

    [Fact]
    public void InfiltrateUsesNormalSweptCollisionAndCannotCrossAWall()
    {
        var world = CreateSpyWorld([new LevelSolid(180f, 0f, 20f, 512f)]);
        var player = world.LocalPlayer;
        player.TeleportTo(100f, 100f);
        player.RestoreMovementProbeState(null, null, 1f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate],
            resetDynamicState: true));

        Assert.True(world.TrySetNetworkPlayerInput(
            SimulationWorld.LocalPlayerSlot,
            default(PlayerInputSnapshot) with
            {
                InteractWeapon = true,
                AimWorldX = 1000f,
                AimWorldY = 100f,
            }));
        world.AdvanceOneTick();
        for (var tick = 1; tick < player.LastToDieSpyInfiltrateDurationTicks; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(player.Right <= 180.01f, $"right edge crossed wall: {player.Right:0.###}");
        Assert.True(player.X < 180f);
    }

    [Fact]
    public void ProjectileOnlyImmunityRejectsDirectEntityDamageButNotOtherSources()
    {
        var world = CreateSpyWorld();
        var spy = world.LocalPlayer;
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Soldier, PlayerTeam.Blue);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate],
            resetDynamicState: true));
        Assert.True(spy.TryStartLastToDieSpyInfiltrate(world.Config.TicksPerSecond));
        var healthBefore = spy.Health;

        var directProjectile = ResolveDamage(
            world,
            spy,
            enemy,
            PlayerDamageApplicationKind.Instant,
            PlayerDamageTraits.DirectProjectile | PlayerDamageTraits.Bullet);
        Assert.Equal(PlayerDamageDisposition.Invulnerable, directProjectile.Disposition);
        Assert.Equal(healthBefore, spy.Health);

        var directContinuousProjectile = ResolveDamage(
            world,
            spy,
            enemy,
            PlayerDamageApplicationKind.Continuous,
            PlayerDamageTraits.DirectProjectile | PlayerDamageTraits.Fire);
        Assert.Equal(PlayerDamageDisposition.Invulnerable, directContinuousProjectile.Disposition);
        Assert.Equal(healthBefore, spy.Health);

        var hitscan = ResolveDamage(
            world,
            spy,
            enemy,
            PlayerDamageApplicationKind.Instant,
            PlayerDamageTraits.Bullet);
        Assert.Equal(PlayerDamageDisposition.Applied, hitscan.Disposition);

        var melee = ResolveDamage(
            world,
            spy,
            enemy,
            PlayerDamageApplicationKind.Instant,
            PlayerDamageTraits.Melee);
        Assert.Equal(PlayerDamageDisposition.Applied, melee.Disposition);

        var periodic = ResolveDamage(
            world,
            spy,
            enemy,
            PlayerDamageApplicationKind.Continuous,
            PlayerDamageTraits.Periodic | PlayerDamageTraits.Fire);
        Assert.Equal(PlayerDamageDisposition.Applied, periodic.Disposition);

        var explosion = ResolveDamage(
            world,
            spy,
            enemy,
            PlayerDamageApplicationKind.Instant,
            PlayerDamageTraits.Explosive);
        Assert.Equal(PlayerDamageDisposition.Applied, explosion.Disposition);
        Assert.Equal(healthBefore - 80, spy.Health);
    }

    [Fact]
    public void DynamicStateRoundTripsThroughPredictionAndResetsOnBuildRemovalAndDeath()
    {
        var world = CreateSpyWorld();
        var player = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate],
            resetDynamicState: true));
        Assert.True(player.TryStartLastToDieSpyInfiltrate(world.Config.TicksPerSecond));
        player.AdvanceTickState(default, world.Config.FixedDeltaSeconds);
        Assert.True(player.TryGetReplicatedStateInt(
            PlayerEntity.LastToDieSpyInfiltrateReplicatedStateOwnerId,
            PlayerEntity.LastToDieSpyInfiltrateReplicatedStateKey,
            out var legacyEncodedState));
        Assert.Equal(unchecked((int)player.LastToDieSpyInfiltrateState), legacyEncodedState);

        var clone = new PlayerEntity(5000, CharacterClassCatalog.Spy, "PredictionClone");
        clone.ConfigureLastToDieSpyInfiltrate(
            enabled: true,
            ticksPerSecond: world.Config.TicksPerSecond,
            resetDynamicState: false);
        clone.RestorePredictionState(player.CapturePredictionState());

        Assert.Equal(player.LastToDieSpyInfiltrateState, clone.LastToDieSpyInfiltrateState);
        Assert.Equal(
            player.LastToDieSpyInfiltrateCooldownTicksRemaining,
            clone.LastToDieSpyInfiltrateCooldownTicksRemaining);
        Assert.True(clone.IsLastToDieSpyInfiltrateDashing);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [],
            resetDynamicState: false));
        Assert.Equal(0u, player.LastToDieSpyInfiltrateState);
        Assert.False(player.TryGetReplicatedStateInt(
            PlayerEntity.LastToDieSpyInfiltrateReplicatedStateOwnerId,
            PlayerEntity.LastToDieSpyInfiltrateReplicatedStateKey,
            out _));

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate]));
        Assert.True(player.TryStartLastToDieSpyInfiltrate(world.Config.TicksPerSecond));
        player.Kill();

        Assert.Equal(0u, player.LastToDieSpyInfiltrateState);
        Assert.False(player.IsLastToDieSpyInfiltrateDashing);
    }

    [Fact]
    public void Protocol64PublisherCodecAndWorldPreserveExactHudState()
    {
        var source = CreateSpyWorld();
        var player = source.LocalPlayer;
        player.RestoreMovementProbeState(null, null, -1f);
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate],
            resetDynamicState: true));
        Assert.True(player.TryStartLastToDieSpyInfiltrate(source.Config.TicksPerSecond));

        var published = new Protocol64StatePublisher(source).BuildPlayerStateBatch(44);
        var publishedPlayer = Assert.Single(published.Players);
        Assert.Equal(player.LastToDieSpyInfiltrateState, publishedPlayer.LastToDieSpyInfiltrateState);

        var schema = new Protocol64PlayerStateBatchSchema();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            schema.WriteBody(published, writer);
        }

        stream.Position = 0;
        Protocol64PlayerStateBatch decoded;
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            decoded = schema.ReadBody(reader);
        }

        var decodedPlayer = Assert.Single(decoded.Players);
        Assert.Equal(publishedPlayer.LastToDieSpyInfiltrateState, decodedPlayer.LastToDieSpyInfiltrateState);
        Assert.Equal((ushort)22, schema.Descriptor.Key.Revision);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(decodedPlayer));
        Assert.True(receiver.TryApplyLastToDiePlayerPredictionProfile(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate.Value]));

        Assert.Equal(
            publishedPlayer.LastToDieSpyInfiltrateState,
            receiver.LocalPlayer.LastToDieSpyInfiltrateState);
        Assert.Equal(180, receiver.LocalPlayer.LastToDieSpyInfiltrateCooldownTicksRemaining);
        Assert.Equal(9, receiver.LocalPlayer.LastToDieSpyInfiltrateDashTicksRemaining);
        Assert.Equal(-1f, receiver.LocalPlayer.LastToDieSpyInfiltrateDirectionX);
        Assert.True(receiver.LocalPlayer.IsLastToDieSpyInfiltrateDashing);
    }

    private static PlayerDamageResolution ResolveDamage(
        SimulationWorld world,
        PlayerEntity target,
        PlayerEntity attacker,
        PlayerDamageApplicationKind applicationKind,
        PlayerDamageTraits traits)
    {
        return world.ResolvePlayerDamage(
            target,
            new PlayerDamageRequest(
                applicationKind,
                Amount: 20f,
                attacker,
                PlayerEntity.SpyDamageRevealAlpha,
                DamageEventFlags.None,
                traits,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));
    }

    private static SimulationWorld CreateSpyWorld(IReadOnlyList<LevelSolid>? solids = null)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Spy);
        var spawn = new SpawnPoint(100f, 100f);
        world.CombatTestSetLevel(new SimpleLevel(
            "ltd-spy-infiltrate-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(1024f, 512f),
            1f,
            null,
            1,
            1,
            spawn,
            [spawn],
            [new SpawnPoint(800f, 100f)],
            [],
            [],
            floorY: 512f,
            solids ?? [],
            importedFromSource: false));
        world.LocalPlayer.Spawn(PlayerTeam.Red, spawn.X, spawn.Y);
        return world;
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }
}
