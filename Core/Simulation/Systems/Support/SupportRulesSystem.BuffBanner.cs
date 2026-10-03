using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal sealed partial class SupportRulesSystem
{
    internal void TryRegisterBuffBannerDamage(PlayerEntity attacker, PlayerEntity target, int appliedDamage)
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
            _host.WorldEffects.RegisterWorldSoundEvent(PlayerEntity.BuffBannerReadySoundName, attacker.X, attacker.Y, attacker.Id);
        }
    }

    internal void UpdateBuffBannerAuras()
    {
        foreach (var source in _host.EnumerateSimulatedPlayers())
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

            var providerSlot = _host.NetworkPlayers.TryGetPlayerNetworkSlot(source, out var resolvedSlot)
                ? resolvedSlot
                : int.MaxValue;
            var radiusSquared = source.BuffBannerRadius * source.BuffBannerRadius;
            foreach (var target in _host.EnumerateSimulatedPlayers())
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

    internal void ApplyBuffBannerRegeneration()
    {
        foreach (var target in _host.EnumerateSimulatedPlayers())
        {
            if (!target.IsAlive)
            {
                continue;
            }

            var healthRegenPerSecond = 0f;
            foreach (var source in _host.EnumerateSimulatedPlayers())
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
                _host.DamageRules.ApplyHealingWithFeedback(
                    target,
                    healthRegenPerSecond / Math.Max(1, _host.Config.TicksPerSecond));
            }
        }
    }
}
