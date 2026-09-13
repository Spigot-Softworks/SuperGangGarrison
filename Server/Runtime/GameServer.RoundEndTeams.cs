using System;
using System.Collections.Generic;
using OpenGarrison.Core;

partial class GameServer
{
    private enum RoundEndTeamRuleAction
    {
        None,
        Switch,
        Shuffle,
    }

    private readonly record struct RoundEndTeamAssignment(byte Slot, PlayerTeam PreviousTeam, PlayerTeam Team);

    private void ApplyRoundEndTeamRules(MapChangeTransition transition)
    {
        var action = DetermineRoundEndTeamRuleAction(transition);
        if (action == RoundEndTeamRuleAction.None)
        {
            return;
        }

        var assignments = action == RoundEndTeamRuleAction.Shuffle
            ? BuildRoundEndTeamShuffleAssignments()
            : BuildRoundEndTeamSwitchAssignments();
        if (assignments.Count == 0)
        {
            return;
        }

        var appliedCount = 0;
        foreach (var assignment in assignments)
        {
            if (TryApplyRoundEndTeamAssignment(assignment))
            {
                appliedCount += 1;
            }
        }

        if (appliedCount <= 0)
        {
            return;
        }

        var winner = transition.WinnerTeam.HasValue ? transition.WinnerTeam.Value.ToString() : "tie";
        if (action == RoundEndTeamRuleAction.Shuffle)
        {
            Console.WriteLine($"[server] round-end team shuffle after {_teamShuffleAfterWins} consecutive {winner} wins: changed {appliedCount} slot(s).");
        }
        else
        {
            Console.WriteLine($"[server] round-end team switch: changed {appliedCount} slot(s).");
        }
    }

    private RoundEndTeamRuleAction DetermineRoundEndTeamRuleAction(MapChangeTransition transition)
    {
        if (IsVipTransition(transition))
        {
            return RoundEndTeamRuleAction.Switch;
        }

        if (transition.PreservePlayerStats)
        {
            return RoundEndTeamRuleAction.None;
        }

        if (UpdateTeamShuffleWinStreak(transition.WinnerTeam))
        {
            return RoundEndTeamRuleAction.Shuffle;
        }

        return _switchTeamsAfterRoundEnd
            ? RoundEndTeamRuleAction.Switch
            : RoundEndTeamRuleAction.None;
    }

    private static bool IsVipTransition(MapChangeTransition transition)
    {
        return transition.CurrentGameMode == GameModeKind.Vip
            || transition.CurrentLevelName.StartsWith("vip_", StringComparison.OrdinalIgnoreCase);
    }

    private bool UpdateTeamShuffleWinStreak(PlayerTeam? winnerTeam)
    {
        if (_teamShuffleAfterWins <= 0)
        {
            ResetTeamShuffleWinStreak();
            return false;
        }

        if (!winnerTeam.HasValue)
        {
            ResetTeamShuffleWinStreak();
            return false;
        }

        if (_teamShuffleWinnerStreakTeam == winnerTeam.Value)
        {
            _teamShuffleWinnerStreakCount += 1;
        }
        else
        {
            _teamShuffleWinnerStreakTeam = winnerTeam.Value;
            _teamShuffleWinnerStreakCount = 1;
        }

        if (_teamShuffleWinnerStreakCount < _teamShuffleAfterWins)
        {
            return false;
        }

        ResetTeamShuffleWinStreak();
        return true;
    }

    private void ResetTeamShuffleWinStreak()
    {
        _teamShuffleWinnerStreakTeam = null;
        _teamShuffleWinnerStreakCount = 0;
    }

    private void SetTeamShuffleAfterWins(int wins)
    {
        _teamShuffleAfterWins = OpenGarrisonHostSettings.NormalizeTeamShuffleAfterWins(wins);
        if (_teamShuffleAfterWins <= 0)
        {
            ResetTeamShuffleWinStreak();
        }
    }

    private List<RoundEndTeamAssignment> BuildRoundEndTeamSwitchAssignments()
        => BuildTeamSwitchAssignments(EnumerateRoundEndTeamSlots());

    private static List<RoundEndTeamAssignment> BuildTeamSwitchAssignments(
        IReadOnlyList<(byte Slot, PlayerTeam Team)> players)
    {
        var assignments = new List<RoundEndTeamAssignment>();
        foreach (var entry in players)
        {
            assignments.Add(new RoundEndTeamAssignment(
                entry.Slot,
                entry.Team,
                GetOpposingTeam(entry.Team)));
        }

        return assignments;
    }

    private List<RoundEndTeamAssignment> BuildRoundEndTeamShuffleAssignments()
        => BuildTeamShuffleAssignments(EnumerateRoundEndTeamSlots());

