using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void TryRegisterCivvieMoneyTrail(PlayerEntity player)
    {
        if (_civvieMoneyTrailTracker.TryRegisterTrail(
                (ulong)Frame,
                Config.TicksPerSecond,
                player,
                out var spawn))
        {
            // The server owns the trail spawn. Preserve the source horizontal speed
            // in the direction field so clients can reproduce its deterministic fall.
            RegisterVisualEffect(
                "CivvieMoney",
                spawn.X,
                spawn.Y,
                spawn.HorizontalSpeed,
                normalizeDirection: false);
        }
    }

    private void AdvanceCivvieMoneyPickups()
    {
        _civvieMoneyTrailTracker.AdvancePickups(
            EnumerateSimulatedPlayers(),
            (player, amount) => ApplyHealingWithFeedback(player, amount) > 0);
    }

    internal void CombatTestAddCivvieMoneyPickup(
        int ownerPlayerId,
        PlayerTeam team,
        float x,
        float y,
        int ticksRemaining = CivvieMoneyTrailRules.PickupLifetimeTicks)
    {
        _civvieMoneyTrailTracker.CombatTestAddPickup(ownerPlayerId, team, x, y, ticksRemaining);
    }

    internal int CombatTestCivvieMoneyPickupCount => _civvieMoneyTrailTracker.PickupCount;

    public IReadOnlyList<CivvieMoneyTrailSpawn> DrainPendingCivvieMoneyTrailSpawns()
    {
        return _civvieMoneyTrailTracker.DrainPendingSpawns();
    }
}
