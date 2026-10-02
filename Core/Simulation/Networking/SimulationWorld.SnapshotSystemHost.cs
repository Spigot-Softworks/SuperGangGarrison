namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    byte ISnapshotSystemHost.FirstSpectatorSlot => FirstSpectatorSlot;
    bool ISnapshotSystemHost.AreSpecialAbilitiesEnabled => ExperimentalGameplaySettings.EnableSecondaryAbilities;
    bool ISnapshotSystemHost.IsPlayableNetworkPlayerSlot(byte slot) => NetworkPlayerSystem.IsPlayableNetworkPlayerSlot(slot);
    bool ISnapshotSystemHost.IsNetworkPlayerAwaitingJoin(byte slot) => NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot);
    PlayerTeam ISnapshotSystemHost.GetNetworkPlayerConfiguredTeam(byte slot) => NetworkPlayerRules.GetNetworkPlayerConfiguredTeam(slot);
    int ISnapshotSystemHost.GetNetworkPlayerRespawnTicks(byte slot) => NetworkPlayerRules.GetNetworkPlayerRespawnTicks(slot);
    bool ISnapshotSystemHost.IsNetworkPlayerReady(byte slot) => ReadyUp.IsNetworkPlayerReady(slot);
}