    private List<RoundEndTeamAssignment> BuildTeamShuffleAssignments(
        List<(byte Slot, PlayerTeam Team)> players)
    {
        if (players.Count < 2)
        {
            return [];
        }

        ShuffleRoundEndTeamSlots(players);
        var firstTeam = _teamShuffleRandom.Next(2) == 0 ? PlayerTeam.Red : PlayerTeam.Blue;
        var secondTeam = GetOpposingTeam(firstTeam);
        var assignments = new List<RoundEndTeamAssignment>();
        for (var index = 0; index < players.Count; index += 1)
        {
            var targetTeam = index % 2 == 0 ? firstTeam : secondTeam;
            var player = players[index];
            if (player.Team == targetTeam)
            {
                continue;
            }

            assignments.Add(new RoundEndTeamAssignment(player.Slot, player.Team, targetTeam));
        }

        return assignments;
    }

    private List<(byte Slot, PlayerTeam Team)> EnumerateRoundEndTeamSlots()
    {
        var slots = new SortedSet<byte>();
        foreach (var slot in _clientsBySlot.Keys)
        {
            slots.Add(slot);
        }

        foreach (var slot in _botManager.BotSlots.Keys)
        {
            slots.Add(slot);
        }

        var players = new List<(byte Slot, PlayerTeam Team)>();
        foreach (var slot in slots)
        {
            var team = _world.GetNetworkPlayerConfiguredTeam(slot);
            if (team is PlayerTeam.Red or PlayerTeam.Blue)
            {
                players.Add((slot, team));
            }
        }

        return players;
    }

    private void ShuffleRoundEndTeamSlots(List<(byte Slot, PlayerTeam Team)> players)
    {
        for (var index = players.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = _teamShuffleRandom.Next(index + 1);
            (players[index], players[swapIndex]) = (players[swapIndex], players[index]);
        }
    }

    private bool TryApplyRoundEndTeamAssignment(RoundEndTeamAssignment assignment)
    {
        var applied = _botManager.BotSlots.ContainsKey(assignment.Slot)
            ? _botManager.TrySetBotTeam(assignment.Slot, assignment.Team)
            : _world.TrySetNetworkPlayerTeam(assignment.Slot, assignment.Team);
        if (!applied)
        {
            return false;
        }

        if (_clientsBySlot.TryGetValue(assignment.Slot, out var client))
        {
            _eventReporter.NotifyPlayerTeamChanged(client, assignment.Team);
        }

        return true;
    }

    private bool TryScrambleTeamsByVote()
    {
        var players = EnumerateVoteTeamSlots();
        if (players.Count < 2)
        {
            return false;
        }

        List<RoundEndTeamAssignment> assignments = [];
        for (var attempt = 0; attempt < 8 && assignments.Count == 0; attempt += 1)
        {
            assignments = BuildTeamShuffleAssignments([.. players]);
        }

        // A balanced two-player shuffle can randomly reproduce the current teams.
        // Fall back to a complete side swap so a passed vote always has an effect.
        if (assignments.Count == 0)
        {
            assignments = BuildTeamSwitchAssignments(players);
        }

        var appliedCount = 0;
        foreach (var assignment in assignments)
        {
            var applied = _botManager.BotSlots.ContainsKey(assignment.Slot)
                ? _botManager.TrySetBotTeam(assignment.Slot, assignment.Team)
                : _adminOperations.TrySetTeam(assignment.Slot, assignment.Team);
            if (applied)
            {
                appliedCount += 1;
            }
        }

        if (appliedCount > 0)
        {
            ResetTeamShuffleWinStreak();
            Console.WriteLine($"[server] player vote scrambled teams: changed {appliedCount}/{assignments.Count} slot(s).");
        }

        return appliedCount == assignments.Count;
    }

    private List<(byte Slot, PlayerTeam Team)> EnumerateVoteTeamSlots()
    {
        var slots = new SortedSet<byte>(_botManager.BotSlots.Keys);
        foreach (var client in _clientsBySlot.Values)
        {
            if (client.IsAuthorized
                && !ServerHelpers.IsSpectatorSlot(client.Slot)
                && _world.TryGetNetworkPlayer(client.Slot, out _)
                && !_world.IsNetworkPlayerAwaitingJoin(client.Slot))
            {
                slots.Add(client.Slot);
            }
        }

        var players = new List<(byte Slot, PlayerTeam Team)>();
        foreach (var slot in slots)
        {
            var team = _world.GetNetworkPlayerConfiguredTeam(slot);
            if (team is PlayerTeam.Red or PlayerTeam.Blue)
            {
                players.Add((slot, team));
            }
        }

        return players;
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
    }
}
