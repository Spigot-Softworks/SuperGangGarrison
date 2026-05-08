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

        player.SetAimWorldPosition(input.AimWorldX, input.AimWorldY);

        var jumpPressed = input.Up && !previousInput.Up;
        var dropPressed = input.DropIntel && !previousInput.DropIntel;
        var buildPressed = input.BuildSentry && !previousInput.BuildSentry;
        var destroyPressed = input.DestroySentry && !previousInput.DestroySentry;
        var tauntPressed = input.Taunt && !previousInput.Taunt;
        var killPressed = input.DebugKill && !previousInput.DebugKill;
        var primaryPressed = input.FirePrimary && !previousInput.FirePrimary;
        var secondaryAbilityPressed = input.FireSecondary && !previousInput.FireSecondary;
        var abilityPressed = input.UseAbility && !previousInput.UseAbility;
        var secondaryWeaponTriggeredPyroSelfAirblast = player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility)
            && abilityPressed
            && player.CanFirePyroAirblast();
        var interactWeaponPressed = input.InteractWeapon && !previousInput.InteractWeapon;
        var allowHeldSecondaryAbility = ShouldUseHeldSecondaryAbility(player)
            || player.HasAcquiredMedigunEquipped;
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
            ApplyExperimentalDamageRewards(burner, player, healthBeforeTick - player.Health);
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
            if (!TryHandleExperimentalRageActivation(player))
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

        if (player.ClassId == PlayerClass.Medic)
        {
            if (input.FireSecondary)
            {
                TryHandleNetworkSecondaryAbility(player, input, preAdvanceX, preAdvanceY);
            }
        }
        else if ((allowHeldSecondaryAbility && input.FireSecondary) || (!allowHeldSecondaryAbility && secondaryAbilityPressed))
        {
            TryHandleNetworkSecondaryAbility(player, input, preAdvanceX, preAdvanceY);
        }

        if (abilityPressed)
        {
            TryHandleNetworkAbilityInput(player, input);
            
            // Start charging spy superjump when Space is pressed
            if (player.ClassId == PlayerClass.Spy)
            {
                var directionDegrees = PointDirectionDegrees(player.X, player.Y, input.AimWorldX, input.AimWorldY);
                player.TryStartSpySuperjumpCharge(directionDegrees, input.Left, input.Right, input.Up, input.Down);
            }
        }
        // Also start charging if space is being held and not already charging (handles holding space while landing)
        else if (player.ClassId == PlayerClass.Spy && input.UseAbility && player.SpySuperjumpChargeTicks == 0 && !player.IsSpySuperjumping)
        {
            var directionDegrees = PointDirectionDegrees(player.X, player.Y, input.AimWorldX, input.AimWorldY);
            player.TryStartSpySuperjumpCharge(directionDegrees, input.Left, input.Right, input.Up, input.Down);
        }

        // Handle spy superjump charging and cancellation
        if (player.ClassId == PlayerClass.Spy && player.SpySuperjumpChargeTicks > 0)
        {
            // Cancel if NEW movement buttons are pressed (not ones held when charging started)
            var heldButtons = player.SpySuperjumpChargeStartMovementButtons;
            var leftWasHeld = (heldButtons & 0x01) != 0;
            var rightWasHeld = (heldButtons & 0x02) != 0;
            var upWasHeld = (heldButtons & 0x04) != 0;
            var downWasHeld = (heldButtons & 0x08) != 0;
            
            var newButtonPressed = (input.Left && !leftWasHeld) 
                || (input.Right && !rightWasHeld) 
                || (input.Up && !upWasHeld) 
                || (input.Down && !downWasHeld);
            
            if (newButtonPressed)
            {
                player.CancelSpySuperjumpCharge();
            }
            // Cancel if backstab starts or intel is picked up
            else if (player.IsSpyBackstabAnimating || player.IsCarryingIntel)
            {
                player.CancelSpySuperjumpCharge();
            }
            // Continue charging while Space is held
            else if (input.UseAbility)
            {
                var directionDegrees = PointDirectionDegrees(player.X, player.Y, input.AimWorldX, input.AimWorldY);
                player.IncrementSpySuperjumpCharge(directionDegrees);
            }
            // Release when Space is released (jump only executes if grounded)
            else if (!input.UseAbility && player.SpySuperjumpChargeTicks > 0)
            {
                if (player.TryReleaseSpySuperjump(out var velocityX, out var velocityY))
                {
                    player.ApplyVelocityImpulse(velocityX, velocityY);
                    RegisterWorldSoundEvent("JumpSnd", player.X, player.Y);
                }
            }
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
        player.CompleteMovement(Level, team, Config.FixedDeltaSeconds, startedGrounded, jumped, input.Down);
        HandleJumpPadTriggerTouch(player);
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

        ApplyExperimentalPassivePlayerEffects(player);

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
