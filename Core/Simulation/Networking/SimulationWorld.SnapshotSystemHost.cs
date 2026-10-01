namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    byte ISnapshotSystemHost.FirstSpectatorSlot => FirstSpectatorSlot;
    bool ISnapshotSystemHost.AreSpecialAbilitiesEnabled => ExperimentalGameplaySettings.EnableSecondaryAbilities;
    bool ISnapshotSystemHost.IsPlayableNetworkPlayerSlot(byte slot) => IsPlayableNetworkPlayerSlot(slot);
    bool ISnapshotSystemHost.IsNetworkPlayerAwaitingJoin(byte slot) => IsNetworkPlayerAwaitingJoin(slot);
    PlayerTeam ISnapshotSystemHost.GetNetworkPlayerConfiguredTeam(byte slot) => GetNetworkPlayerConfiguredTeam(slot);
    int ISnapshotSystemHost.GetNetworkPlayerRespawnTicks(byte slot) => GetNetworkPlayerRespawnTicks(slot);
    bool ISnapshotSystemHost.IsNetworkPlayerReady(byte slot) => IsNetworkPlayerReady(slot);
}
