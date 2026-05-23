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
                    Presentation = NormalizeItemPresentation(item.Presentation ?? new GameplayItemPresentationDefinition(), filePath),
                    Ownership = item.Ownership ?? new GameplayItemOwnershipDefinition(),
                    Ability = NormalizeAbilityDefinition(item, filePath),
                };
            });
        var itemsById = items.ToDictionary(static item => item.Id, StringComparer.Ordinal);
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

                foreach (var loadout in gameplayClass.Loadouts.Values)
                {
                    ValidateRequiredText(loadout.Id, nameof(GameplayClassLoadoutDefinition.Id), filePath);
                    ValidateRequiredText(loadout.DisplayName, nameof(GameplayClassLoadoutDefinition.DisplayName), filePath);
                    ValidateRequiredText(loadout.PrimaryItemId, nameof(GameplayClassLoadoutDefinition.PrimaryItemId), filePath);
                    ValidateReferencedItem(itemsById, loadout.PrimaryItemId, GameplayEquipmentSlot.Primary, gameplayClass.Id, loadout.Id, filePath);
                    ValidateOptionalReferencedItem(itemsById, loadout.SecondaryItemId, GameplayEquipmentSlot.Secondary, gameplayClass.Id, loadout.Id, filePath);
                    ValidateOptionalReferencedItem(itemsById, loadout.UtilityItemId, GameplayEquipmentSlot.Utility, gameplayClass.Id, loadout.Id, filePath);
                    ValidateReferencedAbilityItems(itemsById, loadout.AbilityItemIds, gameplayClass.Id, loadout.Id, filePath);
                }

                return gameplayClass with
                {
                    Presentation = NormalizePresentation(gameplayClass.Presentation),
                    Runtime = NormalizeRuntime(gameplayClass.Runtime, filePath),
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
            Assets: new GameplayModPackAssetCatalog(spritesById));
    }

    public static string? FindPackDirectory(string packDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectoryName);

        var runtimePath = ContentRoot.GetPath("Gameplay", packDirectoryName);
        if (Directory.Exists(runtimePath) && HasPackMetadata(runtimePath))
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
        if (Directory.Exists(runtimePath) && HasAnyPackMetadata(runtimePath))
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
        foreach (var filePath in Directory.GetFiles(fullDirectory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
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
        var activation = string.IsNullOrWhiteSpace(ability.Activation)
            ? GameplayAbilityConstants.PressedActivation
            : ability.Activation.Trim();
        var executorId = string.IsNullOrWhiteSpace(ability.ExecutorId)
            ? item.BehaviorId.Trim()
            : ability.ExecutorId.Trim();

        ValidateRequiredText(category, nameof(GameplayAbilityDefinition.Category), filePath);
        ValidateRequiredText(activation, nameof(GameplayAbilityDefinition.Activation), filePath);
        ValidateRequiredText(executorId, nameof(GameplayAbilityDefinition.ExecutorId), filePath);
        if (!IsKnownAbilityActivation(activation))
        {
            throw new InvalidOperationException($"Gameplay ability \"{item.Id}\" declared unsupported activation \"{activation}\" in \"{filePath}\".");
        }

        var normalizedAbility = ability with
        {
            Category = category,
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
            case BuiltInGameplayBehaviorIds.SpySuperjump:
                ValidateNumberParameters(itemId, ability, filePath, "maxChargeTicks", "cooldownTicks", "cooldownSeconds", "minVelocity", "maxVelocity");
                return;
            case BuiltInGameplayBehaviorIds.QuoteBladeThrow:
                ValidateNumberParameters(itemId, ability, filePath, "energyCost", "activeProjectileLimit", "lifetimeTicks");
                return;
            case BuiltInGameplayBehaviorIds.HeavyGhostDash:
                ValidateNumberParameters(itemId, ability, filePath, "durationTicks", "durationSeconds", "movementDurationTicks", "movementDurationSeconds", "cooldownTicks", "cooldownSeconds", "impulse", "nextAttackDamageMultiplier");
                ValidateBoolParameters(itemId, ability, filePath, "useMomentum");
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
        const string contentPrefix = "Content/";
        if (normalizedRelativePath.StartsWith(contentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var contentRelativePath = normalizedRelativePath[contentPrefix.Length..];
            if (string.IsNullOrWhiteSpace(contentRelativePath) || contentRelativePath.Contains("..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Gameplay asset \"{assetId}\" frame path escapes content root in \"{filePath}\": {relativePath}");
            }

            var fullContentRoot = Path.GetFullPath(ContentRoot.Path);
            var combinedContentPath = Path.GetFullPath(ContentRoot.GetPath(contentRelativePath));
            if (!combinedContentPath.StartsWith(fullContentRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Gameplay asset \"{assetId}\" frame path escapes content root in \"{filePath}\": {relativePath}");
            }

            return normalizedRelativePath;
        }

        var fullPackDirectory = Path.GetFullPath(packDirectory);
        var combinedPath = Path.GetFullPath(Path.Combine(fullPackDirectory, normalizedRelativePath));
        if (!combinedPath.StartsWith(fullPackDirectory, StringComparison.OrdinalIgnoreCase))
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
        string Version);
}
