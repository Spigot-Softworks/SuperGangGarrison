using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class GameplayRuntimeRegistry
{
    private readonly Dictionary<string, GameplayModPackDefinition> _modPacks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayItemDefinition> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayClassDefinition> _classes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _itemOwningModPackIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _classOwningModPackIds = new(StringComparer.Ordinal);
    private readonly Dictionary<PlayerClass, GameplayClassRuntimeBinding> _classBindings = new();
    private readonly Dictionary<string, GameplayClassRuntimeBinding> _classBindingsByClassId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayPrimaryWeaponRuntimeBinding> _primaryWeaponBindingsByBehaviorId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IGameplayAbilityExecutor> _abilityExecutorsById = new(StringComparer.Ordinal);
    private bool _abilityDefinitionsSealed;
    private bool _abilityExecutorsSealed;
    private bool _primaryWeaponBehaviorsSealed;

    public IReadOnlyCollection<GameplayModPackDefinition> ModPacks => _modPacks.Values;

    public IReadOnlyCollection<GameplayItemDefinition> Items => _items.Values;

    public IReadOnlyCollection<GameplayClassRuntimeBinding> RuntimeClassBindings => _classBindingsByClassId.Values;

    public void RegisterPrimaryWeaponBehavior(string behaviorId, PrimaryWeaponKind weaponKind)
    {
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(behaviorId, weaponKind));
    }

    public void RegisterPrimaryWeaponBehavior(GameplayPrimaryWeaponRuntimeBinding binding)
    {
        if (!TryRegisterPrimaryWeaponBehavior(binding, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    public bool TryRegisterPrimaryWeaponBehavior(GameplayPrimaryWeaponRuntimeBinding binding, out string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.BehaviorId);
        errorMessage = string.Empty;
        if (_primaryWeaponBehaviorsSealed)
        {
            errorMessage = "Gameplay primary weapon behavior registry is sealed.";
            return false;
        }

        var normalizedBehaviorId = binding.BehaviorId.Trim();
        if (_primaryWeaponBindingsByBehaviorId.ContainsKey(normalizedBehaviorId))
        {
            errorMessage = $"Gameplay primary weapon behavior \"{normalizedBehaviorId}\" is already registered.";
            return false;
        }

        _primaryWeaponBindingsByBehaviorId[normalizedBehaviorId] = binding with { BehaviorId = normalizedBehaviorId };
        return true;
    }

    public void RegisterGameplayAbilityExecutor(string executorId, IGameplayAbilityExecutor executor)
    {
        if (!TryRegisterGameplayAbilityExecutor(executorId, executor, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    public void RegisterGameplayAbilityExecutor(string executorId, Func<GameplayAbilityContext, GameplayAbilityResult> executor)
    {
        RegisterGameplayAbilityExecutor(executorId, new DelegateGameplayAbilityExecutor(executor));
    }

    public bool TryRegisterGameplayAbilityExecutor(string executorId, IGameplayAbilityExecutor executor, out string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executorId);
        ArgumentNullException.ThrowIfNull(executor);
        errorMessage = string.Empty;
        if (_abilityExecutorsSealed)
        {
            errorMessage = "Gameplay ability executor registry is sealed.";
            return false;
        }

        var normalizedExecutorId = executorId.Trim();
        if (_abilityExecutorsById.ContainsKey(normalizedExecutorId))
        {
            errorMessage = $"Gameplay ability executor \"{normalizedExecutorId}\" is already registered.";
            return false;
        }

        _abilityExecutorsById[normalizedExecutorId] = executor;
        return true;
    }

    public void RegisterModPack(GameplayModPackDefinition modPack, IReadOnlyList<GameplayClassRuntimeBinding> classBindings)
    {
        ArgumentNullException.ThrowIfNull(modPack);
        ArgumentNullException.ThrowIfNull(classBindings);
        if (!TryRegisterModPack(modPack, classBindings, allowRuntimeClassBindingOverride: false, out var errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    public bool TryRegisterModPack(
        GameplayModPackDefinition modPack,
        bool allowRuntimeClassBindingOverride,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(modPack);
        return TryRegisterModPack(
            modPack,
            CreateClassBindingsFromRuntimeMetadata(modPack),
            allowRuntimeClassBindingOverride,
            out errorMessage);
    }

    public bool TryRegisterModPack(
        GameplayModPackDefinition modPack,
        IReadOnlyList<GameplayClassRuntimeBinding> classBindings,
        bool allowRuntimeClassBindingOverride,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(modPack);
        ArgumentNullException.ThrowIfNull(classBindings);
        errorMessage = string.Empty;
        if (_abilityDefinitionsSealed)
        {
            errorMessage = "Gameplay mod pack registry is sealed.";
            return false;
        }

        try
        {
            ValidateModPackRegistration(modPack, classBindings, allowRuntimeClassBindingOverride);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        _modPacks[modPack.Id] = modPack;

        foreach (var item in modPack.Items)
        {
            _items[item.Key] = item.Value;
            _itemOwningModPackIds[item.Key] = modPack.Id;
        }

        foreach (var gameplayClass in modPack.Classes)
        {
            _classes[gameplayClass.Key] = gameplayClass.Value;
            _classOwningModPackIds[gameplayClass.Key] = modPack.Id;
        }

        for (var index = 0; index < classBindings.Count; index += 1)
        {
            var classBinding = classBindings[index];
            _classBindingsByClassId[classBinding.ClassId] = classBinding;
            if (classBinding.BindsLegacyPlayerClass)
            {
                _classBindings[classBinding.PlayerClass] = classBinding;
            }
        }

        return true;
    }

    private void ValidateModPackRegistration(
        GameplayModPackDefinition modPack,
        IReadOnlyList<GameplayClassRuntimeBinding> classBindings,
        bool allowRuntimeClassBindingOverride)
    {
        if (_modPacks.TryGetValue(modPack.Id, out var existingModPack))
        {
            throw new InvalidOperationException($"Gameplay mod pack \"{modPack.Id}\" is already registered.");
        }

        foreach (var item in modPack.Items)
        {
            if (_itemOwningModPackIds.TryGetValue(item.Key, out var owningModPackId))
            {
                throw new InvalidOperationException($"Gameplay item id \"{item.Key}\" from mod pack \"{modPack.Id}\" conflicts with mod pack \"{owningModPackId}\".");
            }
        }

        foreach (var gameplayClass in modPack.Classes)
        {
            if (_classOwningModPackIds.TryGetValue(gameplayClass.Key, out var owningModPackId))
            {
                throw new InvalidOperationException($"Gameplay class id \"{gameplayClass.Key}\" from mod pack \"{modPack.Id}\" conflicts with mod pack \"{owningModPackId}\".");
            }
        }

        var playerClassBindingsInModPack = new HashSet<PlayerClass>();
        var gameplayClassBindingsInModPack = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < classBindings.Count; index += 1)
        {
            var classBinding = classBindings[index];
            if (!string.Equals(classBinding.ModPackId, modPack.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Gameplay class binding for player class \"{classBinding.PlayerClass}\" references mod pack \"{classBinding.ModPackId}\" but is being registered with mod pack \"{modPack.Id}\".");
            }

            if (!modPack.Classes.ContainsKey(classBinding.ClassId))
            {
                throw new InvalidOperationException($"Gameplay class binding for player class \"{classBinding.PlayerClass}\" references missing class \"{classBinding.ClassId}\" in mod pack \"{modPack.Id}\".");
            }

            if (!gameplayClassBindingsInModPack.Add(classBinding.ClassId))
            {
                throw new InvalidOperationException($"Gameplay mod pack \"{modPack.Id}\" declares more than one runtime binding for gameplay class \"{classBinding.ClassId}\".");
            }

            if (_classBindingsByClassId.TryGetValue(classBinding.ClassId, out var existingClassBinding))
            {
                throw new InvalidOperationException($"Gameplay class binding for class \"{classBinding.ClassId}\" from mod pack \"{modPack.Id}\" conflicts with existing binding from mod pack \"{existingClassBinding.ModPackId}\".");
            }

            if (!classBinding.BindsLegacyPlayerClass)
            {
                continue;
            }

            if (!playerClassBindingsInModPack.Add(classBinding.PlayerClass))
            {
                throw new InvalidOperationException($"Gameplay mod pack \"{modPack.Id}\" declares more than one runtime binding for player class \"{classBinding.PlayerClass}\".");
            }

            if (_classBindings.TryGetValue(classBinding.PlayerClass, out var existingBinding))
            {
                if (!allowRuntimeClassBindingOverride)
                {
                    throw new InvalidOperationException($"Gameplay class binding for player class \"{classBinding.PlayerClass}\" from mod pack \"{modPack.Id}\" conflicts with existing binding from mod pack \"{existingBinding.ModPackId}\".");
                }
            }
        }
    }

    public GameplayModPackDefinition GetRequiredModPack(string modPackId)
    {
        if (_modPacks.TryGetValue(modPackId, out var modPack))
        {
            return modPack;
        }

        throw new KeyNotFoundException($"Gameplay mod pack \"{modPackId}\" is not registered.");
    }

    public GameplayClassDefinition GetRequiredClass(string classId)
    {
        if (_classes.TryGetValue(classId, out var gameplayClass))
        {
            return gameplayClass;
        }

        throw new KeyNotFoundException($"Gameplay class \"{classId}\" is not registered.");
    }

    public GameplayItemDefinition GetRequiredItem(string itemId)
    {
        if (_items.TryGetValue(itemId, out var item))
        {
            return item;
        }

        throw new KeyNotFoundException($"Gameplay item \"{itemId}\" is not registered.");
    }

    public GameplayClassRuntimeBinding GetRequiredClassBinding(PlayerClass playerClass)
    {
        if (_classBindings.TryGetValue(playerClass, out var binding))
        {
            return binding;
        }

        throw new KeyNotFoundException($"No gameplay runtime binding registered for player class \"{playerClass}\".");
    }

    public bool TryGetClassBinding(PlayerClass playerClass, out GameplayClassRuntimeBinding binding)
    {
        if (_classBindings.TryGetValue(playerClass, out var resolvedBinding))
        {
            binding = resolvedBinding;
            return true;
        }

        binding = null!;
        return false;
    }

    public bool TryGetClassBinding(string? gameplayClassId, out GameplayClassRuntimeBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(gameplayClassId)
            && _classBindingsByClassId.TryGetValue(gameplayClassId.Trim(), out var resolvedBinding))
        {
            binding = resolvedBinding;
            return true;
        }

        binding = null!;
        return false;
    }

    public GameplayClassRuntimeBinding GetRequiredClassBinding(string gameplayClassId)
    {
        if (TryGetClassBinding(gameplayClassId, out var binding))
        {
            return binding;
        }

        throw new KeyNotFoundException($"No gameplay runtime binding registered for gameplay class \"{gameplayClassId}\".");
    }

    public GameplayClassDefinition GetClassDefinition(PlayerClass playerClass)
    {
        var binding = GetRequiredClassBinding(playerClass);
        return GetRequiredClass(binding.ClassId);
    }

    public GameplayClassDefinition GetClassDefinition(string gameplayClassId)
    {
        var binding = GetRequiredClassBinding(gameplayClassId);
        return GetRequiredClass(binding.ClassId);
    }

    public GameplayClassLoadoutDefinition GetDefaultLoadout(PlayerClass playerClass)
    {
        var gameplayClass = GetClassDefinition(playerClass);
        return gameplayClass.Loadouts[gameplayClass.DefaultLoadoutId];
    }

    public GameplayClassLoadoutDefinition GetDefaultLoadout(string gameplayClassId)
    {
        var gameplayClass = GetClassDefinition(gameplayClassId);
        return gameplayClass.Loadouts[gameplayClass.DefaultLoadoutId];
    }

    public bool TryGetLoadout(PlayerClass playerClass, string? loadoutId, out GameplayClassLoadoutDefinition loadout)
    {
        var gameplayClass = GetClassDefinition(playerClass);
        if (!string.IsNullOrWhiteSpace(loadoutId)
            && gameplayClass.Loadouts.TryGetValue(loadoutId, out var resolvedLoadout))
        {
            loadout = resolvedLoadout;
            return true;
        }

        loadout = null!;
        return false;
    }

    public bool TryGetLoadout(string gameplayClassId, string? loadoutId, out GameplayClassLoadoutDefinition loadout)
    {
        var gameplayClass = GetClassDefinition(gameplayClassId);
        if (!string.IsNullOrWhiteSpace(loadoutId)
            && gameplayClass.Loadouts.TryGetValue(loadoutId, out var resolvedLoadout))
        {
            loadout = resolvedLoadout;
            return true;
        }

        loadout = null!;
        return false;
    }

    public GameplayClassLoadoutDefinition GetRequiredLoadout(PlayerClass playerClass, string loadoutId)
    {
        if (TryGetLoadout(playerClass, loadoutId, out var loadout))
        {
            return loadout;
        }

        throw new KeyNotFoundException($"Gameplay loadout \"{loadoutId}\" is not registered for player class \"{playerClass}\".");
    }

    public GameplayClassLoadoutDefinition GetRequiredLoadout(string gameplayClassId, string loadoutId)
    {
        if (TryGetLoadout(gameplayClassId, loadoutId, out var loadout))
        {
            return loadout;
        }

        throw new KeyNotFoundException($"Gameplay loadout \"{loadoutId}\" is not registered for gameplay class \"{gameplayClassId}\".");
    }

    public bool CanUseLoadout(PlayerClass playerClass, string? loadoutId)
    {
        return string.IsNullOrWhiteSpace(loadoutId)
            || TryGetLoadout(playerClass, loadoutId, out _);
    }

    public bool CanUseLoadout(string gameplayClassId, string? loadoutId)
    {
        return string.IsNullOrWhiteSpace(loadoutId)
            || TryGetLoadout(gameplayClassId, loadoutId, out _);
    }

    public bool CanUseLoadout(PlayerClass playerClass, string? loadoutId, Func<string, bool> ownsItem)
    {
        ArgumentNullException.ThrowIfNull(ownsItem);
        var loadout = ResolveValidatedLoadout(playerClass, loadoutId);
        return LoadoutItemsAreOwned(loadout, ownsItem);
    }

    public bool CanUseLoadout(string gameplayClassId, string? loadoutId, Func<string, bool> ownsItem)
    {
        ArgumentNullException.ThrowIfNull(ownsItem);
        var loadout = ResolveValidatedLoadout(gameplayClassId, loadoutId);
        return LoadoutItemsAreOwned(loadout, ownsItem);
    }

    public GameplayItemDefinition GetPrimaryItem(PlayerClass playerClass)
    {
        return GetRequiredItem(GetDefaultLoadout(playerClass).PrimaryItemId);
    }

    public GameplayItemDefinition GetPrimaryItem(string gameplayClassId)
    {
        return GetRequiredItem(GetDefaultLoadout(gameplayClassId).PrimaryItemId);
    }

    public GameplayItemDefinition? GetSecondaryItem(PlayerClass playerClass)
    {
        var itemId = GetDefaultLoadout(playerClass).SecondaryItemId;
        return itemId is null ? null : GetRequiredItem(itemId);
    }

    public GameplayItemDefinition? GetUtilityItem(PlayerClass playerClass)
    {
        var itemId = GetDefaultLoadout(playerClass).UtilityItemId;
        return itemId is null ? null : GetRequiredItem(itemId);
    }

    public bool SupportsExperimentalAcquiredWeapon(PlayerClass playerClass)
    {
        return GetRequiredClassBinding(playerClass).SupportsExperimentalAcquiredWeapon;
    }

    public bool SupportsExperimentalAcquiredWeapon(string gameplayClassId)
    {
        return GetRequiredClassBinding(gameplayClassId).SupportsExperimentalAcquiredWeapon;
    }

    public string GetPrimaryWeaponKillFeedSprite(PlayerClass playerClass)
    {
        return GetRequiredClassBinding(playerClass).PrimaryWeaponKillFeedSprite;
    }

    public string GetPrimaryWeaponKillFeedSprite(string gameplayClassId)
    {
        return GetRequiredClassBinding(gameplayClassId).PrimaryWeaponKillFeedSprite;
    }

    public PrimaryWeaponDefinition CreatePrimaryWeaponDefinition(GameplayItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var binding = GetRequiredPrimaryWeaponBinding(item.BehaviorId);
        var resolvedRocketCombat = ResolveRocketCombatDefinition(binding.WeaponKind, item.Combat);
        var resolvedDirectHitDamage = ResolveDirectHitDamage(binding.WeaponKind, item.Combat, resolvedRocketCombat);
        var resolvedDamagePerTick = ResolveDamagePerTick(binding.WeaponKind, item.Combat);
        return new PrimaryWeaponDefinition(
            DisplayName: item.DisplayName,
            Kind: binding.WeaponKind,
            MaxAmmo: item.Ammo.MaxAmmo,
            AmmoPerShot: item.Ammo.AmmoPerUse,
            ProjectilesPerShot: item.Ammo.ProjectilesPerUse,
            ReloadDelayTicks: item.Ammo.UseDelaySourceTicks,
            AmmoReloadTicks: item.Ammo.ReloadSourceTicks,
            SpreadDegrees: item.Ammo.SpreadDegrees,
            MinShotSpeed: item.Ammo.MinProjectileSpeed,
            AdditionalRandomShotSpeed: item.Ammo.AdditionalProjectileSpeed,
            FireSoundName: item.Combat?.FireSoundName ?? binding.FireSoundName,
            DirectHitDamage: resolvedDirectHitDamage,
            DamagePerTick: resolvedDamagePerTick,
            DirectHitHealAmount: item.Combat?.DirectHitHealAmount,
            PlayerKnockbackScale: Math.Max(0f, item.Combat?.PlayerKnockbackScale ?? 1f),
            PlayerSlowMovementMultiplier: NormalizePlayerSlowMovementMultiplier(item.Combat?.PlayerSlowMovementMultiplier),
            PlayerSlowRefreshSourceTicks: Math.Max(0, item.Combat?.PlayerSlowRefreshSourceTicks ?? 0),
            RocketCombat: resolvedRocketCombat,
            AutoReloads: item.Ammo.AutoReloads,
            AmmoRegenPerTick: item.Ammo.AmmoRegenPerTick,
            RefillsAllAtOnce: item.Ammo.RefillsAllAtOnce,
            ActiveProjectileLimit: item.Combat?.ActiveProjectileLimit);
    }

    private static float? ResolveDirectHitDamage(
        PrimaryWeaponKind weaponKind,
        GameplayItemCombatDefinition? combat,
        RocketCombatDefinition? rocketCombat)
    {
        if (combat?.DirectHitDamage is { } explicitDirectHitDamage)
        {
            return explicitDirectHitDamage;
        }

        return weaponKind switch
        {
            PrimaryWeaponKind.RocketLauncher => rocketCombat?.DirectHitDamage ?? RocketProjectileEntity.DirectHitDamage,
            PrimaryWeaponKind.PelletGun => ShotProjectileEntity.DamagePerHit,
            PrimaryWeaponKind.Minigun => ShotProjectileEntity.DamagePerHit,
            PrimaryWeaponKind.Revolver => RevolverProjectileEntity.DamagePerHit,
            PrimaryWeaponKind.FlameThrower => FlameProjectileEntity.DirectHitDamage,
            _ => null,
        };
    }

    private static float? ResolveDamagePerTick(PrimaryWeaponKind weaponKind, GameplayItemCombatDefinition? combat)
    {
        if (combat?.DamagePerTick is { } explicitDamagePerTick)
        {
            return explicitDamagePerTick;
        }

        return weaponKind == PrimaryWeaponKind.FlameThrower
            ? FlameProjectileEntity.BurnDamagePerTick
            : null;
    }

    private static float? NormalizePlayerSlowMovementMultiplier(float? multiplier)
    {
        return multiplier.HasValue
            ? Math.Clamp(multiplier.Value, 0.05f, 1f)
            : null;
    }

    private static RocketCombatDefinition? ResolveRocketCombatDefinition(
        PrimaryWeaponKind weaponKind,
        GameplayItemCombatDefinition? combat)
    {
        if (weaponKind != PrimaryWeaponKind.RocketLauncher)
        {
            return null;
        }

        return new RocketCombatDefinition(
            DirectHitDamage: combat?.Rocket?.DirectHitDamage ?? RocketProjectileEntity.DirectHitDamage,
            ExplosionDamage: combat?.Rocket?.ExplosionDamage ?? RocketProjectileEntity.ExplosionDamage,
            BlastRadius: combat?.Rocket?.BlastRadius ?? RocketProjectileEntity.BlastRadius,
            SplashThresholdFactor: combat?.Rocket?.SplashThresholdFactor ?? RocketProjectileEntity.SplashThresholdFactor);
    }

    public bool TryGetPrimaryWeaponBinding(string? behaviorId, out GameplayPrimaryWeaponRuntimeBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(behaviorId)
            && _primaryWeaponBindingsByBehaviorId.TryGetValue(behaviorId, out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }

    public GameplayPrimaryWeaponRuntimeBinding GetRequiredPrimaryWeaponBinding(string behaviorId)
    {
        if (TryGetPrimaryWeaponBinding(behaviorId, out var binding))
        {
            return binding;
        }

        throw new InvalidOperationException($"Gameplay behavior id \"{behaviorId}\" is not registered as a primary weapon behavior.");
    }

    public bool TryGetGameplayAbilityExecutor(string? executorId, out IGameplayAbilityExecutor executor)
    {
        if (!string.IsNullOrWhiteSpace(executorId)
            && _abilityExecutorsById.TryGetValue(executorId.Trim(), out executor!))
        {
            return true;
        }

        executor = null!;
        return false;
    }

    public bool TryGetGameplayAbilityDefinition(string? itemId, out GameplayItemDefinition item, out GameplayAbilityDefinition ability)
    {
        if (TryGetItem(itemId, out item)
            && item.Ability is { } resolvedAbility)
        {
            ability = resolvedAbility;
            return true;
        }

        ability = null!;
        item = null!;
        return false;
    }

    public void SealAbilityDefinitions()
    {
        _abilityDefinitionsSealed = true;
        _abilityExecutorsSealed = true;
        _primaryWeaponBehaviorsSealed = true;
    }

    public bool TryRegisterGameplayWeaponItem(GameplayWeaponItemRegistration registration, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(registration);
        errorMessage = string.Empty;
        if (_abilityDefinitionsSealed)
        {
            errorMessage = "Gameplay item registry is sealed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(registration.ItemId)
            || string.IsNullOrWhiteSpace(registration.DisplayName)
            || string.IsNullOrWhiteSpace(registration.BehaviorId))
        {
            errorMessage = "Gameplay weapon item registration requires item id, display name, and behavior id.";
            return false;
        }

        if (registration.Slot is not (GameplayEquipmentSlot.Primary or GameplayEquipmentSlot.Secondary or GameplayEquipmentSlot.Utility))
        {
            errorMessage = $"Gameplay weapon item slot \"{registration.Slot}\" is not a weapon-capable slot.";
            return false;
        }

        var itemId = registration.ItemId.Trim();
        if (_items.ContainsKey(itemId))
        {
            errorMessage = $"Gameplay item \"{itemId}\" is already registered.";
            return false;
        }

        var behaviorId = registration.BehaviorId.Trim();
        if (!TryGetPrimaryWeaponBinding(behaviorId, out _))
        {
            errorMessage = $"Gameplay behavior id \"{behaviorId}\" is not registered as a primary weapon behavior.";
            return false;
        }

        var modPackId = string.IsNullOrWhiteSpace(registration.ModPackId)
            ? "plugin.weapon"
            : registration.ModPackId.Trim();
        var item = new GameplayItemDefinition(
            itemId,
            registration.DisplayName.Trim(),
            registration.Slot,
            behaviorId,
            registration.Ammo ?? new GameplayItemAmmoDefinition(),
            registration.Presentation ?? new GameplayItemPresentationDefinition(),
            registration.Combat,
            registration.Ownership,
            registration.Description);
        AddRuntimeItem(modPackId, item);
        return true;
    }

    public bool TryRegisterGameplayAbility(GameplayAbilityRegistration registration, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(registration);
        errorMessage = string.Empty;
        if (_abilityDefinitionsSealed)
        {
            errorMessage = "Gameplay ability registry is sealed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(registration.ItemId)
            || string.IsNullOrWhiteSpace(registration.DisplayName)
            || string.IsNullOrWhiteSpace(registration.BehaviorId)
            || string.IsNullOrWhiteSpace(registration.Ability.Category)
            || string.IsNullOrWhiteSpace(registration.Ability.Activation))
        {
            errorMessage = "Gameplay ability registration requires item id, display name, behavior id, category, and activation.";
            return false;
        }

        var itemId = registration.ItemId.Trim();
        if (_items.ContainsKey(itemId))
        {
            errorMessage = $"Gameplay item \"{itemId}\" is already registered.";
            return false;
        }

        var modPackId = string.IsNullOrWhiteSpace(registration.ModPackId)
            ? "plugin.ability"
            : registration.ModPackId.Trim();
        var normalizedAbility = NormalizeRuntimeAbility(registration.Ability, registration.BehaviorId);
        var item = new GameplayItemDefinition(
            itemId,
            registration.DisplayName.Trim(),
            registration.Slot,
            registration.BehaviorId.Trim(),
            new GameplayItemAmmoDefinition(),
            registration.Presentation ?? new GameplayItemPresentationDefinition(),
            Ability: normalizedAbility);
        AddRuntimeItem(modPackId, item);
        return true;
    }

    private void AddRuntimeItem(string modPackId, GameplayItemDefinition item)
    {
        _items[item.Id] = item;
        _itemOwningModPackIds[item.Id] = modPackId;
        if (!_modPacks.TryGetValue(modPackId, out var modPack))
        {
            _modPacks[modPackId] = new GameplayModPackDefinition(
                modPackId,
                modPackId,
                new Version(1, 0, 0),
                new Dictionary<string, GameplayItemDefinition>(StringComparer.Ordinal)
                {
                    [item.Id] = item,
                },
                new Dictionary<string, GameplayClassDefinition>(StringComparer.Ordinal),
                new GameplayModPackAssetCatalog(new Dictionary<string, GameplaySpriteAssetDefinition>(StringComparer.Ordinal)));
        }
        else
        {
            var items = modPack.Items.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
            items[item.Id] = item;
            _modPacks[modPackId] = modPack with { Items = items };
        }
    }

    public bool TryRegisterGameplayLoadout(GameplayLoadoutRegistration registration, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(registration);
        errorMessage = string.Empty;
        if (_abilityDefinitionsSealed)
        {
            errorMessage = "Gameplay ability registry is sealed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(registration.ClassId)
            || string.IsNullOrWhiteSpace(registration.LoadoutId)
            || string.IsNullOrWhiteSpace(registration.DisplayName)
            || string.IsNullOrWhiteSpace(registration.PrimaryItemId))
        {
            errorMessage = "Gameplay loadout registration requires class id, loadout id, display name, and primary item id.";
            return false;
        }

        var classId = registration.ClassId.Trim();
        if (!_classes.TryGetValue(classId, out var gameplayClass))
        {
            errorMessage = $"Gameplay class \"{classId}\" is not registered.";
            return false;
        }

        var loadoutId = registration.LoadoutId.Trim();
        if (gameplayClass.Loadouts.ContainsKey(loadoutId))
        {
            errorMessage = $"Gameplay loadout \"{loadoutId}\" is already registered for class \"{classId}\".";
            return false;
        }

        var loadout = new GameplayClassLoadoutDefinition(
            loadoutId,
            registration.DisplayName.Trim(),
            registration.PrimaryItemId.Trim(),
            NormalizeOptionalItemId(registration.SecondaryItemId),
            NormalizeOptionalItemId(registration.UtilityItemId),
            NormalizeAbilityItemIds(registration.AbilityItemIds));
        if (!ValidateRuntimeLoadoutItems(loadout, out errorMessage))
        {
            return false;
        }

        AddLoadoutToClass(classId, gameplayClass, loadout);
        return true;
    }

    public bool TryRegisterGameplaySlotItem(GameplaySlotItemRegistration registration, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(registration);
        errorMessage = string.Empty;
        if (_abilityDefinitionsSealed)
        {
            errorMessage = "Gameplay ability registry is sealed.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(registration.ClassId)
            || string.IsNullOrWhiteSpace(registration.ItemId))
        {
            errorMessage = "Gameplay slot item registration requires class id and item id.";
            return false;
        }

        var classId = registration.ClassId.Trim();
        if (!_classes.TryGetValue(classId, out var gameplayClass))
        {
            errorMessage = $"Gameplay class \"{classId}\" is not registered.";
            return false;
        }

        var itemId = registration.ItemId.Trim();
        if (!_items.TryGetValue(itemId, out var item))
        {
            errorMessage = $"Gameplay item \"{itemId}\" is not registered.";
            return false;
        }

        if (item.Slot != registration.Slot)
        {
            errorMessage = $"Gameplay item \"{itemId}\" uses slot \"{item.Slot}\" but slot registration expected \"{registration.Slot}\".";
            return false;
        }

        var baseLoadoutId = string.IsNullOrWhiteSpace(registration.BaseLoadoutId)
            ? gameplayClass.DefaultLoadoutId
            : registration.BaseLoadoutId.Trim();
        if (!gameplayClass.Loadouts.TryGetValue(baseLoadoutId, out var baseLoadout))
        {
            errorMessage = $"Gameplay base loadout \"{baseLoadoutId}\" is not registered for class \"{classId}\".";
            return false;
        }

        var loadoutId = string.IsNullOrWhiteSpace(registration.LoadoutId)
            ? $"{classId}.{itemId}".Replace(' ', '-')
            : registration.LoadoutId.Trim();
        if (gameplayClass.Loadouts.ContainsKey(loadoutId))
        {
            errorMessage = $"Gameplay loadout \"{loadoutId}\" is already registered for class \"{classId}\".";
            return false;
        }

        var displayName = string.IsNullOrWhiteSpace(registration.DisplayName)
            ? item.DisplayName
            : registration.DisplayName.Trim();
        var loadout = registration.Slot switch
        {
            GameplayEquipmentSlot.Primary => baseLoadout with { Id = loadoutId, DisplayName = displayName, PrimaryItemId = itemId },
            GameplayEquipmentSlot.Secondary => baseLoadout with { Id = loadoutId, DisplayName = displayName, SecondaryItemId = itemId },
            GameplayEquipmentSlot.Utility => baseLoadout with { Id = loadoutId, DisplayName = displayName, UtilityItemId = itemId },
            _ => baseLoadout,
        };
        if (!ValidateRuntimeLoadoutItems(loadout, out errorMessage))
        {
            return false;
        }

        AddLoadoutToClass(classId, gameplayClass, loadout);
        return true;
    }

    public bool TryOverrideGameplayAbility(string itemId, GameplayAbilityPatch patch, out string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentNullException.ThrowIfNull(patch);
        errorMessage = string.Empty;
        if (_abilityDefinitionsSealed)
        {
            errorMessage = "Gameplay ability registry is sealed.";
            return false;
        }

        if (!_items.TryGetValue(itemId.Trim(), out var item))
        {
            errorMessage = $"Gameplay item \"{itemId}\" is not registered.";
            return false;
        }

        var existingAbility = item.Ability ?? new GameplayAbilityDefinition(
            Category: item.Slot == GameplayEquipmentSlot.Utility ? GameplayAbilityConstants.UtilityCategory : GameplayAbilityConstants.SecondaryCategory,
            Activation: GameplayAbilityConstants.PressedActivation,
            ExecutorId: item.BehaviorId);
        var patchedAbility = existingAbility with
        {
            Category = string.IsNullOrWhiteSpace(patch.Category) ? existingAbility.Category : patch.Category.Trim(),
            Activation = string.IsNullOrWhiteSpace(patch.Activation) ? existingAbility.Activation : patch.Activation.Trim(),
            ExecutorId = string.IsNullOrWhiteSpace(patch.ExecutorId) ? existingAbility.ExecutorId : patch.ExecutorId.Trim(),
            Tags = patch.Tags ?? existingAbility.Tags,
            Parameters = patch.Parameters ?? existingAbility.Parameters,
        };
        var updatedItem = item with
        {
            Ability = NormalizeRuntimeAbility(patchedAbility, item.BehaviorId),
        };
        _items[updatedItem.Id] = updatedItem;

        if (_itemOwningModPackIds.TryGetValue(updatedItem.Id, out var modPackId)
            && _modPacks.TryGetValue(modPackId, out var modPack))
        {
            var items = modPack.Items.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
            items[updatedItem.Id] = updatedItem;
            _modPacks[modPackId] = modPack with { Items = items };
        }

        return true;
    }

    public CharacterClassDefinition CreateCharacterClassDefinition(PlayerClass playerClass)
    {
        return CreateCharacterClassDefinition(GetRequiredClassBinding(playerClass));
    }

    public CharacterClassDefinition CreateCharacterClassDefinition(string gameplayClassId)
    {
        return CreateCharacterClassDefinition(GetRequiredClassBinding(gameplayClassId));
    }

    private CharacterClassDefinition CreateCharacterClassDefinition(GameplayClassRuntimeBinding binding)
    {
        var gameplayClass = GetRequiredClass(binding.ClassId);
        var movement = gameplayClass.Movement;
        var primaryWeapon = CreatePrimaryWeaponDefinition(GetPrimaryItem(binding.ClassId));
        var width = movement.CollisionRight - movement.CollisionLeft;
        var height = movement.CollisionBottom - movement.CollisionTop;

        return new CharacterClassDefinition(
            Id: binding.PlayerClass,
            DisplayName: gameplayClass.DisplayName,
            PrimaryWeapon: primaryWeapon,
            MaxHealth: movement.MaxHealth,
            Width: width,
            Height: height,
            CollisionLeft: movement.CollisionLeft,
            CollisionTop: movement.CollisionTop,
            CollisionRight: movement.CollisionRight,
            CollisionBottom: movement.CollisionBottom,
            RunPower: movement.RunPower,
            JumpStrength: movement.JumpStrength,
            MaxRunSpeed: LegacyMovementModel.GetMaxRunSpeed(movement.RunPower),
            GroundAcceleration: LegacyMovementModel.GetContinuousRunDrive(movement.RunPower),
            GroundDeceleration: LegacyMovementModel.GetContinuousRunDrive(movement.RunPower),
            Gravity: LegacyMovementModel.GetGravityPerSecondSquared(),
            JumpSpeed: LegacyMovementModel.GetJumpSpeed(movement.JumpStrength),
            MaxAirJumps: movement.MaxAirJumps,
            TauntLengthFrames: movement.TauntLengthFrames,
            GameplayClassId: binding.ClassId,
            GameplayModPackId: binding.ModPackId,
            BotGraphClassId: binding.BotGraphPlayerClass);
    }

    private PrimaryWeaponKind ResolvePrimaryWeaponKind(string behaviorId)
    {
        if (_primaryWeaponBindingsByBehaviorId.TryGetValue(behaviorId, out var binding))
        {
            return binding.WeaponKind;
        }

        throw new InvalidOperationException($"Gameplay behavior id \"{behaviorId}\" is not registered as a primary weapon behavior.");
    }

    public bool TryResolvePrimaryWeaponItemId(PrimaryWeaponDefinition weaponDefinition, out string itemId)
    {
        ArgumentNullException.ThrowIfNull(weaponDefinition);

        foreach (var item in _items.Values)
        {
            if (item.Slot == GameplayEquipmentSlot.Utility)
            {
                continue;
            }

            if (!TryGetPrimaryWeaponBinding(item.BehaviorId, out _))
            {
                continue;
            }

            if (CreatePrimaryWeaponDefinition(item) != weaponDefinition)
            {
                continue;
            }

            itemId = item.Id;
            return true;
        }

        itemId = string.Empty;
        return false;
    }

    public static bool LoadoutItemsAreOwned(GameplayClassLoadoutDefinition loadout, Func<string, bool> ownsItem)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(ownsItem);
        return ItemIsOwnedOrUnspecified(loadout.PrimaryItemId, ownsItem)
            && ItemIsOwnedOrUnspecified(loadout.SecondaryItemId, ownsItem)
            && ItemIsOwnedOrUnspecified(loadout.UtilityItemId, ownsItem)
            && (loadout.AbilityItemIds is null
                || loadout.AbilityItemIds.All(itemId => ItemIsOwnedOrUnspecified(itemId, ownsItem)));
    }

    private static bool ItemIsOwnedOrUnspecified(string? itemId, Func<string, bool> ownsItem)
    {
        return string.IsNullOrWhiteSpace(itemId) || ownsItem(itemId);
    }

    private static GameplayAbilityDefinition NormalizeRuntimeAbility(GameplayAbilityDefinition ability, string fallbackExecutorId)
    {
        return ability with
        {
            Category = ability.Category.Trim(),
            Activation = ability.Activation.Trim(),
            ExecutorId = string.IsNullOrWhiteSpace(ability.ExecutorId)
                ? fallbackExecutorId.Trim()
                : ability.ExecutorId.Trim(),
            Tags = ability.Tags
                .Select(static tag => tag?.Trim() ?? string.Empty)
                .Where(static tag => tag.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Parameters = ability.Parameters,
        };
    }

    private static string? NormalizeOptionalItemId(string? itemId)
    {
        return string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim();
    }

    private static IReadOnlyList<string>? NormalizeAbilityItemIds(IReadOnlyList<string>? itemIds)
    {
        if (itemIds is null)
        {
            return null;
        }

        var normalizedItemIds = itemIds
            .Select(static itemId => itemId?.Trim() ?? string.Empty)
            .Where(static itemId => itemId.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return normalizedItemIds.Length == 0 ? null : normalizedItemIds;
    }

    private bool ValidateRuntimeLoadoutItems(GameplayClassLoadoutDefinition loadout, out string errorMessage)
    {
        if (!ValidateRuntimeLoadoutItem(loadout.PrimaryItemId, GameplayEquipmentSlot.Primary, out errorMessage)
            || !ValidateRuntimeLoadoutItem(loadout.SecondaryItemId, GameplayEquipmentSlot.Secondary, out errorMessage)
            || !ValidateRuntimeLoadoutItem(loadout.UtilityItemId, GameplayEquipmentSlot.Utility, out errorMessage)
            || !ValidateRuntimeLoadoutAbilityItems(loadout.AbilityItemIds, out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool ValidateRuntimeLoadoutAbilityItems(IReadOnlyList<string>? itemIds, out string errorMessage)
    {
        if (itemIds is null)
        {
            errorMessage = string.Empty;
            return true;
        }

        foreach (var itemId in itemIds)
        {
            if (!_items.TryGetValue(itemId, out var item))
            {
                errorMessage = $"Gameplay loadout references unknown ability item \"{itemId}\".";
                return false;
            }

            if (item.Ability is null)
            {
                errorMessage = $"Gameplay loadout ability item \"{itemId}\" does not declare ability metadata.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool ValidateRuntimeLoadoutItem(string? itemId, GameplayEquipmentSlot slot, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            errorMessage = string.Empty;
            return true;
        }

        if (!_items.TryGetValue(itemId, out var item))
        {
            errorMessage = $"Gameplay loadout references unknown item \"{itemId}\".";
            return false;
        }

        if (item.Slot != slot)
        {
            errorMessage = $"Gameplay loadout expected item \"{itemId}\" to use slot \"{slot}\", but found \"{item.Slot}\".";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void AddLoadoutToClass(string classId, GameplayClassDefinition gameplayClass, GameplayClassLoadoutDefinition loadout)
    {
        var loadouts = gameplayClass.Loadouts.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        loadouts[loadout.Id] = loadout;
        _classes[classId] = gameplayClass with { Loadouts = loadouts };
    }

    public bool OwnsItemByDefault(string? itemId)
    {
        return TryGetItem(itemId, out var item)
            && (item.Ownership?.DefaultGranted ?? true);
    }

    public bool RequiresTrackedOwnership(string? itemId)
    {
        return TryGetItem(itemId, out var item)
            && (item.Ownership?.TrackOwnership ?? false)
            && !(item.Ownership?.DefaultGranted ?? true);
    }

    public bool TryResolveBoundPlayerClassForPrimaryItem(string? itemId, out PlayerClass playerClass)
    {
        if (!TryGetItem(itemId, out var item) || item.Slot != GameplayEquipmentSlot.Primary)
        {
            playerClass = default;
            return false;
        }

        foreach (var classBinding in _classBindings.Values)
        {
            if (string.Equals(GetDefaultLoadout(classBinding.PlayerClass).PrimaryItemId, item.Id, StringComparison.Ordinal))
            {
                playerClass = classBinding.PlayerClass;
                return true;
            }
        }

        playerClass = default;
        return false;
    }

    private bool TryGetItem(string? itemId, out GameplayItemDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(itemId)
            && _items.TryGetValue(itemId, out var resolvedItem))
        {
            item = resolvedItem;
            return true;
        }

        item = null!;
        return false;
    }

}

public sealed record GameplayClassRuntimeBinding(
    PlayerClass PlayerClass,
    string ModPackId,
    string ClassId,
    bool SupportsExperimentalAcquiredWeapon,
    string PrimaryWeaponKillFeedSprite,
    PlayerClass BasePlayerClass,
    PlayerClass BotGraphPlayerClass,
    bool BindsLegacyPlayerClass);

public readonly record struct GameplayPrimaryWeaponRuntimeBinding(
    string BehaviorId,
    PrimaryWeaponKind WeaponKind,
    string? FireSoundName = null,
    IGameplayPrimaryWeaponExecutor? Executor = null);

