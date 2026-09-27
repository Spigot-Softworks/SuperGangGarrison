namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private SnapshotSystemDependencies CreateSnapshotSystemDependencies()
    {
        return new SnapshotSystemDependencies
        {
            IsPlayableNetworkPlayerSlot = IsPlayableNetworkPlayerSlot,
            FirstSpectatorSlot = FirstSpectatorSlot,
            IsNetworkPlayerAwaitingJoin = IsNetworkPlayerAwaitingJoin,
            GetNetworkPlayerConfiguredTeam = GetNetworkPlayerConfiguredTeam,
            GetNetworkPlayerRespawnTicks = GetNetworkPlayerRespawnTicks,
            IsNetworkPlayerReady = IsNetworkPlayerReady,
        };
    }
}
