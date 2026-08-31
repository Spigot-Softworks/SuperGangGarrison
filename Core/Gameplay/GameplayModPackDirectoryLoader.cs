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
    private const string RuntimeMetadataFileName = "runtime.json";
    private const int CurrentSchemaVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly HashSet<string> LegacyAlternatePrimaryItemIds = new(StringComparer.Ordinal)
    {
        "weapon.bow",
        "weapon.medigun.crit",
        "weapon.scout-nailgun",
    };

    public static IReadOnlyList<GameplayModPackDefinition> LoadAllFromContentRoot()
    {
        var gameplayRoot = FindGameplayRootDirectory();
        if (string.IsNullOrWhiteSpace(gameplayRoot) || !Directory.Exists(gameplayRoot))
        {
            return Array.Empty<GameplayModPackDefinition>();
        }

        return LoadAllFromDirectory(gameplayRoot);
    }

    public static IReadOnlyList<GameplayModPackDefinition> LoadAllFromDirectory(string gameplayRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameplayRootDirectory);
        var fullGameplayRootDirectory = Path.GetFullPath(gameplayRootDirectory);
        if (!Directory.Exists(fullGameplayRootDirectory))
        {
            return Array.Empty<GameplayModPackDefinition>();
        }

        return Directory.GetDirectories(fullGameplayRootDirectory)
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

        var metadataPath = Path.Combine(fullPackDirectory, PackMetadataFileName);
        var metadata = LoadRequiredJson<PackMetadataDocument>(metadataPath);
        var schemaVersion = ValidateSchemaVersion(metadata.SchemaVersion, metadataPath);
        var runtimeMetadataPath = Path.Combine(fullPackDirectory, RuntimeMetadataFileName);
        if (File.Exists(runtimeMetadataPath))
        {
            var runtimeDefinition = LoadRequiredJson<BrowserGameplayModPackDefinitionDocument>(runtimeMetadataPath).ToDefinition();
            if (!string.Equals(runtimeDefinition.Id, metadata.Id, StringComparison.Ordinal)
                || !string.Equals(runtimeDefinition.DisplayName, metadata.DisplayName, StringComparison.Ordinal)
                || !string.Equals(runtimeDefinition.Version.ToString(), metadata.Version, StringComparison.Ordinal)
                || runtimeDefinition.SchemaVersion != schemaVersion)
            {
                throw new InvalidOperationException(
                    $"Gameplay runtime metadata \"{runtimeMetadataPath}\" does not match \"{PackMetadataFileName}\".");
            }

            return runtimeDefinition;
        }

        var items = LoadDefinitionsFromDirectory<GameplayItemDefinition>(
            fullPackDirectory,
            "items",
            (item, _, filePath) =>
            {
                var normalizedItem = NormalizeItemDefinition(item, schemaVersion, filePath);
                ValidateRequiredText(normalizedItem.Id, nameof(GameplayItemDefinition.Id), filePath);
                ValidateRequiredText(normalizedItem.DisplayName, nameof(GameplayItemDefinition.DisplayName), filePath);
                ValidateRequiredText(normalizedItem.BehaviorId, nameof(GameplayItemDefinition.BehaviorId), filePath);
                if (normalizedItem.Kind == GameplayItemKind.Weapon
                    && normalizedItem.WeaponSlot == GameplayWeaponSlot.Primary
                    && normalizedItem.Ammo.MaxAmmo < 0)
                {
                    throw new InvalidOperationException($"Primary item ammo cannot be negative in gameplay item file \"{filePath}\".");
                }

                return normalizedItem with
                {
                    Presentation = NormalizeItemPresentation(normalizedItem.Presentation, filePath),
                    Ownership = normalizedItem.Ownership ?? new GameplayItemOwnershipDefinition(),
                    Ability = NormalizeAbilityDefinition(normalizedItem, filePath),
                };
            });
        var itemsById = items.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        ValidateGrantedAbilityItems(itemsById, fullPackDirectory);
        var sprites = LoadDefinitionsFromDirectory<GameplaySpriteAssetDefinition>(
            fullPackDirectory,
            "sprites",
            (sprite, _, filePath) =>
            {
                ValidateRequiredText(sprite.Id, nameof(GameplaySpriteAssetDefinition.Id), filePath);
                if (sprite.FramePaths is null || sprite.FramePaths.Count == 0)
                {
                    throw new InvalidOperationException($"Gameplay sprite asset \"{sprite.Id}\" must declare at least one frame path in \"{filePath}\".");
                }

                if (sprite.FrameWidth.HasValue && sprite.FrameWidth.Value <= 0)
                {
                    throw new InvalidOperationException($"Gameplay sprite asset \"{sprite.Id}\" declared an invalid frame width in \"{filePath}\".");
                }

                if (sprite.FrameHeight.HasValue && sprite.FrameHeight.Value <= 0)
                {
                    throw new InvalidOperationException($"Gameplay sprite asset \"{sprite.Id}\" declared an invalid frame height in \"{filePath}\".");
                }

                var normalizedFramePaths = sprite.FramePaths
                    .Select(framePath => NormalizeAndValidatePackRelativeFilePath(fullPackDirectory, framePath, sprite.Id, filePath))
                    .ToArray();
                return sprite with
                {
                    FramePaths = normalizedFramePaths,
                    Mask = NormalizeMask(sprite.Mask),
                };
            });
        var spritesById = sprites.ToDictionary(static sprite => sprite.Id, StringComparer.Ordinal);
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

                var normalizedLoadouts = gameplayClass.Loadouts.Values
                    .Select(loadout => NormalizeLoadoutDefinition(loadout, itemsById, schemaVersion, gameplayClass.Id, filePath))
                    .ToDictionary(static loadout => loadout.Id, StringComparer.Ordinal);
                foreach (var loadout in normalizedLoadouts.Values)
                {
                    ValidateRequiredText(loadout.Id, nameof(GameplayClassLoadoutDefinition.Id), filePath);
                    ValidateRequiredText(loadout.DisplayName, nameof(GameplayClassLoadoutDefinition.DisplayName), filePath);
                    if (schemaVersion == 1)
                    {
                        ValidateRequiredText(loadout.PrimaryItemId, nameof(GameplayClassLoadoutDefinition.PrimaryItemId), filePath);
                        ValidateReferencedItem(itemsById, loadout.PrimaryItemId, GameplayEquipmentSlot.Primary, gameplayClass.Id, loadout.Id, filePath);
                        ValidateOptionalReferencedItem(itemsById, loadout.SecondaryItemId, GameplayEquipmentSlot.Secondary, gameplayClass.Id, loadout.Id, filePath);
                        ValidateOptionalReferencedItem(itemsById, loadout.UtilityItemId, GameplayEquipmentSlot.Utility, gameplayClass.Id, loadout.Id, filePath);
                        ValidateReferencedAbilityItems(itemsById, loadout.AbilityItemIds, gameplayClass.Id, loadout.Id, filePath);
                    }

                    ValidateCanonicalLoadout(itemsById, loadout, gameplayClass.Id, filePath);
                }

                return gameplayClass with
                {
                    Presentation = NormalizePresentation(gameplayClass.Presentation),
                    Runtime = NormalizeRuntime(gameplayClass.Runtime, filePath),
                    Loadouts = normalizedLoadouts,
                };
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
            Classes: classesById,
            Assets: new GameplayModPackAssetCatalog(spritesById),
            SchemaVersion: schemaVersion);
    }

    public static string? FindPackDirectory(string packDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectoryName);

        var projectContentPath = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content", "Gameplay", packDirectoryName));
        if (ShouldPreferProjectContentRoot()
            && !string.IsNullOrWhiteSpace(projectContentPath)
            && Directory.Exists(projectContentPath)
            && HasPackMetadata(projectContentPath))
        {
            return projectContentPath;
        }

        var runtimePath = ContentRoot.GetPath("Gameplay", packDirectoryName);
        if (Directory.Exists(runtimePath) && HasPackMetadata(runtimePath))
        {
            return runtimePath;
        }

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
        var projectContentPath = ProjectSourceLocator.FindDirectory(Path.Combine("Core", "Content", "Gameplay"));
        if (ShouldPreferProjectContentRoot()
            && !string.IsNullOrWhiteSpace(projectContentPath)
            && Directory.Exists(projectContentPath)
            && HasAnyPackMetadata(projectContentPath))
        {
            return projectContentPath;
        }

        var runtimePath = ContentRoot.GetPath("Gameplay");
        if (Directory.Exists(runtimePath) && HasAnyPackMetadata(runtimePath))
        {
            return runtimePath;
        }

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

    private static bool ShouldPreferProjectContentRoot()
    {
        return string.Equals(ContentRoot.Path, "Content", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyPackMetadata(string gameplayRootDirectory)
    {
        return Directory.Exists(gameplayRootDirectory)
            && Directory.GetDirectories(gameplayRootDirectory).Any(HasPackMetadata);
    }

    private static bool HasPackMetadata(string packDirectory)
    {
        return File.Exists(Path.Combine(packDirectory, PackMetadataFileName));
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
        foreach (var filePath in Directory.GetFiles(fullDirectory, "*.json", SearchOption.AllDirectories).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var definition = LoadRequiredJson<TDefinition>(filePath);
            var normalized = normalize(definition, Path.GetFileNameWithoutExtension(filePath), filePath);
            var id = normalized switch
            {
                GameplayItemDefinition item => item.Id,
                GameplayClassDefinition gameplayClass => gameplayClass.Id,
                GameplaySpriteAssetDefinition sprite => sprite.Id,
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

    private static int ValidateSchemaVersion(int schemaVersion, string metadataPath)
    {
        var normalizedSchemaVersion = schemaVersion <= 0 ? 1 : schemaVersion;
        if (normalizedSchemaVersion is < 1 or > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Gameplay mod pack schema version \"{schemaVersion}\" is not supported in \"{metadataPath}\". "
                + $"Supported versions are 1 through {CurrentSchemaVersion}.");
        }

        return normalizedSchemaVersion;
    }

    private static GameplayItemDefinition NormalizeItemDefinition(
        GameplayItemDefinition item,
        int schemaVersion,
        string filePath)
    {
        var kind = item.Kind;
        if (schemaVersion >= 2 && kind == GameplayItemKind.Unspecified)
        {
            throw new InvalidOperationException($"Gameplay schema v2 item \"{item.Id}\" must declare kind in \"{filePath}\".");
        }

        if (kind == GameplayItemKind.Unspecified)
        {
            kind = InferLegacyItemKind(item);
        }

        if (schemaVersion >= 2 && kind == GameplayItemKind.Ability && item.Ability is null)
        {
            throw new InvalidOperationException($"Gameplay schema v2 ability \"{item.Id}\" must declare ability metadata in \"{filePath}\".");
        }

        if (schemaVersion >= 2 && kind == GameplayItemKind.Weapon && item.Ability is not null)
        {
            throw new InvalidOperationException(
                $"Gameplay schema v2 weapon \"{item.Id}\" cannot embed ability metadata; reference a standalone ability through grantedAbilityItemIds in \"{filePath}\".");
        }

        var weaponSlot = item.WeaponSlot;
        if (kind == GameplayItemKind.Weapon)
        {
            if (schemaVersion >= 2 && weaponSlot is null)
            {
                throw new InvalidOperationException($"Gameplay schema v2 weapon \"{item.Id}\" must declare weaponSlot in \"{filePath}\".");
            }

            weaponSlot ??= item.Slot == GameplayEquipmentSlot.Primary
                || (schemaVersion == 1 && LegacyAlternatePrimaryItemIds.Contains(item.Id))
                ? GameplayWeaponSlot.Primary
                : GameplayWeaponSlot.Secondary;
        }
        else if (weaponSlot is not null)
        {
            throw new InvalidOperationException($"Gameplay ability item \"{item.Id}\" cannot declare weaponSlot in \"{filePath}\".");
        }

        var compatibilitySlot = schemaVersion == 1
            ? item.Slot
            : kind == GameplayItemKind.Weapon
                ? weaponSlot == GameplayWeaponSlot.Primary
                    ? GameplayEquipmentSlot.Primary
                    : GameplayEquipmentSlot.Secondary
                : ResolveCompatibilityAbilitySlot(item.Ability, item.Slot);
        var grantedAbilityItemIds = (item.GrantedAbilityItemIds ?? [])
            .Where(static itemId => !string.IsNullOrWhiteSpace(itemId))
            .Select(static itemId => itemId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (kind != GameplayItemKind.Weapon && grantedAbilityItemIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Gameplay ability item \"{item.Id}\" cannot declare grantedAbilityItemIds in \"{filePath}\".");
        }

        return item with
        {
            Kind = kind,
            WeaponSlot = weaponSlot,
            Slot = compatibilitySlot,
            Ammo = item.Ammo ?? new GameplayItemAmmoDefinition(),
            Presentation = item.Presentation ?? new GameplayItemPresentationDefinition(),
            GrantedAbilityItemIds = grantedAbilityItemIds,
        };
    }

    private static GameplayItemKind InferLegacyItemKind(GameplayItemDefinition item)
    {
        if (item.Slot == GameplayEquipmentSlot.Primary
            || item.BehaviorId.StartsWith("builtin.weapon.", StringComparison.Ordinal)
            || (item.Ammo?.MaxAmmo ?? 0) > 0)
        {
            return GameplayItemKind.Weapon;
        }

        return GameplayItemKind.Ability;
    }

    private static GameplayEquipmentSlot ResolveCompatibilityAbilitySlot(
        GameplayAbilityDefinition? ability,
        GameplayEquipmentSlot legacySlot)
    {
        var channel = ability?.Channel;
        if (string.IsNullOrWhiteSpace(channel))
        {
            channel = ToAbilityChannel(ability?.Category, legacySlot);
        }

        return string.Equals(channel, GameplayAbilityConstants.SpecialChannel, StringComparison.Ordinal)
            ? GameplayEquipmentSlot.Secondary
            : GameplayEquipmentSlot.Utility;
    }

    private static GameplayClassLoadoutDefinition NormalizeLoadoutDefinition(
        GameplayClassLoadoutDefinition loadout,
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        int schemaVersion,
        string classId,
        string filePath)
    {
        return schemaVersion >= 2
            ? NormalizeSchemaV2Loadout(loadout, classId, filePath)
            : NormalizeLegacyLoadout(loadout, items);
    }

    private static GameplayClassLoadoutDefinition NormalizeSchemaV2Loadout(
        GameplayClassLoadoutDefinition loadout,
        string classId,
        string filePath)
    {
        if (loadout.Primary is null)
        {
            throw new InvalidOperationException(
                $"Gameplay schema v2 class \"{classId}\" loadout \"{loadout.Id}\" must declare primary in \"{filePath}\".");
        }

        var defaultPrimaryItemId = loadout.Primary.DefaultItemId?.Trim() ?? string.Empty;
        var primaryItemIds = loadout.Primary.ItemIds
            .Where(static itemId => !string.IsNullOrWhiteSpace(itemId))
            .Select(static itemId => itemId.Trim())
            .Prepend(defaultPrimaryItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var normalizedPrimary = loadout.Primary with
        {
            DefaultItemId = defaultPrimaryItemId,
            ItemIds = primaryItemIds,
            SwitchPolicy = loadout.Primary.SwitchPolicy?.Trim() ?? string.Empty,
            SelectionPersistence = loadout.Primary.SelectionPersistence?.Trim() ?? string.Empty,
        };
        if (!string.Equals(
                normalizedPrimary.SwitchPolicy,
                GameplayLoadoutPolicies.PrimarySwapStation,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Gameplay schema v2 class \"{classId}\" loadout \"{loadout.Id}\" declared unsupported primary switchPolicy \"{normalizedPrimary.SwitchPolicy}\" in \"{filePath}\".");
        }

        if (!string.Equals(
                normalizedPrimary.SelectionPersistence,
                GameplayLoadoutPolicies.SameClassLoadout,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Gameplay schema v2 class \"{classId}\" loadout \"{loadout.Id}\" declared unsupported primary selectionPersistence \"{normalizedPrimary.SelectionPersistence}\" in \"{filePath}\".");
        }
        var secondaryItemId = string.IsNullOrWhiteSpace(loadout.Secondary?.ItemId)
            ? null
            : loadout.Secondary.ItemId.Trim();
        var abilities = loadout.Abilities
            .Where(static itemId => !string.IsNullOrWhiteSpace(itemId))
            .Select(static itemId => itemId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return loadout with
        {
            Primary = normalizedPrimary,
            Secondary = secondaryItemId is null ? null : new GameplaySecondaryLoadoutDefinition(secondaryItemId),
            Abilities = abilities,
            PrimaryItemId = defaultPrimaryItemId,
            SecondaryItemId = secondaryItemId,
            UtilityItemId = null,
            AbilityItemIds = abilities,
        };
    }

    private static GameplayClassLoadoutDefinition NormalizeLegacyLoadout(
        GameplayClassLoadoutDefinition loadout,
        IReadOnlyDictionary<string, GameplayItemDefinition> items)
    {
        var primaryItemIds = new List<string>();
        AddDistinctItemId(primaryItemIds, loadout.PrimaryItemId);

        var legacySecondaryIsAlternatePrimary = !string.IsNullOrWhiteSpace(loadout.SecondaryItemId)
            && LegacyAlternatePrimaryItemIds.Contains(loadout.SecondaryItemId)
            && items.TryGetValue(loadout.SecondaryItemId, out var legacyAlternatePrimary)
            && legacyAlternatePrimary.Kind == GameplayItemKind.Weapon;
        if (legacySecondaryIsAlternatePrimary)
        {
            AddDistinctItemId(primaryItemIds, loadout.SecondaryItemId);
        }

        string? canonicalSecondaryItemId = null;
        if (!legacySecondaryIsAlternatePrimary
            && TryGetItemOfKind(items, loadout.SecondaryItemId, GameplayItemKind.Weapon, out var legacySecondaryWeapon))
        {
            canonicalSecondaryItemId = legacySecondaryWeapon.Id;
        }
        else if (TryGetItemOfKind(items, loadout.UtilityItemId, GameplayItemKind.Weapon, out var legacyUtilityWeapon))
        {
            canonicalSecondaryItemId = legacyUtilityWeapon.Id;
        }

        var abilities = new List<string>();
        if (loadout.AbilityItemIds is not null)
        {
            foreach (var abilityItemId in loadout.AbilityItemIds)
            {
                AddDistinctItemId(abilities, abilityItemId);
            }
        }

        if (TryGetItemOfKind(items, loadout.SecondaryItemId, GameplayItemKind.Ability, out var legacySecondaryAbility))
        {
            AddDistinctItemId(abilities, legacySecondaryAbility.Id);
        }

        if (TryGetItemOfKind(items, loadout.UtilityItemId, GameplayItemKind.Ability, out var legacyUtilityAbility))
        {
            AddDistinctItemId(abilities, legacyUtilityAbility.Id);
        }

        return loadout with
        {
            Primary = new GameplayPrimaryLoadoutDefinition(
                loadout.PrimaryItemId,
                primaryItemIds),
            Secondary = canonicalSecondaryItemId is null
                ? null
                : new GameplaySecondaryLoadoutDefinition(canonicalSecondaryItemId),
            Abilities = abilities,
        };
    }

    private static bool TryGetItemOfKind(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        string? itemId,
        GameplayItemKind kind,
        out GameplayItemDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(itemId)
            && items.TryGetValue(itemId, out var candidate)
            && candidate.Kind == kind)
        {
            item = candidate;
            return true;
        }

        item = null!;
        return false;
    }

    private static void AddDistinctItemId(List<string> itemIds, string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        var normalizedItemId = itemId.Trim();
        if (!itemIds.Contains(normalizedItemId, StringComparer.Ordinal))
        {
            itemIds.Add(normalizedItemId);
        }
    }

    private static void ValidateCanonicalLoadout(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        GameplayClassLoadoutDefinition loadout,
        string classId,
        string filePath)
    {
        if (loadout.Primary is null)
        {
            throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadout.Id}\" has no normalized primary definition in \"{filePath}\".");
        }

        ValidateRequiredText(loadout.Primary.DefaultItemId, nameof(GameplayPrimaryLoadoutDefinition.DefaultItemId), filePath);
        if (loadout.Primary.ItemIds.Count == 0
            || !loadout.Primary.ItemIds.Contains(loadout.Primary.DefaultItemId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadout.Id}\" primary itemIds must include its default item in \"{filePath}\".");
        }

        foreach (var primaryItemId in loadout.Primary.ItemIds)
        {
            ValidateCanonicalWeaponItem(items, primaryItemId, GameplayWeaponSlot.Primary, classId, loadout.Id, filePath);
        }

        if (loadout.Secondary is not null)
        {
            ValidateCanonicalWeaponItem(items, loadout.Secondary.ItemId, GameplayWeaponSlot.Secondary, classId, loadout.Id, filePath);
        }

        ValidateCanonicalAbilityItems(items, loadout.Abilities, classId, loadout.Id, filePath);
        ValidateEffectiveAbilityChannels(items, loadout, classId, filePath);
    }

    private static void ValidateEffectiveAbilityChannels(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        GameplayClassLoadoutDefinition loadout,
        string classId,
        string filePath)
    {
        foreach (var primaryItemId in loadout.Primary!.ItemIds)
        {
            ValidateEquippedWeaponChannels(primaryItemId, "primary");
            if (loadout.Secondary is not null)
            {
                ValidateEquippedWeaponChannels(loadout.Secondary.ItemId, "secondary");
            }

            void ValidateEquippedWeaponChannels(string weaponItemId, string source)
            {
                var activeChannels = new Dictionary<string, string>(StringComparer.Ordinal);
                var seenAbilityItemIds = new HashSet<string>(StringComparer.Ordinal);
                AddAbilityChannels(loadout.Abilities, "loadout", activeChannels, seenAbilityItemIds);
                AddWeaponAbilityChannels(weaponItemId, source, activeChannels, seenAbilityItemIds);
            }

            void AddWeaponAbilityChannels(
                string weaponItemId,
                string source,
                Dictionary<string, string> channels,
                HashSet<string> seenIds)
            {
                var weaponItem = items[weaponItemId];
                AddAbilityChannels(weaponItem.GrantedAbilityItemIds, source, channels, seenIds);
                if (weaponItem.Ability is not null && seenIds.Add(weaponItem.Id))
                {
                    AddActiveChannel(weaponItem.Id, weaponItem.Ability, source, channels);
                }
            }

            void AddAbilityChannels(
                IReadOnlyList<string> abilityItemIds,
                string source,
                Dictionary<string, string> channels,
                HashSet<string> seenIds)
            {
                foreach (var abilityItemId in abilityItemIds)
                {
                    if (!seenIds.Add(abilityItemId))
                    {
                        continue;
                    }

                    var abilityItem = items[abilityItemId];
                    AddActiveChannel(abilityItem.Id, abilityItem.Ability!, source, channels);
                }
            }

            void AddActiveChannel(
                string abilityItemId,
                GameplayAbilityDefinition ability,
                string source,
                Dictionary<string, string> channels)
            {
                if (ability.Channel is not (GameplayAbilityConstants.SpecialChannel or GameplayAbilityConstants.UtilityChannel))
                {
                    return;
                }

                if (channels.TryAdd(ability.Channel, $"{source} ability \"{abilityItemId}\""))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Gameplay class \"{classId}\" loadout \"{loadout.Id}\" primary selection \"{primaryItemId}\" combines {channels[ability.Channel]} and {source} ability \"{abilityItemId}\" on active channel \"{ability.Channel}\" in \"{filePath}\".");
            }
        }
    }

    private static void ValidateCanonicalWeaponItem(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        string itemId,
        GameplayWeaponSlot expectedSlot,
        string classId,
        string loadoutId,
        string filePath)
    {
        if (!items.TryGetValue(itemId, out var item))
        {
            throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadoutId}\" references unknown weapon \"{itemId}\" in \"{filePath}\".");
        }

        if (item.Kind != GameplayItemKind.Weapon || item.WeaponSlot != expectedSlot)
        {
            throw new InvalidOperationException(
                $"Gameplay class \"{classId}\" loadout \"{loadoutId}\" expected \"{itemId}\" to be a {expectedSlot} weapon in \"{filePath}\".");
        }
    }

    private static void ValidateCanonicalAbilityItems(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        IReadOnlyList<string> itemIds,
        string classId,
        string loadoutId,
        string filePath)
    {
        var activeChannels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var itemId in itemIds)
        {
            if (!items.TryGetValue(itemId, out var item)
                || item.Kind != GameplayItemKind.Ability
                || item.Ability is null)
            {
                throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadoutId}\" references invalid ability \"{itemId}\" in \"{filePath}\".");
            }

            if (string.Equals(
                    item.Ability.Category,
                    GameplayAbilityConstants.WeaponAltFireCategory,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Gameplay class \"{classId}\" loadout \"{loadoutId}\" cannot attach weaponAltFire ability \"{itemId}\" directly; grant it from a weapon item in \"{filePath}\".");
            }

            var channel = item.Ability.Channel;
            if (channel is GameplayAbilityConstants.SpecialChannel or GameplayAbilityConstants.UtilityChannel
                && !activeChannels.Add(channel))
            {
                throw new InvalidOperationException(
                    $"Gameplay class \"{classId}\" loadout \"{loadoutId}\" declares more than one active ability for channel \"{channel}\" in \"{filePath}\".");
            }
        }
    }

    private static void ValidateGrantedAbilityItems(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        string packDirectory)
    {
        foreach (var item in items.Values)
        {
            var activeChannels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var abilityItemId in item.GrantedAbilityItemIds)
            {
                if (!items.TryGetValue(abilityItemId, out var abilityItem)
                    || abilityItem.Kind != GameplayItemKind.Ability
                    || abilityItem.Ability is null)
                {
                    throw new InvalidOperationException(
                        $"Gameplay item \"{item.Id}\" grants invalid ability \"{abilityItemId}\" in \"{packDirectory}\".");
                }

                var channel = abilityItem.Ability.Channel;
                if (channel is GameplayAbilityConstants.SpecialChannel or GameplayAbilityConstants.UtilityChannel
                    && !activeChannels.Add(channel))
                {
                    throw new InvalidOperationException(
                        $"Gameplay item \"{item.Id}\" grants more than one active ability for channel \"{channel}\" in \"{packDirectory}\".");
                }
            }
        }
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

    private static void ValidateReferencedAbilityItems(
        IReadOnlyDictionary<string, GameplayItemDefinition> items,
        IReadOnlyList<string>? itemIds,
        string classId,
        string loadoutId,
        string filePath)
    {
        if (itemIds is null)
        {
            return;
        }

        foreach (var itemId in itemIds)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !items.TryGetValue(itemId, out var item))
            {
                throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadoutId}\" references unknown ability item \"{itemId}\" in \"{filePath}\".");
            }

            if (item.Ability is null)
            {
                throw new InvalidOperationException($"Gameplay class \"{classId}\" loadout \"{loadoutId}\" ability item \"{itemId}\" does not declare ability metadata in \"{filePath}\".");
            }
        }
    }

    private static void ValidateRequiredText(string? value, string fieldName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required gameplay field \"{fieldName}\" was empty in \"{filePath}\".");
        }
    }

    private static GameplayItemPresentationDefinition NormalizeItemPresentation(GameplayItemPresentationDefinition presentation, string filePath)
    {
        var hud = NormalizeHudPresentation(presentation.Hud, presentation.HudSpriteName, filePath);
        return presentation with
        {
            Hud = hud,
        };
    }

    private static GameplayItemHudPresentationDefinition? NormalizeHudPresentation(
        GameplayItemHudPresentationDefinition? hud,
        string? hudSpriteName,
        string filePath)
    {
        if (hud is null)
        {
            return null;
        }

        var displayKind = hud.DisplayKind?.Trim() ?? string.Empty;
        var stackGroup = hud.StackGroup?.Trim() ?? string.Empty;
        var stateProvider = hud.StateProvider?.Trim() ?? string.Empty;
        var stateOwner = hud.StateOwner?.Trim() ?? string.Empty;
        var cooldownKey = hud.CooldownKey?.Trim() ?? string.Empty;
        var activeKey = hud.ActiveKey?.Trim() ?? string.Empty;
        var disabledKey = hud.DisabledKey?.Trim() ?? string.Empty;
        var widgetId = hud.WidgetId?.Trim() ?? string.Empty;
        var widgetOwner = hud.WidgetOwner?.Trim() ?? string.Empty;
        var widgetCallback = hud.WidgetCallback?.Trim() ?? string.Empty;
        var anchor = hud.Anchor?.Trim() ?? string.Empty;

        if (displayKind.Length > 0 && !IsKnownHudDisplayKind(displayKind))
        {
            throw new InvalidOperationException($"Gameplay HUD metadata declared unsupported display kind \"{displayKind}\" in \"{filePath}\".");
        }

        if (stackGroup.Length > 0 && !IsKnownHudStackGroup(stackGroup))
        {
            throw new InvalidOperationException($"Gameplay HUD metadata declared unsupported stack group \"{stackGroup}\" in \"{filePath}\".");
        }

        if (stateProvider.Length > 0 && !IsKnownHudStateProvider(stateProvider))
        {
            throw new InvalidOperationException($"Gameplay HUD metadata declared unsupported state provider \"{stateProvider}\" in \"{filePath}\".");
        }

        if (string.Equals(displayKind, GameplayItemHudDisplayKinds.AmmoPanel, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(hudSpriteName))
        {
            throw new InvalidOperationException($"Gameplay HUD ammo panel metadata requires a presentation hudSpriteName in \"{filePath}\".");
        }

        if (string.Equals(displayKind, GameplayItemHudDisplayKinds.Custom, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(widgetCallback) && string.IsNullOrWhiteSpace(widgetId))
            {
                throw new InvalidOperationException($"Gameplay HUD custom metadata requires widgetId or widgetCallback in \"{filePath}\".");
            }

            if (!string.IsNullOrWhiteSpace(widgetCallback)
                && string.IsNullOrWhiteSpace(widgetOwner)
                && string.IsNullOrWhiteSpace(stateOwner))
            {
                throw new InvalidOperationException($"Gameplay HUD custom metadata requires widgetOwner or stateOwner in \"{filePath}\".");
            }
        }

        return hud with
        {
            DisplayKind = displayKind,
            StackGroup = stackGroup,
            StateProvider = stateProvider,
            StateOwner = stateOwner,
            CooldownKey = cooldownKey,
            MaxCooldown = Math.Max(0, hud.MaxCooldown),
            ActiveKey = activeKey,
            DisabledKey = disabledKey,
            WidgetId = widgetId,
            WidgetOwner = widgetOwner,
            WidgetCallback = widgetCallback,
            Anchor = anchor,
        };
    }

    private static bool IsKnownHudDisplayKind(string displayKind)
    {
        return string.Equals(displayKind, GameplayItemHudDisplayKinds.None, StringComparison.Ordinal)
            || string.Equals(displayKind, GameplayItemHudDisplayKinds.AmmoPanel, StringComparison.Ordinal)
            || string.Equals(displayKind, GameplayItemHudDisplayKinds.Meter, StringComparison.Ordinal)
            || string.Equals(displayKind, GameplayItemHudDisplayKinds.CooldownIcon, StringComparison.Ordinal)
            || string.Equals(displayKind, GameplayItemHudDisplayKinds.Custom, StringComparison.Ordinal)
            || string.Equals(displayKind, GameplayItemHudDisplayKinds.Count, StringComparison.Ordinal)
            || string.Equals(displayKind, GameplayItemHudDisplayKinds.Prompt, StringComparison.Ordinal);
    }

    private static bool IsKnownHudStackGroup(string stackGroup)
    {
        return string.Equals(stackGroup, GameplayItemHudStackGroups.Weapon, StringComparison.Ordinal)
            || string.Equals(stackGroup, GameplayItemHudStackGroups.Ability, StringComparison.Ordinal)
            || string.Equals(stackGroup, GameplayItemHudStackGroups.Status, StringComparison.Ordinal);
    }

    private static bool IsKnownHudStateProvider(string stateProvider)
    {
        return string.Equals(stateProvider, GameplayItemHudStateProviders.PrimaryAmmo, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.SecondaryAmmo, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.UtilityAmmo, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.ReloadProgress, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.Cooldown, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.AbilityCooldown, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.Custom, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.HeavySandvichCooldown, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.HeavyGhostDashCooldown, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.SpySuperjumpCooldown, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.StickyCount, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.Uber, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.Metal, StringComparison.Ordinal)
            || string.Equals(stateProvider, GameplayItemHudStateProviders.Sentry, StringComparison.Ordinal);
    }

    private static GameplayAbilityDefinition? NormalizeAbilityDefinition(GameplayItemDefinition item, string filePath)
    {
        var ability = item.Ability;
        if (ability is null)
        {
            return null;
        }

        var category = string.IsNullOrWhiteSpace(ability.Category)
            ? GetDefaultAbilityCategory(item.Slot)
            : ability.Category.Trim();
        var channel = string.IsNullOrWhiteSpace(ability.Channel)
            ? ToAbilityChannel(category, item.Slot)
            : ability.Channel.Trim();
        var activation = string.IsNullOrWhiteSpace(ability.Activation)
            ? GameplayAbilityConstants.PressedActivation
            : ability.Activation.Trim();
        var executorId = string.IsNullOrWhiteSpace(ability.ExecutorId)
            ? item.BehaviorId.Trim()
            : ability.ExecutorId.Trim();

        ValidateRequiredText(category, nameof(GameplayAbilityDefinition.Category), filePath);
        ValidateRequiredText(channel, nameof(GameplayAbilityDefinition.Channel), filePath);
        ValidateRequiredText(activation, nameof(GameplayAbilityDefinition.Activation), filePath);
        ValidateRequiredText(executorId, nameof(GameplayAbilityDefinition.ExecutorId), filePath);
        if (!IsKnownAbilityChannel(channel))
        {
            throw new InvalidOperationException($"Gameplay ability \"{item.Id}\" declared unsupported channel \"{channel}\" in \"{filePath}\".");
        }

        if (string.Equals(category, GameplayAbilityConstants.WeaponAltFireCategory, StringComparison.Ordinal)
            && !string.Equals(channel, GameplayAbilityConstants.SpecialChannel, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Gameplay weaponAltFire ability \"{item.Id}\" must use channel \"{GameplayAbilityConstants.SpecialChannel}\" in \"{filePath}\".");
        }

        if (!IsKnownAbilityActivation(activation))
        {
            throw new InvalidOperationException($"Gameplay ability \"{item.Id}\" declared unsupported activation \"{activation}\" in \"{filePath}\".");
        }

        var normalizedAbility = ability with
        {
            Category = category,
            Channel = channel,
            Activation = activation,
            ExecutorId = executorId,
            Tags = NormalizeAbilityTags(ability.Tags),
            Parameters = ability.Parameters ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase),
        };
        ValidateKnownAbilityParameters(item.Id, normalizedAbility, filePath);
        return normalizedAbility;
    }

    private static void ValidateKnownAbilityParameters(string itemId, GameplayAbilityDefinition ability, string filePath)
    {
        if (ability.Parameters.Count == 0)
        {
            return;
        }

        switch (ability.ExecutorId)
        {
            case BuiltInGameplayBehaviorIds.PyroAirblast:
                ValidateNumberParameters(itemId, ability, filePath, "cost", "cooldownTicks", "cooldownSeconds", "noFlameTicks", "noFlameSeconds");
                return;
            case BuiltInGameplayBehaviorIds.HeavySandvich:
                ValidateNumberParameters(itemId, ability, filePath, "durationTicks", "durationSeconds", "cooldownTicks", "cooldownSeconds", "totalHeal");
                return;
            case BuiltInGameplayBehaviorIds.MedicNeedlegun:
                ValidateNumberParameters(itemId, ability, filePath, "fireCooldownTicks", "fireCooldownSeconds", "refillTicks", "refillSeconds");
                return;
            case BuiltInGameplayBehaviorIds.MedicKritzBeam:
                ValidateNumberParameters(itemId, ability, filePath, "range", "damagePerSecond", "chargePerTick");
                return;
            case BuiltInGameplayBehaviorIds.MedicKritzHealNeedles:
                ValidateNumberParameters(itemId, ability, filePath, "healPerHit", "enemyDamagePerHit");
                return;
            case BuiltInGameplayBehaviorIds.SpySuperjump:
                ValidateNumberParameters(itemId, ability, filePath, "maxChargeTicks", "cooldownTicks", "cooldownSeconds", "minVelocity", "maxVelocity");
                return;
            case BuiltInGameplayBehaviorIds.QuoteBladeThrow:
                ValidateNumberParameters(itemId, ability, filePath, "energyCost", "activeProjectileLimit", "lifetimeTicks");
                return;
            case BuiltInGameplayBehaviorIds.CivvieUmbrella:
                ValidateNumberParameters(itemId, ability, filePath, "maxChargeTicks", "holdDrainPerTick", "rechargePerTick", "impactDrain", "brokenRechargeDelayTicks");
                return;
            case BuiltInGameplayBehaviorIds.CivvieTaunt:
                ValidateNumberParameters(itemId, ability, filePath, "healAmount", "healRadius", "healFrameIndex");
                return;
            case BuiltInGameplayBehaviorIds.CivviePogo:
                ValidateNumberParameters(itemId, ability, filePath, "baseBounceJumpScale", "superJumpScale", "crunchDurationTicks");
                return;
            case BuiltInGameplayBehaviorIds.HeavyGhostDash:
                ValidateNumberParameters(itemId, ability, filePath, "durationTicks", "durationSeconds", "movementDurationTicks", "movementDurationSeconds", "cooldownTicks", "cooldownSeconds", "impulse", "nextAttackDamageMultiplier", "slideVelocityPerTick", "burstSpeedMultiplier");
                ValidateBoolParameters(itemId, ability, filePath, "useMomentum", "disableGravity", "enableGhostTrail");
                return;
        }
    }

    private static void ValidateNumberParameters(string itemId, GameplayAbilityDefinition ability, string filePath, params string[] parameterNames)
    {
        for (var index = 0; index < parameterNames.Length; index += 1)
        {
            var parameterName = parameterNames[index];
            if (ability.Parameters.TryGetValue(parameterName, out var parameter)
                && parameter.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException($"Gameplay ability \"{itemId}\" parameter \"{parameterName}\" must be numeric in \"{filePath}\".");
            }
        }
    }

    private static void ValidateBoolParameters(string itemId, GameplayAbilityDefinition ability, string filePath, params string[] parameterNames)
    {
        for (var index = 0; index < parameterNames.Length; index += 1)
        {
            var parameterName = parameterNames[index];
            if (ability.Parameters.TryGetValue(parameterName, out var parameter)
                && parameter.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new InvalidOperationException($"Gameplay ability \"{itemId}\" parameter \"{parameterName}\" must be boolean in \"{filePath}\".");
            }
        }
    }

    private static string GetDefaultAbilityCategory(GameplayEquipmentSlot slot)
    {
        return slot == GameplayEquipmentSlot.Utility
            ? GameplayAbilityConstants.UtilityCategory
            : GameplayAbilityConstants.SecondaryCategory;
    }

    private static string ToAbilityChannel(string? category, GameplayEquipmentSlot slot)
    {
        if (string.Equals(category, GameplayAbilityConstants.WeaponAltFireCategory, StringComparison.Ordinal))
        {
            return GameplayAbilityConstants.SpecialChannel;
        }

        if (string.Equals(category, GameplayAbilityConstants.SecondaryCategory, StringComparison.Ordinal))
        {
            return GameplayAbilityConstants.SpecialChannel;
        }

        if (string.Equals(category, GameplayAbilityConstants.UtilityCategory, StringComparison.Ordinal))
        {
            return GameplayAbilityConstants.UtilityChannel;
        }

        if (string.Equals(category, GameplayAbilityConstants.PassiveCategory, StringComparison.Ordinal))
        {
            return GameplayAbilityConstants.PassiveChannel;
        }

        if (string.Equals(category, GameplayAbilityConstants.TauntCategory, StringComparison.Ordinal))
        {
            return GameplayAbilityConstants.TauntChannel;
        }

        return slot == GameplayEquipmentSlot.Utility
            ? GameplayAbilityConstants.UtilityChannel
            : GameplayAbilityConstants.SpecialChannel;
    }

    private static bool IsKnownAbilityChannel(string channel)
    {
        return string.Equals(channel, GameplayAbilityConstants.SpecialChannel, StringComparison.Ordinal)
            || string.Equals(channel, GameplayAbilityConstants.UtilityChannel, StringComparison.Ordinal)
            || string.Equals(channel, GameplayAbilityConstants.PassiveChannel, StringComparison.Ordinal)
            || string.Equals(channel, GameplayAbilityConstants.TauntChannel, StringComparison.Ordinal);
    }

    private static bool IsKnownAbilityActivation(string activation)
    {
        return string.Equals(activation, GameplayAbilityConstants.PressedActivation, StringComparison.Ordinal)
            || string.Equals(activation, GameplayAbilityConstants.HeldActivation, StringComparison.Ordinal)
            || string.Equals(activation, GameplayAbilityConstants.ReleasedActivation, StringComparison.Ordinal)
            || string.Equals(activation, GameplayAbilityConstants.PassiveTickActivation, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> NormalizeAbilityTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return Array.Empty<string>();
        }

        return tags
            .Select(static tag => tag?.Trim() ?? string.Empty)
            .Where(static tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static GameplaySpriteMaskDefinition? NormalizeMask(GameplaySpriteMaskDefinition? mask)
    {
        if (mask is null)
        {
            return null;
        }

        return mask with
        {
            Shape = mask.Shape?.Trim() ?? string.Empty,
            BoundsMode = mask.BoundsMode?.Trim() ?? string.Empty,
        };
    }

    private static GameplayClassPresentationDefinition? NormalizePresentation(GameplayClassPresentationDefinition? presentation)
    {
        if (presentation is null)
        {
            return null;
        }

        ValidateRequiredText(presentation.SpritePrefix, nameof(GameplayClassPresentationDefinition.SpritePrefix), nameof(GameplayClassPresentationDefinition));
        return presentation with
        {
            SpritePrefix = presentation.SpritePrefix.Trim(),
            BaseSuffix = string.IsNullOrWhiteSpace(presentation.BaseSuffix) ? "S" : presentation.BaseSuffix.Trim(),
            StandSuffix = NormalizeOptionalPresentationSuffix(presentation.StandSuffix),
            WalkSuffix = NormalizeOptionalPresentationSuffix(presentation.WalkSuffix),
            RunSuffix = NormalizeOptionalPresentationSuffix(presentation.RunSuffix),
            JumpSuffix = NormalizeOptionalPresentationSuffix(presentation.JumpSuffix),
            LeanLeftSuffix = NormalizeOptionalPresentationSuffix(presentation.LeanLeftSuffix),
            LeanRightSuffix = NormalizeOptionalPresentationSuffix(presentation.LeanRightSuffix),
            TauntSuffix = NormalizeOptionalPresentationSuffix(presentation.TauntSuffix),
            PogoSuffix = NormalizeOptionalPresentationSuffix(presentation.PogoSuffix),
            PogoTrickSuffix = NormalizeOptionalPresentationSuffix(presentation.PogoTrickSuffix),
            PogoIntelSuffix = NormalizeOptionalPresentationSuffix(presentation.PogoIntelSuffix),
            HumiliationSuffix = NormalizeOptionalPresentationSuffix(presentation.HumiliationSuffix),
            DeadSuffix = NormalizeOptionalPresentationSuffix(presentation.DeadSuffix),
            IntelSuffix = NormalizeOptionalPresentationSuffix(presentation.IntelSuffix),
            ScopedSuffix = NormalizeOptionalPresentationSuffix(presentation.ScopedSuffix),
            HeavyEatSuffix = NormalizeOptionalPresentationSuffix(presentation.HeavyEatSuffix),
        };
    }

    private static GameplayClassRuntimeDefinition? NormalizeRuntime(GameplayClassRuntimeDefinition? runtime, string filePath)
    {
        if (runtime is null)
        {
            return null;
        }

        ValidateRequiredText(runtime.PrimaryWeaponKillFeedSprite, nameof(GameplayClassRuntimeDefinition.PrimaryWeaponKillFeedSprite), filePath);
        var playerClass = runtime.PlayerClass?.Trim() ?? string.Empty;
        var basePlayerClass = runtime.BasePlayerClass?.Trim() ?? string.Empty;
        if (basePlayerClass.Length == 0)
        {
            basePlayerClass = playerClass.Length == 0 ? nameof(PlayerClass.Scout) : playerClass;
        }

        var botGraphPlayerClass = runtime.BotGraphPlayerClass?.Trim() ?? string.Empty;
        if (botGraphPlayerClass.Length == 0)
        {
            botGraphPlayerClass = basePlayerClass;
        }

        return runtime with
        {
            PlayerClass = playerClass,
            BasePlayerClass = basePlayerClass,
            BotGraphPlayerClass = botGraphPlayerClass,
            PrimaryWeaponKillFeedSprite = runtime.PrimaryWeaponKillFeedSprite.Trim(),
        };
    }

    private static string? NormalizeOptionalPresentationSuffix(string? suffix)
    {
        return string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();
    }

    private static string NormalizeAndValidatePackRelativeFilePath(string packDirectory, string? relativePath, string assetId, string filePath)
    {
        ValidateRequiredText(relativePath, "framePaths", filePath);
        var normalizedRelativePath = relativePath!.Trim().Replace('\\', '/');
        if (normalizedRelativePath.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Gameplay asset \"{assetId}\" frame path must be relative to its pack in \"{filePath}\": {relativePath}");
        }

        var pathSegments = normalizedRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(normalizedRelativePath)
            || normalizedRelativePath.StartsWith('/')
            || normalizedRelativePath.Contains(':', StringComparison.Ordinal)
            || pathSegments.Any(static segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"Gameplay asset \"{assetId}\" frame path escapes pack directory in \"{filePath}\": {relativePath}");
        }

        var fullPackDirectory = Path.GetFullPath(packDirectory);
        var combinedPath = Path.GetFullPath(Path.Combine(fullPackDirectory, normalizedRelativePath));
        var packDirectoryPrefix = fullPackDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!combinedPath.StartsWith(packDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Gameplay asset \"{assetId}\" frame path escapes pack directory in \"{filePath}\": {relativePath}");
        }

        return normalizedRelativePath;
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
        string Version,
        int SchemaVersion = 1);
}
