using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerReadOnlyState
{
    string ServerName { get; }

    string LevelName { get; }

    int MapAreaIndex { get; }

    int MapAreaCount { get; }

    float MapScale { get; }

    GameModeKind GameMode { get; }

    MatchPhase MatchPhase { get; }

    int RedCaps { get; }

    int BlueCaps { get; }

    IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers();

    IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks();

    IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null);

    IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null);

    IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot);

    IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId);

    IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot);

    IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot);

    IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot);

    bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value);

    bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value);

    bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value);
}
