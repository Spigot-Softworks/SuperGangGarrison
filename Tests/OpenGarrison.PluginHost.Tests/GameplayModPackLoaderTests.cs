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

[Collection(ContentRootTestGroup.Name)]
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
                BotMode = OfflineBotControllerMode.BotBrain,
                IngameResolution = IngameResolutionKind.Aspect16x9,
                WindowSize = WindowSizeKind.Scale150,
                OverheadChatEnabled = true,
                HudShowOnlyActiveWeapon = true,
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
            Assert.Equal(DisplayModeKind.Fullscreen, loaded.DisplayMode);
            Assert.True(loaded.VSync);
            Assert.Equal(OfflineBotControllerMode.BotBrain, loaded.BotMode);
            Assert.Equal(IngameResolutionKind.Aspect16x9, loaded.IngameResolution);
            Assert.Equal(WindowSizeKind.Scale150, loaded.WindowSize);
            Assert.True(loaded.OverheadChatEnabled);
            Assert.True(loaded.HudShowOnlyActiveWeapon);
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
    public void ClientSettingsRoundTripsBorderlessDisplayModeThroughIniDocument()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-client-settings-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        var settingsPath = Path.Combine(rootDirectory, ClientSettings.DefaultFileName);

        try
        {
            var settings = new ClientSettings
            {
                DisplayMode = DisplayModeKind.Borderless,
                IngameResolution = IngameResolutionKind.Aspect16x9,
                WindowSize = WindowSizeKind.Scale200,
            };

            settings.Save(settingsPath);

            var loaded = ClientSettings.Load(settingsPath);
            var document = OpenGarrisonPreferencesDocument.Load(settingsPath);

            Assert.False(loaded.Fullscreen);
            Assert.Equal(DisplayModeKind.Borderless, loaded.DisplayMode);
            Assert.False(document.Fullscreen);
            Assert.Equal(DisplayModeKind.Borderless, document.DisplayMode);
            Assert.Equal(WindowSizeKind.Scale200, loaded.WindowSize);
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
    public void ClientSettingsMapsLegacyFullscreenToFullscreenDisplayMode()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-client-settings-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        var settingsPath = Path.Combine(rootDirectory, ClientSettings.DefaultFileName);

        try
        {
            File.WriteAllText(
                settingsPath,
                "[Settings]" + Environment.NewLine +
                "Fullscreen=1" + Environment.NewLine);

            var loaded = ClientSettings.Load(settingsPath);
            var document = OpenGarrisonPreferencesDocument.Load(settingsPath);

            Assert.True(loaded.Fullscreen);
            Assert.Equal(DisplayModeKind.Fullscreen, loaded.DisplayMode);
            Assert.True(document.Fullscreen);
            Assert.Equal(DisplayModeKind.Fullscreen, document.DisplayMode);
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
    public void ClientSettingsDisplayModeOverridesLegacyFullscreenFlag()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-client-settings-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        var settingsPath = Path.Combine(rootDirectory, ClientSettings.DefaultFileName);

        try
        {
            File.WriteAllText(
                settingsPath,
                "[Settings]" + Environment.NewLine +
                "Fullscreen=1" + Environment.NewLine +
                "Display Mode=1" + Environment.NewLine);

            var loaded = ClientSettings.Load(settingsPath);
            var document = OpenGarrisonPreferencesDocument.Load(settingsPath);

            Assert.False(loaded.Fullscreen);
            Assert.Equal(DisplayModeKind.Borderless, loaded.DisplayMode);
            Assert.False(document.Fullscreen);
            Assert.Equal(DisplayModeKind.Borderless, document.DisplayMode);
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
    public void ClientSettingsDefaultsEnableOverheadChat()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-client-settings-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        var settingsPath = Path.Combine(rootDirectory, ClientSettings.DefaultFileName);

        try
        {
            var loaded = ClientSettings.Load(settingsPath);
            var document = OpenGarrisonPreferencesDocument.Load(settingsPath);

            Assert.True(loaded.OverheadChatEnabled);
            Assert.True(document.OverheadChatEnabled);
            Assert.Equal(IngameResolutionKind.Aspect16x9, loaded.IngameResolution);
            Assert.Equal(IngameResolutionKind.Aspect16x9, document.IngameResolution);
            Assert.Equal(WindowSizeKind.Scale100, loaded.WindowSize);
            Assert.Equal(WindowSizeKind.Scale100, document.WindowSize);
            Assert.Equal(100, loaded.DamageVignetteIntensityPercent);
            Assert.Equal(100, document.DamageVignetteIntensityPercent);
            Assert.Equal(120, loaded.CombatMusicVolumePercent);
            Assert.Equal(120, document.CombatMusicVolumePercent);
            Assert.True(loaded.PostGameMvpArtEnabled);
            Assert.True(document.PostGameMvpArtEnabled);
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
    public void ClientSettingsMissingOverheadChatKeyDefaultsEnabled()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-client-settings-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        var settingsPath = Path.Combine(rootDirectory, ClientSettings.DefaultFileName);

        try
        {
            File.WriteAllText(
                settingsPath,
                "[Settings]" + Environment.NewLine +
                "PlayerName=Legacy" + Environment.NewLine);

            var loaded = ClientSettings.Load(settingsPath);

            Assert.True(loaded.OverheadChatEnabled);
            Assert.Equal(OpenGarrisonPreferencesDocument.DefaultDamageVignetteIntensityPercent, loaded.DamageVignetteIntensityPercent);
            Assert.True(loaded.PostGameMvpArtEnabled);
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
    public void ClientSettingsUpgradesLegacyDefaultResolutionTo16x9()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-client-settings-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        var settingsPath = Path.Combine(rootDirectory, ClientSettings.DefaultFileName);

        try
        {
            File.WriteAllText(
                settingsPath,
                "[Settings]" + Environment.NewLine +
                "Resolution=1" + Environment.NewLine);

            var loaded = ClientSettings.Load(settingsPath);
            var document = OpenGarrisonPreferencesDocument.Load(settingsPath);

            Assert.Equal(IngameResolutionKind.Aspect16x9, loaded.IngameResolution);
            Assert.Equal(IngameResolutionKind.Aspect16x9, document.IngameResolution);
            Assert.Equal(WindowSizeKind.Scale100, loaded.WindowSize);
            Assert.Equal(WindowSizeKind.Scale100, document.WindowSize);
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
    public void ClientSettingsPreservesCurrentFormatExplicit4x3Resolution()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-client-settings-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        var settingsPath = Path.Combine(rootDirectory, ClientSettings.DefaultFileName);

        try
        {
            File.WriteAllText(
                settingsPath,
                "[Settings]" + Environment.NewLine +
                "Resolution=1" + Environment.NewLine +
                "Window Size=0" + Environment.NewLine);

            var loaded = ClientSettings.Load(settingsPath);
            var document = OpenGarrisonPreferencesDocument.Load(settingsPath);

            Assert.Equal(IngameResolutionKind.Aspect4x3, loaded.IngameResolution);
            Assert.Equal(IngameResolutionKind.Aspect4x3, document.IngameResolution);
            Assert.Equal(WindowSizeKind.Scale100, loaded.WindowSize);
            Assert.Equal(WindowSizeKind.Scale100, document.WindowSize);
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
        Assert.True(pack.Items.ContainsKey("weapon.umbrella"));
        Assert.True(pack.Items.ContainsKey("ability.umbrella"));
        Assert.True(pack.Items.ContainsKey("ability.civilian-taunt"));
        Assert.True(pack.Classes.ContainsKey("soldier"));
        Assert.True(pack.Classes.ContainsKey("civilian"));
        Assert.Equal("soldier.stock", pack.Classes["soldier"].DefaultLoadoutId);
        var civilianClass = pack.Classes["civilian"];
        Assert.Equal("Civilian/Financier", civilianClass.DisplayName);
        Assert.Equal("civilian.stock", civilianClass.DefaultLoadoutId);
        Assert.Equal("weapon.umbrella", civilianClass.Loadouts["civilian.stock"].PrimaryItemId);
        Assert.Equal("ability.umbrella", civilianClass.Loadouts["civilian.stock"].SecondaryItemId);
        Assert.Equal("ability.civilian-taunt", civilianClass.Loadouts["civilian.stock"].UtilityItemId);
        Assert.Equal(nameof(PlayerClass.Quote), civilianClass.Runtime?.PlayerClass);
        Assert.Equal("CivvieUmbrellaKL", civilianClass.Runtime?.PrimaryWeaponKillFeedSprite);
        Assert.Equal("Civvie", civilianClass.Presentation?.SpritePrefix);
        Assert.Equal("RunS", civilianClass.Presentation?.RunSuffix);
        Assert.Equal("RunS", civilianClass.Presentation?.JumpSuffix);
        Assert.Equal("CivvieUmbrellaAmmoS", pack.Items["weapon.umbrella"].Presentation.HudSpriteName);
        Assert.Equal("CivvieUmbrellaAbilityHudS", pack.Items["ability.umbrella"].Presentation.HudSpriteName);
        Assert.Equal(-16f, pack.Items["weapon.umbrella"].Presentation.WeaponOffsetX);
        Assert.Equal(-25f, pack.Items["weapon.umbrella"].Presentation.WeaponOffsetY);
        Assert.Equal(-16f, pack.Items["ability.umbrella"].Presentation.WeaponOffsetX);
        Assert.Equal(-25f, pack.Items["ability.umbrella"].Presentation.WeaponOffsetY);
        Assert.Equal("CivvieUmbrellaOpenAnimS", pack.Items["ability.umbrella"].Presentation.WorldSpriteName);
        Assert.Equal(360, pack.Items["ability.umbrella"].Presentation.Hud?.MaxCooldown);
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieRedS"));
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieRedRunS"));
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieRedTauntS"));
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieMoneyS"));
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieUmbrellaKL"));
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieUmbrellaAmmoS"));
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieUmbrellaAbilityHudS"));
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieUmbrellaOpenAnimS"));
        Assert.True(pack.Assets.Sprites.ContainsKey("CivvieUmbrellaShieldBlockS"));
        Assert.True(pack.Classes["soldier"].Loadouts.ContainsKey("soldier.direct-hit"));
        var soldierRuntime = pack.Classes["soldier"].Runtime;
        Assert.NotNull(soldierRuntime);
        Assert.Equal(nameof(PlayerClass.Soldier), soldierRuntime!.PlayerClass);
        Assert.True(soldierRuntime.SupportsExperimentalAcquiredWeapon);
        Assert.Equal("RocketKL", soldierRuntime.PrimaryWeaponKillFeedSprite);
        var soldierPresentation = pack.Classes["soldier"].Presentation;
        Assert.NotNull(soldierPresentation);
        Assert.Equal("Soldier", soldierPresentation.SpritePrefix);
        Assert.Equal("StandS", soldierPresentation.StandSuffix);
        Assert.True(pack.Assets.Sprites.ContainsKey("ScoutRedStandS"));
        var scoutStandSprite = pack.Assets.Sprites["ScoutRedStandS"];
        Assert.Equal("Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png", scoutStandSprite.FramePaths[0]);
        Assert.Equal(30, scoutStandSprite.OriginX);
        Assert.Equal(40, scoutStandSprite.OriginY);
        Assert.NotNull(scoutStandSprite.Mask);
        Assert.Equal("RECTANGLE", scoutStandSprite.Mask!.Shape);
        Assert.Equal("MANUAL", scoutStandSprite.Mask.BoundsMode);
        Assert.Equal(24, scoutStandSprite.Mask.Left);
        Assert.Equal(63, scoutStandSprite.Mask.Bottom);
        Assert.True(pack.Assets.Sprites.ContainsKey("gg2FontS"));
        var fontSprite = pack.Assets.Sprites["gg2FontS"];
        Assert.Equal("Content/Sprites/gg2FontS.images/image 0.png", fontSprite.FramePaths[0]);
        Assert.Equal("Content/Sprites/gg2FontS.images/image 10.png", fontSprite.FramePaths[10]);
        Assert.NotNull(fontSprite.Mask);
        Assert.Equal("MANUAL", fontSprite.Mask!.BoundsMode);
        Assert.True(pack.Assets.Sprites.ContainsKey("IntelTimerS"));
        var intelTimerSprite = pack.Assets.Sprites["IntelTimerS"];
        Assert.Equal(24, intelTimerSprite.FramePaths.Count);
        Assert.Equal("Content/Sprites/InGameElements/IntelTimerS.images/image 23.png", intelTimerSprite.FramePaths[23]);
        Assert.Equal(5, intelTimerSprite.OriginX);
        Assert.True(pack.Assets.Sprites.ContainsKey("RocketlauncherFRS"));
        var reloadSprite = pack.Assets.Sprites["RocketlauncherFRS"];
        Assert.Equal(24, reloadSprite.FramePaths.Count);
        Assert.Equal("Content/Sprites/Weapons/Reloading/RocketlauncherFRS.images/image 23.png", reloadSprite.FramePaths[23]);
        Assert.NotNull(reloadSprite.Mask);
        Assert.Equal("PRECISE", reloadSprite.Mask!.Shape);
        Assert.True(pack.Assets.Sprites.ContainsKey("stock.gg2.weapon.directhit.world"));
        Assert.Equal(2, pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths.Count);
        Assert.Equal("assets/directhit/DirectHit.red.png", pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths[0]);
        Assert.Equal("assets/directhit/DirectHit.blue.png", pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths[1]);
        Assert.Equal(2, pack.Assets.Sprites["stock.gg2.weapon.directhit.recoil"].FramePaths.Count);
        Assert.Equal(50, pack.Assets.Sprites["stock.gg2.weapon.directhit.hud"].FrameWidth);
        Assert.True(pack.Assets.Sprites.ContainsKey("MvpRedMedicS"));
        var redMedicMvpSprite = pack.Assets.Sprites["MvpRedMedicS"];
        Assert.Equal(4, redMedicMvpSprite.FramePaths.Count);
        Assert.Equal("assets/mvp/red/medic/nonwinner_0.png", redMedicMvpSprite.FramePaths[0]);
        Assert.Equal("assets/mvp/red/medic/nonwinner_3.png", redMedicMvpSprite.FramePaths[3]);
        Assert.Equal(26, redMedicMvpSprite.OriginX);
        Assert.Equal(52, redMedicMvpSprite.OriginY);
        Assert.True(pack.Assets.Sprites.ContainsKey("MvpRedHeavyWinnerS"));
        var redHeavyWinnerMvpSprite = pack.Assets.Sprites["MvpRedHeavyWinnerS"];
        Assert.Single(redHeavyWinnerMvpSprite.FramePaths);
        Assert.Equal("assets/mvp/red/heavy/winner_0.png", redHeavyWinnerMvpSprite.FramePaths[0]);
        Assert.Equal(26, redHeavyWinnerMvpSprite.OriginX);
        Assert.Equal(52, redHeavyWinnerMvpSprite.OriginY);
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
    public void StockGameplayPackExposesAbilityMetadataForStockAbilities()
    {
        var pack = StockGameplayModCatalog.Definition;

        var pyroAirblast = pack.Items["ability.pyro-airblast"].Ability;
        Assert.NotNull(pyroAirblast);
        Assert.Equal(GameplayAbilityConstants.SecondaryCategory, pyroAirblast!.Category);
        Assert.Equal(GameplayAbilityConstants.PressedActivation, pyroAirblast.Activation);
        Assert.Equal(BuiltInGameplayBehaviorIds.PyroAirblast, pyroAirblast.ExecutorId);

        var heavyUtility = pack.Items["ability.heavy-utility"].Ability;
        Assert.NotNull(heavyUtility);
        Assert.Equal(GameplayAbilityConstants.UtilityCategory, heavyUtility!.Category);
        Assert.Equal(GameplayAbilityConstants.PressedActivation, heavyUtility.Activation);
        Assert.Equal(BuiltInGameplayBehaviorIds.HeavyGhostDash, heavyUtility.ExecutorId);
        Assert.Contains("dash", heavyUtility.Tags);
        Assert.Equal(12, heavyUtility.Parameters["cooldownSeconds"].GetInt32());
        Assert.Equal(ExperimentalGameplaySettings.HeavyGhostDashBurstSpeedMultiplier, heavyUtility.Parameters["burstSpeedMultiplier"].GetSingle());
        Assert.True(heavyUtility.Parameters["disableGravity"].GetBoolean());
        Assert.True(heavyUtility.Parameters["enableGhostTrail"].GetBoolean());

        var soldierExperimentalSecondary = pack.Items["weapon.soldier-shotgun"].Ability;
        Assert.NotNull(soldierExperimentalSecondary);
        Assert.Equal(GameplayAbilityConstants.SecondaryCategory, soldierExperimentalSecondary!.Category);
        Assert.Equal(BuiltInGameplayBehaviorIds.ExperimentalSoldierSecondary, soldierExperimentalSecondary.ExecutorId);
        Assert.Contains("experimental_ltd", soldierExperimentalSecondary.Tags);

        var medigunNeedles = pack.Items["weapon.medigun"].Ability;
        Assert.NotNull(medigunNeedles);
        Assert.Equal(GameplayAbilityConstants.SecondaryCategory, medigunNeedles!.Category);
        Assert.Equal(GameplayAbilityConstants.HeldActivation, medigunNeedles.Activation);
        Assert.Equal(BuiltInGameplayBehaviorIds.MedicNeedlegun, medigunNeedles.ExecutorId);

        var kritzBeam = pack.Items["weapon.medigun.crit"].Ability;
        Assert.NotNull(kritzBeam);
        Assert.Equal(GameplayAbilityConstants.SecondaryCategory, kritzBeam!.Category);
        Assert.Equal(GameplayAbilityConstants.HeldActivation, kritzBeam.Activation);
        Assert.Equal(BuiltInGameplayBehaviorIds.MedicKritzBeam, kritzBeam.ExecutorId);
        Assert.Equal(150, kritzBeam.Parameters["range"].GetInt32());
        Assert.Equal(1, kritzBeam.Parameters["damagePerSecond"].GetInt32());
        Assert.Equal(30, kritzBeam.Parameters["chargePerDamageTick"].GetInt32());

        Assert.False(pack.Items.ContainsKey("ability.quote-blade-throw"));
        Assert.False(pack.Items.ContainsKey("ability.quote-utility"));
    }

    [Fact]
    public void StockGameplayPackExposesHiddenPassiveAndTauntAbilityItems()
    {
        var pack = StockGameplayModCatalog.Definition;

        var passive = pack.Items["ability.experimental-ltd-passive"].Ability;
        Assert.NotNull(passive);
        Assert.Equal(GameplayAbilityConstants.PassiveCategory, passive!.Category);
        Assert.Equal(GameplayAbilityConstants.PassiveTickActivation, passive.Activation);
        Assert.Equal(BuiltInGameplayBehaviorIds.ExperimentalLtdPassive, passive.ExecutorId);
        Assert.Contains("experimental_ltd", passive.Tags);

        var rage = pack.Items["ability.experimental-ltd-rage"].Ability;
        Assert.NotNull(rage);
        Assert.Equal(GameplayAbilityConstants.TauntCategory, rage!.Category);
        Assert.Equal(GameplayAbilityConstants.PressedActivation, rage.Activation);
        Assert.Equal(BuiltInGameplayBehaviorIds.ExperimentalLtdRage, rage.ExecutorId);

        var soldierStock = pack.Classes["soldier"].Loadouts["soldier.stock"];
        Assert.NotNull(soldierStock.AbilityItemIds);
        Assert.Contains("ability.experimental-ltd-passive", soldierStock.AbilityItemIds!);
        Assert.Contains("ability.experimental-ltd-rage", soldierStock.AbilityItemIds!);

        var heavyStock = pack.Classes["heavy"].Loadouts["heavy.stock"];
        Assert.NotNull(heavyStock.AbilityItemIds);
        Assert.Contains("ability.experimental-ltd-passive", heavyStock.AbilityItemIds!);
        Assert.DoesNotContain("ability.experimental-ltd-rage", heavyStock.AbilityItemIds!);
    }

    [Fact]
    public void GameplayAbilityConstantsLabelBuiltInAndReservedCategories()
    {
        Assert.True(GameplayAbilityConstants.IsBuiltInDispatchedCategory(GameplayAbilityConstants.SecondaryCategory));
        Assert.True(GameplayAbilityConstants.IsBuiltInDispatchedCategory(GameplayAbilityConstants.UtilityCategory));
        Assert.True(GameplayAbilityConstants.IsBuiltInDispatchedCategory(GameplayAbilityConstants.PassiveCategory));
        Assert.True(GameplayAbilityConstants.IsBuiltInDispatchedCategory(GameplayAbilityConstants.TauntCategory));

        Assert.True(GameplayAbilityConstants.IsReservedCategory(GameplayAbilityConstants.MovementCategory));
        Assert.True(GameplayAbilityConstants.IsReservedCategory(GameplayAbilityConstants.PrimaryAltCategory));
        Assert.True(GameplayAbilityConstants.IsReservedCategory(GameplayAbilityConstants.StatusCategory));

        Assert.False(GameplayAbilityConstants.IsBuiltInDispatchedCategory(GameplayAbilityConstants.MovementCategory));
        Assert.False(GameplayAbilityConstants.IsReservedCategory(GameplayAbilityConstants.UtilityCategory));
    }

    [Fact]
    public void StockGameplayPackExposesHudMetadata()
    {
        var pack = StockGameplayModCatalog.Definition;

        var soldierShotgunHud = pack.Items["weapon.soldier-shotgun"].Presentation.Hud;
        Assert.NotNull(soldierShotgunHud);
        Assert.Equal(GameplayItemHudDisplayKinds.AmmoPanel, soldierShotgunHud!.DisplayKind);
        Assert.Equal(GameplayItemHudStackGroups.Weapon, soldierShotgunHud.StackGroup);
        Assert.Equal(GameplayItemHudStateProviders.SecondaryAmmo, soldierShotgunHud.StateProvider);
        Assert.Equal(60, soldierShotgunHud.Order);

        var grenadeLauncherHud = pack.Items["weapon.grenadelauncher"].Presentation.Hud;
        Assert.NotNull(grenadeLauncherHud);
        Assert.Equal(GameplayItemHudDisplayKinds.AmmoPanel, grenadeLauncherHud!.DisplayKind);
        Assert.Equal(GameplayItemHudStateProviders.UtilityAmmo, grenadeLauncherHud.StateProvider);
        Assert.Equal(40, grenadeLauncherHud.Order);

        var sandvichHud = pack.Items["ability.heavy-sandvich"].Presentation.Hud;
        Assert.NotNull(sandvichHud);
        Assert.Equal(GameplayItemHudDisplayKinds.CooldownIcon, sandvichHud!.DisplayKind);
        Assert.Equal(GameplayItemHudStackGroups.Ability, sandvichHud.StackGroup);
        Assert.Equal(GameplayItemHudStateProviders.HeavySandvichCooldown, sandvichHud.StateProvider);

        var heavyUtilityHud = pack.Items["ability.heavy-utility"].Presentation.Hud;
        Assert.Null(pack.Items["ability.heavy-utility"].Presentation.HudSpriteName);
        Assert.NotNull(heavyUtilityHud);
        Assert.Equal(GameplayItemHudStackGroups.Ability, heavyUtilityHud!.StackGroup);
        Assert.Equal(GameplayItemHudStateProviders.AbilityCooldown, heavyUtilityHud.StateProvider);
        Assert.Equal(GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, heavyUtilityHud.StateOwner);
        Assert.Equal(GameplayAbilityReplicatedState.HeavyDashCooldownTicksKey, heavyUtilityHud.CooldownKey);
        Assert.Equal(360, heavyUtilityHud.MaxCooldown);
        Assert.Equal(GameplayAbilityReplicatedState.HeavyDashActiveKey, heavyUtilityHud.ActiveKey);
    }

    [Fact]
    public void GameplayPackLoaderRejectsUnsupportedHudMetadata()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var packDirectory = Path.Combine(rootDirectory, "bad-hud-metadata");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items"));

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "pack.json"),
                """
                {
                  "id": "bad.hud.metadata",
                  "displayName": "Bad HUD Metadata",
                  "version": "1.0.0"
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.bad.json"),
                """
                {
                  "id": "weapon.bad",
                  "displayName": "Bad Weapon",
                  "slot": "Primary",
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": {
                    "maxAmmo": 1
                  },
                  "presentation": {
                    "hudSpriteName": "BadHudS",
                    "hud": {
                      "displayKind": "unsupportedPanel",
                      "stackGroup": "weapon"
                    }
                  }
                }
                """);

            var ex = Assert.Throws<InvalidOperationException>(() => GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory));
            Assert.Contains("unsupported display kind", ex.Message, StringComparison.Ordinal);
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
    public void GameplayPackLoaderLeavesAbilityMetadataOmittedWhenDataDoesNotDeclareIt()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var packDirectory = Path.Combine(rootDirectory, "explicit-ability");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items"));

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "pack.json"),
                """
                {
                  "id": "explicit.ability",
                  "displayName": "Explicit Ability",
                  "version": "1.0.0"
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.legacy-airblast.json"),
                """
                {
                  "id": "ability.legacy-airblast",
                  "displayName": "Legacy Airblast",
                  "slot": "Secondary",
                  "behaviorId": "builtin.ability.pyro_airblast",
                  "ammo": {},
                  "presentation": {}
                }
                """);

            var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);
            var ability = pack.Items["ability.legacy-airblast"].Ability;

            Assert.Null(ability);
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
    public void GameplayPackLoaderRejectsUnsupportedAbilityActivation()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var packDirectory = Path.Combine(rootDirectory, "bad-ability");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items"));

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "pack.json"),
                """
                {
                  "id": "bad.ability",
                  "displayName": "Bad Ability",
                  "version": "1.0.0"
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.bad.json"),
                """
                {
                  "id": "ability.bad",
                  "displayName": "Bad Ability",
                  "slot": "Utility",
                  "behaviorId": "builtin.utility.heavy",
                  "ability": {
                    "category": "utility",
                    "activation": "whenever",
                    "executorId": "builtin.ability.heavy_ghost_dash"
                  },
                  "ammo": {},
                  "presentation": {}
                }
                """);

            var exception = Assert.Throws<InvalidOperationException>(() => GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory));
            Assert.Contains("unsupported activation", exception.Message, StringComparison.OrdinalIgnoreCase);
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
    public void GameplayPackLoaderRejectsMalformedKnownAbilityParameter()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var packDirectory = Path.Combine(rootDirectory, "bad-ability-parameter");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items"));

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "pack.json"),
                """
                {
                  "id": "bad.ability-parameter",
                  "displayName": "Bad Ability Parameter",
                  "version": "1.0.0"
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.bad-parameter.json"),
                """
                {
                  "id": "ability.bad-parameter",
                  "displayName": "Bad Ability Parameter",
                  "slot": "Utility",
                  "behaviorId": "builtin.utility.heavy",
                  "ability": {
                    "category": "utility",
                    "activation": "pressed",
                    "executorId": "builtin.ability.heavy_ghost_dash",
                    "parameters": {
                      "cooldownSeconds": "six"
                    }
                  },
                  "ammo": {},
                  "presentation": {}
                }
                """);

            var exception = Assert.Throws<InvalidOperationException>(() => GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory));
            Assert.Contains("cooldownSeconds", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("numeric", exception.Message, StringComparison.OrdinalIgnoreCase);
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
    public void GameplayPackLoaderAcceptsCustomHudMetadata()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var packDirectory = Path.Combine(rootDirectory, "custom-hud-metadata");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items"));

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "pack.json"),
                """
                {
                  "id": "custom.hud.metadata",
                  "displayName": "Custom HUD Metadata",
                  "version": "1.0.0"
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.custom.json"),
                """
                {
                  "id": "ability.custom",
                  "displayName": "Custom Ability",
                  "slot": "Utility",
                  "behaviorId": "builtin.ability.heavy_ghost_dash",
                  "ability": {
                    "category": "utility",
                    "activation": "pressed",
                    "executorId": "builtin.ability.heavy_ghost_dash"
                  },
                  "ammo": {},
                  "presentation": {
                    "hud": {
                      "displayKind": "custom",
                      "stackGroup": "ability",
                      "stateProvider": "abilityCooldown",
                      "stateOwner": "tests.server.custom",
                      "cooldownKey": "custom_cooldown",
                      "widgetId": "custom-widget",
                      "widgetOwner": "tests.client.custom",
                      "widgetCallback": "draw_custom_widget",
                      "anchor": "bottom_right"
                    }
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.custom-panel.json"),
                """
                {
                  "id": "weapon.custom-panel",
                  "displayName": "Custom Panel Weapon",
                  "slot": "Secondary",
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": {
                    "maxAmmo": 6
                  },
                  "presentation": {
                    "hudSpriteName": "ShotgunAmmoS",
                    "hud": {
                      "displayKind": "custom",
                      "stackGroup": "weapon",
                      "stateProvider": "secondaryAmmo",
                      "widgetId": "weaponAmmoPanel"
                    }
                  }
                }
                """);

            var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);
            var abilityHud = pack.Items["ability.custom"].Presentation.Hud;
            Assert.NotNull(abilityHud);
            Assert.Equal(GameplayItemHudDisplayKinds.Custom, abilityHud!.DisplayKind);
            Assert.Equal("custom-widget", abilityHud.WidgetId);
            Assert.Equal("tests.client.custom", abilityHud.WidgetOwner);
            Assert.Equal("draw_custom_widget", abilityHud.WidgetCallback);
            Assert.Equal("bottom_right", abilityHud.Anchor);

            var weaponHud = pack.Items["weapon.custom-panel"].Presentation.Hud;
            Assert.NotNull(weaponHud);
            Assert.Equal(GameplayItemHudDisplayKinds.Custom, weaponHud!.DisplayKind);
            Assert.Equal(GameplayItemHudWidgetIds.WeaponAmmoPanel, weaponHud.WidgetId);
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
    public void RuntimeRegistryCanOverrideExistingRuntimeClassSlotWhenExplicitlyAllowed()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var loadout = new GameplayClassLoadoutDefinition(
            "plugin.example.soldier.stock",
            "Stock",
            "plugin.example.weapon.blade");
        var overridePack = new GameplayModPackDefinition(
            "plugin.example",
            "Example Override Gameplay Pack",
            new Version(1, 0, 0),
            new Dictionary<string, GameplayItemDefinition>(StringComparer.Ordinal)
            {
                ["plugin.example.weapon.blade"] = new(
                    "plugin.example.weapon.blade",
                    "Plugin Blade",
                    GameplayEquipmentSlot.Primary,
                    BuiltInGameplayBehaviorIds.Blade,
                    new GameplayItemAmmoDefinition(
                        MaxAmmo: 100,
                        AmmoPerUse: 0,
                        ProjectilesPerUse: 1,
                        UseDelaySourceTicks: 5,
                        ReloadSourceTicks: 0),
                    new GameplayItemPresentationDefinition()),
            },
            new Dictionary<string, GameplayClassDefinition>(StringComparer.Ordinal)
            {
                ["plugin.example.soldier"] = new(
                    "plugin.example.soldier",
                    "Plugin Soldier",
                    new GameplayClassMovementDefinition(
                        MaxHealth: 140,
                        CollisionLeft: -7.0f,
                        CollisionTop: -12.0f,
                        CollisionRight: 8.0f,
                        CollisionBottom: 12.0f,
                        RunPower: 1.07f,
                        JumpStrength: 8.3f,
                        MaxAirJumps: 0,
                        TauntLengthFrames: 16),
                    new Dictionary<string, GameplayClassLoadoutDefinition>(StringComparer.Ordinal)
                    {
                        [loadout.Id] = loadout,
                    },
                    loadout.Id,
                    new GameplayClassPresentationDefinition("Soldier"),
                    new GameplayClassRuntimeDefinition(
                        PlayerClass: "Soldier",
                        SupportsExperimentalAcquiredWeapon: false,
                        PrimaryWeaponKillFeedSprite: "RocketKL")),
            },
            GameplayModPackAssetCatalog.Empty);

        Assert.False(registry.TryRegisterModPack(overridePack, allowRuntimeClassBindingOverride: false, out var error));
        Assert.Contains("conflicts with existing binding", error, StringComparison.Ordinal);

        Assert.True(registry.TryRegisterModPack(overridePack, allowRuntimeClassBindingOverride: true, out error), error);
        Assert.Equal("Plugin Soldier", registry.GetClassDefinition(PlayerClass.Soldier).DisplayName);
        Assert.Equal("plugin.example.weapon.blade", registry.GetDefaultLoadout(PlayerClass.Soldier).PrimaryItemId);
        Assert.Equal("Plugin Blade", registry.CreateCharacterClassDefinition(PlayerClass.Soldier).PrimaryWeapon.DisplayName);
    }

    [Fact]
    public void RuntimeRegistryAcceptsPluginClassWithoutLegacyPlayerClassBinding()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var loadout = new GameplayClassLoadoutDefinition(
            "plugin.example.ranger.stock",
            "Stock",
            "plugin.example.weapon.carbine");
        var modPack = new GameplayModPackDefinition(
            "plugin.example",
            "Example Plugin Classes",
            new Version(1, 0, 0),
            new Dictionary<string, GameplayItemDefinition>(StringComparer.Ordinal)
            {
                ["plugin.example.weapon.carbine"] = new(
                    "plugin.example.weapon.carbine",
                    "Ranger Carbine",
                    GameplayEquipmentSlot.Primary,
                    BuiltInGameplayBehaviorIds.PelletGun,
                    new GameplayItemAmmoDefinition(
                        MaxAmmo: 6,
                        AmmoPerUse: 1,
                        ProjectilesPerUse: 4,
                        UseDelaySourceTicks: 18,
                        ReloadSourceTicks: 15,
                        SpreadDegrees: 5f,
                        MinProjectileSpeed: 11f,
                        AdditionalProjectileSpeed: 4f,
                        AutoReloads: true),
                    new GameplayItemPresentationDefinition()),
            },
            new Dictionary<string, GameplayClassDefinition>(StringComparer.Ordinal)
            {
                ["plugin.example.ranger"] = new(
                    "plugin.example.ranger",
                    "Plugin Ranger",
                    new GameplayClassMovementDefinition(
                        MaxHealth: 110,
                        CollisionLeft: -6.0f,
                        CollisionTop: -10.0f,
                        CollisionRight: 7.0f,
                        CollisionBottom: 24.0f,
                        RunPower: 1.35f,
                        JumpStrength: 8.3f,
                        MaxAirJumps: 1,
                        TauntLengthFrames: 8),
                    new Dictionary<string, GameplayClassLoadoutDefinition>(StringComparer.Ordinal)
                    {
                        [loadout.Id] = loadout,
                    },
                    loadout.Id,
                    new GameplayClassPresentationDefinition("Scout"),
                    new GameplayClassRuntimeDefinition(
                        BasePlayerClass: "Scout",
                        BotGraphPlayerClass: "Soldier",
                        SupportsExperimentalAcquiredWeapon: false,
                        PrimaryWeaponKillFeedSprite: "ScatterKL")),
            },
            GameplayModPackAssetCatalog.Empty);

        Assert.True(registry.TryRegisterModPack(modPack, allowRuntimeClassBindingOverride: false, out var error), error);

        Assert.True(registry.TryGetClassBinding("plugin.example.ranger", out var binding));
        Assert.False(binding.BindsLegacyPlayerClass);
        Assert.Equal(PlayerClass.Scout, binding.PlayerClass);
        Assert.Equal(PlayerClass.Scout, binding.BasePlayerClass);
        Assert.Equal(PlayerClass.Soldier, binding.BotGraphPlayerClass);
        Assert.Equal("plugin.example.ranger", binding.ClassId);
        Assert.Contains(registry.RuntimeClassBindings, candidate => candidate.ClassId == "plugin.example.ranger");

        var classDefinition = registry.CreateCharacterClassDefinition("plugin.example.ranger");
        Assert.Equal(PlayerClass.Scout, classDefinition.Id);
        Assert.Equal("plugin.example.ranger", classDefinition.GameplayClassId);
        Assert.Equal("plugin.example", classDefinition.GameplayModPackId);
        Assert.Equal(PlayerClass.Soldier, classDefinition.BotGraphClassId);
        Assert.Equal("Plugin Ranger", classDefinition.DisplayName);
        Assert.Equal("Ranger Carbine", classDefinition.PrimaryWeapon.DisplayName);
        Assert.Equal("plugin.example.weapon.carbine", registry.GetDefaultLoadout("plugin.example.ranger").PrimaryItemId);
    }

    [Theory]
    [InlineData("Client")]
    [InlineData("Server")]
    public void PackagedQuoteCurlyGameplayPackLoads(string hostFolder)
    {
        var packDirectory = ProjectSourceLocator.FindDirectory(Path.Combine(
            "Plugins",
            "Packaged",
            hostFolder,
            "Lua.QuoteCurly",
            "Gameplay",
            "quote-curly.gg2"));

        Assert.False(string.IsNullOrWhiteSpace(packDirectory));
        var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory!);
        Assert.Equal("plugin.quote-curly", pack.Id);
        Assert.True(pack.Items.ContainsKey("plugin.quote-curly.weapon.blade"));
        Assert.True(pack.Items.ContainsKey("plugin.quote-curly.weapon.ranger-carbine"));
        var quoteBladeThrow = pack.Items["plugin.quote-curly.ability.blade-throw"].Ability;
        Assert.NotNull(quoteBladeThrow);
        Assert.Equal(PlayerEntity.QuoteBladeEnergyCost, quoteBladeThrow!.Parameters["energyCost"].GetInt32());
        Assert.Equal(PlayerEntity.QuoteBladeMaxOut, quoteBladeThrow.Parameters["activeProjectileLimit"].GetInt32());
        Assert.Equal(PlayerEntity.QuoteBladeLifetimeTicks, quoteBladeThrow.Parameters["lifetimeTicks"].GetInt32());
        Assert.True(pack.Classes.TryGetValue("plugin.quote-curly.quote", out var gameplayClass));
        Assert.Equal("Quote/Curly", gameplayClass!.DisplayName);
        Assert.Equal(string.Empty, gameplayClass.Runtime?.PlayerClass);
        Assert.Equal("Quote", gameplayClass.Runtime?.BasePlayerClass);
        Assert.Equal("Quote", gameplayClass.Runtime?.BotGraphPlayerClass);
        Assert.Equal("BladeKL", gameplayClass.Runtime?.PrimaryWeaponKillFeedSprite);
        Assert.Equal(
            "plugin.quote-curly.weapon.blade",
            gameplayClass.Loadouts[gameplayClass.DefaultLoadoutId].PrimaryItemId);
        Assert.Equal(
            "plugin.quote-curly.ability.blade-throw",
            gameplayClass.Loadouts[gameplayClass.DefaultLoadoutId].SecondaryItemId);
        Assert.Equal(
            "plugin.quote-curly.ability.utility",
            gameplayClass.Loadouts[gameplayClass.DefaultLoadoutId].UtilityItemId);
        Assert.True(pack.Classes.TryGetValue("plugin.quote-curly.ranger", out var rangerClass));
        Assert.Equal(string.Empty, rangerClass!.Runtime?.PlayerClass);
        Assert.Equal("Scout", rangerClass.Runtime?.BasePlayerClass);
        Assert.Equal("Scout", rangerClass.Runtime?.BotGraphPlayerClass);
        Assert.Equal(
            "plugin.quote-curly.weapon.ranger-carbine",
            rangerClass.Loadouts[rangerClass.DefaultLoadoutId].PrimaryItemId);
    }

    [Fact]
    public void PackagedQuoteCurlyGameplayPackDoesNotOverrideStockCivilianRuntimeBinding()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        Assert.True(registry.TryGetClassBinding(PlayerClass.Quote, out var stockQuoteBinding));
        Assert.Equal("civilian", stockQuoteBinding.ClassId);
        Assert.Equal("Civilian/Financier", registry.GetClassDefinition(PlayerClass.Quote).DisplayName);
        Assert.Equal("weapon.umbrella", registry.GetDefaultLoadout(PlayerClass.Quote).PrimaryItemId);

        var packDirectory = ProjectSourceLocator.FindDirectory(Path.Combine(
            "Plugins",
            "Packaged",
            "Server",
            "Lua.QuoteCurly",
            "Gameplay",
            "quote-curly.gg2"));

        Assert.False(string.IsNullOrWhiteSpace(packDirectory));
        var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory!);

        Assert.True(registry.TryRegisterModPack(pack, allowRuntimeClassBindingOverride: false, out var error), error);
        Assert.True(registry.TryGetClassBinding(PlayerClass.Quote, out var quoteBinding));
        Assert.Equal("civilian", quoteBinding.ClassId);
        Assert.Equal("Civilian/Financier", registry.GetClassDefinition(PlayerClass.Quote).DisplayName);
        Assert.Equal("weapon.umbrella", registry.GetDefaultLoadout(PlayerClass.Quote).PrimaryItemId);
        Assert.Equal("ability.umbrella", registry.GetDefaultLoadout(PlayerClass.Quote).SecondaryItemId);
        Assert.Equal("ability.civilian-taunt", registry.GetDefaultLoadout(PlayerClass.Quote).UtilityItemId);
        Assert.Equal("Quote/Curly", registry.GetClassDefinition("plugin.quote-curly.quote").DisplayName);
        Assert.Equal("plugin.quote-curly.weapon.blade", registry.GetDefaultLoadout("plugin.quote-curly.quote").PrimaryItemId);
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
        Assert.Equal("Content/Gameplay/stock.gg2/assets/directhit/DirectHit.red.png", GameplayPackAssetPathUtility.BuildPackAssetPath("stock.gg2", "assets/directhit/DirectHit.red.png"));
        Assert.Equal("Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png", GameplayPackAssetPathUtility.BuildPackAssetPath("stock.gg2", "Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png"));
    }

    [Fact]
    public void GameplayPackLookupPrefersProjectSourceContentWhenUsingDefaultContentRoot()
    {
        var originalContentRoot = ContentRoot.Path;
        try
        {
            ContentRoot.Initialize("Content");

            var packDirectory = GameplayModPackDirectoryLoader.FindPackDirectory(StockGameplayModCatalog.StockPackDirectoryName);

            Assert.NotNull(packDirectory);
            var normalizedPackDirectory = Path.GetFullPath(packDirectory!)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimEnd(Path.DirectorySeparatorChar);
            var expectedSuffix = Path.Combine("Core", "Content", "Gameplay", StockGameplayModCatalog.StockPackDirectoryName)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            Assert.True(
                normalizedPackDirectory.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase),
                $"expected project source pack path ending with {expectedSuffix}, got {normalizedPackDirectory}");
        }
        finally
        {
            ContentRoot.Initialize(originalContentRoot);
        }
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
                        "Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png"
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
        Assert.Equal("Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png", loaded.FirstFrameContentPath);
    }

    [Fact]
    public void GameplaySpriteBinaryLoaderLoadsSourceImagesFromAssetSource()
    {
        var spriteDefinition = new GameplaySpriteAssetDefinition(
            "ScoutRedStandS",
            ["Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png"],
            OriginX: 30,
            OriginY: 40);
        var assetSource = new StubAssetBinarySource(new Dictionary<string, byte[]>
        {
            ["Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png"] = [1, 2, 3, 4],
        });

        var loaded = GameplaySpriteBinaryLoader.LoadSourceImages(assetSource, spriteDefinition);

        Assert.Equal("ScoutRedStandS", loaded.Definition.Id);
        Assert.Single(loaded.SourceImages);
        Assert.Equal("Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png", loaded.SourceImages[0].FramePath);
        Assert.Equal([1, 2, 3, 4], loaded.SourceImages[0].Bytes);
    }

    [Fact]
    public void StockGameplayPackLoadsCivilianSpriteSourceImagesFromFilesystem()
    {
        var stockPack = StockGameplayModCatalog.Definition;
        var packDirectory = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content", "Gameplay", "stock.gg2"));

        Assert.False(string.IsNullOrWhiteSpace(packDirectory));
        var assetSource = new FileSystemAssetBinarySource(packDirectory!);
        var spriteAssetService = new GameplayPackSpriteAssetService(
            "stock.gg2",
            assetSource,
            spriteDefinitions: stockPack.Assets.Sprites);

        var civilianStand = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieRedS"]);
        var civilianRun = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieRedRunS"]);
        var civilianTaunt = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieRedTauntS"]);
        var money = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieMoneyS"]);
        var umbrellaOpen = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieUmbrellaOpenS"]);
        var umbrellaOpenStrip = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieUmbrellaOpenStripS"]);
        var umbrellaOpenAnim = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieUmbrellaOpenAnimS"]);
        var umbrellaShieldBlock = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieUmbrellaShieldBlockS"]);
        var umbrellaAmmoHud = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieUmbrellaAmmoS"]);
        var umbrellaAbilityHud = spriteAssetService.LoadRegisteredSprite(stockPack.Assets.Sprites["CivvieUmbrellaAbilityHudS"]);

        Assert.Equal("stock.gg2", civilianStand.Definition.PackId);
        Assert.True(civilianStand.SourceSet.SourceImages.Count > 0);
        Assert.All(civilianStand.SourceSet.SourceImages, frame => Assert.True(frame.Bytes.Length > 0));
        Assert.NotEqual(civilianTaunt.SourceSet.SourceImages[0].Bytes, civilianStand.SourceSet.SourceImages[0].Bytes);
        Assert.True(civilianRun.SourceSet.SourceImages.Count > 0);
        Assert.All(civilianRun.SourceSet.SourceImages, frame => Assert.True(frame.Bytes.Length > 0));
        Assert.NotEqual(civilianRun.SourceSet.SourceImages[0].Bytes, civilianStand.SourceSet.SourceImages[0].Bytes);
        Assert.True(money.SourceSet.SourceImages.Count > 0);
        Assert.All(money.SourceSet.SourceImages, frame => Assert.True(frame.Bytes.Length > 0));
        Assert.Equal(2, umbrellaOpen.SourceSet.SourceImages.Count);
        Assert.Equal(umbrellaOpenStrip.SourceSet.SourceImages[7].Bytes, umbrellaOpen.SourceSet.SourceImages[0].Bytes);
        Assert.Equal(umbrellaOpenStrip.SourceSet.SourceImages[15].Bytes, umbrellaOpen.SourceSet.SourceImages[1].Bytes);
        Assert.Equal(12, umbrellaOpenAnim.SourceSet.SourceImages.Count);
        Assert.All(umbrellaOpenAnim.SourceSet.SourceImages, frame => Assert.True(frame.Bytes.Length > 0));
        Assert.Equal(6, umbrellaShieldBlock.SourceSet.SourceImages.Count);
        Assert.All(umbrellaShieldBlock.SourceSet.SourceImages, frame => Assert.True(frame.Bytes.Length > 0));
        Assert.Equal(35, umbrellaShieldBlock.Definition.Definition.OriginX);
        Assert.Equal(18, umbrellaShieldBlock.Definition.Definition.OriginY);
        Assert.Equal(2, umbrellaAmmoHud.SourceSet.SourceImages.Count);
        Assert.All(umbrellaAmmoHud.SourceSet.SourceImages, frame => Assert.True(frame.Bytes.Length > 0));
        Assert.Equal(2, umbrellaAbilityHud.SourceSet.SourceImages.Count);
        Assert.All(umbrellaAbilityHud.SourceSet.SourceImages, frame => Assert.True(frame.Bytes.Length > 0));
    }

    [Fact]
    public void GameplayPackSpriteAssetServiceLoadsRegisteredSprite()
    {
        var spriteDefinition = new GameplaySpriteAssetDefinition(
            "ScoutRedStandS",
            ["Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png"],
            OriginX: 30,
            OriginY: 40);
        var assetSource = new StubAssetBinarySource(new Dictionary<string, byte[]>
        {
            ["Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png"] = [5, 6, 7, 8],
        });
        var spriteAssetService = new GameplayPackSpriteAssetService("stock.gg2", assetSource);

        var loaded = spriteAssetService.LoadRegisteredSprite(spriteDefinition);

        Assert.Equal("stock.gg2", loaded.Definition.PackId);
        Assert.Equal("Content/Gameplay/stock.gg2/sprites/ScoutRedStandS.json", loaded.Definition.DefinitionPath);
        Assert.Equal("Content/Sprites/Characters/Scout/ScoutRedStandS.images/image 0.png", loaded.Definition.FirstFrameContentPath);
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

        var soldierBinding = registry.GetRequiredClassBinding(PlayerClass.Soldier);
        Assert.Equal("soldier", soldierBinding.ClassId);
        Assert.True(soldierBinding.SupportsExperimentalAcquiredWeapon);
        Assert.Equal("RocketKL", soldierBinding.PrimaryWeaponKillFeedSprite);
        Assert.True(registry.TryGetClassBinding(PlayerClass.Quote, out var civilianBinding));
        Assert.Equal("civilian", civilianBinding.ClassId);
        Assert.Equal("CivvieUmbrellaKL", civilianBinding.PrimaryWeaponKillFeedSprite);
        Assert.True(registry.TryResolveBoundPlayerClassForPrimaryItem("weapon.umbrella", out var civilianClass));
        Assert.Equal(PlayerClass.Quote, civilianClass);
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
        var tomislav = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.tomislav"));
        var brassBeast = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.brassbeast"));
        var stockRevolver = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.revolver"));
        var diamondback = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.diamondback"));
        var stockFlamethrower = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.flamethrower"));
        var stockBlade = registry.CreatePrimaryWeaponDefinition(registry.GetRequiredItem("weapon.blade"));

        Assert.NotNull(stockRocketLauncher.RocketCombat);
        Assert.Equal(RocketProjectileEntity.DirectHitDamage, stockRocketLauncher.RocketCombat!.DirectHitDamage);
        Assert.Equal(RocketProjectileEntity.ExplosionDamage, stockRocketLauncher.RocketCombat.ExplosionDamage);
        Assert.Equal("RocketSnd", stockRocketLauncher.FireSoundName);
        Assert.Equal(15f, blackBox.DirectHitHealAmount);

        Assert.Equal(ShotProjectileEntity.DamagePerHit, stockMinigun.DirectHitDamage);
        Assert.Equal("ChaingunSnd", stockMinigun.FireSoundName);
        AssertHeavyBulletPlayerEffects(stockMinigun);
        AssertHeavyBulletPlayerEffects(tomislav);
        AssertHeavyBulletPlayerEffects(brassBeast);
        Assert.Equal(10f, brassBeast.DirectHitDamage);

        Assert.Equal(RevolverProjectileEntity.DamagePerHit, stockRevolver.DirectHitDamage);
        Assert.Equal("RevolverSnd", stockRevolver.FireSoundName);
        Assert.Equal(24f, diamondback.DirectHitDamage);
        Assert.Equal(21f, diamondback.MinShotSpeed);

        Assert.Equal(FlameProjectileEntity.DirectHitDamage, stockFlamethrower.DirectHitDamage);
        Assert.Equal(FlameProjectileEntity.BurnDamagePerTick, stockFlamethrower.DamagePerTick);
        Assert.Equal(PlayerEntity.QuoteBubbleLimit, stockBlade.ActiveProjectileLimit);

        static void AssertHeavyBulletPlayerEffects(PrimaryWeaponDefinition weapon)
        {
            Assert.Equal(1.05f, weapon.PlayerKnockbackScale);
            Assert.Equal(0.97f, weapon.PlayerSlowMovementMultiplier);
            Assert.Equal(6, weapon.PlayerSlowRefreshSourceTicks);
        }
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
                    IsUsingBinoculars: false,
                    BinocularsFocusX: 0f,
                    BinocularsFocusY: 0f,
                    FacingDirectionX: 1f,
                    AimDirectionDegrees: 0f,
                    IsTaunting: false,
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
