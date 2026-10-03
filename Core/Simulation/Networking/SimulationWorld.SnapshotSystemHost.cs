namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    byte ISnapshotSystemHost.FirstSpectatorSlot => FirstSpectatorSlot;
    bool ISnapshotSystemHost.AreSpecialAbilitiesEnabled => ExperimentalGameplaySettings.EnableSecondaryAbilities;
    bool ISnapshotSystemHost.IsPlayableNetworkPlayerSlot(byte slot) => NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot);
    bool ISnapshotSystemHost.IsNetworkPlayerAwaitingJoin(byte slot) => NetworkPlayers.IsNetworkPlayerAwaitingJoin(slot);
    PlayerTeam ISnapshotSystemHost.GetNetworkPlayerConfiguredTeam(byte slot) => NetworkPlayers.GetNetworkPlayerConfiguredTeam(slot);
    int ISnapshotSystemHost.GetNetworkPlayerRespawnTicks(byte slot) => NetworkPlayers.GetNetworkPlayerRespawnTicks(slot);
    bool ISnapshotSystemHost.IsNetworkPlayerReady(byte slot) => ReadyUp.IsNetworkPlayerReady(slot);
}
