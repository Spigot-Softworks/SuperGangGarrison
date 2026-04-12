using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.ClientShared;

public static class BrowserStockGameplayModCatalogLoader
{
    private const string StockPackId = "stock.gg2";
    private const string StockPackRoot = "Content/Gameplay/stock.gg2";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string[] ItemFileNames =
    [
        "ability.demoman-detonate.json",
        "ability.engineer-pda.json",
        "ability.heavy-sandvich.json",
        "ability.medic-needlegun.json",
        "ability.medic-uber.json",
        "ability.pyro-airblast.json",
        "ability.quote-blade-throw.json",
        "ability.sniper-scope.json",
        "ability.spy-cloak.json",
        "experimental.demoknight.eyelander.json",
        "experimental.demoknight.paintrain.json",
        "weapon.blackbox.json",
        "weapon.blade.json",
        "weapon.brassbeast.json",
        "weapon.diamondback.json",
        "weapon.directhit.json",
        "weapon.flamethrower.json",
        "weapon.medigun.json",
        "weapon.mine_launcher.json",
        "weapon.minigun.json",
        "weapon.revolver.json",
        "weapon.rifle.json",
        "weapon.rocketlauncher.json",
        "weapon.scattergun.json",
        "weapon.shotgun.json",
        "weapon.tomislav.json",
    ];

    private static readonly string[] ClassFileNames =
    [
        "demoman.json",
        "engineer.json",
        "heavy.json",
        "medic.json",
        "pyro.json",
        "quote.json",
        "scout.json",
        "sniper.json",
        "soldier.json",
        "spy.json",
    ];
    public static async Task EnsureLoadedAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!OperatingSystem.IsBrowser() || BrowserGameplayModCatalog.HasDefinitions)
        {
            return;
        }

        var definition = await LoadStockPackDefinitionAsync(httpClient, cancellationToken);
        BrowserGameplayModCatalog.Register([definition]);
    }

    private static async Task<GameplayModPackDefinition> LoadStockPackDefinitionAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var definition = await LoadJsonAsync<GameplayModPackDefinition>(
            httpClient,
            BrowserDistributionPaths.StockPackDefinitionPath,
            cancellationToken);
        var itemsById = definition.Items;
        var classesById = definition.Classes;
        var spritesById = definition.Assets.Sprites;

        foreach (var gameplayClass in classesById.Values)
        {
            ValidateRequiredText(gameplayClass.Id, nameof(GameplayClassDefinition.Id), gameplayClass.Id);
            ValidateRequiredText(gameplayClass.DisplayName, nameof(GameplayClassDefinition.DisplayName), gameplayClass.Id);
            ValidateRequiredText(gameplayClass.DefaultLoadoutId, nameof(GameplayClassDefinition.DefaultLoadoutId), gameplayClass.Id);
            if (!gameplayClass.Loadouts.ContainsKey(gameplayClass.DefaultLoadoutId))
            {
                throw new InvalidOperationException($"Gameplay class \"{gameplayClass.Id}\" default loadout \"{gameplayClass.DefaultLoadoutId}\" was not found.");
            }

            foreach (var loadout in gameplayClass.Loadouts.Values)
            {
                ValidateRequiredText(loadout.Id, nameof(GameplayClassLoadoutDefinition.Id), gameplayClass.Id);
                ValidateRequiredText(loadout.DisplayName, nameof(GameplayClassLoadoutDefinition.DisplayName), gameplayClass.Id);
                ValidateRequiredText(loadout.PrimaryItemId, nameof(GameplayClassLoadoutDefinition.PrimaryItemId), gameplayClass.Id);
                ValidateReferencedItem(itemsById, loadout.PrimaryItemId, GameplayEquipmentSlot.Primary, gameplayClass.Id, loadout.Id);
                ValidateOptionalReferencedItem(itemsById, loadout.SecondaryItemId, GameplayEquipmentSlot.Secondary, gameplayClass.Id, loadout.Id);
                ValidateOptionalReferencedItem(itemsById, loadout.UtilityItemId, GameplayEquipmentSlot.Utility, gameplayClass.Id, loadout.Id);
            }
        }

        foreach (var item in itemsById.Values)
        {
            ValidateRequiredText(item.Id, nameof(GameplayItemDefinition.Id), item.Id);
            ValidateRequiredText(item.DisplayName, nameof(GameplayItemDefinition.DisplayName), item.Id);
            ValidateRequiredText(item.BehaviorId, nameof(GameplayItemDefinition.BehaviorId), item.Id);
            if (item.Slot == GameplayEquipmentSlot.Primary && item.Ammo.MaxAmmo < 0)
            {
                throw new InvalidOperationException($"Primary item ammo cannot be negative for gameplay item \"{item.Id}\".");
            }
        }

        return definition;
    }

    private static async Task<T> LoadJsonAsync<T>(HttpClient httpClient, string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        if (value is null)
        {
            throw new InvalidOperationException($"Browser gameplay mod file \"{path}\" could not be deserialized as {typeof(T).Name}.");
        }

        return value;
    }

    private static void ValidateReferencedItem(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        string itemId,
        GameplayEquipmentSlot expectedSlot,
        string classId,
        string loadoutId)
    {
        if (!items.TryGetValue(itemId, out var item))
        {
            throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadoutId}\" references unknown item \"{itemId}\".");
        }

        if (item.Slot != expectedSlot)
        {
            throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadoutId}\" expected item \"{itemId}\" to use slot \"{expectedSlot}\", but found \"{item.Slot}\".");
        }
    }

    private static void ValidateOptionalReferencedItem(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        string? itemId,
        GameplayEquipmentSlot expectedSlot,
        string classId,
        string loadoutId)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            ValidateReferencedItem(items, itemId, expectedSlot, classId, loadoutId);
        }
    }

    private static void ValidateRequiredText(string? value, string fieldName, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required gameplay field \"{fieldName}\" was empty in \"{context}\".");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

}
