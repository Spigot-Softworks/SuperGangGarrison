using System;
using System.Collections.Generic;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public readonly record struct CivvieMoneyTrailSpawn(
    float X,
    float Y,
    float HorizontalSpeed,
    ulong Frame,
    int OwnerPlayerId);

public readonly record struct CivvieMoneyPickupParticipant(
    int Id,
    PlayerTeam Team,
    bool IsAlive,
    float Health,
    float MaxHealth,
    Func<float, float, float, float, bool> IntersectsMarker);

public sealed class CivvieMoneyTrailTracker
{
    private sealed class Pickup
    {
        public Pickup(int ownerPlayerId, PlayerTeam team, float x, float y, int ticksRemaining)
        {
            OwnerPlayerId = ownerPlayerId;
            Team = team;
            X = x;
            Y = y;
            TicksRemaining = ticksRemaining;
        }

        public int OwnerPlayerId { get; }

        public PlayerTeam Team { get; }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
    }

    private readonly List<Pickup> _pickups = new();
    private readonly List<CivvieMoneyPickupParticipant> _participantBuffer = new();
    private readonly List<CivvieMoneyTrailSpawn> _pendingSpawns = new();

    public int PickupCount => _pickups.Count;

    public void Clear()
    {
        _pickups.Clear();
        _pendingSpawns.Clear();
    }

    public IReadOnlyList<CivvieMoneyTrailSpawn> DrainPendingSpawns()
    {
        if (_pendingSpawns.Count == 0)
        {
            return Array.Empty<CivvieMoneyTrailSpawn>();
        }

        var spawns = _pendingSpawns.ToArray();
        _pendingSpawns.Clear();
        return spawns;
    }

