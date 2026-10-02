using OpenGarrison.GameplayModding;
using OpenGarrison.Core.LastToDie;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

internal sealed partial class SupportRulesSystem
{
    private const float AcquiredMedigunHealsplosionRadius = 155f;
    private const float AcquiredMedigunHealsplosionMaxDamage = 110f;
    private const float AcquiredMedigunHealsplosionSelfHealing = 120f;
    private const float AcquiredMedigunHealsplosionMinimumDamageFactor = 0.12f;
    private const float MedicUberChargeGainPerTickHealthyTarget = MedicBeamDefaults.UberChargeGainPerTickHealthyTarget;
    private const float MedicUberChargeGainPerTickDamagedTarget = 2.5f;
    private const float MedicHealBeamRange = MedicBeamDefaults.HealBeamRange;
    private const float MedicKritzBeamDefaultRange = MedicBeamDefaults.KritzBeamDefaultRange;
    private const float MedicKritzBeamDefaultDamagePerSecond = MedicBeamDefaults.KritzBeamDefaultDamagePerSecond;
    private const float MedicKritzBeamDefaultChargePerTick = MedicBeamDefaults.KritzBeamDefaultChargePerTick;

    internal string GetMedicSummary()
    {
        var healTarget = _host.LocalPlayer.MedicHealTargetId.HasValue ? _host.LocalPlayer.MedicHealTargetId.Value.ToString(CultureInfo.InvariantCulture) : "none";
        return $"class={_host.LocalPlayer.ClassName} uber={_host.LocalPlayer.MedicUberCharge:F1}/2000 ready={_host.LocalPlayer.IsMedicUberReady} ubering={_host.LocalPlayer.IsMedicUbering} healing={_host.LocalPlayer.IsMedicHealing} target={healTarget} needles={_host.LocalPlayer.CurrentShells}/{_host.LocalPlayer.MaxShells}";
    }

    internal bool TryFillLocalMedicUber()
    {
        if (!_host.LocalPlayer.HasGameplayAbilityBehavior(
                GameplayAbilityConstants.SpecialChannel,
                BuiltInGameplayBehaviorIds.MedicUber))
        {
            return false;
        }

        _host.LocalPlayer.FillMedicUberCharge();
        return true;
    }

