using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool IsNetworkPlayerActive(byte slot)
    {
        return IsNetworkPlayerEnabled(slot);
    }

    private PlayerTeam GetNetworkPlayerTeam(byte slot)
    {
        return TryGetNetworkPlayer(slot, out var player)
            ? player.Team
            : GetNetworkPlayerConfiguredTeam(slot);
    }

    private PlayerEntity? FindPlayerById(int playerId)
    {
        if (_activeNetworkPlayersById.TryGetValue(playerId, out var player))
        {
            return player;
        }

        if (EnemyPlayerEnabled && EnemyPlayer.Id == playerId)
        {
            return EnemyPlayer;
        }

        if (FriendlyDummyEnabled && FriendlyDummy.Id == playerId)
        {
            return FriendlyDummy;
        }

        return null;
    }

    // Includes debug dummy players when enabled.
    private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers()
    {
        foreach (var slot in EnumerateEnabledNetworkPlayerSlots())
        {
            if (!IsNetworkPlayerEnabled(slot) || !TryGetNetworkPlayer(slot, out var player))
            {
                continue;
            }

            yield return player;
        }

        if (EnemyPlayerEnabled)
        {
            yield return EnemyPlayer;
        }

        if (FriendlyDummyEnabled)
        {
            yield return FriendlyDummy;
        }
    }

    private void ApplyHealingCabinets(PlayerEntity player)
    {
        var usingHealingCabinet = false;
        foreach (var index in Level.GetRoomObjectIndices(RoomObjectType.HealingCabinet))
        {
            ref readonly var roomObject = ref Level.GetRoomObject(index);

            if (!player.IntersectsMarker(
                roomObject.CenterX,
                roomObject.CenterY,
                roomObject.Width,
                roomObject.Height))
            {
                continue;
            }

            if (player.IsHealingCabinetResupplyOnCooldown())
            {
                usingHealingCabinet = true;
                continue;
            }

            var needsCabinet = player.NeedsHealingCabinetResupply()
                || HasConfiguredGameplayAbilityCooldownForResupply(player);
            if (!needsCabinet)
            {
                continue;
            }

            usingHealingCabinet = true;
            player.HealAndResupply();
            player.RestartHealingCabinetResupplyCooldown();
            ClearConfiguredGameplayAbilityCooldownsForResupply(player);
            if (player.CanPlayHealingCabinetSound())
            {
                RegisterWorldSoundEvent("CbntHealSnd", roomObject.CenterX, roomObject.CenterY, player.Id);
                player.RestartHealingCabinetSoundCooldown();
            }
        }

        player.SetHealingCabinetState(usingHealingCabinet);
    }

    private static bool HasConfiguredGameplayAbilityCooldownForResupply(PlayerEntity player)
    {
        foreach (var item in ResolveAllPlayerGameplayAbilityItems(player))
        {
            if (TryResolveConfiguredAbilityCooldownState(item, out var stateOwner, out var cooldownKey)
                && player.TryGetReplicatedStateInt(stateOwner, cooldownKey, out var cooldownTicks)
                && cooldownTicks > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearConfiguredGameplayAbilityCooldownsForResupply(PlayerEntity player)
    {
        foreach (var item in ResolveAllPlayerGameplayAbilityItems(player))
        {
            if (!TryResolveConfiguredAbilityCooldownState(item, out var stateOwner, out var cooldownKey)
                || !player.TryGetReplicatedStateInt(stateOwner, cooldownKey, out var cooldownTicks)
                || cooldownTicks <= 0)
            {
                continue;
            }

            player.SetGameplayAbilityCooldownReplicatedState(stateOwner, cooldownKey, 0);
        }
    }

    private static bool TryResolveConfiguredAbilityCooldownState(
        GameplayItemDefinition item,
        out string stateOwner,
        out string cooldownKey)
    {
        stateOwner = string.Empty;
        cooldownKey = string.Empty;
        var hud = item.Presentation.Hud;
        if (hud is null
            || !string.Equals(hud.StateProvider, GameplayItemHudStateProviders.AbilityCooldown, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(hud.StateOwner)
            || string.IsNullOrWhiteSpace(hud.CooldownKey))
        {
            return false;
        }

        stateOwner = hud.StateOwner.Trim();
        cooldownKey = hud.CooldownKey.Trim();
        return !string.Equals(stateOwner, GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, StringComparison.Ordinal);
    }

    private void UpdateSpawnRoomState(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            player.SetSpawnRoomState(false);
            return;
        }

        foreach (var index in Level.GetRoomObjectIndices(RoomObjectType.SpawnRoom))
        {
            ref readonly var roomObject = ref Level.GetRoomObject(index);

            if (IsPointInsideMarker(player.X, player.Y, roomObject))
            {
                player.SetSpawnRoomState(true);
                return;
            }
        }

        player.SetSpawnRoomState(false);
    }

    private static bool IsPointInsideMarker(float x, float y, RoomObjectMarker roomObject)
    {
        return x >= roomObject.Left
            && x <= roomObject.Right
            && y >= roomObject.Top
            && y <= roomObject.Bottom;
    }

    private void ApplyRoomHazards(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return;
        }

        foreach (var index in Level.HazardIndices)
        {
            ref readonly var roomObject = ref Level.GetRoomObject(index);
            switch (roomObject.Type)
            {
                case RoomObjectType.FragBox:
                    if (!player.IntersectsMarker(
                        roomObject.CenterX,
                        roomObject.CenterY,
                        roomObject.Width,
                        roomObject.Height))
                    {
                        continue;
                    }

                    RegisterWorldSoundEvent("ExplosionSnd", player.X, player.Y);
                    RegisterVisualEffect("Explosion", player.X, player.Y);
                    KillPlayer(player, weaponSpriteName: "DeadKL");
                    return;
                case RoomObjectType.KillBox:
                    if (!player.IntersectsMarker(
                        roomObject.CenterX,
                        roomObject.CenterY,
                        roomObject.Width,
                        roomObject.Height))
                    {
                        continue;
                    }

                    KillPlayer(player);
                    return;
                case RoomObjectType.FireBox:
                    if (!player.IntersectsMarker(
                        roomObject.CenterX,
                        roomObject.CenterY,
                        roomObject.Width,
                        roomObject.Height))
                    {
                        continue;
                    }

                    // Refresh the burn every simulation tick while the player
                    // remains inside. An environment hazard has no attacking
                    // player to credit, so owner id 0 intentionally produces
                    // unattributed afterburn damage.
                    player.IgniteAfterburn(
                        ownerPlayerId: 0,
                        durationIncreaseSourceTicks: PlayerEntity.BurnDefaultMaxDurationSourceTicks,
                        intensityIncrease: PlayerEntity.BurnMaxIntensity,
                        afterburnFalloff: false,
                        burnFalloffAmount: 0f);
                    break;
            }
        }
    }
}
