using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class StockGameplayModCatalog
{
    public const string StockPackDirectoryName = "stock.gg2";

    public static GameplayModPackDefinition Definition { get; } = LoadDefinition();

    public static string GetClassId(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Engineer => "engineer",
            PlayerClass.Pyro => "pyro",
            PlayerClass.Soldier => "soldier",
            PlayerClass.Demoman => "demoman",
            PlayerClass.Heavy => "heavy",
            PlayerClass.Sniper => "sniper",
            PlayerClass.Medic => "medic",
            PlayerClass.Spy => "spy",
            PlayerClass.Quote => "quote",
            _ => "scout",
        };
    }

    public static GameplayClassDefinition GetClassDefinition(PlayerClass playerClass)
    {
        return Definition.Classes[GetClassId(playerClass)];
    }

    public static GameplayClassLoadoutDefinition GetDefaultLoadout(PlayerClass playerClass)
    {
        var classDefinition = GetClassDefinition(playerClass);
        return classDefinition.Loadouts[classDefinition.DefaultLoadoutId];
    }

    public static GameplayItemDefinition GetPrimaryItem(PlayerClass playerClass)
    {
        var loadout = GetDefaultLoadout(playerClass);
        return Definition.Items[loadout.PrimaryItemId];
    }

    public static GameplayItemDefinition? GetSecondaryItem(PlayerClass playerClass)
    {
        var loadout = GetDefaultLoadout(playerClass);
        return loadout.SecondaryItemId is null
            ? null
            : Definition.Items[loadout.SecondaryItemId];
    }

    public static GameplayItemDefinition? GetUtilityItem(PlayerClass playerClass)
    {
        var loadout = GetDefaultLoadout(playerClass);
        return loadout.UtilityItemId is null
            ? null
            : Definition.Items[loadout.UtilityItemId];
    }

    public static GameplayItemDefinition GetExperimentalDemoknightEyelanderItem()
    {
        return Definition.Items[ExperimentalDemoknightCatalog.EyelanderItemId];
    }

    public static GameplayItemDefinition GetExperimentalDemoknightPaintrainItem()
    {
        return Definition.Items[ExperimentalDemoknightCatalog.PaintrainItemId];
    }

    private static GameplayModPackDefinition LoadDefinition()
    {
        var packDirectory = GameplayModPackDirectoryLoader.FindPackDirectory(StockPackDirectoryName);
        if (string.IsNullOrWhiteSpace(packDirectory))
        {
            throw new DirectoryNotFoundException($"Stock gameplay pack directory \"{StockPackDirectoryName}\" could not be found under the gameplay content root.");
        }

        return GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);
    }
}
