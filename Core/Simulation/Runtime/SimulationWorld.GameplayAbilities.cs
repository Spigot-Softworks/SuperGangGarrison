using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public readonly record struct WorldGameplayAbilityEvent(
    long Frame,
    int PlayerId,
    PlayerClass ClassId,
    PlayerTeam Team,
    string ItemId,
    string BehaviorId,
    string AbilityCategory,
    string Activation,
    string ExecutorId,
    GameplayAbilityInputPhase Phase,
    IReadOnlyList<string> Tags,
    bool Handled,
    bool ConsumedInput,
    bool Cancelled);

public sealed partial class SimulationWorld
{
    private readonly List<WorldGameplayAbilityEvent> _pendingGameplayAbilityEvents = [];

    public Func<WorldGameplayAbilityEvent, bool>? GameplayAbilityInputInterceptor { get; set; }

    public IReadOnlyList<WorldGameplayAbilityEvent> PendingGameplayAbilityEvents => _pendingGameplayAbilityEvents;

    public IReadOnlyList<WorldGameplayAbilityEvent> DrainPendingGameplayAbilityEvents()
    {
        if (_pendingGameplayAbilityEvents.Count == 0)
        {
            return Array.Empty<WorldGameplayAbilityEvent>();
        }

        var events = _pendingGameplayAbilityEvents.ToArray();
        _pendingGameplayAbilityEvents.Clear();
        return events;
    }

    private GameplayAbilityResult TryDispatchGameplayAbility(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        GameplayAbilityInputPhase phase,
        string category,
        float sourceX,
        float sourceY)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        foreach (var item in ResolveGameplayAbilityItems(player, category))
        {
            if (item.Ability is not { } ability
                || !AbilityCategoryMatches(ability, category)
                || !AbilityActivationMatches(ability, phase)
                || IsGameplayAbilityBlockedBySpecialAbilitiesSetting(ability))
            {
                continue;
            }

            var result = TryDispatchGameplayAbilityItem(
                player,
                item,
                ability,
                input,
                previousInput,
                phase,
                sourceX,
                sourceY);
            return result;
        }

