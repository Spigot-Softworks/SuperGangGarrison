using System;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float KillPointValue = 1f;
    private const float AssistPointValue = 0.5f;
    private const float StabKillBonusPointValue = 1f;
    private const float IntelDefensePointValue = 1f;
    private const float UberReadyKillBonusPointValue = 1f;
    private const float ObjectiveCapturePointValue = 2f;
    private const float BuildingDestructionPointValue = 1f;
    private const float UberActivationPointValue = 1f;

    private bool ShouldAwardRoundPoints()
    {
        return !MatchState.IsEnded;
    }

    private void AwardKillPoints(PlayerEntity victim, PlayerEntity killer, string? weaponSpriteName)
    {
        if (!ShouldAwardRoundPoints())
        {
            return;
        }

        if (ReferenceEquals(victim, killer) || killer.Team == victim.Team)
        {
            return;
        }

        killer.AddPoints(KillPointValue);

        if (string.Equals(weaponSpriteName, "KnifeKL", StringComparison.Ordinal))
        {
            killer.AddPoints(StabKillBonusPointValue);
        }

        if (victim.IsCarryingIntel)
        {
            killer.AddPoints(IntelDefensePointValue);
        }

        if (victim.ClassId == PlayerClass.Medic && victim.IsMedicUberReady)
        {
            killer.AddPoints(UberReadyKillBonusPointValue);
        }
    }

    private void AwardAssistPoints(PlayerEntity? assistant, PlayerEntity victim, PlayerEntity killer)
    {
        if (!ShouldAwardRoundPoints())
        {
            return;
        }

        if (assistant is null
            || ReferenceEquals(assistant, victim)
            || ReferenceEquals(assistant, killer)
            || assistant.Team != killer.Team
            || assistant.Team == victim.Team)
        {
            return;
        }

        assistant.AddAssist();
        assistant.AddPoints(AssistPointValue);
    }

    private void AwardObjectiveCapturePoints(PlayerEntity player)
    {
        if (!ShouldAwardRoundPoints())
        {
            return;
        }

        player.AddPoints(ObjectiveCapturePointValue);
    }

    private void AwardMedicUberActivationPoints(PlayerEntity player)
    {
        if (!ShouldAwardRoundPoints())
        {
            return;
        }

        player.AddPoints(UberActivationPointValue);
    }

    private void AwardSentryDestructionPoints(SentryEntity sentry, PlayerEntity? attacker)
    {
        if (!ShouldAwardRoundPoints())
        {
            return;
        }

        if (attacker is null || attacker.Id == sentry.OwnerPlayerId)
        {
            return;
        }

        attacker.AddPoints(BuildingDestructionPointValue);
    }

    private PlayerEntity? FindHealingMedicPlayer(int targetPlayerId)
    {
        var medicId = FindHealingMedicPlayerId(targetPlayerId);
        return medicId > 0 ? FindPlayerById(medicId) : null;
    }
}
