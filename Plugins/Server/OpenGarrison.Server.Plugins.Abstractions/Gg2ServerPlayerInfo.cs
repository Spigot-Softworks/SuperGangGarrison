using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerPlayerInfo(
    byte Slot,
    int UserId,
    string Name,
    bool IsSpectator,
    bool IsAuthorized,
    bool IsGagged,
    bool IsAlive,
    int? PlayerId,
    PlayerTeam? Team,
    PlayerClass? PlayerClass,
    float PlayerScale,
    string EndPoint,
    string GameplayLoadoutId,
    string GameplaySecondaryItemId,
    string GameplayAcquiredItemId,
    GameplayEquipmentSlot GameplayEquippedSlot,
    string GameplayEquippedItemId);

public readonly record struct OpenGarrisonServerGameplayLoadoutInfo(
    string LoadoutId,
    string DisplayName,
    string PrimaryItemId,
    string? SecondaryItemId,
    string? UtilityItemId,
    bool IsSelected,
    bool IsAvailableToPlayer);

public readonly record struct OpenGarrisonServerGameplayModPackInfo(
    string ModPackId,
    string DisplayName,
    string Version,
    int ItemCount,
    int ClassCount,
    bool IsBoundToPlayableClasses);

public readonly record struct OpenGarrisonServerGameplayClassInfo(
    string ModPackId,
    string ClassId,
    string DisplayName,
    string DefaultLoadoutId,
    int LoadoutCount);

public readonly record struct OpenGarrisonServerGameplayItemInfo(
    string ModPackId,
    string ItemId,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    bool TracksOwnership,
    bool DefaultGranted,
    bool GrantOnAcquire,
    string? GrantKey);

public readonly record struct OpenGarrisonServerGameplaySelectableItemInfo(
    string ItemId,
    string DisplayName,
    GameplayEquipmentSlot Slot,
    string BehaviorId,
    bool IsCurrentlySelected,
    bool IsOwnedByPlayer,
    bool IsOwnershipTracked,
    bool IsDefaultGranted,
    bool IsGrantOnAcquire,
    string? GrantKey);