        return GameplayAbilityResult.Ignored;
    }

    private void DispatchPassiveGameplayAbilities(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        float sourceX,
        float sourceY)
    {
        if (!ExperimentalGameplaySettings.EnableSecondaryAbilities)
        {
            return;
        }

        foreach (var item in ResolveAllPlayerGameplayAbilityItems(player))
        {
            if (item.Ability is not { } ability
                || !AbilityActivationMatches(ability, GameplayAbilityInputPhase.PassiveTick))
            {
                continue;
            }

            TryDispatchGameplayAbilityItem(
                player,
                item,
                ability,
                input,
                previousInput,
                GameplayAbilityInputPhase.PassiveTick,
                sourceX,
                sourceY);
        }
    }

    private GameplayAbilityResult TryDispatchGameplayAbilityItem(
        PlayerEntity player,
        GameplayItemDefinition item,
        GameplayAbilityDefinition ability,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        GameplayAbilityInputPhase phase,
        float sourceX,
        float sourceY)
    {
        var emitAbilityEvent = phase != GameplayAbilityInputPhase.PassiveTick;
        var baseEvent = CreateGameplayAbilityEvent(
            player,
            item,
            ability,
            phase,
            handled: false,
            consumedInput: false,
            cancelled: false);
        if (emitAbilityEvent && GameplayAbilityInputInterceptor?.Invoke(baseEvent) == true)
        {
            _pendingGameplayAbilityEvents.Add(baseEvent with
            {
                ConsumedInput = true,
                Cancelled = true,
            });
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        if (!CanDispatchAbilityForCurrentPlayerState(player, ability))
        {
            if (emitAbilityEvent)
            {
                _pendingGameplayAbilityEvents.Add(baseEvent);
            }

            return GameplayAbilityResult.Ignored;
        }

        if (!CharacterClassCatalog.RuntimeRegistry.TryGetGameplayAbilityExecutor(ability.ExecutorId, out var executor))
        {
            if (emitAbilityEvent)
            {
                _pendingGameplayAbilityEvents.Add(baseEvent);
            }

            return GameplayAbilityResult.Ignored;
        }

        var result = executor.Handle(new GameplayAbilityContext
        {
            World = this,
            Player = player,
            Item = item,
            Ability = ability,
            Phase = phase,
            Input = input,
            PreviousInput = previousInput,
            SourceX = sourceX,
            SourceY = sourceY,
        });
        if (emitAbilityEvent)
        {
            _pendingGameplayAbilityEvents.Add(baseEvent with
            {
                Handled = result.Handled,
                ConsumedInput = result.ConsumedInput,
            });
        }

        return result;
    }

    private static bool CanDispatchAbilityForCurrentPlayerState(PlayerEntity player, GameplayAbilityDefinition ability)
    {
        if (string.Equals(ability.Activation, GameplayAbilityConstants.PassiveTickActivation, StringComparison.Ordinal))
        {
            return true;
        }

        if (player.IsExperimentalCryoFrozen)
        {
            return false;
        }

        return !player.IsTaunting || ability.Tags.Contains("allowed_while_taunting", StringComparer.Ordinal);
    }

    private bool IsGameplayAbilityBlockedBySpecialAbilitiesSetting(GameplayAbilityDefinition ability)
    {
        if (ExperimentalGameplaySettings.EnableSecondaryAbilities)
        {
            return false;
        }

        return !IsCoreSecondaryInputAbility(ability);
    }

    private static bool IsCoreSecondaryInputAbility(GameplayAbilityDefinition ability)
    {
        return string.Equals(ability.Category, GameplayAbilityConstants.SecondaryCategory, StringComparison.Ordinal)
            && ability.Tags.Contains(GameplayAbilityConstants.CoreSecondaryInputTag, StringComparer.Ordinal);
    }

    private static bool AbilityCategoryMatches(GameplayAbilityDefinition ability, string category)
    {
        return string.Equals(ability.Category, category, StringComparison.Ordinal);
    }

    private static bool AbilityActivationMatches(GameplayAbilityDefinition ability, GameplayAbilityInputPhase phase)
    {
        var activation = ToAbilityActivation(phase);
        return string.Equals(ability.Activation, activation, StringComparison.Ordinal)
            || (phase == GameplayAbilityInputPhase.Pressed
                && string.Equals(ability.Activation, GameplayAbilityConstants.HeldActivation, StringComparison.Ordinal))
            || (phase == GameplayAbilityInputPhase.Released
                && string.Equals(ability.Activation, GameplayAbilityConstants.HeldActivation, StringComparison.Ordinal));
    }

    private static string ToAbilityActivation(GameplayAbilityInputPhase phase)
    {
        return phase switch
        {
            GameplayAbilityInputPhase.Held => GameplayAbilityConstants.HeldActivation,
            GameplayAbilityInputPhase.Released => GameplayAbilityConstants.ReleasedActivation,
            GameplayAbilityInputPhase.PassiveTick => GameplayAbilityConstants.PassiveTickActivation,
            _ => GameplayAbilityConstants.PressedActivation,
        };
    }

    private IEnumerable<GameplayItemDefinition> ResolveGameplayAbilityItems(PlayerEntity player, string category)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (string.Equals(category, GameplayAbilityConstants.UtilityCategory, StringComparison.Ordinal))
        {
            if (runtimeRegistry.TryGetGameplayAbilityDefinition(player.GameplayLoadoutState.UtilityItemId, out var utilityItem, out _))
            {
                yield return utilityItem;
            }

            yield break;
        }

        if (string.Equals(category, GameplayAbilityConstants.SecondaryCategory, StringComparison.Ordinal))
        {
            if (TryResolveSecondaryGameplayAbilityItem(player, out var item))
            {
                yield return item;
            }

            yield break;
        }

        foreach (var item in ResolveAllPlayerGameplayAbilityItems(player))
        {
            yield return item;
        }
    }

    private static IEnumerable<GameplayItemDefinition> ResolveAllPlayerGameplayAbilityItems(PlayerEntity player)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var itemId in new[]
        {
            player.GameplayLoadoutState.PrimaryItemId,
            player.GameplayLoadoutState.SecondaryItemId,
            player.GameplayLoadoutState.UtilityItemId,
            player.GameplayLoadoutState.AcquiredItemId,
            player.GameplayLoadoutState.EquippedItemId,
        })
        {
            if (string.IsNullOrWhiteSpace(itemId) || !seenItemIds.Add(itemId))
            {
                continue;
            }

            if (runtimeRegistry.TryGetGameplayAbilityDefinition(itemId, out var item, out _))
            {
                yield return item;
            }
        }

        if (!runtimeRegistry.TryGetLoadout(player.GameplayClassId, player.SelectedGameplayLoadoutId, out var selectedLoadout)
            || selectedLoadout.AbilityItemIds is null)
        {
            yield break;
        }

        foreach (var itemId in selectedLoadout.AbilityItemIds)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !seenItemIds.Add(itemId))
            {
                continue;
            }

            if (runtimeRegistry.TryGetGameplayAbilityDefinition(itemId, out var item, out _))
            {
                yield return item;
            }
        }
    }

    private static bool TryResolveSecondaryGameplayAbilityItem(PlayerEntity player, out GameplayItemDefinition item)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (player.ClassId != PlayerClass.Engineer
            && (player.IsAcquiredWeaponEquipped || player.IsExperimentalOffhandSelected)
            && runtimeRegistry.TryGetGameplayAbilityDefinition(player.GameplayLoadoutState.EquippedItemId, out item, out _))
        {
            return true;
        }

        var secondaryItemId = player.GameplayLoadoutState.SecondaryItemId;
        if ((player.ClassId == PlayerClass.Engineer
                || (player.HasExperimentalOffhandWeapon && !player.IsExperimentalOffhandSelected))
            && runtimeRegistry.TryGetLoadout(player.GameplayClassId, player.SelectedGameplayLoadoutId, out var selectedLoadout))
        {
            secondaryItemId = selectedLoadout.SecondaryItemId;
        }

        return runtimeRegistry.TryGetGameplayAbilityDefinition(secondaryItemId, out item, out _);
    }

    private WorldGameplayAbilityEvent CreateGameplayAbilityEvent(
        PlayerEntity player,
        GameplayItemDefinition item,
        GameplayAbilityDefinition ability,
        GameplayAbilityInputPhase phase,
        bool handled,
        bool consumedInput,
        bool cancelled)
    {
        return new WorldGameplayAbilityEvent(
            Frame,
            player.Id,
            player.ClassId,
            player.Team,
            item.Id,
            item.BehaviorId,
            ability.Category,
            ability.Activation,
            ability.ExecutorId,
            phase,
            ability.Tags.ToArray(),
            handled,
            consumedInput,
            cancelled);
    }

    internal GameplayAbilityResult ExecuteEngineerPdaAbility(GameplayAbilityContext context)
    {
        if (TryHandleExperimentalEngineerDestinyPunctuatorBlast(context.Player, context.Input))
        {
            return GameplayAbilityResult.HandledAndConsumed;
        }

        HandleEngineerPdaSentryCommand(context.Player);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecutePyroAirblastAbility(GameplayAbilityContext context)
    {
        var player = context.Player;
        var fuelCost = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "cost",
            PlayerEntity.PyroAirblastCost,
            minValue: 0);
        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            PlayerEntity.PyroAirblastReloadTicks,
            Config.TicksPerSecond);
        var noFlameTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "noFlameTicks",
            "noFlameSeconds",
            PlayerEntity.PyroAirblastNoFlameTicks,
            Config.TicksPerSecond,
            minValue: 0);
        if (!player.TryFirePyroAirblast(fuelCost, cooldownTicks, noFlameTicks))
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        if (string.Equals(context.Ability.Category, GameplayAbilityConstants.UtilityCategory, StringComparison.Ordinal)
            || string.Equals(context.Item.BehaviorId, BuiltInGameplayBehaviorIds.PyroUtility, StringComparison.Ordinal))
        {
            TriggerPyroSelfAirblast(player, context.Input.AimWorldX, context.Input.AimWorldY, context.Input.FirePrimary);
        }
        else
        {
            TriggerPyroAirblast(player, context.Input.AimWorldX, context.Input.AimWorldY, context.Input.FirePrimary);
        }

        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteDemomanDetonateAbility(GameplayAbilityContext context)
    {
        if (context.Player.IsExperimentalDemoknightEnabled)
        {
            return ExecuteExperimentalDemoknightSecondaryAbility(context);
        }

        DetonateOwnedMines(context.Player.Id);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteExperimentalSoldierSecondaryAbility(GameplayAbilityContext context)
    {
        if (TryHandleExperimentalSoldierStingerDetonation(context.Player)
            || TryHandleExperimentalSoldierCivilDefenseTurret(context.Player)
            || TryHandleExperimentalSoldierThundergunner(context.Player, context.Input))
        {
            return GameplayAbilityResult.HandledAndConsumed;
        }

        return GameplayAbilityResult.Ignored;
    }

    internal GameplayAbilityResult ExecuteExperimentalLtdPassiveAbility(GameplayAbilityContext context)
    {
        ApplyExperimentalPassivePlayerEffects(context.Player);
        return new GameplayAbilityResult(Handled: true, ConsumedInput: false);
    }

    internal GameplayAbilityResult ExecuteExperimentalLtdRageAbility(GameplayAbilityContext context)
    {
        return TryHandleExperimentalRageActivation(context.Player)
            ? GameplayAbilityResult.HandledAndConsumed
            : GameplayAbilityResult.Ignored;
    }

    private GameplayAbilityResult ExecuteExperimentalDemoknightSecondaryAbility(GameplayAbilityContext context)
    {
        var player = context.Player;
        if (!player.IsExperimentalDemoknightEnabled)
        {
            return GameplayAbilityResult.Ignored;
        }

        if (ExperimentalGameplaySettings.EnableDemoknightGhostDash)
        {
            if (player.TryStartExperimentalGhostDash(
                    GetExperimentalGhostDashDurationTicks(),
                    GetExperimentalGhostDashCooldownTicks(),
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultGhostDashNextAttackDamageMultiplier,
                    GetExperimentalGhostDashImpulse()))
            {
                RegisterWorldSoundEvent(ExperimentalDemoknightCatalog.ChargeStartSoundName, player.X, player.Y);
            }

            return GameplayAbilityResult.HandledAndConsumed;
        }

        if (player.IsExperimentalDemoknightCharging)
        {
            player.CancelExperimentalDemoknightCharge(depleteMeter: true);
        }
        else if (player.TryStartExperimentalDemoknightCharge())
        {
            RegisterWorldSoundEvent(ExperimentalDemoknightCatalog.ChargeStartSoundName, player.X, player.Y);
        }

        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteHeavySandvichAbility(GameplayAbilityContext context)
    {
        var durationTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "durationTicks",
            "durationSeconds",
            PlayerEntity.HeavyEatDurationTicks,
            Config.TicksPerSecond);
        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            PlayerEntity.HeavySandvichCooldownTicks,
            Config.TicksPerSecond);
        if (context.Player.IsHeavyEating)
        {
            var cancelCooldownTicks = GameplayAbilityParameterReader.GetTicks(
                context.Ability,
                "cancelCooldownTicks",
                "cancelCooldownSeconds",
                cooldownTicks * 2,
                Config.TicksPerSecond);
            var cancelCooldownMultiplier = GameplayAbilityParameterReader.GetFloat(
                context.Ability,
                "cancelCooldownMultiplier",
                1f,
                minValue: 0f);
            return new GameplayAbilityResult(
                context.Player.TryCancelHeavySelfHeal((int)MathF.Round(cancelCooldownTicks * cancelCooldownMultiplier)),
                ConsumedInput: true);
        }

        var totalHeal = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "totalHeal",
            200f,
            minValue: 0f);
        return new GameplayAbilityResult(context.Player.TryStartHeavySelfHeal(durationTicks, cooldownTicks, totalHeal), ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteSniperScopeAbility(GameplayAbilityContext context)
    {
        return new GameplayAbilityResult(context.Player.TryToggleSniperScope(), ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteSniperBinocularsAbility(GameplayAbilityContext context)
    {
        return new GameplayAbilityResult(context.Player.TryToggleBinoculars(), ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteMedicNeedlegunAbility(GameplayAbilityContext context)
    {
        var player = context.Player;
        var fireCooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "fireCooldownTicks",
            "fireCooldownSeconds",
            PlayerEntity.MedicNeedleFireCooldownTicks,
            Config.TicksPerSecond);
        var refillTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "refillTicks",
            "refillSeconds",
            PlayerEntity.MedicNeedleRefillTicksDefault,
            Config.TicksPerSecond);
        if (player.IsAcquiredWeaponEquipped)
        {
            if (player.TryFireAcquiredMedicNeedle(fireCooldownTicks, refillTicks))
            {
                WeaponHandler.FireAcquiredMedicNeedle(player, context.Input.AimWorldX, context.Input.AimWorldY);
                return GameplayAbilityResult.HandledAndConsumed;
            }
        }
        else if (player.TryFireMedicNeedle(fireCooldownTicks, refillTicks))
        {
            FireMedicNeedle(player, context.Input.AimWorldX, context.Input.AimWorldY);
            return GameplayAbilityResult.HandledAndConsumed;
        }

        if (player.IsMedicUberReady
            && context.Input.FirePrimary
            && player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
        {
            return ExecuteMedicUberAbility(context);
        }

        return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteMedicUberAbility(GameplayAbilityContext context)
    {
        if (!context.Player.IsMedicUberReady || !context.Player.TryStartMedicUber())
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        AwardMedicUberActivationPoints(context.Player);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteMedicMedigunSwapAbility(GameplayAbilityContext context)
    {
        var player = context.Player;
        if (!player.IsAlive || player.ClassId != PlayerClass.Medic)
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        var targetSlot = player.GameplayLoadoutState.EquippedSlot == GameplayEquipmentSlot.Secondary
            ? GameplayEquipmentSlot.Primary
            : GameplayEquipmentSlot.Secondary;
        player.TrySelectGameplayEquippedSlot(targetSlot);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteSpyCloakAbility(GameplayAbilityContext context)
    {
        if (context.Input.FirePrimary)
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        return new GameplayAbilityResult(context.Player.TryToggleSpyCloak(), ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteSpySuperjumpAbility(GameplayAbilityContext context)
    {
        var player = context.Player;
        var maxChargeTicks = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "maxChargeTicks",
            PlayerEntity.SpySuperjumpMaxChargeTicks,
            minValue: 1);
        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            PlayerEntity.SpySuperjumpCooldownTicks,
            Config.TicksPerSecond);
        var minVelocity = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "minVelocity",
            PlayerEntity.SpySuperjumpMinVelocity,
            minValue: 0f);
        var maxVelocity = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "maxVelocity",
            PlayerEntity.SpySuperjumpMaxVelocity,
            minValue: minVelocity);
        var directionDegrees = PointDirectionDegrees(player.X, player.Y, context.Input.AimWorldX, context.Input.AimWorldY);
        if (context.Phase == GameplayAbilityInputPhase.Released)
        {
            if (player.TryReleaseSpySuperjump(out var velocityX, out var velocityY, maxChargeTicks, minVelocity, maxVelocity, cooldownTicks))
            {
                player.ApplyVelocityImpulse(velocityX, velocityY);
                RegisterWorldSoundEvent("JumpSnd", player.X, player.Y);
                return GameplayAbilityResult.HandledAndConsumed;
            }

            return new GameplayAbilityResult(Handled: false, ConsumedInput: player.SpySuperjumpChargeTicks > 0);
        }

        if (player.SpySuperjumpChargeTicks == 0 && !player.IsSpySuperjumping)
        {
            return new GameplayAbilityResult(
                player.TryStartSpySuperjumpCharge(directionDegrees, context.Input.Left, context.Input.Right, context.Input.Up, context.Input.Down),
                ConsumedInput: true);
        }

        if (player.SpySuperjumpChargeTicks <= 0)
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        var heldButtons = player.SpySuperjumpChargeStartMovementButtons;
        var leftWasHeld = (heldButtons & 0x01) != 0;
        var rightWasHeld = (heldButtons & 0x02) != 0;
        var upWasHeld = (heldButtons & 0x04) != 0;
        var downWasHeld = (heldButtons & 0x08) != 0;
        var newButtonPressed = (context.Input.Left && !leftWasHeld)
            || (context.Input.Right && !rightWasHeld)
            || (context.Input.Up && !upWasHeld)
            || (context.Input.Down && !downWasHeld);
        if (newButtonPressed || player.IsSpyBackstabAnimating || player.IsCarryingIntel)
        {
            player.CancelSpySuperjumpCharge();
            return GameplayAbilityResult.HandledAndConsumed;
        }

        player.IncrementSpySuperjumpCharge(directionDegrees, maxChargeTicks);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteQuoteBladeThrowAbility(GameplayAbilityContext context)
    {
        var energyCost = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "energyCost",
            PlayerEntity.QuoteBladeEnergyCost,
            minValue: 0);
        var activeProjectileLimit = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "activeProjectileLimit",
            PlayerEntity.QuoteBladeMaxOut,
            minValue: 0);
        var lifetimeTicks = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "lifetimeTicks",
            PlayerEntity.QuoteBladeLifetimeTicks,
            minValue: 1);
        if (!context.Player.TryFireQuoteBlade(energyCost, activeProjectileLimit))
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        WeaponHandler.FireQuoteBlade(context.Player, context.Input.AimWorldX, context.Input.AimWorldY, lifetimeTicks);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteScoutTauntAbility(GameplayAbilityContext context)
    {
        return new GameplayAbilityResult(context.Player.TryStartTaunt(), ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteSoldierSecondaryToggleAbility(GameplayAbilityContext context)
    {
        if (!context.Input.SwapWeapon)
        {
            TryHandleLegacyNetworkSecondaryWeaponToggle(context.Player, context.Input);
        }

        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteScoutNailgunToggleAbility(GameplayAbilityContext context)
    {
        if (!context.Input.SwapWeapon)
        {
            TryHandleLegacyNetworkSecondaryWeaponToggle(context.Player, context.Input);
        }

        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayPrimaryWeaponResult ExecuteScoutNailgunPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        WeaponHandler.FireScoutNailgun(context.Player, context.AimWorldX, context.AimWorldY);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    internal GameplayAbilityResult ExecuteEngineerJumpPadAbility(GameplayAbilityContext context)
    {
        if (!TryDestroyJumpPad(context.Player))
        {
            TryBuildJumpPad(context.Player);
        }

        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteHeavyGhostDashAbility(GameplayAbilityContext context)
    {
        var durationTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "durationTicks",
            "durationSeconds",
            GetHeavyGhostDashDurationTicks(),
            Config.TicksPerSecond);
        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            GetHeavyGhostDashCooldownTicks(),
            Config.TicksPerSecond);
        var movementTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "movementDurationTicks",
            "movementDurationSeconds",
            GetHeavyGhostDashMovementDurationTicks(),
            Config.TicksPerSecond);
        var impulse = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "impulse",
            GetHeavyGhostDashImpulse(),
            minValue: 0f);
        var nextAttackDamageMultiplier = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "nextAttackDamageMultiplier",
            ExperimentalGameplaySettings.DefaultGhostDashNextAttackDamageMultiplier,
            minValue: 1.0001f);
        var useMomentum = GameplayAbilityParameterReader.GetBool(context.Ability, "useMomentum", defaultValue: true);
        var burstSpeedMultiplier = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "burstSpeedMultiplier",
            ExperimentalGameplaySettings.HeavyGhostDashBurstSpeedMultiplier,
            minValue: 0f);
        var disableGravity = GameplayAbilityParameterReader.GetBool(
            context.Ability,
            "disableGravity",
            defaultValue: ExperimentalGameplaySettings.HeavyGhostDashDisableGravityDefault);
        var enableGhostTrail = GameplayAbilityParameterReader.GetBool(
            context.Ability,
            "enableGhostTrail",
            defaultValue: ExperimentalGameplaySettings.HeavyGhostDashEnableGhostTrailDefault);
        if (!context.Player.TryStartExperimentalGhostDash(
                durationTicks,
                cooldownTicks,
                nextAttackDamageMultiplier,
                dashImpulse: 0f,
                requireExperimentalDemoknight: false,
                useMomentum: false,
                movementTicks: 0,
                burstSpeedMultiplier: burstSpeedMultiplier,
                disableGravity: disableGravity,
                enableGhostTrail: enableGhostTrail))
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        // Initial horizontal burst — decelerates naturally via friction
        var burstSpeed = LegacyMovementModel.GetMaxRunSpeed(context.Player.RunPower) * context.Player.ExperimentalGhostDashBurstSpeedMultiplier;
        context.Player.ApplyVelocityImpulse(
            context.Player.FacingDirectionX >= 0f ? burstSpeed : -burstSpeed,
            velocityY: 0f);

        RegisterWorldSoundEvent(ExperimentalDemoknightCatalog.ChargeStartSoundName, context.Player.X, context.Player.Y);
        return GameplayAbilityResult.HandledAndConsumed;
    }
}
