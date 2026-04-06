using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerPlayerInfo(
    byte Slot,
    string Name,
    bool IsSpectator,
    bool IsAuthorized,
    PlayerTeam? Team,
    PlayerClass? PlayerClass,
    string EndPoint,
    string GameplayLoadoutId,
    GameplayEquipmentSlot GameplayEquippedSlot,
    string GameplayEquippedItemId);

public readonly record struct OpenGarrisonServerGameplayLoadoutInfo(
    string LoadoutId,
    string DisplayName,
    string PrimaryItemId,
    string? SecondaryItemId,
    string? UtilityItemId,
    bool IsSelected);
