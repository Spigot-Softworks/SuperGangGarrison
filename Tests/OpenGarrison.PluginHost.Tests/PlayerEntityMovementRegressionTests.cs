using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityMovementRegressionTests
{
    [Fact]
    public void AirborneSubpixelMovementStillAdvancesInOpenSpace()
    {
        var level = CreateOpenLevel();
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 128f);
        player.TeleportTo(128f, 128f);
        player.AddImpulse(18f, 12f);

        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds: 1d / 120d,
            startedGrounded: false,
            jumped: false,
            allowDropdownFallThrough: false);

        Assert.True(player.X > 128.1f, $"expected airborne subpixel horizontal movement, got X={player.X:0.###}");
        Assert.True(player.Y > 128.05f, $"expected airborne subpixel vertical movement, got Y={player.Y:0.###}");
    }

    [Fact]
    public void RunningJumpRetainsStandingJumpApexOnFlatGround()
    {
        var standingPlayer = CreateGroundedScout();
        var runningPlayer = CreateGroundedScout();
        var level = CreateFlatGroundLevel();
        var standingInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: true,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 0f,
            AimWorldY: 0f,
            DebugKill: false);
        var runningInput = standingInput with { Right = true };

        var standingApexY = SimulateJumpApex(standingPlayer, level, standingInput);
        var runningApexY = SimulateJumpApex(runningPlayer, level, runningInput);

        Assert.True(
            runningApexY <= standingApexY + 1f,
            $"expected running jump apex to match standing jump, got standing={standingApexY:0.###} running={runningApexY:0.###}");
    }

    [Fact]
    public void ConflictBlueHeavyCanLeaveRightSpawnExitBoxChoke()
    {
        var level = SimpleLevelFactory.CreateImportedLevel("Conflict");
        Assert.NotNull(level);
        var spawn = level!.BlueSpawns.OrderByDescending(static candidate => candidate.X).First();
        var player = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Test");
        player.Spawn(PlayerTeam.Blue, spawn.X, spawn.Y);
        player.ResolveBlockingOverlap(level, PlayerTeam.Blue);
        var wasGrounded = player.IsGrounded;

        for (var tick = 0; tick < 600; tick += 1)
        {
            var jumpPressed = tick == 0 || (player.IsGrounded && !wasGrounded);
            wasGrounded = player.IsGrounded;
            player.Advance(
                MoveRightInput with { Up = jumpPressed },
                jumpPressed,
                level,
                PlayerTeam.Blue,
                1d / SimulationConfig.DefaultTicksPerSecond);
        }

        Assert.True(
            player.X > 3300f,
            $"expected Heavy to clear Conflict blue right-exit box choke, got x={player.X:0.###} bounds=({player.Left:0.###},{player.Top:0.###},{player.Right:0.###},{player.Bottom:0.###})");
    }

    [Fact]
    public void CivilianUmbrellaSlowsFallingWhileHeldOpen()
    {
        var level = CreateOpenLevel();
        var normal = CreateAirborneCivilianWithFallSpeed(500f);
        var shielded = CreateAirborneCivilianWithFallSpeed(500f);

        Assert.True(shielded.TryActivateCivvieUmbrella());

        normal.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds: 1d / SimulationConfig.DefaultTicksPerSecond,
            startedGrounded: false,
            jumped: false,
            allowDropdownFallThrough: false);
        shielded.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds: 1d / SimulationConfig.DefaultTicksPerSecond,
            startedGrounded: false,
            jumped: false,
            allowDropdownFallThrough: false);

        var expectedMaxFallSpeed = LegacyMovementModel.MaxFallSpeedPerTick
            * LegacyMovementModel.SourceTicksPerSecond
            * PlayerEntity.CivvieUmbrellaFallSpeedScale;
        Assert.True(shielded.VerticalSpeed <= expectedMaxFallSpeed + 0.001f);
        Assert.True(
            normal.VerticalSpeed > shielded.VerticalSpeed + 50f,
            $"expected umbrella to slow fall, normal={normal.VerticalSpeed:0.###} shielded={shielded.VerticalSpeed:0.###}");
    }

    private static float SimulateJumpApex(PlayerEntity player, SimpleLevel level, PlayerInputSnapshot initialInput)
    {
        const double dt = 1d / SimulationConfig.DefaultTicksPerSecond;
        var input = initialInput;
        var lowestY = player.Y;

        for (var tick = 0; tick < 45; tick += 1)
        {
            var jumpPressed = tick == 0 && input.Up;
            player.Advance(input, jumpPressed, level, PlayerTeam.Red, dt);
            if (player.Y < lowestY)
            {
                lowestY = player.Y;
            }

            if (tick == 0)
            {
                input = input with { Up = false };
            }
        }

        return lowestY;
    }

    private static readonly PlayerInputSnapshot MoveRightInput = new(
        Left: false,
        Right: true,
        Up: false,
        Down: false,
        BuildSentry: false,
        DestroySentry: false,
        Taunt: false,
        FirePrimary: false,
        FireSecondary: false,
        AimWorldX: 4096f,
        AimWorldY: 630f,
        DebugKill: false);

    private static PlayerEntity CreateGroundedScout()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 0f);
        var groundedY = 500f - player.CollisionBottomOffset;
        player.TeleportTo(128f, groundedY);
        return player;
    }

    private static PlayerEntity CreateAirborneCivilianWithFallSpeed(float verticalSpeed)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Civilian, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 128f);
        player.TeleportTo(128f, 128f);
        player.AddImpulse(0f, verticalSpeed);
        return player;
    }

    private static SimpleLevel CreateOpenLevel()
    {
        return new SimpleLevel(
            name: "movement_open",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(2048f, 2048f),
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
            floorY: 2048f,
            solids: [],
            importedFromSource: false);
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
}
