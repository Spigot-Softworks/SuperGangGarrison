using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void TryRegisterCivvieMoneyTrail(PlayerEntity player)
    {
        _civvieMoneyTrailTracker.TryRegisterTrail((ulong)Frame, Config.TicksPerSecond, player);
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
