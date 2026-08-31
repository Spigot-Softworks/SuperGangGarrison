using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void TryRegisterBuffBannerKill(PlayerEntity killer, PlayerEntity victim)
    {
        if (!killer.IsAlive
            || killer.ClassId != PlayerClass.Soldier
            || killer.Team == victim.Team
            || !killer.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.UtilityChannel,
                BuiltInGameplayBehaviorIds.SoldierBuffBanner,
                out var bannerItem)
            || bannerItem.Ability is not { } bannerAbility)
        {
            return;
        }

        var maxChargeKills = GameplayAbilityParameterReader.GetInt(
            bannerAbility,
            "maxChargeKills",
            PlayerEntity.BuffBannerDefaultMaxChargeKills,
            minValue: 1);
        killer.TryAddBuffBannerKillCharge(maxChargeKills: maxChargeKills);
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
}
