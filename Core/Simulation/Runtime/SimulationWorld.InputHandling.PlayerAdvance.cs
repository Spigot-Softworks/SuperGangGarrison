using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int JumpInputBufferTicks = 4;

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
        player.ObserveTauntInput(input.Taunt);
        player.ObserveCivviePogoTrickInput(input.Taunt);

        if (isHumiliated)
        {
            input = input with
            {
                FirePrimary = false,
                FireSecondary = false,
                UseAbility = false,
                SwapWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };

            // Force exit binoculars at the start of humiliation
            if (player.IsUsingBinoculars)
            {
                player.TryToggleBinoculars();
            }

            player.ForceEndSniperScopeForHumiliation();
            player.ForceEndSpyStealthForHumiliation();
        }
        
        // Disable shooting while using binoculars
        if (player.IsUsingBinoculars)
        {
            input = input with
            {
                FirePrimary = false,
                FireSecondary = false,
            };
        }

        // Disable shooting during Heavy ghost dash
        if (player.ClassId == PlayerClass.Heavy && player.IsExperimentalGhostDashing)
        {
            input = input with
            {
                FirePrimary = false,
                FireSecondary = false,
            };
        }

        player.SetAimWorldPosition(input.AimWorldX, input.AimWorldY);
        
        // Update binoculars focus position if active
        if (input.IsUsingBinoculars)
        {
            player.SetBinocularsFocusPosition(input.BinocularsFocusX, input.BinocularsFocusY);
        }

        var jumpPressed = input.Up && !previousInput.Up;
        var dropPressed = input.DropIntel && !previousInput.DropIntel;
        var buildPressed = input.BuildSentry && !previousInput.BuildSentry;
        var destroyPressed = input.DestroySentry && !previousInput.DestroySentry;
        var tauntPressed = input.Taunt && !previousInput.Taunt;
        var killPressed = input.DebugKill && !previousInput.DebugKill;
        var primaryPressed = input.FirePrimary && !previousInput.FirePrimary;
        var secondaryAbilityPressed = input.FireSecondary && !previousInput.FireSecondary;
        var abilityPressed = input.UseAbility && !previousInput.UseAbility;
        var abilityReleased = !input.UseAbility && previousInput.UseAbility;
        var swapWeaponPressed = input.SwapWeapon && !previousInput.SwapWeapon;
        var interactWeaponPressed = input.InteractWeapon && !previousInput.InteractWeapon;
        if (jumpPressed)
        {
            StartJumpInputBuffer(player);
        }
        else if (!input.Up)
        {
            ClearJumpInputBuffer(player);
        }

        var allowHeldSecondaryAbility = ShouldUseHeldSecondaryAbility(player)
            || player.HasAcquiredMedigunEquipped;
        var allowHeldUtilityAbility = ShouldUseHeldUtilityAbility(player);
        var suppressPyroPrimaryThisTick = player.HasPyroWeaponEquipped
            && secondaryAbilityPressed
            && player.CanFirePyroAirblast();

        player.ObserveSpySuperjumpAbilityInput(input.UseAbility);

        player.SyncCivvieUmbrellaSecondaryInput(input.FireSecondary);
        player.SyncCivviePogoSuperJumpInput(input.Up);

        var healthBeforeTick = player.Health;
        var afterburn = player.AdvanceTickState(input, Config.FixedDeltaSeconds);
        var afterburnDamageCommitted = false;
        if (healthBeforeTick > player.Health)
        {
            var burnedByPlayerId = afterburn.BurnedByPlayerId ?? player.BurnedByPlayerId;
            var burner = burnedByPlayerId.HasValue
                ? FindPlayerById(burnedByPlayerId.Value)
                : null;
            var afterburnDamage = healthBeforeTick - player.Health;
            if (burner is not null
                && TryAbsorbCivvieUmbrellaDamage(
                    player,
                    burner,
                    DamageEventFlags.None,
                    burner.X,
                    burner.Y))
            {
                player.ForceSetHealth(healthBeforeTick);
            }
            else if (!TryAbsorbPracticeCombatDummyTickDamage(player, afterburnDamage, burner))
            {
                RegisterDamageEvent(
                    burner,
                    DamageTargetKind.Player,
                    player.Id,
                    player.X,
                    player.Y,
                    afterburnDamage,
                    afterburn.IsFatal,
                    playerTarget: player,
                    flags: DamageEventFlags.AfterburnTick);
                ApplyExperimentalDamageRewards(burner, player, afterburnDamage, allowOsmosisHealOwnedSentries: false);
                afterburnDamageCommitted = true;
            }
        }

        if (afterburnDamageCommitted && (afterburn.IsFatal || player.Health <= 0))
        {
            var burnedByPlayerId = afterburn.BurnedByPlayerId ?? player.BurnedByPlayerId;
            var burner = burnedByPlayerId.HasValue
                ? FindPlayerById(burnedByPlayerId.Value)
                : null;
            KillPlayer(player, killer: burner, weaponSpriteName: "FlameKL");
            return;
        }

        TryApplyPendingCivvieTauntHeal(player);

        if (isHumiliated)
        {
            player.ForceEndSniperScopeForHumiliation();
            player.ForceEndSpyStealthForHumiliation();
        }

        var wasSpyBackstabAnimating = player.IsSpyBackstabAnimating;
        TryHandleNetworkPrimaryFire(player, input, primaryPressed, suppressPyroPrimaryThisTick);
        if (!wasSpyBackstabAnimating && player.IsSpyBackstabAnimating)
        {
            input = ResetMovementInput(input);
            jumpPressed = false;
            ClearJumpInputBuffer(player);
        }

        if (tauntPressed)
        {
            var tauntAbilityResult = TryDispatchGameplayAbility(
                player,
                input,
                previousInput,
                GameplayAbilityInputPhase.Pressed,
                GameplayAbilityConstants.TauntCategory,
                preAdvanceX,
                preAdvanceY);
            if (!tauntAbilityResult.ConsumedInput)
            {
                TryStartTauntWithCivvieHeal(player);
            }
        }

        ApplyRoomForces(player);
        var cancelledSpySuperjumpChargeWithJump = TryCancelSpySuperjumpChargeFromJumpInput(player, jumpPressed, input.UseAbility);
        if (cancelledSpySuperjumpChargeWithJump)
        {
            jumpPressed = false;
            input = input with { Up = false };
            ClearJumpInputBuffer(player);
        }

        var startedGrounded = player.PrepareMovement(input, Level, team, Config.FixedDeltaSeconds, out var canMove, isHumiliated);
        var effectiveJumpPressed = jumpPressed || HasBufferedJumpInput(player);
        var jumped = player.TryJumpIfPossible(canMove, effectiveJumpPressed);
        AdvanceJumpInputBufferAfterAttempt(player, input.Up, jumped);
        var emitWallspinDust = player.IsAlive && player.IsPerformingSourceSpinjump(Level);
        if (jumped)
        {
            RegisterWorldSoundEvent("JumpSnd", player.X, player.Y);
            TryApplyJumpPadJumpBoostFromPlayerJump(player, jumped);
        }

        var secondaryAbilityConsumedInput = false;
        if (player.ClassId == PlayerClass.Medic)
        {
            if (input.FireSecondary)
            {
                var secondaryResult = TryHandleNetworkSecondaryAbility(
                    player,
                    input,
                    previousInput,
                    GameplayAbilityInputPhase.Held,
                    preAdvanceX,
                    preAdvanceY);
                secondaryAbilityConsumedInput = secondaryResult.ConsumedInput;
            }
        }
        else if ((allowHeldSecondaryAbility && input.FireSecondary) || (!allowHeldSecondaryAbility && secondaryAbilityPressed))
        {
            var secondaryResult = TryHandleNetworkSecondaryAbility(
                player,
                input,
                previousInput,
                allowHeldSecondaryAbility ? GameplayAbilityInputPhase.Held : GameplayAbilityInputPhase.Pressed,
                preAdvanceX,
                preAdvanceY);
            secondaryAbilityConsumedInput = secondaryResult.ConsumedInput;
        }

        var swappedWeaponThisTick = false;
        if (swapWeaponPressed && !secondaryAbilityConsumedInput)
        {
            swappedWeaponThisTick = TryHandleNetworkWeaponSwap(player);
        }

        var utilityInputActive = abilityPressed
            || (allowHeldUtilityAbility && input.UseAbility)
            || (allowHeldUtilityAbility && abilityReleased);
        if (!cancelledSpySuperjumpChargeWithJump
            && utilityInputActive
            && !input.FireSecondary)
        {
            var utilityPhase = allowHeldUtilityAbility
                ? (abilityReleased ? GameplayAbilityInputPhase.Released : GameplayAbilityInputPhase.Held)
                : GameplayAbilityInputPhase.Pressed;
            var abilityInputConsumedByWeaponSwap = TryHandleNetworkAbilityInput(
                player,
                input,
                previousInput,
                utilityPhase,
                swappedWeaponThisTick);
            _ = abilityInputConsumedByWeaponSwap;
        }

        if (interactWeaponPressed)
        {
            TryHandleNetworkWeaponInteraction(player);
        }

        if (emitWallspinDust)
        {
            RegisterWallspinDustEffect(player);
        }

        AdvancePendingRocketsForOwner(player.Id);
        var previousBottom = preAdvanceY + player.CollisionBottomOffset;
        player.CompleteMovement(Level, team, Config.FixedDeltaSeconds, startedGrounded, jumped, input.Down);
        if (player.TryConsumeCivviePogoSuperJumpSoundRequest(out var pogoJumpSoundX, out var pogoJumpSoundY))
        {
            RegisterWorldSoundEvent("JumpSnd", pogoJumpSoundX, pogoJumpSoundY);
        }

        ResolveMovingPlatformLanding(player, previousBottom, input.Down);
        HandleJumpPadTriggerContactEffects(player);
        TryRegisterIntelTrailEffect(player);
        TryRegisterCivvieMoneyTrail(player);
        UpdateSpawnRoomState(player);
        TryActivatePendingSpyBackstab(player);

        if (dropPressed)
        {
            TryDropCarriedIntel(player);
        }

        if (destroyPressed)
        {
            TryDestroySentry(player);
        }
        else if (buildPressed)
        {
            TryBuildSentry(player);
        }

        ApplyHealingCabinets(player);
        ApplyRoomHazards(player);
        ApplyTeleportZones(player);
        if (!player.IsAlive)
        {
            return;
        }

        DispatchPassiveGameplayAbilities(player, input, previousInput, preAdvanceX, preAdvanceY);

        if (allowDebugKill && killPressed)
        {
            KillPlayer(player);
        }
    }

    private static bool TryCancelSpySuperjumpChargeFromJumpInput(PlayerEntity player, bool jumpPressed, bool useAbilityHeld)
    {
        if (!jumpPressed
            || !useAbilityHeld
            || player.ClassId != PlayerClass.Spy
            || player.SpySuperjumpChargeTicks <= 0)
        {
            return false;
        }

        player.CancelSpySuperjumpCharge(blockRestartUntilAbilityRelease: true);
        return true;
    }

    private void StartJumpInputBuffer(PlayerEntity player)
    {
        _jumpInputBufferTicksByPlayerId[player.Id] = JumpInputBufferTicks;
    }

    private bool HasBufferedJumpInput(PlayerEntity player)
    {
        return _jumpInputBufferTicksByPlayerId.TryGetValue(player.Id, out var ticksRemaining)
            && ticksRemaining > 0;
    }

    private void AdvanceJumpInputBufferAfterAttempt(PlayerEntity player, bool jumpHeld, bool jumped)
    {
        if (jumped || !jumpHeld)
        {
            ClearJumpInputBuffer(player);
            return;
        }

        if (!_jumpInputBufferTicksByPlayerId.TryGetValue(player.Id, out var ticksRemaining))
        {
            return;
        }

        ticksRemaining -= 1;
        if (ticksRemaining <= 0)
        {
            ClearJumpInputBuffer(player);
            return;
        }

        _jumpInputBufferTicksByPlayerId[player.Id] = ticksRemaining;
    }

    private void ClearJumpInputBuffer(PlayerEntity player)
    {
        _jumpInputBufferTicksByPlayerId.Remove(player.Id);
    }

    private static PlayerInputSnapshot ResetMovementInput(PlayerInputSnapshot input)
    {
        return input with
        {
            Left = false,
            Right = false,
            Up = false,
            Down = false,
        };
    }
}
