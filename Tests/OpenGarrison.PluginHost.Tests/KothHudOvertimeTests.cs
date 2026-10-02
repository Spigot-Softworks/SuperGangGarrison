using System.Collections.Generic;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class KothHudOvertimeTests
{
    [Fact]
    public void SingleKothShowsOvertimeOnlyWhenExhaustedOwnerIsBeingChallenged()
    {
        var point = CreatePoint("KothControlPoint", PlayerTeam.Red);
        point.CappingTicks = 1f;

        Assert.True(IsOvertime(GameModeKind.KingOfTheHill, PlayerTeam.Red, [point], redTimer: 0, blueTimer: 20));
        Assert.False(IsOvertime(GameModeKind.KingOfTheHill, PlayerTeam.Blue, [point], redTimer: 0, blueTimer: 0));

        point.BlueCappers = 0;
        point.CappingTicks = 0f;
        Assert.False(IsOvertime(GameModeKind.KingOfTheHill, PlayerTeam.Red, [point], redTimer: 0, blueTimer: 20));
    }

    [Fact]
    public void DoubleKothUsesTheTeamOwnedEnemyPointAndRequiresLiveMatchTime()
    {
        var redHome = CreatePoint("KothRedControlPoint", PlayerTeam.Red);
        var blueHome = CreatePoint("KothBlueControlPoint", PlayerTeam.Red);
        blueHome.BlueCappers = 1;
        var points = new[] { redHome, blueHome };

        Assert.True(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 0, blueTimer: 60));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 1, blueTimer: 60));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 0, blueTimer: 60, matchTime: 0));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 0, blueTimer: 60, unlockTicks: 1));
        Assert.False(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Red, points, redTimer: 0, blueTimer: 60, matchEnded: true));

        redHome.Team = PlayerTeam.Blue;
        redHome.RedCappers = 1;
        Assert.True(IsOvertime(GameModeKind.DoubleKingOfTheHill, PlayerTeam.Blue, points, redTimer: 60, blueTimer: 0));
    }

    private static bool IsOvertime(
        GameModeKind mode,
        PlayerTeam team,
        IReadOnlyList<ControlPointState> points,
        int redTimer,
        int blueTimer,
        int unlockTicks = 0,
        int matchTime = 1,
        bool matchEnded = false)
    {
        return KothHudOvertimeResolver.IsTeamInOvertime(
            mode,
            team,
            points,
            redTimer,
            blueTimer,
            unlockTicks,
            matchTime,
            matchEnded);
    }

    private static ControlPointState CreatePoint(string sourceName, PlayerTeam? owner)
    {
        var marker = new RoomObjectMarker(
            RoomObjectType.ControlPoint,
            0f,
            0f,
            42f,
            42f,
            string.Empty,
            SourceName: sourceName);
        return new ControlPointState(0, marker) { Team = owner };
    }
}
