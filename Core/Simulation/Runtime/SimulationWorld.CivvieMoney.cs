namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void TryRegisterCivvieMoneyTrail(PlayerEntity player)
    {
        if (CombatRuntime.CivvieMoneyTrailTracker.TryRegisterTrail(
                (ulong)Frame,
                Config.TicksPerSecond,
                player,
                out var spawn))
        {
            // The server owns the trail spawn. Preserve the source horizontal speed
            // in the direction field so clients can reproduce its deterministic fall.
            WorldEffects.RegisterVisualEffect(
                "CivvieMoney",
                spawn.X,
                spawn.Y,
                spawn.HorizontalSpeed,
                normalizeDirection: false);
        }
    }

    private void AdvanceCivvieMoneyPickups()
    {
        CombatRuntime.CivvieMoneyTrailTracker.AdvancePickups(
            EnumerateSimulatedPlayers(),
            (player, amount) => DamageRules.ApplyHealingWithFeedback(player, amount) > 0);
    }

    public IReadOnlyList<CivvieMoneyTrailSpawn> DrainPendingCivvieMoneyTrailSpawns()
    {
        return CombatRuntime.CivvieMoneyTrailTracker.DrainPendingSpawns();
    }
}
