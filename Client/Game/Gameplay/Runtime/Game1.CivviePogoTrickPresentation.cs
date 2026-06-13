#nullable enable

using System.Collections.Generic;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Dictionary<int, int> _civviePogoTrickDurationTicksByPlayerId = new();
    private readonly Dictionary<int, int> _civviePogoTrickPreviousTicksByPlayerId = new();

    private void ResetCivviePogoTrickPresentationObservation()
    {
        _civviePogoTrickDurationTicksByPlayerId.Clear();
        _civviePogoTrickPreviousTicksByPlayerId.Clear();
    }

    private void ObserveCivviePogoTrickPresentationFromPlayerState()
    {
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || player.ClassId != PlayerClass.Quote)
            {
                _civviePogoTrickDurationTicksByPlayerId.Remove(player.Id);
                _civviePogoTrickPreviousTicksByPlayerId.Remove(player.Id);
                continue;
            }

            var previousTicks = _civviePogoTrickPreviousTicksByPlayerId.GetValueOrDefault(player.Id);
            var currentTicks = player.CivviePogoTrickTicksRemaining;
            if (previousTicks <= 0 && currentTicks > 0)
            {
                _civviePogoTrickDurationTicksByPlayerId[player.Id] = currentTicks;
            }
            else if (currentTicks <= 0)
            {
                _civviePogoTrickDurationTicksByPlayerId.Remove(player.Id);
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
            _civviePogoTrickPreviousTicksByPlayerId.Remove(playerId);
        }
    }

    private int GetCivviePogoTrickPresentationFrameIndex(PlayerEntity player, int frameCount)
    {
        if (!player.IsCivviePogoTrickActive || frameCount <= 0)
        {
            return 0;
        }

        var durationTicks = player.CivviePogoTrickDurationAtStart > 0
            ? player.CivviePogoTrickDurationAtStart
            : _civviePogoTrickDurationTicksByPlayerId.GetValueOrDefault(
                player.Id,
                PlayerEntity.CivviePogoTrickDurationTicksDefault);
        return CivviePogoTrickRules.ResolveTrickFrameIndex(
            _world.SessionPresentationSeed,
            player.Id,
            (ulong)System.Math.Max(0, _world.Frame),
            durationTicks,
            player.CivviePogoTrickTicksRemaining,
            frameCount);
    }
}
