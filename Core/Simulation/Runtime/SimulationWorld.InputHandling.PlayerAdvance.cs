using OpenGarrison.GameplayModding;

using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int JumpInputBufferTicks = 4;
    private static readonly bool SlowPlayerPhaseTracingEnabled =
        Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_TRACE") is "1" or "true" or "TRUE";
    private static readonly double SlowPlayerPhaseThresholdMilliseconds = ResolveSlowPlayerPhaseThresholdMilliseconds();
    private static readonly string? SlowPlayerPhaseTracePath = SlowPlayerPhaseTracingEnabled
        ? RuntimePaths.GetLogPath($"simulation-player-phases-{DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)}.log")
        : null;
    private static readonly object SlowPlayerPhaseTraceSync = new();

    private void AdvanceAlivePlayerWithInput(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        PlayerTeam team,
        bool allowDebugKill)
    {
        var preAdvanceX = player.X;
        var preAdvanceY = player.Y;
        var phaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        var advanceTickStateMilliseconds = 0d;
        var primaryFireMilliseconds = 0d;
        var prepareMovementMilliseconds = 0d;
        var completeMovementMilliseconds = 0d;
        var postMovementMilliseconds = 0d;
        var postMovementContactEffectsMilliseconds = 0d;
        var postMovementObjectiveEffectsMilliseconds = 0d;
        var postMovementInventoryEffectsMilliseconds = 0d;
        var postMovementPassiveAbilitiesMilliseconds = 0d;
        if (player.IsServerInputSuppressed)
        {
            input = input with
            {
                Left = false,
                Right = false,
                Up = false,
                Down = false,
                BuildSentry = false,
                BuildDispenser = false,
                DestroySentry = false,
                DestroyDispenser = false,
                Taunt = false,
                FirePrimary = false,
                FireSecondary = false,
                DebugKill = false,
                DropIntel = false,
                UseAbility = false,
                InteractWeapon = false,
                SwapWeapon = false,
                ToggleSecondaryWeapon = false,
            };
        }
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
                ToggleSecondaryWeapon = false,
                BuildSentry = false,
                BuildDispenser = false,
                DestroySentry = false,
                DestroyDispenser = false,
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
        var buildDispenserPressed = input.BuildDispenser && !previousInput.BuildDispenser;
        var destroyPressed = input.DestroySentry && !previousInput.DestroySentry;
        var destroyDispenserPressed = input.DestroyDispenser && !previousInput.DestroyDispenser;
        var tauntPressed = input.Taunt && !previousInput.Taunt;
        var killPressed = input.DebugKill && !previousInput.DebugKill;
        var primaryPressed = input.FirePrimary && !previousInput.FirePrimary;
        var secondaryAbilityPressed = input.FireSecondary && !previousInput.FireSecondary;
        var secondaryAbilityReleased = !input.FireSecondary && previousInput.FireSecondary;
        var abilityPressed = input.UseAbility && !previousInput.UseAbility;
        var abilityReleased = !input.UseAbility && previousInput.UseAbility;
        var swapWeaponPressed = input.SwapWeapon && !previousInput.SwapWeapon;
        var toggleSecondaryWeaponPressed = input.ToggleSecondaryWeapon && !previousInput.ToggleSecondaryWeapon;
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
        var subphaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        var afterburn = player.AdvanceTickState(input, Config.FixedDeltaSeconds);
        advanceTickStateMilliseconds = ElapsedMilliseconds(subphaseStartTimestamp);
        if (TryCompleteExpiredLastToDieSpyAfterlife(player))
        {
            return;
        }

        while (player.TryTakeDueLastToDieSniperVolleyArrow(out var volleyArrow))
        {
            if (!player.IsAlive || player.ClassId != PlayerClass.Sniper || !player.IsSniperBowEquipped)
            {
                player.CancelLastToDieSniperVolley();
                break;
            }

            WeaponHandler.FireQueuedLastToDieSniperBowArrow(player, volleyArrow);
        }

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
                ApplyLastToDieDamageRewards(
                    burner,
                    player,
                    afterburnDamage,
                    PlayerDamageTraits.Periodic | PlayerDamageTraits.Fire);
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

        if (player.IsServerFrozen)
        {
            return;
        }

        TryApplyPendingCivvieTauntHeal(player);

        if (isHumiliated)
        {
            player.ForceEndSniperScopeForHumiliation();
            player.ForceEndSpyStealthForHumiliation();
        }

        var wasSpyBackstabAnimating = player.IsSpyBackstabAnimating;
        subphaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        TryHandleNetworkPrimaryFire(player, input, previousInput, primaryPressed, suppressPyroPrimaryThisTick);
        primaryFireMilliseconds = ElapsedMilliseconds(subphaseStartTimestamp);
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

        if (ApplyRoomForces(player, jumpPressed))
        {
            jumpPressed = false;
            input = input with { Up = false };
            ClearJumpInputBuffer(player);
        }

        var cancelledSpySuperjumpChargeWithJump = TryCancelSpySuperjumpChargeFromJumpInput(player, jumpPressed, input.UseAbility);
        if (cancelledSpySuperjumpChargeWithJump)
        {
            jumpPressed = false;
            input = input with { Up = false };
            ClearJumpInputBuffer(player);
        }

        subphaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        var startedGrounded = player.PrepareMovement(
            input,
            Level,
            team,
            Config.FixedDeltaSeconds,
            out var canMove,
            isHumiliated,
            HasLandedArrowGroundSupport(player, input.Down));
        prepareMovementMilliseconds = ElapsedMilliseconds(subphaseStartTimestamp);
        var effectiveJumpPressed = jumpPressed || HasBufferedJumpInput(player);
        var jumped = player.TryJumpIfPossible(canMove, effectiveJumpPressed);
        AdvanceJumpInputBufferAfterAttempt(player, input.Up, jumped);
        var emitWallspinDust = player.IsAlive && player.IsPerformingSourceSpinjump(Level);
        if (jumped)
        {
            RegisterWorldSoundEvent("JumpSnd", player.X, player.Y, player.Id);
            TryApplyJumpPadJumpBoostFromPlayerJump(player, jumped);
        }

        var secondaryAbilityConsumedInput = false;
        if (secondaryAbilityReleased
            && player.TryReleaseLastToDieProfessionalFireChord(out var shouldDecloakFromProfessionalChord))
        {
            if (shouldDecloakFromProfessionalChord)
            {
                // The chord deliberately defers the normal M2 toggle until
                // release. Complete that toggle even while cloak is still
                // fading in; TryToggleSpyCloak rejects that transition.
                player.ForceDecloak();
            }

            secondaryAbilityConsumedInput = true;
        }
        else if (secondaryAbilityPressed
            && player.IsSniperBowEquipped
            && (player.LastToDieSniperProfile.ExplosiveTipEnabled
                || HasOwnedLastToDieSniperExplosiveArrow(player)))
        {
            secondaryAbilityConsumedInput = true;
            _ = DetonateOwnedLastToDieSniperArrows(player);
        }
        else if (player.ClassId == PlayerClass.Medic)
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

        if (toggleSecondaryWeaponPressed
            && !player.IsTaunting
            && !player.IsExperimentalCryoFrozen)
        {
            _ = TryHandleSecondaryWeaponToggle(player);
        }
        else if (swapWeaponPressed && !secondaryAbilityConsumedInput)
        {
            _ = TryHandleNetworkWeaponSwap(player);
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
            _ = TryHandleNetworkAbilityInput(
                player,
                input,
                previousInput,
                utilityPhase);
        }

        if (interactWeaponPressed)
        {
            var ghostConsumedInput = !isHumiliated
                && player.TryActivateLastToDieSniperGhostCloak();
            var infiltrateConsumedInput = !ghostConsumedInput
                && !isHumiliated
                && player.TryStartLastToDieSpyInfiltrate(Config.TicksPerSecond);
            if (!ghostConsumedInput && !infiltrateConsumedInput)
            {
                TryHandleNetworkWeaponInteraction(player);
            }
        }

        if (emitWallspinDust)
        {
            RegisterWallspinDustEffect(player);
        }

        subphaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        AdvancePendingRocketsForOwner(player.Id);
        var previousBottom = preAdvanceY + player.CollisionBottomOffset;
        player.CompleteMovement(Level, team, Config.FixedDeltaSeconds, startedGrounded, jumped, input.Down);
        completeMovementMilliseconds = ElapsedMilliseconds(subphaseStartTimestamp);
        if (player.TryConsumeCivviePogoSuperJumpSoundRequest(out var pogoJumpSoundX, out var pogoJumpSoundY))
        {
            RegisterWorldSoundEvent("JumpSnd", pogoJumpSoundX, pogoJumpSoundY, player.Id);
        }

        var postMovementSubphaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        ResolveMovingPlatformLanding(player, previousBottom, input.Down);
        ResolveLandedArrowLanding(player, previousBottom, input.Down);
        HandleJumpPadTriggerContactEffects(player);
        TryRegisterIntelTrailEffect(player);
        TryRegisterCivvieMoneyTrail(player);
        postMovementContactEffectsMilliseconds = ElapsedMilliseconds(postMovementSubphaseStartTimestamp);

        postMovementSubphaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        UpdateSpawnRoomState(player);
        TryActivatePendingSpyBackstab(player);
        postMovementObjectiveEffectsMilliseconds = ElapsedMilliseconds(postMovementSubphaseStartTimestamp);

        postMovementSubphaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        if (dropPressed)
        {
            TryDropCarriedIntel(player);
        }

        if (destroyPressed)
        {
            TryDestroySentry(player);
        }
        else if (destroyDispenserPressed)
        {
            TryDestroyDispenser(player);
        }
        else if (buildDispenserPressed)
        {
            TryBuildDispenser(player);
        }
        else if (buildPressed)
        {
            TryBuildSentry(player);
        }

        ApplyHealingCabinets(player);
        ApplyRoomHazards(player);
        ApplyTeleportZones(player);
        postMovementInventoryEffectsMilliseconds = ElapsedMilliseconds(postMovementSubphaseStartTimestamp);
        if (!player.IsAlive)
        {
            return;
        }

        postMovementSubphaseStartTimestamp = SlowPlayerPhaseTracingEnabled ? Stopwatch.GetTimestamp() : 0L;
        DispatchPassiveGameplayAbilities(player, input, previousInput, preAdvanceX, preAdvanceY);
        postMovementPassiveAbilitiesMilliseconds = ElapsedMilliseconds(postMovementSubphaseStartTimestamp);

        postMovementMilliseconds = ElapsedMilliseconds(phaseStartTimestamp)
            - advanceTickStateMilliseconds
            - primaryFireMilliseconds
            - prepareMovementMilliseconds
            - completeMovementMilliseconds;
        TraceSlowPlayerPhases(
            player,
            phaseStartTimestamp,
            advanceTickStateMilliseconds,
            primaryFireMilliseconds,
            prepareMovementMilliseconds,
            completeMovementMilliseconds,
            postMovementMilliseconds,
            postMovementContactEffectsMilliseconds,
            postMovementObjectiveEffectsMilliseconds,
            postMovementInventoryEffectsMilliseconds,
            postMovementPassiveAbilitiesMilliseconds,
            player.MovementCollisionContactIterations,
            player.MovementCollisionOccupyChecks,
            player.MovementCollisionResolutionIterations);

        if (allowDebugKill && killPressed)
        {
            KillPlayer(player);
        }
    }

    private static double ResolveSlowPlayerPhaseThresholdMilliseconds()
    {
        var configured = Environment.GetEnvironmentVariable("OG_CLIENT_PERF_SIM_PLAYER_PHASE_TRACE_THRESHOLD_MS");
        return double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            ? Math.Max(0d, threshold)
            : 10d;
    }

    private static double ElapsedMilliseconds(long startTimestamp)
    {
        return startTimestamp == 0L
            ? 0d
            : (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
    }

    private void TraceSlowPlayerPhases(
        PlayerEntity player,
        long startTimestamp,
        double advanceTickStateMilliseconds,
        double primaryFireMilliseconds,
        double prepareMovementMilliseconds,
        double completeMovementMilliseconds,
        double postMovementMilliseconds,
        double postMovementContactEffectsMilliseconds,
        double postMovementObjectiveEffectsMilliseconds,
        double postMovementInventoryEffectsMilliseconds,
        double postMovementPassiveAbilitiesMilliseconds,
        int movementCollisionContactIterations,
        int movementCollisionOccupyChecks,
        int movementCollisionResolutionIterations)
    {
        if (!SlowPlayerPhaseTracingEnabled
            || startTimestamp == 0L
            || string.IsNullOrWhiteSpace(SlowPlayerPhaseTracePath))
        {
            return;
        }

        var totalMilliseconds = ElapsedMilliseconds(startTimestamp);
        if (totalMilliseconds < SlowPlayerPhaseThresholdMilliseconds)
        {
            return;
        }

        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:O} frame={Frame} slot={FindNetworkSlotForPlayer(player)} class={player.ClassId} totalMs={totalMilliseconds:0.0} advanceTickStateMs={advanceTickStateMilliseconds:0.0} primaryFireMs={primaryFireMilliseconds:0.0} prepareMovementMs={prepareMovementMilliseconds:0.0} completeMovementMs={completeMovementMilliseconds:0.0} postMovementMs={postMovementMilliseconds:0.0} postContactMs={postMovementContactEffectsMilliseconds:0.0} postObjectiveMs={postMovementObjectiveEffectsMilliseconds:0.0} postInventoryMs={postMovementInventoryEffectsMilliseconds:0.0} postPassiveMs={postMovementPassiveAbilitiesMilliseconds:0.0} collisionContactIterations={movementCollisionContactIterations} collisionOccupyChecks={movementCollisionOccupyChecks} collisionResolutionIterations={movementCollisionResolutionIterations}{Environment.NewLine}");
        lock (SlowPlayerPhaseTraceSync)
        {
            File.AppendAllText(SlowPlayerPhaseTracePath, line);
        }
    }

    private byte FindNetworkSlotForPlayer(PlayerEntity player)
    {
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            var slot = NetworkPlayerSlots[index];
            if (TryGetNetworkPlayer(slot, out var networkPlayer) && networkPlayer.Id == player.Id)
            {
                return slot;
            }
        }

        return byte.MaxValue;
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
