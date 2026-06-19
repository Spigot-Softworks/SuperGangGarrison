using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class PlayerEntityMovementRegressionTests
{
    private static readonly FieldInfo AimDirectionDegreesBackingField = typeof(PlayerEntity).GetField(
        "<AimDirectionDegrees>k__BackingField",
        BindingFlags.Instance | BindingFlags.NonPublic)!;
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
    public void DirectionalFloorKeepsPlayerGroundedWhileWalkingAcross()
    {
        var configuration = new DirectionalWallConfiguration(
            DirectionalWallPassDirection.Up,
            DirectionalWallAffectSetting.Affect,
            DirectionalWallAffectSetting.Ignore);
        var wall = DirectionalWallConfiguration.CreateMarker(64f, 300f, 4f, 1f, configuration);
        var level = CreateOpenLevel([wall]);
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 128f);
        player.TeleportTo(128f, wall.Top - player.CollisionBottomOffset);
        var startX = player.X;

        for (var tick = 0; tick < 20; tick += 1)
        {
            player.Advance(
                MoveRightInput,
                jumpPressed: false,
                level,
                PlayerTeam.Red,
                1d / SimulationConfig.DefaultTicksPerSecond);
            Assert.True(player.IsGrounded, $"expected player to stay grounded on tick {tick}, y={player.Y:0.###}");
        }

        Assert.True(player.X > startX + 4f, $"expected player to walk across directional floor, start={startX:0.###} x={player.X:0.###}");
        Assert.Equal(wall.Top - player.CollisionBottomOffset, player.Y, precision: 2);
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
    public void CivilianUmbrellaSlowsFallingWhileAimingUpWithUmbrellaOpen()
    {
        var level = CreateOpenLevel();
        var normal = CreateAirborneCivilianWithFallSpeed(500f);
        var shielded = CreateAirborneCivilianWithFallSpeed(500f);

        Assert.True(shielded.TryActivateCivvieUmbrella());
        SetAimDirectionDegrees(normal, 0f);
        SetAimDirectionDegrees(shielded, 270f);

        AdvanceAirborneTick(normal, level);
        AdvanceAirborneTick(shielded, level, keepUmbrellaActive: true);

        var expectedMaxFallSpeed = LegacyMovementModel.MaxFallSpeedPerTick
            * LegacyMovementModel.SourceTicksPerSecond
            * PlayerEntity.CivvieUmbrellaFallSpeedScale;
        Assert.True(shielded.VerticalSpeed <= expectedMaxFallSpeed + 0.001f);
        Assert.True(
            normal.VerticalSpeed > shielded.VerticalSpeed + 50f,
            $"expected umbrella to slow fall, normal={normal.VerticalSpeed:0.###} shielded={shielded.VerticalSpeed:0.###}");
    }

    [Fact]
    public void CivilianUmbrellaDoesNotSlowFallWhenAimingOutsideUpArc()
    {
        var level = CreateOpenLevel();
        var shielded = CreateAirborneCivilianWithFallSpeed(500f);
        var horizontal = CreateAirborneCivilianWithFallSpeed(500f);

        Assert.True(shielded.TryActivateCivvieUmbrella());
        Assert.True(horizontal.TryActivateCivvieUmbrella());
        SetAimDirectionDegrees(shielded, 0f);
        SetAimDirectionDegrees(horizontal, 0f);

        AdvanceAirborneTick(shielded, level, keepUmbrellaActive: true);
        AdvanceAirborneTick(horizontal, level, keepUmbrellaActive: true);

        Assert.True(
            MathF.Abs(shielded.VerticalSpeed - horizontal.VerticalSpeed) < 0.001f,
            $"expected horizontal aim to ignore slow fall, shielded={shielded.VerticalSpeed:0.###} horizontal={horizontal.VerticalSpeed:0.###}");
    }

    [Fact]
    public void CivilianUmbrellaSlowFallUsesSlipperyAirMovement()
    {
        var level = CreateOpenLevel();
        var slowFall = CreateAirborneCivilianWithFallSpeed(0f);
        var normalAir = CreateAirborneCivilianWithFallSpeed(0f);

        Assert.True(slowFall.TryActivateCivvieUmbrella());
        Assert.True(normalAir.TryActivateCivvieUmbrella());
        SetAimDirectionDegrees(slowFall, 270f);
        SetAimDirectionDegrees(normalAir, 0f);

        const int accelerationTicks = 8;
        for (var tick = 0; tick < accelerationTicks; tick += 1)
        {
            AdvanceAirborneTick(slowFall, level, moveRight: true, keepUmbrellaActive: true);
            AdvanceAirborneTick(normalAir, level, moveRight: true, keepUmbrellaActive: true);
        }

        const int topSpeedTicks = 30;
        for (var tick = 0; tick < topSpeedTicks; tick += 1)
        {
            AdvanceAirborneTick(slowFall, level, moveRight: true, keepUmbrellaActive: true);
            AdvanceAirborneTick(normalAir, level, moveRight: true, keepUmbrellaActive: true);
        }

        var slowFallReleaseSpeed = slowFall.HorizontalSpeed;
        var normalReleaseSpeed = normalAir.HorizontalSpeed;
        for (var tick = 0; tick < 8; tick += 1)
        {
            AdvanceAirborneTick(slowFall, level, keepUmbrellaActive: true);
            AdvanceAirborneTick(normalAir, level, keepUmbrellaActive: true);
        }

        Assert.True(
            MathF.Abs(slowFall.HorizontalSpeed) > MathF.Abs(normalAir.HorizontalSpeed),
            $"expected slower slow-fall deceleration, slowFall={slowFall.HorizontalSpeed:0.###} normalAir={normalAir.HorizontalSpeed:0.###}");
        Assert.True(
            MathF.Abs(slowFall.HorizontalSpeed) > MathF.Abs(slowFallReleaseSpeed) * 0.75f,
            $"expected slow-fall momentum to persist after releasing input, speed={slowFall.HorizontalSpeed:0.###}");
        Assert.True(
            MathF.Abs(normalAir.HorizontalSpeed) < MathF.Abs(normalReleaseSpeed) * 0.6f,
            $"expected normal air movement to bleed off faster, speed={normalAir.HorizontalSpeed:0.###}");
    }

    [Fact]
    public void CivilianUmbrellaSlowFallAirMovementHasHorizontalSpeedBonus()
    {
        var level = CreateOpenLevel();
        var player = CreateAirborneCivilianWithFallSpeed(0f);

        Assert.True(player.TryActivateCivvieUmbrella());
        SetAimDirectionDegrees(player, 270f);

        for (var tick = 0; tick < 180; tick += 1)
        {
            AdvanceAirborneTick(player, level, moveRight: true, keepUmbrellaActive: true);
        }

        var unboostedMaxSpeed = LegacyMovementModel.GetMaxRunSpeed(
            player.RunPower
            * PlayerEntity.CivvieUmbrellaSlowFallRunPowerMultiplier
            * PlayerEntity.CivvieUmbrellaSlowFallControlFactorScale);
        var boostedMaxSpeed = LegacyMovementModel.GetMaxRunSpeed(
            player.RunPower
            * PlayerEntity.CivvieUmbrellaSlowFallRunPowerMultiplier
            * PlayerEntity.CivvieUmbrellaSlowFallHorizontalSpeedMultiplier
            * PlayerEntity.CivvieUmbrellaSlowFallControlFactorScale);

        Assert.True(
            player.HorizontalSpeed > unboostedMaxSpeed * 1.09f,
            $"expected umbrella slow-fall horizontal speed bonus, speed={player.HorizontalSpeed:0.###} unboostedMax={unboostedMaxSpeed:0.###}");
        Assert.True(
            player.HorizontalSpeed <= boostedMaxSpeed + 0.001f,
            $"expected umbrella slow-fall speed to stay under boosted max, speed={player.HorizontalSpeed:0.###} boostedMax={boostedMaxSpeed:0.###}");
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

    [Fact]
    public void CivilianUmbrellaOpeningAirblastWaitsForThirdFrame()
    {
        var player = CreateAirborneCivilianWithFallSpeed(0f);
        Assert.True(player.TryActivateCivvieUmbrella());
        player.BeginCivvieUmbrellaOpening();

        for (var tick = 0; tick < PlayerEntity.CivvieUmbrellaAirblastOpeningTick; tick += 1)
        {
            Assert.False(player.ShouldTriggerCivvieUmbrellaOpeningAirblast());
            player.AdvanceCivvieUmbrellaOpeningTick();
        }

        Assert.True(player.ShouldTriggerCivvieUmbrellaOpeningAirblast());
    }

    [Fact]
    public void CivilianUmbrellaOpeningAirblastRetriggersOnEachNewOpening()
    {
        var player = CreateAirborneCivilianWithFallSpeed(0f);
        Assert.True(player.TryActivateCivvieUmbrella());

        player.BeginCivvieUmbrellaOpening();
        AdvanceCivvieUmbrellaOpeningToAirblastFrame(player);
        Assert.True(player.ShouldTriggerCivvieUmbrellaOpeningAirblast());
        player.MarkCivvieUmbrellaOpeningAirblastTriggered();

        player.BeginCivvieUmbrellaOpening();
        AdvanceCivvieUmbrellaOpeningToAirblastFrame(player);
        Assert.True(player.ShouldTriggerCivvieUmbrellaOpeningAirblast());
    }

    private static void AdvanceCivvieUmbrellaOpeningToAirblastFrame(PlayerEntity player)
    {
        for (var tick = 0; tick < PlayerEntity.CivvieUmbrellaAirblastOpeningTick; tick += 1)
        {
            Assert.False(player.ShouldTriggerCivvieUmbrellaOpeningAirblast());
            player.AdvanceCivvieUmbrellaOpeningTick();
        }
    }

    [Fact]
    public void CivilianUmbrellaPausesPrimaryReloadWhileOpen()
    {
        var reloading = CreateAirborneCivilianWithFallSpeed(0f);
        var baseline = CreateAirborneCivilianWithFallSpeed(0f);
        reloading.ForceSetAmmo(0);
        baseline.ForceSetAmmo(0);

        var reloadTicks = reloading.ReloadTicksUntilNextShell;
        Assert.True(reloadTicks > 0);

        Assert.True(reloading.TryActivateCivvieUmbrella());
        const int advanceTicks = 8;
        for (var tick = 0; tick < advanceTicks; tick += 1)
        {
            AdvanceReloadTick(reloading, keepUmbrellaActive: true);
            AdvanceReloadTick(baseline);
        }

        Assert.Equal(reloadTicks, reloading.ReloadTicksUntilNextShell);
        Assert.True(
            baseline.ReloadTicksUntilNextShell < reloadTicks,
            $"expected reload to advance without umbrella, baseline={baseline.ReloadTicksUntilNextShell} initial={reloadTicks}");
    }

    [Fact]
    public void CivilianPogoToggleStaysGroundedUnlessUpHeld()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);
        player.SyncCivviePogoSuperJumpInput(false);
        Assert.True(player.TryToggleCivviePogo());
        FulfillCivviePogoGroundBounce(player, level);
        Assert.True(player.IsGrounded);
        Assert.Equal(0f, player.VerticalSpeed);
        player.DeactivateCivviePogo();

        player.SyncCivviePogoSuperJumpInput(true);
        Assert.True(player.TryToggleCivviePogo());
        FulfillCivviePogoGroundBounce(player, level);
        var superBounceSpeed = MathF.Abs(player.VerticalSpeed);

        Assert.False(player.IsGrounded);
        Assert.True(superBounceSpeed > 0f);
    }

    [Fact]
    public void CivilianUmbrellaRequiresPogoToBeStoppedBeforeOpening()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);

        EnterCivviePogoSuperJumpAirPhase(player, level);

        Assert.False(player.TryActivateCivvieUmbrella());
        Assert.True(player.IsCivviePogoActive);
        Assert.True(player.IsCivviePogoSuperJumpAirPhaseActive);
        Assert.False(player.IsCivvieUmbrellaActive);
    }

    [Fact]
    public void CivilianUmbrellaAirLiftAppliesOncePerAirtimeAndResetsOnLanding()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateAirborneCivilianWithFallSpeed(180f);

        var firstSpeedBefore = player.VerticalSpeed;
        Assert.True(player.TryActivateCivvieUmbrella());

        var expectedFirstSpeed = firstSpeedBefore - PlayerEntity.CivvieUmbrellaAirLiftSpeedPerTick
            * LegacyMovementModel.SourceTicksPerSecond;
        Assert.Equal(expectedFirstSpeed, player.VerticalSpeed, precision: 3);

        player.SyncCivvieUmbrellaSecondaryInput(secondaryHeld: false);
        var reopenSpeedBefore = player.VerticalSpeed;
        Assert.True(player.TryActivateCivvieUmbrella());
        Assert.Equal(reopenSpeedBefore, player.VerticalSpeed, precision: 3);

        player.SyncCivvieUmbrellaSecondaryInput(secondaryHeld: false);
        LandGroundedCivilian(player, level);
        player.AddImpulse(0f, -60f);

        var secondSpeedBefore = player.VerticalSpeed;
        Assert.True(player.TryActivateCivvieUmbrella());
        var expectedSecondSpeed = secondSpeedBefore - PlayerEntity.CivvieUmbrellaAirLiftSpeedPerTick
            * LegacyMovementModel.SourceTicksPerSecond;
        Assert.Equal(expectedSecondSpeed, player.VerticalSpeed, precision: 3);
        Assert.True(
            player.VerticalSpeed < secondSpeedBefore,
            $"expected umbrella opening after landing reset to add lift, before={secondSpeedBefore:0.###} after={player.VerticalSpeed:0.###}");
    }

    [Fact]
    public void CivilianPogoSuperJumpSoundPendingOnlyWhenUpHeldOnBounce()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);
        player.SyncCivviePogoSuperJumpInput(false);
        Assert.True(player.TryToggleCivviePogo());
        FulfillCivviePogoGroundBounce(player, level);
        Assert.False(player.TryConsumeCivviePogoSuperJumpSoundRequest(out _, out _));
        player.DeactivateCivviePogo();
        LandGroundedCivilian(player, level);

        player.SyncCivviePogoSuperJumpInput(true);
        Assert.True(player.TryToggleCivviePogo());
        FulfillCivviePogoGroundBounce(player, level);
        Assert.True(player.TryConsumeCivviePogoSuperJumpSoundRequest(out _, out _));
    }

    [Fact]
    public void CivilianPogoBouncesWhenActivatedDuringSlowFallLandingTick()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateAirborneCivilianWithFallSpeed(0f);
        var groundedY = level.FloorY - player.CollisionBottomOffset;
        player.TeleportTo(128f, groundedY - 2f);
        var slowFallSpeed = LegacyMovementModel.MaxFallSpeedPerTick
            * LegacyMovementModel.SourceTicksPerSecond
            * PlayerEntity.CivvieUmbrellaFallSpeedScale;
        player.ApplyVelocityImpulse(0f, slowFallSpeed);

        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        var idleInput = MoveRightInput with { Right = false };
        var startedGrounded = player.PrepareMovement(idleInput, level, PlayerTeam.Red, deltaSeconds, out _);
        Assert.False(startedGrounded);
        Assert.True(player.TryToggleCivviePogo());
        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds,
            startedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);

        Assert.True(player.IsCivviePogoActive);
        Assert.True(
            MathF.Abs(player.VerticalSpeed) > 0f || !player.IsGrounded,
            $"expected pogo to bounce on slow-fall landing, grounded={player.IsGrounded} vertical={player.VerticalSpeed:0.###}");
    }

    [Fact]
    public void CivilianPogoLandingOnLowerSurfaceAppliesPassiveBoost()
    {
        var level = CreateSteppedPogoLevel();
        const float upperSurfaceY = 400f;
        const float lowerSurfaceY = 520f;
        var player = new PlayerEntity(1, CharacterClassCatalog.Civilian, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 0f);
        player.TeleportTo(128f, upperSurfaceY - player.CollisionBottomOffset);
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        var idleInput = MoveRightInput with { Right = false };
        var startedGrounded = player.PrepareMovement(idleInput, level, PlayerTeam.Red, deltaSeconds, out _);
        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds,
            startedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);
        Assert.True(player.IsGrounded);

        player.SyncCivviePogoSuperJumpInput(false);
        Assert.True(player.TryToggleCivviePogo());
        FulfillCivviePogoGroundBounce(player, level);
        Assert.True(player.IsGrounded);

        player.TeleportTo(512f, lowerSurfaceY - player.CollisionBottomOffset - 2f);
        player.ApplyVelocityImpulse(0f, 300f);
        startedGrounded = player.PrepareMovement(idleInput, level, PlayerTeam.Red, deltaSeconds, out _);
        Assert.False(startedGrounded);
        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds,
            startedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);

        Assert.True(player.IsCivviePogoActive);
        Assert.False(player.IsGrounded);
        Assert.True(
            player.VerticalSpeed < -0.01f,
            $"expected lower pogo landing to add upward boost, vertical={player.VerticalSpeed:0.###}");
    }

    [Fact]
    public void CivilianPogoStuckGroundRecoveryDoesNotRebounceWithoutUpHeld()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);
        player.SyncCivviePogoSuperJumpInput(false);
        Assert.True(player.TryToggleCivviePogo());
        ClearCivviePogoNeedsGroundBounce(player);

        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        var moveInput = MoveRightInput;
        var stayedGrounded = true;
        for (var tick = 0; tick < 48; tick += 1)
        {
            var startedGrounded = player.PrepareMovement(moveInput, level, PlayerTeam.Red, deltaSeconds, out _);
            player.CompleteMovement(
                level,
                PlayerTeam.Red,
                deltaSeconds,
                startedGrounded,
                jumped: false,
                allowDropdownFallThrough: false);
            if (!player.IsGrounded || player.VerticalSpeed < -0.01f)
            {
                stayedGrounded = false;
                break;
            }
        }

        Assert.True(player.IsCivviePogoActive);
        Assert.True(stayedGrounded, "expected idle pogo recovery to avoid adding vertical momentum");
    }

    private static void ClearCivviePogoNeedsGroundBounce(PlayerEntity player)
    {
        var field = typeof(PlayerEntity).GetField(
            "<CivviePogoNeedsGroundBounce>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(player, false);
    }

    private static PlayerEntity CreateGroundedCivilian(SimpleLevel? level = null)
    {
        level ??= CreateFlatGroundLevel();
        var player = new PlayerEntity(1, CharacterClassCatalog.Civilian, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 0f);
        var groundedY = level.FloorY - player.CollisionBottomOffset;
        player.TeleportTo(128f, groundedY);
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        player.PrepareMovement(
            MoveRightInput with { Right = false },
            level,
            PlayerTeam.Red,
            deltaSeconds,
            out _);
        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds,
            startedGrounded: false,
            jumped: false,
            allowDropdownFallThrough: false);
        Assert.True(player.IsGrounded);
        return player;
    }

    private static void LandGroundedCivilian(PlayerEntity player, SimpleLevel level)
    {
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        var idleInput = MoveRightInput with { Right = false };
        for (var tick = 0; tick < 600 && !player.IsGrounded; tick += 1)
        {
            player.PrepareMovement(idleInput, level, PlayerTeam.Red, deltaSeconds, out _);
            player.CompleteMovement(
                level,
                PlayerTeam.Red,
                deltaSeconds,
                startedGrounded: player.IsGrounded,
                jumped: false,
                allowDropdownFallThrough: false);
        }

        Assert.True(player.IsGrounded);
    }

    private static void FulfillCivviePogoGroundBounce(PlayerEntity player, SimpleLevel level)
    {
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        var idleInput = MoveRightInput with { Right = false };
        var startedGrounded = player.PrepareMovement(idleInput, level, PlayerTeam.Red, deltaSeconds, out _);
        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds,
            startedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);
    }

    private static PlayerEntity CreateAirborneCivilianWithFallSpeed(float verticalSpeed)
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Civilian, "Test");
        player.Spawn(PlayerTeam.Red, 128f, 128f);
        player.TeleportTo(128f, 128f);
        player.AddImpulse(0f, verticalSpeed);
        return player;
    }

    private static void SetAimDirectionDegrees(PlayerEntity player, float aimDirectionDegrees)
    {
        AimDirectionDegreesBackingField.SetValue(player, aimDirectionDegrees);
    }

    private static void AdvanceReloadTick(PlayerEntity player, bool keepUmbrellaActive = false)
    {
        var input = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: keepUmbrellaActive,
            AimWorldX: player.X,
            AimWorldY: player.Y,
            DebugKill: false);
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        if (keepUmbrellaActive)
        {
            player.TryActivateCivvieUmbrella();
        }

        player.SyncCivvieUmbrellaSecondaryInput(input.FireSecondary);
        player.AdvanceTickState(input, deltaSeconds);
    }

    private static void AdvanceAirborneTick(
        PlayerEntity player,
        SimpleLevel level,
        bool moveRight = false,
        bool keepUmbrellaActive = false)
    {
        var input = new PlayerInputSnapshot(
            Left: false,
            Right: moveRight,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X,
            AimWorldY: player.Y,
            DebugKill: false);
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        if (keepUmbrellaActive)
        {
            player.TryActivateCivvieUmbrella();
        }

        player.PrepareMovement(input, level, PlayerTeam.Red, deltaSeconds, out _);
        player.CompleteMovement(
            level,
            PlayerTeam.Red,
            deltaSeconds,
            startedGrounded: false,
            jumped: false,
            allowDropdownFallThrough: false);
    }

    private static SimpleLevel CreateOpenLevel(IReadOnlyList<RoomObjectMarker>? roomObjects = null)
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
            roomObjects: roomObjects ?? [],
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

    private static SimpleLevel CreateSteppedPogoLevel()
    {
        return new SimpleLevel(
            name: "movement_stepped_pogo",
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
            floorY: 900f,
            solids:
            [
                new LevelSolid(0f, 400f, 256f, 624f),
                new LevelSolid(384f, 520f, 1664f, 504f),
            ],
            importedFromSource: false);
    }

    [Fact]
    public void CivilianPogoTrickStartsOnTauntInputAndEndsOnLandingCrunch()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);
        EnterCivviePogoSuperJumpAirPhase(player, level);
        Assert.True(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 18));
        Assert.True(player.IsCivviePogoTrickActive);
        Assert.InRange(player.GetCivviePogoTrickFrameIndex(sessionSeed: 0, currentFrame: 100, frameCount: 2), 0, 1);

        FulfillCivviePogoGroundBounce(player, level);
        player.AdvanceCivviePogoState();

        Assert.False(player.IsCivviePogoTrickActive);
        Assert.True(player.CivviePogoCrunchTicksRemaining > 0);
    }

    [Fact]
    public void CivilianPogoTrickRequiresSuperJumpAirPhase()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);
        Assert.True(player.TryToggleCivviePogo());
        Assert.False(player.CanPerformCivviePogoTrick);
        Assert.False(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 18));

        EnterCivviePogoSuperJumpAirPhase(player, level);
        Assert.True(player.CanPerformCivviePogoTrick);
        Assert.True(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 18));
    }

    [Fact]
    public void CivilianPogoTrickDurationIsCappedAtPointSixSeconds()
    {
        Assert.Equal(18, PlayerEntity.ResolveCivviePogoTrickDurationTicks(30, ticksPerSecond: 30));
        Assert.Equal(12, PlayerEntity.ResolveCivviePogoTrickDurationTicks(30, ticksPerSecond: 20));
        Assert.Equal(10, PlayerEntity.ResolveCivviePogoTrickDurationTicks(10, ticksPerSecond: 30));
    }

    [Fact]
    public void CivilianPogoTrickAllowsOnlyOneTrickPerSuperJump()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);
        EnterCivviePogoSuperJumpAirPhase(player, level);
        Assert.True(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 2));
        Assert.False(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 2));

        for (var tick = 0; tick < 2; tick += 1)
        {
            player.AdvanceCivviePogoState();
        }

        Assert.False(player.IsCivviePogoTrickActive);
        player.ObserveCivviePogoTrickInput(isHeld: false);
        Assert.False(player.CanPerformCivviePogoTrick);
        Assert.False(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 2));
    }

    [Fact]
    public void CivilianPogoTrickResetsAfterAnotherSuperJump()
    {
        var level = CreateFlatGroundLevel();
        var player = CreateGroundedCivilian(level);
        EnterCivviePogoSuperJumpAirPhase(player, level);
        Assert.True(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 2));

        for (var tick = 0; tick < 2; tick += 1)
        {
            player.AdvanceCivviePogoState();
        }

        player.ObserveCivviePogoTrickInput(isHeld: false);
        player.SyncCivviePogoSuperJumpInput(true);
        AdvanceCivviePogoMovementUntil(player, level, static candidate => candidate.CanPerformCivviePogoTrick);

        Assert.True(player.TryStartCivviePogoTrick(trickFrameCount: 4, durationTicks: 2));
    }

    private static void AdvanceCivviePogoMovementUntil(
        PlayerEntity player,
        SimpleLevel level,
        Func<PlayerEntity, bool> predicate,
        int maxTicks = 900)
    {
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        var input = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: true,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 96f,
            AimWorldY: player.Y,
            DebugKill: false);
        for (var tick = 0; tick < maxTicks; tick += 1)
        {
            var startedGrounded = player.PrepareMovement(input, level, PlayerTeam.Red, deltaSeconds, out _);
            player.CompleteMovement(
                level,
                PlayerTeam.Red,
                deltaSeconds,
                startedGrounded,
                jumped: false,
                allowDropdownFallThrough: false);
            player.TryApplyCivviePogoLandingBounce(wasAirborneBeforeMove: !startedGrounded);
            player.AdvanceCivviePogoState();
            if (predicate(player))
            {
                return;
            }
        }

        throw new InvalidOperationException("Movement predicate was not satisfied.");
    }

    private static void EnterCivviePogoSuperJumpAirPhase(PlayerEntity player, SimpleLevel level)
    {
        player.SyncCivviePogoSuperJumpInput(true);
        if (!player.IsCivviePogoActive)
        {
            Assert.True(player.TryToggleCivviePogo());
        }

        FulfillCivviePogoGroundBounce(player, level);
        Assert.False(player.IsGrounded);
        Assert.True(player.IsCivviePogoSuperJumpAirPhaseActive);
    }
}
