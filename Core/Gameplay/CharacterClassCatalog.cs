using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class CharacterClassCatalog
{
    public static GameplayRuntimeRegistry RuntimeRegistry { get; } = GameplayRuntimeRegistry.CreateStock();

    public static GameplayModPackDefinition StockModPack => RuntimeRegistry.GetRequiredModPack(StockGameplayModCatalog.Definition.Id);

    public static PrimaryWeaponDefinition Scattergun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.scattergun"]);

    public static PrimaryWeaponDefinition Shotgun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.shotgun"]);

    public static PrimaryWeaponDefinition SoldierShotgun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.soldier-shotgun"]);

    public static PrimaryWeaponDefinition SoldierShotgunLtd { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.soldier-shotgun-ltd"]);

    public static PrimaryWeaponDefinition Flamethrower { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.flamethrower"]);

    public static PrimaryWeaponDefinition RocketLauncher { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.rocketlauncher"]);

    public static PrimaryWeaponDefinition MineLauncher { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.minelauncher"]);

    public static PrimaryWeaponDefinition Minigun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.minigun"]);

    public static PrimaryWeaponDefinition Rifle { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.rifle"]);

    public static PrimaryWeaponDefinition Medigun { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.medigun"]);

    public static PrimaryWeaponDefinition Revolver { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.revolver"]);

    public static PrimaryWeaponDefinition Blade { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.blade"]);

    public static PrimaryWeaponDefinition ExperimentalDemoknightEyelander { get; } = RuntimeRegistry.CreatePrimaryWeaponDefinition(StockGameplayModCatalog.GetExperimentalDemoknightEyelanderItem());

    public static CharacterClassDefinition Scout => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Scout);

    public static CharacterClassDefinition Engineer => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Engineer);

    public static CharacterClassDefinition Pyro => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Pyro);

    public static CharacterClassDefinition Soldier => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Soldier);

    public static CharacterClassDefinition Demoman => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Demoman);

    public static CharacterClassDefinition Heavy => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Heavy);

    public static CharacterClassDefinition Sniper => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Sniper);

    public static CharacterClassDefinition Medic => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Medic);

    public static CharacterClassDefinition Spy => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Spy);

    public static CharacterClassDefinition Civilian => RuntimeRegistry.CreateCharacterClassDefinition(PlayerClass.Quote);

    public static CharacterClassDefinition Quote => Civilian;

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
            PlayerClass.Quote => Civilian,
            _ => Scout,
        };
    }

    public static CharacterClassDefinition GetDefinition(string gameplayClassId)
    {
        return RuntimeRegistry.CreateCharacterClassDefinition(gameplayClassId);
    }

    public static bool SupportsExperimentalAcquiredWeapon(PlayerClass playerClass)
    {
        return RuntimeRegistry.SupportsExperimentalAcquiredWeapon(playerClass);
    }

    public static bool SupportsExperimentalAcquiredWeapon(string gameplayClassId)
    {
        return RuntimeRegistry.SupportsExperimentalAcquiredWeapon(gameplayClassId);
    }

    public static string GetPrimaryWeaponKillFeedSprite(PlayerClass playerClass)
    {
        return RuntimeRegistry.GetPrimaryWeaponKillFeedSprite(playerClass);
    }

    public static string GetPrimaryWeaponKillFeedSprite(string gameplayClassId)
    {
        return RuntimeRegistry.GetPrimaryWeaponKillFeedSprite(gameplayClassId);
    }
}
