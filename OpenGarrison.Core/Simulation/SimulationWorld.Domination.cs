namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void ClearDominationsForPlayer(PlayerEntity player)
    {
        foreach (var otherPlayer in EnumerateSimulatedPlayers())
        {
            otherPlayer.ClearDominationKillCount(player.Id);
        }

        player.ClearDominations();
    }

    private void ClearAllDominations()
    {
        foreach (var player in EnumerateSimulatedPlayers())
        {
            player.ClearDominations();
        }
    }

    private void UpdateDominationStateForKill(PlayerEntity victim, PlayerEntity killer)
    {
        if (ReferenceEquals(victim, killer))
        {
            return;
        }

        var specialType = KillFeedSpecialType.None;
        var messageText = string.Empty;
        if (victim.GetDominationKillCount(killer.Id) > 3)
        {
            specialType = KillFeedSpecialType.Revenge;
            messageText = "got REVENGE on ";
        }
        else if (killer.GetDominationKillCount(victim.Id) == 3)
        {
            specialType = KillFeedSpecialType.Domination;
            messageText = "is DOMINATING ";
        }

        killer.IncrementDominationKillCount(victim.Id);
        victim.ClearDominationKillCount(killer.Id);

        if (specialType != KillFeedSpecialType.None)
        {
            RecordKillFeedEntry(victim, killer, "DominationKL", messageText, specialType);
        }
    }
}
