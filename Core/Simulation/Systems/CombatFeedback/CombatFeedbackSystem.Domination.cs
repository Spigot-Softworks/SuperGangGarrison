namespace OpenGarrison.Core;

internal sealed partial class CombatFeedbackSystem
{
    internal void ClearDominationsForPlayer(PlayerEntity player)
    {
        foreach (var otherPlayer in _host.EnumerateSimulatedPlayers())
        {
            otherPlayer.ClearDominationKillCount(player.Id);
        }

        player.ClearDominations();
    }

    internal void ClearAllDominations()
    {
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            player.ClearDominations();
        }
    }

    internal void UpdateDominationStateForKill(PlayerEntity victim, PlayerEntity killer, PlayerEntity? assistingPlayer)
    {
        if (ReferenceEquals(victim, killer))
        {
            return;
        }

        UpdateDominationStateForParticipant(victim, killer);
        if (assistingPlayer is not null
            && !ReferenceEquals(assistingPlayer, victim)
            && !ReferenceEquals(assistingPlayer, killer))
        {
            UpdateDominationStateForParticipant(victim, assistingPlayer);
        }
    }

    private void UpdateDominationStateForParticipant(PlayerEntity victim, PlayerEntity participant)
    {
        var specialType = KillFeedSpecialType.None;
        var messageText = string.Empty;
        if (victim.GetDominationKillCount(participant.Id) > 3)
        {
            specialType = KillFeedSpecialType.Revenge;
            messageText = "got REVENGE on ";
        }
        else if (participant.GetDominationKillCount(victim.Id) == 3)
        {
            specialType = KillFeedSpecialType.Domination;
            messageText = "is DOMINATING ";
        }

        participant.IncrementDominationKillCount(victim.Id);
        victim.ClearDominationKillCount(participant.Id);

        if (specialType != KillFeedSpecialType.None)
        {
            _host.KillFeedRules.RecordKillFeedEntry(victim, participant, "DominationKL", messageText, specialType: specialType);
        }
    }
}