    internal void UpdateMedicHealing(PlayerEntity medic, float aimWorldX, float aimWorldY)
    {
        if (!medic.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Medigun)
            && !medic.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
        {
            return;
        }

        var existingTarget = medic.MedicHealTargetId.HasValue
            ? _host.FindPlayerById(medic.MedicHealTargetId.Value)
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

    internal void UpdateExperimentalEngineerEssenceExtractor(PlayerEntity engineer, float aimWorldX, float aimWorldY)
    {
        var existingTarget = engineer.MedicHealTargetId.HasValue
            ? _host.FindPlayerById(engineer.MedicHealTargetId.Value)
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

    internal void UpdateExperimentalEngineerFreezeRay(PlayerEntity engineer, float aimWorldX, float aimWorldY)
    {
        var primaryTarget = engineer.MedicHealTargetId.HasValue
            ? _host.FindPlayerById(engineer.MedicHealTargetId.Value)
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

    internal bool UpdateMedicKritzBeam(
        PlayerEntity medic,
        float aimWorldX,
        float aimWorldY,
        float maxRange = MedicKritzBeamDefaultRange,
        float damagePerSecond = MedicKritzBeamDefaultDamagePerSecond,
        float chargePerTick = MedicKritzBeamDefaultChargePerTick)
    {
        if (medic.ClassId != PlayerClass.Medic || !medic.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit))
        {
            return false;
        }

        maxRange = Math.Clamp(maxRange, 1f, MedicHealBeamRange);
        var existingTarget = medic.MedicHealTargetId.HasValue
            ? _host.FindPlayerById(medic.MedicHealTargetId.Value)
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

    internal bool CanMedicHealTarget(PlayerEntity medic, PlayerEntity target)
    {
        if (!target.IsAlive || target.Team != medic.Team || target.Id == medic.Id)
        {
            return false;
        }

        if (SimulationMath.DistanceBetween(medic.X, medic.Y, target.X, target.Y) > MedicHealBeamRange)
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

        if (SimulationMath.DistanceBetween(engineer.X, engineer.Y, target.X, target.Y) > MedicHealBeamRange)
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

        if (SimulationMath.DistanceBetween(medic.X, medic.Y, target.X, target.Y) > maxRange)
        {
            return false;
        }

        return HasMedicHealingLineOfSight(medic, target);
    }

    private bool CanExperimentalEngineerFreezeRayTarget(PlayerEntity engineer, PlayerEntity target)
    {
        return CanExperimentalEngineerEssenceExtractorTarget(engineer, target);
    }

    internal void ApplyMedicHealing(PlayerEntity medic, PlayerEntity target)
    {
        target.ReduceBurnDuration((float)_host.Config.FixedDeltaSeconds * LegacyMovementModel.SourceTicksPerSecond);

        var healAmount = target.Health < target.MaxHealth / 2f
            ? 1f
            : target.Health < target.MaxHealth
                ? 0.5f
                : 0f;
        if (healAmount > 0f)
        {
            healAmount *= _host.GetLastToDieMedicHealingMultiplier(medic, target);
            if (medic.IsMedicRejuvenationRayDeliveryActive)
            {
                healAmount *= global::OpenGarrison.Core.LastToDie.LastToDieDerivedModifiers.MedicRejuvenationRayHealingMultiplier;
            }

            var appliedHealing = target.ApplyContinuousHealingAndGetAmount(healAmount);
            _host.AwardHealingPoints(medic, appliedHealing);
            _host.ApplyLastToDieMedicHomeostasis(medic, appliedHealing);
        }

        if (!medic.IsMedicUbering)
        {
            var uberGain = target.Health < target.MaxHealth || _host.ControlPointSetupActive
                ? MedicUberChargeGainPerTickDamagedTarget
                : MedicUberChargeGainPerTickHealthyTarget;
            medic.AddMedicUberCharge(uberGain * _host.GetLastToDieMedicUberChargeGainMultiplier(medic));
        }

        medic.SetMedicHealingTarget(target);
    }

    internal void ApplyMedicHealNeedleTeammateHit(
        PlayerEntity? medic,
        PlayerEntity target,
        MedicHealNeedleProjectileEntity needle)
    {
        if (!target.IsAlive
            || target.Id == needle.OwnerId
            || target.Team != needle.Team)
        {
            return;
        }

        if (needle.LastToDiePayload.IsMedicKritzM2
            && needle.AppliesLastToDieHailMary)
        {
            _ = target.RefreshLastToDieMedicHailMaryInvulnerability(
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        LastToDieDerivedModifiers.MedicHailMaryInvulnerabilitySeconds
                            * Math.Max(1, _host.Config.TicksPerSecond))));
        }

        target.ReduceBurnDuration((float)_host.Config.FixedDeltaSeconds * LegacyMovementModel.SourceTicksPerSecond);

        var healedAmount = needle.HealPerHit > 0
            ? _host.ApplyHealingWithFeedback(
                target,
                needle.HealPerHit,
                "HealSnd",
                medic?.X ?? needle.X,
                medic?.Y ?? needle.Y)
            : 0;
        if (medic is not
            {
                IsAlive: true,
                ClassId: PlayerClass.Medic,
            }
            || !medic.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MedigunCrit)
            || medic.Team != needle.Team)
        {
            return;
        }

        if (healedAmount > 0)
        {
            _host.AwardHealingPoints(medic, healedAmount);
            _host.ApplyLastToDieMedicHomeostasis(medic, healedAmount);
        }

        if (!medic.IsMedicUbering)
        {
            var uberGain = healedAmount > 0f
                ? healedAmount * MedicHealNeedleProjectileEntity.DamagedTargetUberChargePerHealedHealth
                : target.Health < target.MaxHealth || _host.ControlPointSetupActive
                    ? MedicHealNeedleProjectileEntity.DamagedTargetUberChargePerHealedHealth
                    : MedicHealNeedleProjectileEntity.HealthyTargetUberChargePerHit;
            medic.AddMedicUberCharge(uberGain * _host.GetLastToDieMedicUberChargeGainMultiplier(medic));
        }

        _ = _host.TryApplyLastToDieMedicSupportRelay(medic, target);
    }

