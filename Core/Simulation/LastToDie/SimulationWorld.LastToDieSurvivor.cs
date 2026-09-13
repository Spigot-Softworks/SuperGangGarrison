namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    // Only authoritative offline/server stages set this. Prediction receives
    // the buff's presentation but must never regenerate health independently.
    private int _lastToDieStageNumber;

    public void ConfigureLastToDieStage(int stageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stageNumber);
        _lastToDieStageNumber = stageNumber;
        if (stageNumber == 0)
        {
            foreach (var player in EnumerateSimulatedPlayers())
                player.SetLastToDieSurvivorBuff(false);
        }
    }

    public bool TrySetLastToDieSurvivorBuff(byte slot, bool enabled)
    {
        if (!TryGetNetworkPlayer(slot, out var player)) return false;
        player.SetLastToDieSurvivorBuff(enabled);
        return true;
    }
}
