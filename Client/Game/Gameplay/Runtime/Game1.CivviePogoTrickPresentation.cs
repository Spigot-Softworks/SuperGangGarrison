#nullable enable

using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int CivviePogoTrickPresentationDropoutGraceTicks = 3;
    private readonly Dictionary<int, int> _civviePogoTrickDurationTicksByPlayerId = new();
    private readonly Dictionary<int, int> _civviePogoTrickPresentationTicksByPlayerId = new();
    private readonly Dictionary<int, int> _civviePogoTrickPresentationGraceByPlayerId = new();
    private readonly Dictionary<int, int> _civviePogoTrickPresentationFrameByPlayerId = new();
    private readonly Dictionary<int, int> _civviePogoTrickPreviousTicksByPlayerId = new();
    private readonly HashSet<int> _civviePogoTrickBurstSpawnedPlayerIds = new();

    private void ResetCivviePogoTrickPresentationObservation()
    {
        _civviePogoTrickDurationTicksByPlayerId.Clear();
        _civviePogoTrickPresentationTicksByPlayerId.Clear();
        _civviePogoTrickPresentationGraceByPlayerId.Clear();
        _civviePogoTrickPresentationFrameByPlayerId.Clear();
        _civviePogoTrickPreviousTicksByPlayerId.Clear();
        _civviePogoTrickBurstSpawnedPlayerIds.Clear();
    }

    private void ObserveCivviePogoTrickPresentationFromPlayerState()
    {
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || player.ClassId != PlayerClass.Quote)
            {
                _civviePogoTrickDurationTicksByPlayerId.Remove(player.Id);
                _civviePogoTrickPresentationTicksByPlayerId.Remove(player.Id);
                _civviePogoTrickPresentationGraceByPlayerId.Remove(player.Id);
                _civviePogoTrickPresentationFrameByPlayerId.Remove(player.Id);
                _civviePogoTrickPreviousTicksByPlayerId.Remove(player.Id);
                _civviePogoTrickBurstSpawnedPlayerIds.Remove(player.Id);
                continue;
            }

            var currentTicks = GetPlayerCivviePogoTrickTicksRemaining(player);
            if (currentTicks > 0)
            {
                _civviePogoTrickPresentationTicksByPlayerId[player.Id] = currentTicks;
                _civviePogoTrickPresentationGraceByPlayerId[player.Id] = CivviePogoTrickPresentationDropoutGraceTicks;
                if (_civviePogoTrickBurstSpawnedPlayerIds.Add(player.Id))
                {
                    _civviePogoTrickDurationTicksByPlayerId[player.Id] = currentTicks;
                    SpawnCivviePogoTrickMoneyBurst(
                        GetPlayerCivviePresentationSource(player),
                        (ulong)Math.Max(0, _world.Frame));
                }
            }
            else if (_civviePogoTrickPresentationTicksByPlayerId.TryGetValue(player.Id, out var presentationTicks)
                && _civviePogoTrickPresentationGraceByPlayerId.TryGetValue(player.Id, out var graceTicks)
                && graceTicks > 0)
            {
                // Snapshot interpolation can briefly expose the completed state
                // between two positive trick states. Keep the presentation alive
                // for a few client ticks so the trick sprite cannot flash back to
                // the normal body animation.
                _civviePogoTrickPresentationTicksByPlayerId[player.Id] = Math.Max(1, presentationTicks - 1);
                _civviePogoTrickPresentationGraceByPlayerId[player.Id] = graceTicks - 1;
            }
            else
            {
                _civviePogoTrickDurationTicksByPlayerId.Remove(player.Id);
                _civviePogoTrickPresentationTicksByPlayerId.Remove(player.Id);
                _civviePogoTrickPresentationGraceByPlayerId.Remove(player.Id);
                _civviePogoTrickPresentationFrameByPlayerId.Remove(player.Id);
                _civviePogoTrickBurstSpawnedPlayerIds.Remove(player.Id);
            }

            _civviePogoTrickPreviousTicksByPlayerId[player.Id] = currentTicks;
        }

        if (_civviePogoTrickPreviousTicksByPlayerId.Count == 0)
        {
            return;
        }

        var stalePlayerIds = new List<int>();
        foreach (var playerId in _civviePogoTrickPreviousTicksByPlayerId.Keys)
        {
            if (FindPlayerById(playerId) is not { IsAlive: true } found
                || found.ClassId != PlayerClass.Quote)
            {
                stalePlayerIds.Add(playerId);
            }
        }

        for (var index = 0; index < stalePlayerIds.Count; index += 1)
        {
            var playerId = stalePlayerIds[index];
            _civviePogoTrickDurationTicksByPlayerId.Remove(playerId);
            _civviePogoTrickPresentationTicksByPlayerId.Remove(playerId);
            _civviePogoTrickPresentationGraceByPlayerId.Remove(playerId);
            _civviePogoTrickPresentationFrameByPlayerId.Remove(playerId);
            _civviePogoTrickPreviousTicksByPlayerId.Remove(playerId);
            _civviePogoTrickBurstSpawnedPlayerIds.Remove(playerId);
        }
    }

    private int GetCivviePogoTrickPresentationFrameIndex(PlayerEntity player, int frameCount)
    {
        if (frameCount <= 0
            || (!GetPlayerIsCivviePogoTrickActive(player)
                && !_civviePogoTrickPresentationTicksByPlayerId.ContainsKey(player.Id)))
        {
            return 0;
        }

        var predictedDurationTicks = GetPlayerCivviePogoTrickDurationAtStart(player);
        var durationTicks = predictedDurationTicks > 0
            ? predictedDurationTicks
            : _civviePogoTrickDurationTicksByPlayerId.GetValueOrDefault(
                player.Id,
                PlayerEntity.CivviePogoTrickDurationTicksDefault);
        var presentationTicks = GetPlayerCivviePogoTrickTicksRemaining(player);
        if (presentationTicks <= 0)
        {
            presentationTicks = _civviePogoTrickPresentationTicksByPlayerId.GetValueOrDefault(player.Id, 1);
        }
        if (_civviePogoTrickPresentationFrameByPlayerId.TryGetValue(player.Id, out var latchedFrame))
        {
            return System.Math.Clamp(latchedFrame, 0, frameCount - 1);
        }

        var resolvedFrame = CivviePogoTrickRules.ResolveTrickFrameIndex(
            _world.SessionPresentationSeed,
            player.Id,
            (ulong)System.Math.Max(0, _world.Frame),
            durationTicks,
            presentationTicks,
            frameCount);
        _civviePogoTrickPresentationFrameByPlayerId[player.Id] = resolvedFrame;
        return resolvedFrame;
    }
}
