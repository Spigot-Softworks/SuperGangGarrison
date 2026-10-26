using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class CustomPlayerClassSlotTests
{
    private const string ModPackId = "plugin.test.custom-slot";
    private const string BrawlerClassId = "plugin.test.custom-slot.brawler";
    private const string NinjaClassId = "plugin.test.custom-slot.ninja";

    [Fact]
    public void CustomSlotClassesRegisterWithoutLegacyBindingAndDefaultToScoutBotGraph()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();

        Assert.True(registry.TryRegisterModPack(CreateModPack(), allowRuntimeClassBindingOverride: false, out var error), error);

        Assert.True(registry.TryGetClassBinding(BrawlerClassId, out var brawler));
        Assert.True(registry.TryGetClassBinding(NinjaClassId, out var ninja));
        Assert.False(brawler.BindsLegacyPlayerClass);
        Assert.Equal(PlayerClass.Custom, brawler.PlayerClass);
        Assert.Equal(PlayerClass.Custom, ninja.PlayerClass);
        Assert.Equal(PlayerClass.Scout, brawler.BotGraphPlayerClass);
        Assert.Equal(PlayerClass.Soldier, ninja.BotGraphPlayerClass);
        Assert.False(registry.TryGetClassBinding(PlayerClass.Custom, out _));

        var definition = registry.CreateCharacterClassDefinition(BrawlerClassId);
        Assert.True(definition.IsCustomSlot);
        Assert.Equal(BrawlerClassId, definition.GameplayClassId);
        Assert.Equal(160, definition.MaxHealth);
        Assert.False(registry.CreateCharacterClassDefinition(PlayerClass.Scout).IsCustomSlot);
    }

    [Theory]
    [InlineData("Custom", "", "player class slot")]
    [InlineData("", "Custom", "bot graph player class")]
    [InlineData("", "42", "bot graph player class")]
    public void CustomSlotCannotBeUsedAsLegacyBindingOrBotGraph(string playerClass, string botGraphPlayerClass, string expectedError)
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var modPack = CreateModPack(new GameplayClassRuntimeDefinition(
            PlayerClass: playerClass,
            BasePlayerClass: "Custom",
            BotGraphPlayerClass: botGraphPlayerClass,
            SupportsExperimentalAcquiredWeapon: false));

        var exception = Record.Exception(() => registry.TryRegisterModPack(modPack, allowRuntimeClassBindingOverride: false, out _));
        var message = exception?.Message ?? string.Empty;
        if (exception is null)
        {
            Assert.False(registry.TryRegisterModPack(modPack, allowRuntimeClassBindingOverride: false, out var error));
            message = error;
        }

        Assert.Contains(expectedError, message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassHandleLookupsResolveCustomSlotClassesById()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        Assert.True(registry.TryRegisterModPack(CreateModPack(), allowRuntimeClassBindingOverride: false, out var error), error);
        var brawler = registry.CreateCharacterClassDefinition(BrawlerClassId);
        var scout = registry.CreateCharacterClassDefinition(PlayerClass.Scout);

        Assert.Equal("Brawler", registry.GetClassDefinition(brawler).DisplayName);
        Assert.Equal($"{BrawlerClassId}.fists", registry.GetPrimaryItem(brawler).Id);
        // The class JSON opts in, but dropped weapons are keyed by legacy slot.
        Assert.False(registry.SupportsExperimentalAcquiredWeapon(brawler));
        Assert.False(registry.SupportsExperimentalAcquiredWeapon(PlayerClass.Custom));

        Assert.Equal(registry.GetClassDefinition(PlayerClass.Scout), registry.GetClassDefinition(scout));
        Assert.Equal(registry.GetPrimaryItem(PlayerClass.Scout), registry.GetPrimaryItem(scout));
        Assert.Equal(
            registry.SupportsExperimentalAcquiredWeapon(PlayerClass.Scout),
            registry.SupportsExperimentalAcquiredWeapon(scout));
    }

    [Fact]
    public void LoadoutResolverUsesClassHandleAndTreatsCustomSlotAsEmpty()
    {
        EnsureModPackRegistered();
        var brawler = CharacterClassCatalog.GetDefinition(BrawlerClassId);

        var loadouts = GameplayLoadoutSelectionResolver.GetOrderedLoadouts(brawler);
        Assert.Single(loadouts);
        Assert.Equal($"{BrawlerClassId}.stock", loadouts[0].Id);
        Assert.Empty(GameplayLoadoutSelectionResolver.GetOrderedLoadouts(PlayerClass.Custom));
        Assert.False(GameplayLoadoutSelectionResolver.TryResolveLoadoutId(PlayerClass.Custom, "1", out _));
        Assert.Equal(
            GameplayLoadoutSelectionResolver.GetOrderedLoadouts(PlayerClass.Soldier),
            GameplayLoadoutSelectionResolver.GetOrderedLoadouts(CharacterClassCatalog.Soldier));
    }

    [Fact]
    public void CustomSlotPlayersSimulateTakeDamageAndDieWithoutLegacyBinding()
    {
        EnsureModPackRegistered();
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.NetworkPlayers.PrepareLocalPlayerJoin();
        world.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        world.NetworkPlayers.CompleteLocalPlayerJoin(BrawlerClassId);
        var brawler = world.LocalPlayer;

        Assert.True(world.NetworkPlayers.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.NetworkPlayers.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.NetworkPlayers.TryApplyNetworkPlayerClassSelection(2, NinjaClassId));
        Assert.True(world.NetworkPlayers.TryGetNetworkPlayer(2, out var ninja));

        Assert.Equal(PlayerClass.Custom, brawler.ClassId);
        Assert.Equal(BrawlerClassId, brawler.GameplayClassId);
        Assert.Equal(NinjaClassId, ninja.GameplayClassId);
        Assert.Equal(160, brawler.MaxHealth);

        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with { Right = true, FirePrimary = true });
        for (var tick = 0; tick < 90; tick += 1)
        {
            world.AdvanceOneTick();
        }

        // Hit-mask sprites come from the plugin class presentation, not a legacy slot.
        Assert.True(brawler.IsAlive);
        Assert.StartsWith("BrawlerRed", world.PresentationBounds.GetPlayerPresentationBodySpriteName(brawler), StringComparison.Ordinal);

        world.Combat.ApplyPlayerDamage(brawler, 30, ninja);
        Assert.Equal(brawler.MaxHealth - 30, brawler.Health);
        world.PlayerDeaths.KillPlayer(brawler, false, ninja, "BrawlerKL");
        Assert.False(brawler.IsAlive);
        Assert.Empty(world.WorldObjects.DroppedWeapons);

        for (var tick = 0; tick < 30; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }

    [Fact]
    public void RuntimeClassRegistrationAddsCustomSlotClassesInDisplayOrder()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        Assert.Empty(registry.CustomSlotClassBindings);

        Assert.True(registry.TryRegisterGameplayClass(CreateRuntimeClass("plugin.test.zeta", "Zeta"), out var error), error);
        Assert.True(registry.TryRegisterGameplayClass(CreateRuntimeClass("plugin.test.alpha", "alpha"), out error), error);

        Assert.Equal(
            ["plugin.test.alpha", "plugin.test.zeta"],
            registry.CustomSlotClassBindings.Select(static binding => binding.ClassId));
        Assert.Contains(registry.ModPacks, modPack => modPack.Id == "plugin.test" && modPack.Classes.ContainsKey("plugin.test.zeta"));
        Assert.Equal(PlayerClass.Scout, registry.GetRequiredClassBinding("plugin.test.zeta").BotGraphPlayerClass);
        Assert.Equal("Scattergun", registry.CreateCharacterClassDefinition("plugin.test.zeta").PrimaryWeapon.DisplayName);
    }

    [Fact]
    public void RuntimeClassRegistrationRejectsInvalidClasses()
    {
        var registry = GameplayRuntimeRegistry.CreateStock();
        var valid = CreateRuntimeClass("plugin.test.kano", "Kano");

        Assert.False(registry.TryRegisterGameplayClass(valid with { ClassId = "scout" }, out var error));
        Assert.Contains("already registered", error, StringComparison.Ordinal);
        Assert.False(registry.TryRegisterGameplayClass(valid with { BotGraphPlayerClass = "Custom" }, out error));
        Assert.Contains("bot graph", error, StringComparison.Ordinal);
        Assert.False(registry.TryRegisterGameplayClass(valid with { Movement = valid.Movement with { MaxHealth = 0 } }, out error));
        Assert.Contains("movement", error, StringComparison.Ordinal);
        Assert.False(registry.TryRegisterGameplayClass(valid with { DefaultLoadout = valid.DefaultLoadout with { PrimaryItemId = "weapon.missing" } }, out error));
        Assert.Contains("weapon.missing", error, StringComparison.Ordinal);

        Assert.True(registry.TryRegisterGameplayClass(valid, out error), error);
        Assert.False(registry.TryRegisterGameplayClass(valid, out error));
        Assert.Contains("already registered", error, StringComparison.Ordinal);

        registry.SealAbilityDefinitions();
        Assert.False(registry.TryRegisterGameplayClass(CreateRuntimeClass("plugin.test.late", "Late"), out error));
        Assert.Contains("sealed", error, StringComparison.Ordinal);
    }

    private static GameplayClassRegistration CreateRuntimeClass(string classId, string displayName)
    {
        return new GameplayClassRegistration(
            classId,
            displayName,
            new GameplayClassMovementDefinition(
                MaxHealth: 150,
                CollisionLeft: -7f,
                CollisionTop: -12f,
                CollisionRight: 8f,
                CollisionBottom: 12f,
                RunPower: 1.1f,
                JumpStrength: 8.3f,
                MaxAirJumps: 0,
                TauntLengthFrames: 8),
            new GameplayLoadoutRegistration(classId, $"{classId}.stock", "Stock", "weapon.scattergun"),
            new GameplayClassPresentationDefinition(displayName),
            ModPackId: "plugin.test");
    }

    private static void EnsureModPackRegistered()
    {
        if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(BrawlerClassId, out _))
        {
            return;
        }

        Assert.True(
            CharacterClassCatalog.RuntimeRegistry.TryRegisterModPack(CreateModPack(), allowRuntimeClassBindingOverride: false, out var error),
            error);
    }

    private static GameplayModPackDefinition CreateModPack(GameplayClassRuntimeDefinition? brawlerRuntime = null)
    {
        var classes = new Dictionary<string, GameplayClassDefinition>(StringComparer.Ordinal)
        {
            [BrawlerClassId] = CreateClass(
                BrawlerClassId,
                "Brawler",
                maxHealth: 160,
                brawlerRuntime ?? new GameplayClassRuntimeDefinition(
                    BasePlayerClass: "Custom",
                    SupportsExperimentalAcquiredWeapon: true,
                    PrimaryWeaponKillFeedSprite: "BrawlerKL")),
            [NinjaClassId] = CreateClass(
                NinjaClassId,
                "Ninja",
                maxHealth: 120,
                new GameplayClassRuntimeDefinition(
                    BasePlayerClass: "Custom",
                    BotGraphPlayerClass: "Soldier",
                    SupportsExperimentalAcquiredWeapon: false)),
        };

        var items = new Dictionary<string, GameplayItemDefinition>(StringComparer.Ordinal);
        foreach (var classId in classes.Keys)
        {
            var itemId = $"{classId}.fists";
            items[itemId] = new GameplayItemDefinition(
                itemId,
                "Fists",
                GameplayEquipmentSlot.Primary,
                BuiltInGameplayBehaviorIds.PelletGun,
                new GameplayItemAmmoDefinition(
                    MaxAmmo: 6,
                    AmmoPerUse: 1,
                    ProjectilesPerUse: 1,
                    UseDelaySourceTicks: 18,
                    ReloadSourceTicks: 15,
                    SpreadDegrees: 0f,
                    MinProjectileSpeed: 11f,
                    AdditionalProjectileSpeed: 0f,
                    AutoReloads: true),
                new GameplayItemPresentationDefinition());
        }

        return new GameplayModPackDefinition(
            ModPackId,
            "Custom Slot Test Classes",
            new Version(1, 0, 0),
            items,
            classes,
            GameplayModPackAssetCatalog.Empty);
    }

    private static GameplayClassDefinition CreateClass(
        string classId,
        string displayName,
        int maxHealth,
        GameplayClassRuntimeDefinition runtime)
    {
        var loadout = new GameplayClassLoadoutDefinition($"{classId}.stock", "Stock", $"{classId}.fists");
        return new GameplayClassDefinition(
            classId,
            displayName,
            new GameplayClassMovementDefinition(
                MaxHealth: maxHealth,
                CollisionLeft: -6.0f,
                CollisionTop: -10.0f,
                CollisionRight: 7.0f,
                CollisionBottom: 24.0f,
                RunPower: 1.2f,
                JumpStrength: 8.3f,
                MaxAirJumps: 0,
                TauntLengthFrames: 8),
            new Dictionary<string, GameplayClassLoadoutDefinition>(StringComparer.Ordinal)
            {
                [loadout.Id] = loadout,
            },
            loadout.Id,
            new GameplayClassPresentationDefinition(displayName, RunSuffix: "RunS", JumpSuffix: "JumpS"),
            runtime);
    }
}
