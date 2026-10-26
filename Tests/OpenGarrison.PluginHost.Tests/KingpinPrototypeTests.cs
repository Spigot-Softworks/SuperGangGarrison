using OpenGarrison.Core;
using OpenGarrison.Client;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class KingpinPrototypeTests
{
    [Fact]
    public void PrototypeHasItsOwnLoadoutAndAllTemporaryFramesWithoutReplacingHeavy()
    {
        var definition = CharacterClassCatalog.GetDefinition("kingpin");
        Assert.Equal(200, definition.MaxHealth);
        Assert.Equal("kingpin", definition.GameplayClassId);
        Assert.Equal("weapon.kingpin-fists", definition.PrimaryWeapon.ItemId);
        Assert.Equal("heavy", CharacterClassCatalog.Heavy.GameplayClassId);
        Assert.Equal("weapon.minigun", CharacterClassCatalog.Heavy.PrimaryWeapon.ItemId);
        Assert.Equal(CharacterClassCatalog.Heavy.Width, definition.Width);
        Assert.Equal(CharacterClassCatalog.Heavy.Height, definition.Height);
        Assert.Equal(2f, GameplayPlayerSpriteRenderController.GetBodySpriteArtScale("KingpinRedS"));
        Assert.Equal(1f, GameplayPlayerSpriteRenderController.GetBodySpriteArtScale("HeavyRedStandS"));
        var pack = CharacterClassCatalog.StockModPack;
        foreach (var sprite in new[] { "KingpinRedS", "KingpinBlueS" })
        {
            Assert.True(pack.Assets.Sprites.TryGetValue(sprite, out var asset));
            Assert.Equal(13, asset.FramePaths.Count);
            var packDirectory = GameplayModPackDirectoryLoader.FindPackDirectory("stock.gg2")!;
            Assert.All(asset.FramePaths, path => Assert.True(File.Exists(Path.Combine(packDirectory, path))));
        }
    }

    [Fact]
    public void ThicketIsBundledClassicKothAndLoadsWithSpawnsSolidsAndAnObjective()
    {
        Assert.True(OpenGarrisonStockMapCatalog.TryGetDefinition("koth_thicket", out var definition));
        Assert.False(Game1.IsSuperGangGarrisonMap(definition.LevelName, definition.DisplayName));
        var level = SimpleLevelFactory.CreateImportedLevel("koth_thicket");
        Assert.NotNull(level);
        Assert.Equal(GameModeKind.KingOfTheHill, level.Mode);
        Assert.NotEmpty(level.Solids);
        Assert.NotEmpty(level.RedSpawns);
        Assert.NotEmpty(level.BlueSpawns);
        Assert.Contains(level.RoomObjects, marker => marker.Type == RoomObjectType.ControlPoint);
    }

    [Fact]
    public void RealInputPunchesAlternateAndDoNotShootHeavyProjectiles()
    {
        var world = CreateWorld();
        var enemy = Enemy(world, 125, world.LocalPlayer.Y);
        var health = enemy.Health;
        var input = default(PlayerInputSnapshot) with { AimWorldX = 500, AimWorldY = enemy.Y, FirePrimary = true };
        world.NetworkPlayers.SetLocalInput(input);
        world.AdvanceOneTick();
        Assert.Equal(health - 30, enemy.Health);
        Assert.Equal((int)KingpinAction.Jab1, KingpinPrototype.Read(world.LocalPlayer, "action"));
        for (var tick = 0; tick < 11; tick++) world.AdvanceOneTick();
        Assert.Equal((int)KingpinAction.Jab2, KingpinPrototype.Read(world.LocalPlayer, "action"));
        Assert.Empty(world.Shots);
    }

    [Fact]
    public void AbilityAndSwapInputsCycleInOppositeDirectionsWithoutEquippingHeavyWeapons()
    {
        var world = CreateWorld();
        var input = default(PlayerInputSnapshot) with { AimWorldX = 500, UseAbility = true };
        world.NetworkPlayers.SetLocalInput(input);
        world.AdvanceOneTick();
        Assert.Equal(KingpinSpecial.Beatdown, KingpinPrototype.SelectedSpecial(world.LocalPlayer));
        world.NetworkPlayers.SetLocalInput(input with { UseAbility = false }); world.AdvanceOneTick();
        world.NetworkPlayers.SetLocalInput(input with { UseAbility = false, SwapWeapon = true }); world.AdvanceOneTick();
        Assert.Equal(KingpinSpecial.ShoulderBash, KingpinPrototype.SelectedSpecial(world.LocalPlayer));
        Assert.Equal("weapon.kingpin-fists", world.LocalPlayer.GameplayLoadoutState.EquippedItemId);
    }

    [Theory]
    [InlineData(KingpinSpecial.ShoulderBash, 0)]
    [InlineData(KingpinSpecial.Uppercut, 25)]
    [InlineData(KingpinSpecial.DiveBodyslam, 50)]
    [InlineData(KingpinSpecial.Beatdown, 100)]
    public void SpecialsRequireAndSpendTheirConfiguredMeter(KingpinSpecial special, int cost)
    {
        var world = CreateWorld();
        world.LocalPlayer.SetReplicatedStateInt(KingpinPrototype.StateOwner, "special", (int)special);
        if (cost > 0)
        {
            KingpinPrototype.SetMeter(world.LocalPlayer, cost - 1);
            Special(world);
            Assert.Equal(0, KingpinPrototype.Read(world.LocalPlayer, "ticks"));
            Assert.Equal(cost - 1, KingpinPrototype.Meter(world.LocalPlayer));
        }
        KingpinPrototype.SetMeter(world.LocalPlayer, 100);
        Special(world);
        Assert.Equal(100 - cost, KingpinPrototype.Meter(world.LocalPlayer));
        Assert.True(KingpinPrototype.Read(world.LocalPlayer, "ticks") > 0);
        Special(world);
        Assert.Equal(100 - cost, KingpinPrototype.Meter(world.LocalPlayer));
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(5, 30)]
    [InlineData(20, 40)]
    public void RetreatGuardScalesByTimingAndBuildsMeter(int guardTicks, int expectedDamage)
    {
        var world = CreateWorld();
        var player = world.LocalPlayer;
        var enemy = Enemy(world, 400, player.Y);
        Guard(world, guardTicks);
        var before = player.Health;
        world.Combat.ApplyPlayerDamageWithContext(player, 60, enemy, additionalTraits: PlayerDamageTraits.DirectProjectile);
        Assert.Equal(before - expectedDamage, player.Health);
        Assert.Equal((60 - expectedDamage) * 0.5f + expectedDamage * KingpinPrototype.MeterPerDamageTaken,
            KingpinPrototype.Meter(player), 3);
        Assert.Equal(guardTicks == 1 ? 12 : 11, KingpinPrototype.GetSpriteFrame(player));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GuardDoesNotProtectMeleeOrRearShots(bool melee, bool rear)
    {
        var world = CreateWorld();
        var player = world.LocalPlayer;
        var enemy = Enemy(world, rear ? 0 : 400, player.Y);
        Guard(world, 1);
        world.Combat.ApplyPlayerDamageWithContext(player, 60, enemy,
            additionalTraits: melee ? PlayerDamageTraits.Melee : PlayerDamageTraits.DirectProjectile);
        Assert.Equal(140, player.Health);
        Assert.Equal(60 * KingpinPrototype.MeterPerDamageTaken, KingpinPrototype.Meter(player));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void RetreatInputActivatesParryEvenWhileStoppedOrBraking(float horizontalSpeed)
    {
        var world = CreateWorld();
        var player = world.LocalPlayer;
        var enemy = Enemy(world, 400, player.Y);
        player.ApplyVelocityImpulse(horizontalSpeed, 0);
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { Left = true, AimWorldX = 600 });
        world.AdvanceOneTick();
        world.Combat.ApplyPlayerDamageWithContext(player, 60, enemy, additionalTraits: PlayerDamageTraits.Bullet);
        Assert.Equal(194, player.Health);
        Assert.Equal(12, KingpinPrototype.GetSpriteFrame(player));
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { AimWorldX = 600 });
        world.AdvanceOneTick();
        world.Combat.ApplyPlayerDamageWithContext(player, 60, enemy, additionalTraits: PlayerDamageTraits.Bullet);
        Assert.Equal(134, player.Health);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualCombatDamageBuildsMeterForBothPlayersAndClampsOverkill(bool continuous)
    {
        var world = CreateWorld();
        var player = world.LocalPlayer;
        var enemy = Enemy(world, 125, player.Y);
        enemy.SetClassDefinition(CharacterClassCatalog.GetDefinition("kingpin"));
        enemy.ForceSetHealth(20);
        if (continuous) world.Combat.ApplyPlayerContinuousDamageWithContext(enemy, 500, player);
        else world.Combat.ApplyPlayerDamageWithContext(enemy, 500, player);
        Assert.Equal(20 * KingpinPrototype.MeterPerDamageDealt, KingpinPrototype.Meter(player));
        Assert.Equal(20 * KingpinPrototype.MeterPerDamageTaken, KingpinPrototype.Meter(enemy));
    }

    [Fact]
    public void PunchBuildsMeterAndSelfDamageDoesNot()
    {
        var world = CreateWorld();
        Enemy(world, 125, world.LocalPlayer.Y);
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { FirePrimary = true, AimWorldX = 500 });
        world.AdvanceOneTick();
        Assert.Equal(15, KingpinPrototype.Meter(world.LocalPlayer));
        world.Combat.ApplyPlayerDamageWithContext(world.LocalPlayer, 20, world.LocalPlayer);
        Assert.Equal(15, KingpinPrototype.Meter(world.LocalPlayer));
    }

    [Fact]
    public void PredictionAdvancesAndFinishesAttackWithoutApplyingDamageOrRewards()
    {
        var world = CreateWorld();
        var player = world.LocalPlayer;
        var enemy = Enemy(world, 125, player.Y);
        KingpinPrototype.Punch(world, player, prediction: true);
        Assert.Equal(2, KingpinPrototype.GetSpriteFrame(player));
        var duration = KingpinPrototype.Read(player, "duration");
        for (var tick = 0; tick < duration / 2 + 1; tick++)
            KingpinPrototype.Advance(world, player, default, default, prediction: true);
        Assert.Equal(3, KingpinPrototype.GetSpriteFrame(player));
        for (var tick = 0; tick < duration; tick++)
            KingpinPrototype.Advance(world, player, default, default, prediction: true);
        Assert.Equal(0, KingpinPrototype.GetSpriteFrame(player));
        Assert.Equal((int)KingpinAction.None, KingpinPrototype.Read(player, "action"));
        Assert.Equal(100, enemy.Health);
        Assert.Equal(0, KingpinPrototype.Meter(player));
        Assert.Empty(world.Combat.PendingDamageEvents);
    }

    [Theory]
    [InlineData(KingpinSpecial.ShoulderBash)]
    [InlineData(KingpinSpecial.Uppercut)]
    [InlineData(KingpinSpecial.DiveBodyslam)]
    [InlineData(KingpinSpecial.Beatdown)]
    public void PredictedSpecialsSpendMeterAndAnimateWithoutHitting(KingpinSpecial special)
    {
        var world = CreateWorld();
        var player = world.LocalPlayer;
        var enemy = Enemy(world, 125, player.Y);
        player.SetReplicatedStateInt(KingpinPrototype.StateOwner, "special", (int)special);
        KingpinPrototype.SetMeter(player, 100);
        KingpinPrototype.UseSpecial(world, player, prediction: true);
        Assert.Equal(100 - KingpinPrototype.SpecialCost(special), KingpinPrototype.Meter(player));
        Assert.True(KingpinPrototype.Read(player, "ticks") > 0);
        Assert.False(player.TryFirePrimaryWeapon());
        Assert.Equal(0, player.PrimaryCooldownTicks);
        for (var tick = 0; tick < 61; tick++)
            KingpinPrototype.Advance(world, player, default, default, prediction: true);
        Assert.Equal(100, enemy.Health);
        Assert.Empty(world.Combat.PendingDamageEvents);
    }

    [Fact]
    public void TauntAndCorpseAliasesNeverPlayTheCombatPoseBank()
    {
        var sprites = CharacterClassCatalog.StockModPack.Assets.Sprites;
        foreach (var team in new[] { "Red", "Blue" })
        {
            var taunt = sprites[$"Kingpin{team}TauntS"];
            Assert.Single(taunt.FramePaths);
            Assert.EndsWith("Kingpin12.png", taunt.FramePaths[0]);
            var corpse = sprites[$"Kingpin{team}DeadS"];
            Assert.Single(corpse.FramePaths);
            Assert.EndsWith("Kingpin11.png", corpse.FramePaths[0]);
            var idle = sprites[$"Kingpin{team}StandS"];
            Assert.Single(idle.FramePaths);
            Assert.Equal(24, (32 - idle.OriginY) * KingpinPrototype.BodySpriteScale);
        }
    }

    [Fact]
    public void MeterAndSelectionReplicateAndRespawnClearsThePrototypeState()
    {
        var server = CreateWorld();
        KingpinPrototype.SetMeter(server.LocalPlayer, 77);
        server.LocalPlayer.SetReplicatedStateInt(KingpinPrototype.StateOwner, "special", 2);
        var state = server.Snapshots.ToSnapshotPlayerState(1, server.LocalPlayer, null, static _ => 0);
        var snapshot = SimulationWorldSnapshotPresentationTests.CreateSnapshot(server, 12, state);
        Assert.True(ProtocolCodec.TryDeserialize(ProtocolCodec.Serialize(snapshot), out var decoded));
        var client = CreateWorld();
        client.ClientPredictionMode = true;
        Assert.True(client.SnapshotApply.ApplySnapshot(Assert.IsType<SnapshotMessage>(decoded)));
        Assert.Equal("kingpin", client.LocalPlayer.GameplayClassId);
        Assert.Equal(77, KingpinPrototype.Meter(client.LocalPlayer));
        Assert.Equal(KingpinSpecial.DiveBodyslam, KingpinPrototype.SelectedSpecial(client.LocalPlayer));
        server.NetworkPlayers.ForceRespawnLocalPlayer();
        Assert.Equal(0, KingpinPrototype.Meter(server.LocalPlayer));
        Assert.Equal(KingpinSpecial.ShoulderBash, KingpinPrototype.SelectedSpecial(server.LocalPlayer));
    }

    [Fact]
    public void RightClickUsesSelectedSpecialInsteadOfSandvich()
    {
        var world = CreateWorld();
        world.LocalPlayer.SetReplicatedStateInt(KingpinPrototype.StateOwner, "special", 1);
        KingpinPrototype.SetMeter(world.LocalPlayer, 50);
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { FireSecondary = true, AimWorldX = 500 });
        world.AdvanceOneTick();
        Assert.Equal((int)KingpinAction.Uppercut, KingpinPrototype.Read(world.LocalPlayer, "action"));
        Assert.Equal(25, KingpinPrototype.Meter(world.LocalPlayer));
        Assert.False(world.LocalPlayer.IsHeavyEating);
        Assert.True(world.LocalPlayer.VerticalSpeed < 0);
    }

    [Fact]
    public void BeatdownStrikesFourTimesAndBodyslamOnlyStrikesOnLanding()
    {
        var world = CreateWorld();
        var enemy = Enemy(world, 125, world.LocalPlayer.Y);
        KingpinPrototype.SetMeter(world.LocalPlayer, 100);
        world.LocalPlayer.SetReplicatedStateInt(KingpinPrototype.StateOwner, "special", 3);
        Special(world);
        Assert.Equal(75, enemy.Health);
        for (var tick = 0; tick < 30; tick++) KingpinPrototype.Advance(world, world.LocalPlayer, default, default);
        Assert.False(enemy.IsAlive);

        world.NetworkPlayers.ForceRespawnLocalPlayer();
        enemy.Spawn(PlayerTeam.Blue, 125, world.LocalPlayer.Y);
        KingpinPrototype.SetMeter(world.LocalPlayer, 50);
        world.LocalPlayer.SetReplicatedStateInt(KingpinPrototype.StateOwner, "special", 2);
        Special(world);
        for (var tick = 0; tick < 8; tick++) KingpinPrototype.Advance(world, world.LocalPlayer, default, default);
        Assert.Equal(100, enemy.Health);
        world.LocalPlayer.RestoreMovementProbeState(true, 0, 1);
        KingpinPrototype.Advance(world, world.LocalPlayer, default, default);
        Assert.Equal(30, enemy.Health);
        for (var tick = 0; tick < 10; tick++) KingpinPrototype.Advance(world, world.LocalPlayer, default, default);
        Assert.Equal(30, enemy.Health);
    }

    [Fact]
    public void PunchCannotDamageAnEnemyThroughAWallAndPassiveMeterIsOff()
    {
        var world = CreateWorld(wall: true);
        var enemy = Enemy(world, 130, world.LocalPlayer.Y);
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { FirePrimary = true, AimWorldX = 500 });
        for (var tick = 0; tick < 60; tick++) world.AdvanceOneTick();
        Assert.Equal(100, enemy.Health);
        Assert.Equal(0, KingpinPrototype.Meter(world.LocalPlayer));
    }

    [Fact]
    public void ShoulderBashHitsAnEnemyReachedDuringTheDashOnlyOnce()
    {
        var world = CreateWorld();
        var enemy = Enemy(world, 170, world.LocalPlayer.Y);
        Special(world);
        Assert.Equal(100, enemy.Health);
        world.LocalPlayer.TeleportTo(140, world.LocalPlayer.Y);
        for (var tick = 0; tick < 5; tick++) KingpinPrototype.Advance(world, world.LocalPlayer, default, default);
        Assert.Equal(65, enemy.Health);
    }

    [Fact]
    public void HardcoreCapsPrototypeHealthAsItDoesOtherClasses()
    {
        var world = CreateWorld();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(EnableHardcore: true));
        Assert.Equal(75, world.LocalPlayer.MaxHealth);
    }

    private static void Guard(SimulationWorld world, int ticks)
    {
        var input = default(PlayerInputSnapshot) with { Left = true, AimWorldX = 600 };
        world.LocalPlayer.SetAimWorldPosition(600, world.LocalPlayer.Y);
        world.LocalPlayer.ApplyVelocityImpulse(-100, 0);
        for (var tick = 0; tick < ticks; tick++) KingpinPrototype.Advance(world, world.LocalPlayer, input, input);
    }

    private static void Special(SimulationWorld world)
    {
        world.LocalPlayer.SetAimWorldPosition(500, world.LocalPlayer.Y);
        var item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem("ability.kingpin-special");
        KingpinPrototype.UseSpecial(new GameplayAbilityContext { World = world, Player = world.LocalPlayer,
            Item = item, Ability = item.Ability!, Phase = GameplayAbilityInputPhase.Pressed,
            Input = default, PreviousInput = default, SourceX = world.LocalPlayer.X, SourceY = world.LocalPlayer.Y });
    }

    private static PlayerEntity Enemy(SimulationWorld world, float x, float y)
    {
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.NetworkPlayers.TryCompleteNetworkPlayerJoinState(2, PlayerClass.Scout));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(2, out var enemy));
        enemy.TeleportTo(x, y);
        return enemy;
    }

    private static SimulationWorld CreateWorld(bool wall = false)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.TestSetLevel(new SimpleLevel("kingpin_test", GameModeKind.CaptureTheFlag, new WorldBounds(700, 400),
            1, null, 1, 1, new SpawnPoint(100, 250), [new SpawnPoint(100, 250)], [new SpawnPoint(600, 250)],
            [], [], 300, wall ? [new LevelSolid(0, 300, 700, 100), new LevelSolid(111, 0, 10, 300)]
                : [new LevelSolid(0, 300, 700, 100)], false));
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("kingpin");
        return world;
    }
}
