namespace OpenGarrison.Core;

internal sealed partial class LastToDieRulesSystem
{
    // Only authoritative offline/server stages set this. Prediction receives
    // the buff's presentation but must never regenerate health independently.

    internal void ConfigureLastToDieStage(int stageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stageNumber);
        _host.LastToDieState.StageNumber = stageNumber;
        if (stageNumber == 0)
        {
            foreach (var player in _host.EnumerateSimulatedPlayers())
                player.SetLastToDieSurvivorBuff(false);
        }
    }

    internal bool TrySetLastToDieSurvivorBuff(byte slot, bool enabled)
    {
        if (!_host.NetworkPlayerRules.TryGetNetworkPlayer(slot, out var player)) return false;
        player.SetLastToDieSurvivorBuff(enabled);
        return true;
    }
}
