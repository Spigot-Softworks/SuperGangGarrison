using OpenGarrison.GameplayModding;
using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

internal sealed partial class GameplayAbilitySystem
{
    public Func<WorldGameplayAbilityEvent, bool>? GameplayAbilityInputInterceptor { get; set; }

    public IReadOnlyList<WorldGameplayAbilityEvent> PendingGameplayAbilityEvents => _host.PresentationEvents.GameplayAbilityEvents;

    public IReadOnlyList<WorldGameplayAbilityEvent> DrainPendingGameplayAbilityEvents()
    {
        return _host.PresentationEvents.DrainGameplayAbilityEvents();
    }

    internal GameplayAbilityResult TryDispatchGameplayAbility(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        GameplayAbilityInputPhase phase,
        string category,
        float sourceX,
        float sourceY)
    {
        var channel = ToGameplayAbilityChannel(category);
        foreach (var item in ResolveGameplayAbilityItems(player, channel))
        {
            if (item.Ability is not { } ability
                || !AbilityChannelMatches(ability, channel)
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

    internal void DispatchPassiveGameplayAbilities(
        PlayerEntity player,
        PlayerInputSnapshot input,
        PlayerInputSnapshot previousInput,
        float sourceX,
        float sourceY)
    {
        if (!_host.ExperimentalGameplaySettings.EnableSecondaryAbilities)
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

    internal GameplayAbilityResult TryDispatchGameplayAbilityItem(
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
            _host.PresentationEvents.AddGameplayAbilityEvent(baseEvent with
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
                _host.PresentationEvents.AddGameplayAbilityEvent(baseEvent);
            }

            return GameplayAbilityResult.Ignored;
        }

        if (!CharacterClassCatalog.RuntimeRegistry.TryGetGameplayAbilityExecutor(ability.ExecutorId, out var executor))
        {
            if (emitAbilityEvent)
            {
                _host.PresentationEvents.AddGameplayAbilityEvent(baseEvent);
            }

            return GameplayAbilityResult.Ignored;
        }

        var result = _host.ExecuteAbilityExecutor(executor, player, item, ability, phase, input, previousInput, sourceX, sourceY);
        if (emitAbilityEvent)
        {
            _host.PresentationEvents.AddGameplayAbilityEvent(baseEvent with
            {
                Handled = result.Handled,
                ConsumedInput = result.ConsumedInput,
            });
        }

        return result;
    }

    internal static bool CanDispatchAbilityForCurrentPlayerState(PlayerEntity player, GameplayAbilityDefinition ability)
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

    internal void ApplyNetworkSpecialAbilitiesSetting(bool enabled)
    {
        // Equipment arrives with the authoritative player state; avoid rebuilding
        // loadouts here and overwriting that equipment during reconciliation.
        _host.ExperimentalGameplaySettings = _host.ExperimentalGameplaySettings with { EnableSecondaryAbilities = enabled };
    }

    internal bool IsGameplayAbilityBlockedBySpecialAbilitiesSetting(GameplayAbilityDefinition ability)
    {
        if (_host.ExperimentalGameplaySettings.EnableSecondaryAbilities)
        {
            return false;
        }

        return !IsCoreSecondaryInputAbility(ability);
    }

    internal static bool IsCoreSecondaryInputAbility(GameplayAbilityDefinition ability)
    {
        return string.Equals(ability.Channel, GameplayAbilityConstants.SpecialChannel, StringComparison.Ordinal)
            && ability.Tags.Contains(GameplayAbilityConstants.CoreSecondaryInputTag, StringComparer.Ordinal);
    }

    internal static bool AbilityChannelMatches(GameplayAbilityDefinition ability, string channel)
    {
        return string.Equals(ability.Channel, channel, StringComparison.Ordinal);
    }

    internal static string ToGameplayAbilityChannel(string categoryOrChannel)
    {
        return categoryOrChannel switch
        {
            GameplayAbilityConstants.WeaponAltFireCategory => GameplayAbilityConstants.SpecialChannel,
            GameplayAbilityConstants.SecondaryCategory => GameplayAbilityConstants.SpecialChannel,
            GameplayAbilityConstants.UtilityCategory => GameplayAbilityConstants.UtilityChannel,
            GameplayAbilityConstants.PassiveCategory => GameplayAbilityConstants.PassiveChannel,
            GameplayAbilityConstants.TauntCategory => GameplayAbilityConstants.TauntChannel,
            _ => categoryOrChannel,
        };
    }

    internal static bool AbilityActivationMatches(GameplayAbilityDefinition ability, GameplayAbilityInputPhase phase)
    {
        var activation = ToAbilityActivation(phase);
        return string.Equals(ability.Activation, activation, StringComparison.Ordinal)
            || (phase == GameplayAbilityInputPhase.Pressed
                && string.Equals(ability.Activation, GameplayAbilityConstants.HeldActivation, StringComparison.Ordinal))
            || (phase == GameplayAbilityInputPhase.Released
                && string.Equals(ability.Activation, GameplayAbilityConstants.HeldActivation, StringComparison.Ordinal));
    }

    internal static string ToAbilityActivation(GameplayAbilityInputPhase phase)
    {
        return phase switch
        {
            GameplayAbilityInputPhase.Held => GameplayAbilityConstants.HeldActivation,
            GameplayAbilityInputPhase.Released => GameplayAbilityConstants.ReleasedActivation,
            GameplayAbilityInputPhase.PassiveTick => GameplayAbilityConstants.PassiveTickActivation,
            _ => GameplayAbilityConstants.PressedActivation,
        };
    }

    internal static IEnumerable<GameplayItemDefinition> ResolveGameplayAbilityItems(PlayerEntity player, string channel)
    {
        foreach (var item in ResolveAllPlayerGameplayAbilityItems(player))
        {
            if (item.Ability is { } ability && AbilityChannelMatches(ability, channel))
            {
                yield return item;
            }
        }
    }

    internal static IReadOnlyList<GameplayItemDefinition> ResolveAllPlayerGameplayAbilityItems(PlayerEntity player)
    {
        return CharacterClassCatalog.RuntimeRegistry.ResolveGameplayAbilityItems(player.GameplayLoadoutState);
    }

    internal static bool TryResolveSecondaryGameplayAbilityItem(PlayerEntity player, out GameplayItemDefinition item)
    {
        foreach (var candidate in ResolveAllPlayerGameplayAbilityItems(player))
        {
            if (candidate.Ability is { } ability
                && AbilityChannelMatches(ability, GameplayAbilityConstants.SpecialChannel))
            {
                item = candidate;
                return true;
            }
        }

        item = null!;
        return false;
    }

    internal WorldGameplayAbilityEvent CreateGameplayAbilityEvent(
        PlayerEntity player,
        GameplayItemDefinition item,
        GameplayAbilityDefinition ability,
        GameplayAbilityInputPhase phase,
        bool handled,
        bool consumedInput,
        bool cancelled)
    {
        return new WorldGameplayAbilityEvent(
            _host.Frame,
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
        if (_host.TryHandleExperimentalEngineerDestinyPunctuatorBlast(context.Player, context.Input))
        {
            return GameplayAbilityResult.HandledAndConsumed;
        }

        _host.HandleEngineerPdaSentryCommand(context.Player);
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
            _host.Config.TicksPerSecond);
        var noFlameTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "noFlameTicks",
            "noFlameSeconds",
            PlayerEntity.PyroAirblastNoFlameTicks,
            _host.Config.TicksPerSecond,
            minValue: 0);
        var isAirburst = string.Equals(
                context.Ability.Category,
                GameplayAbilityConstants.UtilityCategory,
                StringComparison.Ordinal)
            || string.Equals(
                context.Item.BehaviorId,
                BuiltInGameplayBehaviorIds.PyroUtility,
                StringComparison.Ordinal);
        if (!player.TryFirePyroAirblast(
                fuelCost,
                cooldownTicks,
                noFlameTicks,
                allowAirburstWithAlternatePrimary: isAirburst))
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        if (isAirburst)
        {
            _host.TriggerPyroSelfAirblast(player, context.Input.AimWorldX, context.Input.AimWorldY);
        }
        else
        {
            _host.TriggerPyroAirblast(player, context.Input.AimWorldX, context.Input.AimWorldY);
        }

        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteDemomanDetonateAbility(GameplayAbilityContext context)
    {
        if (context.Player.IsExperimentalDemoknightEnabled)
        {
            return ExecuteExperimentalDemoknightSecondaryAbility(context);
        }

        _host.DetonateOwnedMines(context.Player.Id);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteExperimentalSoldierSecondaryAbility(GameplayAbilityContext context)
    {
        if (_host.TryHandleExperimentalSoldierStingerDetonation(context.Player)
            || _host.TryHandleExperimentalSoldierCivilDefenseTurret(context.Player)
            || _host.TryHandleExperimentalSoldierThundergunner(context.Player, context.Input))
        {
            return GameplayAbilityResult.HandledAndConsumed;
        }

        return GameplayAbilityResult.Ignored;
    }

    internal GameplayAbilityResult ExecuteExperimentalLtdPassiveAbility(GameplayAbilityContext context)
    {
        _host.ApplyExperimentalPassivePlayerEffects(context.Player);
        return new GameplayAbilityResult(Handled: true, ConsumedInput: false);
    }

    internal GameplayAbilityResult ExecuteExperimentalLtdRageAbility(GameplayAbilityContext context)
    {
        return _host.TryHandleExperimentalRageActivation(context.Player)
            ? GameplayAbilityResult.HandledAndConsumed
            : GameplayAbilityResult.Ignored;
    }

    internal GameplayAbilityResult ExecuteExperimentalDemoknightSecondaryAbility(GameplayAbilityContext context)
    {
        var player = context.Player;
        if (!player.IsExperimentalDemoknightEnabled)
        {
            return GameplayAbilityResult.Ignored;
        }

        if (_host.GetLastToDieGameplaySettings(player).EnableDemoknightGhostDash)
        {
            if (player.TryStartExperimentalGhostDash(
                    _host.GetExperimentalGhostDashDurationTicks(),
                    _host.GetExperimentalGhostDashCooldownTicks(),
                    global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultGhostDashNextAttackDamageMultiplier,
                    _host.GetExperimentalGhostDashImpulse()))
            {
                _host.RegisterWorldSoundEvent(ExperimentalDemoknightCatalog.ChargeStartSoundName, player.X, player.Y);
            }

            return GameplayAbilityResult.HandledAndConsumed;
        }

        if (player.IsExperimentalDemoknightCharging)
        {
            player.CancelExperimentalDemoknightCharge(depleteMeter: true);
        }
        else if (player.TryStartExperimentalDemoknightCharge())
        {
            _host.RegisterWorldSoundEvent(ExperimentalDemoknightCatalog.ChargeStartSoundName, player.X, player.Y);
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
            _host.Config.TicksPerSecond);
        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            PlayerEntity.HeavySandvichCooldownTicks,
            _host.Config.TicksPerSecond);
        if (context.Player.IsHeavyEating)
        {
            var cancelCooldownTicks = GameplayAbilityParameterReader.GetTicks(
                context.Ability,
                "cancelCooldownTicks",
                "cancelCooldownSeconds",
                cooldownTicks * 2,
                _host.Config.TicksPerSecond);
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

    internal static GameplayAbilityResult ExecuteSniperScopeAbility(GameplayAbilityContext context)
    {
        return new GameplayAbilityResult(context.Player.TryToggleSniperScope(), ConsumedInput: true);
    }

    internal static GameplayAbilityResult ExecuteSniperBinocularsAbility(GameplayAbilityContext context)
    {
        return new GameplayAbilityResult(context.Player.TryToggleBinoculars(), ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteSniperStrongDrinkAbility(GameplayAbilityContext context)
    {
        var player = context.Player;
        if (player.ClassId != PlayerClass.Sniper || !player.IsAlive)
        {
            player.CancelStrongDrinkCharge();
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            450,
            _host.Config.TicksPerSecond);
        if (player.TryGetReplicatedStateInt(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.SniperStrongDrinkCooldownTicksKey,
                out var remainingCooldown)
            && remainingCooldown > 0)
        {
            player.CancelStrongDrinkCharge();
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        if (player.IsTaunting || player.IsHeavyEating)
        {
            player.CancelStrongDrinkCharge();
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        var maxThrowSpeed = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "throwSpeed",
            PlayerEntity.StrongDrinkMaxThrowSpeed,
            minValue: 0.1f);
        // Legacy alias used by older ability params.
        if (context.Ability.Parameters.ContainsKey("maxThrowSpeed"))
        {
            maxThrowSpeed = GameplayAbilityParameterReader.GetFloat(
                context.Ability,
                "maxThrowSpeed",
                maxThrowSpeed,
                minValue: 0.1f);
        }

        var lobBiasDegrees = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "lobBiasDegrees",
            PlayerEntity.StrongDrinkLobBiasDegrees,
            minValue: 0f);
        var gravityPerTick = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "gravityPerTick",
            GrenadeProjectileEntity.StrongDrinkGravityPerTick,
            minValue: 0f);
        var spinSpeed = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "spinSpeed",
            GrenadeProjectileEntity.StrongDrinkDefaultSpinSpeed);
        var fuseTicks = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "fuseTicks",
            GrenadeProjectileEntity.StrongDrinkDefaultFuseTicks,
            minValue: 1);
        var directionDegrees = SimulationMath.PointDirectionDegrees(
            player.X,
            player.Y,
            context.Input.AimWorldX,
            context.Input.AimWorldY);

        if (context.Phase == GameplayAbilityInputPhase.Released)
        {
            // Holding is only for the aim-arc preview; release always throws at full strength.
            if (!player.TryReleaseStrongDrinkCharge(out _, out _))
            {
                return new GameplayAbilityResult(Handled: false, ConsumedInput: false);
            }

            _host.WeaponHandler.FireStrongDrink(
                player,
                context.Input.AimWorldX,
                context.Input.AimWorldY,
                maxThrowSpeed,
                chargeFraction: 1f,
                spinSpeed,
                fuseTicks,
                lobBiasDegrees,
                gravityPerTick);
            player.SetGameplayAbilityCooldownReplicatedState(
                GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId,
                GameplayAbilityReplicatedState.SniperStrongDrinkCooldownTicksKey,
                cooldownTicks);
            return GameplayAbilityResult.HandledAndConsumed;
        }

        if (player.StrongDrinkChargeTicks == 0)
        {
            return new GameplayAbilityResult(
                player.TryStartStrongDrinkCharge(directionDegrees),
                ConsumedInput: true);
        }

        // Keep the preview aim live while held; throw power is not charged.
        player.IncrementStrongDrinkCharge(directionDegrees, maxChargeTicks: 1);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteMedicNeedlegunAbility(GameplayAbilityContext context)
    {
        var player = context.Player;
        var fireCooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "fireCooldownTicks",
            "fireCooldownSeconds",
            PlayerEntity.MedicNeedleFireCooldownTicks,
            _host.Config.TicksPerSecond);
        var refillTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "refillTicks",
            "refillSeconds",
            PlayerEntity.MedicNeedleRefillTicksDefault,
            _host.Config.TicksPerSecond);
        if (player.IsAcquiredWeaponEquipped)
        {
            if (player.TryFireAcquiredMedicNeedle(fireCooldownTicks, refillTicks))
            {
                _host.WeaponHandler.FireAcquiredMedicNeedle(player, context.Input.AimWorldX, context.Input.AimWorldY);
                return GameplayAbilityResult.HandledAndConsumed;
            }
        }
        else if (player.TryFireMedicNeedle(fireCooldownTicks, refillTicks))
        {
            _host.FireMedicNeedle(player, context.Input.AimWorldX, context.Input.AimWorldY);
            return GameplayAbilityResult.HandledAndConsumed;
        }

        if (player.IsMedicUberReady
            && context.Input.FirePrimary
            && player.HasGameplayAbilityBehavior(
                GameplayAbilityConstants.SpecialChannel,
                BuiltInGameplayBehaviorIds.MedicUber))
        {
            return ExecuteMedicUberAbility(context);
        }

        return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteMedicKritzBeamAbility(GameplayAbilityContext context)
    {
        if (context.Input.FirePrimary)
        {
            if (context.Player.IsMedicUberReady
                && context.Player.HasGameplayAbilityBehavior(
                    GameplayAbilityConstants.SpecialChannel,
                    BuiltInGameplayBehaviorIds.MedicUber))
            {
                return ExecuteMedicUberAbility(context);
            }

            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        var maxRange = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "range",
            MedicBeamDefaults.KritzBeamDefaultRange,
            minValue: 1f);
        var damagePerSecond = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "damagePerSecond",
            MedicBeamDefaults.KritzBeamDefaultDamagePerSecond,
            minValue: 0f);
        var chargePerTick = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "chargePerTick",
            MedicBeamDefaults.KritzBeamDefaultChargePerTick,
            minValue: 0f);

        return new GameplayAbilityResult(
            _host.UpdateMedicKritzBeam(
                context.Player,
                context.Input.AimWorldX,
                context.Input.AimWorldY,
                maxRange,
                damagePerSecond,
                chargePerTick),
            ConsumedInput: true);
    }

    internal GameplayAbilityResult ExecuteMedicKritzHealNeedlesAbility(GameplayAbilityContext context)
    {
        var healPerHit = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "healPerHit",
            MedicHealNeedleProjectileEntity.DefaultHealPerHit,
            minValue: 0);
        var enemyDamagePerHit = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "enemyDamagePerHit",
            MedicHealNeedleProjectileEntity.DefaultEnemyDamagePerHit,
            minValue: 0);
        var projectileSpeed = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "projectileSpeed",
            MedicHealNeedleProjectileEntity.DefaultProjectileSpeed,
            minValue: 0f);
        var spreadDegrees = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "spreadDegrees",
            MedicHealNeedleProjectileEntity.DefaultSpreadDegrees,
            minValue: 0f);
        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            PlayerEntity.MedicHealDartDefaultCooldownTicks,
            _host.Config.TicksPerSecond);

        if (!context.Player.TryFireMedicHealDart(cooldownTicks))
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        _host.FireMedicKritzHealNeedle(
            context.Player,
            context.Input.AimWorldX,
            context.Input.AimWorldY,
            healPerHit,
            enemyDamagePerHit,
            projectileSpeed,
            spreadDegrees);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteMedicUberAbility(GameplayAbilityContext context)
    {
        if (!context.Input.FirePrimary
            || !context.Player.IsMedicUberReady
            || !context.Player.TryStartMedicUber())
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        _host.AwardMedicUberActivationPoints(context.Player);
        _host.RegisterWorldSoundEvent("UberStartSnd", context.Player.X, context.Player.Y);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal static GameplayAbilityResult ExecuteSpyCloakAbility(GameplayAbilityContext context)
    {
        if (context.Player.TryBeginLastToDieProfessionalFireChord())
        {
            return GameplayAbilityResult.HandledAndConsumed;
        }

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
        maxChargeTicks = player.ResolveSpySuperjumpMaxChargeTicks(maxChargeTicks);
        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            PlayerEntity.SpySuperjumpCooldownTicks,
            _host.Config.TicksPerSecond);
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
        var directionDegrees = SimulationMath.PointDirectionDegrees(player.X, player.Y, context.Input.AimWorldX, context.Input.AimWorldY);
        if (context.Phase == GameplayAbilityInputPhase.Released)
        {
            if (player.TryReleaseSpySuperjump(out var velocityX, out var velocityY, maxChargeTicks, minVelocity, maxVelocity, cooldownTicks))
            {
                player.ApplyVelocityImpulse(velocityX, velocityY);
                if (player.LastToDieHealingHarnessEnabled)
                {
                    _host.ApplyHealingWithFeedback(
                        player,
                        LastToDieDerivedModifiers.SpyHealingHarnessHealing,
                        "HealSnd",
                        player.X,
                        player.Y);
                    player.ExtinguishAfterburn();
                }
                _host.RegisterWorldSoundEvent("JumpSnd", player.X, player.Y, player.Id);
                return GameplayAbilityResult.HandledAndConsumed;
            }

            return new GameplayAbilityResult(Handled: false, ConsumedInput: player.SpySuperjumpChargeTicks > 0);
        }

        if (player.SpySuperjumpChargeTicks == 0)
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

        _host.WeaponHandler.FireQuoteBlade(context.Player, context.Input.AimWorldX, context.Input.AimWorldY, lifetimeTicks);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteCivvieUmbrellaAbility(GameplayAbilityContext context)
    {
        var maxChargeTicks = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "maxChargeTicks",
            PlayerEntity.CivvieUmbrellaMaxChargeTicks,
            minValue: 1);
        if (!context.Player.TryActivateCivvieUmbrella(maxChargeTicks))
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        var openingChargeCost = GameplayAbilityParameterReader.GetInt(context.Ability,
            "openingChargeCost", PlayerEntity.CivvieUmbrellaOpeningChargeCost, minValue: 0);
        if (context.Player.TrySpendCivvieUmbrellaOpeningCharge(openingChargeCost))
        {
            _host.TriggerCivvieUmbrellaAirblast(context.Player, context.Input.AimWorldX, context.Input.AimWorldY);
        }

        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal static GameplayAbilityResult ExecuteCivvieTauntAbility(GameplayAbilityContext context)
    {
        if (context.Player.IsCivviePogoActive)
        {
            var trickFrameCount = GameplayAbilityParameterReader.GetInt(
                context.Ability,
                "pogoTrickFrameCount",
                PlayerEntity.CivviePogoTrickFrameCountDefault,
                minValue: 1);
            var requestedDurationTicks = GameplayAbilityParameterReader.GetInt(
                context.Ability,
                "pogoTrickDurationTicks",
                PlayerEntity.CivviePogoTrickDurationTicksDefault,
                minValue: 1);
            var trickDurationTicks = PlayerEntity.ResolveCivviePogoTrickDurationTicks(
                requestedDurationTicks,
                context.World.Config.TicksPerSecond);
            return new GameplayAbilityResult(
                context.Player.TryStartCivviePogoTrick(trickFrameCount, trickDurationTicks),
                ConsumedInput: true);
        }

        return new GameplayAbilityResult(
            TryStartTauntWithCivvieHeal(context.Player, context.Item.Id),
            ConsumedInput: true);
    }

    internal static GameplayAbilityResult ExecuteCivviePogoAbility(GameplayAbilityContext context)
    {
        if (context.Phase != GameplayAbilityInputPhase.Pressed)
        {
            return GameplayAbilityResult.Ignored;
        }

        var baseBounceJumpScale = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "baseBounceJumpScale",
            PlayerEntity.CivviePogoBaseBounceJumpScaleDefault,
            minValue: 0f);
        var superJumpScale = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "superJumpScale",
            PlayerEntity.CivviePogoSuperJumpScaleDefault,
            minValue: 0f);
        var crunchDurationTicks = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "crunchDurationTicks",
            PlayerEntity.CivviePogoCrunchDurationTicksDefault,
            minValue: 1);
        return new GameplayAbilityResult(
            context.Player.TryToggleCivviePogo(baseBounceJumpScale, superJumpScale, crunchDurationTicks),
            ConsumedInput: true);
    }

    internal static GameplayAbilityResult ExecuteScoutTauntAbility(GameplayAbilityContext context)
    {
        return new GameplayAbilityResult(context.Player.TryStartTaunt(), ConsumedInput: true);
    }

    internal static bool TryStartTauntWithCivvieHeal(PlayerEntity player)
    {
        if (!player.TryStartTaunt())
        {
            return false;
        }

        if (player.TryGetGameplayAbilityItem(
                GameplayAbilityConstants.TauntChannel,
                BuiltInGameplayBehaviorIds.CivvieTaunt,
                out var tauntAbilityItem))
        {
            _ = player.BeginPendingCivvieTauntHeal(tauntAbilityItem.Id);
        }
        else
        {
            player.BeginPendingCivvieTauntHeal();
        }

        return true;
    }

    internal static bool TryStartTauntWithCivvieHeal(PlayerEntity player, string abilityItemId)
    {
        if (!player.TryStartTaunt())
        {
            return false;
        }

        _ = player.BeginPendingCivvieTauntHeal(abilityItemId);
        return true;
    }

    internal void TryApplyPendingCivvieTauntHeal(PlayerEntity player)
    {
        if (!player.CivvieTauntHealPending
            || string.IsNullOrWhiteSpace(player.CivvieTauntHealAbilityItemId))
        {
            return;
        }

        GameplayAbilityDefinition? ability = null;
        var abilityItemId = player.CivvieTauntHealAbilityItemId;
        CharacterClassCatalog.RuntimeRegistry.TryGetGameplayAbilityDefinition(
            abilityItemId,
            out _,
            out ability);

        var healFrameIndex = ability is null
            ? PlayerEntity.CivvieTauntHealFrameIndex
            : GameplayAbilityParameterReader.GetInt(
                ability,
                "healFrameIndex",
                PlayerEntity.CivvieTauntHealFrameIndex,
                minValue: 0);
        if (!player.ShouldTriggerCivvieTauntHeal(healFrameIndex))
        {
            return;
        }

        var emitMoneyBurst = ability is not null
            && GameplayAbilityParameterReader.GetBool(ability, "moneyBurst", defaultValue: false);
        player.MarkCivvieTauntHealTriggered();
        if (emitMoneyBurst)
        {
            _host.PresentationEvents.AddVisualEvent(new WorldVisualEvent(
                "CivvieMoneyBurst",
                player.X,
                player.Y,
                Count: CivvieMoneyTrailRules.PogoTrickBurstParticleCount,
                SourceFrame: _host.Frame < 0 ? 0UL : (ulong)_host.Frame));
        }

        TryApplyCivvieTauntHeal(player, ability, abilityItemId);
    }

    internal void TryApplyCivvieTauntHeal(
        PlayerEntity player,
        GameplayAbilityDefinition? ability,
        string? abilityItemId = null)
    {
        if (ability is null)
        {
            abilityItemId ??= player.CivvieTauntHealAbilityItemId ?? PlayerEntity.CivvieTauntAbilityItemId;
            CharacterClassCatalog.RuntimeRegistry.TryGetGameplayAbilityDefinition(
                abilityItemId,
                out _,
                out ability);
        }

        var healAmount = ability is null
            ? PlayerEntity.CivvieTauntHealAmountDefault
            : GameplayAbilityParameterReader.GetInt(
                ability,
                "healAmount",
                PlayerEntity.CivvieTauntHealAmountDefault,
                minValue: 0);
        var healRadius = ability is null
            ? PlayerEntity.CivvieTauntHealRadiusDefault
            : GameplayAbilityParameterReader.GetFloat(
                ability,
                "healRadius",
                PlayerEntity.CivvieTauntHealRadiusDefault,
                minValue: 0f);
        if (healAmount <= 0 || healRadius <= 0f)
        {
            return;
        }

        var healRadiusSquared = healRadius * healRadius;
        var healSelfOnly = ability is not null
            && GameplayAbilityParameterReader.GetBool(ability, "healSelfOnly", defaultValue: false);
        foreach (var target in _host.EnumerateSimulatedPlayers())
        {
            if (!target.IsAlive || target.Team != player.Team)
            {
                continue;
            }

            if (healSelfOnly && target.Id != player.Id)
            {
                continue;
            }

            var deltaX = target.X - player.X;
            var deltaY = target.Y - player.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) > healRadiusSquared)
            {
                continue;
            }

            _host.ApplyHealingWithFeedback(target, healAmount, "HealSnd", player.X, player.Y);
        }
    }

    internal static GameplayAbilityResult ExecuteSoldierSecondaryToggleAbility(GameplayAbilityContext context)
    {
        // Legacy compatibility definition only. Weapon selection is exclusively driven by
        // PlayerInputSnapshot.SwapWeapon and must never be reached through a utility ability.
        _ = context;
        return GameplayAbilityResult.Ignored;
    }

    internal GameplayAbilityResult ExecuteSoldierBuffBannerAbility(GameplayAbilityContext context)
    {
        var maxChargeDamage = GameplayAbilityParameterReader.GetInt(
            context.Ability,
            "maxChargeDamage",
            PlayerEntity.BuffBannerDefaultMaxChargeDamage,
            minValue: 1);
        var deployTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "deployTicks",
            "deploySeconds",
            PlayerEntity.BuffBannerDefaultDeployTicks,
            _host.Config.TicksPerSecond);
        var activeTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "activeTicks",
            "activeSeconds",
            PlayerEntity.BuffBannerDefaultActiveTicks,
            _host.Config.TicksPerSecond);
        var radius = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "radius",
            PlayerEntity.BuffBannerDefaultRadius,
            minValue: 1f);
        var damageMultiplier = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "damageMultiplier",
            PlayerEntity.BuffBannerDefaultDamageMultiplier,
            minValue: 1f);
        var healthRegenPerSecond = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "healthRegenPerSecond",
            PlayerEntity.BuffBannerDefaultHealthRegenPerSecond,
            minValue: 0f);
        var started = context.Player.TryStartBuffBanner(
            maxChargeDamage,
            deployTicks,
            activeTicks,
            radius,
            damageMultiplier,
            healthRegenPerSecond);
        if (started)
        {
            _host.RegisterWorldSoundEvent("BuffbannerSnd", context.Player.X, context.Player.Y, context.Player.Id);
        }

        return new GameplayAbilityResult(Handled: started, ConsumedInput: true);
    }

    internal static GameplayAbilityResult ExecuteScoutNailgunToggleAbility(GameplayAbilityContext context)
    {
        // Legacy compatibility definition only. Alternate primaries are selected at a swap
        // station and secondary weapons are selected through the dedicated swap input.
        _ = context;
        return GameplayAbilityResult.Ignored;
    }

    internal static GameplayAbilityResult ExecuteSniperBowToggleAbility(GameplayAbilityContext context)
    {
        // Legacy compatibility definition only. Weapon swaps perform their own scope and bow
        // cleanup through the dedicated swap path.
        _ = context;
        return GameplayAbilityResult.Ignored;
    }

    internal GameplayPrimaryWeaponResult ExecuteScoutNailgunPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        _host.WeaponHandler.FireScoutNailgun(context.Player, context.Weapon, context.AimWorldX, context.AimWorldY);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    internal GameplayPrimaryWeaponResult ExecuteFlaregunPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        _host.WeaponHandler.FireFlaregun(
            context.Player,
            context.Weapon,
            context.AimWorldX,
            context.AimWorldY,
            context.KillFeedWeaponSpriteName);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    internal GameplayPrimaryWeaponResult ExecuteDragonRagePrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        _host.WeaponHandler.FireDragonRage(
            context.Player,
            context.Weapon,
            context.AimWorldX,
            context.AimWorldY,
            context.KillFeedWeaponSpriteName);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    internal GameplayPrimaryWeaponResult ExecuteBoomstickPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        _host.WeaponHandler.FireBoomstick(
            context.Player,
            context.Weapon,
            context.WeaponClassId,
            context.AimWorldX,
            context.AimWorldY,
            context.KillFeedWeaponSpriteName);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    internal GameplayPrimaryWeaponResult ExecuteNeedlegunPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        _host.WeaponHandler.FireMedicNeedlegun(context.Player, context.Weapon, context.AimWorldX, context.AimWorldY);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    internal GameplayPrimaryWeaponResult ExecuteSniperBowPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        if (!context.Player.TryReleaseSniperBowCharge(out var velocityX, out var velocityY, out var damage, out var fakeSpeedMultiplier))
        {
            return GameplayPrimaryWeaponResult.Ignored;
        }

        if (!context.Player.TryFireExperimentalOffhandWeapon())
        {
            return GameplayPrimaryWeaponResult.Ignored;
        }

        _host.WeaponHandler.FireSniperBow(
            context.Player,
            context.Weapon,
            context.AimWorldX,
            context.AimWorldY,
            velocityX,
            velocityY,
            damage,
            fakeSpeedMultiplier,
            context.KillFeedWeaponSpriteName);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    internal GameplayAbilityResult ExecuteEngineerJumpPadAbility(GameplayAbilityContext context)
    {
        // Retained ability path for future loadouts / non-menu callers.
        // The Constructor build menu now uses BuildJumpPad / DestroyJumpPad
        // structure commands instead of routing through UseAbility.
        if (!_host.TryDestroyJumpPad(context.Player))
        {
            _host.TryBuildJumpPad(context.Player);
        }

        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal GameplayAbilityResult ExecuteHeavyGhostDashAbility(GameplayAbilityContext context)
    {
        var durationTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "durationTicks",
            "durationSeconds",
            _host.GetHeavyGhostDashDurationTicks(),
            _host.Config.TicksPerSecond);
        var cooldownTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "cooldownTicks",
            "cooldownSeconds",
            _host.GetHeavyGhostDashCooldownTicks(),
            _host.Config.TicksPerSecond);
        var movementTicks = GameplayAbilityParameterReader.GetTicks(
            context.Ability,
            "movementDurationTicks",
            "movementDurationSeconds",
            _host.GetHeavyGhostDashMovementDurationTicks(),
            _host.Config.TicksPerSecond);
        var impulse = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "impulse",
            _host.GetHeavyGhostDashImpulse(),
            minValue: 0f);
        var nextAttackDamageMultiplier = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "nextAttackDamageMultiplier",
            ExperimentalGameplaySettings.DefaultGhostDashNextAttackDamageMultiplier,
            minValue: 1.0001f);
        var hasBurstParameters = context.Ability.Parameters.ContainsKey("burstSpeedMultiplier")
            || context.Ability.Parameters.ContainsKey("disableGravity")
            || context.Ability.Parameters.ContainsKey("enableGhostTrail");
        var forceStockBurstDash = IsStockHeavyGhostDashUtility(context.Item, context.Ability);
        var useMomentum = GameplayAbilityParameterReader.GetBool(
            context.Ability,
            "useMomentum",
            defaultValue: !hasBurstParameters);
        if (forceStockBurstDash)
        {
            useMomentum = false;
        }

        var slideVelocityPerTick = GameplayAbilityParameterReader.GetFloat(
            context.Ability,
            "slideVelocityPerTick",
            ExperimentalGameplaySettings.HeavyGhostDashSlideVelocityPerTick,
            minValue: 0f);
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
                dashImpulse: useMomentum ? impulse : 0f,
                requireExperimentalDemoknight: false,
                useMomentum: useMomentum,
                movementTicks: useMomentum ? movementTicks : 0,
                slideVelocityPerTick: slideVelocityPerTick,
                burstSpeedMultiplier: burstSpeedMultiplier,
                disableGravity: disableGravity,
                enableGhostTrail: enableGhostTrail))
        {
            return new GameplayAbilityResult(Handled: false, ConsumedInput: true);
        }

        if (!useMomentum && context.Player.ExperimentalGhostDashBurstSpeedMultiplier > 0f)
        {
            var burstSpeed = LegacyMovementModel.GetMaxRunSpeed(context.Player.RunPower) * context.Player.ExperimentalGhostDashBurstSpeedMultiplier;
            context.Player.ApplyVelocityImpulse(
                context.Player.FacingDirectionX >= 0f ? burstSpeed : -burstSpeed,
                velocityY: 0f);
        }

        _host.RegisterWorldSoundEvent(ExperimentalDemoknightCatalog.ChargeStartSoundName, context.Player.X, context.Player.Y);
        return GameplayAbilityResult.HandledAndConsumed;
    }

    internal static bool IsStockHeavyGhostDashUtility(GameplayItemDefinition item, GameplayAbilityDefinition ability)
    {
        return string.Equals(item.Id, StockGameplayModCatalog.HeavyUtilityItemId, StringComparison.Ordinal)
            && string.Equals(item.BehaviorId, BuiltInGameplayBehaviorIds.HeavyUtility, StringComparison.Ordinal)
            && string.Equals(ability.ExecutorId, BuiltInGameplayBehaviorIds.HeavyGhostDash, StringComparison.Ordinal);
    }
}
