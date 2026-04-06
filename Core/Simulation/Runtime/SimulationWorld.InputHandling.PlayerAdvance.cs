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

        var jumpPressed = input.Up && !previousInput.Up;
        var dropPressed = input.DropIntel && !previousInput.DropIntel;
        var buildPressed = input.BuildSentry && !previousInput.BuildSentry;
        var destroyPressed = input.DestroySentry && !previousInput.DestroySentry;
        var tauntPressed = input.Taunt && !previousInput.Taunt;
        var killPressed = input.DebugKill && !previousInput.DebugKill;
        var secondaryAbilityPressed = input.FireSecondary && !previousInput.FireSecondary;
        var secondaryWeaponPressed = input.FireSecondaryWeapon && !previousInput.FireSecondaryWeapon;
        var interactWeaponPressed = input.InteractWeapon && !previousInput.InteractWeapon;
        var allowHeldSecondaryAbility = ShouldUseHeldSecondaryAbility(player)
            || player.HasAcquiredMedigunEquipped;
        var suppressPyroPrimaryThisTick = player.HasPyroWeaponEquipped
            && secondaryAbilityPressed
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

        TryHandleNetworkPrimaryFire(player, input, suppressPyroPrimaryThisTick);

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

        if (secondaryWeaponPressed && !input.FirePrimary)
        {
            TryHandleNetworkSecondaryWeaponFire(player, input);
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
}
