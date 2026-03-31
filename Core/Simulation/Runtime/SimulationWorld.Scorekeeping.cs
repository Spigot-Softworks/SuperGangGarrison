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

    private static void AwardKillPoints(PlayerEntity victim, PlayerEntity killer, string? weaponSpriteName)
    {
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

    private static void AwardAssistPoints(PlayerEntity? assistant, PlayerEntity victim, PlayerEntity killer)
    {
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

    private static void AwardObjectiveCapturePoints(PlayerEntity player)
    {
        player.AddPoints(ObjectiveCapturePointValue);
    }

    private static void AwardMedicUberActivationPoints(PlayerEntity player)
    {
        player.AddPoints(UberActivationPointValue);
    }

    private static void AwardSentryDestructionPoints(SentryEntity sentry, PlayerEntity? attacker)
    {
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
