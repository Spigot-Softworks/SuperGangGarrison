using OpenGarrison.GameplayModding;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float AcquiredMedigunHealsplosionRadius = 155f;
    private const float AcquiredMedigunHealsplosionMaxDamage = 110f;
    private const float AcquiredMedigunHealsplosionSelfHealing = 120f;
    private const float AcquiredMedigunHealsplosionMinimumDamageFactor = 0.12f;
    private const float MedicUberChargeGainPerTickHealthyTarget = 1.75f;
    private const float MedicUberChargeGainPerTickDamagedTarget = 2.5f;
    private const float MedicHealBeamRange = 300f;
    private const float MedicKritzBeamDefaultRange = MedicHealBeamRange * 0.5f;
    private const float MedicKritzBeamDefaultDamagePerSecond = 1f;
    private const float MedicKritzBeamDefaultChargePerTick = MedicUberChargeGainPerTickHealthyTarget;

    public string GetMedicSummary()
    {
        var healTarget = LocalPlayer.MedicHealTargetId.HasValue ? LocalPlayer.MedicHealTargetId.Value.ToString(CultureInfo.InvariantCulture) : "none";
        return $"class={LocalPlayer.ClassName} uber={LocalPlayer.MedicUberCharge:F1}/2000 ready={LocalPlayer.IsMedicUberReady} ubering={LocalPlayer.IsMedicUbering} healing={LocalPlayer.IsMedicHealing} target={healTarget} needles={LocalPlayer.CurrentShells}/{LocalPlayer.MaxShells}";
    }

    public bool TryFillLocalMedicUber()
    {
        if (!LocalPlayer.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
        {
            return false;
        }

        LocalPlayer.FillMedicUberCharge();
        return true;
    }

    private void UpdateMedicHealing(PlayerEntity medic, float aimWorldX, float aimWorldY)
    {
        if (!medic.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Medigun))
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

    private void UpdateExperimentalEngineerEssenceExtractor(PlayerEntity engineer, float aimWorldX, float aimWorldY)
    {
        var existingTarget = engineer.MedicHealTargetId.HasValue
            ? FindPlayerById(engineer.MedicHealTargetId.Value)
            : null;
        if (existingTarget is not null && CanExperimentalEngineerEssenceExtractorTarget(engineer, existingTarget))
        {
            ApplyExperimentalEngineerEssenceExtractor(engineer, existingTarget);
            return;
        }

        FlushExperimentalEngineerEssenceExtractorHealing(engineer);
        engineer.ClearMedicHealingTarget();
        var newTarget = AcquireExperimentalEngineerEssenceExtractorTarget(engineer, aimWorldX, aimWorldY);
        if (newTarget is null)
        {
            return;
        }

        ApplyExperimentalEngineerEssenceExtractor(engineer, newTarget);
    }

    private void UpdateExperimentalEngineerFreezeRay(PlayerEntity engineer, float aimWorldX, float aimWorldY)
    {
        var primaryTarget = engineer.MedicHealTargetId.HasValue
            ? FindPlayerById(engineer.MedicHealTargetId.Value)
            : null;
        if (primaryTarget is not null && !CanExperimentalEngineerFreezeRayTarget(engineer, primaryTarget))
        {
            primaryTarget = null;
        }

        if (primaryTarget is null)
        {
            engineer.ClearMedicHealingTarget();
            primaryTarget = AcquireExperimentalEngineerFreezeRayPrimaryTarget(engineer, aimWorldX, aimWorldY);
            if (primaryTarget is null)
            {
                return;
            }
        }

        var chainedTargets = AcquireExperimentalEngineerFreezeRayAdditionalTargets(engineer, primaryTarget);
        ApplyExperimentalEngineerFreezeRay(engineer, primaryTarget);
        for (var index = 0; index < chainedTargets.Length; index += 1)
        {
            ApplyExperimentalEngineerFreezeRay(engineer, chainedTargets[index]);
        }

        engineer.SetMedicHealingTarget(primaryTarget.IsAlive ? primaryTarget : null);
        engineer.SetExperimentalAdditionalMedicBeamTargets(
            chainedTargets.Length > 0 && chainedTargets[0].IsAlive ? chainedTargets[0] : null,
            chainedTargets.Length > 1 && chainedTargets[1].IsAlive ? chainedTargets[1] : null);
    }

    private bool UpdateMedicKritzBeam(
        PlayerEntity medic,
        float aimWorldX,
        float aimWorldY,
        float maxRange = MedicKritzBeamDefaultRange,
        float damagePerSecond = MedicKritzBeamDefaultDamagePerSecond,
        float chargePerTick = MedicKritzBeamDefaultChargePerTick)
    {
        if (medic.ClassId != PlayerClass.Medic || !medic.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
        {
            return false;
        }

        maxRange = Math.Clamp(maxRange, 1f, MedicHealBeamRange);
        var existingTarget = medic.MedicHealTargetId.HasValue
            ? FindPlayerById(medic.MedicHealTargetId.Value)
            : null;
        if (existingTarget is not null && CanMedicKritzBeamTarget(medic, existingTarget, MedicHealBeamRange))
        {
            ApplyMedicKritzBeam(medic, existingTarget, damagePerSecond, chargePerTick);
            return true;
        }

        medic.ClearMedicHealingTarget();
        var newTarget = AcquireMedicKritzBeamTarget(medic, aimWorldX, aimWorldY, maxRange);
        if (newTarget is null)
        {
            return false;
        }

        ApplyMedicKritzBeam(medic, newTarget, damagePerSecond, chargePerTick);
        return true;
    }

    private bool CanMedicHealTarget(PlayerEntity medic, PlayerEntity target)
    {
        if (!target.IsAlive || target.Team != medic.Team || target.Id == medic.Id)
        {
            return false;
        }

        if (DistanceBetween(medic.X, medic.Y, target.X, target.Y) > MedicHealBeamRange)
        {
            return false;
        }

        return HasMedicHealingLineOfSight(medic, target);
    }

    private bool CanExperimentalEngineerEssenceExtractorTarget(PlayerEntity engineer, PlayerEntity target)
    {
        if (!target.IsAlive || target.Team == engineer.Team || target.Id == engineer.Id)
        {
            return false;
        }

        if (DistanceBetween(engineer.X, engineer.Y, target.X, target.Y) > MedicHealBeamRange)
        {
            return false;
        }

        return HasMedicHealingLineOfSight(engineer, target);
    }

    private bool CanMedicKritzBeamTarget(PlayerEntity medic, PlayerEntity target, float maxRange)
    {
        if (!target.IsAlive || target.Team == medic.Team || target.Id == medic.Id)
        {
            return false;
        }

        if (DistanceBetween(medic.X, medic.Y, target.X, target.Y) > maxRange)
        {
            return false;
        }

        return HasMedicHealingLineOfSight(medic, target);
    }

    private bool CanExperimentalEngineerFreezeRayTarget(PlayerEntity engineer, PlayerEntity target)
    {
        return CanExperimentalEngineerEssenceExtractorTarget(engineer, target);
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
            var uberGain = target.Health < target.MaxHealth || ControlPointSetupActive
                ? MedicUberChargeGainPerTickDamagedTarget
                : MedicUberChargeGainPerTickHealthyTarget;
            medic.AddMedicUberCharge(uberGain);
        }

        medic.SetMedicHealingTarget(target);
    }

    private void ApplyMedicHealNeedleTeammateHit(PlayerEntity medic, PlayerEntity target, MedicHealNeedleProjectileEntity needle)
    {
        target.ReduceBurnDuration((float)Config.FixedDeltaSeconds * LegacyMovementModel.SourceTicksPerSecond);

        var healthBefore = target.Health;
        if (needle.HealPerHit > 0)
        {
            target.ApplyContinuousHealing(needle.HealPerHit);
        }

        var healedAmount = Math.Max(0, target.Health - healthBefore);
        if (healedAmount > 0)
        {
            medic.AddHealPoints(healedAmount);
            ApplyHealingWithFeedback(target, healedAmount, "HealSnd", medic.X, medic.Y);
        }

        if (!medic.IsMedicUbering)
        {
            var uberGain = healedAmount > 0f
                ? healedAmount * MedicHealNeedleProjectileEntity.DamagedTargetUberChargePerHealedHealth
                : target.Health < target.MaxHealth || ControlPointSetupActive
                    ? MedicHealNeedleProjectileEntity.DamagedTargetUberChargePerHealedHealth
                    : MedicHealNeedleProjectileEntity.HealthyTargetUberChargePerHit;
            medic.AddMedicUberCharge(uberGain);
        }
    }

    private void ApplyExperimentalEngineerEssenceExtractor(PlayerEntity engineer, PlayerEntity target)
    {
        var healthBefore = target.Health;
        var damagePerTick = GetContinuousPerTickRate(
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorDrainPerSecond);
        if (ApplyPlayerContinuousDamage(target, damagePerTick, engineer, PlayerEntity.SpyDamageRevealAlpha))
        {
            KillPlayer(target, killer: engineer, weaponSpriteName: "NeedleKL");
        }

        var appliedDamage = Math.Max(0, healthBefore - target.Health);
        if (appliedDamage > 0)
        {
            var appliedHealing = engineer.ApplyContinuousHealingAndGetAmount(appliedDamage);
            engineer.AddHealPoints(appliedHealing);
            var chunkHealing = engineer.AccumulateExperimentalEngineerEssenceExtractorHealing(
                appliedHealing,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorHealingChunkSize);
            if (chunkHealing > 0)
            {
                RegisterHealingFeedbackOnly(engineer, chunkHealing);
            }
        }

        target.RefreshExperimentalDamageTakenDebuff(
            GetExperimentalEngineerEssenceExtractorDebuffTicks(),
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorVulnerabilityMultiplier);
        target.RefreshExperimentalEngineerEssenceExtractorSlow(
            GetExperimentalEngineerEssenceExtractorSlowTicks(),
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorSlowMovementMultiplier);
        engineer.SetMedicHealingTarget(target);
        engineer.ClearExperimentalAdditionalMedicBeamTargets();
    }

    private void ApplyExperimentalEngineerFreezeRay(PlayerEntity engineer, PlayerEntity target)
    {
        var damagePerTick = GetContinuousPerTickRate(
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayDamagePerSecond);
        if (ApplyPlayerContinuousDamage(target, damagePerTick, engineer, PlayerEntity.SpyDamageRevealAlpha))
        {
            KillPlayer(target, killer: engineer, weaponSpriteName: "NeedleKL", gibbed: target.IsExperimentalCryoFrozen);
        }
        target.AccumulateExperimentalCryoExposure(
            engineer.Id,
            freezeThresholdTicks: GetExperimentalEngineerFreezeRayFreezeThresholdTicks(),
            exposureWindowTicks: GetExperimentalEngineerFreezeRayExposureWindowTicks(),
            freezeDurationTicks: GetExperimentalEngineerCryoFreezeDurationTicks(),
            slowMovementMultiplier: global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRaySlowMovementMultiplier,
            slowTicks: GetExperimentalEngineerFreezeRaySlowTicks());
        target.RefreshExperimentalFreezeRayCombatDebuff(
            GetExperimentalEngineerFreezeRaySlowTicks(),
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayAttackCycleMultiplier,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayOutgoingDamageMultiplier);
    }

    private void ApplyMedicKritzBeam(PlayerEntity medic, PlayerEntity target, float damagePerSecond, float chargePerTick)
    {
        var healthBefore = target.Health;
        var damagePerTick = damagePerSecond <= 0f
            ? 0f
            : GetContinuousPerTickRate(damagePerSecond);
        if (ApplyPlayerContinuousDamage(target, damagePerTick, medic, PlayerEntity.SpyDamageRevealAlpha))
        {
            KillPlayer(target, killer: medic, weaponSpriteName: "NeedleKL");
        }

        if (healthBefore > target.Health)
        {
            RegisterBloodEffect(target.X, target.Y, PointDirectionDegrees(medic.X, medic.Y, target.X, target.Y) - 180f, 4);
        }

        if (!medic.IsMedicUbering)
        {
            medic.AddMedicUberCharge(Math.Max(0f, chargePerTick));
        }

        medic.SetMedicHealingTarget(target);
        medic.ClearExperimentalAdditionalMedicBeamTargets();
    }

    private void AdvanceMedicUberEffects()
    {
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || !player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber) || !player.IsMedicUbering)
            {
                continue;
            }

            var isKritz = player.HasEquippedBehavior(BuiltInGameplayBehaviorIds.MedigunCrit);

            if (isKritz)
            {
                player.RefreshKritzCritBoost();
            }
            else
            {
                player.RefreshUber();
            }

            if (!player.MedicHealTargetId.HasValue)
            {
                continue;
            }

            var healTarget = FindPlayerById(player.MedicHealTargetId.Value);
            if (healTarget is not null && healTarget.IsAlive)
            {
                if (isKritz)
                {
                    healTarget.RefreshKritzCritBoost();
                }
                else if (!healTarget.IsCarryingIntel)
                {
                    healTarget.RefreshUber();
                }
            }
        }
    }

    private void EmitPendingMedicUberReadyPresentation()
    {
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.TryConsumeMedicUberReadyPresentation())
            {
                continue;
            }

            player.TriggerChatBubble(ChatBubbleFrameCatalog.UberReady);
            RegisterWorldSoundEvent("UberChargedSnd", player.X, player.Y);
        }
    }

    private PlayerEntity? AcquireMedicHealingTarget(PlayerEntity medic, float aimWorldX, float aimWorldY)
    {
        return AcquireMedicBeamTarget(medic, aimWorldX, aimWorldY, requireSameTeam: true, maxDistance: MedicHealBeamRange);
    }

    private PlayerEntity? AcquireExperimentalEngineerEssenceExtractorTarget(PlayerEntity engineer, float aimWorldX, float aimWorldY)
    {
        return AcquireMedicBeamTarget(engineer, aimWorldX, aimWorldY, requireSameTeam: false, maxDistance: MedicHealBeamRange);
    }

    private PlayerEntity? AcquireExperimentalEngineerFreezeRayPrimaryTarget(PlayerEntity engineer, float aimWorldX, float aimWorldY)
    {
        return AcquireMedicBeamTarget(engineer, aimWorldX, aimWorldY, requireSameTeam: false, maxDistance: MedicHealBeamRange);
    }

    private PlayerEntity? AcquireMedicKritzBeamTarget(PlayerEntity medic, float aimWorldX, float aimWorldY, float maxRange)
    {
        return AcquireMedicBeamTarget(medic, aimWorldX, aimWorldY, requireSameTeam: false, maxDistance: maxRange);
    }

    private PlayerEntity[] AcquireExperimentalEngineerFreezeRayAdditionalTargets(PlayerEntity engineer, PlayerEntity primaryTarget)
    {
        var maxAdditionalTargets = Math.Max(0, global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayTargetCount - 1);
        if (maxAdditionalTargets <= 0)
        {
            return [];
        }

        var candidateTargets = new List<PlayerEntity>(maxAdditionalTargets);
        var chainRadius = global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayChainRadius;
        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Id == engineer.Id
                || candidate.Id == primaryTarget.Id
                || candidate.Team == engineer.Team
                || DistanceBetween(primaryTarget.X, primaryTarget.Y, candidate.X, candidate.Y) > chainRadius
                || DistanceBetween(engineer.X, engineer.Y, candidate.X, candidate.Y) > 340f)
            {
                continue;
            }

            candidateTargets.Add(candidate);
            if (candidateTargets.Count >= maxAdditionalTargets)
            {
                break;
            }
        }

        return [.. candidateTargets];
    }

    private PlayerEntity? AcquireMedicBeamTarget(
        PlayerEntity medic,
        float aimWorldX,
        float aimWorldY,
        bool requireSameTeam,
        float maxDistance)
    {
        const float maxMouseSelectDistance = 150f;
        const float aimSelectionThicknessRadius = 8f;
        var aimOriginX = medic.X;
        var aimOriginY = GetMedicAimOriginY(medic);
        var aimDeltaX = aimWorldX - aimOriginX;
        var aimDeltaY = aimWorldY - aimOriginY;
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
        var aimEndX = aimOriginX + directionX * maxDistance;
        var aimEndY = aimOriginY + directionY * maxDistance;
        PlayerEntity? bestTarget = null;
        var bestScore = 0f;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive
                || player.Id == medic.Id
                || (requireSameTeam ? player.Team != medic.Team : player.Team == medic.Team))
            {
                continue;
            }

            var healDistance = DistanceBetween(medic.X, medic.Y, player.X, player.Y);
            if (healDistance > maxDistance)
            {
                continue;
            }

            var hitDistance = GetThickLineIntersectionDistanceToPlayer(
                aimOriginX,
                aimOriginY,
                aimEndX,
                aimEndY,
                player,
                maxDistance,
                aimSelectionThicknessRadius);
            if (!hitDistance.HasValue)
            {
                hitDistance = GetThickLineIntersectionDistanceToPlayer(
                    medic.X,
                    medic.Y,
                    aimEndX,
                    aimEndY,
                    player,
                    maxDistance,
                    aimSelectionThicknessRadius);
            }
            if (!hitDistance.HasValue)
            {
                continue;
            }

            if (!HasMedicHealingLineOfSight(medic, player))
            {
                continue;
            }

            var mouseDistance = DistanceBetween(player.X, player.Y, aimWorldX, aimWorldY);
            var targetScore = mouseDistance <= maxMouseSelectDistance
                ? 3f - (mouseDistance / maxMouseSelectDistance)
                : 1f - (healDistance / maxDistance);
            if (targetScore < bestScore)
            {
                continue;
            }

            bestTarget = player;
            bestScore = targetScore;
        }

        return bestTarget;
    }

    private static float GetMedicAimOriginY(PlayerEntity medic)
    {
        return medic.Y - MathF.Min(8f, medic.Height * 0.25f);
    }

    private static float GetMedicTargetFocusY(PlayerEntity target)
    {
        return target.Y - MathF.Min(8f, target.Height * 0.25f);
    }

    private void FlushExperimentalEngineerEssenceExtractorHealing(PlayerEntity engineer)
    {
        var pendingHealing = engineer.FlushExperimentalEngineerEssenceExtractorHealing();
        if (pendingHealing <= 0)
        {
            return;
        }

        var appliedHealing = ApplyHealingWithFeedback(engineer, pendingHealing);
        engineer.AddHealPoints(appliedHealing);
    }

    private int GetExperimentalEngineerEssenceExtractorSlowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Ceiling(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorSlowRefreshSeconds * Config.TicksPerSecond));
    }

    private float GetContinuousPerTickRate(float perSecond)
    {
        var ticksPerSecond = Math.Max(1, Config.TicksPerSecond);
        return MathF.BitIncrement(perSecond / ticksPerSecond);
    }

    private bool HasMedicHealingLineOfSight(PlayerEntity medic, PlayerEntity target)
    {
        var medicAimOriginY = GetMedicAimOriginY(medic);
        var targetFocusY = GetMedicTargetFocusY(target);
        return HasObstacleLineOfSight(medic.X, medic.Y, target.X, target.Y)
            || HasObstacleLineOfSight(medic.X, medicAimOriginY, target.X, targetFocusY)
            || HasObstacleLineOfSight(medic.X, medicAimOriginY, target.X, target.Y)
            || HasObstacleLineOfSight(medic.X, medic.Y, target.X, targetFocusY);
    }

    private bool TryTriggerAcquiredMedigunHealsplosion(PlayerEntity player)
    {
        if (!player.CanTriggerAcquiredMedigunHealsplosion())
        {
            return false;
        }

        RegisterWorldSoundEvent("HealExplosionSnd", player.X, player.Y);
        RegisterVisualEffect("HealExplosion", player.X, player.Y);
        ApplyHealingWithFeedback(player, AcquiredMedigunHealsplosionSelfHealing);

        foreach (var candidate in EnumerateSimulatedPlayers())
        {
            if (!CanPlayerDamagePlayer(player, candidate) || candidate.Id == player.Id)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(this, candidate, player.X, player.Y);
            if (distance >= AcquiredMedigunHealsplosionRadius)
            {
                continue;
            }

            var distanceFactor = 1f - (distance / AcquiredMedigunHealsplosionRadius);
            if (distanceFactor <= AcquiredMedigunHealsplosionMinimumDamageFactor)
            {
                continue;
            }

            RegisterBloodEffect(candidate.X, candidate.Y, PointDirectionDegrees(player.X, player.Y, candidate.X, candidate.Y) - 180f, 3);
            if (ApplyPlayerContinuousDamage(candidate, AcquiredMedigunHealsplosionMaxDamage * distanceFactor, player, PlayerEntity.SpyDamageRevealAlpha))
            {
                KillPlayer(candidate, killer: player, weaponSpriteName: "NeedleKL");
            }
        }

        player.SetAcquiredWeapon(null);
        return true;
    }
}
