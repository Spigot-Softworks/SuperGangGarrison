using OpenGarrison.Core;
using OpenGarrison.ClientShared;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using Xunit;
using System.IO;
using System.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayModPackLoaderTests
{
    [Fact]
    public void ClientSettingsRoundTripThroughIniDocument()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-client-settings-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        var settingsPath = Path.Combine(rootDirectory, ClientSettings.DefaultFileName);

        try
        {
            var settings = new ClientSettings
            {
                PlayerName = "BrowserTester",
                Rewards = "rocketjumper,marketgardener",
                Fullscreen = true,
                VSync = true,
                DisableLegacyGameplaySpriteFallback = true,
                RecentConnection = new ClientRecentConnectionSettings
                {
                    Host = "example.invalid",
                    Port = 9001,
                },
                LobbyHost = "lobby.example.invalid",
                LobbyPort = 5001,
            };

            settings.Save(settingsPath);

            var loaded = ClientSettings.Load(settingsPath);

            Assert.Equal("BrowserTester", loaded.PlayerName);
            Assert.Equal("rocketjumper,marketgardener", loaded.Rewards);
            Assert.True(loaded.Fullscreen);
            Assert.True(loaded.VSync);
            Assert.True(loaded.DisableLegacyGameplaySpriteFallback);
            Assert.Equal("example.invalid", loaded.RecentConnection.Host);
            Assert.Equal(9001, loaded.RecentConnection.Port);
            Assert.Equal("lobby.example.invalid", loaded.LobbyHost);
            Assert.Equal(5001, loaded.LobbyPort);
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
        var soldierPresentation = pack.Classes["soldier"].Presentation;
        Assert.NotNull(soldierPresentation);
        Assert.Equal("Soldier", soldierPresentation.SpritePrefix);
        Assert.Equal("StandS", soldierPresentation.StandSuffix);
        Assert.True(pack.Assets.Sprites.ContainsKey("ScoutRedStandS"));
        var scoutStandSprite = pack.Assets.Sprites["ScoutRedStandS"];
        Assert.Equal("assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png", scoutStandSprite.FramePaths[0]);
        Assert.Equal(30, scoutStandSprite.OriginX);
        Assert.Equal(40, scoutStandSprite.OriginY);
        Assert.NotNull(scoutStandSprite.Mask);
        Assert.Equal("RECTANGLE", scoutStandSprite.Mask!.Shape);
        Assert.Equal("MANUAL", scoutStandSprite.Mask.BoundsMode);
        Assert.Equal(24, scoutStandSprite.Mask.Left);
        Assert.Equal(63, scoutStandSprite.Mask.Bottom);
        Assert.True(pack.Assets.Sprites.ContainsKey("gg2FontS"));
        var fontSprite = pack.Assets.Sprites["gg2FontS"];
        Assert.Equal("assets/legacy-sprites/gg2FontS/image 0.png", fontSprite.FramePaths[0]);
        Assert.Equal("assets/legacy-sprites/gg2FontS/image 10.png", fontSprite.FramePaths[10]);
        Assert.NotNull(fontSprite.Mask);
        Assert.Equal("MANUAL", fontSprite.Mask!.BoundsMode);
        Assert.True(pack.Assets.Sprites.ContainsKey("IntelTimerS"));
        var intelTimerSprite = pack.Assets.Sprites["IntelTimerS"];
        Assert.Equal(24, intelTimerSprite.FramePaths.Count);
        Assert.Equal("assets/legacy-sprites/InGameElements/IntelTimerS/image 23.png", intelTimerSprite.FramePaths[23]);
        Assert.Equal(5, intelTimerSprite.OriginX);
        Assert.True(pack.Assets.Sprites.ContainsKey("RocketlauncherFRS"));
        var reloadSprite = pack.Assets.Sprites["RocketlauncherFRS"];
        Assert.Equal(24, reloadSprite.FramePaths.Count);
        Assert.Equal("assets/legacy-sprites/Weapons/Reloading/RocketlauncherFRS/image 23.png", reloadSprite.FramePaths[23]);
        Assert.NotNull(reloadSprite.Mask);
        Assert.Equal("PRECISE", reloadSprite.Mask!.Shape);
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
                  "originY": 0,
                  "mask": {
                    "separate": true,
                    "shape": "Rectangle",
                    "boundsMode": "Manual",
                    "left": 1,
                    "top": 2,
                    "right": 3,
                    "bottom": 4
                  }
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
                  "presentation": {
                    "spritePrefix": "Tester",
                    "baseSuffix": "S",
                    "standSuffix": "StandS",
                    "runSuffix": "RunS",
                    "jumpSuffix": "JumpS"
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
            var presentation = modPack.Classes["tester"].Presentation;
            Assert.NotNull(presentation);
            Assert.Equal("Tester", presentation.SpritePrefix);
            Assert.Equal("StandS", presentation.StandSuffix);
            Assert.True(modPack.Assets.Sprites.ContainsKey("example.test.weapon.test-shotgun.world"));
            Assert.Equal("assets/test-shotgun.png", modPack.Assets.Sprites["example.test.weapon.test-shotgun.world"].FramePaths[0]);
            var mask = modPack.Assets.Sprites["example.test.weapon.test-shotgun.world"].Mask;
            Assert.NotNull(mask);
            Assert.True(mask.Separate);
            Assert.Equal("Rectangle", mask.Shape);
            Assert.Equal("Manual", mask.BoundsMode);
            Assert.Equal(1, mask.Left);
            Assert.Equal(4, mask.Bottom);
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
    public void GameplaySpriteMigrationImportsOriginAndMaskFromLegacyMetadata()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());

        try
        {
            var metadataPath = Path.Combine(rootDirectory, "PistolS.xml");
            var imagesDirectory = Path.Combine(rootDirectory, "PistolS.images");
            Directory.CreateDirectory(imagesDirectory);
            File.WriteAllText(
                metadataPath,
                """
                <?xml version="1.0" encoding="UTF-8" standalone="no"?>
                <sprite>
                  <origin x="8" y="7"/>
                  <mask>
                    <separate>false</separate>
                    <shape>PRECISE</shape>
                    <bounds alphaTolerance="0" mode="AUTO"/>
                  </mask>
                  <preload>true</preload>
                  <transparent>false</transparent>
                </sprite>
                """);
            File.WriteAllBytes(
                Path.Combine(imagesDirectory, "image0.png"),
                Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6l8AAAAASUVORK5CYII="));

            var imported = GameplaySpriteAssetMigration.ImportDefinitionFromGameMakerMetadata(
                "example.test.weapon.pistol.world",
                metadataPath);

            Assert.Equal("example.test.weapon.pistol.world", imported.Id);
            Assert.Single(imported.FramePaths);
            Assert.Equal(8, imported.OriginX);
            Assert.Equal(7, imported.OriginY);
            var mask = imported.Mask;
            Assert.NotNull(mask);
            Assert.False(mask.Separate);
            Assert.Equal("PRECISE", mask.Shape);
            Assert.Equal("AUTO", mask.BoundsMode);
            Assert.Null(mask.Left);
            Assert.Null(mask.Bottom);
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
    public void GameplayPackLoaderAllowsDeferredSpriteFrameResolution()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var gameplayRootDirectory = Path.Combine(rootDirectory, "Gameplay");
        var packDirectory = Path.Combine(gameplayRootDirectory, "example.missing-frame");
        Directory.CreateDirectory(Path.Combine(packDirectory, "sprites"));

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "pack.json"),
                """
                {
                  "id": "example.missing-frame",
                  "displayName": "Missing Frame Pack",
                  "version": "1.0.0"
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "sprites", "missing.json"),
                """
                {
                  "id": "example.missing-frame.sprite",
                  "framePaths": [
                    "assets/does-not-exist.png"
                  ],
                  "originX": 3,
                  "originY": 4
                }
                """);

            var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);

            Assert.True(pack.Assets.Sprites.ContainsKey("example.missing-frame.sprite"));
            Assert.Equal("assets/does-not-exist.png", pack.Assets.Sprites["example.missing-frame.sprite"].FramePaths[0]);
            Assert.Equal(3, pack.Assets.Sprites["example.missing-frame.sprite"].OriginX);
            Assert.Equal(4, pack.Assets.Sprites["example.missing-frame.sprite"].OriginY);
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
    public void GameplayPackAssetPathUtilityBuildsStableContentPaths()
    {
        ContentRoot.Initialize("Content");

        Assert.Equal("Content/Gameplay/stock.gg2", GameplayPackAssetPathUtility.GetPackContentRoot("stock.gg2"));
        Assert.Equal("Content/Gameplay/stock.gg2/sprites/ScoutRedStandS.json", GameplayPackAssetPathUtility.GetSpriteDefinitionPath("stock.gg2", "ScoutRedStandS"));
        Assert.Equal("Content/Gameplay/stock.gg2/assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png", GameplayPackAssetPathUtility.BuildPackAssetPath("stock.gg2", "assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png"));
    }

    [Fact]
    public void ClientRuntimeBootstrapInitializesContentRoot()
    {
        ClientRuntimeBootstrap.InitializeContentRoot("Content");

        Assert.Equal("Content", ContentRoot.Path);
    }

    [Fact]
    public async Task GameplaySpriteDefinitionHttpReaderBuildsStableDefinitionAndFramePaths()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "ScoutRedStandS",
                      "framePaths": [
                        "assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png"
                      ],
                      "originX": 30,
                      "originY": 40
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            }))
        {
            BaseAddress = new Uri("https://example.invalid/"),
        };
        var reader = new GameplaySpriteDefinitionHttpReader(httpClient);

        var loaded = await reader.TryLoadAsync("stock.gg2", "ScoutRedStandS");

        Assert.NotNull(loaded);
        Assert.Equal("stock.gg2", loaded!.PackId);
        Assert.Equal("Content/Gameplay/stock.gg2/sprites/ScoutRedStandS.json", loaded.DefinitionPath);
        Assert.Equal("ScoutRedStandS", loaded.Definition.Id);
        Assert.Equal("Content/Gameplay/stock.gg2/assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png", loaded.FirstFrameContentPath);
    }

    [Fact]
    public void GameplaySpriteBinaryLoaderLoadsSourceImagesFromAssetSource()
    {
        var spriteDefinition = new GameplaySpriteAssetDefinition(
            "ScoutRedStandS",
            ["assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png"],
            OriginX: 30,
            OriginY: 40);
        var assetSource = new StubAssetBinarySource(new Dictionary<string, byte[]>
        {
            ["assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png"] = [1, 2, 3, 4],
        });

        var loaded = GameplaySpriteBinaryLoader.LoadSourceImages(assetSource, spriteDefinition);

        Assert.Equal("ScoutRedStandS", loaded.Definition.Id);
        Assert.Single(loaded.SourceImages);
        Assert.Equal("assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png", loaded.SourceImages[0].FramePath);
        Assert.Equal([1, 2, 3, 4], loaded.SourceImages[0].Bytes);
    }

    [Fact]
    public void GameplayPackSpriteAssetServiceLoadsRegisteredSprite()
    {
        var spriteDefinition = new GameplaySpriteAssetDefinition(
            "ScoutRedStandS",
            ["assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png"],
            OriginX: 30,
            OriginY: 40);
        var assetSource = new StubAssetBinarySource(new Dictionary<string, byte[]>
        {
            ["assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png"] = [5, 6, 7, 8],
        });
        var spriteAssetService = new GameplayPackSpriteAssetService("stock.gg2", assetSource);

        var loaded = spriteAssetService.LoadRegisteredSprite(spriteDefinition);

        Assert.Equal("stock.gg2", loaded.Definition.PackId);
        Assert.Equal("Content/Gameplay/stock.gg2/sprites/ScoutRedStandS.json", loaded.Definition.DefinitionPath);
        Assert.Equal("Content/Gameplay/stock.gg2/assets/legacy-sprites/Characters/Scout/ScoutRedStandS/image 0.png", loaded.Definition.FirstFrameContentPath);
        Assert.Single(loaded.SourceSet.SourceImages);
        Assert.Equal([5, 6, 7, 8], loaded.SourceSet.SourceImages[0].Bytes);
    }

    [Fact]
    public void GameplayPackSpriteAssetServiceRegistryResolvesRegisteredPackServices()
    {
        var stockService = new GameplayPackSpriteAssetService("stock.gg2", new StubAssetBinarySource(new Dictionary<string, byte[]>()));
        var customService = new GameplayPackSpriteAssetService("example.test", new StubAssetBinarySource(new Dictionary<string, byte[]>()));
        var registry = new GameplayPackSpriteAssetServiceRegistry(new Dictionary<string, GameplayPackSpriteAssetService>
        {
            ["stock.gg2"] = stockService,
            ["example.test"] = customService,
        });

        Assert.True(registry.TryGet("stock.gg2", out var resolvedStockService));
        Assert.Same(stockService, resolvedStockService);
        Assert.Same(customService, registry.GetRequired("example.test"));
    }

    [Fact]
    public void ClientRuntimeCompositionExposesGameplayPackSpriteAssets()
    {
        var stockService = new GameplayPackSpriteAssetService("stock.gg2", new StubAssetBinarySource(new Dictionary<string, byte[]>()));
        var registry = new GameplayPackSpriteAssetServiceRegistry(new Dictionary<string, GameplayPackSpriteAssetService>
        {
            ["stock.gg2"] = stockService,
        });
        var gameplayModPacks = (IReadOnlyList<GameplayModPackDefinition>)[StockGameplayModCatalog.Definition];
        var composition = new ClientRuntimeComposition(gameplayModPacks, registry);

        Assert.Single(composition.GameplayModPacks);
        Assert.Equal("stock.gg2", composition.GameplayModPacks[0].Id);
        Assert.Same(stockService, composition.GameplayPackSpriteAssets.GetRequired("stock.gg2"));
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
                    IsSpySuperjumping: false,
                    SpySuperjumpHorizontalVelocity: 0f,
                    SpySuperjumpCooldownTicksRemaining: 0,
                    SpyBackstabVisualTicksRemaining: 0,
                    IsUbered: false,
                    IsKritzCritBoosted: false,
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

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class StubAssetBinarySource(IReadOnlyDictionary<string, byte[]> assets) : IAssetBinarySource
    {
        private readonly IReadOnlyDictionary<string, byte[]> _assets = assets;

        public byte[]? TryReadAllBytes(string assetPath)
        {
            return _assets.TryGetValue(assetPath, out var bytes) ? bytes : null;
        }

        public Task<byte[]?> TryReadAllBytesAsync(string assetPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TryReadAllBytes(assetPath));
        }
    }
}
