using OpenGarrison.Core;
using OpenGarrison.Client;
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
    public void StockGameplayPackLoadsFromJsonDirectory()
    {
        var pack = StockGameplayModCatalog.Definition;

        Assert.Equal("stock.gg2", pack.Id);
        Assert.Equal("Stock OpenGarrison Gameplay", pack.DisplayName);
        Assert.Equal(2, pack.SchemaVersion);
        Assert.True(pack.Items.ContainsKey("weapon.scattergun"));
        Assert.True(pack.Items.ContainsKey("weapon.directhit"));
        Assert.True(pack.Items.ContainsKey("weapon.umbrella"));
        Assert.True(pack.Items.ContainsKey("ability.umbrella"));
        Assert.True(pack.Items.ContainsKey("ability.civilian-taunt"));
        Assert.True(pack.Items.ContainsKey("ability.civilian-pogo"));
        Assert.True(pack.Classes.ContainsKey("soldier"));
        Assert.True(pack.Classes.ContainsKey("civilian"));
        Assert.True(pack.Classes.ContainsKey("quote"));
        Assert.Equal("soldier.stock", pack.Classes["soldier"].DefaultLoadoutId);
        var civilianClass = pack.Classes["civilian"];
        Assert.Equal("Civilian/Employer", civilianClass.DisplayName);
        Assert.Equal("civilian.stock", civilianClass.DefaultLoadoutId);
        var civilianLoadout = civilianClass.Loadouts["civilian.stock"];
        Assert.Equal("weapon.umbrella", civilianLoadout.Primary!.DefaultItemId);
        Assert.Null(civilianLoadout.Secondary);
        Assert.DoesNotContain("ability.umbrella", civilianLoadout.Abilities);
        Assert.Contains("ability.umbrella", pack.Items["weapon.umbrella"].GrantedAbilityItemIds);
        Assert.Contains("ability.civilian-pogo", civilianLoadout.Abilities);
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
        var quoteClass = pack.Classes["quote"];
        Assert.Equal("Crewmate", quoteClass.DisplayName);
        Assert.Equal("Quote", quoteClass.Runtime?.BasePlayerClass);
        Assert.True(string.IsNullOrEmpty(quoteClass.Runtime?.PlayerClass));
        Assert.Equal("Impostor", quoteClass.Presentation?.SpritePrefix);
        Assert.Equal(11, quoteClass.Movement.TauntLengthFrames);
        Assert.Equal("weapon.blade", quoteClass.Loadouts[quoteClass.DefaultLoadoutId].Primary!.DefaultItemId);
        Assert.Null(quoteClass.Loadouts[quoteClass.DefaultLoadoutId].Secondary);
        Assert.Contains("ability.quote-blade-throw", quoteClass.Loadouts[quoteClass.DefaultLoadoutId].Abilities);
        Assert.Contains("ability.quote-taunt", quoteClass.Loadouts[quoteClass.DefaultLoadoutId].Abilities);
        Assert.Equal(30, pack.Items["ability.quote-taunt"].Ability!.Parameters["healAmount"].GetInt32());
        Assert.True(pack.Items["ability.quote-taunt"].Ability!.Parameters["healSelfOnly"].GetBoolean());
        Assert.True(pack.Items["ability.quote-taunt"].Ability!.Parameters["moneyBurst"].GetBoolean());
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
        Assert.Equal("assets/characters/scout/ScoutRedStandS.images/image 0.png", scoutStandSprite.FramePaths[0]);
        Assert.Equal(30, scoutStandSprite.OriginX);
        Assert.Equal(40, scoutStandSprite.OriginY);
        Assert.NotNull(scoutStandSprite.Mask);
        Assert.Equal("RECTANGLE", scoutStandSprite.Mask!.Shape);
        Assert.Equal("MANUAL", scoutStandSprite.Mask.BoundsMode);
        Assert.Equal(24, scoutStandSprite.Mask.Left);
        Assert.Equal(63, scoutStandSprite.Mask.Bottom);
        Assert.True(pack.Assets.Sprites.ContainsKey("gg2FontS"));
        var fontSprite = pack.Assets.Sprites["gg2FontS"];
        Assert.Equal("assets/shared/gg2FontS.images/image 0.png", fontSprite.FramePaths[0]);
        Assert.Equal("assets/shared/gg2FontS.images/image 10.png", fontSprite.FramePaths[10]);
        Assert.NotNull(fontSprite.Mask);
        Assert.Equal("MANUAL", fontSprite.Mask!.BoundsMode);
        Assert.True(pack.Assets.Sprites.ContainsKey("IntelTimerS"));
        var intelTimerSprite = pack.Assets.Sprites["IntelTimerS"];
        Assert.Equal(24, intelTimerSprite.FramePaths.Count);
        Assert.Equal("assets/world/in-game-elements/IntelTimerS.images/image 23.png", intelTimerSprite.FramePaths[23]);
        Assert.Equal(5, intelTimerSprite.OriginX);
        Assert.True(pack.Assets.Sprites.ContainsKey("RocketlauncherFRS"));
        var reloadSprite = pack.Assets.Sprites["RocketlauncherFRS"];
        Assert.Equal(24, reloadSprite.FramePaths.Count);
        Assert.Equal("assets/weapons/reloading/RocketlauncherFRS.images/image 23.png", reloadSprite.FramePaths[23]);
        Assert.NotNull(reloadSprite.Mask);
        Assert.Equal("PRECISE", reloadSprite.Mask!.Shape);
        Assert.True(pack.Assets.Sprites.ContainsKey("stock.gg2.weapon.directhit.world"));
        Assert.Equal(2, pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths.Count);
        Assert.Equal("assets/weapons/variants/directhit/DirectHit.red.png", pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths[0]);
        Assert.Equal("assets/weapons/variants/directhit/DirectHit.blue.png", pack.Assets.Sprites["stock.gg2.weapon.directhit.world"].FramePaths[1]);
        Assert.Equal(2, pack.Assets.Sprites["stock.gg2.weapon.directhit.recoil"].FramePaths.Count);
        Assert.Equal(50, pack.Assets.Sprites["stock.gg2.weapon.directhit.hud"].FrameWidth);
        Assert.True(pack.Assets.Sprites.ContainsKey("MvpRedMedicS"));
        var redMedicMvpSprite = pack.Assets.Sprites["MvpRedMedicS"];
        Assert.Equal(4, redMedicMvpSprite.FramePaths.Count);
        Assert.Equal("assets/hud/mvp/red/medic/nonwinner_0.png", redMedicMvpSprite.FramePaths[0]);
        Assert.Equal("assets/hud/mvp/red/medic/nonwinner_3.png", redMedicMvpSprite.FramePaths[3]);
        Assert.Equal(26, redMedicMvpSprite.OriginX);
        Assert.Equal(52, redMedicMvpSprite.OriginY);
        Assert.True(pack.Assets.Sprites.ContainsKey("MvpRedHeavyWinnerS"));
        var redHeavyWinnerMvpSprite = pack.Assets.Sprites["MvpRedHeavyWinnerS"];
        Assert.Single(redHeavyWinnerMvpSprite.FramePaths);
        Assert.Equal("assets/hud/mvp/red/heavy/winner_0.png", redHeavyWinnerMvpSprite.FramePaths[0]);
        Assert.Equal(26, redHeavyWinnerMvpSprite.OriginX);
        Assert.Equal(52, redHeavyWinnerMvpSprite.OriginY);

        var scattergun = pack.Items["weapon.scattergun"];
        Assert.Equal(GameplayItemKind.Weapon, scattergun.Kind);
        Assert.Equal(GameplayWeaponSlot.Primary, scattergun.WeaponSlot);
        Assert.Equal(4f, scattergun.Combat?.PlayerKnockback?.ImpulsePerUse);
        Assert.Equal(0.5f, scattergun.Combat?.PlayerKnockback?.AirborneVerticalScale);
        Assert.Equal(0.5f, scattergun.Combat?.PlayerKnockback?.GroundedVerticalScale);

        var flamethrowerReach = pack.Items["weapon.flamethrower"].Combat?.AirborneVelocityReach;
        Assert.NotNull(flamethrowerReach);
        Assert.Equal("classRunJump", flamethrowerReach!.Baseline);
        Assert.Equal(0.5f, flamethrowerReach.BonusPerExcessBaseline);
        Assert.Equal(1.5f, flamethrowerReach.MaxReachMultiplier);

        var scoutNailgun = pack.Items["weapon.scout-nailgun"];
        Assert.Equal(GameplayItemKind.Weapon, scoutNailgun.Kind);
        Assert.Equal(GameplayWeaponSlot.Primary, scoutNailgun.WeaponSlot);
        Assert.Equal(5, scoutNailgun.Ammo.UseDelaySourceTicks);

        var engineerPistol = pack.Items["weapon.engineer-pistol"];
        Assert.Equal(GameplayWeaponSlot.Secondary, engineerPistol.WeaponSlot);
        Assert.Equal(22, engineerPistol.Ammo.MaxAmmo);

        var pyroAirblast = pack.Items["ability.pyro-airblast"];
        Assert.Equal(GameplayItemKind.Ability, pyroAirblast.Kind);
        Assert.Null(pyroAirblast.WeaponSlot);
        Assert.Equal(GameplayAbilityConstants.WeaponAltFireCategory, pyroAirblast.Ability?.Category);
        Assert.Equal(GameplayAbilityConstants.SpecialChannel, pyroAirblast.Ability?.Channel);
        Assert.Contains("ability.pyro-airblast", pack.Items["weapon.flamethrower"].GrantedAbilityItemIds);

        var scoutStock = pack.Classes["scout"].Loadouts["scout.stock"];
        Assert.NotNull(scoutStock.Primary);
        Assert.Equal("weapon.scattergun", scoutStock.Primary!.DefaultItemId);
        Assert.Equal(["weapon.scattergun", "weapon.scout-nailgun"], scoutStock.Primary.ItemIds);
        Assert.Equal("weapon.scout-pistol", scoutStock.Secondary?.ItemId);
        Assert.DoesNotContain("ability.scout-nailgun-utility", scoutStock.Abilities);

        var scoutPistol = pack.Items["weapon.scout-pistol"];
        Assert.Equal(GameplayWeaponSlot.Secondary, scoutPistol.WeaponSlot);
        Assert.Equal("PistolKL", scoutPistol.Combat?.KillFeedSpriteName);
        Assert.Equal(8f, scoutPistol.Combat?.DirectHitDamage);

        var soldierStock = pack.Classes["soldier"].Loadouts["soldier.stock"];
        Assert.Equal("weapon.soldier-shotgun", soldierStock.Secondary?.ItemId);
        Assert.Equal(4, pack.Items["weapon.soldier-shotgun"].Ammo.MaxAmmo);
        Assert.DoesNotContain("ability.soldier-utility", soldierStock.Abilities);
        Assert.Contains(
            "ability.experimental-ltd-soldier-secondary",
            pack.Items["weapon.soldier-shotgun"].GrantedAbilityItemIds);

        var heavyStock = pack.Classes["heavy"].Loadouts["heavy.stock"];
        Assert.Equal("weapon.heavy-shotgun", heavyStock.Secondary?.ItemId);
        Assert.Equal(4, pack.Items["weapon.heavy-shotgun"].Ammo.MaxAmmo);

        var buffBanner = pack.Items["ability.soldier-buff-banner"].Ability;
        Assert.NotNull(buffBanner);
        Assert.Equal(400, buffBanner!.Parameters["maxChargeDamage"].GetInt32());
        Assert.Equal(5f, buffBanner.Parameters["healthRegenPerSecond"].GetSingle());

        var demomanStock = pack.Classes["demoman"].Loadouts["demoman.stock"];
        Assert.Equal("weapon.grenadelauncher", demomanStock.Secondary?.ItemId);
        Assert.Contains("ability.demoman-detonate", demomanStock.Abilities);
    }

    [Fact]
    public void StockQuoteCurlyHasItsOwnIdentityWhileLegacyQuoteStillBindsCivilian()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();

        Assert.True(registry.TryGetClassBinding("quote", out var quoteBinding));
        Assert.Equal("quote", quoteBinding.ClassId);
        Assert.Equal(PlayerClass.Quote, quoteBinding.PlayerClass);
        Assert.Equal(PlayerClass.Quote, quoteBinding.BasePlayerClass);
        Assert.False(quoteBinding.BindsLegacyPlayerClass);
        Assert.True(registry.TryGetClassBinding(PlayerClass.Quote, out var legacyBinding));
        Assert.Equal("civilian", legacyBinding.ClassId);
        Assert.Equal("Crewmate", registry.GetClassDefinition("quote").DisplayName);
        Assert.Equal("BladeKL", CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite("quote"));
        Assert.Equal("CivvieUmbrellaKL", CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(PlayerClass.Quote));
        Assert.Equal("civilian", Game1.ResolveClassSelectCivilianShortcutGameplayClassId());
        Assert.Equal("quote", Game1.ResolveClassSelectRandomDoorGameplayClassId());
    }

    [Fact]
    public void StockQuoteCurlyHasBubbleAndBladeActionsWithoutCivilianAbilities()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("quote");

        var player = world.LocalPlayer;
        Assert.Equal(PlayerClass.Quote, player.ClassId);
        Assert.Equal("quote", player.GameplayClassId);
        Assert.False(player.IsCivilian);
        Assert.True(player.IsQuoteCurly);
        Assert.Equal("weapon.blade", player.GameplayLoadoutState.PrimaryItemId);
        Assert.Null(player.GameplayLoadoutState.SecondaryItemId);
        Assert.Null(player.GameplayLoadoutState.UtilityItemId);
        Assert.Contains("ability.quote-blade-throw", player.GameplayLoadoutState.AbilityItemIds ?? []);
        Assert.Contains("ability.quote-taunt", player.GameplayLoadoutState.AbilityItemIds ?? []);
        Assert.DoesNotContain("ability.quote-utility", player.GameplayLoadoutState.AbilityItemIds ?? []);
        Assert.Equal(BuiltInGameplayBehaviorIds.Blade, player.PrimaryBehaviorId);
        Assert.Equal(BuiltInGameplayBehaviorIds.QuoteBladeThrow, player.SpecialAbilityBehaviorId);

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = player.X + 96f,
            AimWorldY = player.Y,
        });
        world.AdvanceOneTick();

        Assert.Single(world.Bubbles);
        Assert.Empty(world.Blades);
        Assert.DoesNotContain(
            GameplayAbilityReplicatedState.CreateEntries(player),
            entry => entry.Key.StartsWith("civvie_", StringComparison.Ordinal));
        player.BeginPendingCivvieTauntHeal();
        Assert.False(player.CivvieTauntHealPending);
        Assert.False(CivvieMoneyTrailRules.IsEligibleTrailSource(player));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(40, 30)]
    [InlineData(20, 20)]
    public void CrewmateUseAbilityTauntHealsSelfAndEmitsMoneyBurst(int missingHealth, int expectedHealing)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("quote");
        var player = world.LocalPlayer;
        var startingHealth = player.MaxHealth - missingHealth;
        player.ForceSetHealth(startingHealth);

        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(2, PlayerTeam.Red));
        Assert.True(world.NetworkPlayers.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(2, out var nearbyAlly));
        nearbyAlly.TeleportTo(player.X + 24f, player.Y);
        nearbyAlly.ForceSetHealth(50);

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();

        Assert.True(player.IsTaunting);
        Assert.True(player.CivvieTauntHealPending);
        Assert.Equal(startingHealth, player.Health);
        for (var tick = 0; tick < 40 && player.CivvieTauntHealPending; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(player.CivvieTauntHealPending);
        Assert.Equal(Math.Min(player.MaxHealth, startingHealth + expectedHealing), player.Health);
        Assert.Equal(50, nearbyAlly.Health);
        var burst = Assert.Single(world.PendingVisualEvents.Where(static e => e.EffectName == "CivvieMoneyBurst"));
        Assert.Equal(CivvieMoneyTrailRules.PogoTrickBurstParticleCount, burst.Count);
    }

    [Fact]
    public void CrewmateUseAbilityRequiresAReleaseAndFreshPressAfterTauntCooldown()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("quote");
        var player = world.LocalPlayer;

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();
        Assert.True(player.IsTaunting);
        for (var tick = 0; tick < 40 && player.IsTaunting; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(player.IsTaunting);
        Assert.True(player.TauntRestartCooldownTicksRemaining > 0);
        world.NetworkPlayers.SetLocalInput(default);
        world.AdvanceOneTick();
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();
        Assert.False(player.IsTaunting);

        for (var tick = 0; tick < 40; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(player.IsTaunting);
        Assert.Single(world.PendingVisualEvents.Where(static e => e.EffectName == "CivvieMoneyBurst"));
        world.NetworkPlayers.SetLocalInput(default);
        world.AdvanceOneTick();
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();
        Assert.True(player.IsTaunting);
    }

    [Fact]
    public void CrewmatePendingTauntEffectClearsOnDeath()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("quote");
        var player = world.LocalPlayer;

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();
        Assert.True(player.CivvieTauntHealPending);
        Assert.Equal("ability.quote-taunt", player.CivvieTauntHealAbilityItemId);

        player.Kill();

        Assert.False(player.CivvieTauntHealPending);
        Assert.Null(player.CivvieTauntHealAbilityItemId);
    }

    [Fact]
    public void CrewmateUseAbilityTauntIsBlockedWhenSecondaryAbilitiesAreDisabled()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("quote");
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(EnableSecondaryAbilities: false));

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();

        Assert.False(world.LocalPlayer.IsTaunting);
        Assert.False(world.LocalPlayer.CivvieTauntHealPending);
        Assert.DoesNotContain(world.PendingVisualEvents, static e => e.EffectName == "CivvieMoneyBurst");
    }

    [Fact]
    public void CivilianTauntKeepsFifteenPointHealingWithoutCrewMoneyBurst()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("civilian");
        var player = world.LocalPlayer;
        var startingHealth = player.MaxHealth - 30;
        player.ForceSetHealth(startingHealth);

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { Taunt = true });
        world.AdvanceOneTick();
        Assert.True(player.CivvieTauntHealPending);
        for (var tick = 0; tick < 40 && player.CivvieTauntHealPending; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(startingHealth + 15, player.Health);
        Assert.DoesNotContain(world.PendingVisualEvents, static e => e.EffectName == "CivvieMoneyBurst");
    }

    [Fact]
    public void StockQuoteCurlyCanThrowItsBladeAndRecordsBladeKillFeedSprite()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("quote");
        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.NetworkPlayers.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(2, out var target));

        var player = world.LocalPlayer;
        target.TeleportTo(player.X + 90f, player.Y);
        // Let both players settle on the floor so the throw is aimed at where the target actually stands.
        for (var tick = 0; tick < 30; tick += 1)
        {
            world.AdvanceOneTick();
        }

        target.ForceSetHealth(1);
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with
        {
            FireSecondary = true,
            AimWorldX = target.X,
            AimWorldY = target.Y,
        });
        world.AdvanceOneTick();
        Assert.Single(world.Blades);

        world.NetworkPlayers.SetLocalInput(default);
        // An unattended network slot respawns immediately, so the kill feed (not IsAlive) records the kill.
        for (var tick = 0; tick < 20 && world.KillFeedEntries.Count == 0; tick += 1)
        {
            world.AdvanceOneTick();
        }

        var killFeedEntry = Assert.Single(world.KillFeedEntries);
        Assert.Equal(player.Id, killFeedEntry.KillerPlayerId);
        Assert.Equal("BladeKL", killFeedEntry.WeaponSpriteName);
    }

    [Fact]
    public void GameplaySchemaV2SeparatesPrimarySecondaryAndAbilities()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var packDirectory = Path.Combine(rootDirectory, "schema-v2");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "classes"));

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "pack.json"),
                """
                {
                  "id": "schema.v2",
                  "displayName": "Schema V2",
                  "version": "1.0.0",
                  "schemaVersion": 2
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.primary-a.json"),
                """
                {
                  "id": "weapon.primary-a",
                  "displayName": "Primary A",
                  "kind": "Weapon",
                  "weaponSlot": "Primary",
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": { "maxAmmo": 6 },
                  "presentation": {}
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.primary-b.json"),
                """
                {
                  "id": "weapon.primary-b",
                  "displayName": "Primary B",
                  "kind": "Weapon",
                  "weaponSlot": "Primary",
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": { "maxAmmo": 8 },
                  "presentation": {}
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.secondary.json"),
                """
                {
                  "id": "weapon.secondary",
                  "displayName": "Secondary",
                  "kind": "Weapon",
                  "weaponSlot": "Secondary",
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": { "maxAmmo": 4 },
                  "presentation": {}
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.special.json"),
                """
                {
                  "id": "ability.special",
                  "displayName": "Special",
                  "kind": "Ability",
                  "behaviorId": "builtin.ability.pyro_airblast",
                  "ammo": {},
                  "presentation": {},
                  "ability": {
                    "channel": "special",
                    "category": "secondary",
                    "activation": "pressed",
                    "executorId": "builtin.ability.pyro_airblast"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "classes", "tester.json"),
                """
                {
                  "id": "tester",
                  "displayName": "Tester",
                  "movement": {},
                  "loadouts": {
                    "tester.stock": {
                      "id": "tester.stock",
                      "displayName": "Stock",
                      "primary": {
                        "defaultItemId": "weapon.primary-a",
                        "itemIds": [ "weapon.primary-a", "weapon.primary-b" ],
                        "switchPolicy": "primary_swap_station",
                        "selectionPersistence": "same_class_loadout"
                      },
                      "secondary": { "itemId": "weapon.secondary" },
                      "abilities": [ "ability.special" ]
                    }
                  },
                  "defaultLoadoutId": "tester.stock"
                }
                """);

            var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);
            var loadout = pack.Classes["tester"].Loadouts["tester.stock"];

            Assert.Equal(2, pack.SchemaVersion);
            Assert.Equal("weapon.primary-a", loadout.Primary?.DefaultItemId);
            Assert.Equal(["weapon.primary-a", "weapon.primary-b"], loadout.Primary?.ItemIds);
            Assert.Equal("weapon.secondary", loadout.Secondary?.ItemId);
            Assert.Equal(["ability.special"], loadout.Abilities);

            Assert.Equal("weapon.primary-a", loadout.PrimaryItemId);
            Assert.Equal("weapon.secondary", loadout.SecondaryItemId);
            Assert.Null(loadout.UtilityItemId);
            Assert.Equal(["ability.special"], loadout.AbilityItemIds);
            Assert.Equal(GameplayEquipmentSlot.Secondary, pack.Items["ability.special"].Slot);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("future_switch_policy", GameplayLoadoutPolicies.SameClassLoadout, "switchPolicy")]
    [InlineData(GameplayLoadoutPolicies.PrimarySwapStation, "future_persistence_policy", "selectionPersistence")]
    public void GameplaySchemaV2RejectsUnsupportedPrimaryPolicies(
        string switchPolicy,
        string selectionPersistence,
        string expectedFieldName)
    {
        var (rootDirectory, packDirectory) = CreateMinimalSchemaV2ValidationPack();

        try
        {
            WriteMinimalSchemaV2Class(packDirectory, switchPolicy, selectionPersistence);

            var ex = Assert.Throws<InvalidOperationException>(
                () => GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory));

            Assert.Contains(expectedFieldName, ex.Message, StringComparison.Ordinal);
            Assert.Contains("unsupported", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void GameplaySchemaV2RejectsConflictingLoadoutAndWeaponGrantedActiveAbilities()
    {
        var (rootDirectory, packDirectory) = CreateMinimalSchemaV2ValidationPack();

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.loadout.json"),
                """
                {
                  "id": "ability.loadout",
                  "displayName": "Loadout Special",
                  "kind": "Ability",
                  "behaviorId": "builtin.ability.pyro_airblast",
                  "ammo": {},
                  "presentation": {},
                  "ability": {
                    "channel": "special",
                    "category": "secondary",
                    "activation": "pressed",
                    "executorId": "builtin.ability.pyro_airblast"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.weapon.json"),
                """
                {
                  "id": "ability.weapon",
                  "displayName": "Weapon Special",
                  "kind": "Ability",
                  "behaviorId": "builtin.ability.medic_needlegun",
                  "ammo": {},
                  "presentation": {},
                  "ability": {
                    "channel": "special",
                    "category": "secondary",
                    "activation": "held",
                    "executorId": "builtin.ability.medic_needlegun"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.primary.json"),
                """
                {
                  "id": "weapon.primary",
                  "displayName": "Primary",
                  "kind": "Weapon",
                  "weaponSlot": "Primary",
                  "grantedAbilityItemIds": [ "ability.weapon" ],
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": { "maxAmmo": 6 },
                  "presentation": {}
                }
                """);
            WriteMinimalSchemaV2Class(
                packDirectory,
                GameplayLoadoutPolicies.PrimarySwapStation,
                GameplayLoadoutPolicies.SameClassLoadout,
                "\"abilities\": [ \"ability.loadout\" ]");

            var ex = Assert.Throws<InvalidOperationException>(
                () => GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory));

            Assert.Contains("combines", ex.Message, StringComparison.Ordinal);
            Assert.Contains("active channel \"special\"", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
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
        Assert.True(eyelander.Presentation.UseTorsoReplacement);
        Assert.Equal(ExperimentalDemoknightCatalog.EyelanderWorldSpriteName, eyelander.Presentation.WorldSpriteName);
        Assert.Equal(ExperimentalDemoknightCatalog.EyelanderRecoilSpriteName, eyelander.Presentation.RecoilSpriteName);
        Assert.Equal(ExperimentalDemoknightCatalog.EyelanderMeleeHitboxSpriteName, eyelander.Presentation.MeleeHitboxSpriteName);
        Assert.True(StockGameplayModCatalog.Definition.Assets.Sprites.ContainsKey(ExperimentalDemoknightCatalog.EyelanderMeleeHitboxSpriteName));
        Assert.True(StockGameplayModCatalog.Definition.Assets.Sprites.ContainsKey(ExperimentalDemoknightCatalog.EyelanderBlueWorldSpriteName));
        Assert.True(StockGameplayModCatalog.Definition.Assets.Sprites.ContainsKey(ExperimentalDemoknightCatalog.EyelanderBlueRecoilSpriteName));
        Assert.Equal(
            ExperimentalDemoknightCatalog.EyelanderBlueWorldSpriteName,
            ExperimentalDemoknightCatalog.ResolveTorsoReplacementTeamSpriteName(
                ExperimentalDemoknightCatalog.EyelanderWorldSpriteName,
                PlayerTeam.Blue));
        Assert.Equal(
            ExperimentalDemoknightCatalog.EyelanderBlueRecoilSpriteName,
            ExperimentalDemoknightCatalog.ResolveTorsoReplacementTeamSpriteName(
                ExperimentalDemoknightCatalog.EyelanderRecoilSpriteName,
                PlayerTeam.Blue));
        Assert.Equal(
            ExperimentalDemoknightCatalog.EyelanderWorldSpriteName,
            ExperimentalDemoknightCatalog.ResolveTorsoReplacementTeamSpriteName(
                ExperimentalDemoknightCatalog.EyelanderWorldSpriteName,
                PlayerTeam.Red));

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
        Assert.Equal(GameplayAbilityConstants.WeaponAltFireCategory, pyroAirblast!.Category);
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

        var soldierExperimentalSecondary = pack.Items["ability.experimental-ltd-soldier-secondary"].Ability;
        Assert.NotNull(soldierExperimentalSecondary);
        Assert.Equal(GameplayAbilityConstants.SecondaryCategory, soldierExperimentalSecondary!.Category);
        Assert.Equal(BuiltInGameplayBehaviorIds.ExperimentalSoldierSecondary, soldierExperimentalSecondary.ExecutorId);
        Assert.Contains("experimental_ltd", soldierExperimentalSecondary.Tags);

        var medigun = pack.Items["weapon.medigun"];
        Assert.Contains("ability.medic-uber", medigun.GrantedAbilityItemIds);
        Assert.Equal(1, medigun.Ammo.MaxAmmo);
        Assert.Equal(0, medigun.Ammo.AmmoPerUse);
        Assert.Null(medigun.Presentation.HudSpriteName);

        var kritz = pack.Items["weapon.medigun.crit"];
        Assert.Contains("ability.medic-uber", kritz.GrantedAbilityItemIds);
        Assert.Equal(1, kritz.Ammo.MaxAmmo);
        Assert.Equal(0, kritz.Ammo.AmmoPerUse);
        Assert.Null(kritz.Presentation.HudSpriteName);

        var medicStock = pack.Classes["medic"].Loadouts["medic.stock"];
        Assert.Contains("ability.medic-kritz-heal-needles", medicStock.Abilities);
        var kritzHealNeedles = pack.Items["ability.medic-kritz-heal-needles"].Ability;
        Assert.NotNull(kritzHealNeedles);
        Assert.Equal(GameplayAbilityConstants.UtilityCategory, kritzHealNeedles!.Category);
        Assert.Equal(GameplayAbilityConstants.UtilityChannel, kritzHealNeedles.Channel);
        Assert.Equal(GameplayAbilityConstants.PressedActivation, kritzHealNeedles.Activation);
        Assert.Equal(BuiltInGameplayBehaviorIds.MedicKritzHealNeedles, kritzHealNeedles.ExecutorId);
        Assert.Equal(3, kritzHealNeedles.Parameters["cooldownSeconds"].GetInt32());
        Assert.Equal(20, kritzHealNeedles.Parameters["projectileSpeed"].GetInt32());
        Assert.Equal(1, kritzHealNeedles.Parameters["spreadDegrees"].GetInt32());
        Assert.Equal(30, kritzHealNeedles.Parameters["healPerHit"].GetInt32());
        Assert.Equal(22, kritzHealNeedles.Parameters["enemyDamagePerHit"].GetInt32());
        Assert.Equal(0, pack.Items["ability.medic-kritz-heal-needles"].Ammo.MaxAmmo);
        Assert.Equal(0, pack.Items["ability.medic-kritz-heal-needles"].Ammo.AmmoPerUse);

        var kritzBeam = pack.Items["ability.medic-kritz-beam"].Ability;
        Assert.NotNull(kritzBeam);
        Assert.Equal(BuiltInGameplayBehaviorIds.MedicKritzBeam, kritzBeam!.ExecutorId);
        Assert.Equal(150, kritzBeam.Parameters["range"].GetInt32());
        Assert.Equal(1, kritzBeam.Parameters["damagePerSecond"].GetInt32());

        Assert.True(pack.Items.ContainsKey("ability.quote-blade-throw"));
        Assert.True(pack.Items.ContainsKey("ability.quote-utility"));
        Assert.True(pack.Items.ContainsKey("ability.quote-taunt"));
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

        var demomanStock = pack.Classes["demoman"].Loadouts["demoman.stock"];
        Assert.Contains("ability.experimental-ltd-rage", demomanStock.AbilityItemIds!);

        var engineerStock = pack.Classes["engineer"].Loadouts["engineer.stock"];
        Assert.Contains("ability.experimental-ltd-rage", engineerStock.AbilityItemIds!);

        var spyStock = pack.Classes["spy"].Loadouts["spy.stock"];
        var spyDiamondback = pack.Classes["spy"].Loadouts["spy.diamondback"];
        Assert.Contains("ability.experimental-ltd-rage", spyStock.AbilityItemIds!);
        Assert.Contains("ability.experimental-ltd-rage", spyDiamondback.AbilityItemIds!);

        var medicStock = pack.Classes["medic"].Loadouts["medic.stock"];
        Assert.Contains("ability.experimental-ltd-rage", medicStock.AbilityItemIds!);

        var sniperStock = pack.Classes["sniper"].Loadouts["sniper.stock"];
        Assert.Contains("ability.experimental-ltd-rage", sniperStock.AbilityItemIds!);
    }

    [Fact]
    public void GameplayAbilityConstantsLabelBuiltInAndReservedCategories()
    {
        Assert.True(GameplayAbilityConstants.IsBuiltInDispatchedCategory(GameplayAbilityConstants.WeaponAltFireCategory));
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
    public void GameplaySchemaV2RejectsWeaponAltFireAttachedDirectlyToLoadout()
    {
        var (rootDirectory, packDirectory) = CreateMinimalSchemaV2ValidationPack();

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.weapon-alt.json"),
                """
                {
                  "id": "ability.weapon-alt",
                  "displayName": "Weapon Alt Fire",
                  "kind": "Ability",
                  "behaviorId": "builtin.ability.pyro_airblast",
                  "ammo": {},
                  "presentation": {},
                  "ability": {
                    "channel": "special",
                    "category": "weaponAltFire",
                    "activation": "pressed",
                    "executorId": "builtin.ability.pyro_airblast"
                  }
                }
                """);
            WriteMinimalSchemaV2Class(
                packDirectory,
                GameplayLoadoutPolicies.PrimarySwapStation,
                GameplayLoadoutPolicies.SameClassLoadout,
                "\"abilities\": [ \"ability.weapon-alt\" ]");

            var exception = Assert.Throws<InvalidOperationException>(
                () => GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory));

            Assert.Contains("cannot attach weaponAltFire", exception.Message, StringComparison.Ordinal);
            Assert.Contains("grant it from a weapon item", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void GameplayPackLoaderRejectsWeaponAltFireOnNonSpecialChannel()
    {
        var (rootDirectory, packDirectory) = CreateMinimalSchemaV2ValidationPack();

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.weapon-alt.json"),
                """
                {
                  "id": "ability.weapon-alt",
                  "displayName": "Weapon Alt Fire",
                  "kind": "Ability",
                  "behaviorId": "builtin.ability.pyro_airblast",
                  "ammo": {},
                  "presentation": {},
                  "ability": {
                    "channel": "utility",
                    "category": "weaponAltFire",
                    "activation": "pressed",
                    "executorId": "builtin.ability.pyro_airblast"
                  }
                }
                """);

            var exception = Assert.Throws<InvalidOperationException>(
                () => GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory));

            Assert.Contains("must use channel \"special\"", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void GameplaySchemaV2AllowsEachEquippableWeaponToGrantItsOwnAltFire()
    {
        var (rootDirectory, packDirectory) = CreateMinimalSchemaV2ValidationPack();

        try
        {
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.primary-alt.json"),
                CreateWeaponAltFireAbilityJson("ability.primary-alt"));
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "ability.secondary-alt.json"),
                CreateWeaponAltFireAbilityJson("ability.secondary-alt"));
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.primary.json"),
                """
                {
                  "id": "weapon.primary",
                  "displayName": "Primary",
                  "kind": "Weapon",
                  "weaponSlot": "Primary",
                  "grantedAbilityItemIds": [ "ability.primary-alt" ],
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": { "maxAmmo": 6 },
                  "presentation": {}
                }
                """);
            File.WriteAllText(
                Path.Combine(packDirectory, "items", "weapon.secondary.json"),
                """
                {
                  "id": "weapon.secondary",
                  "displayName": "Secondary",
                  "kind": "Weapon",
                  "weaponSlot": "Secondary",
                  "grantedAbilityItemIds": [ "ability.secondary-alt" ],
                  "behaviorId": "builtin.weapon.pellet_gun",
                  "ammo": { "maxAmmo": 4 },
                  "presentation": {}
                }
                """);
            WriteMinimalSchemaV2Class(
                packDirectory,
                GameplayLoadoutPolicies.PrimarySwapStation,
                GameplayLoadoutPolicies.SameClassLoadout,
                "\"secondary\": { \"itemId\": \"weapon.secondary\" }");

            var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);

            Assert.Contains("ability.primary-alt", pack.Items["weapon.primary"].GrantedAbilityItemIds);
            Assert.Contains("ability.secondary-alt", pack.Items["weapon.secondary"].GrantedAbilityItemIds);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void RuntimeWeaponRegistrationCarriesGrantedWeaponAltFireItems()
    {
        var registry = new GameplayRuntimeRegistry();
        registry.RegisterPrimaryWeaponBehavior("tests.weapon", PrimaryWeaponKind.Custom);
        Assert.False(
            registry.TryRegisterGameplayAbility(
                new GameplayAbilityRegistration(
                    "ability.tests-invalid-alt",
                    "Invalid Tests Alt Fire",
                    GameplayEquipmentSlot.Secondary,
                    "tests.invalid-alt-fire",
                    new GameplayAbilityDefinition(
                        GameplayAbilityConstants.WeaponAltFireCategory,
                        GameplayAbilityConstants.PressedActivation,
                        "tests.invalid-alt-fire",
                        Channel: GameplayAbilityConstants.UtilityChannel)),
                out var invalidAbilityError));
        Assert.Contains("must use channel \"special\"", invalidAbilityError, StringComparison.Ordinal);

        Assert.True(
            registry.TryRegisterGameplayAbility(
                new GameplayAbilityRegistration(
                    "ability.tests-alt",
                    "Tests Alt Fire",
                    GameplayEquipmentSlot.Secondary,
                    "tests.alt-fire",
                    new GameplayAbilityDefinition(
                        GameplayAbilityConstants.WeaponAltFireCategory,
                        GameplayAbilityConstants.PressedActivation,
                        "tests.alt-fire",
                        Channel: GameplayAbilityConstants.SpecialChannel)),
                out var abilityError),
            abilityError);
        Assert.True(
            registry.TryRegisterGameplayWeaponItem(
                new GameplayWeaponItemRegistration(
                    "weapon.tests",
                    "Tests Weapon",
                    GameplayEquipmentSlot.Primary,
                    "tests.weapon",
                    new GameplayItemAmmoDefinition())
                {
                    GrantedAbilityItemIds = ["ability.tests-alt"],
                },
                out var weaponError),
            weaponError);

        Assert.True(registry.TryGetItem("weapon.tests", out var weapon));
        Assert.Equal(["ability.tests-alt"], weapon.GrantedAbilityItemIds);
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
    public void RuntimeRegistryRegistersDiscoveredPacksWithNestedDefinitions()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var gameplayRootDirectory = Path.Combine(rootDirectory, "Gameplay");
        var packDirectory = Path.Combine(gameplayRootDirectory, "example.test");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items", "weapons"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "classes", "playable"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "sprites", "weapons"));
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
                Path.Combine(packDirectory, "items", "weapons", "weapon.test-shotgun.json"),
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
                Path.Combine(packDirectory, "sprites", "weapons", "weapon.test-shotgun.world.json"),
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
                Path.Combine(packDirectory, "classes", "playable", "tester.json"),
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
        Assert.Equal("Crewmate", gameplayClass!.DisplayName);
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
            "plugin.quote-curly.ability.taunt",
            gameplayClass.Loadouts[gameplayClass.DefaultLoadoutId].UtilityItemId);
        Assert.True(pack.Items.ContainsKey("plugin.quote-curly.ability.taunt"));
        var crewTaunt = pack.Items["plugin.quote-curly.ability.taunt"].Ability;
        Assert.NotNull(crewTaunt);
        Assert.Equal(30, crewTaunt!.Parameters["healAmount"].GetInt32());
        Assert.True(crewTaunt.Parameters["healSelfOnly"].GetBoolean());
        Assert.True(crewTaunt.Parameters["moneyBurst"].GetBoolean());
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
        Assert.Equal("Civilian/Employer", registry.GetClassDefinition(PlayerClass.Quote).DisplayName);
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
        Assert.Equal("Civilian/Employer", registry.GetClassDefinition(PlayerClass.Quote).DisplayName);
        Assert.Equal("weapon.umbrella", registry.GetDefaultLoadout(PlayerClass.Quote).PrimaryItemId);
        Assert.Null(registry.GetDefaultLoadout(PlayerClass.Quote).SecondaryItemId);
        Assert.Null(registry.GetDefaultLoadout(PlayerClass.Quote).UtilityItemId);
        Assert.DoesNotContain("ability.umbrella", registry.GetDefaultLoadout(PlayerClass.Quote).Abilities);
        Assert.Contains("ability.umbrella", registry.GetRequiredItem("weapon.umbrella").GrantedAbilityItemIds);
        Assert.Contains("ability.civilian-pogo", registry.GetDefaultLoadout(PlayerClass.Quote).Abilities);
        Assert.Equal("Crewmate", registry.GetClassDefinition("plugin.quote-curly.quote").DisplayName);
        Assert.Equal("plugin.quote-curly.weapon.blade", registry.GetDefaultLoadout("plugin.quote-curly.quote").PrimaryItemId);
    }

    [Fact]
    public void QuoteCurlyUsesBubbleOnPrimaryAndDamageBladeAsSpecialAbility()
    {
        EnsureQuoteCurlyGameplayPackRegistered();

        var world = new SimulationWorld();
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("plugin.quote-curly.quote");

        var player = world.LocalPlayer;
        Assert.Equal(PlayerClass.Quote, player.ClassId);
        Assert.Equal("plugin.quote-curly.quote", player.GameplayClassId);
        Assert.Equal("plugin.quote-curly.weapon.blade", player.GameplayLoadoutState.PrimaryItemId);
        Assert.Null(player.GameplayLoadoutState.SecondaryItemId);
        Assert.Contains("plugin.quote-curly.ability.blade-throw", player.GameplayLoadoutState.AbilityItemIds ?? []);
        Assert.Equal(BuiltInGameplayBehaviorIds.Blade, player.PrimaryBehaviorId);
        Assert.Equal(BuiltInGameplayBehaviorIds.QuoteBladeThrow, player.SpecialAbilityBehaviorId);

        world.NetworkPlayers.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: player.X + 96f,
            AimWorldY: player.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        Assert.Single(world.Bubbles);
        Assert.Empty(world.Blades);

        world.NetworkPlayers.SetLocalInput(default);
        world.AdvanceOneTick();
        for (var tick = 0; tick < 10 && player.PrimaryCooldownTicks > 0; tick += 1)
        {
            world.AdvanceOneTick();
        }

        world.NetworkPlayers.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: player.X + 96f,
            AimWorldY: player.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        Assert.Single(world.Blades);
        Assert.Equal(1, player.QuoteBladesOut);
    }

    [Fact]
    public void PackagedCrewmateUseAbilityTauntUsesTheSameSelfHealAndMoneyBurst()
    {
        EnsureQuoteCurlyGameplayPackRegistered();

        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin("plugin.quote-curly.quote");
        var player = world.LocalPlayer;
        var startingHealth = player.MaxHealth - 40;
        player.ForceSetHealth(startingHealth);

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { UseAbility = true });
        world.AdvanceOneTick();
        Assert.True(player.CivvieTauntHealPending);

        for (var tick = 0; tick < 40 && player.CivvieTauntHealPending; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(startingHealth + 30, player.Health);
        Assert.Single(world.PendingVisualEvents.Where(static e => e.EffectName == "CivvieMoneyBurst"));
    }

    private static void EnsureQuoteCurlyGameplayPackRegistered()
    {
        if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding("plugin.quote-curly.quote", out _))
        {
            return;
        }

        var packDirectory = ProjectSourceLocator.FindDirectory(Path.Combine(
            "Plugins",
            "Packaged",
            "Server",
            "Lua.QuoteCurly",
            "Gameplay",
            "quote-curly.gg2"));
        Assert.False(string.IsNullOrWhiteSpace(packDirectory));

        var pack = GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory!);
        Assert.True(
            CharacterClassCatalog.RuntimeRegistry.TryRegisterModPack(pack, allowRuntimeClassBindingOverride: false, out var error),
            error);
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
        Assert.Equal(new AirborneVelocityReachDefinition(0.5f, 1.5f), stockFlamethrower.AirborneVelocityReach);
        Assert.Equal(new PlayerKnockbackDefinition(4f, 0.5f, 0.5f), CharacterClassCatalog.Scattergun.PlayerKnockback);
        Assert.Equal(new PlayerKnockbackDefinition(2.5f, 0.5f, 0.5f), stockRevolver.PlayerKnockback);
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

    private static (string RootDirectory, string PackDirectory) CreateMinimalSchemaV2ValidationPack()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "og2-gameplay-pack-tests", Path.GetRandomFileName());
        var packDirectory = Path.Combine(rootDirectory, "schema-v2-validation");
        Directory.CreateDirectory(Path.Combine(packDirectory, "items"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "classes"));
        File.WriteAllText(
            Path.Combine(packDirectory, "pack.json"),
            """
            {
              "id": "schema.v2.validation",
              "displayName": "Schema V2 Validation",
              "version": "1.0.0",
              "schemaVersion": 2
            }
            """);
        File.WriteAllText(
            Path.Combine(packDirectory, "items", "weapon.primary.json"),
            """
            {
              "id": "weapon.primary",
              "displayName": "Primary",
              "kind": "Weapon",
              "weaponSlot": "Primary",
              "behaviorId": "builtin.weapon.pellet_gun",
              "ammo": { "maxAmmo": 6 },
              "presentation": {}
            }
            """);
        WriteMinimalSchemaV2Class(
            packDirectory,
            GameplayLoadoutPolicies.PrimarySwapStation,
            GameplayLoadoutPolicies.SameClassLoadout);
        return (rootDirectory, packDirectory);
    }

    private static string CreateWeaponAltFireAbilityJson(string itemId)
    {
        return $$"""
            {
              "id": "{{itemId}}",
              "displayName": "Weapon Alt Fire",
              "kind": "Ability",
              "behaviorId": "builtin.ability.pyro_airblast",
              "ammo": {},
              "presentation": {},
              "ability": {
                "channel": "special",
                "category": "weaponAltFire",
                "activation": "pressed",
                "executorId": "builtin.ability.pyro_airblast"
              }
            }
            """;
    }

    private static void WriteMinimalSchemaV2Class(
        string packDirectory,
        string switchPolicy,
        string selectionPersistence,
        string additionalLoadoutProperty = "")
    {
        const string template =
            """
            {
              "id": "tester",
              "displayName": "Tester",
              "movement": {},
              "loadouts": {
                "tester.stock": {
                  "id": "tester.stock",
                  "displayName": "Stock",
                  "primary": {
                    "defaultItemId": "weapon.primary",
                    "itemIds": [ "weapon.primary" ],
                    "switchPolicy": "$SWITCH_POLICY$",
                    "selectionPersistence": "$SELECTION_PERSISTENCE$"
                  }$ADDITIONAL_LOADOUT_PROPERTY$
                }
              },
              "defaultLoadoutId": "tester.stock"
            }
            """;
        var additionalProperty = string.IsNullOrWhiteSpace(additionalLoadoutProperty)
            ? string.Empty
            : ",\n      " + additionalLoadoutProperty;
        var document = template
            .Replace("$SWITCH_POLICY$", switchPolicy, StringComparison.Ordinal)
            .Replace("$SELECTION_PERSISTENCE$", selectionPersistence, StringComparison.Ordinal)
            .Replace("$ADDITIONAL_LOADOUT_PROPERTY$", additionalProperty, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(packDirectory, "classes", "tester.json"), document);
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
