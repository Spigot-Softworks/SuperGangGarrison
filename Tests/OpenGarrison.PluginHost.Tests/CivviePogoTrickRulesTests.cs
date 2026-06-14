using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CivviePogoTrickRulesTests
{
    [Fact]
    public void DeterministicFrameIndexIsStableForSameInputs()
    {
        var first = CivviePogoTrickRules.GetDeterministicFrameIndex(
            sessionSeed: 12345,
            playerId: 7,
            startFrame: 500,
            frameCount: 4);
        var second = CivviePogoTrickRules.GetDeterministicFrameIndex(
            sessionSeed: 12345,
            playerId: 7,
            startFrame: 500,
            frameCount: 4);

        Assert.Equal(first, second);
        Assert.InRange(first, 0, 3);
    }

    [Fact]
    public void DeterministicFrameIndexVariesAcrossSessionSeeds()
    {
        var frames = new HashSet<int>();
        for (var sessionSeed = 0; sessionSeed < 32; sessionSeed += 1)
        {
            frames.Add(CivviePogoTrickRules.GetDeterministicFrameIndex(
                sessionSeed,
                playerId: 1,
                startFrame: 100,
                frameCount: 4));
        }

        Assert.True(frames.Count > 1);
    }

    [Fact]
    public void ResolveTrickFrameIndexUsesTrickStartFrameFromRemainingTicks()
    {
        const int sessionSeed = 4242;
        const int playerId = 5;
        const int durationTicks = 30;
        const int ticksRemaining = 27;
        const ulong currentFrame = 1000;
        const int frameCount = 2;

        var expectedStartFrame = CivviePogoTrickRules.ResolveTrickStartFrame(
            currentFrame,
            durationTicks,
            ticksRemaining);
        var expectedFrame = CivviePogoTrickRules.GetDeterministicFrameIndex(
            sessionSeed,
            playerId,
            expectedStartFrame,
            frameCount);
        var resolvedFrame = CivviePogoTrickRules.ResolveTrickFrameIndex(
            sessionSeed,
            playerId,
            currentFrame,
            durationTicks,
            ticksRemaining,
            frameCount);

        Assert.Equal(expectedFrame, resolvedFrame);
    }

    [Fact]
    public void PlayerEntityFrameIndexMatchesRulesForLocalSimulation()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);
        EnterCivviePogoSuperJumpAirPhase(player, level);
        Assert.True(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 18));

        const int sessionSeed = 777;
        const ulong currentFrame = 250;
        const int frameCount = 4;
        var fromPlayer = player.GetCivviePogoTrickFrameIndex(sessionSeed, currentFrame, frameCount);
        var fromRules = CivviePogoTrickRules.ResolveTrickFrameIndex(
            sessionSeed,
            player.Id,
            currentFrame,
            durationTicks: 18,
            player.CivviePogoTrickTicksRemaining,
            frameCount);

        Assert.Equal(fromRules, fromPlayer);
    }

    private static SimpleLevel CreateFlatGroundLevel()
    {
        return new SimpleLevel(
            name: "movement_floor",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(2048f, 1024f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(128f, 128f),
            redSpawns: [new SpawnPoint(128f, 128f)],
            blueSpawns: [new SpawnPoint(256f, 128f)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, 128f, 128f),
                new IntelBaseMarker(PlayerTeam.Blue, 256f, 128f),
            ],
            roomObjects: [],
            floorY: 500f,
            solids: [new LevelSolid(0f, 500f, 2048f, 524f)],
            importedFromSource: false);
    }

    private static PlayerEntity CreateGroundedCivilian(SimpleLevel level)
    {
        var player = new PlayerEntity(9, CharacterClassCatalog.Civilian, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 0f);
        var groundedY = level.FloorY - player.CollisionBottomOffset;
        player.TeleportTo(128f, groundedY);
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        var idleInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 224f,
            AimWorldY: groundedY,
            DebugKill: false);
        var startedGrounded = player.PrepareMovement(idleInput, level, PlayerTeam.Red, deltaSeconds, out _);
        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds,
            startedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);
        Assert.True(player.IsGrounded);
        return player;
    }

    private static void EnterCivviePogoSuperJumpAirPhase(PlayerEntity player, SimpleLevel level)
    {
        player.SyncCivviePogoSuperJumpInput(true);
        Assert.True(player.TryToggleCivviePogo());
        FulfillCivviePogoGroundBounce(player, level);
        Assert.False(player.IsGrounded);
        Assert.True(player.IsCivviePogoSuperJumpAirPhaseActive);
    }

    private static void FulfillCivviePogoGroundBounce(PlayerEntity player, SimpleLevel level)
    {
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        var idleInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 96f,
            AimWorldY: player.Y,
            DebugKill: false);
        var startedGrounded = player.PrepareMovement(idleInput, level, PlayerTeam.Red, deltaSeconds, out _);
        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds,
            startedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);
    }
}
