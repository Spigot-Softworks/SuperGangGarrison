using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
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

        if (isHumiliated)
        {
            input = input with
            {
                FirePrimary = false,
                FireSecondary = false,
                BuildSentry = false,
                DestroySentry = false,
            };

            // Force exit binoculars at the start of humiliation
            if (player.IsUsingBinoculars)
            {
                player.TryToggleBinoculars();
            }
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
        var specialAbilitiesEnabled = ExperimentalGameplaySettings.EnableSecondaryAbilities;
        var secondaryWeaponTriggeredPyroSelfAirblast = specialAbilitiesEnabled
            && player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility)
            && abilityPressed
            && player.CanFirePyroAirblast();
        var interactWeaponPressed = input.InteractWeapon && !previousInput.InteractWeapon;
        var allowHeldSecondaryAbility = ShouldUseHeldSecondaryAbility(player)
            || player.HasAcquiredMedigunEquipped;
        var allowHeldUtilityAbility = ShouldUseHeldUtilityAbility(player);
        var suppressPyroPrimaryThisTick = player.HasPyroWeaponEquipped
            && (secondaryAbilityPressed || secondaryWeaponTriggeredPyroSelfAirblast)
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
            ApplyExperimentalDamageRewards(burner, player, healthBeforeTick - player.Health, allowOsmosisHealOwnedSentries: false);
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

        var wasSpyBackstabAnimating = player.IsSpyBackstabAnimating;
        TryHandleNetworkPrimaryFire(player, input, primaryPressed, suppressPyroPrimaryThisTick);
        if (!wasSpyBackstabAnimating && player.IsSpyBackstabAnimating)
        {
            input = ResetMovementInput(input);
            jumpPressed = false;
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
                player.TryStartTaunt();
            }
        }

        ApplyRoomForces(player);
        var startedGrounded = player.PrepareMovement(input, Level, team, Config.FixedDeltaSeconds, out var canMove, isHumiliated);
        var jumped = player.TryJumpIfPossible(canMove, jumpPressed);
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

        if (abilityPressed || (allowHeldUtilityAbility && input.UseAbility) || (allowHeldUtilityAbility && abilityReleased))
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
        ResolveMovingPlatformLanding(player, previousBottom, input.Down);
        HandleJumpPadTriggerContactEffects(player);
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

        DispatchPassiveGameplayAbilities(player, input, previousInput, preAdvanceX, preAdvanceY);

        if (allowDebugKill && killPressed)
        {
            KillPlayer(player);
        }
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
