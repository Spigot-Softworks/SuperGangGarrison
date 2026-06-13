#nullable enable

using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly CivvieMoneyTrailTracker _civvieMoneyPresentationTracker = new();
    private readonly List<CivvieMoneyPickupParticipant> _civvieMoneyParticipantBuffer = new();

    private void ResetCivvieMoneyTrailPresentation()
    {
        _civvieMoneyPresentationTracker.Clear();
    }

    private void PlayPendingCivvieMoneyTrailSpawns()
    {
        if (_networkClient.IsConnected)
        {
            return;
        }

        foreach (var spawn in _world.DrainPendingCivvieMoneyTrailSpawns())
        {
            SpawnCivvieMoneyVisual(spawn);
        }
    }

    private void ProcessOnlineCivvieMoneyTrailPresentation(SnapshotMessage snapshot)
    {
        if (!_networkClient.IsConnected || _world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        var ticksPerSecond = snapshot.TickRate > 0
            ? snapshot.TickRate
            : SimulationConfig.DefaultTicksPerSecond;

        foreach (var (_, player) in _world.EnumerateReplicatedNetworkPlayers())
        {
            _civvieMoneyPresentationTracker.TryRegisterTrail(snapshot.Frame, ticksPerSecond, player);
        }

        _civvieMoneyParticipantBuffer.Clear();
        foreach (var (_, player) in _world.EnumerateReplicatedNetworkPlayers())
        {
            _civvieMoneyParticipantBuffer.Add(CivvieMoneyTrailTracker.CreateParticipant(player));
        }

        _civvieMoneyPresentationTracker.AdvancePickups(
            _civvieMoneyParticipantBuffer,
            static (participant, _) => participant.Health < participant.MaxHealth);

        foreach (var spawn in _civvieMoneyPresentationTracker.DrainPendingSpawns())
        {
            SpawnCivvieMoneyVisual(spawn);
        }
    }
}
