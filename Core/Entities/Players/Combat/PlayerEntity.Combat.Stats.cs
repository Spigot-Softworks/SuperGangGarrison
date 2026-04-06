using System;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{

    public void AddHealPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        HealPoints += amount;
    }

    public void AddPoints(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Points += amount;
    }

    public void AddAssist()
    {
        Assists += 1;
    }

    public void AddKill()
    {
        Kills += 1;
    }

    public void AddDeath()
    {
        Deaths += 1;
    }

    public void RegisterCombatComboHit(int comboTimeoutTicks)
    {
        if (!IsAlive || comboTimeoutTicks <= 0)
        {
            return;
        }

        CurrentCombo += 1;
        HighestCombo = Math.Max(HighestCombo, CurrentCombo);
        ComboTicksRemaining = Math.Max(1, comboTimeoutTicks);
    }

    public void RegisterKillStreakKill(int multiKillWindowTicks)
    {
        if (!IsAlive)
        {
            return;
        }

        KillStreak += 1;
        HighestKillStreak = Math.Max(HighestKillStreak, KillStreak);
        CurrentMultiKillCount = MultiKillTicksRemaining > 0 && CurrentMultiKillCount > 0
            ? CurrentMultiKillCount + 1
            : 1;
        MultiKillTicksRemaining = Math.Max(0, multiKillWindowTicks);
    }

    public void AdvanceCombatPerformanceTracking()
    {
        if (ComboTicksRemaining > 0)
        {
            ComboTicksRemaining -= 1;
            if (ComboTicksRemaining <= 0)
            {
                CurrentCombo = 0;
                ComboTicksRemaining = 0;
            }
        }

        if (MultiKillTicksRemaining > 0)
        {
            MultiKillTicksRemaining -= 1;
            if (MultiKillTicksRemaining <= 0)
            {
                CurrentMultiKillCount = 0;
                MultiKillTicksRemaining = 0;
            }
        }
    }

    public void ResetCombatPerformanceTracking()
    {
        CurrentCombo = 0;
        ComboTicksRemaining = 0;
        KillStreak = 0;
        CurrentMultiKillCount = 0;
        MultiKillTicksRemaining = 0;
    }

    public void ResetRoundStats()
    {
        Kills = 0;
        Deaths = 0;
        Assists = 0;
        Caps = 0;
        Points = 0f;
        HealPoints = 0;
        HighestCombo = 0;
        HighestKillStreak = 0;
        ResetCombatPerformanceTracking();
    }

    public void SetBadgeMask(ulong badgeMask)
    {
        BadgeMask = BadgeCatalog.SanitizeBadgeMask(badgeMask);
    }

    public void RegisterDamageDealer(int playerId, int assistTicks)
    {
        if (playerId <= 0 || playerId == Id || assistTicks <= 0)
        {
            return;
        }

        if (LastDamageDealerPlayerId != playerId && LastDamageDealerPlayerId.HasValue)
        {
            SecondToLastDamageDealerPlayerId = LastDamageDealerPlayerId;
            SecondToLastDamageDealerAssistTicksRemaining = LastDamageDealerAssistTicksRemaining;
        }

        LastDamageDealerPlayerId = playerId;
        LastDamageDealerAssistTicksRemaining = assistTicks;
    }

    public void AdvanceAssistTracking()
    {
        if (LastDamageDealerAssistTicksRemaining > 0)
        {
            LastDamageDealerAssistTicksRemaining -= 1;
        }

        if (LastDamageDealerAssistTicksRemaining <= 0)
        {
            ClearRecentDamageDealers();
            return;
        }

        if (SecondToLastDamageDealerAssistTicksRemaining > 0)
        {
            SecondToLastDamageDealerAssistTicksRemaining -= 1;
        }

        if (SecondToLastDamageDealerAssistTicksRemaining <= 0)
        {
            SecondToLastDamageDealerPlayerId = null;
            SecondToLastDamageDealerAssistTicksRemaining = 0;
        }
    }

    public void ClearRecentDamageDealers()
    {
        LastDamageDealerPlayerId = null;
        LastDamageDealerAssistTicksRemaining = 0;
        SecondToLastDamageDealerPlayerId = null;
        SecondToLastDamageDealerAssistTicksRemaining = 0;
    }

}
