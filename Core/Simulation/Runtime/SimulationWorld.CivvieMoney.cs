using System.Collections.Generic;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int CivvieMoneyPickupLifetimeTicks = 90;
    private const int CivvieMoneyMaxPickupsPerOwner = 10;
    private const int CivvieMoneyMaxPickupsTotal = 80;
    private const float CivvieMoneyPickupWidth = 34f;
    private const float CivvieMoneyPickupHeight = 32f;
    private const float CivvieMoneyPickupSpacingSquared = 9f * 9f;
    private const float CivvieMoneyTrailMoveSpeedThreshold = 0.04f * LegacyMovementModel.SourceTicksPerSecond;
    private const float CivvieMoneyTrailSpawnChance = 0.35f;

    private sealed class CivvieMoneyPickup
    {
        public CivvieMoneyPickup(int ownerPlayerId, PlayerTeam team, float x, float y, int ticksRemaining)
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

    private void TryRegisterCivvieMoneyTrail(PlayerEntity player)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Quote
            || !player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.CivvieTaunt))
        {
            return;
        }

        if (MathF.Abs(player.HorizontalSpeed) < CivvieMoneyTrailMoveSpeedThreshold
            || !ShouldEmitSourceTickChance(CivvieMoneyTrailSpawnChance))
        {
            return;
        }

        var trailDirection = player.HorizontalSpeed == 0f
            ? player.FacingDirectionX
            : -MathF.Sign(player.HorizontalSpeed);
        var pickupX = player.X + (trailDirection * 8f);
        var pickupY = player.Y - 11f + (_random.NextSingle() * 9f);
        if (HasNearbyCivvieMoneyPickup(player.Id, pickupX, pickupY))
        {
            return;
        }

        PruneCivvieMoneyPickupsForSpawn(player.Id);
        _civvieMoneyPickups.Add(new CivvieMoneyPickup(
            player.Id,
            player.Team,
            pickupX,
            pickupY,
            CivvieMoneyPickupLifetimeTicks));
        RegisterVisualEffect("CivvieMoney", pickupX, pickupY, player.HorizontalSpeed, normalizeDirection: false);
    }

    private bool HasNearbyCivvieMoneyPickup(int ownerPlayerId, float x, float y)
    {
        for (var index = 0; index < _civvieMoneyPickups.Count; index += 1)
        {
            var pickup = _civvieMoneyPickups[index];
            if (pickup.OwnerPlayerId != ownerPlayerId)
            {
                continue;
            }

            var deltaX = pickup.X - x;
            var deltaY = pickup.Y - y;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= CivvieMoneyPickupSpacingSquared)
            {
                return true;
            }
        }

        return false;
    }

    private void PruneCivvieMoneyPickupsForSpawn(int ownerPlayerId)
    {
        var ownerPickupCount = 0;
        for (var index = 0; index < _civvieMoneyPickups.Count; index += 1)
        {
            if (_civvieMoneyPickups[index].OwnerPlayerId == ownerPlayerId)
            {
                ownerPickupCount += 1;
            }
        }

        while (ownerPickupCount >= CivvieMoneyMaxPickupsPerOwner)
        {
            var removed = RemoveOldestCivvieMoneyPickup(ownerPlayerId);
            if (!removed)
            {
                break;
            }

            ownerPickupCount -= 1;
        }

        while (_civvieMoneyPickups.Count >= CivvieMoneyMaxPickupsTotal)
        {
            _civvieMoneyPickups.RemoveAt(0);
        }
    }

    private bool RemoveOldestCivvieMoneyPickup(int ownerPlayerId)
    {
        for (var index = 0; index < _civvieMoneyPickups.Count; index += 1)
        {
            if (_civvieMoneyPickups[index].OwnerPlayerId == ownerPlayerId)
            {
                _civvieMoneyPickups.RemoveAt(index);
                return true;
            }
        }

        return false;
    }

    private void AdvanceCivvieMoneyPickups()
    {
        if (_civvieMoneyPickups.Count == 0)
        {
            return;
        }

        _civvieMoneyPickupPlayerBuffer.Clear();
        foreach (var player in EnumerateSimulatedPlayers())
        {
            _civvieMoneyPickupPlayerBuffer.Add(player);
        }

        for (var pickupIndex = _civvieMoneyPickups.Count - 1; pickupIndex >= 0; pickupIndex -= 1)
        {
            var pickup = _civvieMoneyPickups[pickupIndex];
            pickup.TicksRemaining -= 1;
            if (pickup.TicksRemaining <= 0)
            {
                _civvieMoneyPickups.RemoveAt(pickupIndex);
                continue;
            }

            foreach (var player in _civvieMoneyPickupPlayerBuffer)
            {
                if (!CanConsumeCivvieMoneyPickup(player, pickup))
                {
                    continue;
                }

                if (ApplyHealingWithFeedback(player, 1f) <= 0)
                {
                    continue;
                }

                _civvieMoneyPickups.RemoveAt(pickupIndex);
                break;
            }
        }
    }

    private static bool CanConsumeCivvieMoneyPickup(PlayerEntity player, CivvieMoneyPickup pickup)
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
            CivvieMoneyPickupWidth,
            CivvieMoneyPickupHeight);
    }

    internal void CombatTestAddCivvieMoneyPickup(
        int ownerPlayerId,
        PlayerTeam team,
        float x,
        float y,
        int ticksRemaining = CivvieMoneyPickupLifetimeTicks)
    {
        _civvieMoneyPickups.Add(new CivvieMoneyPickup(
            ownerPlayerId,
            team,
            x,
            y,
            Math.Max(1, ticksRemaining)));
    }

    internal int CombatTestCivvieMoneyPickupCount => _civvieMoneyPickups.Count;
}
