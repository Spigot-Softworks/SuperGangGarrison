#nullable enable

using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private PlayerEntity? GetSpectatorFocusPlayer()
    {
        if (_networkClient.IsSpectator && !_spectatorTrackingEnabled)
        {
            return null;
        }

        if (_spectatorTrackedPlayerId.HasValue)
        {
            foreach (var player in EnumerateRemotePlayersForView())
            {
                if (player.Id == _spectatorTrackedPlayerId.Value)
                {
                    return player;
                }
            }

            _spectatorTrackedPlayerId = null;
        }

        PlayerEntity? fallback = null;
        foreach (var player in EnumerateRemotePlayersForView())
        {
            if (player.IsAlive)
            {
                _spectatorTrackedPlayerId ??= player.Id;
                return player;
            }

            fallback ??= player;
        }

        _spectatorTrackedPlayerId = fallback?.Id;
        return fallback;
    }

    private void ResetSpectatorTracking(bool enableTracking)
    {
        _spectatorTrackedPlayerId = null;
        _spectatorTrackingEnabled = enableTracking;
    }

    private void CycleSpectatorTracking(bool forward)
    {
        var candidates = GetSpectatorTrackingCandidates();
        if (candidates.Count == 0)
        {
            return;
        }

        var wasTracking = _spectatorTrackingEnabled;
        var currentIndex = -1;
        if (_spectatorTrackedPlayerId.HasValue)
        {
            for (var index = 0; index < candidates.Count; index += 1)
            {
                if (candidates[index].Id == _spectatorTrackedPlayerId.Value)
                {
                    currentIndex = index;
                    break;
                }
            }
        }

        var nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + (forward ? 1 : -1) + candidates.Count) % candidates.Count;
        var trackedPlayer = candidates[nextIndex];
        _spectatorTrackedPlayerId = trackedPlayer.Id;
        _spectatorTrackingEnabled = true;
        _respawnCameraDetached = false;
        _respawnCameraCenter = GetRenderPosition(trackedPlayer);
        if (!wasTracking)
        {
            ShowNotice(NoticeKind.PlayerTrackEnable);
        }
    }

    private List<PlayerEntity> GetSpectatorTrackingCandidates()
    {
        var alivePlayers = new List<PlayerEntity>();
        var fallbackPlayers = new List<PlayerEntity>();
        foreach (var player in EnumerateRemotePlayersForView())
        {
            fallbackPlayers.Add(player);
            if (player.IsAlive)
            {
                alivePlayers.Add(player);
            }
        }

        return alivePlayers.Count > 0
            ? alivePlayers
            : fallbackPlayers;
    }

    private IEnumerable<PlayerEntity> EnumerateRemotePlayersForView()
    {
        if (_networkClient.IsConnected)
        {
            for (var index = 0; index < _world.RemoteSnapshotPlayers.Count; index += 1)
            {
                yield return _world.RemoteSnapshotPlayers[index];
            }

            yield break;
        }

        if (IsOfflineBotSessionActive)
        {
            foreach (var bot in EnumeratePracticeBotPlayersForView())
            {
                yield return bot;
            }
        }

        if (_config.EnableEnemyTrainingDummy && _world.EnemyPlayerEnabled)
        {
            yield return _world.EnemyPlayer;
        }

        if (_config.EnableFriendlySupportDummy && _world.FriendlyDummyEnabled)
        {
            yield return _world.FriendlyDummy;
        }
    }
}
