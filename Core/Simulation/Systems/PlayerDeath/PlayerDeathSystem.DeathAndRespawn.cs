using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal sealed partial class PlayerDeathSystem
{
    internal void KillPlayer(
        PlayerEntity player,
        bool gibbed = false,
        PlayerEntity? killer = null,
        string? weaponSpriteName = null,
        DeadBodyAnimationKind deadBodyAnimationKind = DeadBodyAnimationKind.Default,
        string? deathCamMessage = null,
        SentryEntity? deathCamSentry = null,
        string? killFeedMessage = null,
        bool createDeathCam = true,
        bool spawnRemains = true,
        bool forceCorpseRemains = false,
        bool recordKillFeed = true,
        int assistingPlayerIdOverride = -1,
        bool completingLastToDieSpyAfterlifeDeath = false)
    {
        if (!completingLastToDieSpyAfterlifeDeath
            && player.IsLastToDieSpyAfterlifeActive)
        {
            return;
        }

        if (!completingLastToDieSpyAfterlifeDeath
            && player.IsAlive
            && player.IsExperimentalLuckyBastardActive)
        {
            return;
        }

        if (!completingLastToDieSpyAfterlifeDeath && forceCorpseRemains)
        {
            gibbed = false;
        }
        else if (!completingLastToDieSpyAfterlifeDeath && player.IsExperimentalCryoFrozen)
        {
            gibbed = true;
        }

        if (!completingLastToDieSpyAfterlifeDeath
            && !_host.TryBeginPlayerDeath(player, gibbed, killer, weaponSpriteName))
        {
            return;
        }

        var originalKillerWasSelf = killer is not null && ReferenceEquals(killer, player);
        if (!completingLastToDieSpyAfterlifeDeath
            && (killer is null || originalKillerWasSelf)
            && player.Health <= 0
            && TryResolveAfterburnDeathCredit(player, out var afterburnKiller))
        {
            killer = afterburnKiller;
            weaponSpriteName = player.AfterburnKillFeedWeaponSpriteName;
            if (originalKillerWasSelf && string.IsNullOrEmpty(killFeedMessage))
            {
                killFeedMessage = " finished off ";
            }
        }

        var hasPinnedAssistingPlayer = completingLastToDieSpyAfterlifeDeath && assistingPlayerIdOverride > 0;
        PlayerEntity? assistingPlayer;
        if (hasPinnedAssistingPlayer)
        {
            assistingPlayer = _host.FindPlayerById(assistingPlayerIdOverride);
            if (assistingPlayer is not null
                && (assistingPlayer.Id == player.Id
                    || assistingPlayer.Id == killer?.Id))
            {
                assistingPlayer = null;
            }
        }
        else if (!completingLastToDieSpyAfterlifeDeath)
        {
            assistingPlayer = killer is not null && !ReferenceEquals(killer, player)
                ? _host.Combat.ResolveAssistPlayer(player, killer)
                : null;
        }
        else
        {
            assistingPlayer = null;
        }

        if (!completingLastToDieSpyAfterlifeDeath
            && _host.LastToDieRules.TryActivateLastToDieSecondChance(player))
        {
            return;
        }

        if (!completingLastToDieSpyAfterlifeDeath
            && _host.LastToDieRules.TryStartLastToDieSpyAfterlife(
                player,
                gibbed,
                killer,
                weaponSpriteName,
                deadBodyAnimationKind,
                deathCamMessage,
                deathCamSentry,
                killFeedMessage,
                createDeathCam,
                spawnRemains,
                forceCorpseRemains,
                recordKillFeed,
                assistingPlayer?.Id ?? -1))
        {
            return;
        }

        _host.VipRules.ApplyVipDeathTimerPenalty(player, killer);

        player.AddDeath();
        if (gibbed)
        {
            player.AddGibDeath();
            _host.Combat.MarkPendingFatalPlayerDamageEventGibbed(player.Id);
        }

        if (killer is not null && !ReferenceEquals(killer, player))
        {
            killer.AddKill();
            _host.LastToDieRules.ApplyLastToDieKillRewards(killer);
            _host.LastToDieRules.TryCompleteLastToDieSpyAfterlifeSuccess(killer, player);
            _host.LastToDieRules.TryRegisterLastToDieSniperConquistadorKill(killer, player);
            _host.CombatFeedback.TryRegisterKillStreakKill(killer, player);
            _host.Scorekeeping.AwardKillPoints(player, killer, weaponSpriteName);
            if (hasPinnedAssistingPlayer)
            {
                AwardPinnedAssistPoints(assistingPlayer, player, killer);
            }
            else
            {
                _host.Scorekeeping.AwardAssistPoints(assistingPlayer, player, killer);
            }
            _host.ExperimentalRules.ApplyExperimentalKillRewards(killer, player);
            _host.Pickups.TrySpawnExperimentalEnemyDroppedWeapon(player, killer);

            if (_host.MatchRules.Mode == GameModeKind.TeamDeathmatch && killer.Team != player.Team)
            {
                _host.Decisions.TryAwardTeamScore(killer.Team, 1, "team_deathmatch_kill", killer.Id);
            }
        }

        _host.Pickups.TrySpawnExperimentalEnemyHealthPackDrop(player, killer);

        if (player.IsCarryingIntel)
        {
            _host.ObjectiveRules.GetEnemyIntelState(player.Team).Drop(
                player.X,
                player.Y,
                ObjectiveRulesSystem.GetPlayerIntelReturnTicks(player));
            player.DropIntel(ObjectiveRulesSystem.IntelPickupCooldownTicksAfterDrop);
            _host.WorldEffects.RegisterWorldSoundEvent("IntelDropSnd", player.X, player.Y);
            _host.KillFeedRules.RecordIntelDroppedObjectiveLog(player);
            if (killer is not null && !ReferenceEquals(killer, player))
            {
                _host.KillFeedRules.RecordIntelDefendedObjectiveLog(killer);
            }
        }

        if (!spawnRemains)
        {
        }
        else if (gibbed)
        {
            _host.PlayerRemains.SpawnPlayerGibs(player);
            _host.WorldEffects.RegisterWorldSoundEvent("Gibbing", player.X, player.Y);
            _host.ExplosionRules.TryTriggerExperimentalDangerCloseExplosion(player, killer);
        }
        else
        {
            SpawnDeadBody(player, deadBodyAnimationKind, killer, weaponSpriteName, deathCamSentry);
            _host.WorldEffects.RegisterWorldSoundEvent(_host.Randoms.Gameplay.Next(2) == 0 ? "DeathSnd1" : "DeathSnd2", player.X, player.Y);
        }

        if (recordKillFeed)
        {
            _host.KillFeedRules.RecordKillFeedEntry(player, killer, weaponSpriteName ?? "DeadKL", killFeedMessage, assistingPlayer: assistingPlayer);
        }

        if (killer is not null && !ReferenceEquals(killer, player))
        {
            _host.CombatFeedback.UpdateDominationStateForKill(player, killer, assistingPlayer);
        }

        var respawnTicks = _host.MatchRules.Mode == GameModeKind.Arena
            ? 0
            : player.IsInSpawnRoom
                ? 1
                : _host.MatchSettings.RespawnTicks;
        var hasNetworkSlot = _host.NetworkPlayerRules.TryGetNetworkPlayerSlot(player, out var slot);

        var shouldCreateDeathCam = createDeathCam
            && hasNetworkSlot
            && !_host.PlayerRegistry.AutomaticRespawnSuppressedSlots.Contains(slot)
            && (deathCamSentry is not null || (killer is not null && !ReferenceEquals(killer, player)));
        if (shouldCreateDeathCam)
        {
            var deathCamTicks = Math.Clamp(respawnTicks > 0 ? respawnTicks : _host.MatchSettings.RespawnTicks, 1, 150);
            var resolvedDeathCamMessage = deathCamMessage
                ?? DeathCamPhraseCatalog.ChoosePhrase(_host.Randoms.DeathCamPhrase, weaponSpriteName, deathCamSentry is not null);
            LocalDeathCamState deathCam;
            if (deathCamSentry is not null)
            {
                deathCam = new LocalDeathCamState(
                    deathCamSentry.X,
                    deathCamSentry.Y,
                    resolvedDeathCamMessage,
                    killer?.DisplayName ?? string.Empty,
                    killer?.Team,
                    deathCamSentry.Health,
                    deathCamSentry.MaxHealth,
                    deathCamTicks,
                    deathCamTicks);
            }
            else if (killer is not null)
            {
                deathCam = new LocalDeathCamState(
                    killer.X,
                    killer.Y,
                    resolvedDeathCamMessage,
                    killer.DisplayName,
                    killer.Team,
                    killer.Health,
                    killer.MaxHealth,
                    deathCamTicks,
                    deathCamTicks,
                    killer.Id);
            }
            else
            {
                deathCam = new LocalDeathCamState(
                    player.X,
                    player.Y,
                    resolvedDeathCamMessage,
                    string.Empty,
                    null,
                    0,
                    0,
                    deathCamTicks,
                    deathCamTicks);
            }

            SetNetworkPlayerDeathCam(slot, deathCam);
        }

        _host.Projectiles.RemoveOwnedSpyArtifacts(player.Id);

        // Remove demoman mines on death without exploding them
        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MineLauncher))
        {
            _host.Projectiles.RemoveOwnedMines(player.Id);
        }

        player.Kill();
        _host.LastToDieRules.ClearLastToDieStatusEffectsForTarget(player.Id);
        if (hasNetworkSlot)
        {
            _host.LastToDieRules.ClearLastToDieSniperMarksTargeting(slot);
            _host.LastToDieRules.ResetLastToDiePerkRuntimeOnDeath(slot);
            _host.NetworkPlayerRules.TrySetNetworkPlayerRespawnTicks(slot, respawnTicks);
        }
        else if (ReferenceEquals(player, _host.EnemyPlayer))
        {
            _host.DummyState.EnemyRespawnTicks = respawnTicks;
        }

        foreach (var otherPlayer in _host.EnumerateSimulatedPlayers())
        {
            if (otherPlayer.MedicHealTargetId == player.Id)
            {
                otherPlayer.ClearMedicHealingTarget();
            }
        }
    }

    private void AwardPinnedAssistPoints(
        PlayerEntity? assistant,
        PlayerEntity victim,
        PlayerEntity killer)
    {
        if (!_host.Scorekeeping.ShouldAwardRoundPoints()
            || assistant is null
            || assistant.Id == victim.Id
            || assistant.Id == killer.Id)
        {
            return;
        }

        assistant.AddAssist();
        assistant.AddPoints(ScorekeepingSystem.AssistPointValue);
    }

    private bool TryResolveAfterburnDeathCredit(PlayerEntity victim, out PlayerEntity killer)
    {
        killer = null!;
        if (!victim.BurnedByPlayerId.HasValue
            || !victim.IsBurning
            || _host.FindPlayerById(victim.BurnedByPlayerId.Value) is not { } burner
            || ReferenceEquals(burner, victim)
            || burner.Team == victim.Team)
        {
            return false;
        }

        killer = burner;
        return true;
    }

    internal void AdvanceLocalDeathCam()
    {
        if (_host.LocalDeathCam is null)
        {
            AdvanceAdditionalNetworkDeathCams();
            return;
        }

        if (_host.LocalDeathCam.RemainingTicks <= 1)
        {
            _host.LocalDeathCam = null;
            AdvanceAdditionalNetworkDeathCams();
            return;
        }

        _host.LocalDeathCam = AdvanceDeathCamState(_host.LocalDeathCam);
        AdvanceAdditionalNetworkDeathCams();
    }

    private void AdvanceAdditionalNetworkDeathCams()
    {
        if (_host.PlayerRegistry.DeathCams.Count == 0)
        {
            return;
        }

        var staleSlots = new List<byte>();
        foreach (var entry in _host.PlayerRegistry.DeathCams)
        {
            if (entry.Value.RemainingTicks <= 1)
            {
                staleSlots.Add(entry.Key);
                continue;
            }

            _host.PlayerRegistry.DeathCams[entry.Key] = AdvanceDeathCamState(entry.Value);
        }

        for (var index = 0; index < staleSlots.Count; index += 1)
        {
            _host.PlayerRegistry.DeathCams.Remove(staleSlots[index]);
        }
    }

    internal void SetNetworkPlayerDeathCam(byte slot, LocalDeathCamState? deathCam)
    {
        if (slot == SimulationConstants.LocalPlayerSlot)
        {
            _host.LocalDeathCam = deathCam;
            return;
        }

        if (deathCam is null)
        {
            _host.PlayerRegistry.DeathCams.Remove(slot);
            return;
        }

        _host.PlayerRegistry.DeathCams[slot] = deathCam;
    }

    private LocalDeathCamState AdvanceDeathCamState(LocalDeathCamState deathCam)
    {
        var advanced = deathCam with { RemainingTicks = deathCam.RemainingTicks - 1 };
        return IsDeathCamFocusFrozen(advanced)
            ? advanced
            : ResolveTrackedDeathCamFocus(advanced);
    }

    internal LocalDeathCamState ResolveTrackedDeathCamFocus(LocalDeathCamState deathCam)
    {
        if (IsDeathCamFocusFrozen(deathCam)
            || deathCam.FocusPlayerId <= 0
            || _host.FindPlayerById(deathCam.FocusPlayerId) is not { } focusPlayer)
        {
            return deathCam;
        }

        return deathCam with
        {
            FocusX = focusPlayer.X,
            FocusY = focusPlayer.Y,
        };
    }

    private static bool IsDeathCamFocusFrozen(LocalDeathCamState deathCam)
    {
        var initialTicks = deathCam.InitialTicks > 0
            ? deathCam.InitialTicks
            : deathCam.RemainingTicks;
        return Math.Max(0, initialTicks - deathCam.RemainingTicks) >= SimulationConstants.DeathCamFocusFreezeDelayTicks;
    }

    internal void AdvanceNetworkRespawnTimer(byte slot)
    {
        if (_host.NetworkPlayerRules.IsNetworkPlayerAwaitingJoin(slot)
            || _host.PlayerRegistry.AutomaticRespawnSuppressedSlots.Contains(slot)
            || !_host.NetworkPlayerRules.TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        if (_host.MatchRules.Mode == GameModeKind.Arena)
        {
            return;
        }

        var respawnTicks = _host.NetworkPlayerRules.GetNetworkPlayerRespawnTicks(slot);
        if (respawnTicks > 0)
        {
            respawnTicks -= 1;
            _host.NetworkPlayerRules.TrySetNetworkPlayerRespawnTicks(slot, respawnTicks);
        }

        if (respawnTicks > 0)
        {
            return;
        }

        _host.Spawns.RespawnConfiguredNetworkPlayer(slot, player);
    }

    internal void AdvanceEnemyDummyRespawnTimer()
    {
        if (!_host.EnemyPlayerEnabled)
        {
            return;
        }

        if (_host.MatchRules.Mode == GameModeKind.Arena)
        {
            return;
        }

        if (_host.DummyState.EnemyRespawnTicks > 0)
        {
            _host.DummyState.EnemyRespawnTicks -= 1;
        }

        if (_host.DummyState.EnemyRespawnTicks > 0)
        {
            return;
        }

        if (_host.DummyState.CombatMode != PracticeCombatDummyMode.None)
        {
            _host.PracticeDummies.SpawnPracticeCombatDummyResolved(playRespawnSound: true);
            return;
        }

        _host.EnemyPlayer.SetClassDefinition(_host.DummyState.EnemyClassDefinition);
        _host.Spawns.SpawnPlayerResolved(_host.EnemyPlayer, _host.DummyState.EnemyTeam, _host.Spawns.ReserveSpawn(_host.EnemyPlayer, _host.DummyState.EnemyTeam), playRespawnSound: true);
    }

    internal void SpawnDeadBody(
        PlayerEntity player,
        DeadBodyAnimationKind animationKind = DeadBodyAnimationKind.Default,
        PlayerEntity? killer = null,
        string? weaponSpriteName = null,
        SentryEntity? knockbackOriginSentry = null)
    {
        if (!player.IsAlive)
        {
            return;
        }

        var horizontalSpeed = player.HorizontalSpeed * (float)_host.Config.FixedDeltaSeconds;
        var verticalSpeed = player.VerticalSpeed * (float)_host.Config.FixedDeltaSeconds;
        var diedToFire = player.IsBurning || DeathCamPhraseCatalog.IsFireWeapon(weaponSpriteName);
        if (!diedToFire && killer is not null && !ReferenceEquals(killer, player))
        {
            // Prefer shot origin (sentry barrel / killer) so remains fly away from the shot, not a distant owner.
            var originX = knockbackOriginSentry?.X ?? killer.X;
            var originY = knockbackOriginSentry?.Y ?? killer.Y;
            var knockbackSpeed = CorpseKnockbackRules.ResolveSpeed(weaponSpriteName, killer.ClassId);
            // Opposite of facing — used only when origin coincides with the corpse.
            var facingFallbackSign = player.FacingDirectionX >= 0f ? -1f : 1f;
            CorpseKnockbackRules.ApplyDirectedLaunch(
                ref horizontalSpeed,
                ref verticalSpeed,
                player.X,
                player.Y,
                originX,
                originY,
                knockbackSpeed,
                facingFallbackSign);
        }
        else if (!diedToFire)
        {
            verticalSpeed -= 1.2f;
        }

        var deadBody = new DeadBodyEntity(
            _host.AllocateEntityId(),
            player.Id,
            player.ClassId,
            player.Team,
            animationKind,
            player.X,
            player.Y,
            player.Width,
            player.Height,
            horizontalSpeed,
            verticalSpeed,
            DeterministicMath.Cos(player.AimDirectionDegrees * (MathF.PI / 180f)) < 0f,
            player.GameplayClassId,
            diedToFire);
        _host.WorldObjects.DeadBodies.Add(deadBody);
        _host.EntityStore.Add(deadBody);
    }
}
