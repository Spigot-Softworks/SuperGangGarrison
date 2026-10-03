using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public bool TryApplyLastToDieStatusEffect(
        int targetPlayerId,
        int? sourcePlayerId,
        LastToDieStatusEffectSpec requestedSpec,
        int? assistingMedicPlayerId = null)
        => LastToDieRules.TryApplyLastToDieStatusEffect(targetPlayerId, sourcePlayerId, requestedSpec, assistingMedicPlayerId);

    public IReadOnlyList<LastToDieActiveStatusEffectSnapshot> GetLastToDieStatusEffects(int targetPlayerId)
        => LastToDieRules.GetLastToDieStatusEffects(targetPlayerId);


    // Perks, afterlife, javelin, sniper explosive tip, survivor/stage objectives.
    public bool CanPlayerCaptureControlPointsWhileCloaked(PlayerEntity player)
        => LastToDieRules.CanPlayerCaptureControlPointsWhileCloaked(player);
    public bool CanPlayerCaptureControlPointsWhileUbered(PlayerEntity player)
        => LastToDieRules.CanPlayerCaptureControlPointsWhileUbered(player);
    public bool CanPlayerContributeToControlPoint(PlayerEntity player)
        => LastToDieRules.CanPlayerContributeToControlPoint(player);
    public bool ClearLastToDiePlayerPredictionProfile(byte slot)
        => LastToDieRules.ClearLastToDiePlayerPredictionProfile(slot);
    public void ConfigureLastToDieCombatSeed(ulong seed)
        => LastToDieRules.ConfigureLastToDieCombatSeed(seed);
    public void ResetLastToDieClientSession()
        => LastToDieRules.ResetLastToDieClientSession();
    public bool TryApplyLastToDiePlayerPredictionProfile(byte slot, IEnumerable<string> ownedPerkIds, int runKills = 0, bool secondChanceConsumed = false)
        => LastToDieRules.TryApplyLastToDiePlayerPredictionProfile(slot, ownedPerkIds, runKills, secondChanceConsumed);
    public bool TryConfigureLastToDiePlayerBuild(byte slot, IEnumerable<LastToDiePerkId> perks, int? baseMaximumHealthOverride = null, bool refillHealth = false, bool resetDynamicState = false, int runKills = 0, bool secondChanceConsumed = false, bool runKillProgressionOwner = true)
        => LastToDieRules.TryConfigureLastToDiePlayerBuild(slot, perks, baseMaximumHealthOverride, refillHealth, resetDynamicState, runKills, secondChanceConsumed, runKillProgressionOwner);
    public bool TryGetLastToDiePlayerModifiers(byte slot, out LastToDieDerivedModifiers modifiers)
        => LastToDieRules.TryGetLastToDiePlayerModifiers(slot, out modifiers);
    public bool TryGetLastToDieSecondChanceConsumed(byte slot, out bool consumed)
        => LastToDieRules.TryGetLastToDieSecondChanceConsumed(slot, out consumed);
    public bool TryGetLastToDieSniperConquistadorStacks(byte slot, out int stacks)
        => LastToDieRules.TryGetLastToDieSniperConquistadorStacks(slot, out stacks);
    public bool TryRestoreLastToDieSniperConquistadorStacks(byte slot, int stacks)
        => LastToDieRules.TryRestoreLastToDieSniperConquistadorStacks(slot, stacks);
    public bool TrySetLastToDiePlayerRunKills(byte slot, int runKills)
        => LastToDieRules.TrySetLastToDiePlayerRunKills(slot, runKills);
    public bool ConsumeLastToDieSpyAfterlifeDisconnectFailure(byte slot)
        => LastToDieRules.ConsumeLastToDieSpyAfterlifeDisconnectFailure(slot);
    public bool IsLastToDieSpyAfterlifeWindowActive(byte slot)
        => LastToDieRules.IsLastToDieSpyAfterlifeWindowActive(slot);
    public void ConfigureLastToDieStage(int stageNumber)
        => LastToDieRules.ConfigureLastToDieStage(stageNumber);
    public bool TrySetLastToDieSurvivorBuff(byte slot, bool enabled)
        => LastToDieRules.TrySetLastToDieSurvivorBuff(slot, enabled);
    public int CountOwnedLastToDieSniperExplosiveArrows(PlayerEntity owner)
        => LastToDieRules.CountOwnedLastToDieSniperExplosiveArrows(owner);
    public bool CanCompleteLastToDieStageOnTimeout => LastToDieRules.CanCompleteLastToDieStageOnTimeout;
}
