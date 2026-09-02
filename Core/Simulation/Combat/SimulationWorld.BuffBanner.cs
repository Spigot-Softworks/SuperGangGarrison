using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void TryRegisterBuffBannerDamage(PlayerEntity attacker, PlayerEntity target, int appliedDamage)
    {
        if (!attacker.IsAlive
            || attacker.ClassId != PlayerClass.Soldier
            || appliedDamage <= 0
            || ReferenceEquals(attacker, target)
            || attacker.Team == target.Team
            || !attacker.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.UtilityChannel,
                BuiltInGameplayBehaviorIds.SoldierBuffBanner,
                out var bannerItem)
            || bannerItem.Ability is not { } bannerAbility)
        {
            return;
        }

        var maxChargeDamage = GameplayAbilityParameterReader.GetInt(
            bannerAbility,
            "maxChargeDamage",
            PlayerEntity.BuffBannerDefaultMaxChargeDamage,
            minValue: 1);
        var wasReady = attacker.IsBuffBannerReady;
        var chargeChanged = attacker.TryAddBuffBannerDamageCharge(appliedDamage, maxChargeDamage);
        if (chargeChanged && !wasReady && attacker.IsBuffBannerReady)
        {
            RegisterWorldSoundEvent(PlayerEntity.BuffBannerReadySoundName, attacker.X, attacker.Y, attacker.Id);
        }
    }

    private void UpdateBuffBannerAuras()
    {
        foreach (var source in EnumerateSimulatedPlayers())
        {
            if (!source.IsAlive
                || !source.IsBuffBannerActive
                || source.ClassId != PlayerClass.Soldier
                || !source.HasGameplayAbilityBehavior(
                    GameplayAbilityConstants.UtilityChannel,
                    BuiltInGameplayBehaviorIds.SoldierBuffBanner))
            {
                continue;
            }

            var providerSlot = TryGetPlayerNetworkSlot(source, out var resolvedSlot)
                ? resolvedSlot
                : int.MaxValue;
            var radiusSquared = source.BuffBannerRadius * source.BuffBannerRadius;
            foreach (var target in EnumerateSimulatedPlayers())
            {
                if (!target.IsAlive || target.Team != source.Team)
                {
                    continue;
                }

                var deltaX = target.X - source.X;
                var deltaY = target.Y - source.Y;
                if ((deltaX * deltaX) + (deltaY * deltaY) >= radiusSquared)
                {
                    continue;
                }

                target.RefreshKritzCritBoost(
                    source.Id,
                    providerSlot,
                    source.BuffBannerDamageMultiplier,
                    ticks: 2);
            }
        }
    }

    private void ApplyBuffBannerRegeneration()
    {
        foreach (var target in EnumerateSimulatedPlayers())
        {
            if (!target.IsAlive)
            {
                continue;
            }

            var healthRegenPerSecond = 0f;
            foreach (var source in EnumerateSimulatedPlayers())
            {
                if (!source.IsAlive
                    || !source.IsBuffBannerActive
                    || source.ClassId != PlayerClass.Soldier
                    || source.Team != target.Team
                    || !source.HasGameplayAbilityBehavior(
                        GameplayAbilityConstants.UtilityChannel,
                        BuiltInGameplayBehaviorIds.SoldierBuffBanner))
                {
                    continue;
                }

                var deltaX = target.X - source.X;
                var deltaY = target.Y - source.Y;
                if ((deltaX * deltaX) + (deltaY * deltaY) >= source.BuffBannerRadius * source.BuffBannerRadius)
                {
                    continue;
                }

                healthRegenPerSecond = MathF.Max(
                    healthRegenPerSecond,
                    source.BuffBannerHealthRegenPerSecond);
            }

            if (healthRegenPerSecond > 0f)
            {
                ApplyHealingWithFeedback(
                    target,
                    healthRegenPerSecond / Math.Max(1, Config.TicksPerSecond));
            }
        }
    }
}
