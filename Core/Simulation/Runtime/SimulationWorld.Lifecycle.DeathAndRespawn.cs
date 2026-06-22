using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void KillPlayer(
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
        bool recordKillFeed = true)
    {
        if (player.IsAlive && player.IsExperimentalLuckyBastardActive)
        {
            return;
        }

        if (forceCorpseRemains)
        {
            gibbed = false;
        }
        else if (player.IsExperimentalCryoFrozen)
        {
            gibbed = true;
        }

        if (ShouldCancelDeath(player, gibbed, killer, weaponSpriteName))
        {
            return;
        }

        if ((killer is null || ReferenceEquals(killer, player))
            && player.Health <= 0
            && TryResolveAfterburnDeathCredit(player, out var afterburnKiller))
        {
            killer = afterburnKiller;
            weaponSpriteName = "FlameKL";
        }

        var assistingPlayer = killer is not null && !ReferenceEquals(killer, player)
            ? ResolveAssistPlayer(player, killer)
            : null;

        ApplyVipDeathTimerPenalty(player, killer);

        player.AddDeath();
        if (gibbed)
        {
            player.AddGibDeath();
        }

        if (killer is not null && !ReferenceEquals(killer, player))
        {
            killer.AddKill();
            TryRegisterKillStreakKill(killer, player);
            AwardKillPoints(player, killer, weaponSpriteName);
            AwardAssistPoints(assistingPlayer, player, killer);
            ApplyExperimentalKillRewards(killer, player);
            TrySpawnExperimentalEnemyHealthPackDrop(player, killer);
            TrySpawnExperimentalEnemyDroppedWeapon(player, killer);

            if (MatchRules.Mode == GameModeKind.TeamDeathmatch && killer.Team != player.Team)
            {
                TryAwardTeamScore(killer.Team, 1, "team_deathmatch_kill", killer.Id);
            }
        }

        if (player.IsCarryingIntel)
        {
            GetEnemyIntelState(player.Team).Drop(
                player.X,
                player.Y,
                GetPlayerIntelReturnTicks(player));
            player.DropIntel(IntelPickupCooldownTicksAfterDrop);
            RegisterWorldSoundEvent("IntelDropSnd", player.X, player.Y);
            RecordIntelDroppedObjectiveLog(player);
            if (killer is not null && !ReferenceEquals(killer, player))
            {
                RecordIntelDefendedObjectiveLog(killer);
            }
        }

        if (!spawnRemains)
        {
        }
        else if (gibbed)
        {
            SpawnPlayerGibs(player);
            RegisterWorldSoundEvent("Gibbing", player.X, player.Y);
            TryTriggerExperimentalDangerCloseExplosion(player, killer);
        }
        else
        {
            SpawnDeadBody(player, deadBodyAnimationKind);
            RegisterWorldSoundEvent(_random.Next(2) == 0 ? "DeathSnd1" : "DeathSnd2", player.X, player.Y);
        }

        if (recordKillFeed)
        {
            RecordKillFeedEntry(player, killer, weaponSpriteName ?? "DeadKL", killFeedMessage);
        }

        if (killer is not null && !ReferenceEquals(killer, player))
        {
            UpdateDominationStateForKill(player, killer);
        }

        var respawnTicks = MatchRules.Mode == GameModeKind.Arena
            ? 0
            : player.IsInSpawnRoom
                ? 1
                : _configuredRespawnTicks;
        var hasNetworkSlot = TryGetNetworkPlayerSlot(player, out var slot);

        var shouldCreateDeathCam = createDeathCam
            && hasNetworkSlot
            && (deathCamSentry is not null || (killer is not null && !ReferenceEquals(killer, player)));
        if (shouldCreateDeathCam)
        {
            var deathCamTicks = Math.Clamp(respawnTicks > 0 ? respawnTicks : _configuredRespawnTicks, 1, 150);
            LocalDeathCamState deathCam;
            if (deathCamSentry is not null)
            {
                deathCam = new LocalDeathCamState(
                    deathCamSentry.X,
                    deathCamSentry.Y,
                    deathCamMessage ?? "You were killed by the autogun of",
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
                    deathCamMessage ?? "You were killed by",
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
                    deathCamMessage ?? "You were killed by the late",
                    string.Empty,
                    null,
                    0,
                    0,
                    deathCamTicks,
                    deathCamTicks);
            }

            SetNetworkPlayerDeathCam(slot, deathCam);
        }

        RemoveOwnedSpyArtifacts(player.Id);

        // Remove demoman mines on death without exploding them
        if (player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.MineLauncher))
        {
            RemoveOwnedMines(player.Id);
        }

        player.Kill();
        if (hasNetworkSlot)
        {
            TrySetNetworkPlayerRespawnTicks(slot, respawnTicks);
        }
        else if (ReferenceEquals(player, EnemyPlayer))
        {
            _enemyDummyRespawnTicks = respawnTicks;
        }

        foreach (var otherPlayer in EnumerateSimulatedPlayers())
        {
            if (otherPlayer.MedicHealTargetId == player.Id)
            {
                otherPlayer.ClearMedicHealingTarget();
            }
        }
    }

    private bool TryResolveAfterburnDeathCredit(PlayerEntity victim, out PlayerEntity killer)
    {
        killer = null!;
        if (!victim.BurnedByPlayerId.HasValue
            || !victim.IsBurning
            || FindPlayerById(victim.BurnedByPlayerId.Value) is not { } burner
            || ReferenceEquals(burner, victim)
            || burner.Team == victim.Team)
        {
            return false;
        }

        killer = burner;
        return true;
    }

    private void AdvanceLocalDeathCam()
    {
        if (LocalDeathCam is null)
        {
            AdvanceAdditionalNetworkDeathCams();
            return;
        }

        if (LocalDeathCam.RemainingTicks <= 1)
        {
            LocalDeathCam = null;
            AdvanceAdditionalNetworkDeathCams();
            return;
        }

        LocalDeathCam = AdvanceDeathCamState(LocalDeathCam);
        AdvanceAdditionalNetworkDeathCams();
    }

    private void AdvanceAdditionalNetworkDeathCams()
    {
        if (_networkPlayerDeathCams.Count == 0)
        {
            return;
        }

        var staleSlots = new List<byte>();
        foreach (var entry in _networkPlayerDeathCams)
        {
            if (entry.Value.RemainingTicks <= 1)
            {
                staleSlots.Add(entry.Key);
                continue;
            }

            _networkPlayerDeathCams[entry.Key] = AdvanceDeathCamState(entry.Value);
        }

        for (var index = 0; index < staleSlots.Count; index += 1)
        {
            _networkPlayerDeathCams.Remove(staleSlots[index]);
        }
    }

    private void SetNetworkPlayerDeathCam(byte slot, LocalDeathCamState? deathCam)
    {
        if (slot == LocalPlayerSlot)
        {
            LocalDeathCam = deathCam;
            return;
        }

        if (deathCam is null)
        {
            _networkPlayerDeathCams.Remove(slot);
            return;
        }

        _networkPlayerDeathCams[slot] = deathCam;
    }

    private LocalDeathCamState AdvanceDeathCamState(LocalDeathCamState deathCam)
    {
        return ResolveTrackedDeathCamFocus(deathCam with { RemainingTicks = deathCam.RemainingTicks - 1 });
    }

    private LocalDeathCamState ResolveTrackedDeathCamFocus(LocalDeathCamState deathCam)
    {
        if (deathCam.FocusPlayerId <= 0 || FindPlayerById(deathCam.FocusPlayerId) is not { } focusPlayer)
        {
            return deathCam;
        }

        return deathCam with
        {
            FocusX = focusPlayer.X,
            FocusY = focusPlayer.Y,
        };
    }

    private void AdvanceNetworkRespawnTimer(byte slot)
    {
        if (IsNetworkPlayerAwaitingJoin(slot)
            || !TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        if (MatchRules.Mode == GameModeKind.Arena && !MatchState.IsEnded)
        {
            return;
        }

        var respawnTicks = GetNetworkPlayerRespawnTicks(slot);
        if (respawnTicks > 0)
        {
            respawnTicks -= 1;
            TrySetNetworkPlayerRespawnTicks(slot, respawnTicks);
        }

        if (respawnTicks > 0)
        {
            return;
        }

        RespawnConfiguredNetworkPlayer(slot, player);
    }

    private void AdvanceEnemyDummyRespawnTimer()
    {
        if (!EnemyPlayerEnabled)
        {
            return;
        }

        if (MatchRules.Mode == GameModeKind.Arena && !MatchState.IsEnded)
        {
            return;
        }

        if (_enemyDummyRespawnTicks > 0)
        {
            _enemyDummyRespawnTicks -= 1;
        }

        if (_enemyDummyRespawnTicks > 0)
        {
            return;
        }

        if (_practiceCombatDummyMode != PracticeCombatDummyMode.None)
        {
            SpawnPracticeCombatDummyResolved(playRespawnSound: true);
            return;
        }

        EnemyPlayer.SetClassDefinition(_enemyDummyClassDefinition);
        SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, ReserveSpawn(EnemyPlayer, _enemyDummyTeam), playRespawnSound: true);
    }

    private void SpawnDeadBody(PlayerEntity player, DeadBodyAnimationKind animationKind = DeadBodyAnimationKind.Default)
    {
        if (!player.IsAlive)
        {
            return;
        }

        var deadBody = new DeadBodyEntity(
            AllocateEntityId(),
            player.Id,
            player.ClassId,
            player.Team,
            animationKind,
            player.X,
            player.Y,
            player.Width,
            player.Height,
            player.HorizontalSpeed * (float)Config.FixedDeltaSeconds,
            player.VerticalSpeed * (float)Config.FixedDeltaSeconds,
            MathF.Cos(player.AimDirectionDegrees * (MathF.PI / 180f)) < 0f);
        _deadBodies.Add(deadBody);
        _entities.Add(deadBody.Id, deadBody);
    }
}
