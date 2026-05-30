using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainClassBehaviorTests
{
    [Fact]
    public void PyroReflectsAccurateIncomingProjectileInAirblastWindow()
    {
        var world = CreateClassWorld(PlayerClass.Pyro, out var pyro);
        var projectileId = FindProjectileIdForPyroReflect(pyro, accurate: true);
        AddIncomingRocket(world, projectileId, pyro, timeToImpactTicks: 6f);

        var decision = CombatDecisionResolver.Resolve(world, pyro, null, null, new CombatDecisionMemory());

        Assert.True(decision.FireSecondary);
        Assert.False(decision.UseAbility);
    }

    [Fact]
    public void PyroMistimesFailedReflectBeforeCorrectAirblastWindow()
    {
        var world = CreateClassWorld(PlayerClass.Pyro, out var pyro);
        var projectileId = FindProjectileIdForPyroReflect(pyro, accurate: false);
        AddIncomingRocket(world, projectileId, pyro, timeToImpactTicks: 12f);

        var decision = CombatDecisionResolver.Resolve(world, pyro, null, null, new CombatDecisionMemory());

        Assert.True(decision.FireSecondary);
        Assert.False(decision.UseAbility);
    }

    [Fact]
    public void HarvestPyroEscapesRightSpoolPocket()
    {
        var world = CreateImportedWorld("Harvest");
        Assert.True(world.TrySetLocalClass(PlayerClass.Pyro));
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        var pyro = world.LocalPlayer;
        pyro.TeleportTo(3022f, 762f);
        pyro.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var controller = new BotBrainController();

        PlayerInputSnapshot input = default;
        for (var tick = 0; tick < 12; tick += 1)
        {
            input = controller.Think(pyro, world, PlayerTeam.Red);
        }

        Assert.True(input.Left);
        Assert.True(input.Up);
        Assert.Contains("harvestRightSpoolEscape", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void HeavySometimesDashesToDodgeIncomingFire()
    {
        var world = CreateClassWorld(PlayerClass.Heavy, out var heavy);
        var projectileId = FindProjectileIdForHeavyDash(heavy);
        AddIncomingRocket(world, projectileId, heavy, timeToImpactTicks: 8f);

        var decision = CombatDecisionResolver.Resolve(world, heavy, null, null, new CombatDecisionMemory());

        Assert.True(decision.UseAbility);
        Assert.False(decision.FireSecondary);
    }

    [Fact]
    public void DemomanSometimesSwapsToGrenadeLauncherInCombat()
    {
        var world = CreateClassWorld(PlayerClass.Demoman, out var demoman);
        Assert.Equal(PrimaryWeaponKind.GrenadeLauncher, demoman.ExperimentalOffhandWeapon?.Kind);
        var target = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, demoman.X + 240f, demoman.Y);
        var combatTarget = new BotBrainCombatTarget(BotBrainCombatTargetKind.Player, target.Team, target.X, target.Y, Player: target);
        var memory = new CombatDecisionMemory();
        CombatFireDecision decision = default;

        for (var tick = 0; tick < 180; tick += 1)
        {
            decision = CombatDecisionResolver.Resolve(world, demoman, combatTarget, null, memory);
            if (decision.UseAbility)
            {
                break;
            }
        }

        Assert.True(decision.UseAbility);
        var input = BotInputSynthesizer.Synthesize(
            demoman,
            default,
            target.X,
            target.Y,
            decision,
            default);
        Assert.True(input.SwapWeapon);
        Assert.False(input.UseAbility);
        Assert.False(input.FirePrimary);
    }

    [Fact]
    public void SoldierBotPulsesSwapWeaponForShotgunAndLauncherSelection()
    {
        var soldier = new PlayerEntity(1, CharacterClassCatalog.Soldier, "Soldier");
        soldier.Spawn(PlayerTeam.Blue, 100f, 100f);
        soldier.SetExperimentalOffhandWeapon(CharacterClassCatalog.SoldierShotgun);

        var shotgunInput = BotInputSynthesizer.Synthesize(
            soldier,
            default,
            soldier.X + 96f,
            soldier.Y,
            new CombatFireDecision(FirePrimary: false, FireSecondary: false, UseAbility: true),
            default);

        Assert.True(shotgunInput.SwapWeapon);
        Assert.False(shotgunInput.UseAbility);

        soldier.EquipExperimentalOffhandWeapon();
        var releaseInput = BotInputSynthesizer.Synthesize(
            soldier,
            default,
            soldier.X + 96f,
            soldier.Y,
            new CombatFireDecision(FirePrimary: true, FireSecondary: false, UseAbility: false),
            shotgunInput);

        Assert.False(releaseInput.SwapWeapon);
        Assert.False(releaseInput.UseAbility);

        var launcherInput = BotInputSynthesizer.Synthesize(
            soldier,
            default,
            soldier.X + 96f,
            soldier.Y,
            new CombatFireDecision(FirePrimary: true, FireSecondary: false, UseAbility: false),
            releaseInput);

        Assert.True(launcherInput.SwapWeapon);
        Assert.False(launcherInput.UseAbility);
    }

    [Fact]
    public void BotBrainClearsTransientNavigationStateAfterRespawnWithoutClassChange()
    {
        var world = CreateKothWorld(PlayerTeam.Blue, PlayerClass.Soldier, out var player);
        var controller = new BotBrainController(CreateSingleNodeGraph(player.X, player.Y, GameModeKind.KingOfTheHill));
        _ = controller.Think(player, world, PlayerTeam.Blue);
        SetControllerField(controller, "_platformLadderStage", 3);
        SetControllerField(controller, "_platformLadderSide", 1f);

        player.Kill();
        player.AddDeath();
        player.Spawn(PlayerTeam.Blue, player.X + 40f, player.Y);
        _ = controller.Think(player, world, PlayerTeam.Blue);

        Assert.Equal(0, GetControllerField<int>(controller, "_platformLadderStage"));
        Assert.Equal(0f, GetControllerField<float>(controller, "_platformLadderSide"));
    }

    [Fact]
    public void SniperCtfGoalHoldsSightlineWhenAllyCanSeekObjective()
    {
        var world = CreateClassWorld(PlayerClass.Sniper, out var sniper);
        var ally = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Red, sniper.X + 40f, sniper.Y);
        ally.PickUpIntel();
        var enemy = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Blue, sniper.X + 620f, sniper.Y);

        var goal = ObjectiveEvaluator.EvaluateGoal(sniper, world, PlayerTeam.Red, enemy);

        Assert.Equal(sniper.X, goal.X);
        Assert.Equal(sniper.Y, goal.Y);
    }

    [Fact]
    public void SniperCtfGoalTakesObjectiveWhenNoAllyCanSeekObjective()
    {
        var world = CreateClassWorld(PlayerClass.Sniper, out var sniper);

        var goal = ObjectiveEvaluator.EvaluateGoal(sniper, world, PlayerTeam.Red, combatTarget: null);

        Assert.Equal(400f, goal.X);
        Assert.Equal(100f, goal.Y);
    }

    [Fact]
    public void SniperCtfGoalRetrievesNearbyDroppedIntel()
    {
        var world = CreateClassWorld(PlayerClass.Sniper, out var sniper);
        _ = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Red, sniper.X + 40f, sniper.Y);
        world.BlueIntel.Drop(sniper.X + 160f, sniper.Y, returnTicks: 600);

        var goal = ObjectiveEvaluator.EvaluateGoal(sniper, world, PlayerTeam.Red, combatTarget: null);

        Assert.Equal(sniper.X + 160f, goal.X);
        Assert.Equal(sniper.Y, goal.Y);
    }

    [Fact]
    public void DoubleKothGoalDefendsOwnPointWhenEnemyIsCappingIt()
    {
        var world = CreateDoubleKothWorld(PlayerTeam.Red, out var player);
        var ownPoint = Assert.Single(world.ControlPoints, point => point.Marker.IsRedKothControlPoint());
        ownPoint.CappingTeam = PlayerTeam.Blue;

        var goal = ObjectiveEvaluator.EvaluateGoal(player, world, PlayerTeam.Red, combatTarget: null);

        Assert.Equal(ownPoint.HealingAuraCenterX, goal.X);
        Assert.Equal(ownPoint.HealingAuraCenterY, goal.Y);
    }

    [Fact]
    public void DoubleKothGoalAttacksEnemyPointWhenOwnPointIsSecure()
    {
        var world = CreateDoubleKothWorld(PlayerTeam.Red, out var player);
        var enemyPoint = Assert.Single(world.ControlPoints, point => point.Marker.IsBlueKothControlPoint());

        var goal = ObjectiveEvaluator.EvaluateGoal(player, world, PlayerTeam.Red, combatTarget: null);

        Assert.Equal(enemyPoint.HealingAuraCenterX, goal.X);
        Assert.Equal(enemyPoint.HealingAuraCenterY, goal.Y);
    }

    [Fact]
    public void EngineerBuildsSentryOnNeutralControlPoint()
    {
        var world = CreateControlPointWorld(PlayerTeam.Red, PlayerClass.Engineer, out var engineer);
        var point = Assert.Single(world.ControlPoints, point => point.Team is null);
        engineer.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
        engineer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(engineer.X, engineer.Y, GameModeKind.ControlPoint));

        var input = controller.Think(engineer, world, PlayerTeam.Red);

        Assert.True(input.BuildSentry);
        Assert.False(input.DestroySentry);
    }

    [Fact]
    public void EngineerBuildsSentryOnOwnedKothPoint()
    {
        var world = CreateKothWorld(PlayerTeam.Red, PlayerClass.Engineer, out var engineer);
        var point = Assert.Single(world.ControlPoints);
        point.Team = PlayerTeam.Red;
        engineer.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
        engineer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(engineer.X, engineer.Y, GameModeKind.KingOfTheHill));

        var input = controller.Think(engineer, world, PlayerTeam.Red);

        Assert.True(input.BuildSentry);
        Assert.False(input.DestroySentry);
    }

    [Fact]
    public void EngineerDestroysMisplacedSentryBeforeBuildingOnControlPoint()
    {
        var world = CreateControlPointWorld(PlayerTeam.Red, PlayerClass.Engineer, out var engineer);
        engineer.TeleportTo(20f, 100f);
        engineer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        Assert.True(world.TryBuildLocalSentry());

        var point = Assert.Single(world.ControlPoints, point => point.Team is null);
        engineer.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
        engineer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(engineer.X, engineer.Y, GameModeKind.ControlPoint));

        var input = controller.Think(engineer, world, PlayerTeam.Red);

        Assert.False(input.BuildSentry);
        Assert.True(input.DestroySentry);
    }

    [Fact]
    public void CtfEngineerPatrolsAroundBuiltIntelSentry()
    {
        var world = CreateClassWorld(PlayerClass.Engineer, out var engineer);
        engineer.TeleportTo(100f, 100f);
        engineer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        Assert.True(world.TryBuildLocalSentry());
        var controller = new BotBrainController(CreateSingleNodeGraph(engineer.X, engineer.Y, GameModeKind.CaptureTheFlag));

        var lateralTicks = CountLateralInputTicks(controller, engineer, world, PlayerTeam.Red, ticks: 90);

        Assert.True(lateralTicks > 0);
        Assert.Contains("engineerIntelDefense=patrol", controller.LastDirectDriveTrace);
    }

    [Fact]
    public void CtfEngineerChasesEnemyCarrierWhenOwnIntelIsPickedUp()
    {
        var world = CreateClassWorld(PlayerClass.Engineer, out var engineer);
        engineer.TeleportTo(100f, 100f);
        engineer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var carrier = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, engineer.X + 200f, engineer.Y);
        carrier.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        world.RedIntel.PickUp();
        carrier.PickUpIntel();
        var controller = new BotBrainController(CreateSingleNodeGraph(engineer.X, engineer.Y, GameModeKind.CaptureTheFlag));

        var input = controller.Think(engineer, world, PlayerTeam.Red);

        Assert.True(input.Right);
        Assert.False(input.BuildSentry);
        Assert.Contains("Carrier", controller.LastDirectDriveTrace, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BotDoesNotCaptureStrafeOnOwnedKothPoint()
    {
        var world = CreateKothWorld(PlayerTeam.Red, PlayerClass.Heavy, out var player);
        var point = Assert.Single(world.ControlPoints);
        point.Team = PlayerTeam.Red;
        point.IsLocked = true;
        player.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(player.X, player.Y, GameModeKind.KingOfTheHill));

        var lateralTicks = CountLateralInputTicks(controller, player, world, PlayerTeam.Red, ticks: 32);

        Assert.Equal(0, lateralTicks);
        Assert.DoesNotContain("lockedHold", controller.LastDirectDriveTrace);
        Assert.DoesNotContain("captureStrafeHop", controller.LastDirectDriveTrace);
    }

    [Fact]
    public void BotStrafesMoreOftenWhileCapturingControlPoint()
    {
        var world = CreateControlPointWorld(PlayerTeam.Red, PlayerClass.Heavy, out var player);
        var point = Assert.Single(world.ControlPoints, point => point.Team is null);
        point.IsLocked = false;
        point.CappingTeam = PlayerTeam.Red;
        player.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(player.X, player.Y, GameModeKind.ControlPoint));

        var lateralTicks = CountLateralInputTicks(controller, player, world, PlayerTeam.Red, ticks: 32);

        Assert.True(lateralTicks >= 10);
        Assert.Contains("reason:capture", controller.LastDirectDriveTrace);
    }

    [Fact]
    public void BotClearsNearbyEnemyBeforeSettlingOntoControlPoint()
    {
        var world = CreateControlPointWorld(PlayerTeam.Red, PlayerClass.Heavy, out var player);
        var point = Assert.Single(world.ControlPoints, point => point.Team is null);
        point.IsLocked = false;
        point.CappingTeam = PlayerTeam.Red;
        player.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, point.HealingAuraCenterX + 160f, point.HealingAuraCenterY);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(player.X, player.Y, GameModeKind.ControlPoint));

        var input = controller.Think(player, world, PlayerTeam.Red);

        Assert.False(input.Left);
        Assert.True(input.Right);
        Assert.Equal(1f, controller.LastSteeringOutput.MoveDirection);
        Assert.Contains("controlPointClearEnemy", controller.LastDirectDriveTrace);
    }

    [Fact]
    public void BotClearsEnemyOnOwnedPointInsteadOfCaptureHolding()
    {
        var world = CreateKothWorld(PlayerTeam.Red, PlayerClass.Heavy, out var player);
        var point = Assert.Single(world.ControlPoints);
        point.Team = PlayerTeam.Red;
        point.IsLocked = true;
        point.CappingTeam = PlayerTeam.Blue;
        player.TeleportTo(point.HealingAuraCenterX, point.HealingAuraCenterY);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, point.HealingAuraCenterX + 8f, point.HealingAuraCenterY);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(player.X, player.Y, GameModeKind.KingOfTheHill));

        var input = controller.Think(player, world, PlayerTeam.Red);

        Assert.True(input.Left);
        Assert.False(input.Right);
        Assert.Equal(-1f, controller.LastSteeringOutput.MoveDirection);
        Assert.Contains("controlPointClearEnemy", controller.LastDirectDriveTrace);
        Assert.DoesNotContain("lockedHold", controller.LastDirectDriveTrace);
    }

    [Fact]
    public void BotBacksAwayFromPointBlankCombatTarget()
    {
        var world = CreateClassWorld(PlayerClass.Heavy, out var player);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, player.X + 8f, player.Y);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(player.X, player.Y, GameModeKind.CaptureTheFlag));

        var input = controller.Think(player, world, PlayerTeam.Red);

        Assert.True(input.Left);
        Assert.False(input.Right);
        Assert.Equal(-1f, controller.LastSteeringOutput.MoveDirection);
        Assert.Contains("combat:spacing", controller.LastDirectDriveTrace);
    }

    [Fact]
    public void BotKeepsMovingWhileEngagingAtCombatRange()
    {
        var world = CreateClassWorld(PlayerClass.Heavy, out var player);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Blue, player.X + 150f, player.Y);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var controller = new BotBrainController(CreateSingleNodeGraph(player.X, player.Y, GameModeKind.CaptureTheFlag));

        var input = controller.Think(player, world, PlayerTeam.Red);

        Assert.True(input.Left || input.Right);
        Assert.NotEqual(0f, controller.LastSteeringOutput.MoveDirection);
        Assert.Contains("combat:spacing", controller.LastDirectDriveTrace);
    }

    [Fact]
    public void ControlPointCaptureMovementIsDesynchronizedByTeam()
    {
        var redWorld = CreateControlPointWorld(PlayerTeam.Red, PlayerClass.Heavy, out var redPlayer);
        var redPoint = Assert.Single(redWorld.ControlPoints, point => point.Team is null);
        redPoint.CappingTeam = PlayerTeam.Red;
        redPlayer.TeleportTo(redPoint.HealingAuraCenterX, redPoint.HealingAuraCenterY);
        redPlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var redController = new BotBrainController(CreateSingleNodeGraph(redPlayer.X, redPlayer.Y, GameModeKind.ControlPoint));

        var blueWorld = CreateControlPointWorld(PlayerTeam.Blue, PlayerClass.Heavy, out var bluePlayer);
        var bluePoint = Assert.Single(blueWorld.ControlPoints, point => point.Team is null);
        bluePoint.CappingTeam = PlayerTeam.Blue;
        bluePlayer.TeleportTo(bluePoint.HealingAuraCenterX, bluePoint.HealingAuraCenterY);
        bluePlayer.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var blueController = new BotBrainController(CreateSingleNodeGraph(bluePlayer.X, bluePlayer.Y, GameModeKind.ControlPoint));

        var redPattern = CaptureMovementPattern(redController, redPlayer, redWorld, PlayerTeam.Red, ticks: 48);
        var bluePattern = CaptureMovementPattern(blueController, bluePlayer, blueWorld, PlayerTeam.Blue, ticks: 48);

        Assert.NotEqual(redPattern, bluePattern);
    }

    [Fact]
    public void DirectDriveDoesNotJumpInPlaceIntoCeilingForVerticalTarget()
    {
        var world = CreateDirectDriveWorld(hasBlockedHeadroom: true, out var player);

        var resolved = PrimitiveDirectDrive.TryResolveRecovery(
            world,
            player,
            new DirectDriveTarget(DirectDriveTargetKind.Carrier, player.X, player.Y - 160f, "carrier"),
            default,
            out _,
            out _);

        Assert.False(resolved);
    }

    [Fact]
    public void DirectDriveStillMovesTowardHorizontalTargetWhenHeadroomBlocked()
    {
        var world = CreateDirectDriveWorld(hasBlockedHeadroom: true, out var player);

        var resolved = PrimitiveDirectDrive.TryResolveRecovery(
            world,
            player,
            new DirectDriveTarget(DirectDriveTargetKind.Carrier, player.X + 180f, player.Y - 160f, "carrier"),
            default,
            out var steering,
            out _);

        Assert.True(resolved);
        Assert.Equal(1, steering.MoveDirection);
    }

    [Fact]
    public void PreferredEnemyPlayerSeekReusesGraphPathWhenGoalNodeStaysStable()
    {
        var world = CreateClassWorld(PlayerClass.Demoman, out var player);
        player.TeleportTo(0f, 100f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, 780f, 100f);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var controller = new BotBrainController(CreateWideStraightWalkGraph())
        {
            PreferEnemyPlayerObjective = true,
        };

        _ = controller.Think(player, world, PlayerTeam.Red);
        enemy.TeleportTo(820f, 100f);
        var input = controller.Think(player, world, PlayerTeam.Red);

        Assert.Equal(2, controller.CurrentGoalNode);
        Assert.True(input.Right);
        Assert.Contains("reuseGoal", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void PreferredEnemyPlayerSeekUsesGraphRouteForDistantGoalProxy()
    {
        var world = CreateClassWorld(PlayerClass.Demoman, out var player);
        player.TeleportTo(0f, 100f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, 1_200f, 100f);
        enemy.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: -1f);
        var controller = new BotBrainController(CreateWideStraightWalkGraph())
        {
            PreferEnemyPlayerObjective = true,
        };

        var input = controller.Think(player, world, PlayerTeam.Red);

        Assert.Equal(2, controller.CurrentGoalNode);
        Assert.True(input.Right);
        Assert.Contains("directRoute=preferredEnemy", controller.LastDirectDriveTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void SteeringSkipsPassedInitialWalkAttachmentOnSameSurface()
    {
        var world = CreateClassWorld(PlayerClass.Pyro, out var player);
        player.TeleportTo(135f, 100f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var graph = CreateStraightWalkGraph();
        var path = graph.FindPath(0, 3, PlayerClass.Pyro, team: PlayerTeam.Red);
        Assert.NotNull(path);

        var steering = new SteeringMachine().Update(player, graph, path, world.Level, PlayerTeam.Red);

        Assert.Equal(2, path!.CurrentIndex);
        Assert.Equal(1f, steering.MoveDirection);
    }

    [Fact]
    public void SteeringSkipsPassedWalkWaypointOnSameSurface()
    {
        var world = CreateClassWorld(PlayerClass.Pyro, out var player);
        player.TeleportTo(105f, 100f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var graph = CreateStraightWalkGraph();
        var path = graph.FindPath(0, 3, PlayerClass.Pyro, team: PlayerTeam.Red);
        Assert.NotNull(path);
        path!.Advance();

        var steering = new SteeringMachine().Update(player, graph, path, world.Level, PlayerTeam.Red);

        Assert.Equal(2, path.CurrentIndex);
        Assert.Equal(1f, steering.MoveDirection);
    }

    [Fact]
    public void SteeringJumpsImmediatelyIntoJumpableWalkObstacle()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Scout");
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        player.TeleportTo(100f, 100f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var level = CreateJumpableObstacleLevel(player, direction: 1f);
        var graph = CreateObstacleWalkGraph(player.X, player.Y, player.X + 160f, player.Y);
        var path = graph.FindPath(0, 1, PlayerClass.Scout, team: PlayerTeam.Red);
        Assert.NotNull(path);

        var steering = new SteeringMachine().Update(player, graph, path, level, PlayerTeam.Red);

        Assert.Equal(1f, steering.MoveDirection);
        Assert.True(steering.Jump);
    }

    [Fact]
    public void SteeringFastPulsesJumpWhenPressedAgainstJumpableWalkObstacle()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Scout");
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        player.TeleportTo(100f, 100f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var level = CreateJumpableObstacleLevel(player, direction: 1f);
        var graph = CreateObstacleWalkGraph(player.X, player.Y, player.X + 160f, player.Y);
        var path = graph.FindPath(0, 1, PlayerClass.Scout, team: PlayerTeam.Red);
        Assert.NotNull(path);
        var steeringMachine = new SteeringMachine();

        var first = steeringMachine.Update(player, graph, path, level, PlayerTeam.Red);
        var second = steeringMachine.Update(player, graph, path!, level, PlayerTeam.Red);
        var third = steeringMachine.Update(player, graph, path!, level, PlayerTeam.Red);

        Assert.True(first.Jump);
        Assert.False(second.Jump);
        Assert.True(third.Jump);
    }

    [Fact]
    public void SteeringQuicklyHopsWhenNonWalkEdgePressesIntoJumpableObstacle()
    {
        var player = new PlayerEntity(1, CharacterClassCatalog.Scout, "Scout");
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        player.TeleportTo(100f, 100f);
        player.RestoreMovementProbeState(isGrounded: true, remainingAirJumps: null, facingDirectionX: 1f);
        var level = CreateJumpableObstacleLevel(player, direction: 1f);
        var graph = CreateObstacleFallGraph(player.X, player.Y, player.X + 160f, player.Y);
        var path = graph.FindPath(0, 1, PlayerClass.Scout, team: PlayerTeam.Red);
        Assert.NotNull(path);
        path!.Advance();
        var steeringMachine = new SteeringMachine();

        var first = steeringMachine.Update(player, graph, path, level, PlayerTeam.Red);
        var second = steeringMachine.Update(player, graph, path, level, PlayerTeam.Red);
        var third = steeringMachine.Update(player, graph, path, level, PlayerTeam.Red);

        Assert.Equal(1f, first.MoveDirection);
        Assert.False(first.Jump);
        Assert.False(second.Jump);
        Assert.True(third.Jump);
    }

    [Fact]
    public void CarrierReturnUsesGraphForKnownProblemMaps()
    {
        var conflict = SimpleLevelFactory.CreateImportedLevel("Conflict");
        var waterway = SimpleLevelFactory.CreateImportedLevel("Waterway");
        var truefort = SimpleLevelFactory.CreateImportedLevel("Truefort");
        Assert.NotNull(conflict);
        Assert.NotNull(waterway);
        Assert.NotNull(truefort);
        var heavy = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Heavy");
        var scout = new PlayerEntity(2, CharacterClassCatalog.Scout, "Scout");

        Assert.True(InvokeShouldPreferCarrierReturnGraph(conflict!, heavy));
        Assert.False(InvokeShouldPreferCarrierReturnGraph(conflict!, scout));
        Assert.True(InvokeShouldPreferCarrierReturnGraph(waterway!, heavy));
        Assert.True(InvokeShouldPreferCarrierReturnGraph(waterway!, scout));
        Assert.True(InvokeShouldPreferCarrierReturnGraph(truefort!, heavy));
        Assert.True(InvokeShouldPreferCarrierReturnGraph(truefort!, scout));
    }

    [Fact]
    public void CarrierReturnBypassesProofGraphOnlyWhenProofGraphPoisonsReturn()
    {
        var conflict = CreateImportedWorld("Conflict");
        var waterway = CreateImportedWorld("Waterway");
        var truefort = CreateImportedWorld("Truefort");
        var heavy = new PlayerEntity(1, CharacterClassCatalog.Heavy, "Heavy");
        var scout = new PlayerEntity(2, CharacterClassCatalog.Scout, "Scout");
        heavy.PickUpIntel();
        scout.PickUpIntel();

        Assert.True(InvokeShouldBypassCarrierReturnProofGraph(conflict, heavy, proofGraphRequired: false));
        Assert.False(InvokeShouldBypassCarrierReturnProofGraph(conflict, scout, proofGraphRequired: false));
        Assert.True(InvokeShouldBypassCarrierReturnProofGraph(waterway, heavy, proofGraphRequired: false));
        Assert.True(InvokeShouldBypassCarrierReturnProofGraph(waterway, scout, proofGraphRequired: false));
        Assert.False(InvokeShouldBypassCarrierReturnProofGraph(waterway, scout, proofGraphRequired: true));
        Assert.False(InvokeShouldBypassCarrierReturnProofGraph(truefort, heavy, proofGraphRequired: false));
        Assert.False(InvokeShouldBypassCarrierReturnProofGraph(truefort, scout, proofGraphRequired: false));
    }

    [Fact]
    public void CarrierReturnEscapeDoesNotOverrideFarRecoveryDirection()
    {
        var controller = new BotBrainController();
        var carrier = new PlayerEntity(1, CharacterClassCatalog.Scout, "Scout");
        carrier.Spawn(PlayerTeam.Red, 4416f, 642f);
        carrier.TeleportTo(4416f, 642f);
        var steering = new SteeringOutput { MoveDirection = -1f };
        var trace = "localMotion=active label:dynamicCarrierReturnBaseAfterProofFailure";

        for (var i = 0; i < 45; i += 1)
        {
            InvokeApplyCarrierReturnDirectEscape(controller, carrier, targetX: 384f, ref steering, ref trace);
        }

        Assert.Equal(-1f, steering.MoveDirection);
        Assert.DoesNotContain("escape:carrierReturn", trace, StringComparison.Ordinal);
    }

    [Fact]
    public void CarrierReturnEscapeStillAppliesNearReturnTarget()
    {
        var controller = new BotBrainController();
        var carrier = new PlayerEntity(1, CharacterClassCatalog.Scout, "Scout");
        carrier.Spawn(PlayerTeam.Red, 600f, 642f);
        carrier.TeleportTo(600f, 642f);
        var steering = new SteeringOutput { MoveDirection = -1f };
        var trace = "localMotion=active label:dynamicCarrierReturnBase";

        for (var i = 0; i < 45; i += 1)
        {
            InvokeApplyCarrierReturnDirectEscape(controller, carrier, targetX: 384f, ref steering, ref trace);
        }

        Assert.Equal(1f, steering.MoveDirection);
        Assert.Contains("escape:carrierReturn", trace, StringComparison.Ordinal);
    }

    private static SimulationWorld CreateClassWorld(PlayerClass playerClass, out PlayerEntity player)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(playerClass));
        SetCombatLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        player = world.LocalPlayer;
        player.TeleportTo(100f, 100f);
        return world;
    }

    private static SimulationWorld CreateDoubleKothWorld(PlayerTeam team, out PlayerEntity player)
    {
        return CreateDoubleKothWorld(team, PlayerClass.Heavy, out player);
    }

    private static SimulationWorld CreateDoubleKothWorld(PlayerTeam team, PlayerClass playerClass, out PlayerEntity player)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(playerClass));
        SetDoubleKothLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, team));
        world.ForceRespawnLocalPlayer();
        player = world.LocalPlayer;
        player.TeleportTo(120f, 100f);
        return world;
    }

    private static SimulationWorld CreateControlPointWorld(PlayerTeam team, PlayerClass playerClass, out PlayerEntity player)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(playerClass));
        SetControlPointLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, team));
        world.ForceRespawnLocalPlayer();
        player = world.LocalPlayer;
        player.TeleportTo(120f, 100f);
        return world;
    }

    private static SimulationWorld CreateKothWorld(PlayerTeam team, PlayerClass playerClass, out PlayerEntity player)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(playerClass));
        SetKothLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, team));
        world.ForceRespawnLocalPlayer();
        player = world.LocalPlayer;
        player.TeleportTo(120f, 100f);
        return world;
    }

    private static SimulationWorld CreateDirectDriveWorld(bool hasBlockedHeadroom, out PlayerEntity player)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Pyro));
        SetDirectDriveLevel(world, hasBlockedHeadroom);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.ForceRespawnLocalPlayer();
        player = world.LocalPlayer;
        player.TeleportTo(200f, 120f);
        return world;
    }

    private static int FindProjectileIdForPyroReflect(PlayerEntity pyro, bool accurate)
    {
        var method = typeof(CombatDecisionResolver).GetMethod("ShouldPyroReflectAccurately", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        for (var id = 1; id < 512; id += 1)
        {
            var result = (bool)method!.Invoke(null, [pyro, id])!;
            if (result == accurate)
            {
                return id;
            }
        }

        throw new InvalidOperationException("Could not find deterministic Pyro reflect projectile id.");
    }

    private static int FindProjectileIdForHeavyDash(PlayerEntity heavy)
    {
        var method = typeof(CombatDecisionResolver).GetMethod("ShouldHeavyDashIncomingProjectile", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        for (var id = 1; id < 512; id += 1)
        {
            var result = (bool)method!.Invoke(null, [heavy, id])!;
            if (result)
            {
                return id;
            }
        }

        throw new InvalidOperationException("Could not find deterministic Heavy dash projectile id.");
    }

    private static bool InvokeShouldPreferCarrierReturnGraph(SimpleLevel level, PlayerEntity player)
    {
        var method = typeof(BotBrainController).GetMethod(
            "ShouldPreferCarrierReturnGraph",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(SimpleLevel), typeof(PlayerEntity)],
            modifiers: null);
        Assert.NotNull(method);
        return (bool)method!.Invoke(null, [level, player])!;
    }

    private static bool InvokeShouldBypassCarrierReturnProofGraph(
        SimulationWorld world,
        PlayerEntity player,
        bool proofGraphRequired)
    {
        var method = typeof(BotBrainController).GetMethod(
            "ShouldBypassCarrierReturnProofGraph",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(SimulationWorld), typeof(PlayerEntity), typeof(bool)],
            modifiers: null);

        Assert.NotNull(method);
        return (bool)method!.Invoke(null, [world, player, proofGraphRequired])!;
    }

    private static void InvokeApplyCarrierReturnDirectEscape(
        BotBrainController controller,
        PlayerEntity player,
        float targetX,
        ref SteeringOutput steering,
        ref string trace)
    {
        var method = typeof(BotBrainController).GetMethod(
            "ApplyCarrierReturnDirectEscape",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        object?[] args = [player, targetX, steering, trace];
        method!.Invoke(controller, args);
        steering = (SteeringOutput)args[2]!;
        trace = (string)args[3]!;
    }

    private static void SetControllerField<T>(BotBrainController controller, string fieldName, T value)
    {
        var field = typeof(BotBrainController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(controller, value);
    }

    private static T GetControllerField<T>(BotBrainController controller, string fieldName)
    {
        var field = typeof(BotBrainController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(controller));
    }

    private static SimulationWorld CreateImportedWorld(string levelName)
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel(levelName, 1, preservePlayerStats: false));
        return world;
    }

    private static void AddIncomingRocket(SimulationWorld world, int projectileId, PlayerEntity target, float timeToImpactTicks)
    {
        const float speed = 12f;
        var rocket = new RocketProjectileEntity(
            projectileId,
            PlayerTeam.Blue,
            ownerId: 900 + projectileId,
            target.X + speed * timeToImpactTicks,
            target.Y,
            speed,
            directionRadians: MathF.PI);
        var field = typeof(SimulationWorld).GetField("_rockets", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var rockets = Assert.IsType<List<RocketProjectileEntity>>(field!.GetValue(world));
        rockets.Add(rocket);
    }

    private static int CountLateralInputTicks(
        BotBrainController controller,
        PlayerEntity player,
        SimulationWorld world,
        PlayerTeam team,
        int ticks)
    {
        var lateralTicks = 0;
        for (var tick = 0; tick < ticks; tick += 1)
        {
            var input = controller.Think(player, world, team);
            if (input.Left || input.Right)
            {
                lateralTicks += 1;
            }
        }

        return lateralTicks;
    }

    private static string CaptureMovementPattern(
        BotBrainController controller,
        PlayerEntity player,
        SimulationWorld world,
        PlayerTeam team,
        int ticks)
    {
        var pattern = new char[ticks];
        for (var tick = 0; tick < ticks; tick += 1)
        {
            var input = controller.Think(player, world, team);
            pattern[tick] = input.Up
                ? 'U'
                : input.Left
                    ? 'L'
                    : input.Right
                        ? 'R'
                        : '.';
        }

        return new string(pattern);
    }

    private static void SetCombatLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_class_behavior_test",
                    mode: GameModeKind.CaptureTheFlag,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(100f, 100f),
                    redSpawns: [new SpawnPoint(100f, 100f)],
                    blueSpawns: [new SpawnPoint(400f, 100f)],
                    intelBases:
                    [
                        new IntelBaseMarker(PlayerTeam.Red, 100f, 100f),
                        new IntelBaseMarker(PlayerTeam.Blue, 400f, 100f),
                    ],
                    roomObjects: [],
                    floorY: 2048f,
                    solids: [],
                    importedFromSource: false),
            ]);
    }

    private static void SetControlPointLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_control_point_behavior_test",
                    mode: GameModeKind.ControlPoint,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(120f, 100f),
                    redSpawns: [new SpawnPoint(120f, 100f)],
                    blueSpawns: [new SpawnPoint(520f, 100f)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            80f,
                            90f,
                            40f,
                            20f,
                            "",
                            SourceName: "RedControlPoint"),
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            280f,
                            90f,
                            40f,
                            20f,
                            "",
                            SourceName: "MiddleControlPoint"),
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            480f,
                            90f,
                            40f,
                            20f,
                            "",
                            SourceName: "BlueControlPoint"),
                    ],
                    floorY: 2048f,
                    solids: [],
                    importedFromSource: false),
            ]);
    }

    private static void SetKothLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_koth_behavior_test",
                    mode: GameModeKind.KingOfTheHill,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(120f, 100f),
                    redSpawns: [new SpawnPoint(120f, 100f)],
                    blueSpawns: [new SpawnPoint(520f, 100f)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            280f,
                            90f,
                            40f,
                            20f,
                            "",
                            SourceName: "KothControlPoint"),
                    ],
                    floorY: 2048f,
                    solids: [],
                    importedFromSource: false),
            ]);
    }

    private static void SetDoubleKothLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_double_koth_behavior_test",
                    mode: GameModeKind.DoubleKingOfTheHill,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(120f, 100f),
                    redSpawns: [new SpawnPoint(120f, 100f)],
                    blueSpawns: [new SpawnPoint(520f, 100f)],
                    intelBases: [],
                    roomObjects:
                    [
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            80f,
                            90f,
                            40f,
                            20f,
                            "",
                            SourceName: "KothRedControlPoint"),
                        new RoomObjectMarker(
                            RoomObjectType.ControlPoint,
                            480f,
                            90f,
                            40f,
                            20f,
                            "",
                            SourceName: "KothBlueControlPoint"),
                    ],
                    floorY: 2048f,
                    solids: [],
                importedFromSource: false),
            ]);
    }

    private static void SetDirectDriveLevel(SimulationWorld world, bool hasBlockedHeadroom)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_direct_drive_test",
                    mode: GameModeKind.CaptureTheFlag,
                    bounds: new WorldBounds(2048f, 2048f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(200f, 120f),
                    redSpawns: [new SpawnPoint(200f, 120f)],
                    blueSpawns: [new SpawnPoint(600f, 120f)],
                    intelBases:
                    [
                        new IntelBaseMarker(PlayerTeam.Red, 200f, 120f),
                        new IntelBaseMarker(PlayerTeam.Blue, 600f, 120f),
                    ],
                    roomObjects: [],
                    floorY: 2048f,
                    solids: hasBlockedHeadroom ? [new LevelSolid(120f, 20f, 180f, 80f)] : [],
                    importedFromSource: false),
            ]);
    }

    private static SimpleLevel CreateJumpableObstacleLevel(PlayerEntity player, float direction)
    {
        var obstacleLeft = direction > 0f
            ? player.Right + 2f
            : player.Left - 14f;
        var obstacleTop = player.Bottom - 24f;
        return new SimpleLevel(
            name: "botbrain_jumpable_obstacle_test",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(2048f, 1024f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: new SpawnPoint(player.X, player.Y),
            redSpawns: [new SpawnPoint(player.X, player.Y)],
            blueSpawns: [new SpawnPoint(player.X + 320f, player.Y)],
            intelBases:
            [
                new IntelBaseMarker(PlayerTeam.Red, player.X, player.Y),
                new IntelBaseMarker(PlayerTeam.Blue, player.X + 320f, player.Y),
            ],
            roomObjects: [],
            floorY: 1024f,
            solids:
            [
                new LevelSolid(obstacleLeft, obstacleTop, obstacleLeft + 12f, player.Bottom),
            ],
            importedFromSource: false);
    }

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team,
        float x,
        float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }

    private static NavGraph CreateObstacleWalkGraph(float startX, float startY, float targetX, float targetY)
    {
        var nodes = new[]
        {
            new NavNode(startX, startY, NavNodeKind.Surface, 1),
            new NavNode(targetX, targetY, NavNodeKind.Surface, 1),
        };
        var adjacency = CreateAdjacency(nodes.Length);
        AddWalkEdge(adjacency, 0, 1, MathF.Abs(targetX - startX));
        return new NavGraph(nodes, adjacency, levelName: "Synthetic", mode: GameModeKind.CaptureTheFlag);
    }

    private static NavGraph CreateObstacleFallGraph(float startX, float startY, float targetX, float targetY)
    {
        var nodes = new[]
        {
            new NavNode(startX, startY, NavNodeKind.Surface, 1),
            new NavNode(targetX, targetY, NavNodeKind.Surface, 1),
        };
        var adjacency = CreateAdjacency(nodes.Length);
        AddFallEdge(adjacency, 0, 1, MathF.Abs(targetX - startX));
        return new NavGraph(nodes, adjacency, levelName: "Synthetic", mode: GameModeKind.CaptureTheFlag);
    }

    private static NavGraph CreateSingleNodeGraph(float x, float y, GameModeKind mode)
    {
        var nodes = new[]
        {
            new NavNode(x, y, NavNodeKind.Surface, 1),
        };
        return new NavGraph(nodes, CreateAdjacency(nodes.Length), levelName: "Synthetic", mode: mode);
    }

    private static NavGraph CreateStraightWalkGraph()
    {
        var nodes = new[]
        {
            new NavNode(0f, 100f, NavNodeKind.Ledge, 1),
            new NavNode(80f, 100f, NavNodeKind.Surface, 1),
            new NavNode(160f, 100f, NavNodeKind.Surface, 1),
            new NavNode(240f, 100f, NavNodeKind.Ledge, 1),
        };
        var adjacency = CreateAdjacency(nodes.Length);
        AddWalkEdge(adjacency, 0, 1, 80f);
        AddWalkEdge(adjacency, 1, 2, 80f);
        AddWalkEdge(adjacency, 2, 3, 80f);
        return new NavGraph(nodes, adjacency, levelName: "Synthetic", mode: GameModeKind.CaptureTheFlag);
    }

    private static NavGraph CreateWideStraightWalkGraph()
    {
        var nodes = new[]
        {
            new NavNode(0f, 100f, NavNodeKind.Ledge, 1),
            new NavNode(400f, 100f, NavNodeKind.Surface, 1),
            new NavNode(800f, 100f, NavNodeKind.Ledge, 1),
        };
        var adjacency = CreateAdjacency(nodes.Length);
        AddWalkEdge(adjacency, 0, 1, 400f);
        AddWalkEdge(adjacency, 1, 2, 400f);
        return new NavGraph(nodes, adjacency, levelName: "Synthetic", mode: GameModeKind.CaptureTheFlag);
    }

    private static List<NavEdge>[] CreateAdjacency(int count)
    {
        var adjacency = new List<NavEdge>[count];
        for (var index = 0; index < adjacency.Length; index += 1)
        {
            adjacency[index] = [];
        }

        return adjacency;
    }

    private static void AddWalkEdge(List<NavEdge>[] adjacency, int from, int to, float cost)
    {
        adjacency[from].Add(new NavEdge(to, NavEdgeKind.Walk, cost));
        adjacency[to].Add(new NavEdge(from, NavEdgeKind.Walk, cost));
    }

    private static void AddFallEdge(List<NavEdge>[] adjacency, int from, int to, float cost)
    {
        adjacency[from].Add(new NavEdge(to, NavEdgeKind.Fall, cost));
        adjacency[to].Add(new NavEdge(from, NavEdgeKind.Fall, cost));
    }
}
