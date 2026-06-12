using System;
using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private int[] _foregroundJungleRoomObjectIndices = [];

    private void RebuildForegroundJungleSpriteCache()
    {
        var indices = new List<int>();
        for (var index = 0; index < Level.RoomObjects.Count; index += 1)
        {
            var marker = Level.RoomObjects[index];
            if (marker.Type == RoomObjectType.ForegroundSprite && marker.ForegroundSprite.Jungle)
            {
                indices.Add(index);
            }
        }

        _foregroundJungleRoomObjectIndices = indices.ToArray();
    }

    public void TickForegroundSpriteJungle()
    {
        if (_foregroundJungleRoomObjectIndices.Length == 0)
        {
            return;
        }

        IReadOnlyDictionary<int, ForegroundSpriteHitMask> hitMasks =
            Level.CustomMapVisuals.ForegroundSpriteJungleHitMasks
            ?? CustomMapVisualMetadata.Empty.ForegroundSpriteJungleHitMasks
            ?? new Dictionary<int, ForegroundSpriteHitMask>();
        foreach (var player in EnumerateForegroundSpriteJunglePlayers())
        {
            if (player is null || !player.IsAlive)
            {
                ClearForegroundSpriteJungleState(player);
                continue;
            }

            foreach (var roomObjectIndex in _foregroundJungleRoomObjectIndices)
            {
                if (!Level.IsRoomObjectActive(roomObjectIndex))
                {
                    UpdateForegroundSpriteJungleState(player, roomObjectIndex, isInside: false);
                    continue;
                }

                var marker = Level.RoomObjects[roomObjectIndex];
                hitMasks.TryGetValue(roomObjectIndex, out var hitMask);
                var isInside = ForegroundSpriteMetadata.IsPlayerInsideWithExtensions(
                    Level.RoomObjects,
                    roomObjectIndex,
                    player.X,
                    player.Y,
                    marker.ForegroundSprite.Boundary,
                    hitMask,
                    Level.IsRoomObjectActive);
                UpdateForegroundSpriteJungleState(player, roomObjectIndex, isInside);
            }
        }
    }

    private IEnumerable<PlayerEntity> EnumerateForegroundSpriteJunglePlayers()
    {
        var yielded = new HashSet<int>();
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (player is null || !yielded.Add(player.Id))
            {
                continue;
            }

            yield return player;
        }

        if (LocalPlayer.IsAlive && yielded.Add(LocalPlayer.Id))
        {
            yield return LocalPlayer;
        }
    }

    private void ClearForegroundSpriteJungleState(PlayerEntity player)
    {
        foreach (var roomObjectIndex in _foregroundJungleRoomObjectIndices)
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
