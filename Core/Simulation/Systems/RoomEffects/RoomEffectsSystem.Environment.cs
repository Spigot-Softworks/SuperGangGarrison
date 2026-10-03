using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal sealed partial class RoomEffectsSystem
{
    internal void ApplyHealingCabinets(PlayerEntity player)
    {
        var usingHealingCabinet = false;
        foreach (var index in _host.Level.GetRoomObjectIndices(RoomObjectType.HealingCabinet))
        {
            ref readonly var roomObject = ref _host.Level.GetRoomObject(index);

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
                _host.WorldEffects.RegisterWorldSoundEvent("CbntHealSnd", roomObject.CenterX, roomObject.CenterY, player.Id);
                player.RestartHealingCabinetSoundCooldown();
            }
        }

        player.SetHealingCabinetState(usingHealingCabinet);
    }

    private static bool HasConfiguredGameplayAbilityCooldownForResupply(PlayerEntity player)
    {
        foreach (var item in GameplayAbilitySystem.ResolveAllPlayerGameplayAbilityItems(player))
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
        foreach (var item in GameplayAbilitySystem.ResolveAllPlayerGameplayAbilityItems(player))
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

    internal void UpdateSpawnRoomState(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            player.SetSpawnRoomState(false);
            return;
        }

        foreach (var index in _host.Level.GetRoomObjectIndices(RoomObjectType.SpawnRoom))
        {
            ref readonly var roomObject = ref _host.Level.GetRoomObject(index);

            if (IsPointInsideMarker(player.X, player.Y, roomObject))
            {
                player.SetSpawnRoomState(true);
                return;
            }
        }

        player.SetSpawnRoomState(false);
    }

    internal static bool IsPointInsideMarker(float x, float y, RoomObjectMarker roomObject)
    {
        return x >= roomObject.Left
            && x <= roomObject.Right
            && y >= roomObject.Top
            && y <= roomObject.Bottom;
    }

    internal void ApplyRoomHazards(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return;
        }

        foreach (var index in _host.Level.HazardIndices)
        {
            ref readonly var roomObject = ref _host.Level.GetRoomObject(index);
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

                    _host.WorldEffects.RegisterWorldSoundEvent("ExplosionSnd", player.X, player.Y);
                    _host.WorldEffects.RegisterVisualEffect("Explosion", player.X, player.Y);
                    _host.PlayerDeaths.KillPlayer(player, weaponSpriteName: "DeadKL");
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

                    _host.PlayerDeaths.KillPlayer(player);
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
