namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{

    private void AdvancePlayableNetworkPlayer(byte slot)
    {
        if (!IsNetworkPlayerActive(slot) || !TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        var input = ResolveNetworkPlayerInput(slot);
        var previousInput = GetPreviousNetworkInput(slot);
        if (player.IsAlive)
        {
            AdvanceAlivePlayerWithInput(player, input, previousInput, GetNetworkPlayerTeam(slot), slot == LocalPlayerSlot);
        }
        else
        {
            AdvanceNetworkRespawnTimer(slot);
        }

        SetPreviousNetworkInput(slot, input);
    }

    private void AdvanceAlivePlayerWithInput(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        PlayerTeam team,
        bool allowDebugKill)
    {
        var preAdvanceX = player.X;
        var preAdvanceY = player.Y;
        var isHumiliated = IsPlayerHumiliated(player);

        if (isHumiliated)
        {
            input = input with
            {
                FirePrimary = false,
                FireSecondary = false,
                BuildSentry = false,
                DestroySentry = false,
            };
        }

        var jumpPressed = input.Up && !previousInput.Up;
        var dropPressed = input.DropIntel && !previousInput.DropIntel;
        var buildPressed = input.BuildSentry && !previousInput.BuildSentry;
        var destroyPressed = input.DestroySentry && !previousInput.DestroySentry;
        var tauntPressed = input.Taunt && !previousInput.Taunt;
        var killPressed = input.DebugKill && !previousInput.DebugKill;
        var secondaryPressed = input.FireSecondary && !previousInput.FireSecondary;
        var allowHeldSecondary = player.ClassId is PlayerClass.Demoman or PlayerClass.Quote;
        var suppressPyroPrimaryThisTick = player.ClassId == PlayerClass.Pyro
            && secondaryPressed
            && player.CanFirePyroAirblast();

        var healthBeforeTick = player.Health;
        var afterburn = player.AdvanceTickState(input, Config.FixedDeltaSeconds);
        if (healthBeforeTick > player.Health)
        {
            var burner = afterburn.BurnedByPlayerId.HasValue
                ? FindPlayerById(afterburn.BurnedByPlayerId.Value)
                : null;
            RegisterDamageEvent(
                burner,
                DamageTargetKind.Player,
                player.Id,
                player.X,
                player.Y,
                healthBeforeTick - player.Health,
                afterburn.IsFatal);
        }

        if (afterburn.IsFatal)
        {
            var burner = afterburn.BurnedByPlayerId.HasValue
                ? FindPlayerById(afterburn.BurnedByPlayerId.Value)
                : null;
            KillPlayer(player, killer: burner, weaponSpriteName: "FlameKL");
            return;
        }

        if (isHumiliated && player.ClassId == PlayerClass.Spy && !player.IsSpyBackstabAnimating)
        {
            player.ForceDecloak();
        }

        TryHandleNetworkPrimaryFire(player, input, suppressPyroPrimaryThisTick);

        if (tauntPressed)
        {
            player.TryStartTaunt();
        }

        var startedGrounded = player.PrepareMovement(input, Level, team, Config.FixedDeltaSeconds, out var canMove);
        var jumped = player.TryJumpIfPossible(canMove, jumpPressed);
        var emitWallspinDust = player.IsAlive && player.IsPerformingSourceSpinjump(Level);
        if (jumped)
        {
            RegisterWorldSoundEvent("JumpSnd", player.X, player.Y);
        }
        if (player.ClassId == PlayerClass.Medic)
        {
            if (input.FireSecondary)
            {
                TryHandleNetworkSecondaryFire(player, input, preAdvanceX, preAdvanceY);
            }
        }
        else if ((allowHeldSecondary && input.FireSecondary) || (!allowHeldSecondary && secondaryPressed))
        {
            TryHandleNetworkSecondaryFire(player, input, preAdvanceX, preAdvanceY);
        }

        if (emitWallspinDust)
        {
            RegisterWallspinDustEffect(player);
        }

        player.CompleteMovement(Level, team, Config.FixedDeltaSeconds, startedGrounded, jumped);
        TryRegisterIntelTrailEffect(player);
        UpdateSpawnRoomState(player);
        TryActivatePendingSpyBackstab(player);

        if (dropPressed)
        {
            TryDropCarriedIntel(player);
        }

        if (buildPressed)
        {
            TryBuildSentry(player);
        }
        if (destroyPressed)
        {
            TryDestroySentry(player);
        }

        ApplyHealingCabinets(player);
        ApplyRoomHazards(player);
        if (!player.IsAlive)
        {
            return;
        }

        if (allowDebugKill && killPressed)
        {
            KillPlayer(player);
        }
    }

    private PlayerInputSnapshot ResolveNetworkPlayerInput(byte slot)
    {
        if (slot == LocalPlayerSlot)
        {
            return _localInput;
        }

        return _additionalNetworkPlayerInputs.TryGetValue(slot, out var input) ? input : default;
    }

    private PlayerInputSnapshot GetPreviousNetworkInput(byte slot)
    {
        if (slot == LocalPlayerSlot)
        {
            return _previousLocalInput;
        }

        return _additionalNetworkPlayerPreviousInputs.TryGetValue(slot, out var input) ? input : default;
    }

    private void SetPreviousNetworkInput(byte slot, PlayerInputSnapshot input)
    {
        if (slot == LocalPlayerSlot)
        {
            _previousLocalInput = input;
            return;
        }

        _additionalNetworkPlayerPreviousInputs[slot] = input;
    }


    private void TryHandleNetworkPrimaryFire(PlayerEntity player, PlayerInputSnapshot input, bool suppressPyroPrimaryThisTick)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.Medigun)
        {
            if (input.FirePrimary)
            {
                UpdateMedicHealing(player, input.AimWorldX, input.AimWorldY);
            }
            else
            {
                player.ClearMedicHealingTarget();
            }
            return;
        }

        if (input.FirePrimary && TryStartSpyBackstab(player, input.AimWorldX, input.AimWorldY))
        {
            return;
        }

        if (player.ClassId == PlayerClass.Quote)
        {
            if (input.FirePrimary && player.TryFireQuoteBubble())
            {
                FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Pyro)
        {
            if (input.FirePrimary && !suppressPyroPrimaryThisTick)
            {
                WeaponHandler.TryFirePyroPrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Demoman && input.FirePrimary && CountOwnedMines(player.Id) >= player.PrimaryWeapon.MaxAmmo)
        {
            return;
        }

        if (!input.FirePrimary || !player.TryFirePrimaryWeapon())
        {
            return;
        }

        FirePrimaryWeapon(player, input.AimWorldX, input.AimWorldY);
    }

    private void TryHandleNetworkSecondaryFire(PlayerEntity player, PlayerInputSnapshot input, float sourceX, float sourceY)
    {
        if (player.IsTaunting)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Demoman)
        {
            DetonateOwnedMines(player.Id);
            return;
        }

        if (player.ClassId == PlayerClass.Engineer)
        {
            if (!TryDestroySentry(player))
            {
                TryBuildSentry(player);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Heavy)
        {
            player.TryStartHeavySelfHeal();
            return;
        }

        if (player.ClassId == PlayerClass.Pyro)
        {
            if (player.TryFirePyroAirblast())
            {
                TriggerPyroAirblast(player, input.AimWorldX, input.AimWorldY, input.FirePrimary);
            }

            return;
        }

        if (player.ClassId == PlayerClass.Sniper)
        {
            player.TryToggleSniperScope();
            return;
        }

        if (player.ClassId == PlayerClass.Spy)
        {
            if (!input.FirePrimary)
            {
                player.TryToggleSpyCloak();
            }
            return;
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            if (player.TryFireMedicNeedle())
            {
                FireMedicNeedle(player, input.AimWorldX, input.AimWorldY);
                return;
            }

            if (player.IsMedicUberReady && input.FirePrimary)
            {
                player.TryStartMedicUber();
            }
        }

        if (player.ClassId == PlayerClass.Quote && player.TryFireQuoteBlade())
        {
            WeaponHandler.FireQuoteBlade(player, input.AimWorldX, input.AimWorldY);
        }
    }

    private bool TryStartSpyBackstab(PlayerEntity attacker, float aimWorldX, float aimWorldY)
    {
        if (attacker.ClassId != PlayerClass.Spy || !attacker.IsSpyCloaked)
        {
            return false;
        }

        var directionDegrees = PointDirectionDegrees(attacker.X, attacker.Y, aimWorldX, aimWorldY);
        if (!attacker.TryStartSpyBackstab(directionDegrees))
        {
            return false;
        }

        SpawnStabAnimation(attacker, directionDegrees);
        return true;
    }

    private void TryActivatePendingSpyBackstab(PlayerEntity player)
    {
        if (!player.TryConsumeSpyBackstabHitboxTrigger(out var directionDegrees))
        {
            return;
        }

        SpawnStabMask(player, directionDegrees);
    }

}
