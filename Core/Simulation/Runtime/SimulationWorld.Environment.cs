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

    private bool WouldRunIntoWall(PlayerEntity player, float moveDirection)
    {
        if (moveDirection == 0f)
        {
            return false;
        }

        var probeDistance = 18f;
        var probeLeft = moveDirection > 0f
            ? player.Right + probeDistance
            : player.Left - probeDistance;
        var probeRight = probeLeft + MathF.Sign(moveDirection) * 2f;
        if (probeRight < probeLeft)
        {
            (probeLeft, probeRight) = (probeRight, probeLeft);
        }

        var probeTop = player.Top;
        var probeBottom = player.Bottom - 4f;
        foreach (var solid in Level.Solids)
        {
            if (probeLeft < solid.Right
                && probeRight > solid.Left
                && probeTop < solid.Bottom
                && probeBottom > solid.Top)
            {
                return true;
            }
        }

        foreach (var gate in Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            var gateLeft = gate.Left;
            var gateRight = gate.Right;
            var gateTop = gate.Top;
            var gateBottom = gate.Bottom;
            if (probeLeft < gateRight
                && probeRight > gateLeft
                && probeTop < gateBottom
                && probeBottom > gateTop)
            {
                return true;
            }
        }

        foreach (var wall in Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            var wallLeft = wall.Left;
            var wallRight = wall.Right;
            var wallTop = wall.Top;
            var wallBottom = wall.Bottom;
            if (probeLeft < wallRight
                && probeRight > wallLeft
                && probeTop < wallBottom
                && probeBottom > wallTop)
            {
                return true;
            }
        }

        return false;
    }

    private PlayerEntity? FindPlayerById(int playerId)
    {
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (player.Id == playerId)
            {
                return player;
            }
        }

        return null;
    }

    // Includes debug dummy players when enabled.
    private IEnumerable<PlayerEntity> EnumerateSimulatedPlayers()
    {
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            var slot = NetworkPlayerSlots[index];
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
        foreach (var roomObject in Level.RoomObjects)
        {
            if (roomObject.Type != RoomObjectType.HealingCabinet)
            {
                continue;
            }

            if (!player.IntersectsMarker(
                roomObject.CenterX,
                roomObject.CenterY,
                roomObject.Width,
                roomObject.Height))
            {
                continue;
            }

            var needsCabinet = player.Health < player.MaxHealth
                || player.Metal < player.MaxMetal
                || player.CurrentShells < player.PrimaryWeapon.MaxAmmo
                || player.ReloadTicksUntilNextShell > 0
                || player.MedicNeedleRefillTicks > 0
                || player.IsBurning
                || player.HeavyEatCooldownTicksRemaining > 0;
            if (!needsCabinet)
            {
                continue;
            }

            usingHealingCabinet = true;
            player.HealAndResupply();
            if (player.CanPlayHealingCabinetSound())
            {
                RegisterWorldSoundEvent("CbntHealSnd", player.X, player.Y);
                player.RestartHealingCabinetSoundCooldown();
            }
        }

        player.SetHealingCabinetState(usingHealingCabinet);
    }

    private void UpdateSpawnRoomState(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            player.SetSpawnRoomState(false);
            return;
        }

        foreach (var roomObject in Level.RoomObjects)
        {
            if (roomObject.Type != RoomObjectType.SpawnRoom)
            {
                continue;
            }

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

        foreach (var roomObject in Level.RoomObjects)
        {
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
            }
        }
    }
}
