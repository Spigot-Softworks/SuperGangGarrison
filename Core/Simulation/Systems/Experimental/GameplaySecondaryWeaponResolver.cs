namespace OpenGarrison.Core;

/// <summary>Resolves the secondary weapon a player's loadout grants.</summary>
internal static class GameplaySecondaryWeaponResolver
{
    public static PrimaryWeaponDefinition? Resolve(
        PlayerEntity player,
        bool allowSoldierShotgun,
        bool allowSoldierShotgunLtd)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (allowSoldierShotgunLtd && player.ClassId == PlayerClass.Soldier)
        {
            return CharacterClassCatalog.SoldierShotgunLtd;
        }

        var secondaryItemId = player.GameplayLoadoutState.SecondaryItemId;
        if (!string.IsNullOrWhiteSpace(secondaryItemId))
        {
            var secondaryItem = runtimeRegistry.GetRequiredItem(secondaryItemId);
            if (runtimeRegistry.TryGetPrimaryWeaponBinding(secondaryItem.BehaviorId, out _))
            {
                return runtimeRegistry.CreatePrimaryWeaponDefinition(secondaryItem);
            }
        }

        return allowSoldierShotgun && player.ClassId == PlayerClass.Soldier
            ? CharacterClassCatalog.SoldierShotgun
            : null;
    }
}
