using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class CharacterClassCatalog
{
    public static GameplayRuntimeRegistry RuntimeRegistry { get; } = GameplayRuntimeRegistry.CreateStock();

    public static GameplayModPackDefinition StockModPack => RuntimeRegistry.GetRequiredModPack(StockGameplayModCatalog.Definition.Id);

    public static PrimaryWeaponDefinition Scattergun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.scattergun"]);

    public static PrimaryWeaponDefinition Shotgun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.shotgun"]);

    public static PrimaryWeaponDefinition Flamethrower { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.flamethrower"]);

    public static PrimaryWeaponDefinition RocketLauncher { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.rocketlauncher"]);

    public static PrimaryWeaponDefinition MineLauncher { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.minelauncher"]);

    public static PrimaryWeaponDefinition Minigun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.minigun"]);

    public static PrimaryWeaponDefinition Rifle { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.rifle"]);

    public static PrimaryWeaponDefinition Medigun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.medigun"]);

    public static PrimaryWeaponDefinition Revolver { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.revolver"]);

    public static PrimaryWeaponDefinition Blade { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.blade"]);

    public static PrimaryWeaponDefinition ExperimentalDemoknightEyelander { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.GetExperimentalDemoknightEyelanderItem());

    public static CharacterClassDefinition Scout { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Scout);

    public static CharacterClassDefinition Engineer { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Engineer);

    public static CharacterClassDefinition Pyro { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Pyro);

    public static CharacterClassDefinition Soldier { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Soldier);

    public static CharacterClassDefinition Demoman { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Demoman);

    public static CharacterClassDefinition Heavy { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Heavy);

    public static CharacterClassDefinition Sniper { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Sniper);

    public static CharacterClassDefinition Medic { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Medic);

    public static CharacterClassDefinition Spy { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Spy);

    public static CharacterClassDefinition Quote { get; } = RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Quote);

    public static CharacterClassDefinition GetDefinition(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Engineer => Engineer,
            PlayerClass.Pyro => Pyro,
            PlayerClass.Soldier => Soldier,
            PlayerClass.Demoman => Demoman,
            PlayerClass.Heavy => Heavy,
            PlayerClass.Sniper => Sniper,
            PlayerClass.Medic => Medic,
            PlayerClass.Spy => Spy,
            PlayerClass.Quote => Quote,
            _ => Scout,
        };
    }

    public static bool SupportsExperimentalAcquiredWeapon(PlayerClass playerClass)
    {
        return RuntimeRegistry.SupportsExperimentalAcquiredWeapon(playerClass);
    }

    public static string GetPrimaryWeaponKillFeedSprite(PlayerClass playerClass)
    {
        return RuntimeRegistry.GetPrimaryWeaponKillFeedSprite(playerClass);
    }
}
