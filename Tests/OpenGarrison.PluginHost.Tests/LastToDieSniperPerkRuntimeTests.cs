using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieSniperPerkRuntimeTests
{
    [Fact]
    public void StaticSniperPerksAggregateAndRoundTripAsOneProfile()
    {
        var modifiers = LastToDieDerivedModifiers.FromPerks(
        [
            LastToDiePerkIds.Sniper.Overcharged,
            LastToDiePerkIds.Sniper.GreasedBolt,
            LastToDiePerkIds.Sniper.LightMarksman,
            LastToDiePerkIds.Sniper.ExtremeConditioning,
            LastToDiePerkIds.Sniper.FiftyCal,
            LastToDiePerkIds.Sniper.Fmj,
            LastToDiePerkIds.Sniper.Guardian,
            LastToDiePerkIds.Sniper.Mechanica,
            LastToDiePerkIds.Sniper.Spotted,
            LastToDiePerkIds.Sniper.Conquistador,
            LastToDiePerkIds.Sniper.TranqDarts,
            LastToDiePerkIds.Sniper.PoisonTip,
        ]);

        var profile = Assert.IsType<LastToDieSniperProfile>(modifiers.SniperProfile);
        Assert.True(profile.OverchargedEnabled);
        Assert.True(profile.GreasedBoltEnabled);
        Assert.True(profile.LightMarksmanEnabled);
        Assert.True(profile.ExtremeConditioningEnabled);
        Assert.True(profile.FiftyCalEnabled);
        Assert.True(profile.FmjEnabled);
        Assert.True(profile.GuardianEnabled);
        Assert.True(profile.MechanicaEnabled);
        Assert.True(profile.SpottedEnabled);
        Assert.True(profile.ConquistadorEnabled);
        Assert.True(profile.TranqDartsEnabled);
        Assert.True(profile.PoisonTipEnabled);
        Assert.Equal(profile, LastToDieSniperProfile.Decode(profile.Encode()));
        Assert.Equal(42, profile.ScaleRifleCycleTicks(40));
        Assert.Equal(PlayerEntity.SniperChargeMaxTicks, profile.RifleFullChargeTicks);
        Assert.Equal(15, profile.HuntsmanFullChargeTicks);
    }

    [Fact]
    public void OverchargedNormalizesRifleChargeToFortyFiveTicks()
    {
        var player = CreateSniperWorld().LocalPlayer;
        Assert.Equal(85, player.GetSniperRifleDamageForCharge(120, isScoped: true));

        var world = CreateSniperWorld();
        player = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Overcharged]));
        Assert.True(player.TryToggleSniperScope());

        AdvanceSourceTicks(player, 44);
        Assert.Equal(44, player.SniperChargeTicks);
        Assert.Equal(84, player.GetSniperRifleDamage());

        AdvanceSourceTicks(player, 1);
        Assert.Equal(45, player.SniperChargeTicks);
        Assert.Equal(85, player.GetSniperRifleDamage());

        AdvanceSourceTicks(player, 20);
        Assert.Equal(45, player.SniperChargeTicks);
    }

    [Theory]
    [InlineData(false, false, false, 40)]
    [InlineData(true, false, false, 29)]
    [InlineData(false, true, false, 20)]
    [InlineData(true, true, false, 17)]
    [InlineData(true, false, true, 43)]
    public void RifleCyclePerksUseOneAdditiveSpeedBucket(
        bool greasedBolt,
        bool lightMarksman,
        bool scoped,
        int expectedCooldownTicks)
    {
        var world = CreateSniperWorld();
        var perks = new List<LastToDiePerkId>();
        if (greasedBolt) perks.Add(LastToDiePerkIds.Sniper.GreasedBolt);
        if (lightMarksman) perks.Add(LastToDiePerkIds.Sniper.LightMarksman);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            perks));
        if (scoped)
        {
            Assert.True(world.LocalPlayer.TryToggleSniperScope());
        }

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        Assert.Equal(expectedCooldownTicks, world.LocalPlayer.PrimaryCooldownTicks);
    }

    [Theory]
    [InlineData(false, false, 100)]
    [InlineData(true, false, 72)]
    [InlineData(false, true, 50)]
    [InlineData(true, true, 42)]
    public void FiftyCalRateFactorComposesWithPositiveSpeedBucket(
        bool greasedBolt,
        bool lightMarksman,
        int expectedCooldownTicks)
    {
        var world = CreateSniperWorld();
        var perks = new List<LastToDiePerkId> { LastToDiePerkIds.Sniper.FiftyCal };
        if (greasedBolt) perks.Add(LastToDiePerkIds.Sniper.GreasedBolt);
        if (lightMarksman) perks.Add(LastToDiePerkIds.Sniper.LightMarksman);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            perks));

        Assert.True(world.LocalPlayer.TryFirePrimaryWeapon());
        Assert.Equal(expectedCooldownTicks, world.LocalPlayer.PrimaryCooldownTicks);
    }

    [Fact]
    public void FiftyCalGibsFirstEnemyDamagesSecondAndStopsBeforeThird()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var first = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        var second = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 340f, 100f);
        var third = AddNetworkPlayer(world, 4, PlayerTeam.Blue, 460f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.FiftyCal]));

        FireRifle(world, sniper, third.X, third.Y);

        Assert.False(first.IsAlive);
        Assert.Equal(1, first.GibDeaths);
        Assert.Equal(second.MaxHealth - PlayerEntity.SniperBaseDamage, second.Health);
        Assert.Equal(third.MaxHealth, third.Health);
        var events = world.DrainPendingDamageEvents();
        Assert.Contains(events, damageEvent =>
            damageEvent.TargetEntityId == first.Id
            && damageEvent.WasFatal
            && damageEvent.Flags.HasFlag(DamageEventFlags.Gibbed));
        Assert.Contains(events, damageEvent =>
            damageEvent.TargetEntityId == second.Id
            && !damageEvent.WasFatal);
        Assert.DoesNotContain(events, damageEvent => damageEvent.TargetEntityId == third.Id);
    }

    [Fact]
    public void FullyChargedMechanicaOverridesFiftyCalPlayerCap()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var first = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        var second = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 340f, 100f);
        var third = AddNetworkPlayer(world, 4, PlayerTeam.Blue, 460f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.FiftyCal, LastToDiePerkIds.Sniper.Mechanica]));
        Assert.True(sniper.TryToggleSniperScope());
        AdvanceSourceTicks(sniper, sniper.LastToDieSniperRifleFullChargeTicks);

        FireRifle(world, sniper, third.X, third.Y);

        Assert.False(first.IsAlive);
        Assert.Equal(second.MaxHealth - 85, second.Health);
        Assert.Equal(third.MaxHealth - 85, third.Health);
    }

    [Fact]
    public void FmjIgnoresOrdinarySolidWithoutAddingPlayerPenetration()
    {
        var solid = new LevelSolid(190f, 0f, 20f, 400f);
        var blockedWorld = CreateSniperCombatWorld([solid]);
        blockedWorld.LocalPlayer.TeleportTo(100f, 100f);
        var blockedEnemy = AddNetworkPlayer(blockedWorld, 2, PlayerTeam.Blue, 300f, 100f);

        FireRifle(blockedWorld, blockedWorld.LocalPlayer, blockedEnemy.X, blockedEnemy.Y);

        Assert.Equal(blockedEnemy.MaxHealth, blockedEnemy.Health);

        var fmjWorld = CreateSniperCombatWorld([solid]);
        fmjWorld.LocalPlayer.TeleportTo(100f, 100f);
        var firstEnemy = AddNetworkPlayer(fmjWorld, 2, PlayerTeam.Blue, 300f, 100f);
        var secondEnemy = AddNetworkPlayer(fmjWorld, 3, PlayerTeam.Blue, 420f, 100f);
        Assert.True(fmjWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Fmj]));

        FireRifle(fmjWorld, fmjWorld.LocalPlayer, secondEnemy.X, secondEnemy.Y);

        Assert.Equal(firstEnemy.MaxHealth - PlayerEntity.SniperBaseDamage, firstEnemy.Health);
        Assert.Equal(secondEnemy.MaxHealth, secondEnemy.Health);
    }

    [Fact]
    public void GuardianFriendlyHitConsumesRifleAndGrantsExactThreeSecondBuff()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var ally = AddNetworkPlayer(world, 2, PlayerTeam.Red, 220f, 100f);
        var enemy = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 340f, 100f);
        ally.ForceSetHealth(ally.MaxHealth - 100);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Guardian]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, []));

        FireRifle(world, sniper, enemy.X, enemy.Y);

        var status = Assert.Single(world.GetLastToDieStatusEffects(ally.Id));
        Assert.Equal(LastToDieStatusEffectKind.BeneficialBuff, status.Kind);
        Assert.Equal(LastToDieSniperProfile.GuardianHealingPerSecond, status.HealingPerSecond);
        Assert.Equal(LastToDieSniperProfile.GuardianEvasionChance, ally.LastToDieGuardianEvasionChance);
        Assert.Equal(enemy.MaxHealth, enemy.Health);

        var startingHealth = ally.Health;
        for (var tick = 0;
            tick < LastToDieSniperProfile.GuardianDurationSeconds * world.Config.TicksPerSecond;
            tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(startingHealth + 36, ally.Health);
        Assert.Equal(0f, ally.LastToDieGuardianEvasionChance);
        Assert.Empty(world.GetLastToDieStatusEffects(ally.Id));
    }

    [Fact]
    public void GuardianHuntsmanArrowBuffsNearestAllyAndIsConsumed()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var ally = AddNetworkPlayer(world, 2, PlayerTeam.Red, 200f, 100f);
        var enemy = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 300f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Guardian]));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(2, []));

        var arrow = SpawnTestArrow(
            world,
            sniper,
            velocityX: 300f,
            damage: PlayerEntity.SniperBowMinDamage);
        Assert.True(arrow.AppliesLastToDieGuardian);

        world.AdvanceOneTick();

        Assert.DoesNotContain(arrow, world.Needles);
        Assert.Equal(ally.MaxHealth, ally.Health);
        Assert.Equal(LastToDieSniperProfile.GuardianEvasionChance, ally.LastToDieGuardianEvasionChance);
        Assert.Equal(enemy.MaxHealth, enemy.Health);
    }

    [Fact]
    public void FullyChargedMechanicaArrowPiercesEveryEnemyInItsSweep()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var first = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 200f, 100f);
        var second = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 300f, 100f);
        var third = AddNetworkPlayer(world, 4, PlayerTeam.Blue, 400f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Mechanica]));

        var arrow = SpawnTestArrow(
            world,
            sniper,
            velocityX: 400f,
            damage: PlayerEntity.SniperBowMaxDamage);
        Assert.True(arrow.PiercesPlayers);

        world.AdvanceOneTick();

        Assert.Equal(first.MaxHealth - PlayerEntity.SniperBowMaxDamage, first.Health);
        Assert.Equal(second.MaxHealth - PlayerEntity.SniperBowMaxDamage, second.Health);
        Assert.Equal(third.MaxHealth - PlayerEntity.SniperBowMaxDamage, third.Health);
        Assert.Contains(arrow, world.Needles);
    }

    [Fact]
    public void PartialChargeMechanicaArrowStopsAtFirstEnemyAndReflectionClearsPayload()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var first = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 200f, 100f);
        var second = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 300f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Guardian, LastToDiePerkIds.Sniper.Mechanica]));

        var arrow = SpawnTestArrow(
            world,
            sniper,
            velocityX: 300f,
            damage: PlayerEntity.SniperBowMaxDamage - 1);
        Assert.True(arrow.AppliesLastToDieGuardian);
        Assert.False(arrow.PiercesPlayers);

        world.AdvanceOneTick();

        Assert.Equal(first.MaxHealth - (PlayerEntity.SniperBowMaxDamage - 1), first.Health);
        Assert.Equal(second.MaxHealth, second.Health);
        Assert.DoesNotContain(arrow, world.Needles);

        arrow.ConfigureLastToDiePayload(appliesGuardian: true, piercesPlayers: true);
        arrow.Reflect(first.Id, PlayerTeam.Blue, directionRadians: MathF.PI);
        Assert.False(arrow.AppliesLastToDieGuardian);
        Assert.False(arrow.PiercesPlayers);
    }

    [Fact]
    public void Protocol64PublisherAndHydratorPreserveArrowKindAndPerkPayload()
    {
        var source = CreateSniperCombatWorld();
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [
                LastToDiePerkIds.Sniper.Guardian,
                LastToDiePerkIds.Sniper.Mechanica,
                LastToDiePerkIds.Sniper.TranqDarts,
                LastToDiePerkIds.Sniper.PoisonTip,
                LastToDiePerkIds.Sniper.Ghost,
                LastToDiePerkIds.Sniper.Decapitator,
            ]));
        Assert.True(source.LocalPlayer.TryActivateLastToDieSniperGhostCloak());
        var sourceArrow = SpawnTestArrow(
            source,
            source.LocalPlayer,
            velocityX: 18f,
            damage: PlayerEntity.SniperBowMaxDamage);
        Assert.True(sourceArrow.TryAttachLastToDieDecapitatedHead(
            PlayerClass.Heavy,
            PlayerTeam.Blue));

        var publisher = new Protocol64StatePublisher(source);
        var state = Assert.Single(publisher.BuildProjectileStates(12));
        Assert.Equal(Protocol64ProjectileKind.Arrow, state.EntityKind);
        Assert.True(state.AppliesLastToDieGuardian);
        Assert.True(state.PiercesPlayers);
        Assert.True(state.AppliesLastToDieTranqDarts);
        Assert.Equal(20f, state.LastToDiePoisonDamagePerSecond);
        Assert.Equal(3f, state.LastToDieGhostDamageMultiplier);
        Assert.True(state.AppliesLastToDieDecapitator);
        Assert.True(state.IsLastToDieDecapitatorFullyCharged);
        Assert.Equal((byte)PlayerClass.Heavy, state.LastToDieAttachedHeadClassId);
        Assert.Equal((byte)PlayerTeam.Blue, state.LastToDieAttachedHeadTeam);

        var receiver = CreateSniperCombatWorld();
        Assert.True(receiver.ApplyProtocol64ProjectileState(state));
        var hydratedArrow = Assert.IsType<ArrowProjectileEntity>(Assert.Single(receiver.Needles));
        Assert.Equal(sourceArrow.Damage, hydratedArrow.Damage);
        Assert.Equal(sourceArrow.FakeSpeedMultiplier, hydratedArrow.FakeSpeedMultiplier);
        Assert.True(hydratedArrow.AppliesLastToDieGuardian);
        Assert.True(hydratedArrow.PiercesPlayers);
        Assert.True(hydratedArrow.AppliesLastToDieTranqDarts);
        Assert.Equal(20f, hydratedArrow.LastToDiePoisonDamagePerSecond);
        Assert.Equal(3f, hydratedArrow.LastToDieGhostDamageMultiplier);
        Assert.True(hydratedArrow.AppliesLastToDieDecapitator);
        Assert.True(hydratedArrow.IsLastToDieDecapitatorFullyCharged);
        Assert.Equal(PlayerClass.Heavy, hydratedArrow.LastToDieAttachedHeadClassId);
        Assert.Equal(PlayerTeam.Blue, hydratedArrow.LastToDieAttachedHeadTeam);
    }

    [Fact]
    public void LightMarksmanClearsChargeDisablesScopeAndDealsSixtyDamage()
    {
        var world = CreateSniperWorld();
        var player = world.LocalPlayer;
        Assert.True(player.TryToggleSniperScope());
        AdvanceSourceTicks(player, 20);
        Assert.Equal(20, player.SniperChargeTicks);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.LightMarksman]));

        Assert.False(player.IsSniperScoped);
        Assert.Equal(0, player.SniperChargeTicks);
        Assert.False(player.TryToggleSniperScope());
        Assert.Equal(60, player.GetSniperRifleDamageForCharge(120, isScoped: true));
    }

    [Fact]
    public void ExtremeConditioningRemovesScopePenaltiesAndAddsMovementSpeed()
    {
        var world = CreateSniperWorld();
        var player = world.LocalPlayer;
        var stockMaxRunSpeed = player.MaxRunSpeed;
        Assert.True(player.TryToggleSniperScope());
        Assert.Equal(PlayerEntity.SniperScopedMoveScale, InvokeMovementScale(player), precision: 5);
        Assert.Equal(PlayerEntity.SniperScopedJumpScale, InvokeJumpScale(player), precision: 5);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.ExtremeConditioning]));

        Assert.Equal(stockMaxRunSpeed * 1.2f, player.MaxRunSpeed, precision: 5);
        Assert.Equal(1f, InvokeMovementScale(player));
        Assert.Equal(1f, InvokeJumpScale(player));
    }

    [Fact]
    public void OverchargedHuntsmanReachesFullPowerAtFifteenTicks()
    {
        var world = CreateSniperWorld(enableExperimentalWeapons: true);
        var player = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Overcharged]));
        player.EquipExperimentalOffhandWeapon();
        Assert.True(player.IsSniperBowEquipped);
        Assert.True(player.TryStartSniperBowCharge(0f));
        for (var tick = 1; tick < 15; tick += 1)
        {
            player.IncrementSniperBowCharge(0f);
        }

        Assert.Equal(15, player.SniperBowChargeTicks);
        Assert.True(player.TryReleaseSniperBowCharge(
            out _,
            out _,
            out var damage,
            out var fakeSpeedMultiplier));
        Assert.Equal(PlayerEntity.SniperBowMaxDamage, damage);
        Assert.Equal(PlayerEntity.SniperBowMaxFakeSpeedMultiplier, fakeSpeedMultiplier, precision: 5);
    }

    [Fact]
    public void PredictionAndProtocol64HydrateTheClassSpecificSniperProfile()
    {
        var source = CreateSniperWorld();
        var perks = new[]
        {
            LastToDiePerkIds.Sniper.Overcharged,
            LastToDiePerkIds.Sniper.GreasedBolt,
            LastToDiePerkIds.Sniper.ExtremeConditioning,
            LastToDiePerkIds.Sniper.Spotted,
            LastToDiePerkIds.Sniper.Conquistador,
            LastToDiePerkIds.Sniper.TranqDarts,
            LastToDiePerkIds.Sniper.PoisonTip,
            LastToDiePerkIds.Sniper.Ghost,
            LastToDiePerkIds.Sniper.Overkiller,
            LastToDiePerkIds.Sniper.Decapitator,
            LastToDiePerkIds.Sniper.MenageATrois,
            LastToDiePerkIds.Sniper.ExplosiveTip,
        };
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            perks));
        source.LocalPlayer.SetLastToDieSniperMarkedTargetSlot(2);
        Assert.True(source.LocalPlayer.TryIncrementLastToDieSniperConquistadorStacks());
        Assert.True(source.LocalPlayer.TryActivateLastToDieSniperGhostCloak());
        source.LocalPlayer.BeginLastToDieSniperVolley(
            12f,
            -3f,
            PlayerEntity.SniperBowMaxDamage,
            PlayerEntity.SniperBowMaxFakeSpeedMultiplier,
            new LastToDieSniperArrowPayload(
                AppliesGuardian: true,
                PiercesPlayers: true,
                AppliesTranqDarts: true,
                PoisonDamagePerSecond: 14f,
                GhostDamageMultiplier: 3f,
                AppliesDecapitator: true,
                IsDecapitatorFullyCharged: true,
                AppliesExplosiveTip: true,
                IsCritical: true,
                CriticalDamageMultiplier: ExperimentalGameplaySettings.KritzCriticalDamageMultiplier));
        var expected = source.LocalPlayer.LastToDieSniperProfile;

        var predictionClone = new PlayerEntity(5000, CharacterClassCatalog.Sniper, "PredictionClone");
        predictionClone.RestorePredictionState(source.LocalPlayer.CapturePredictionState());
        Assert.Equal(expected, predictionClone.LastToDieSniperProfile);
        Assert.Equal((byte)2, predictionClone.LastToDieSniperMarkedTargetSlot);
        Assert.Equal(1, predictionClone.LastToDieSniperConquistadorStacks);
        Assert.True(predictionClone.IsLastToDieSniperGhostCloaked);
        Assert.Equal(source.LocalPlayer.LastToDieSniperVolleyState, predictionClone.LastToDieSniperVolleyState);

        var publisher = new Protocol64StatePublisher(source);
        var state = Assert.Single(publisher.BuildPlayerStateBatch(12).Players);
        Assert.Equal((ushort)expected.Encode(), state.LastToDieSpyRevolverState);
        Assert.Equal(source.LocalPlayer.LastToDieSniperRuntimeState, state.LastToDieSniperRuntimeState);
        Assert.Equal(source.LocalPlayer.LastToDieSniperExtensionState, state.LastToDieSniperExtensionState);
        Assert.NotNull(state.LastToDieSniperVolleyState);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(state));
        Assert.Equal(expected, receiver.LocalPlayer.LastToDieSniperProfile);
        Assert.Equal((byte)2, receiver.LocalPlayer.LastToDieSniperMarkedTargetSlot);
        Assert.Equal(1, receiver.LocalPlayer.LastToDieSniperConquistadorStacks);
        Assert.True(receiver.LocalPlayer.IsLastToDieSniperGhostCloaked);
        Assert.True(receiver.LocalPlayer.LastToDieSniperProfile.DecapitatorEnabled);
        Assert.True(receiver.LocalPlayer.LastToDieSniperProfile.MenageATroisEnabled);
        Assert.True(receiver.LocalPlayer.LastToDieSniperProfile.ExplosiveTipEnabled);
        Assert.Equal(source.LocalPlayer.LastToDieSniperVolleyState, receiver.LocalPlayer.LastToDieSniperVolleyState);
        Assert.Equal(45, receiver.LocalPlayer.LastToDieSniperRifleFullChargeTicks);
    }

    [Fact]
    public void SpottedRifleMarksOnFirstDamageAndDoublesTheNextHit()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted]));

        FireRifle(world, sniper, target.X, target.Y);

        Assert.Equal(target.MaxHealth - PlayerEntity.SniperBaseDamage, target.Health);
        Assert.Equal((byte)2, sniper.LastToDieSniperMarkedTargetSlot);

        AdvanceSourceTicks(sniper, 40);
        FireRifle(world, sniper, target.X, target.Y);

        Assert.Equal(target.MaxHealth - (PlayerEntity.SniperBaseDamage * 3), target.Health);
        Assert.Equal((byte)2, sniper.LastToDieSniperMarkedTargetSlot);
    }

    [Fact]
    public void SpottedTransfersOnlyAfterActualDamage()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var first = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        var second = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 220f, 220f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted]));

        FireRifle(world, sniper, first.X, first.Y);
        Assert.Equal((byte)2, sniper.LastToDieSniperMarkedTargetSlot);

        AdvanceSourceTicks(sniper, 40);
        FireRifle(world, sniper, second.X, second.Y);
        Assert.Equal(second.MaxHealth - PlayerEntity.SniperBaseDamage, second.Health);
        Assert.Equal((byte)3, sniper.LastToDieSniperMarkedTargetSlot);

        AdvanceSourceTicks(sniper, 40);
        FireRifle(world, sniper, first.X, first.Y);
        Assert.Equal(first.MaxHealth - (PlayerEntity.SniperBaseDamage * 2), first.Health);
        Assert.Equal((byte)2, sniper.LastToDieSniperMarkedTargetSlot);
    }

    [Fact]
    public void SpottedHuntsmanUsesTheSameFirstHitThenBonusContract()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted]));

        _ = SpawnTestArrow(world, sniper, 300f, PlayerEntity.SniperBowMinDamage);
        world.AdvanceOneTick();
        Assert.Equal((byte)2, sniper.LastToDieSniperMarkedTargetSlot);
        Assert.Equal(target.MaxHealth - PlayerEntity.SniperBowMinDamage, target.Health);

        _ = SpawnTestArrow(world, sniper, 300f, PlayerEntity.SniperBowMinDamage);
        world.AdvanceOneTick();
        Assert.Equal(target.MaxHealth - (PlayerEntity.SniperBowMinDamage * 3), target.Health);
    }

    [Fact]
    public void SpottedBenefitOnlyPeriodicDamageUsesButNeverMovesTheMark()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var marked = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        var unmarked = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 220f, 220f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted]));
        sniper.SetLastToDieSniperMarkedTargetSlot(2);

        var markedResolution = world.ResolvePlayerDamage(
            marked,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                10,
                sniper,
                0f,
                DamageEventFlags.StatusTick,
                PlayerDamageTraits.Periodic | PlayerDamageTraits.BenefitFromLastToDieSpotted,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));
        var unmarkedResolution = world.ResolvePlayerDamage(
            unmarked,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                10,
                sniper,
                0f,
                DamageEventFlags.StatusTick,
                PlayerDamageTraits.Periodic | PlayerDamageTraits.BenefitFromLastToDieSpotted,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));

        Assert.Equal(20, markedResolution.AppliedHealthDamage);
        Assert.Equal(10, unmarkedResolution.AppliedHealthDamage);
        Assert.Equal((byte)2, sniper.LastToDieSniperMarkedTargetSlot);
    }

    [Fact]
    public void FullyShieldedSpottedHitDoesNotEstablishAMark()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        target.SetExperimentalShieldHealth(100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted]));

        FireRifle(world, sniper, target.X, target.Y);

        Assert.Equal(target.MaxHealth, target.Health);
        Assert.Equal((byte)0, sniper.LastToDieSniperMarkedTargetSlot);
    }

    [Fact]
    public void ConquistadorEnemyKillIncrementsAndScalesSubsequentDamage()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var victim = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.FiftyCal, LastToDiePerkIds.Sniper.Conquistador]));

        FireRifle(world, sniper, victim.X, victim.Y);

        Assert.False(victim.IsAlive);
        Assert.Equal(1, sniper.LastToDieSniperConquistadorStacks);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Conquistador]));
        var nextTarget = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 340f, 100f);
        AdvanceSourceTicks(sniper, 100);
        FireRifle(world, sniper, nextTarget.X, nextTarget.Y);

        Assert.Equal(nextTarget.MaxHealth - 36, nextTarget.Health);
    }

    [Fact]
    public void SpottedAndConquistadorShareOneAdditiveSniperDamageBucket()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TrySetNetworkPlayerMaxHealthOverride(2, 500, refillHealth: true));
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted, LastToDiePerkIds.Sniper.Conquistador]));
        for (var stack = 0; stack < 105; stack += 1)
        {
            _ = sniper.TryIncrementLastToDieSniperConquistadorStacks();
        }
        Assert.Equal(LastToDieSniperProfile.ConquistadorMaximumStacks,
            sniper.LastToDieSniperConquistadorStacks);

        FireRifle(world, sniper, target.X, target.Y);
        Assert.Equal(target.MaxHealth - 105, target.Health);

        AdvanceSourceTicks(sniper, 40);
        FireRifle(world, sniper, target.X, target.Y);
        Assert.Equal(target.MaxHealth - 245, target.Health);
    }

    [Fact]
    public void SniperDynamicStateClearsOnDeathTargetDeathAndPerkRemoval()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted, LastToDiePerkIds.Sniper.Conquistador]));

        FireRifle(world, sniper, target.X, target.Y);
        Assert.True(sniper.TryIncrementLastToDieSniperConquistadorStacks());
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            []));
        Assert.Equal((byte)0, sniper.LastToDieSniperMarkedTargetSlot);
        Assert.Equal(0, sniper.LastToDieSniperConquistadorStacks);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted, LastToDiePerkIds.Sniper.Conquistador]));
        sniper.SetLastToDieSniperMarkedTargetSlot(2);
        Assert.True(sniper.TryIncrementLastToDieSniperConquistadorStacks());
        world.ForceKillLocalPlayer();
        Assert.Equal((byte)0, sniper.LastToDieSniperMarkedTargetSlot);
        Assert.Equal(0, sniper.LastToDieSniperConquistadorStacks);
    }

    [Fact]
    public void LethalSpottedHitClearsEveryMarkTargetingTheDeadSlot()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted, LastToDiePerkIds.Sniper.FiftyCal]));

        FireRifle(world, sniper, target.X, target.Y);

        Assert.False(target.IsAlive);
        Assert.Equal((byte)0, sniper.LastToDieSniperMarkedTargetSlot);
    }

    [Fact]
    public void SameBuildPreservesConquistadorAndRunSeedRestoreUsesCheckpoint()
    {
        var world = CreateSniperWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Conquistador]));
        Assert.True(world.LocalPlayer.TryIncrementLastToDieSniperConquistadorStacks());
        Assert.True(world.LocalPlayer.TryIncrementLastToDieSniperConquistadorStacks());

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Conquistador],
            resetDynamicState: true));
        Assert.Equal(2, world.LocalPlayer.LastToDieSniperConquistadorStacks);

        world.ConfigureLastToDieCombatSeed(999);
        Assert.Equal(0, world.LocalPlayer.LastToDieSniperConquistadorStacks);
        Assert.True(world.TryRestoreLastToDieSniperConquistadorStacks(
            SimulationWorld.LocalPlayerSlot,
            73));
        Assert.Equal(73, world.LocalPlayer.LastToDieSniperConquistadorStacks);
    }

    [Fact]
    public void Protocol64RedactsSpottedTargetFromEnemiesButKeepsConquistadorStacks()
    {
        var world = CreateSniperCombatWorld();
        _ = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Spotted, LastToDiePerkIds.Sniper.Conquistador]));
        world.LocalPlayer.SetLastToDieSniperMarkedTargetSlot(2);
        Assert.True(world.LocalPlayer.TryIncrementLastToDieSniperConquistadorStacks());
        var publisher = new Protocol64StatePublisher(world);

        var ownerView = Assert.Single(
            publisher.BuildPlayerStateBatch(1, viewerSlot: 1).Players,
            player => player.Slot == 1);
        var enemyView = Assert.Single(
            publisher.BuildPlayerStateBatch(2, viewerSlot: 2).Players,
            player => player.Slot == 1);

        Assert.Equal(2, ownerView.LastToDieSniperRuntimeState & 0x7f);
        Assert.Equal(0, enemyView.LastToDieSniperRuntimeState & 0x7f);
        Assert.Equal(1, enemyView.LastToDieSniperRuntimeState >> 7);
    }

    [Fact]
    public void TranqRifleDealsFortyPercentDamageAndRampsSourceOwnedDebuffs()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.TranqDarts]));

        for (var shot = 0; shot < LastToDieSniperProfile.TranqDartsMaximumSlowStacks; shot += 1)
        {
            FireRifle(world, sniper, target.X, target.Y);
            AdvanceSourceTicks(sniper, 40);
        }

        Assert.Equal(target.MaxHealth - 70, target.Health);
        var poison = Assert.Single(
            world.GetLastToDieStatusEffects(target.Id),
            status => status.Id == LastToDieStatusEffectIds.SniperTranqPoison);
        var slow = Assert.Single(
            world.GetLastToDieStatusEffects(target.Id),
            status => status.Id == LastToDieStatusEffectIds.SniperTranqSlow);
        Assert.Equal(LastToDieSniperProfile.TranqDartsPoisonDamagePerSecond, poison.DamagePerSecond);
        Assert.Equal(LastToDieSniperProfile.TranqDartsMaximumSlowStacks, slow.StackCount);
        Assert.Equal(0.5f, slow.MovementSpeedMultiplier, precision: 5);
        Assert.Equal(0.6f, slow.OutgoingDamageMultiplier, precision: 5);
        Assert.Equal(0.5f, target.LastToDieStatusMovementSpeedMultiplier, precision: 5);
        Assert.Equal(0.6f, target.LastToDieStatusOutgoingDamageMultiplier, precision: 5);

        var outgoingResolution = world.ResolvePlayerDamage(
            sniper,
            new PlayerDamageRequest(
                PlayerDamageApplicationKind.Instant,
                100,
                target,
                0f,
                DamageEventFlags.None,
                PlayerDamageTraits.None,
                AllowOsmosisHealOwnedSentries: false,
                new PlayerDamageUmbrellaOptions(AllowBlock: false)));
        Assert.Equal(60, outgoingResolution.AppliedHealthDamage);
    }

    [Fact]
    public void TranqStatusExpiresAfterFourSecondsAndRestoresBothDebuffs()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.TranqDarts]));

        FireRifle(world, sniper, target.X, target.Y);
        var healthAfterDirectHit = target.Health;
        for (var tick = 0;
            tick < LastToDieSniperProfile.TranqDartsDurationSeconds * LegacyMovementModel.SourceTicksPerSecond;
            tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(
            healthAfterDirectHit - (int)(LastToDieSniperProfile.TranqDartsPoisonDamagePerSecond
                * LastToDieSniperProfile.TranqDartsDurationSeconds),
            target.Health);
        Assert.Empty(world.GetLastToDieStatusEffects(target.Id));
        Assert.Equal(1f, target.LastToDieStatusMovementSpeedMultiplier);
        Assert.Equal(1f, target.LastToDieStatusOutgoingDamageMultiplier);
    }

    [Fact]
    public void PoisonTipCapturesLinearChargePotencyAndKeepsStrongestRefresh()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.PoisonTip]));

        var halfChargeArrow = SpawnTestArrow(
            world,
            sniper,
            300f,
            PlayerEntity.GetSniperBowDamageForChargeFraction(0.5f),
            PlayerEntity.GetSniperBowFakeSpeedMultiplierForChargeFraction(0.5f));
        Assert.Equal(14.5f, halfChargeArrow.LastToDiePoisonDamagePerSecond, precision: 5);
        world.AdvanceOneTick();

        _ = SpawnTestArrow(
            world,
            sniper,
            300f,
            PlayerEntity.SniperBowMaxDamage,
            PlayerEntity.SniperBowMaxFakeSpeedMultiplier);
        world.AdvanceOneTick();
        _ = SpawnTestArrow(
            world,
            sniper,
            300f,
            PlayerEntity.SniperBowMinDamage,
            PlayerEntity.SniperBowMinFakeSpeedMultiplier);
        world.AdvanceOneTick();

        var poison = Assert.Single(
            world.GetLastToDieStatusEffects(target.Id),
            status => status.Id == LastToDieStatusEffectIds.SniperPoisonTip);
        Assert.Equal(LastToDieSniperProfile.PoisonTipMaximumDamagePerSecond, poison.DamagePerSecond);
        Assert.Equal(
            (LastToDieSniperProfile.PoisonTipDurationSeconds * LegacyMovementModel.SourceTicksPerSecond) - 1,
            poison.RemainingTicks);
    }

    [Fact]
    public void HuntsmanPayloadSurvivesPerkRemovalAndReflectionClearsIt()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.TranqDarts, LastToDiePerkIds.Sniper.PoisonTip]));

        var arrow = SpawnTestArrow(
            world,
            sniper,
            300f,
            PlayerEntity.SniperBowMaxDamage,
            PlayerEntity.SniperBowMaxFakeSpeedMultiplier);
        Assert.True(arrow.AppliesLastToDieTranqDarts);
        Assert.Equal(20f, arrow.LastToDiePoisonDamagePerSecond);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));

        world.AdvanceOneTick();

        Assert.Equal(target.MaxHealth - 30, target.Health);
        Assert.Contains(
            world.GetLastToDieStatusEffects(target.Id),
            status => status.Id == LastToDieStatusEffectIds.SniperTranqPoison);
        Assert.Contains(
            world.GetLastToDieStatusEffects(target.Id),
            status => status.Id == LastToDieStatusEffectIds.SniperPoisonTip);

        arrow.ConfigureLastToDiePayload(
            appliesGuardian: false,
            piercesPlayers: false,
            appliesTranqDarts: true,
            poisonDamagePerSecond: 20f);
        arrow.Reflect(target.Id, PlayerTeam.Blue, directionRadians: MathF.PI);
        Assert.False(arrow.AppliesLastToDieTranqDarts);
        Assert.Equal(0f, arrow.LastToDiePoisonDamagePerSecond);
    }

    [Fact]
    public void RemovingSniperProfileRestoresStockBehavior()
    {
        var world = CreateSniperWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Overcharged, LastToDiePerkIds.Sniper.ExtremeConditioning]));
        Assert.Equal(45, world.LocalPlayer.LastToDieSniperRifleFullChargeTicks);

        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            []));

        Assert.Equal(LastToDieSniperProfile.Stock, world.LocalPlayer.LastToDieSniperProfile);
        Assert.Equal(PlayerEntity.SniperChargeMaxTicks, world.LocalPlayer.LastToDieSniperRifleFullChargeTicks);
        Assert.False(world.LocalPlayer.TryGetReplicatedStateInt(
            PlayerEntity.LastToDieWeaponReplicatedStateOwnerId,
            PlayerEntity.LastToDieSniperProfileReplicatedStateKey,
            out _));
    }

    [Fact]
    public void ExtensionSniperPerksRoundTripThroughSecondSniperWord()
    {
        var profile = LastToDieSniperProfile.FromPerks(new HashSet<LastToDiePerkId>
        {
            LastToDiePerkIds.Sniper.Ghost,
            LastToDiePerkIds.Sniper.Overkiller,
            LastToDiePerkIds.Sniper.Decapitator,
            LastToDiePerkIds.Sniper.MenageATrois,
            LastToDiePerkIds.Sniper.ExplosiveTip,
        });

        Assert.True(profile.GhostEnabled);
        Assert.True(profile.OverkillerEnabled);
        Assert.True(profile.DecapitatorEnabled);
        Assert.True(profile.MenageATroisEnabled);
        Assert.True(profile.ExplosiveTipEnabled);
        Assert.Equal(0, profile.Encode());
        Assert.Equal(profile, LastToDieSniperProfile.Decode(profile.Encode(), profile.EncodeExtensionProfile()));
    }

    [Fact]
    public void MenageATroisQueuesExactlyThreeReleaseTimeArrowsAtThreeSourceTickIntervals()
    {
        var world = CreateSniperWorld(enableExperimentalWeapons: true);
        var sniper = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.MenageATrois, LastToDiePerkIds.Sniper.ExplosiveTip]));
        sniper.EquipExperimentalOffhandWeapon();
        Assert.True(sniper.IsSniperBowEquipped);
        var ammoBefore = sniper.ExperimentalOffhandCurrentShells;
        Assert.True(sniper.TryFireExperimentalOffhandWeapon());

        _ = SpawnTestArrow(
            world,
            sniper,
            velocityX: 1f,
            damage: PlayerEntity.SniperBowMaxDamage);

        Assert.Equal(ammoBefore - 1, sniper.ExperimentalOffhandCurrentShells);
        Assert.Single(world.Needles);
        Assert.Equal((byte)2, sniper.LastToDieSniperVolleyState.QueuedArrowCount);
        Assert.Equal((byte)3, sniper.LastToDieSniperVolleyState.SourceTicksUntilNextArrow);

        // A later perk rebuild cannot mutate the already accepted release.
        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));
        for (var tick = 0; tick < 2; tick += 1)
        {
            world.AdvanceOneTick();
        }
        Assert.Single(world.Needles);

        world.AdvanceOneTick();
        Assert.Equal(2, world.Needles.Count);
        for (var tick = 0; tick < 3; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(3, world.Needles.Count);
        Assert.All(
            world.Needles.Cast<ArrowProjectileEntity>(),
            static arrow => Assert.True(arrow.AppliesLastToDieExplosiveTip));
        Assert.False(sniper.LastToDieSniperVolleyState.IsActive);
        Assert.Equal(ammoBefore - 1, sniper.ExperimentalOffhandCurrentShells);
    }

    [Fact]
    public void MenageATroisCancelsDelayedArrowsOnDeath()
    {
        var world = CreateSniperWorld(enableExperimentalWeapons: true);
        var sniper = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.MenageATrois]));
        sniper.EquipExperimentalOffhandWeapon();
        _ = SpawnTestArrow(world, sniper, 1f, PlayerEntity.SniperBowMaxDamage);

        sniper.ForceSetHealth(0);
        for (var tick = 0; tick < 20; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(sniper.LastToDieSniperVolleyState.IsActive);
        Assert.Single(world.Needles);
    }

    [Fact]
    public void ExplosiveTipSecondaryRisingEdgeDetonatesAllOwnedArrowsOnce()
    {
        var world = CreateSniperWorld(enableExperimentalWeapons: true);
        var sniper = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.ExplosiveTip]));
        sniper.EquipExperimentalOffhandWeapon();
        _ = SpawnTestArrow(world, sniper, 0f, PlayerEntity.SniperBowMinDamage);
        _ = SpawnTestArrow(world, sniper, 0f, PlayerEntity.SniperBowMinDamage);
        Assert.Equal(2, world.Needles.Count);

        world.SetLocalPreviousInput(default);
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: sniper.X + 100f,
            AimWorldY: sniper.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        Assert.Empty(world.Needles);
        Assert.Equal(2, world.PendingVisualEvents.Count(effect => effect.EffectName == "Explosion"));
        Assert.Equal(2, world.PendingSoundEvents.Count(sound => sound.SoundName == "ExplosionSnd"));
    }

    [Fact]
    public void ExplosiveTipUsesFalloffSelfScaleNoFriendlyDamageAndPerTargetDedupe()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var teammate = AddNetworkPlayer(world, 2, PlayerTeam.Red, 150f, 100f);
        var enemy = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 180f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.ExplosiveTip]));
        var arrow = SpawnTestArrow(world, sniper, 0f, PlayerEntity.SniperBowMinDamage);

        Assert.True(InvokeDetonateOwnedExplosiveArrows(world, sniper));

        Assert.Equal(sniper.MaxHealth - 40, sniper.Health);
        Assert.Equal(teammate.MaxHealth, teammate.Health);
        Assert.InRange(enemy.MaxHealth - enemy.Health, 40, 80);
        Assert.Single(world.DrainPendingDamageEvents(), damage => damage.TargetEntityId == enemy.Id);
        Assert.False(arrow.IsLastToDieExplosiveTipArmed);
        Assert.False(InvokeDetonateOwnedExplosiveArrows(world, sniper));
        Assert.Single(world.PendingVisualEvents, effect => effect.EffectName == "Explosion");
    }

    [Fact]
    public void ExplosiveTipCollisionAutoDetonatesWithoutDuplicateEffects()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        _ = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.ExplosiveTip]));
        _ = SpawnTestArrow(world, sniper, 500f, PlayerEntity.SniperBowMinDamage);

        AdvanceNeedles(world);

        Assert.Empty(world.Needles);
        Assert.Single(world.PendingVisualEvents, effect => effect.EffectName == "Explosion");
        Assert.Single(world.PendingSoundEvents, sound => sound.SoundName == "ExplosionSnd");
    }

    [Fact]
    public void DecapitatorRequiresFullChargeAndHeadIntersectionForRifleExecute()
    {
        var bodyWorld = CreateSniperCombatWorld();
        var bodySniper = bodyWorld.LocalPlayer;
        var bodyTarget = AddNetworkPlayer(bodyWorld, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(bodyWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Decapitator]));
        Assert.True(bodySniper.TryToggleSniperScope());
        AdvanceSourceTicks(bodySniper, bodySniper.LastToDieSniperRifleFullChargeTicks);
        var bodyBounds = bodyWorld.CombatTestGetPlayerPresentationHitBounds(bodyTarget);

        FireRifle(
            bodyWorld,
            bodySniper,
            (bodyBounds.Left + bodyBounds.Right) * 0.5f,
            (bodyBounds.Top + bodyBounds.Bottom) * 0.5f);

        Assert.True(bodyTarget.IsAlive);
        Assert.Equal(bodyTarget.MaxHealth - 85, bodyTarget.Health);

        var unchargedWorld = CreateSniperCombatWorld();
        var unchargedSniper = unchargedWorld.LocalPlayer;
        var unchargedTarget = AddNetworkPlayer(unchargedWorld, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(unchargedWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Decapitator]));
        var headAim = GetDecapitatorHeadAim(unchargedWorld, unchargedTarget);

        FireRifle(unchargedWorld, unchargedSniper, headAim.X, headAim.Y);

        Assert.True(unchargedTarget.IsAlive);
        Assert.Equal(
            unchargedTarget.MaxHealth - PlayerEntity.SniperBaseDamage,
            unchargedTarget.Health);
    }

    [Fact]
    public void FullyChargedDecapitatorRifleHeadshotExecutesWithDemoknightRemains()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Decapitator]));
        Assert.True(sniper.TryToggleSniperScope());
        AdvanceSourceTicks(sniper, sniper.LastToDieSniperRifleFullChargeTicks);
        var headAim = GetDecapitatorHeadAim(world, target);

        FireRifle(world, sniper, headAim.X, headAim.Y);

        Assert.False(target.IsAlive);
        Assert.Equal(DeadBodyAnimationKind.Decapitated, Assert.Single(world.DeadBodies).AnimationKind);
        Assert.Contains(
            world.PlayerGibs,
            gib => gib.SpriteName == ExperimentalDemoknightCatalog.GetDecapitatedHeadSpriteName(
                target.ClassId,
                target.Team));
    }

    [Fact]
    public void FullyChargedMechanicaRifleExecutesEveryOrderedHeadIntersection()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        var first = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        var second = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 340f, 100f);
        var third = AddNetworkPlayer(world, 4, PlayerTeam.Blue, 460f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Decapitator, LastToDiePerkIds.Sniper.Mechanica]));
        Assert.True(sniper.TryToggleSniperScope());
        AdvanceSourceTicks(sniper, sniper.LastToDieSniperRifleFullChargeTicks);
        var headAim = GetDecapitatorHeadAim(world, third);
        sniper.TeleportTo(100f, headAim.Y);

        FireRifle(world, sniper, headAim.X, headAim.Y);

        Assert.False(first.IsAlive);
        Assert.False(second.IsAlive);
        Assert.False(third.IsAlive);
        Assert.Equal(3, world.DeadBodies.Count(body =>
            body.AnimationKind == DeadBodyAnimationKind.Decapitated));
    }

    [Fact]
    public void DecapitatorArrowCapturesChargeCarriesFirstHeadAndCleansOnReflection()
    {
        var world = CreateSniperCombatWorld([new LevelSolid(500f, 0f, 20f, 480f)]);
        var sniper = world.LocalPlayer;
        var first = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        var second = AddNetworkPlayer(world, 3, PlayerTeam.Blue, 340f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Decapitator, LastToDiePerkIds.Sniper.Mechanica]));
        var headAim = GetDecapitatorHeadAim(world, first);
        sniper.TeleportTo(100f, headAim.Y);

        var arrow = SpawnTestArrow(
            world,
            sniper,
            velocityX: 500f,
            damage: PlayerEntity.SniperBowMaxDamage);
        AdvanceNeedles(world);

        Assert.False(first.IsAlive);
        Assert.False(second.IsAlive);
        Assert.True(arrow.IsLanded);
        Assert.True(arrow.HasLastToDieAttachedHead);
        Assert.Equal(first.ClassId, arrow.LastToDieAttachedHeadClassId);
        Assert.Equal(first.Team, arrow.LastToDieAttachedHeadTeam);
        Assert.Equal("HeavyBlueHeadS", arrow.LastToDieAttachedHeadSpriteName);
        Assert.Single(world.PlayerGibs, gib => gib.SpriteName == "HeavyBlueHeadS");

        arrow.Reflect(first.Id, PlayerTeam.Blue, MathF.PI);

        Assert.False(arrow.AppliesLastToDieDecapitator);
        Assert.False(arrow.IsLastToDieDecapitatorFullyCharged);
        Assert.False(arrow.HasLastToDieAttachedHead);
        Assert.Null(arrow.LastToDieAttachedHeadSpriteName);

        arrow.ConfigureLastToDiePayload(
            appliesGuardian: false,
            piercesPlayers: false,
            appliesDecapitator: true,
            isDecapitatorFullyCharged: true,
            attachedHeadClassId: PlayerClass.Medic,
            attachedHeadTeam: PlayerTeam.Red);
        arrow.Destroy();
        Assert.False(arrow.HasLastToDieAttachedHead);
        Assert.False(arrow.AppliesLastToDieDecapitator);
    }

    [Fact]
    public void UnchargedDecapitatorArrowCanHitHeadZoneWithoutExecuting()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Decapitator]));
        var headAim = GetDecapitatorHeadAim(world, target);
        sniper.TeleportTo(100f, headAim.Y);

        var arrow = SpawnTestArrow(
            world,
            sniper,
            velocityX: 300f,
            damage: PlayerEntity.SniperBowMinDamage,
            fakeSpeedMultiplier: PlayerEntity.SniperBowMinFakeSpeedMultiplier);
        AdvanceNeedles(world);

        Assert.True(target.IsAlive);
        Assert.Equal(target.MaxHealth - PlayerEntity.SniperBowMinDamage, target.Health);
        Assert.False(arrow.HasLastToDieAttachedHead);
    }

    [Fact]
    public void GhostSpendsOnlyAcceptedRifleShotAndAppliesThreeTimesDamage()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Ghost]));

        // Establish weapon cooldown before cloaking, then prove a rejected dry
        // trigger cannot spend Ghost.
        FireRifle(world, sniper, 700f, 300f);
        Assert.True(sniper.TryActivateLastToDieSniperGhostCloak());
        Assert.False(sniper.TryFirePrimaryWeapon());
        Assert.True(sniper.IsLastToDieSniperGhostCloaked);
        Assert.Equal(0, sniper.LastToDieSniperGhostCooldownTicksRemaining);

        AdvanceSourceTicks(sniper, sniper.PrimaryCooldownTicks);
        FireRifle(world, sniper, target.X, target.Y);

        Assert.False(sniper.IsLastToDieSniperGhostCloaked);
        Assert.Equal(
            LastToDieSniperProfile.GhostCooldownSeconds * LegacyMovementModel.SourceTicksPerSecond,
            sniper.LastToDieSniperGhostCooldownTicksRemaining);
        Assert.Equal(
            target.MaxHealth - (PlayerEntity.SniperBaseDamage * 3),
            target.Health);
    }

    [Fact]
    public void GhostHuntsmanPayloadSurvivesPerkRemovalAndReflectionClearsIt()
    {
        var world = CreateSniperCombatWorld();
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Ghost]));
        Assert.True(sniper.TryActivateLastToDieSniperGhostCloak());

        var arrow = SpawnTestArrow(
            world,
            sniper,
            300f,
            PlayerEntity.SniperBowMinDamage);
        Assert.Equal(LastToDieSniperProfile.GhostShotDamageMultiplier, arrow.LastToDieGhostDamageMultiplier);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(SimulationWorld.LocalPlayerSlot, []));
        world.AdvanceOneTick();

        Assert.Equal(
            target.MaxHealth - (PlayerEntity.SniperBowMinDamage * 3),
            target.Health);

        arrow.ConfigureLastToDiePayload(
            appliesGuardian: false,
            piercesPlayers: false,
            ghostDamageMultiplier: LastToDieSniperProfile.GhostShotDamageMultiplier);
        arrow.Reflect(target.Id, PlayerTeam.Blue, MathF.PI);
        Assert.Equal(1f, arrow.LastToDieGhostDamageMultiplier);
    }

    [Fact]
    public void OverkillerUsesRunSeededRollAndExecutesAfterPositiveDirectDamage()
    {
        var seed = FindFirstOverkillerSuccessSeed(SimulationWorld.LocalPlayerSlot);
        var world = CreateSniperCombatWorld();
        world.ConfigureLastToDieCombatSeed(seed);
        var sniper = world.LocalPlayer;
        sniper.TeleportTo(100f, 100f);
        var target = AddNetworkPlayer(world, 2, PlayerTeam.Blue, 220f, 100f);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Sniper.Overkiller]));

        FireRifle(world, sniper, target.X, target.Y);

        Assert.False(target.IsAlive);
        Assert.Contains(
            world.DrainPendingDamageEvents(),
            damageEvent => damageEvent.TargetEntityId == target.Id && damageEvent.WasFatal);
    }

    private static ulong FindFirstOverkillerSuccessSeed(byte slot)
    {
        const ulong domain = 0x4F56_4552_4B49_4C4CUL;
        for (ulong seed = 0; seed < 10_000; seed += 1)
        {
            var domainSeed = seed ^ domain;
            var stream = LastToDieRandom.DeriveSeed(domainSeed, slot);
            var random = new LastToDieRandom(
                LastToDieRandom.DeriveSeed(domainSeed, stream),
                stream);
            if (random.NextUInt32()
                < (uint)(LastToDieSniperProfile.OverkillerChance * (uint.MaxValue + 1d)))
            {
                return seed;
            }
        }

        throw new InvalidOperationException("Could not find a deterministic Overkiller success seed.");
    }

    private static SimulationWorld CreateSniperWorld(bool enableExperimentalWeapons = false)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        if (enableExperimentalWeapons)
        {
            Assert.True(world.TryLoadLevel("Harvest"));
        }

        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Sniper);
        if (enableExperimentalWeapons)
        {
            world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings());
            world.AdvanceOneTick();
        }

        return world;
    }

    private static SimulationWorld CreateSniperCombatWorld(
        IReadOnlyList<LevelSolid>? solids = null)
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            EnableLocalDummies = false,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = false,
        });
        world.CombatTestSetLevel(new SimpleLevel(
            name: "ltd_sniper_ordered_hit_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(800f, 480f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(100f, 100f),
            redSpawns: [new SpawnPoint(100f, 100f)],
            blueSpawns: [new SpawnPoint(700f, 100f)],
            intelBases: [],
            roomObjects: [],
            floorY: 480f,
            solids: solids ?? [],
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Sniper);
        return world;
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerTeam team,
        float x,
        float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Heavy));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }

    private static void FireRifle(
        SimulationWorld world,
        PlayerEntity sniper,
        float aimWorldX,
        float aimWorldY)
    {
        Assert.True(sniper.TryFirePrimaryWeapon());
        var method = typeof(SimulationWorld).GetMethod(
            "FirePrimaryWeapon",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [sniper, aimWorldX, aimWorldY]);
    }

    private static ArrowProjectileEntity SpawnTestArrow(
        SimulationWorld world,
        PlayerEntity sniper,
        float velocityX,
        int damage,
        float fakeSpeedMultiplier = PlayerEntity.SniperBowMaxFakeSpeedMultiplier)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "SpawnArrow",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                sniper,
                sniper.X,
                sniper.Y,
                velocityX,
                0f,
                damage,
                fakeSpeedMultiplier,
            ]);
        return Assert.IsType<ArrowProjectileEntity>(world.Needles[^1]);
    }

    private static (float X, float Y) GetDecapitatorHeadAim(
        SimulationWorld world,
        PlayerEntity target)
    {
        var bounds = world.CombatTestGetPlayerPresentationHitBounds(target);
        return (
            (bounds.Left + bounds.Right) * 0.5f,
            bounds.Top - LastToDieSniperProfile.DecapitatorHeadshotZoneSize + 0.01f);
    }

    private static void AdvanceNeedles(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "AdvanceNeedles",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, null);
    }

    private static bool InvokeDetonateOwnedExplosiveArrows(
        SimulationWorld world,
        PlayerEntity owner)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "DetonateOwnedLastToDieSniperArrows",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (bool)method!.Invoke(world, [owner])!;
    }

    private static void AdvanceSourceTicks(PlayerEntity player, int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            player.AdvanceTickState(default, 1d / LegacyMovementModel.SourceTicksPerSecond);
        }
    }

    private static float InvokeMovementScale(PlayerEntity player)
    {
        var method = typeof(PlayerEntity).GetMethod(
            "GetMovementScale",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (float)method!.Invoke(player, [default(PlayerInputSnapshot)])!;
    }

    private static float InvokeJumpScale(PlayerEntity player)
    {
        var method = typeof(PlayerEntity).GetMethod(
            "GetJumpScale",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (float)method!.Invoke(player, null)!;
    }
}
