using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Server;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class AlternatePrimaryWeaponBalanceTests
{
    [Fact]
    public void PyroStockLoadoutIncludesConfiguredDragonRage()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var loadout = registry.GetRequiredLoadout("pyro", "pyro.stock");
        var item = registry.GetRequiredItem("weapon.dragon-rage");
        var weapon = registry.CreatePrimaryWeaponDefinition(item);

        Assert.Contains("weapon.dragon-rage", loadout.Primary!.ItemIds);
        Assert.Equal("weapon.flamethrower", loadout.Primary.DefaultItemId);
        Assert.Equal(BuiltInGameplayBehaviorIds.DragonRage, item.BehaviorId);
        Assert.Equal(PrimaryWeaponKind.Custom, weapon.Kind);
        Assert.Equal(6, weapon.MaxAmmo);
        Assert.Equal(1, weapon.AmmoPerShot);
        Assert.Equal(16, weapon.ReloadDelayTicks);
        Assert.Equal(18, weapon.AmmoReloadTicks);
        Assert.Equal(22f, weapon.MinShotSpeed);
        Assert.Equal(35f, weapon.DirectHitDamage);
        Assert.Equal("DragonRageS", item.Presentation.WorldSpriteName);
        Assert.Null(item.Presentation.RecoilSpriteName);
        Assert.Equal("DragonRageFRS", item.Presentation.ReloadSpriteName);
        Assert.Equal(16.8f, FlareProjectileEntity.DragonRageVisualWidth);
        Assert.Equal(12.8f, FlareProjectileEntity.DragonRageCoreVisualWidth);
    }

    [Fact]
    public void DragonRageFiresOneFastShortLivedIncendiarySlug()
    {
        var world = CreateJoinedWorld(PlayerClass.Pyro);
        var pyro = world.LocalPlayer;
        Assert.True(pyro.TrySelectGameplayPrimaryItem("weapon.dragon-rage"));
        var input = default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = pyro.X + 300f,
            AimWorldY = pyro.Y,
        };

        world.SetLocalInput(input);
        world.AdvanceOneTick();

        var slug = Assert.Single(world.Flares);
        Assert.True(slug.IsDragonRageSlug);
        Assert.Equal(35f, slug.DamagePerHit);
        Assert.Equal("DragonRageKL", slug.KillFeedWeaponSpriteName);
        Assert.Equal(FlareProjectileEntity.DragonRageLifetimeTicks, slug.TicksRemaining);
        Assert.InRange(
            MathF.Sqrt((slug.VelocityX * slug.VelocityX) + (slug.VelocityY * slug.VelocityY)),
            21.99f,
            22.01f);
        Assert.Equal(5, pyro.CurrentShells);
        Assert.False(pyro.HasPyroWeaponEquipped);

        var networkState = Assert.Single(new Protocol64StatePublisher(world).BuildProjectileStates(1));
        Assert.Equal((byte)FlareProjectileStyle.DragonRageSlug, networkState.FlareStyle);
        var receiver = CreateJoinedWorld(PlayerClass.Pyro);
        Assert.True(receiver.ApplyProtocol64ProjectileState(
            networkState,
            SimulationWorld.LocalPlayerSlot));
        var replicatedSlug = Assert.Single(receiver.Flares);
        Assert.True(replicatedSlug.IsDragonRageSlug);
        Assert.Equal(35f, replicatedSlug.DamagePerHit);
    }

    [Fact]
    public void DragonRageDirectHitDealsThirtyFiveAndIgnites()
    {
        var world = CreateJoinedWorld(PlayerClass.Pyro);
        var owner = world.LocalPlayer;
        world.SpawnPracticeCombatDummy();
        var target = world.EnemyPlayer;
        owner.TeleportTo(200f, 500f);
        target.TeleportTo(300f, 500f);
        target.ForceSetHealth(target.MaxHealth);
        var healthBefore = target.Health;
        world.CombatTestSpawnFlare(
            owner,
            target.Left - 8f,
            target.Y,
            velocityX: 16f,
            damagePerHit: 35f,
            style: FlareProjectileStyle.DragonRageSlug,
            lifetimeTicks: FlareProjectileEntity.DragonRageLifetimeTicks);

        world.AdvanceOneTick();

        Assert.Equal(healthBefore - 35, target.Health);
        Assert.True(target.IsBurning);
        Assert.Single(world.Flares);
        world.AdvanceOneTick();
        Assert.Equal(healthBefore - 35, target.Health);
    }

    [Fact]
    public void DragonRagePiercesTwoEnemiesInOneTickWithoutHittingEitherTwice()
    {
        var world = CreateJoinedWorld(PlayerClass.Pyro);
        var owner = world.LocalPlayer;
        owner.TeleportTo(200f, 500f);
        var first = AddEnemy(world, 2, 300f, 500f);
        var second = AddEnemy(world, 3, 320f, 500f);
        var firstHealth = first.Health;
        var secondHealth = second.Health;
        world.CombatTestSpawnFlare(owner, 270f, 500f, velocityX: 80f,
            damagePerHit: 35f, style: FlareProjectileStyle.DragonRageSlug);
        world.AdvanceOneTick();
        Assert.Equal(firstHealth - 35, first.Health);
        Assert.Equal(secondHealth - 35, second.Health);
        Assert.Single(world.Flares);
        world.AdvanceOneTick();
        Assert.Equal(firstHealth - 35, first.Health);
        Assert.Equal(secondHealth - 35, second.Health);
    }

    [Fact]
    public void PiercingSlugStillStopsAtFloor()
    {
        var world = CreateJoinedWorld(PlayerClass.Pyro);
        world.CombatTestSpawnFlare(world.LocalPlayer, 700f, 1010f, velocityY: 30f,
            style: FlareProjectileStyle.DragonRageSlug);
        world.AdvanceOneTick();
        Assert.Empty(world.Flares);
    }

    [Fact]
    public void SuccessfulHitDoublesDragonRageFireRateAndExpiryCannotUndoIt()
    {
        var world = CreateJoinedWorld(PlayerClass.Pyro);
        var pyro = world.LocalPlayer;
        Assert.True(pyro.TrySelectGameplayPrimaryItem("weapon.dragon-rage"));
        world.SetLocalInput(default(PlayerInputSnapshot) with
        {
            FirePrimary = true, AimWorldX = pyro.X + 300f, AimWorldY = pyro.Y,
        });
        world.AdvanceOneTick();
        var shot = pyro.DragonRageCurrentShotSequence;
        Assert.Equal(23, pyro.PrimaryCooldownTicks);
        pyro.ResolveDragonRageShot(shot, true, 0);
        Assert.Equal(11, pyro.PrimaryCooldownTicks);
        pyro.ResolveDragonRageShot(shot, false, FlareProjectileEntity.DragonRageLifetimeTicks);
        Assert.True(pyro.IsDragonRageRapidFireActive);
        Assert.Equal(11, pyro.PrimaryCooldownTicks);
    }

    [Fact]
    public void DragonRageSlugFadesAggressivelyAndReflectionPreservesItsRange()
    {
        var slug = new FlareProjectileEntity(
            2,
            PlayerTeam.Red,
            1,
            0f,
            0f,
            22f,
            0f,
            ticksRemaining: FlareProjectileEntity.DragonRageLifetimeTicks,
            damagePerHit: 35f,
            style: FlareProjectileStyle.DragonRageSlug);

        for (var tick = 0; tick < FlareProjectileEntity.DragonRageLifetimeTicks / 2; tick += 1)
        {
            slug.AdvanceOneTick();
        }

        Assert.Equal(1f, slug.PresentationAlpha);
        slug.AdvanceOneTick();
        Assert.InRange(slug.PresentationAlpha, 0.56f, 0.57f);
        for (var tick = 0; tick < 3; tick += 1)
        {
            slug.AdvanceOneTick();
        }
        Assert.True(slug.IsExpired);
        Assert.Equal(176f, slug.X);

        slug.Reflect(3, PlayerTeam.Blue, MathF.PI);
        Assert.Equal(FlareProjectileEntity.DragonRageLifetimeTicks, slug.TicksRemaining);
        Assert.True(slug.IsDragonRageSlug);
    }

    [Fact]
    public void DragonRageKeepsAirburstWithoutSpendingOrDelayingShotgunShells()
    {
        var world = CreateJoinedWorld(PlayerClass.Pyro);
        var pyro = world.LocalPlayer;
        Assert.True(pyro.TrySelectGameplayPrimaryItem("weapon.dragon-rage"));
        var initialShells = pyro.CurrentShells;

        Assert.False(pyro.CanFirePyroAirblast(PlayerEntity.PyroAirburstCost));
        world.SetLocalInput(default(PlayerInputSnapshot) with
        {
            UseAbility = true,
            AimWorldX = pyro.X + 96f,
            AimWorldY = pyro.Y,
        });
        world.AdvanceOneTick();

        Assert.Equal(initialShells, pyro.CurrentShells);
        Assert.Equal(0, pyro.PrimaryCooldownTicks);
        Assert.Equal(0, pyro.ReloadTicksUntilNextShell);
        Assert.True(pyro.PyroAirblastCooldownTicks > 0);
    }

    [Fact]
    public void SmgAndTommyGunUseSharedReloadAnimationSheet()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();

        Assert.Equal(
            "SmgFRS",
            registry.GetRequiredItem("weapon.sniper-smg").Presentation.ReloadSpriteName);
        Assert.Equal(
            "SmgFRS",
            registry.GetRequiredItem("weapon.tommy-gun").Presentation.ReloadSpriteName);
    }

    [Fact]
    public void SoldierStockLoadoutIncludesConfiguredMortarLauncher()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var loadout = registry.GetRequiredLoadout("soldier", "soldier.stock");
        var mortar = registry.CreatePrimaryWeaponDefinition(
            registry.GetRequiredItem("weapon.mortar-launcher"));

        Assert.Contains("weapon.mortar-launcher", loadout.Primary!.ItemIds);
        Assert.Equal(BuiltInGameplayBehaviorIds.MortarLauncher,
            registry.GetRequiredItem("weapon.mortar-launcher").BehaviorId);
        Assert.Equal(PrimaryWeaponKind.RocketLauncher, mortar.Kind);
        Assert.Equal(1, mortar.MaxAmmo);
        Assert.Equal(28, mortar.AmmoReloadTicks);
        Assert.Equal(1.3f, mortar.PlayerKnockbackScale);
        Assert.NotNull(mortar.RocketCombat);
        Assert.Equal(33, mortar.RocketCombat!.DirectHitDamage);
        Assert.Equal(45.5f, mortar.RocketCombat.BlastRadius);
        Assert.Equal(45f, mortar.RocketCombat.MinimumSplashDamage);
        Assert.Equal(1.2f, mortar.RocketCombat.SelfDamageMultiplier);
    }

    [Fact]
    public void MortarChargeAndProjectileUseHeavierBallisticArc()
    {
        var soldier = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Soldier");
        Assert.True(soldier.TrySelectGameplayPrimaryItem("weapon.mortar-launcher"));
        soldier.ForceSetHealth(soldier.MaxHealth);
        Assert.True(soldier.TryStartMortarLauncherCharge(-45f));
        for (var tick = 1; tick < PlayerEntity.MortarLauncherMaxChargeTicks; tick += 1)
        {
            soldier.IncrementMortarLauncherCharge(-45f);
        }

        Assert.True(soldier.TryReleaseMortarLauncherCharge(out var chargeFraction, out var directionRadians));
        Assert.Equal(1f, chargeFraction);
        Assert.Equal(-MathF.PI / 4f, directionRadians, precision: 4);

        var rocket = new RocketProjectileEntity(
            2,
            PlayerTeam.Red,
            soldier.Id,
            0f,
            0f,
            18f,
            directionRadians,
            isBallistic: true,
            ballisticGravityPerTick: PlayerEntity.MortarLauncherGravityPerTick,
            suppressSmokeTrail: true);
        var initialDirection = rocket.DirectionRadians;
        rocket.AdvanceOneTick(1f / 30f);

        Assert.True(rocket.IsBallistic);
        Assert.True(rocket.SuppressSmokeTrail);
        Assert.True(rocket.BallisticGravityPerTick > ArrowProjectileEntity.GravityPerTick);
        Assert.True(rocket.DirectionRadians > initialDirection);
    }

    [Fact]
    public void MortarAuthorityWaitsForReleaseAndSpawnsOneSmokeFreeBallisticRocket()
    {
        var world = new SimulationWorld();
        var soldier = world.LocalPlayer;
        soldier.SetClassDefinition(CharacterClassCatalog.Soldier);
        Assert.True(soldier.TrySelectGameplayPrimaryItem("weapon.mortar-launcher"));
        var heldInput = default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = soldier.X + 300f,
            AimWorldY = soldier.Y - 150f,
        };

        for (var tick = 0; tick < 12; tick += 1)
        {
            world.SetLocalInput(heldInput);
            world.AdvanceOneTick();
        }

        Assert.Empty(world.Rockets);
        Assert.Equal(12, soldier.MortarLauncherChargeTicks);

        world.SetLocalInput(heldInput with { FirePrimary = false });
        world.AdvanceOneTick();

        var rocket = Assert.Single(world.Rockets);
        Assert.True(rocket.IsBallistic);
        Assert.True(rocket.SuppressSmokeTrail);
        Assert.Equal(PlayerEntity.MortarLauncherGravityPerTick, rocket.BallisticGravityPerTick);
        Assert.Equal(0, soldier.CurrentShells);
        Assert.Equal(0, soldier.MortarLauncherChargeTicks);

        var networkState = Assert.Single(new Protocol64StatePublisher(world).BuildProjectileStates(1));
        Assert.True(networkState.IsBallisticRocket);
        Assert.Equal(PlayerEntity.MortarLauncherGravityPerTick, networkState.BallisticRocketGravityPerTick);
        Assert.True(networkState.SuppressRocketSmokeTrail);
    }

    [Fact]
    public void HeavyStockLoadoutIncludesMobileMagazineFedTommyGun()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var loadout = registry.GetRequiredLoadout("heavy", "heavy.stock");
        var tommyItem = registry.GetRequiredItem("weapon.tommy-gun");
        var tommy = registry.CreatePrimaryWeaponDefinition(
            tommyItem);

        Assert.Contains("weapon.tommy-gun", loadout.Primary!.ItemIds);
        Assert.Equal(BuiltInGameplayBehaviorIds.TommyGun,
            registry.GetRequiredItem("weapon.tommy-gun").BehaviorId);
        Assert.Equal(PrimaryWeaponKind.PelletGun, tommy.Kind);
        Assert.Equal(40, tommy.MaxAmmo);
        Assert.Equal(9f, tommy.DirectHitDamage);
        Assert.Equal(54, tommy.AmmoReloadTicks);
        Assert.True(tommy.RefillsAllAtOnce);
        Assert.Equal(0, tommy.AmmoRegenPerTick);
        Assert.Null(tommy.PlayerSlowMovementMultiplier);
        Assert.Equal(-5f, tommyItem.Presentation.WeaponOffsetX);
        Assert.Equal(-5f, tommyItem.Presentation.WeaponOffsetY);
        Assert.Equal(8f, tommyItem.Presentation.ReloadSpriteOffsetX);
    }

    [Fact]
    public void DescendingMortarDirectHitPullsOnlyDirectVictimTowardSoldier()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Soldier));
        var owner = world.LocalPlayer;
        owner.TeleportTo(0f, 0f);
        var directVictim = AddEnemy(world, id: 2, x: 100f, y: 0f);
        var splashVictim = AddEnemy(world, id: 3, x: 130f, y: 0f);
        var registry = GameplayRuntimeRegistry.CreateStock();
        var mortar = registry.CreatePrimaryWeaponDefinition(
            registry.GetRequiredItem("weapon.mortar-launcher"));
        var rocket = new RocketProjectileEntity(
            id: 999,
            team: owner.Team,
            ownerId: owner.Id,
            x: directVictim.X,
            y: directVictim.Y - 8f,
            speed: 12f,
            directionRadians: MathF.PI / 2f,
            rocketCombat: mortar.RocketCombat,
            knockbackScale: mortar.PlayerKnockbackScale,
            isBallistic: true,
            ballisticGravityPerTick: PlayerEntity.MortarLauncherGravityPerTick,
            suppressSmokeTrail: true);

        InvokeDirectHitExplosion(world, rocket, directVictim);

        Assert.True(directVictim.HorizontalSpeed < 0f,
            $"expected direct victim to move toward Soldier, got {directVictim.HorizontalSpeed}");
        Assert.True(splashVictim.HorizontalSpeed > 0f,
            $"expected splash victim to keep radial knockback, got {splashVictim.HorizontalSpeed}");
    }

    [Fact]
    public void RifleUsesReducedUnscopedDamageAndCappedSuccessiveHitBonuses()
    {
        var sniper = new PlayerEntity(1, CharacterClassCatalog.Sniper, "Sniper");
        Assert.Equal(25, sniper.GetSniperRifleDamageForCharge(0, isScoped: false));
        Assert.Equal(PlayerEntity.SniperChargeMaxTicks, sniper.SniperRifleFullChargeTicks);

        for (var hit = 0; hit < 10; hit += 1)
        {
            sniper.ResolveSniperRifleStreakShot(isFullyCharged: true, hitEnemyPlayer: true);
        }

        Assert.Equal(PlayerEntity.SniperRifleStreakMaximum, sniper.SniperRifleFullyChargedHitStreak);
        Assert.Equal(43, sniper.SniperRifleFullChargeTicks);
        Assert.Equal(2.5f, sniper.GetSniperRifleStreakDamageMultiplier(isFullyCharged: true));

        sniper.ResolveSniperRifleStreakShot(isFullyCharged: true, hitEnemyPlayer: false);
        Assert.Equal(0, sniper.SniperRifleFullyChargedHitStreak);
        Assert.Equal(PlayerEntity.SniperChargeMaxTicks, sniper.SniperRifleFullChargeTicks);
    }

    private static PlayerEntity AddEnemy(SimulationWorld world, int id, float x, float y)
    {
        var networkId = checked((byte)id);
        Assert.True(world.TryPrepareNetworkPlayerJoin(networkId));
        Assert.True(world.TrySetNetworkPlayerTeam(networkId, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(networkId, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(networkId, out var enemy));
        enemy.TeleportTo(x, y);
        return enemy;
    }

    private static SimulationWorld CreateJoinedWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld();
        var redSpawn = new SpawnPoint(300f, 500f);
        var blueSpawn = new SpawnPoint(1700f, 500f);
        world.CombatTestSetLevel(new SimpleLevel(
            "alternate-primary-balance",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(2048f, 2048f),
            1f,
            null,
            1,
            1,
            redSpawn,
            [redSpawn],
            [blueSpawn],
            [],
            [],
            floorY: 1024f,
            [new LevelSolid(0f, 1024f, 2048f, 1024f)],
            importedFromSource: false));
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(playerClass);
        world.LocalPlayer.SetSpawnRoomState(false);
        world.SetLocalInput(default);
        world.SetLocalPreviousInput(default);
        _ = world.DrainPendingSoundEvents();
        return world;
    }

    private static void InvokeDirectHitExplosion(
        SimulationWorld world,
        RocketProjectileEntity rocket,
        PlayerEntity directHitPlayer)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ExplodeRocket",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(RocketProjectileEntity),
                typeof(PlayerEntity),
                typeof(SentryEntity),
                typeof(GeneratorState),
                typeof(int),
            ],
            modifiers: null);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [rocket, directHitPlayer, null, null, -1]);
    }
}
