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
        bool spawnRemains = true)
    {
        var assistingPlayer = killer is not null && !ReferenceEquals(killer, player)
            ? ResolveAssistPlayer(player, killer)
            : null;

        player.AddDeath();
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
                if (killer.Team == PlayerTeam.Red)
                {
                    RedCaps += 1;
                }
                else if (killer.Team == PlayerTeam.Blue)
                {
                    BlueCaps += 1;
                }
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
        }
        else
        {
            SpawnDeadBody(player, deadBodyAnimationKind);
            RegisterWorldSoundEvent(_random.Next(2) == 0 ? "DeathSnd1" : "DeathSnd2", player.X, player.Y);
        }

        RecordKillFeedEntry(player, killer, weaponSpriteName ?? "DeadKL", killFeedMessage);
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
                    SentryEntity.MaxHealth,
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
                    deathCamTicks);
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

        LocalDeathCam = LocalDeathCam with { RemainingTicks = LocalDeathCam.RemainingTicks - 1 };
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

            _networkPlayerDeathCams[entry.Key] = entry.Value with { RemainingTicks = entry.Value.RemainingTicks - 1 };
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

    private void AdvanceKillFeed()
    {
        if (_killFeed.Count == 0)
        {
            _killFeedTrimTicks = 0;
            return;
        }

        if (_killFeedTrimTicks > 0)
        {
            _killFeedTrimTicks -= 1;
        }

        if (_killFeedTrimTicks > 0)
        {
            return;
        }

        _killFeed.RemoveAt(0);
        _killFeedTrimTicks = _killFeed.Count > 0 ? KillFeedLifetimeTicks : 0;
    }

    private void RecordKillFeedEntry(
        PlayerEntity victim,
        PlayerEntity? killer,
        string weaponSpriteName,
        string? messageText = null,
        int messageHighlightStart = 0,
        int messageHighlightLength = 0,
        KillFeedSpecialType specialType = KillFeedSpecialType.None)
    {
        var isSelfKill = killer is not null && ReferenceEquals(killer, victim);
        var resolvedMessageText = messageText ?? string.Empty;
        var victimName = isSelfKill && resolvedMessageText.Length > 0
            ? string.Empty
            : victim.DisplayName;

        var entry = killer is null || isSelfKill
            ? new KillFeedEntry(
                string.Empty,
                victim.Team,
                weaponSpriteName,
                victimName,
                victim.Team,
                resolvedMessageText,
                messageHighlightStart,
                messageHighlightLength,
                KillerPlayerId: -1,
                VictimPlayerId: victim.Id,
                SpecialType: specialType,
                EventId: _nextKillFeedEventId++)
            : new KillFeedEntry(
                killer.DisplayName,
                killer.Team,
                weaponSpriteName,
                victimName,
                victim.Team,
                resolvedMessageText,
                messageHighlightStart,
                messageHighlightLength,
                KillerPlayerId: killer.Id,
                VictimPlayerId: victim.Id,
                SpecialType: specialType,
                EventId: _nextKillFeedEventId++);
        AppendKillFeedEntry(entry);
    }

    private bool ShouldSuppressDuplicateKillFeedEntry(KillFeedEntry entry)
    {
        if (_killFeed.Count == 0 || _lastKillFeedRecordedFrame != Frame)
        {
            return false;
        }

        var previousEntry = _killFeed[^1];
        return previousEntry.KillerName == entry.KillerName
            && previousEntry.KillerTeam == entry.KillerTeam
            && previousEntry.WeaponSpriteName == entry.WeaponSpriteName
            && previousEntry.VictimName == entry.VictimName
            && previousEntry.VictimTeam == entry.VictimTeam
            && previousEntry.MessageText == entry.MessageText
            && previousEntry.MessageHighlightStart == entry.MessageHighlightStart
            && previousEntry.MessageHighlightLength == entry.MessageHighlightLength
            && previousEntry.KillerPlayerId == entry.KillerPlayerId
            && previousEntry.VictimPlayerId == entry.VictimPlayerId
            && previousEntry.SpecialType == entry.SpecialType;
    }

    private void AppendKillFeedEntry(KillFeedEntry entry)
    {
        if (ShouldSuppressDuplicateKillFeedEntry(entry))
        {
            return;
        }

        _killFeed.Add(entry);
        _lastKillFeedRecordedFrame = Frame;
        if (_killFeed.Count > 5)
        {
            _killFeed.RemoveAt(0);
        }

        _killFeedTrimTicks = KillFeedLifetimeTicks;
    }

    private void RecordObjectiveLogEntry(PlayerTeam team, string name, string messageText, string weaponSpriteName = "", int playerId = -1)
    {
        AppendKillFeedEntry(new KillFeedEntry(
            name,
            team,
            weaponSpriteName,
            string.Empty,
            team,
            messageText,
            0,
            0,
            playerId,
            -1,
            KillFeedSpecialType.None,
            _nextKillFeedEventId++));
    }

    private void RecordKillFeedAnnouncement(PlayerEntity player, string prefix, string highlightedText, string suffix, string weaponSpriteName = "")
    {
        if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(highlightedText))
        {
            return;
        }

        var messageText = prefix + highlightedText + suffix;
        AppendKillFeedEntry(new KillFeedEntry(
            player.DisplayName,
            player.Team,
            weaponSpriteName,
            string.Empty,
            player.Team,
            messageText,
            prefix.Length,
            highlightedText.Length,
            player.Id,
            -1,
            KillFeedSpecialType.None,
            _nextKillFeedEventId++));
    }

    private void RecordControlPointCapturedObjectiveLog(PlayerTeam team, IReadOnlyCollection<int> capperIds)
    {
        RecordObjectiveLogEntry(
            team,
            BuildPlayerNameList(capperIds, team),
            "captured the point!",
            team == PlayerTeam.Blue ? "BlueCaptureS" : "RedCaptureS");
    }

    private void RecordControlPointDefendedObjectiveLog(PlayerTeam team, IReadOnlyCollection<int> defenderIds)
    {
        RecordObjectiveLogEntry(
            team,
            BuildPlayerNameList(defenderIds, team),
            "defended the point!",
            team == PlayerTeam.Blue ? "BlueDefenseS" : "RedDefenseS");
    }

    private void RecordIntelPickedUpObjectiveLog(PlayerEntity player)
    {
        RecordObjectiveLogEntry(
            player.Team,
            player.DisplayName,
            "picked up the intelligence!",
            player.Team == PlayerTeam.Blue ? "BlueCaptureS" : "RedCaptureS",
            player.Id);
    }

    private void RecordIntelCapturedObjectiveLog(PlayerEntity player)
    {
        RecordObjectiveLogEntry(
            player.Team,
            player.DisplayName,
            "captured the intelligence!",
            player.Team == PlayerTeam.Blue ? "BlueCaptureS" : "RedCaptureS",
            player.Id);
    }

    private void RecordIntelDroppedObjectiveLog(PlayerEntity player)
    {
        RecordObjectiveLogEntry(
            player.Team,
            player.DisplayName,
            " dropped the intelligence!",
            string.Empty,
            player.Id);
    }

    private void RecordIntelDefendedObjectiveLog(PlayerEntity player)
    {
        RecordObjectiveLogEntry(
            player.Team,
            player.DisplayName,
            "defended the intelligence!",
            player.Team == PlayerTeam.Blue ? "BlueDefenseS" : "RedDefenseS",
            player.Id);
    }

    private void RecordIntelReturnedObjectiveLog(PlayerTeam team)
    {
        RecordObjectiveLogEntry(
            team,
            team == PlayerTeam.Blue ? "Blue" : "Red",
            " Intel has returned to base!");
    }

    private void RecordGeneratorDestroyedObjectiveLog(PlayerTeam team)
    {
        RecordObjectiveLogEntry(
            team,
            team == PlayerTeam.Blue ? "Blue team" : "Red team",
            " has destroyed the enemy generator!");
    }

    private string BuildPlayerNameList(IReadOnlyCollection<int> playerIds, PlayerTeam fallbackTeam)
    {
        if (playerIds.Count == 0)
        {
            return fallbackTeam == PlayerTeam.Blue ? "Blue team" : "Red team";
        }

        var names = new List<string>(playerIds.Count);
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (ContainsPlayerId(playerIds, player.Id))
            {
                names.Add(player.DisplayName);
            }
        }

        if (names.Count == 0)
        {
            return fallbackTeam == PlayerTeam.Blue ? "Blue team" : "Red team";
        }

        if (names.Count == 1)
        {
            return names[0];
        }

        var combined = names[0];
        for (var index = 1; index < names.Count; index += 1)
        {
            combined += index == names.Count - 1
                ? " and " + names[index]
                : ", " + names[index];
        }

        return combined;
    }

    private static bool ContainsPlayerId(IReadOnlyCollection<int> playerIds, int playerId)
    {
        foreach (var candidateId in playerIds)
        {
            if (candidateId == playerId)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetKillFeedWeaponSprite(PlayerEntity? attacker)
    {
        if (attacker is null)
        {
            return "DeadKL";
        }

        return CharacterClassCatalog.GetPrimaryWeaponKillFeedSprite(attacker.ClassId);
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

        EnemyPlayer.SetClassDefinition(_enemyDummyClassDefinition);
        SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, ReserveSpawn(EnemyPlayer, _enemyDummyTeam));
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
