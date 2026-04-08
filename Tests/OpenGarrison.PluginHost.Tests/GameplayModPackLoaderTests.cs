using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using Xunit;
using System.IO;
using System.Linq;
using System;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayModPackLoaderTests
{
    [Fact]
    public void StockGameplayPackLoadsFromJsonDirectory()
    {
        var pack = StockGameplayModCatalog.Definition;

        Assert.Equal("stock.gg2", pack.Id);
        Assert.Equal("Stock OpenGarrison Gameplay", pack.DisplayName);
        Assert.True(pack.Items.ContainsKey("weapon.scattergun"));
        Assert.True(pack.Items.ContainsKey("weapon.directhit"));
        Assert.True(pack.Classes.ContainsKey("soldier"));
        Assert.Equal("soldier.stock", pack.Classes["soldier"].DefaultLoadoutId);
        Assert.True(pack.Classes["soldier"].Loadouts.ContainsKey("soldier.direct-hit"));
        Assert.True(pack.Assets.Sprites.ContainsKey("stock.gg2.weapon.directhit.world"));
        Assert.Equal(2, pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths.Count);
        Assert.Equal("assets/directhit/DirectHit.red.png", pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths[0]);
        Assert.Equal("assets/directhit/DirectHit.blue.png", pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths[1]);
        Assert.Equal(2, pack.Assets.Sprites["stock.gg2.weapon.directhit.recoil"].FramePaths.Count);
        Assert.Equal(50, pack.Assets.Sprites["stock.gg2.weapon.directhit.hud"].FrameWidth);
    }

    [Fact]
    public void StockGameplayPackExposesOwnershipReadyExperimentalItems()
    {
        var eyelander = StockGameplayModCatalog.GetExperimentalDemoknightEyelanderItem();
        var paintrain = StockGameplayModCatalog.GetExperimentalDemoknightPaintrainItem();

        Assert.NotNull(eyelander.Ownership);
        Assert.True(eyelander.Ownership!.TrackOwnership);
        Assert.False(eyelander.Ownership.DefaultGranted);
        Assert.True(eyelander.Ownership.GrantOnAcquire);
        Assert.Equal(ExperimentalDemoknightCatalog.EyelanderItemId, eyelander.Id);

        Assert.NotNull(paintrain.Ownership);
        Assert.True(paintrain.Ownership!.TrackOwnership);
        Assert.False(paintrain.Ownership.DefaultGranted);
        Assert.True(paintrain.Ownership.GrantOnAcquire);
        Assert.Equal(ExperimentalDemoknightCatalog.PaintrainItemId, paintrain.Id);
    }

    [Fact]
    public void RuntimeRegistryRegistersDiscoveredNonStockGameplayPacks()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var gameplayRootDirectory = Path.Combine(rootDirectory, "Gameplay");
        var packDirectory = Path.Combine(gameplayRootDirectory, "example.test");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "classes"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "sprites"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "assets"));

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "pack.json"),
                """
                {
                  "id": "example.test",
                  "displayName": "Example Test Pack",
                  "version": "1.0.0"
                }
                """);
            File.WriteAllBytes(
                Path.Combine(packDirectory, "assets", "test-shotgun.png"),
                Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6l8AAAAASUVORK5CYII="));
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.test-shotgun.json"),
                """
                {
                  "id": "weapon.test-shotgun",
                  "displayName": "Test Shotgun",
                  "slot": "Primary",
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": {
                    "maxAmmo": 4,
                    "ammoPerUse": 1,
                    "projectilesPerUse": 2,
                    "useDelaySourceTicks": 10,
                    "reloadSourceTicks": 10
                  },
                  "presentation": {
                    "worldSpriteName": "ShotgunS",
                    "hudSpriteName": "ShotgunAmmoS"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "sprites", "weapon.test-shotgun.world.json"),
                """
                {
                  "id": "example.test.weapon.test-shotgun.world",
                  "framePaths": [
                    "assets/test-shotgun.png"
                  ],
                  "originX": 0,
                  "originY": 0
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "classes", "tester.json"),
                """
                {
                  "id": "tester",
                  "displayName": "Tester",
                  "movement": {
                    "maxHealth": 100,
                    "collisionLeft": -6.0,
                    "collisionTop": -10.0,
                    "collisionRight": 7.0,
                    "collisionBottom": 24.0,
                    "runPower": 1.0,
                    "jumpStrength": 8.3,
                    "maxAirJumps": 0,
                    "tauntLengthFrames": 8
                  },
                  "loadouts": {
                    "tester.stock": {
                      "id": "tester.stock",
                      "displayName": "Stock",
                      "primaryItemId": "weapon.test-shotgun"
                    }
                  },
                  "defaultLoadoutId": "tester.stock"
                }
                """);

            var discoveredPacks = GameplayModPackDirectoryLoader.LoadAllFromDirectory(gameplayRootDirectory)
                .Concat([StockGameplayModCatalog.Definition])
                .ToArray();
            var registry = GameplayRuntimeRegistry.CreateStock(discoveredPacks);

            var modPack = registry.GetRequiredModPack("example.test");
            Assert.Equal("Example Test Pack", modPack.DisplayName);
            Assert.Equal("weapon.test-shotgun", modPack.Classes["tester"].Loadouts["tester.stock"].PrimaryItemId);
            Assert.True(modPack.Assets.Sprites.ContainsKey("example.test.weapon.test-shotgun.world"));
            Assert.Equal("assets/test-shotgun.png", modPack.Assets.Sprites["example.test.weapon.test-shotgun.world"].FramePaths[0]);
            Assert.Equal(2, registry.ModPacks.Count);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void RuntimeRegistryResolvesBoundPlayerClassesForStockPrimaryItemsOnly()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();

        Assert.True(registry.TryResolveBoundPlayerClassForPrimaryItem("weapon.rocketlauncher", out var soldierClass));
        Assert.Equal(PlayerClass.Soldier, soldierClass);
        Assert.False(registry.TryResolveBoundPlayerClassForPrimaryItem(ExperimentalDemoknightCatalog.EyelanderItemId, out _));
        Assert.False(registry.TryResolveBoundPlayerClassForPrimaryItem("weapon.sandvich", out _));
    }

    [Fact]
    public void GameplayLoadoutSelectionResolverOrdersAndResolvesSoldierLoadouts()
    {
        var orderedLoadouts = GameplayLoadoutSelectionResolver.GetOrderedLoadouts(PlayerClass.Soldier);

        Assert.True(orderedLoadouts.Count >= 3);
        Assert.Equal("soldier.black-box", orderedLoadouts[0].Id);
        Assert.Equal("soldier.direct-hit", orderedLoadouts[1].Id);
        Assert.Equal("soldier.stock", orderedLoadouts[2].Id);
        Assert.True(GameplayLoadoutSelectionResolver.TryResolveLoadoutId(PlayerClass.Soldier, "1", out var firstLoadoutId));
        Assert.Equal("soldier.black-box", firstLoadoutId);
        Assert.True(GameplayLoadoutSelectionResolver.TryResolveLoadoutId(PlayerClass.Soldier, "Stock", out var stockLoadoutId));
        Assert.Equal("soldier.stock", stockLoadoutId);
    }

    [Fact]
    public void GameplayLoadoutOwnershipValidationRejectsUnownedTrackedItems()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var trackedLoadout = new GameplayClassLoadoutDefinition(
            "test.experimental",
            "Experimental",
            ExperimentalDemoknightCatalog.EyelanderItemId,
            ExperimentalDemoknightCatalog.PaintrainItemId,
            null);

        Assert.False(GameplayRuntimeRegistry.LoadoutItemsAreOwned(trackedLoadout, static _ => false));
        Assert.False(GameplayRuntimeRegistry.LoadoutItemsAreOwned(trackedLoadout, itemId =>
            string.Equals(itemId, ExperimentalDemoknightCatalog.EyelanderItemId, StringComparison.Ordinal)));
        Assert.True(GameplayRuntimeRegistry.LoadoutItemsAreOwned(trackedLoadout, itemId =>
            string.Equals(itemId, ExperimentalDemoknightCatalog.EyelanderItemId, StringComparison.Ordinal)
            || string.Equals(itemId, ExperimentalDemoknightCatalog.PaintrainItemId, StringComparison.Ordinal)));
    }

    [Fact]
    public void RuntimeRegistryResolvesEffectiveWeaponStatsFromSharedAuthoritativeModel()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();

        var stockRocketLauncher = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.rocketlauncher"));
        var blackBox = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.blackbox"));
        var stockMinigun = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.minigun"));
        var brassBeast = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.brassbeast"));
        var stockRevolver = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.revolver"));
        var diamondback = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.diamondback"));
        var stockFlamethrower = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.flamethrower"));

        Assert.NotNull(stockRocketLauncher.RocketCombat);
        Assert.Equal(RocketProjectileEntity.DirectHitDamage, stockRocketLauncher.RocketCombat!.DirectHitDamage);
        Assert.Equal(RocketProjectileEntity.ExplosionDamage, stockRocketLauncher.RocketCombat.ExplosionDamage);
        Assert.Equal(15f, blackBox.DirectHitHealAmount);

        Assert.Equal(ShotProjectileEntity.DamagePerHit, stockMinigun.DirectHitDamage);
        Assert.Equal(10f, brassBeast.DirectHitDamage);

        Assert.Equal(RevolverProjectileEntity.DamagePerHit, stockRevolver.DirectHitDamage);
        Assert.Equal(24f, diamondback.DirectHitDamage);
        Assert.Equal(21f, diamondback.MinShotSpeed);

        Assert.Equal(FlameProjectileEntity.DirectHitDamage, stockFlamethrower.DirectHitDamage);
        Assert.Equal(FlameProjectileEntity.BurnDamagePerTick, stockFlamethrower.DamagePerTick);
    }

    [Fact]
    public void ControlCommandAndSnapshotRoundTripGameplayIds()
    {
        var command = new ControlCommandMessage(12u, ControlCommandKind.SelectGameplayLoadout, 0, "soldier.direct-hit");
        Assert.True(ProtocolCodec.TryDeserialize(ProtocolCodec.Serialize(command), out var deserializedCommand));
        var roundTrippedCommand = Assert.IsType<ControlCommandMessage>(deserializedCommand);
        Assert.Equal("soldier.direct-hit", roundTrippedCommand.TextValue);

        var snapshot = new SnapshotMessage(
            5ul,
            60,
            "ctf_test",
            1,
            1,
            (byte)GameModeKind.CaptureTheFlag,
            (byte)MatchPhase.Running,
            0,
            0,
            0,
            0,
            0,
            0u,
            new SnapshotIntelState(0, 0f, 0f, true, false, 0),
            new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            [
                new SnapshotPlayerState(
                    Slot: 1,
                    PlayerId: 1,
                    Name: "Player",
                    Team: (byte)PlayerTeam.Red,
                    ClassId: (byte)PlayerClass.Soldier,
                    IsAlive: true,
                    IsAwaitingJoin: false,
                    IsSpectator: false,
                    RespawnTicks: 0,
                    X: 0f,
                    Y: 0f,
                    HorizontalSpeed: 0f,
                    VerticalSpeed: 0f,
                    Health: 200,
                    MaxHealth: 200,
                    Ammo: 4,
                    MaxAmmo: 4,
                    Kills: 0,
                    Deaths: 0,
                    Caps: 0,
                    Points: 0f,
                    HealPoints: 0,
                    ActiveDominationCount: 0,
                    IsDominatingLocalViewer: false,
                    IsDominatedByLocalViewer: false,
                    Metal: 0f,
                    IsGrounded: true,
                    RemainingAirJumps: 0,
                    IsCarryingIntel: false,
                    IntelRechargeTicks: 0f,
                    IsSpyCloaked: false,
                    SpyCloakAlpha: 1f,
                    IsUbered: false,
                    IsHeavyEating: false,
                    HeavyEatTicksRemaining: 0,
                    IsSniperScoped: false,
                    SniperChargeTicks: 0,
                    FacingDirectionX: 1f,
                    AimDirectionDegrees: 0f,
                    IsTaunting: false,
                    TauntFrameIndex: 0f,
                    IsChatBubbleVisible: false,
                    ChatBubbleFrameIndex: 0,
                    ChatBubbleAlpha: 0f,
                    GameplayModPackId: "stock.gg2",
                    GameplayLoadoutId: "soldier.direct-hit",
                    GameplayPrimaryItemId: "weapon.directhit",
                    GameplaySecondaryItemId: "",
                    GameplayUtilityItemId: "",
                    GameplayEquippedSlot: (byte)GameplayEquipmentSlot.Primary,
                    GameplayEquippedItemId: "weapon.directhit",
                    GameplayAcquiredItemId: "",
                    OwnedGameplayItemIds:
                    [
                        ExperimentalDemoknightCatalog.EyelanderItemId,
                        ExperimentalDemoknightCatalog.PaintrainItemId,
                    ]),
            ],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            0,
            0,
            0,
            0,
            [],
            [],
            null,
            [],
            [],
            [],
            []);

        Assert.True(ProtocolCodec.TryDeserialize(ProtocolCodec.Serialize(snapshot), out var deserializedSnapshot));
        var roundTrippedSnapshot = Assert.IsType<SnapshotMessage>(deserializedSnapshot);
        Assert.Equal("soldier.direct-hit", Assert.Single(roundTrippedSnapshot.Players).GameplayLoadoutId);
        Assert.Equal(2, Assert.Single(roundTrippedSnapshot.Players).OwnedGameplayItemIds!.Count);
    }
}
