using System.Globalization;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public string GetMedicSummary()
    {
        var healTarget = LocalPlayer.MedicHealTargetId.HasValue ? LocalPlayer.MedicHealTargetId.Value.ToString(CultureInfo.InvariantCulture) : "none";
        return $"class={LocalPlayer.ClassName} uber={LocalPlayer.MedicUberCharge:F1}/2000 ready={LocalPlayer.IsMedicUberReady} ubering={LocalPlayer.IsMedicUbering} healing={LocalPlayer.IsMedicHealing} target={healTarget} needles={LocalPlayer.CurrentShells}/{LocalPlayer.MaxShells}";
    }

    public bool TryFillLocalMedicUber()
    {
        if (LocalPlayer.ClassId != PlayerClass.Medic)
        {
            return false;
        }

        LocalPlayer.FillMedicUberCharge();
        return true;
    }

    private void UpdateMedicHealing(PlayerEntity medic, float aimWorldX, float aimWorldY)
    {
        if (medic.ClassId != PlayerClass.Medic)
        {
            return;
        }

        var existingTarget = medic.MedicHealTargetId.HasValue
            ? FindPlayerById(medic.MedicHealTargetId.Value)
            : null;
        if (existingTarget is not null && CanMedicHealTarget(medic, existingTarget))
        {
            ApplyMedicHealing(medic, existingTarget);
            return;
        }

        medic.ClearMedicHealingTarget();
        var newTarget = AcquireMedicHealingTarget(medic, aimWorldX, aimWorldY);
        if (newTarget is null)
        {
            return;
        }

        ApplyMedicHealing(medic, newTarget);
    }

    private bool CanMedicHealTarget(PlayerEntity medic, PlayerEntity target)
    {
        if (!target.IsAlive || target.Team != medic.Team || target.Id == medic.Id)
        {
            return false;
        }

        if (DistanceBetween(medic.X, medic.Y, target.X, target.Y) > 300f)
        {
            return false;
        }

        return HasObstacleLineOfSight(medic.X, medic.Y, target.X, target.Y);
    }

    private void ApplyMedicHealing(PlayerEntity medic, PlayerEntity target)
    {
        target.ReduceBurnDuration((float)Config.FixedDeltaSeconds * LegacyMovementModel.SourceTicksPerSecond);

        var healAmount = target.Health < target.MaxHealth / 2f
            ? 1f
            : target.Health < target.MaxHealth
                ? 0.5f
                : 0f;
        if (healAmount > 0f)
        {
            var previousHealth = target.Health;
            target.ApplyContinuousHealing(healAmount);
            medic.AddHealPoints(Math.Max(0, target.Health - previousHealth));
        }

        if (!medic.IsMedicUbering)
        {
            var uberGain = 1f;
            if (target.Health < target.MaxHealth)
            {
                uberGain += 0.5f;
            }
            if (target.Health < target.MaxHealth / 2f)
            {
                uberGain += 1f;
            }

            medic.AddMedicUberCharge(uberGain);
        }

        medic.SetMedicHealingTarget(target);
    }

    private void AdvanceMedicUberEffects()
    {
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.ClassId != PlayerClass.Medic || !player.IsMedicUbering)
            {
                continue;
            }

            player.RefreshUber();
            if (!player.MedicHealTargetId.HasValue)
            {
                continue;
            }

            var healTarget = FindPlayerById(player.MedicHealTargetId.Value);
            if (healTarget is not null && healTarget.IsAlive)
            {
                healTarget.RefreshUber();
            }
        }
    }

    private PlayerEntity? AcquireMedicHealingTarget(PlayerEntity medic, float aimWorldX, float aimWorldY)
    {
        const float maxDistance = 300f;
        var aimDeltaX = aimWorldX - medic.X;
        var aimDeltaY = aimWorldY - medic.Y;
        if (aimDeltaX == 0f && aimDeltaY == 0f)
        {
            aimDeltaX = medic.FacingDirectionX;
        }

        var aimDistance = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
        if (aimDistance <= 0.0001f)
        {
            return null;
        }

        var directionX = aimDeltaX / aimDistance;
        var directionY = aimDeltaY / aimDistance;
        var aimEndX = medic.X + directionX * maxDistance;
        var aimEndY = medic.Y + directionY * maxDistance;
        PlayerEntity? bestTarget = null;
        var bestDistance = maxDistance;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.Id == medic.Id || player.Team != medic.Team)
            {
                continue;
            }

            var hitDistance = GetLineIntersectionDistanceToPlayer(
                medic.X,
                medic.Y,
                aimEndX,
                aimEndY,
                player,
                maxDistance);
            if (!hitDistance.HasValue || hitDistance.Value > bestDistance)
            {
                continue;
            }

            if (!HasObstacleLineOfSight(medic.X, medic.Y, player.X, player.Y))
            {
                continue;
            }

            bestTarget = player;
            bestDistance = hitDistance.Value;
        }

        return bestTarget;
    }
}
