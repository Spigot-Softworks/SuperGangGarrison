using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    private BladeProjectileEntity CreateSnapshotBlade(SnapshotShotState state)
    {
        var lifetimeSimulationTicks = _host.GetSimulationTicksFromSourceTicks(
            ResolveQuoteBladeLifetimeSourceTicks(state.OwnerId));
        var blade = new BladeProjectileEntity(
            state.Id,
            (PlayerTeam)state.Team,
            state.OwnerId,
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            hitDamage: 0,
            ticksRemaining: state.TicksRemaining,
            lifetimeSimulationTicks: lifetimeSimulationTicks,
            ownerAmmoGeneration: ResolveSnapshotBladeAmmoGeneration(state.OwnerId));
        blade.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
        blade.HydrateAmmoDrainProgress(
            state.TicksRemaining,
            LegacyMovementModel.SourceTicksPerSecond / Math.Max(1f, _host.Config.TicksPerSecond));
        return blade;
    }

    private void ApplySnapshotBladeState(BladeProjectileEntity entity, SnapshotShotState state)
    {
        var lifetimeSimulationTicks = _host.GetSimulationTicksFromSourceTicks(
            ResolveQuoteBladeLifetimeSourceTicks(state.OwnerId));
        entity.HydrateLifetimeSimulationTicks(lifetimeSimulationTicks);
        entity.ApplyNetworkState(
            state.X,
            state.Y,
            state.VelocityX,
            state.VelocityY,
            state.TicksRemaining,
            hitDamage: 0);
        entity.HydrateCritical(state.IsCritical, state.CriticalDamageMultiplier);
        entity.HydrateAmmoDrainProgress(
            state.TicksRemaining,
            LegacyMovementModel.SourceTicksPerSecond / Math.Max(1f, _host.Config.TicksPerSecond));
    }

    private int ResolveQuoteBladeLifetimeSourceTicks(int ownerId)
    {
        if (!_host.PlayerRegistry.ActivePlayersById.TryGetValue(ownerId, out var owner))
        {
            return PlayerEntity.QuoteBladeLifetimeTicks;
        }

        foreach (var abilityItem in owner.GetGameplayAbilityItems())
        {
            if (abilityItem.Ability is { } ability
                && string.Equals(
                    abilityItem.BehaviorId,
                    BuiltInGameplayBehaviorIds.QuoteBladeThrow,
                    StringComparison.Ordinal))
            {
                return GameplayAbilityParameterReader.GetInt(
                    ability,
                    "lifetimeTicks",
                    PlayerEntity.QuoteBladeLifetimeTicks,
                    minValue: 1);
            }
        }

        return PlayerEntity.QuoteBladeLifetimeTicks;
    }

    private long ResolveSnapshotBladeAmmoGeneration(int ownerId)
    {
        if (!_host.PlayerRegistry.ActivePlayersById.TryGetValue(ownerId, out var owner))
        {
            return 0;
        }

        if (owner.IsAlive
            && owner.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.Blade)
            && owner.HasSecondaryBehavior(BuiltInGameplayBehaviorIds.QuoteBladeThrow))
        {
            return owner.QuoteBladeAmmoGeneration;
        }

        return owner.QuoteBladeAmmoGeneration - 1;
    }

    private void ReapplyLocallyAheadQuoteBladeAmmoDrain(IReadOnlyList<SnapshotShotState> snapshotBlades)
    {
        for (var snapshotIndex = 0; snapshotIndex < snapshotBlades.Count; snapshotIndex += 1)
        {
            var snapshotBlade = snapshotBlades[snapshotIndex];
            var blade = _host.Blades.FirstOrDefault(candidate => candidate.Id == snapshotBlade.Id);
            if (blade is null
                || blade.OwnerId != snapshotBlade.OwnerId
                || !_host.PlayerRegistry.ActivePlayersById.TryGetValue(blade.OwnerId, out var owner)
                || blade.OwnerAmmoGeneration != owner.QuoteBladeAmmoGeneration)
            {
                continue;
            }

            var lifetimeSimulationTicks = _host.GetSimulationTicksFromSourceTicks(
                ResolveQuoteBladeLifetimeSourceTicks(blade.OwnerId));
            var elapsedSimulationTicks = Math.Clamp(
                lifetimeSimulationTicks - Math.Max(0, snapshotBlade.TicksRemaining),
                0,
                lifetimeSimulationTicks);
            var snapshotDrainedSourceTicks = (int)MathF.Floor(
                elapsedSimulationTicks
                    * (LegacyMovementModel.SourceTicksPerSecond / Math.Max(1f, _host.Config.TicksPerSecond)));
            var locallyDrainedExtraTicks = blade.AmmoDrainedSourceTicks - snapshotDrainedSourceTicks;
            for (var drainTick = 0; drainTick < locallyDrainedExtraTicks; drainTick += 1)
            {
                owner.DrainQuoteBladeAmmoSourceTick(blade.OwnerAmmoGeneration);
            }
        }
    }

    private void HydrateSnapshotQuoteBladeCounts()
    {
        foreach (var player in _host.PlayerRegistry.ActivePlayersById.Values)
        {
            var bladeCount = 0;
            for (var bladeIndex = 0; bladeIndex < _host.Blades.Count; bladeIndex += 1)
            {
                if (_host.Blades[bladeIndex].OwnerId == player.Id
                    && _host.Blades[bladeIndex].OwnerAmmoGeneration == player.QuoteBladeAmmoGeneration)
                {
                    bladeCount += 1;
                }
            }

            player.HydrateQuoteBladeCount(bladeCount);
        }
    }
}
