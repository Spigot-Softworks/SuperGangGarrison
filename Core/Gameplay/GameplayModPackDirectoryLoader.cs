using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class GameplayModPackDirectoryLoader
{
    private const string PackMetadataFileName = "pack.json";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static IReadOnlyList<GameplayModPackDefinition> LoadAllFromContentRoot()
    {
        var gameplayRoot = FindGameplayRootDirectory();
        if (string.IsNullOrWhiteSpace(gameplayRoot) || !Directory.Exists(gameplayRoot))
        {
            return Array.Empty<GameplayModPackDefinition>();
        }

        return Directory.GetDirectories(gameplayRoot)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(LoadFromDirectory)
            .ToArray();
    }

    public static GameplayModPackDefinition LoadFromDirectory(string packDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectory);
        var fullPackDirectory = Path.GetFullPath(packDirectory);
        if (!Directory.Exists(fullPackDirectory))
        {
            throw new DirectoryNotFoundException($"Gameplay mod pack directory was not found: {fullPackDirectory}");
        }

        var metadata = LoadRequiredJson<PackMetadataDocument>(Path.Combine(fullPackDirectory, PackMetadataFileName));
        var items = LoadDefinitionsFromDirectory<GameplayItemDefinition>(
            fullPackDirectory,
            "items",
            (item, _, filePath) =>
            {
                ValidateRequiredText(item.Id, nameof(GameplayItemDefinition.Id), filePath);
                ValidateRequiredText(item.DisplayName, nameof(GameplayItemDefinition.DisplayName), filePath);
                ValidateRequiredText(item.BehaviorId, nameof(GameplayItemDefinition.BehaviorId), filePath);
                if (item.Slot == GameplayEquipmentSlot.Primary && item.Ammo.MaxAmmo < 0)
                {
                    throw new InvalidOperationException($"Primary item ammo cannot be negative in gameplay item file \"{filePath}\".");
                }

                return item with
                {
                    Ownership = item.Ownership ?? new GameplayItemOwnershipDefinition(),
                };
            });
        var itemsById = items.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        var classes = LoadDefinitionsFromDirectory<GameplayClassDefinition>(
            fullPackDirectory,
            "classes",
            (gameplayClass, _, filePath) =>
            {
                ValidateRequiredText(gameplayClass.Id, nameof(GameplayClassDefinition.Id), filePath);
                ValidateRequiredText(gameplayClass.DisplayName, nameof(GameplayClassDefinition.DisplayName), filePath);
                ValidateRequiredText(gameplayClass.DefaultLoadoutId, nameof(GameplayClassDefinition.DefaultLoadoutId), filePath);
                if (!gameplayClass.Loadouts.ContainsKey(gameplayClass.DefaultLoadoutId))
                {
                    throw new InvalidOperationException($"Gameplay class \"{gameplayClass.Id}\" default loadout \"{gameplayClass.DefaultLoadoutId}\" was not found in \"{filePath}\".");
                }

                foreach (var loadout in gameplayClass.Loadouts.Values)
                {
                    ValidateRequiredText(loadout.Id, nameof(GameplayClassLoadoutDefinition.Id), filePath);
                    ValidateRequiredText(loadout.DisplayName, nameof(GameplayClassLoadoutDefinition.DisplayName), filePath);
                    ValidateRequiredText(loadout.PrimaryItemId, nameof(GameplayClassLoadoutDefinition.PrimaryItemId), filePath);
                    ValidateReferencedItem(itemsById, loadout.PrimaryItemId, GameplayEquipmentSlot.Primary, gameplayClass.Id, loadout.Id, filePath);
                    ValidateOptionalReferencedItem(itemsById, loadout.SecondaryItemId, GameplayEquipmentSlot.Secondary, gameplayClass.Id, loadout.Id, filePath);
                    ValidateOptionalReferencedItem(itemsById, loadout.UtilityItemId, GameplayEquipmentSlot.Utility, gameplayClass.Id, loadout.Id, filePath);
                }

                return gameplayClass;
            });
        var classesById = classes.ToDictionary(static gameplayClass => gameplayClass.Id, StringComparer.Ordinal);
        var versionText = metadata.Version?.Trim();
        if (!Version.TryParse(versionText, out var version))
        {
            throw new InvalidOperationException($"Gameplay mod pack version \"{metadata.Version}\" is invalid in \"{Path.Combine(fullPackDirectory, PackMetadataFileName)}\".");
        }

        ValidateRequiredText(metadata.Id, nameof(PackMetadataDocument.Id), fullPackDirectory);
        ValidateRequiredText(metadata.DisplayName, nameof(PackMetadataDocument.DisplayName), fullPackDirectory);

        return new GameplayModPackDefinition(
            Id: metadata.Id.Trim(),
            DisplayName: metadata.DisplayName.Trim(),
            Version: version,
            Items: itemsById,
            Classes: classesById);
    }

    public static string? FindPackDirectory(string packDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectoryName);

        var runtimePath = ContentRoot.GetPath("Gameplay", packDirectoryName);
        if (Directory.Exists(runtimePath))
        {
            return runtimePath;
        }

        var projectContentPath = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content", "Gameplay", packDirectoryName));
        if (!string.IsNullOrWhiteSpace(projectContentPath) && Directory.Exists(projectContentPath))
        {
            return projectContentPath;
        }

        var sourceContentPath = ProjectSourceLocator.FindDirectory(Path.Combine(ContentRoot.Path, "Gameplay", packDirectoryName));
        if (!string.IsNullOrWhiteSpace(sourceContentPath) && Directory.Exists(sourceContentPath))
        {
            return sourceContentPath;
        }

        return null;
    }

    private static string? FindGameplayRootDirectory()
    {
        var runtimePath = ContentRoot.GetPath("Gameplay");
        if (Directory.Exists(runtimePath))
        {
            return runtimePath;
        }

        var projectContentPath = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content", "Gameplay"));
        if (!string.IsNullOrWhiteSpace(projectContentPath) && Directory.Exists(projectContentPath))
        {
            return projectContentPath;
        }

        var sourceContentPath = ProjectSourceLocator.FindDirectory(Path.Combine(ContentRoot.Path, "Gameplay"));
        if (!string.IsNullOrWhiteSpace(sourceContentPath) && Directory.Exists(sourceContentPath))
        {
            return sourceContentPath;
        }

        return null;
    }

    private static T LoadRequiredJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required gameplay mod pack file was not found: {path}", path);
        }

        var json = File.ReadAllText(path);
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException($"Gameplay mod pack file \"{path}\" could not be deserialized as {typeof(T).Name}.");
        }

        return value;
    }

    private static IReadOnlyList<TDefinition> LoadDefinitionsFromDirectory<TDefinition>(
        string packDirectory,
        string relativeDirectory,
        Func<TDefinition, string, string, TDefinition> normalize)
    {
        var fullDirectory = Path.Combine(packDirectory, relativeDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            return Array.Empty<TDefinition>();
        }

        var results = new List<TDefinition>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filePath in Directory.GetFiles(fullDirectory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var definition = LoadRequiredJson<TDefinition>(filePath);
            var normalized = normalize(definition, Path.GetFileNameWithoutExtension(filePath), filePath);
            var id = normalized switch
            {
                GameplayItemDefinition item => item.Id,
                GameplayClassDefinition gameplayClass => gameplayClass.Id,
                _ => throw new InvalidOperationException($"Unsupported gameplay mod definition type: {typeof(TDefinition).Name}"),
            };

            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"Duplicate gameplay definition id \"{id}\" was found in \"{fullDirectory}\".");
            }

            results.Add(normalized);
        }

        return results;
    }

    private static void ValidateReferencedItem(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        string itemId,
        GameplayEquipmentSlot expectedSlot,
        string classId,
        string loadoutId,
        string filePath)
    {
        if (!items.TryGetValue(itemId, out var item))
        {
            throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadoutId}\" references unknown item \"{itemId}\" in \"{filePath}\".");
        }

        if (item.Slot != expectedSlot)
        {
            throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadoutId}\" expected item \"{itemId}\" to use slot \"{expectedSlot}\", but found \"{item.Slot}\" in \"{filePath}\".");
        }
    }

    private static void ValidateOptionalReferencedItem(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        string? itemId,
        GameplayEquipmentSlot expectedSlot,
        string classId,
        string loadoutId,
        string filePath)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            ValidateReferencedItem(items, itemId, expectedSlot, classId, loadoutId, filePath);
        }
    }

    private static void ValidateRequiredText(string? value, string fieldName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required gameplay field \"{fieldName}\" was empty in \"{filePath}\".");
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

    private sealed record PackMetadataDocument(
        string Id,
        string DisplayName,
        string Version);
}
