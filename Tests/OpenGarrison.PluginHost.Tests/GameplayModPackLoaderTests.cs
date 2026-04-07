using OpenGarrison.Core;
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

        Assert.True(orderedLoadouts.Count >= 2);
        Assert.Equal("soldier.direct-hit", orderedLoadouts[0].Id);
        Assert.Equal("soldier.stock", orderedLoadouts[1].Id);
        Assert.True(GameplayLoadoutSelectionResolver.TryResolveLoadoutId(PlayerClass.Soldier, "1", out var firstLoadoutId));
        Assert.Equal("soldier.direct-hit", firstLoadoutId);
        Assert.True(GameplayLoadoutSelectionResolver.TryResolveLoadoutId(PlayerClass.Soldier, "Stock", out var stockLoadoutId));
        Assert.Equal("soldier.stock", stockLoadoutId);
    }
}
