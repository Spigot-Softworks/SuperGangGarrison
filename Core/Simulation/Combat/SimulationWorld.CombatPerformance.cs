using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private readonly record struct KillFeedAnnouncementTemplate(
        string Prefix,
        string Highlight,
        string Suffix);

    private int GetComboTimeoutTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * ExperimentalGameplaySettings.ComboTimeoutSeconds));
    }

    private int GetMultiKillWindowTicks()
    {
        return Math.Max(1, (int)MathF.Round(Config.TicksPerSecond * ExperimentalGameplaySettings.MultiKillWindowSeconds));
    }

    private bool ShouldTrackCombatPerformanceForPlayer(PlayerEntity? player)
    {
        return player is not null
            && ReferenceEquals(player, LocalPlayer);
    }

    private void TryRegisterCombatComboHit(PlayerEntity? attacker, PlayerEntity target, int appliedDamage)
    {
        if (appliedDamage <= 0
            || attacker is null
            || !ExperimentalGameplaySettings.EnableComboTracking
            || !ShouldTrackCombatPerformanceForPlayer(attacker)
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team)
        {
            return;
        }

        attacker.RegisterCombatComboHit(GetComboTimeoutTicks());
    }

    private void TryRegisterKillStreakKill(PlayerEntity? killer, PlayerEntity victim)
    {
        if (killer is null
            || !ExperimentalGameplaySettings.EnableKillStreakTracking
            || !ShouldTrackCombatPerformanceForPlayer(killer)
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team)
        {
            return;
        }

        var previousKillStreak = killer.KillStreak;
        var previousMultiKillCount = killer.CurrentMultiKillCount;
        killer.RegisterKillStreakKill(GetMultiKillWindowTicks());
        TryRecordCombatPerformanceAnnouncements(killer, previousKillStreak, previousMultiKillCount);
    }

    private void TryRecordCombatPerformanceAnnouncements(PlayerEntity killer, int previousKillStreak, int previousMultiKillCount)
    {
        if (killer.CurrentMultiKillCount > previousMultiKillCount)
        {
            TryRecordMultiKillAnnouncement(killer, killer.CurrentMultiKillCount);
        }

        if (killer.KillStreak > previousKillStreak)
        {
            TryRecordKillSpreeAnnouncement(killer, killer.KillStreak);
        }
    }

    private void TryRecordMultiKillAnnouncement(PlayerEntity killer, int multiKillCount)
    {
        var template = multiKillCount switch
        {
            2 => new KillFeedAnnouncementTemplate(" scored a ", "Double kill", "!"),
            3 => new KillFeedAnnouncementTemplate(" scored a ", "Triple kill", "!"),
            4 => new KillFeedAnnouncementTemplate(" scored an ", "Ultra kill", "!"),
            >= 5 => new KillFeedAnnouncementTemplate(" is on a ", "RAMPAGE", "!!!"),
            _ => default,
        };

        if (string.IsNullOrWhiteSpace(template.Highlight))
        {
            return;
        }

        RecordKillFeedAnnouncement(killer, template.Prefix, template.Highlight, template.Suffix);
    }

    private void TryRecordKillSpreeAnnouncement(PlayerEntity killer, int killStreak)
    {
        var template = killStreak switch
        {
            3 => new KillFeedAnnouncementTemplate(" is on a ", "Killing Spree", "!"),
            4 => new KillFeedAnnouncementTemplate(" is ", "Dominating", "!"),
            5 => new KillFeedAnnouncementTemplate(" has reached ", "Mega kill", "!"),
            6 => new KillFeedAnnouncementTemplate(" is ", "Unstoppable", "!"),
            7 => new KillFeedAnnouncementTemplate(" is ", "Wicked sick", "!"),
            8 => new KillFeedAnnouncementTemplate(" has reached ", "Monster kill", "!"),
            9 => new KillFeedAnnouncementTemplate(" is ", "Godlike", "!"),
            >= 10 => new KillFeedAnnouncementTemplate(" has gone ", "Beyond Godlike", "!"),
            _ => default,
        };

        if (string.IsNullOrWhiteSpace(template.Highlight))
        {
            return;
        }

        RecordKillFeedAnnouncement(killer, template.Prefix, template.Highlight, template.Suffix);
    }
}
