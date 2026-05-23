using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class StockGameplayModCatalog
{
    public const string StockPackDirectoryName = "stock.gg2";

    public static GameplayModPackDefinition Definition { get; } = LoadDefinition();

    public static string GetClassId(PlayerClass playerClass)
    {
        foreach (var gameplayClass in Definition.Classes.Values)
        {
            var runtime = gameplayClass.Runtime;
            if (runtime is null)
            {
                continue;
            }

            if (Enum.TryParse<PlayerClass>(runtime.PlayerClass, ignoreCase: true, out var runtimePlayerClass)
                && runtimePlayerClass == playerClass)
            {
                return gameplayClass.Id;
            }
        }

        return Definition.Classes.ContainsKey("scout") ? "scout" : Definition.Classes.Keys.First();
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
        if (OperatingSystem.IsBrowser()
            && BrowserGameplayModCatalog.TryGetDefinition(StockPackDirectoryName, out var browserDefinition))
        {
            return browserDefinition;
        }

        var packDirectory = GameplayModPackDirectoryLoader.FindPackDirectory(StockPackDirectoryName);
        if (string.IsNullOrWhiteSpace(packDirectory))
        {
            throw new DirectoryNotFoundException($"Stock gameplay pack directory \"{StockPackDirectoryName}\" could not be found under the gameplay content root.");
        }

        return GameplayModPackDirectoryLoader.LoadFromDirectory(packDirectory);
    }
}
