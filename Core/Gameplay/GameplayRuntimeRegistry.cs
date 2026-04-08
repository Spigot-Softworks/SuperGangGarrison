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
    private readonly Dictionary<string, GameplayPrimaryWeaponRuntimeBinding> _primaryWeaponBindingsByBehaviorId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplaySecondaryAbilityRuntimeBinding> _secondaryAbilityBindingsByBehaviorId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameplayUtilityAbilityRuntimeBinding> _utilityAbilityBindingsByBehaviorId = new(StringComparer.Ordinal);

    public IReadOnlyCollection<GameplayModPackDefinition> ModPacks => _modPacks.Values;

    public void RegisterPrimaryWeaponBehavior(string behaviorId, PrimaryWeaponKind weaponKind)
    {
        RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(behaviorId, weaponKind));
    }

    public void RegisterPrimaryWeaponBehavior(GameplayPrimaryWeaponRuntimeBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.BehaviorId);
        _primaryWeaponBindingsByBehaviorId[binding.BehaviorId] = binding;
    }

    public void RegisterSecondaryAbilityBehavior(GameplaySecondaryAbilityRuntimeBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.BehaviorId);
        _secondaryAbilityBindingsByBehaviorId[binding.BehaviorId] = binding;
    }

    public void RegisterUtilityAbilityBehavior(GameplayUtilityAbilityRuntimeBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.BehaviorId);
        _utilityAbilityBindingsByBehaviorId[binding.BehaviorId] = binding;
    }

    public void RegisterModPack(GameplayModPackDefinition modPack, IReadOnlyList<GameplayClassRuntimeBinding> classBindings)
    {
        ArgumentNullException.ThrowIfNull(modPack);
        ArgumentNullException.ThrowIfNull(classBindings);
        ValidateModPackRegistration(modPack, classBindings);
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
            _classBindings[classBindings[index].PlayerClass] = classBindings[index];
        }
    }

    private void ValidateModPackRegistration(GameplayModPackDefinition modPack, IReadOnlyList<GameplayClassRuntimeBinding> classBindings)
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

        for (var index = 0; index < classBindings.Count; index += 1)
        {
            var classBinding = classBindings[index];
            if (_classBindings.TryGetValue(classBinding.PlayerClass, out var existingBinding))
            {
                throw new InvalidOperationException($"Gameplay class binding for player class \"{classBinding.PlayerClass}\" from mod pack \"{modPack.Id}\" conflicts with existing binding from mod pack \"{existingBinding.ModPackId}\".");
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

    public GameplayClassDefinition GetClassDefinition(PlayerClass playerClass)
    {
        var binding = GetRequiredClassBinding(playerClass);
        return GetRequiredClass(binding.ClassId);
    }

    public GameplayClassLoadoutDefinition GetDefaultLoadout(PlayerClass playerClass)
    {
        var gameplayClass = GetClassDefinition(playerClass);
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

    public GameplayClassLoadoutDefinition GetRequiredLoadout(PlayerClass playerClass, string loadoutId)
    {
        if (TryGetLoadout(playerClass, loadoutId, out var loadout))
        {
            return loadout;
        }

        throw new KeyNotFoundException($"Gameplay loadout \"{loadoutId}\" is not registered for player class \"{playerClass}\".");
    }

    public bool CanUseLoadout(PlayerClass playerClass, string? loadoutId)
    {
        return string.IsNullOrWhiteSpace(loadoutId)
            || TryGetLoadout(playerClass, loadoutId, out _);
    }

    public bool CanUseLoadout(PlayerClass playerClass, string? loadoutId, Func<string, bool> ownsItem)
    {
        ArgumentNullException.ThrowIfNull(ownsItem);
        var loadout = ResolveValidatedLoadout(playerClass, loadoutId);
        return LoadoutItemsAreOwned(loadout, ownsItem);
    }

    public GameplayItemDefinition GetPrimaryItem(PlayerClass playerClass)
    {
        return GetRequiredItem(GetDefaultLoadout(playerClass).PrimaryItemId);
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

    public string GetPrimaryWeaponKillFeedSprite(PlayerClass playerClass)
    {
        return GetRequiredClassBinding(playerClass).PrimaryWeaponKillFeedSprite;
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
            RocketCombat: resolvedRocketCombat,
            AutoReloads: item.Ammo.AutoReloads,
            AmmoRegenPerTick: item.Ammo.AmmoRegenPerTick,
            RefillsAllAtOnce: item.Ammo.RefillsAllAtOnce);
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

    public bool TryGetSecondaryAbilityBinding(string? behaviorId, out GameplaySecondaryAbilityRuntimeBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(behaviorId)
            && _secondaryAbilityBindingsByBehaviorId.TryGetValue(behaviorId, out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }

    public bool TryGetUtilityAbilityBinding(string? behaviorId, out GameplayUtilityAbilityRuntimeBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(behaviorId)
            && _utilityAbilityBindingsByBehaviorId.TryGetValue(behaviorId, out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }

    public CharacterClassDefinition CreateCharacterClassDefinition(PlayerClass playerClass)
    {
        var gameplayClass = GetClassDefinition(playerClass);
        var movement = gameplayClass.Movement;
        var primaryWeapon = CreatePrimaryWeaponDefinition(GetPrimaryItem(playerClass));
        var width = movement.CollisionRight - movement.CollisionLeft;
        var height = movement.CollisionBottom - movement.CollisionTop;

        return new CharacterClassDefinition(
            Id: playerClass,
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
            TauntLengthFrames: movement.TauntLengthFrames);
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
            if (item.Slot != GameplayEquipmentSlot.Primary)
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
            && ItemIsOwnedOrUnspecified(loadout.UtilityItemId, ownsItem);
    }

    private static bool ItemIsOwnedOrUnspecified(string? itemId, Func<string, bool> ownsItem)
    {
        return string.IsNullOrWhiteSpace(itemId) || ownsItem(itemId);
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
    string PrimaryWeaponKillFeedSprite);

public readonly record struct GameplayPrimaryWeaponRuntimeBinding(
    string BehaviorId,
    PrimaryWeaponKind WeaponKind,
    string? FireSoundName = null);

public enum GameplaySecondaryAbilityActionKind
{
    None = 0,
    EngineerPda = 1,
    PyroAirblast = 2,
    DemomanDetonate = 3,
    HeavySandvich = 4,
    SniperScope = 5,
    MedicNeedlegun = 6,
    SpyCloak = 7,
    QuoteBladeThrow = 8,
}

public readonly record struct GameplaySecondaryAbilityRuntimeBinding(
    string BehaviorId,
    GameplaySecondaryAbilityActionKind ActionKind,
    bool UsesHeldInput = false);

public enum GameplayUtilityAbilityActionKind
{
    None = 0,
    MedicUber = 1,
}

public readonly record struct GameplayUtilityAbilityRuntimeBinding(
    string BehaviorId,
    GameplayUtilityAbilityActionKind ActionKind);
