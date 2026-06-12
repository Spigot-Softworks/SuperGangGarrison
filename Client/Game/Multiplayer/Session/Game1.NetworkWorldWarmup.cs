#nullable enable

using OpenGarrison.Protocol;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void BeginNetworkWorldWarmup(string levelName)
    {
        if (!_networkClient.IsConnected || _networkClient.IsReplayConnection)
        {
            _networkWorldWarmupActive = false;
            return;
        }

        _networkWorldWarmupActive = true;
        _networkWorldWarmupFullSnapshotApplied = false;
        _networkWorldWarmupAppliedSnapshotsAfterFull = 0;
        _networkWorldWarmupStartedClockSeconds = _networkInterpolationClockSeconds;
        ShowJoiningServerLoadingOverlay();
    }

    private void CancelNetworkWorldWarmup()
    {
        _networkWorldWarmupActive = false;
        _networkWorldWarmupFullSnapshotApplied = false;
        _networkWorldWarmupAppliedSnapshotsAfterFull = 0;
        _networkWorldWarmupStartedClockSeconds = -1d;
    }

    private bool IsNetworkWorldWarmupBlockingGameplay()
    {
        return _networkWorldWarmupActive
            && _networkClient.IsConnected
            && !_networkClient.IsReplayConnection;
    }

    private void ObserveAppliedNetworkWorldSnapshot(SnapshotMessage snapshot, bool isServerFullSnapshot)
    {
        if (!IsNetworkWorldWarmupBlockingGameplay())
        {
            return;
        }

        if (isServerFullSnapshot)
        {
            _networkWorldWarmupFullSnapshotApplied = true;
            _networkWorldWarmupAppliedSnapshotsAfterFull = 1;
        }
        else if (_networkWorldWarmupFullSnapshotApplied)
        {
            _networkWorldWarmupAppliedSnapshotsAfterFull += 1;
        }

        if (!HasAuthoritativeLocalPlayerForNetworkWorldWarmup())
        {
            ShowJoiningServerLoadingOverlay();
            return;
        }

        if (!_networkWorldWarmupFullSnapshotApplied)
        {
            ShowJoiningServerLoadingOverlay();
            return;
        }

        if (_networkWorldWarmupAppliedSnapshotsAfterFull < NetworkWorldWarmupMinimumAppliedSnapshotsAfterFull)
        {
            ShowJoiningServerLoadingOverlay();
            return;
        }

        if (!HasFreshRemotePlayerHistoriesForCurrentWorld())
        {
            ShowJoiningServerLoadingOverlay();
            return;
        }

        HideLoadingOverlay();
        CancelNetworkWorldWarmup();
    }

    private bool HasAuthoritativeLocalPlayerForNetworkWorldWarmup()
    {
        return _networkClient.IsSpectator || _localPlayerSnapshotEntityId.HasValue;
    }

    private bool HasFreshRemotePlayerHistoriesForCurrentWorld()
    {
        if (_latestSnapshotServerTimeSeconds < 0d)
        {
            return false;
        }

        if (!HasAuthoritativeLocalPlayerForNetworkWorldWarmup())
        {
            return false;
        }

        foreach (var player in _world.RemoteSnapshotPlayers)
        {
            if (!player.IsAlive)
            {
                continue;
            }

            if (!HasFreshRemotePlayerRenderHistory(player.Id))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasFreshRemotePlayerRenderHistory(int playerId)
    {
        if (!_remotePlayerSnapshotHistories.TryGetValue(playerId, out var history) || history.Count == 0)
        {
            return false;
        }

        return _latestSnapshotServerTimeSeconds < 0d
            || _latestSnapshotServerTimeSeconds - history[^1].TimeSeconds <= NetworkWorldWarmupFreshPlayerHistorySeconds;
    }

    private bool HasFreshPlayerRenderHistory(PlayerEntity player)
    {
        if (!_networkClient.IsConnected || _networkClient.IsReplayConnection)
        {
            return true;
        }

        if (IsNetworkWorldWarmupBlockingGameplay())
        {
            return false;
        }

        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            return true;
        }

        if (!_remotePlayerSnapshotHistories.TryGetValue(player.Id, out var history) || history.Count == 0)
        {
            return true;
        }

        return _latestSnapshotServerTimeSeconds < 0d
            || _latestSnapshotServerTimeSeconds - history[^1].TimeSeconds <= StaleRemotePlayerSnapshotHistoryPruneSeconds;
    }
}
