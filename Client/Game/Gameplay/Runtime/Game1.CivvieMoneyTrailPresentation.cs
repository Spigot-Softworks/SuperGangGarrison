#nullable enable

using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly CivvieMoneyTrailTracker _civvieMoneyPresentationTracker = new();
    private readonly List<CivvieMoneyPickupParticipant> _civvieMoneyParticipantBuffer = new();
    private readonly HashSet<int> _civvieMoneyTrailPlayerIds = new();

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

        // The replicated set covers remote players only; the locally-predicted player is not in it.
        // Process the local player first (its predicted entity carries the authoritative movement/pogo
        // state the trail rules need) and dedupe by id so it is never counted twice.
        _civvieMoneyParticipantBuffer.Clear();
        _civvieMoneyTrailPlayerIds.Clear();

        var localPlayer = _world.LocalPlayer;
        if (_civvieMoneyTrailPlayerIds.Add(localPlayer.Id))
        {
            _civvieMoneyPresentationTracker.TryRegisterTrail(snapshot.Frame, ticksPerSecond, localPlayer);
            _civvieMoneyParticipantBuffer.Add(CivvieMoneyTrailTracker.CreateParticipant(localPlayer));
        }

        foreach (var (_, player) in _world.EnumerateReplicatedNetworkPlayers())
        {
            if (!_civvieMoneyTrailPlayerIds.Add(player.Id))
            {
                continue;
            }

            _civvieMoneyPresentationTracker.TryRegisterTrail(snapshot.Frame, ticksPerSecond, player);
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

    private void SpawnCivviePogoTrickMoneyBurst(PlayerEntity player, ulong frame)
    {
        for (var particleIndex = 0; particleIndex < CivvieMoneyTrailRules.PogoTrickBurstParticleCount; particleIndex += 1)
        {
            var spawn = CivvieMoneyTrailRules.CreatePogoTrickBurstSpawn(
                frame,
                player.Id,
                particleIndex,
                player.X,
                player.Y);
            SpawnCivvieMoneyBurstVisual(spawn);
        }
    }
}
