namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
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
}
