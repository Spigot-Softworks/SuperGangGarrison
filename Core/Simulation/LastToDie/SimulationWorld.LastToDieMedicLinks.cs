using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const byte LastToDieMedicStimulantDripProjectionFlag = 1 << 0;

    private const byte LastToDieMedicAgilityDriveProjectionFlag = 1 << 1;

    private const byte LastToDieMedicMartyrProtectedProjectionFlag = 1 << 2;

    private const byte LastToDieMedicMartyrProtectorProjectionFlag = 1 << 3;

    private readonly Dictionary<int, int> _lastToDieMartyrProtectorPlayerIdByProtectedTargetId = [];

    private bool TryResolveLastToDieMedicLink(
        PlayerEntity medic,
        out PlayerEntity target,
        out LastToDiePlayerPerkRuntime perkRuntime)
    {
        target = null!;
        perkRuntime = null!;
        if (!medic.IsAlive
            || medic.ClassId != PlayerClass.Medic
            || !medic.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Medigun)
            || !medic.IsMedicHealing
            || !medic.MedicHealTargetId.HasValue
            || !TryGetPlayerNetworkSlot(medic, out var medicSlot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(medicSlot, out perkRuntime))
        {
            return false;
        }

        var resolvedTarget = FindPlayerById(medic.MedicHealTargetId.Value);
        if (resolvedTarget is null
            || !CanMedicHealTarget(medic, resolvedTarget))
        {
            target = null!;
            perkRuntime = null!;
            return false;
        }

        target = resolvedTarget;
        return true;
    }

    private void RefreshLastToDieMedicLinkProjections()
    {
        var desiredProjectionByPlayerId = new Dictionary<int, byte>();
        _lastToDieMartyrProtectorPlayerIdByProtectedTargetId.Clear();
        foreach (var entry in _lastToDiePerkRuntimesBySlot.OrderBy(static entry => entry.Key))
        {
            if (!TryGetNetworkPlayer(entry.Key, out var medic)
                || !TryResolveLastToDieMedicLink(medic, out var target, out var perkRuntime))
            {
                entry.Value.MedicSupportRelayActiveLinkTargetPlayerId = null;
                continue;
            }

            var supportRelayLinkAcquired =
                perkRuntime.MedicSupportRelayActiveLinkTargetPlayerId != target.Id;
            perkRuntime.MedicSupportRelayActiveLinkTargetPlayerId = target.Id;
            if (supportRelayLinkAcquired && perkRuntime.Modifiers.MedicSupportRelayEnabled)
            {
                _ = TryApplyLastToDieMedicSupportRelay(medic, target);
            }

            if (perkRuntime.Modifiers.MedicAgilityDriveEnabled)
            {
                AddLastToDieMedicLinkProjection(
                    desiredProjectionByPlayerId,
                    medic.Id,
                    LastToDieMedicAgilityDriveProjectionFlag);
                AddLastToDieMedicLinkProjection(
                    desiredProjectionByPlayerId,
                    target.Id,
                    LastToDieMedicAgilityDriveProjectionFlag);
            }

            if (perkRuntime.Modifiers.MedicStimulantDripEnabled)
            {
                AddLastToDieMedicLinkProjection(
                    desiredProjectionByPlayerId,
                    target.Id,
                    LastToDieMedicStimulantDripProjectionFlag);
            }

            if (perkRuntime.Modifiers.MedicMartyrEnabled
                && _lastToDieMartyrProtectorPlayerIdByProtectedTargetId.TryAdd(target.Id, medic.Id))
            {
                AddLastToDieMedicLinkProjection(
                    desiredProjectionByPlayerId,
                    target.Id,
                    LastToDieMedicMartyrProtectedProjectionFlag);
                AddLastToDieMedicLinkProjection(
                    desiredProjectionByPlayerId,
                    medic.Id,
                    LastToDieMedicMartyrProtectorProjectionFlag);
            }
        }

        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            if (!TryGetNetworkPlayer(NetworkPlayerSlots[index], out var player))
            {
                continue;
            }

            desiredProjectionByPlayerId.TryGetValue(player.Id, out var projection);
            player.SetLastToDieMedicLinkProjection(
                stimulantDripActive: (projection & LastToDieMedicStimulantDripProjectionFlag) != 0,
                agilityDriveActive: (projection & LastToDieMedicAgilityDriveProjectionFlag) != 0,
                martyrProtectedActive: (projection & LastToDieMedicMartyrProtectedProjectionFlag) != 0,
                martyrProtectorActive: (projection & LastToDieMedicMartyrProtectorProjectionFlag) != 0);
        }
    }

    internal bool TryGetLastToDieMartyrProtector(
        PlayerEntity protectedTarget,
        out PlayerEntity protector)
    {
        protector = null!;
        if (!protectedTarget.LastToDieMedicMartyrProtectedLinkActive
            || !_lastToDieMartyrProtectorPlayerIdByProtectedTargetId.TryGetValue(
                protectedTarget.Id,
                out var protectorPlayerId))
        {
            return false;
        }

        var resolvedProtector = FindPlayerById(protectorPlayerId);
        if (resolvedProtector is null
            || !resolvedProtector.LastToDieMedicMartyrProtectorLinkActive
            || resolvedProtector.ClassId != PlayerClass.Medic
            || resolvedProtector.Team != protectedTarget.Team)
        {
            return false;
        }

        protector = resolvedProtector;
        return true;
    }

    private bool TryApplyLastToDieMedicSupportRelay(
        PlayerEntity medic,
        PlayerEntity target)
    {
        if (!medic.IsAlive
            || medic.ClassId != PlayerClass.Medic
            || !target.IsAlive
            || ReferenceEquals(medic, target)
            || medic.Team != target.Team
            || !TryGetPlayerNetworkSlot(medic, out var medicSlot)
            || !_lastToDiePerkRuntimesBySlot.TryGetValue(medicSlot, out var perkRuntime)
            || !perkRuntime.Modifiers.MedicSupportRelayEnabled)
        {
            return false;
        }

        if (perkRuntime.MedicSupportRelayCooldownUntilFrameByTargetPlayerId.TryGetValue(
                target.Id,
                out var cooldownUntilFrame)
            && Frame < cooldownUntilFrame)
        {
            return false;
        }

        if (!target.TryRestoreLastToDieSupportRelayAmmo())
        {
            return false;
        }

        perkRuntime.MedicSupportRelayCooldownUntilFrameByTargetPlayerId[target.Id] = checked(
            Frame
                + (LastToDieDerivedModifiers.MedicSupportRelayCooldownSeconds
                    * Math.Max(1, Config.TicksPerSecond)));
        return true;
    }

    private static void AddLastToDieMedicLinkProjection(
        Dictionary<int, byte> projectionByPlayerId,
        int playerId,
        byte projection)
    {
        projectionByPlayerId.TryGetValue(playerId, out var existing);
        projectionByPlayerId[playerId] = (byte)(existing | projection);
    }

    private bool TryResolveLastToDieExsanguinationMedic(
        PlayerEntity attacker,
        out PlayerEntity medic)
    {
        medic = null!;
        foreach (var entry in _lastToDiePerkRuntimesBySlot.OrderBy(static entry => entry.Key))
        {
            if (!entry.Value.Modifiers.MedicExsanguinationEnabled
                || !TryGetNetworkPlayer(entry.Key, out var candidateMedic)
                || !TryResolveLastToDieMedicLink(candidateMedic, out var healTarget, out _)
                || (!ReferenceEquals(candidateMedic, attacker)
                    && !ReferenceEquals(healTarget, attacker)))
            {
                continue;
            }

            medic = candidateMedic;
            return true;
        }

        return false;
    }

    private PlayerEntity? ResolveLastToDieMedicLinkedOnHit(
        PlayerEntity? attacker,
        PlayerEntity target,
        int appliedDamage,
        PlayerDamageTraits damageTraits)
    {
        if (attacker is null
            || appliedDamage <= 0
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || !damageTraits.HasFlag(PlayerDamageTraits.CanApplyOnHitEffects)
            || (damageTraits & (PlayerDamageTraits.Periodic | PlayerDamageTraits.Reflected)) != 0
            || !TryResolveLastToDieExsanguinationMedic(attacker, out var medic))
        {
            return null;
        }

        return medic;
    }

    private static int ResolveLastToDieMedicLinkedAssistPlayerId(
        PlayerEntity? attacker,
        PlayerEntity? medic)
    {
        return attacker is not null
            && medic is not null
            && medic.Id != attacker.Id
                ? medic.Id
                : -1;
    }

    private void ApplyLastToDieMedicLinkedOnHitEffects(
        PlayerEntity? attacker,
        PlayerEntity target,
        PlayerEntity? medic)
    {
        if (attacker is null || medic is null || target.Health <= 0)
        {
            return;
        }

        var durationTicks = checked(
            LastToDieDerivedModifiers.MedicExsanguinationDurationSeconds
                * Math.Max(1, Config.TicksPerSecond));
        int? assistingMedicPlayerId = ReferenceEquals(medic, attacker)
            ? null
            : medic.Id;
        _ = TryApplyLastToDieStatusEffect(
            target.Id,
            attacker.Id,
            LastToDieStatusEffectSpec.Bleed(
                LastToDieStatusEffectIds.MedicExsanguinationBleed,
                durationTicks,
                LastToDieDerivedModifiers.MedicExsanguinationBleedDamagePerSecond),
            assistingMedicPlayerId);
        _ = TryApplyLastToDieStatusEffect(
            target.Id,
            attacker.Id,
            LastToDieStatusEffectSpec.Slow(
                LastToDieStatusEffectIds.MedicExsanguinationSlow,
                durationTicks,
                LastToDieDerivedModifiers.MedicExsanguinationMovementSpeedMultiplier),
            assistingMedicPlayerId);
    }
}