    public void TryRegisterTrail(
        ulong frame,
        int ticksPerSecond,
        PlayerEntity player)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Quote
            || !player.IsTaunting)
        {
            return;
        }

        if (MathF.Abs(player.HorizontalSpeed) < CivvieMoneyTrailRules.TrailMoveSpeedThreshold
            || !CivvieMoneyTrailRules.ShouldEmitDeterministicSourceTickChance(
                frame,
                player.Id,
                ticksPerSecond,
                CivvieMoneyTrailRules.TrailSpawnChance))
        {
            return;
        }

        var trailDirection = player.HorizontalSpeed == 0f
            ? player.FacingDirectionX
            : -MathF.Sign(player.HorizontalSpeed);
        var pickupX = player.X + (trailDirection * CivvieMoneyTrailRules.TrailHorizontalOffset);
        var pickupY = player.Y
            - CivvieMoneyTrailRules.TrailVerticalBaseOffset
            + CivvieMoneyTrailRules.GetDeterministicVerticalOffset(frame, player.Id);
        if (HasNearbyPickup(player.Id, pickupX, pickupY))
        {
            return;
        }

        PrunePickupsForSpawn(player.Id);
        _pickups.Add(new Pickup(
            player.Id,
            player.Team,
            pickupX,
            pickupY,
            CivvieMoneyTrailRules.PickupLifetimeTicks));
        _pendingSpawns.Add(new CivvieMoneyTrailSpawn(
            pickupX,
            pickupY,
            player.HorizontalSpeed,
            frame,
            player.Id));
    }

    public void AdvancePickups(
        IEnumerable<PlayerEntity> players,
        Func<PlayerEntity, float, bool>? tryApplyHealing = null)
    {
        if (_pickups.Count == 0)
        {
            return;
        }

        _playerBuffer.Clear();
        foreach (var player in players)
        {
            _playerBuffer.Add(player);
        }

        AdvancePickupsCore(_playerBuffer, tryApplyHealing is null ? null : TryConsumeWithHealing);
        return;

        bool TryConsumeWithHealing(PlayerEntity player, Pickup pickup)
        {
            if (!CanConsumePickup(player, pickup))
            {
                return false;
            }

            return tryApplyHealing!(player, 1f);
        }
    }

    public void AdvancePickups(
        IReadOnlyList<CivvieMoneyPickupParticipant> players,
        Func<CivvieMoneyPickupParticipant, float, bool>? tryConsume = null)
    {
        if (_pickups.Count == 0)
        {
            return;
        }

        _participantBuffer.Clear();
        _participantBuffer.AddRange(players);
        AdvancePickupsCore(
            _participantBuffer,
            tryConsume is null
                ? null
                : (player, pickup) =>
                {
                    if (!CanConsumePickup(player, pickup))
                    {
                        return false;
                    }

                    return tryConsume(player, 1f);
                });
    }

    private readonly List<PlayerEntity> _playerBuffer = new();

    private void AdvancePickupsCore<TPlayer>(
        IReadOnlyList<TPlayer> players,
        Func<TPlayer, Pickup, bool>? tryConsume)
    {
        for (var pickupIndex = _pickups.Count - 1; pickupIndex >= 0; pickupIndex -= 1)
        {
            var pickup = _pickups[pickupIndex];
            pickup.TicksRemaining -= 1;
            if (pickup.TicksRemaining <= 0)
            {
                _pickups.RemoveAt(pickupIndex);
                continue;
            }

            if (tryConsume is null)
            {
                continue;
            }

            for (var playerIndex = 0; playerIndex < players.Count; playerIndex += 1)
            {
                var player = players[playerIndex];
                if (!tryConsume(player, pickup))
                {
                    continue;
                }

                _pickups.RemoveAt(pickupIndex);
                break;
            }
        }
    }

    public void CombatTestAddPickup(
        int ownerPlayerId,
        PlayerTeam team,
        float x,
        float y,
        int ticksRemaining = CivvieMoneyTrailRules.PickupLifetimeTicks)
    {
        _pickups.Add(new Pickup(
            ownerPlayerId,
            team,
            x,
            y,
            Math.Max(1, ticksRemaining)));
    }

    public static CivvieMoneyPickupParticipant CreateParticipant(PlayerEntity player)
    {
        return new CivvieMoneyPickupParticipant(
            player.Id,
            player.Team,
            player.IsAlive,
            player.Health,
            player.MaxHealth,
            player.IntersectsMarker);
    }

    private bool HasNearbyPickup(int ownerPlayerId, float x, float y)
    {
        for (var index = 0; index < _pickups.Count; index += 1)
        {
            var pickup = _pickups[index];
            if (pickup.OwnerPlayerId != ownerPlayerId)
            {
                continue;
            }

            var deltaX = pickup.X - x;
            var deltaY = pickup.Y - y;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= CivvieMoneyTrailRules.PickupSpacingSquared)
            {
                return true;
            }
        }

        return false;
    }

    private void PrunePickupsForSpawn(int ownerPlayerId)
    {
        var ownerPickupCount = 0;
        for (var index = 0; index < _pickups.Count; index += 1)
        {
            if (_pickups[index].OwnerPlayerId == ownerPlayerId)
            {
                ownerPickupCount += 1;
            }
        }

        while (ownerPickupCount >= CivvieMoneyTrailRules.MaxPickupsPerOwner)
        {
            if (!RemoveOldestPickup(ownerPlayerId))
            {
                break;
            }

            ownerPickupCount -= 1;
        }

        while (_pickups.Count >= CivvieMoneyTrailRules.MaxPickupsTotal)
        {
            _pickups.RemoveAt(0);
        }
    }

    private bool RemoveOldestPickup(int ownerPlayerId)
    {
        for (var index = 0; index < _pickups.Count; index += 1)
        {
            if (_pickups[index].OwnerPlayerId == ownerPlayerId)
            {
                _pickups.RemoveAt(index);
                return true;
            }
        }

        return false;
    }

    private static bool CanConsumePickup(PlayerEntity player, Pickup pickup)
    {
        if (!player.IsAlive
            || player.Id == pickup.OwnerPlayerId
            || player.Team != pickup.Team
            || player.Health >= player.MaxHealth)
        {
            return false;
        }

        return player.IntersectsMarker(
            pickup.X,
            pickup.Y,
            CivvieMoneyTrailRules.PickupWidth,
            CivvieMoneyTrailRules.PickupHeight);
    }

    private static bool CanConsumePickup(CivvieMoneyPickupParticipant player, Pickup pickup)
    {
        if (!player.IsAlive
            || player.Id == pickup.OwnerPlayerId
            || player.Team != pickup.Team
            || player.Health >= player.MaxHealth)
        {
            return false;
        }

        return player.IntersectsMarker(
            pickup.X,
            pickup.Y,
            CivvieMoneyTrailRules.PickupWidth,
            CivvieMoneyTrailRules.PickupHeight);
    }
}
