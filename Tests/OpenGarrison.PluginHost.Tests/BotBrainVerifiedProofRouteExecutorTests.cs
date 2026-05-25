using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainVerifiedProofRouteExecutorTests
{
    [Fact]
    public void ValleyLowerStairTransitKeepsDrivingTowardProofEdgeExit()
    {
        var asset = CreateValleyBlueHeavyLowerStairGraph();
        var bot = CreateGroundedHeavy(PlayerTeam.Blue, x: 3930f, bottom: 636f);
        var executor = new VerifiedNavProofRouteExecutor();

        var resolved = executor.TryResolve(
            asset,
            bot,
            PlayerTeam.Blue,
            thinkTick: 1,
            baseSteering: default,
            out var steering);

        Assert.True(resolved, executor.LastTrace);
        Assert.Equal(-1f, steering.MoveDirection);
        Assert.Contains("transit:1", executor.LastTrace, StringComparison.Ordinal);
    }

    private static PlayerEntity CreateGroundedHeavy(PlayerTeam team, float x, float bottom)
    {
        var bot = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Bot");
        var y = bottom - bot.CollisionBottomOffset;
        bot.Spawn(team, x, y);
        bot.TeleportTo(x, y);
        bot.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        return bot;
    }

    private static VerifiedNavProofGraphAsset CreateValleyBlueHeavyLowerStairGraph()
    {
        return new VerifiedNavProofGraphAsset
        {
            LevelName = "Valley",
            MapAreaIndex = 1,
            Team = PlayerTeam.Blue,
            ClassId = PlayerClass.Heavy,
            Surfaces =
            [
                new VerifiedNavSurface(30, VerifiedNavSurfaceKind.SolidTop, 3953f, 4313f, 630f, 797),
                new VerifiedNavSurface(35, VerifiedNavSurfaceKind.SolidTop, 3701f, 3877f, 666f, 844),
            ],
            Edges =
            [
                new VerifiedNavProofGraphEdge(
                    Id: 0,
                    RouteKind: VerifiedNavProofRouteKind.Pickup,
                    FromSurfaceId: 30,
                    ToSurfaceId: 35,
                    EntryX: 4197.3306f,
                    EntryBottom: 630f,
                    ExitX: 3876.8638f,
                    ExitBottom: 666f,
                    CostTicks: 72,
                    LeftTicks: 73,
                    RightTicks: 0,
                    JumpTicks: 0,
                    DropTicks: 0,
                    Source: "Valley.a1.Blue.Heavy.lower_stair_regression",
                    Actions:
                    [
                        new VerifiedNavProofGraphActionRun(
                            Ticks: 73,
                            MoveDirection: -1f,
                            Jump: false,
                            DropDown: false),
                    ]),
            ],
            Routes =
            [
                new VerifiedNavProofGraphRoute(
                    Kind: VerifiedNavProofRouteKind.Pickup,
                    Source: "Valley.a1.Blue.Heavy.lower_stair_regression",
                    SampleCount: 1,
                    SurfaceSequence: [30, 35],
                    EdgeIds: [0],
                    LaneSegmentIds: [],
                    Actions: [],
                    TerminalStartX: 3876.8638f,
                    TerminalStartBottom: 666f,
                    TerminalEndX: 3876.8638f,
                    TerminalEndBottom: 666f,
                    TerminalActions: [],
                    StartX: 4224f,
                    StartBottom: 594f),
            ],
        };
    }
}
