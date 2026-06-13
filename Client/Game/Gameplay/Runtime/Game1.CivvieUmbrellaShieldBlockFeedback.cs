#nullable enable

using System;
using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private readonly Dictionary<int, CivvieUmbrellaShieldBlockObservationState> _civvieUmbrellaShieldBlockObservationByPlayerId = new();

    private readonly record struct CivvieUmbrellaShieldBlockObservationState(
        int ChargeTicks,
        bool IsUmbrellaActive,
        bool IsUmbrellaBroken);

    private void ResetCivvieUmbrellaShieldBlockObservation()
    {
        _civvieUmbrellaShieldBlockObservationByPlayerId.Clear();
    }

    private void ObserveCivvieUmbrellaShieldBlockDamageEvent(WorldDamageEvent damageEvent)
    {
        if (!IsCivvieUmbrellaShieldBlockEvent(damageEvent.Flags, damageEvent.TargetKind))
        {
            return;
        }

        TryTriggerCivvieUmbrellaShieldBlockVisual(damageEvent.TargetEntityId);
    }

    private void ObserveCivvieUmbrellaShieldBlockDamageEvent(SnapshotDamageEvent damageEvent)
    {
        if (!IsCivvieUmbrellaShieldBlockEvent((DamageEventFlags)damageEvent.Flags, (DamageTargetKind)damageEvent.TargetKind))
        {
            return;
        }

        TryTriggerCivvieUmbrellaShieldBlockVisual(damageEvent.TargetEntityId);
    }

    private static bool IsCivvieUmbrellaShieldBlockEvent(DamageEventFlags flags, DamageTargetKind targetKind)
    {
        return targetKind == DamageTargetKind.Player
            && flags.HasFlag(DamageEventFlags.CivvieUmbrellaBlock);
    }

    private void ObserveCivvieUmbrellaShieldBlocksFromPlayerState()
    {
        if (!_networkClient.IsConnected)
        {
            return;
        }

        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || player.ClassId != PlayerClass.Quote)
            {
                _civvieUmbrellaShieldBlockObservationByPlayerId.Remove(player.Id);
                continue;
            }

            ObserveCivvieUmbrellaShieldBlocksForPlayer(player);
        }

        if (_civvieUmbrellaShieldBlockObservationByPlayerId.Count == 0)
        {
            return;
        }

        var stalePlayerIds = new List<int>();
        foreach (var playerId in _civvieUmbrellaShieldBlockObservationByPlayerId.Keys)
        {
            if (FindPlayerById(playerId) is not { IsAlive: true } found
                || found.ClassId != PlayerClass.Quote)
            {
                stalePlayerIds.Add(playerId);
            }
        }

        for (var index = 0; index < stalePlayerIds.Count; index += 1)
        {
            _civvieUmbrellaShieldBlockObservationByPlayerId.Remove(stalePlayerIds[index]);
        }
    }

    private void ObserveCivvieUmbrellaShieldBlocksForPlayer(PlayerEntity player)
    {
        var currentState = CreateCivvieUmbrellaShieldBlockObservationState(player);

        if (_civvieUmbrellaShieldBlockObservationByPlayerId.TryGetValue(player.Id, out var previousState))
        {
            var blockCount = CountCivvieUmbrellaShieldBlocks(previousState, currentState);
            for (var blockIndex = 0; blockIndex < blockCount; blockIndex += 1)
            {
                TryTriggerCivvieUmbrellaShieldBlockVisual(player.Id);
            }
        }

        _civvieUmbrellaShieldBlockObservationByPlayerId[player.Id] = currentState;
    }

    private CivvieUmbrellaShieldBlockObservationState CreateCivvieUmbrellaShieldBlockObservationState(PlayerEntity player)
    {
        if (!_networkClient.IsConnected || ReferenceEquals(player, _world.LocalPlayer))
        {
            return new CivvieUmbrellaShieldBlockObservationState(
                player.CivvieUmbrellaChargeTicks,
                player.IsCivvieUmbrellaActive,
                player.IsCivvieUmbrellaBroken);
        }

        var chargeTicks = player.CivvieUmbrellaChargeTicks;
        if (GameplayAbilityReplicatedState.TryGetInt(
                player,
                GameplayAbilityReplicatedState.CivvieUmbrellaCooldownTicksKey,
                out var cooldownTicks))
        {
            chargeTicks = Math.Clamp(
                PlayerEntity.CivvieUmbrellaMaxChargeTicks - cooldownTicks,
                0,
                PlayerEntity.CivvieUmbrellaMaxChargeTicks);
        }

        var isUmbrellaActive = player.IsCivvieUmbrellaActive;
        if (GameplayAbilityReplicatedState.TryGetBool(
                player,
                GameplayAbilityReplicatedState.CivvieUmbrellaActiveKey,
                out var replicatedActive))
        {
            isUmbrellaActive = replicatedActive;
        }

        var isUmbrellaBroken = player.IsCivvieUmbrellaBroken;
        if (GameplayAbilityReplicatedState.TryGetBool(
                player,
                GameplayAbilityReplicatedState.CivvieUmbrellaDisabledKey,
                out var isDisabled)
            && isDisabled
            && chargeTicks <= 0)
        {
            isUmbrellaBroken = true;
        }

        return new CivvieUmbrellaShieldBlockObservationState(chargeTicks, isUmbrellaActive, isUmbrellaBroken);
    }

    private static int CountCivvieUmbrellaShieldBlocks(
        CivvieUmbrellaShieldBlockObservationState previousState,
        CivvieUmbrellaShieldBlockObservationState currentState)
    {
        if (!previousState.IsUmbrellaActive && !currentState.IsUmbrellaActive)
        {
            return 0;
        }

        var chargeDelta = previousState.ChargeTicks - currentState.ChargeTicks;
        if (chargeDelta <= 0)
        {
            return 0;
        }

        var blockCount = chargeDelta / PlayerEntity.CivvieUmbrellaImpactDrain;
        if (chargeDelta % PlayerEntity.CivvieUmbrellaImpactDrain > 0)
        {
            blockCount += 1;
        }

        return blockCount;
    }

    private void TryTriggerCivvieUmbrellaShieldBlockVisual(int targetPlayerId)
    {
        if (FindPlayerById(targetPlayerId) is not { IsAlive: true })
        {
            return;
        }

        const int maxBlockVisualsPerPlayer = 6;
        var count = 0;
        for (var index = _civvieUmbrellaShieldBlockVisuals.Count - 1; index >= 0; index -= 1)
        {
            if (_civvieUmbrellaShieldBlockVisuals[index].PlayerId != targetPlayerId)
            {
                continue;
            }

            count += 1;
            if (count >= maxBlockVisualsPerPlayer)
            {
                _civvieUmbrellaShieldBlockVisuals.RemoveAt(index);
            }
        }

        _civvieUmbrellaShieldBlockVisuals.Add(new CivvieUmbrellaShieldBlockVisual(targetPlayerId));
    }
}
