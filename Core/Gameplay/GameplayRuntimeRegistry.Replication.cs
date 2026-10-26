using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class GameplayRuntimeRegistry
{
    /// <summary>Registry owner of content received from the connected server.</summary>
    public const string ReplicatedContentOwnerKey = "replicated-server-content";

    /// <summary>
    /// Captures the current state of every owned (plugin-registered) item, class, loadout and
    /// primary weapon behavior. Stock content is never included.
    /// </summary>
    public GameplayReplicatedContent CreateReplicatedContent()
    {
        var items = new Dictionary<string, GameplayReplicatedItem>(StringComparer.Ordinal);
        var classes = new Dictionary<string, GameplayReplicatedClass>(StringComparer.Ordinal);
        var loadouts = new Dictionary<(string ClassId, string LoadoutId), GameplayReplicatedLoadout>();
        var behaviors = new Dictionary<string, GameplayReplicatedPrimaryWeaponBehavior>(StringComparer.Ordinal);
        var assetPacks = new Dictionary<string, GameplayReplicatedAssetPack>(StringComparer.Ordinal);
        foreach (var registration in EnumerateOwnedRegistrations())
        {
            switch (registration.Kind)
            {
                case OwnedRegistrationKind.Item
                    when _items.TryGetValue(registration.Key, out var item)
                        && _itemOwningModPackIds.TryGetValue(registration.Key, out var itemModPackId):
                    items[registration.Key] = new GameplayReplicatedItem(itemModPackId, item);
                    break;
                case OwnedRegistrationKind.Class
                    when _classes.TryGetValue(registration.Key, out var gameplayClass)
                        && _classOwningModPackIds.TryGetValue(registration.Key, out var classModPackId)
                        && _classBindingsByClassId.TryGetValue(registration.Key, out var binding):
                    classes[registration.Key] = new GameplayReplicatedClass(classModPackId, gameplayClass, binding);
                    break;
                case OwnedRegistrationKind.Loadout
                    when _classes.TryGetValue(registration.Key, out var loadoutClass)
                        && loadoutClass.Loadouts.TryGetValue(registration.SubKey, out var loadout):
                    loadouts[(registration.Key, registration.SubKey)] = new GameplayReplicatedLoadout(registration.Key, loadout);
                    break;
                case OwnedRegistrationKind.PrimaryWeaponBehavior
                    when _primaryWeaponBindingsByBehaviorId.TryGetValue(registration.Key, out var behavior):
                    behaviors[registration.Key] = new GameplayReplicatedPrimaryWeaponBehavior(registration.Key, behavior.FireSoundName);
                    break;
                case OwnedRegistrationKind.PackDirectory
                    when !assetPacks.ContainsKey(registration.Key)
                        && TryCreateReplicatedAssetPack(registration.Key, out var assetPack):
                    assetPacks[registration.Key] = assetPack;
                    break;
            }
        }

        // A replicated class already carries its current loadouts.
        return new GameplayReplicatedContent(
            items.Values.ToArray(),
            classes.Values.ToArray(),
            loadouts.Values.Where(loadout => !classes.ContainsKey(loadout.ClassId)).ToArray(),
            behaviors.Values.ToArray(),
            assetPacks.Values.ToArray());
    }

    // Packs whose art can't be published (missing or invalid files) are left out; their
    // classes still sync and fall back to stock sprites on clients.
    private bool TryCreateReplicatedAssetPack(string modPackId, out GameplayReplicatedAssetPack assetPack)
    {
        assetPack = null!;
        if (!_modPacks.TryGetValue(modPackId, out var modPack)
            || (modPack.Assets.Sprites.Count == 0 && modPack.Assets.SoundsOrEmpty.Count == 0)
            || !TryGetPackDirectory(modPackId, out var directory)
            || !GameplayPackAssetManifest.TryCreate(modPackId, directory, modPack.Assets, out var manifest, out _))
        {
            return false;
        }

        assetPack = new GameplayReplicatedAssetPack(
            modPackId,
            manifest.ContentHash,
            GameplayPackAssetManifest.BuildManifestUrl(modPackId, manifest.ContentHash),
            modPack.Assets.Sprites,
            modPack.Assets.SoundsOrEmpty.Count == 0 ? null : modPack.Assets.SoundsOrEmpty);
        return true;
    }

    /// <summary>
    /// Registers content received from a server under <paramref name="ownerKey"/>. Content that
    /// already exists unchanged (for example from a server running in this process) is skipped.
    /// Replicated content ignores the registry seal and has no executors: the server runs
    /// plugin behavior authoritatively.
    /// </summary>
    public void ApplyReplicatedContent(GameplayReplicatedContent content, string ownerKey)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var scope = BeginRegistrationScope(ownerKey);
        foreach (var behavior in content.PrimaryWeaponBehaviors)
        {
            if (!_primaryWeaponBindingsByBehaviorId.ContainsKey(behavior.BehaviorId))
            {
                SetPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(
                    behavior.BehaviorId,
                    PrimaryWeaponKind.Custom,
                    behavior.FireSoundName));
            }
        }

        foreach (var replicatedItem in content.Items)
        {
            var item = replicatedItem.Item;
            if (!_items.TryGetValue(item.Id, out var existingItem) || !IsSameDefinition(existingItem, item))
            {
                SetItem(replicatedItem.ModPackId, item);
            }
        }

        foreach (var replicatedClass in content.Classes)
        {
            var gameplayClass = replicatedClass.Class;
            if (_classes.TryGetValue(gameplayClass.Id, out var existingClass) && IsSameDefinition(existingClass, gameplayClass))
            {
                continue;
            }

            SetClass(replicatedClass.ModPackId, gameplayClass, replicatedClass.Binding);
            if (replicatedClass.Binding.BindsLegacyPlayerClass)
            {
                SetLegacyClassBinding(replicatedClass.Binding);
            }
        }

        foreach (var replicatedLoadout in content.Loadouts)
        {
            if (_classes.TryGetValue(replicatedLoadout.ClassId, out var gameplayClass)
                && !(gameplayClass.Loadouts.TryGetValue(replicatedLoadout.Loadout.Id, out var existingLoadout)
                    && IsSameDefinition(existingLoadout, replicatedLoadout.Loadout)))
            {
                SetLoadout(replicatedLoadout.ClassId, replicatedLoadout.Loadout);
            }
        }

        foreach (var assetPack in content.AssetPacks ?? [])
        {
            var sounds = assetPack.Sounds ?? new Dictionary<string, GameplaySoundAssetDefinition>(StringComparer.Ordinal);
            if (!(_modPacks.TryGetValue(assetPack.PackId, out var existingPack)
                && IsSameDefinition(existingPack.Assets.Sprites, assetPack.Sprites)
                && IsSameDefinition(existingPack.Assets.SoundsOrEmpty, sounds)))
            {
                SetPackAssets(assetPack.PackId, assetPack.Sprites, sounds);
            }
        }

        _characterClassDefinitionCache.Clear();
        ModPackRevision += 1;
    }

    private static bool IsSameDefinition<T>(T existing, T incoming)
    {
        return ReferenceEquals(existing, incoming)
            || string.Equals(
                GameplayReplicatedContent.SerializeForComparison(existing),
                GameplayReplicatedContent.SerializeForComparison(incoming),
                StringComparison.Ordinal);
    }
}
