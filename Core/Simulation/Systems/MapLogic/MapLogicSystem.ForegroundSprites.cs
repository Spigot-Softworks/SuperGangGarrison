using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

internal sealed partial class MapLogicSystem
{

    internal void RebuildForegroundJungleSpriteCache()
    {
        var indices = new List<int>();
        for (var index = 0; index < _host.Level.RoomObjects.Count; index += 1)
        {
            var marker = _host.Level.RoomObjects[index];
            if (marker.Type == RoomObjectType.ForegroundSprite && marker.ForegroundSprite.Jungle)
            {
                indices.Add(index);
            }
        }

        _host.MapRuntime.ForegroundJungleRoomObjectIndices = indices.ToArray();
    }

    internal void TickForegroundSpriteJungle()
    {
        if (_host.MapRuntime.ForegroundJungleRoomObjectIndices.Length == 0)
        {
            return;
        }

        IReadOnlyDictionary<int, ForegroundSpriteHitMask> hitMasks =
            _host.Level.CustomMapVisuals.ForegroundSpriteJungleHitMasks
            ?? CustomMapVisualMetadata.Empty.ForegroundSpriteJungleHitMasks
            ?? new Dictionary<int, ForegroundSpriteHitMask>();
        foreach (var player in EnumerateForegroundSpriteJunglePlayers())
        {
            if (!player.IsAlive)
            {
                ClearForegroundSpriteJungleState(player);
                continue;
            }

            foreach (var roomObjectIndex in _host.MapRuntime.ForegroundJungleRoomObjectIndices)
            {
                if (!_host.Level.IsRoomObjectActive(roomObjectIndex))
                {
                    UpdateForegroundSpriteJungleState(player, roomObjectIndex, isInside: false);
                    continue;
                }

                var marker = _host.Level.RoomObjects[roomObjectIndex];
                hitMasks.TryGetValue(roomObjectIndex, out var hitMask);
                var isInside = ForegroundSpriteMetadata.IsPlayerInsideWithExtensions(
                    _host.Level.RoomObjects,
                    roomObjectIndex,
                    player.X,
                    player.Y,
                    marker.ForegroundSprite.Boundary,
                    hitMask,
                    _host.Level.IsRoomObjectActive);
                UpdateForegroundSpriteJungleState(player, roomObjectIndex, isInside);
            }
        }
    }

    private IEnumerable<PlayerEntity> EnumerateForegroundSpriteJunglePlayers()
    {
        var yielded = new HashSet<int>();
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (player is null || !yielded.Add(player.Id))
            {
                continue;
            }

            yield return player;
        }

        if (_host.LocalPlayer.IsAlive && yielded.Add(_host.LocalPlayer.Id))
        {
            yield return _host.LocalPlayer;
        }
    }

    private void ClearForegroundSpriteJungleState(PlayerEntity player)
    {
        foreach (var roomObjectIndex in _host.MapRuntime.ForegroundJungleRoomObjectIndices)
        {
            UpdateForegroundSpriteJungleState(player, roomObjectIndex, isInside: false);
        }
    }

    private static void UpdateForegroundSpriteJungleState(PlayerEntity player, int roomObjectIndex, bool isInside)
    {
        var key = ForegroundSpriteMetadata.JungleReplicatedStateKey(roomObjectIndex);
        if (isInside)
        {
            player.SetReplicatedStateBool(
                ForegroundSpriteMetadata.JungleReplicatedStateOwnerId,
                key,
                true);
            return;
        }

        player.ClearReplicatedState(
            ForegroundSpriteMetadata.JungleReplicatedStateOwnerId,
            key);
    }
}