    private void ApplyExperimentalEngineerEssenceExtractor(PlayerEntity engineer, PlayerEntity target)
    {
        var healthBefore = target.Health;
        var damagePerTick = GetContinuousPerTickRate(
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorDrainPerSecond);
        if (_host.ApplyPlayerContinuousDamage(target, damagePerTick, engineer, PlayerEntity.SpyDamageRevealAlpha))
        {
            _host.KillPlayer(target, killer: engineer, weaponSpriteName: "NeedleKL");
        }

        var appliedDamage = Math.Max(0, healthBefore - target.Health);
        if (appliedDamage > 0)
        {
            var appliedHealing = engineer.ApplyContinuousHealingAndGetAmount(appliedDamage);
            _host.AwardHealingPoints(engineer, appliedHealing);
            var chunkHealing = engineer.AccumulateExperimentalEngineerEssenceExtractorHealing(
                appliedHealing,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorHealingChunkSize);
            if (chunkHealing > 0)
            {
                _host.RegisterHealingFeedbackOnly(engineer, chunkHealing);
            }
        }

        target.RefreshExperimentalDamageTakenDebuff(
            _host.GetExperimentalEngineerEssenceExtractorDebuffTicks(),
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
        if (_host.ApplyPlayerContinuousDamage(target, damagePerTick, engineer, PlayerEntity.SpyDamageRevealAlpha))
        {
            _host.KillPlayer(target, killer: engineer, weaponSpriteName: "NeedleKL", gibbed: target.IsExperimentalCryoFrozen);
        }
        target.AccumulateExperimentalCryoExposure(
            engineer.Id,
            freezeThresholdTicks: _host.GetExperimentalEngineerFreezeRayFreezeThresholdTicks(),
            exposureWindowTicks: _host.GetExperimentalEngineerFreezeRayExposureWindowTicks(),
            freezeDurationTicks: _host.GetExperimentalEngineerCryoFreezeDurationTicks(),
            slowMovementMultiplier: global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRaySlowMovementMultiplier,
            slowTicks: _host.GetExperimentalEngineerFreezeRaySlowTicks());
        target.RefreshExperimentalFreezeRayCombatDebuff(
            _host.GetExperimentalEngineerFreezeRaySlowTicks(),
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayAttackCycleMultiplier,
            global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerFreezeRayOutgoingDamageMultiplier);
    }

    private void ApplyMedicKritzBeam(PlayerEntity medic, PlayerEntity target, float damagePerSecond, float chargePerTick)
    {
        var healthBefore = target.Health;
        var damagePerTick = damagePerSecond <= 0f
            ? 0f
            : GetContinuousPerTickRate(damagePerSecond);
        if (_host.ApplyPlayerContinuousDamage(target, damagePerTick, medic, PlayerEntity.SpyDamageRevealAlpha))
        {
            _host.KillPlayer(target, killer: medic, weaponSpriteName: "NeedleKL");
        }

        if (healthBefore > target.Health)
        {
            _host.RegisterBloodEffect(target.X, target.Y, SimulationMath.PointDirectionDegrees(medic.X, medic.Y, target.X, target.Y) - 180f, 4);
        }

        if (!medic.IsMedicUbering)
        {
            medic.AddMedicUberCharge(
                Math.Max(0f, chargePerTick) * _host.GetLastToDieMedicUberChargeGainMultiplier(medic));
        }

        medic.SetMedicHealingTarget(target);
        medic.ClearExperimentalAdditionalMedicBeamTargets();
    }

    internal void AdvanceMedicUberEffects()
    {
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive
                || !player.HasGameplayAbilityBehavior(
                    GameplayAbilityConstants.SpecialChannel,
                    BuiltInGameplayBehaviorIds.MedicUber)
                || !player.IsMedicUbering)
            {
                continue;
            }

            if (player.IsMedicKritzUberDeliveryActive)
            {
                _host.TryGetPlayerNetworkSlot(player, out var providerSlot);
                var criticalDamageMultiplier = LastToDieRulesSystem.GetLastToDieMedicKritzCriticalDamageMultiplier(player);
                player.RefreshKritzCritBoost(
                    player.Id,
                    providerSlot,
                    criticalDamageMultiplier);
            }
            else if (player.IsMedicInvulnerabilityUberDeliveryActive)
            {
                player.RefreshUber();
            }

            if (!player.MedicHealTargetId.HasValue)
            {
                continue;
            }

            var healTarget = _host.FindPlayerById(player.MedicHealTargetId.Value);
            if (healTarget is not null && healTarget.IsAlive)
            {
                if (player.IsMedicKritzUberDeliveryActive)
                {
                    _host.TryGetPlayerNetworkSlot(player, out var providerSlot);
                    healTarget.RefreshKritzCritBoost(
                        player.Id,
                        providerSlot,
                        LastToDieRulesSystem.GetLastToDieMedicKritzCriticalDamageMultiplier(player));
                }
                else if (player.IsMedicInvulnerabilityUberDeliveryActive
                    && !healTarget.IsCarryingIntel)
                {
                    healTarget.RefreshUber();
                }
            }
        }
    }

    internal void EmitPendingMedicUberReadyPresentation()
    {
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.TryConsumeMedicUberReadyPresentation())
            {
                continue;
            }

            player.TriggerChatBubble(ChatBubbleFrameCatalog.UberReady);
            _host.RegisterWorldSoundEvent("UberChargedSnd", player.X, player.Y);
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
        foreach (var candidate in _host.EnumerateSimulatedPlayers())
        {
            if (!candidate.IsAlive
                || candidate.Id == engineer.Id
                || candidate.Id == primaryTarget.Id
                || candidate.Team == engineer.Team
                || SimulationMath.DistanceBetween(primaryTarget.X, primaryTarget.Y, candidate.X, candidate.Y) > chainRadius
                || SimulationMath.DistanceBetween(engineer.X, engineer.Y, candidate.X, candidate.Y) > 340f)
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
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive
                || player.Id == medic.Id
                || (requireSameTeam ? player.Team != medic.Team : player.Team == medic.Team))
            {
                continue;
            }

            var healDistance = SimulationMath.DistanceBetween(medic.X, medic.Y, player.X, player.Y);
            if (healDistance > maxDistance)
            {
                continue;
            }

            var hitDistance = _host.GetThickLineIntersectionDistanceToPlayer(
                aimOriginX,
                aimOriginY,
                aimEndX,
                aimEndY,
                player,
                maxDistance,
                aimSelectionThicknessRadius);
            if (!hitDistance.HasValue)
            {
                hitDistance = _host.GetThickLineIntersectionDistanceToPlayer(
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

            var mouseDistance = SimulationMath.DistanceBetween(player.X, player.Y, aimWorldX, aimWorldY);
            var targetScore = mouseDistance <= maxMouseSelectDistance
                ? 3f - (mouseDistance / maxMouseSelectDistance)
                : 1f - (healDistance / maxDistance);
            if (targetScore < bestScore)
            {
                // LOS cannot make a lower-scoring candidate win. Defer the
                // expensive four-ray check until the candidate can actually
                // replace the current target; selection semantics are unchanged.
                continue;
            }

            if (!HasMedicHealingLineOfSight(medic, player))
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

    internal void FlushExperimentalEngineerEssenceExtractorHealing(PlayerEntity engineer)
    {
        var pendingHealing = engineer.FlushExperimentalEngineerEssenceExtractorHealing();
        if (pendingHealing <= 0)
        {
            return;
        }

        var appliedHealing = _host.ApplyHealingWithFeedback(engineer, pendingHealing);
        _host.AwardHealingPoints(engineer, appliedHealing);
    }

    private int GetExperimentalEngineerEssenceExtractorSlowTicks()
    {
        return Math.Max(
            1,
            (int)MathF.Ceiling(global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerEssenceExtractorSlowRefreshSeconds * _host.Config.TicksPerSecond));
    }

    private float GetContinuousPerTickRate(float perSecond)
    {
        var ticksPerSecond = Math.Max(1, _host.Config.TicksPerSecond);
        return MathF.BitIncrement(perSecond / ticksPerSecond);
    }

    private bool HasMedicHealingLineOfSight(PlayerEntity medic, PlayerEntity target)
    {
        var medicAimOriginY = GetMedicAimOriginY(medic);
        var targetFocusY = GetMedicTargetFocusY(target);
        return _host.HasObstacleLineOfSight(medic.X, medic.Y, target.X, target.Y)
            || _host.HasObstacleLineOfSight(medic.X, medicAimOriginY, target.X, targetFocusY)
            || _host.HasObstacleLineOfSight(medic.X, medicAimOriginY, target.X, target.Y)
            || _host.HasObstacleLineOfSight(medic.X, medic.Y, target.X, targetFocusY);
    }

    internal bool TryTriggerAcquiredMedigunHealsplosion(PlayerEntity player)
    {
        if (!player.CanTriggerAcquiredMedigunHealsplosion())
        {
            return false;
        }

        _host.RegisterWorldSoundEvent("HealExplosionSnd", player.X, player.Y);
        _host.RegisterVisualEffect("HealExplosion", player.X, player.Y);
        _host.ApplyHealingWithFeedback(player, AcquiredMedigunHealsplosionSelfHealing);

        foreach (var candidate in _host.EnumerateSimulatedPlayers())
        {
            if (!_host.CanPlayerDamagePlayer(player, candidate) || candidate.Id == player.Id)
            {
                continue;
            }

            var distance = GetExplosionDistanceToPlayer(candidate, player.X, player.Y);
            if (distance >= AcquiredMedigunHealsplosionRadius)
            {
                continue;
            }

            var distanceFactor = 1f - (distance / AcquiredMedigunHealsplosionRadius);
            if (distanceFactor <= AcquiredMedigunHealsplosionMinimumDamageFactor)
            {
                continue;
            }

            _host.RegisterBloodEffect(candidate.X, candidate.Y, SimulationMath.PointDirectionDegrees(player.X, player.Y, candidate.X, candidate.Y) - 180f, 3);
            if (_host.ApplyPlayerContinuousDamage(candidate, AcquiredMedigunHealsplosionMaxDamage * distanceFactor, player, PlayerEntity.SpyDamageRevealAlpha))
            {
                _host.KillPlayer(candidate, killer: player, weaponSpriteName: "NeedleKL");
            }
        }

        player.SetAcquiredWeapon(null);
        return true;
    }
}
