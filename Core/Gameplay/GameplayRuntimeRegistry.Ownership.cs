using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

// Runtime registrations made inside a registration scope belong to that scope's owner
// (a plugin, or content replicated from a server) and can be removed again, so plugin
// reloads, practice restarts and server switches do not leave stale content behind.
public sealed partial class GameplayRuntimeRegistry
{
    private readonly Dictionary<string, RegistrationOwner> _registrationOwners = new(StringComparer.Ordinal);
    private readonly Stack<string> _registrationOwnerScopes = new();

    private string? ActiveRegistrationOwner => _registrationOwnerScopes.Count > 0 ? _registrationOwnerScopes.Peek() : null;

    public IDisposable BeginRegistrationScope(string ownerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerKey);
        _registrationOwnerScopes.Push(ownerKey);
        return new RegistrationScope(this, ownerKey);
    }

    public bool HasOwnedRegistrations(string ownerKey)
    {
        return _registrationOwners.ContainsKey(ownerKey);
    }

    /// <summary>
    /// Undoes everything registered under <paramref name="ownerKey"/>, newest first.
    /// </summary>
    public bool RemoveOwnedRegistrations(string ownerKey)
    {
        if (!_registrationOwners.Remove(ownerKey, out var owner))
        {
            return false;
        }

        for (var index = owner.Entries.Count - 1; index >= 0; index -= 1)
        {
            owner.Entries[index].Undo();
        }

        _characterClassDefinitionCache.Clear();
        ModPackRevision += 1;
        return true;
    }

    public void UnsealAbilityDefinitions()
    {
        _abilityDefinitionsSealed = false;
        _abilityExecutorsSealed = false;
        _primaryWeaponBehaviorsSealed = false;
    }

    private void EndRegistrationScope(string ownerKey)
    {
        if (_registrationOwnerScopes.Count > 0
            && string.Equals(_registrationOwnerScopes.Peek(), ownerKey, StringComparison.Ordinal))
        {
            _registrationOwnerScopes.Pop();
        }
    }

    private void RecordRegistration(OwnedRegistrationKind kind, string key, string subKey, Action undo)
    {
        var ownerKey = ActiveRegistrationOwner;
        if (ownerKey is null)
        {
            return;
        }

        if (!_registrationOwners.TryGetValue(ownerKey, out var owner))
        {
            owner = new RegistrationOwner();
            _registrationOwners[ownerKey] = owner;
        }

        owner.Entries.Add(new OwnedRegistration(kind, key, subKey, undo));
    }

    private IEnumerable<OwnedRegistration> EnumerateOwnedRegistrations()
    {
        return _registrationOwners.Values.SelectMany(static owner => owner.Entries);
    }

    private void SetModPack(GameplayModPackDefinition modPack)
    {
        var hadModPack = _modPacks.TryGetValue(modPack.Id, out var previousModPack);
        _modPacks[modPack.Id] = modPack;
        RecordRegistration(OwnedRegistrationKind.ModPack, modPack.Id, string.Empty, () =>
        {
            if (hadModPack)
            {
                _modPacks[modPack.Id] = previousModPack!;
            }
            else
            {
                _modPacks.Remove(modPack.Id);
            }
        });
    }

    private void EnsureRuntimeModPack(string modPackId)
    {
        if (_modPacks.ContainsKey(modPackId))
        {
            return;
        }

        _modPacks[modPackId] = new GameplayModPackDefinition(
            modPackId,
            modPackId,
            new Version(1, 0, 0),
            new Dictionary<string, GameplayItemDefinition>(StringComparer.Ordinal),
            new Dictionary<string, GameplayClassDefinition>(StringComparer.Ordinal),
            new GameplayModPackAssetCatalog(new Dictionary<string, GameplaySpriteAssetDefinition>(StringComparer.Ordinal)),
            SchemaVersion: 2);
        // Other owners may have added to the pack since, so only drop it once it is empty again.
        RecordRegistration(OwnedRegistrationKind.ModPack, modPackId, string.Empty, () =>
        {
            if (_modPacks.TryGetValue(modPackId, out var modPack)
                && modPack.Items.Count == 0
                && modPack.Classes.Count == 0)
            {
                _modPacks.Remove(modPackId);
            }
        });
    }

    private void SetModPackItem(string modPackId, string itemId, GameplayItemDefinition? item)
    {
        if (!_modPacks.TryGetValue(modPackId, out var modPack))
        {
            return;
        }

        var items = modPack.Items.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        if (item is null)
        {
            items.Remove(itemId);
        }
        else
        {
            items[itemId] = item;
        }

        _modPacks[modPackId] = modPack with { Items = items };
    }

    private void SetModPackClass(string modPackId, string classId, GameplayClassDefinition? gameplayClass)
    {
        if (!_modPacks.TryGetValue(modPackId, out var modPack))
        {
            return;
        }

        var classes = modPack.Classes.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        if (gameplayClass is null)
        {
            classes.Remove(classId);
        }
        else
        {
            classes[classId] = gameplayClass;
        }

        _modPacks[modPackId] = modPack with { Classes = classes };
    }

    private void SetItem(string modPackId, GameplayItemDefinition item, bool updateModPack = true)
    {
        var itemId = item.Id;
        var hadItem = _items.TryGetValue(itemId, out var previousItem);
        var hadOwningModPack = _itemOwningModPackIds.TryGetValue(itemId, out var previousModPackId);
        _characterClassDefinitionCache.Clear();
        _items[itemId] = item;
        _itemOwningModPackIds[itemId] = modPackId;
        if (updateModPack)
        {
            EnsureRuntimeModPack(modPackId);
            SetModPackItem(modPackId, itemId, item);
        }

        RecordRegistration(OwnedRegistrationKind.Item, itemId, modPackId, () =>
        {
            SetModPackItem(modPackId, itemId, null);
            if (hadItem)
            {
                _items[itemId] = previousItem!;
                if (hadOwningModPack)
                {
                    _itemOwningModPackIds[itemId] = previousModPackId!;
                    SetModPackItem(previousModPackId!, itemId, previousItem);
                }
            }
            else
            {
                _items.Remove(itemId);
                _itemOwningModPackIds.Remove(itemId);
            }
        });
    }

    private void SetClass(
        string modPackId,
        GameplayClassDefinition gameplayClass,
        GameplayClassRuntimeBinding? binding,
        bool updateModPack = true)
    {
        var classId = gameplayClass.Id;
        var hadClass = _classes.TryGetValue(classId, out var previousClass);
        var hadOwningModPack = _classOwningModPackIds.TryGetValue(classId, out var previousModPackId);
        var hadBinding = _classBindingsByClassId.TryGetValue(classId, out var previousBinding);
        _characterClassDefinitionCache.Clear();
        _classes[classId] = gameplayClass;
        _classOwningModPackIds[classId] = modPackId;
        if (binding is not null)
        {
            _classBindingsByClassId[classId] = binding;
        }

        if (updateModPack)
        {
            EnsureRuntimeModPack(modPackId);
            SetModPackClass(modPackId, classId, gameplayClass);
        }

        RecordRegistration(OwnedRegistrationKind.Class, classId, modPackId, () =>
        {
            SetModPackClass(modPackId, classId, null);
            if (hadClass)
            {
                _classes[classId] = previousClass!;
                if (hadOwningModPack)
                {
                    _classOwningModPackIds[classId] = previousModPackId!;
                    SetModPackClass(previousModPackId!, classId, previousClass);
                }
            }
            else
            {
                _classes.Remove(classId);
                _classOwningModPackIds.Remove(classId);
            }

            if (hadBinding)
            {
                _classBindingsByClassId[classId] = previousBinding!;
            }
            else
            {
                _classBindingsByClassId.Remove(classId);
            }
        });
    }

    private void SetLegacyClassBinding(GameplayClassRuntimeBinding binding)
    {
        var playerClass = binding.PlayerClass;
        var hadBinding = _classBindings.TryGetValue(playerClass, out var previousBinding);
        _characterClassDefinitionCache.Clear();
        _classBindings[playerClass] = binding;
        RecordRegistration(OwnedRegistrationKind.LegacyClassBinding, playerClass.ToString(), binding.ClassId, () =>
        {
            if (hadBinding)
            {
                _classBindings[playerClass] = previousBinding!;
            }
            else
            {
                _classBindings.Remove(playerClass);
            }
        });
    }

    private void SetPackAssets(
        string modPackId,
        IReadOnlyDictionary<string, GameplaySpriteAssetDefinition> sprites,
        IReadOnlyDictionary<string, GameplaySoundAssetDefinition> sounds)
    {
        EnsureRuntimeModPack(modPackId);
        var previousAssets = _modPacks[modPackId].Assets;
        _modPacks[modPackId] = _modPacks[modPackId] with
        {
            Assets = new GameplayModPackAssetCatalog(
                new Dictionary<string, GameplaySpriteAssetDefinition>(sprites, StringComparer.Ordinal),
                new Dictionary<string, GameplaySoundAssetDefinition>(sounds, StringComparer.Ordinal)),
        };
        RecordRegistration(OwnedRegistrationKind.PackSprites, modPackId, string.Empty, () =>
        {
            if (_modPacks.TryGetValue(modPackId, out var current))
            {
                _modPacks[modPackId] = current with { Assets = previousAssets };
            }
        });
    }

    private void SetLoadout(string classId, GameplayClassLoadoutDefinition loadout)
    {
        if (!_classes.TryGetValue(classId, out var gameplayClass))
        {
            return;
        }

        var loadoutId = loadout.Id;
        var hadLoadout = gameplayClass.Loadouts.TryGetValue(loadoutId, out var previousLoadout);
        ReplaceClassLoadout(classId, loadoutId, loadout);
        // Undo against the class as it is then, so other owners' loadouts on a shared class survive.
        RecordRegistration(OwnedRegistrationKind.Loadout, classId, loadoutId, () =>
            ReplaceClassLoadout(classId, loadoutId, hadLoadout ? previousLoadout : null));
    }

    private void ReplaceClassLoadout(string classId, string loadoutId, GameplayClassLoadoutDefinition? loadout)
    {
        if (!_classes.TryGetValue(classId, out var gameplayClass))
        {
            return;
        }

        _characterClassDefinitionCache.Clear();
        var loadouts = gameplayClass.Loadouts.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        if (loadout is null)
        {
            loadouts.Remove(loadoutId);
        }
        else
        {
            loadouts[loadoutId] = loadout;
        }

        _classes[classId] = gameplayClass with { Loadouts = loadouts };
    }

    private void SetPrimaryWeaponBehavior(GameplayPrimaryWeaponRuntimeBinding binding)
    {
        var behaviorId = binding.BehaviorId;
        var hadBinding = _primaryWeaponBindingsByBehaviorId.TryGetValue(behaviorId, out var previousBinding);
        _characterClassDefinitionCache.Clear();
        _primaryWeaponBindingsByBehaviorId[behaviorId] = binding;
        RecordRegistration(OwnedRegistrationKind.PrimaryWeaponBehavior, behaviorId, string.Empty, () =>
        {
            if (hadBinding)
            {
                _primaryWeaponBindingsByBehaviorId[behaviorId] = previousBinding;
            }
            else
            {
                _primaryWeaponBindingsByBehaviorId.Remove(behaviorId);
            }
        });
    }

    private void SetAbilityExecutor(string executorId, IGameplayAbilityExecutor executor)
    {
        var hadExecutor = _abilityExecutorsById.TryGetValue(executorId, out var previousExecutor);
        _abilityExecutorsById[executorId] = executor;
        RecordRegistration(OwnedRegistrationKind.AbilityExecutor, executorId, string.Empty, () =>
        {
            if (hadExecutor)
            {
                _abilityExecutorsById[executorId] = previousExecutor!;
            }
            else
            {
                _abilityExecutorsById.Remove(executorId);
            }
        });
    }

    private enum OwnedRegistrationKind
    {
        ModPack,
        Item,
        Class,
        LegacyClassBinding,
        Loadout,
        PrimaryWeaponBehavior,
        AbilityExecutor,
        PackDirectory,
        PackSprites,
    }

    // Key is the registered id; SubKey is the owning mod pack for items and classes and
    // the loadout id for loadouts.
    private sealed record OwnedRegistration(OwnedRegistrationKind Kind, string Key, string SubKey, Action Undo);

    private sealed class RegistrationOwner
    {
        public List<OwnedRegistration> Entries { get; } = [];
    }

    private sealed class RegistrationScope(GameplayRuntimeRegistry registry, string ownerKey) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            registry.EndRegistrationScope(ownerKey);
        }
    }
}
