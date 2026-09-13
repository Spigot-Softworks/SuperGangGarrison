using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotSecondaryWeaponFiringTests
{
    [Fact]
    public void MedicReturnsToMedigunWhenAHealingTargetBecomesAvailable()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Medic);
        var medic = world.LocalPlayer;
        medic.EquipExperimentalOffhandWeapon();
        var enemy = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Blue, medic.X + 140, medic.Y);
        var ally = AddNetworkPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Red, medic.X + 80, medic.Y);
        var decision = CombatDecisionResolver.Resolve(world, medic,
            new BotBrainCombatTarget(BotBrainCombatTargetKind.Player, enemy.Team, enemy.X, enemy.Y, Player: enemy),
            ally, new CombatDecisionMemory());
        Assert.False(decision.SelectSecondaryWeapon);
        Assert.True(decision.FirePrimary);
    }

    [Fact]
    public void SoldierFiresShotgunAfterBotCompletesSecondarySelection()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        world.LocalPlayer.SetSpawnRoomState(false);

        var soldier = world.LocalPlayer;
        Assert.True(soldier.HasExperimentalOffhandWeapon);
        var target = AddNetworkPlayer(
            world,
            2,
            PlayerClass.Heavy,
            PlayerTeam.Blue,
            soldier.X + 140f,
            soldier.Y);
        var combatTarget = new BotBrainCombatTarget(
            BotBrainCombatTargetKind.Player,
            target.Team,
            target.X,
            target.Y,
            Player: target);
        var memory = new CombatDecisionMemory();

        // Put the stowed primary on cooldown so the first combat pass asks for
        // the shotgun. The old path sent only the toggle, then immediately
        // decided to stow it again before ever producing an M1 fire intent.
        Assert.True(soldier.TryFirePrimaryWeapon());
        var selectionDecision = CombatDecisionResolver.Resolve(
            world,
            soldier,
            combatTarget,
            healTarget: null,
            memory);
        Assert.True(selectionDecision.SelectSecondaryWeapon);
        var selectionInput = BotInputSynthesizer.Synthesize(
            soldier,
            default,
            target.X,
            target.Y,
            selectionDecision,
            default);
        Assert.True(selectionInput.ToggleSecondaryWeapon);
        Assert.False(selectionInput.FirePrimary);

        world.SetLocalInput(selectionInput);
        world.AdvanceOneTick();
        Assert.True(soldier.IsExperimentalOffhandSelected);

        var secondaryAmmoBefore = soldier.ExperimentalOffhandCurrentShells;
        var firingDecision = CombatDecisionResolver.Resolve(
            world,
            soldier,
            combatTarget,
            healTarget: null,
            memory);
        Assert.True(firingDecision.SelectSecondaryWeapon);
        Assert.True(firingDecision.FirePrimary);
        var firingInput = BotInputSynthesizer.Synthesize(
            soldier,
            default,
            target.X,
            target.Y,
            firingDecision,
            selectionInput);
        Assert.True(firingInput.FirePrimary);
        Assert.False(firingInput.ToggleSecondaryWeapon);

        world.SetLocalInput(firingInput);
        world.AdvanceOneTick();

        Assert.True(soldier.IsExperimentalOffhandSelected);
        Assert.Equal(secondaryAmmoBefore - 1, soldier.ExperimentalOffhandCurrentShells);
        Assert.NotEmpty(world.Shots.Where(shot => shot.OwnerId == soldier.Id));
    }

    [Theory]
    [InlineData(PlayerClass.Scout)]
    [InlineData(PlayerClass.Sniper)]
    [InlineData(PlayerClass.Medic)]
    public void SelectedStockSecondaryProducesM1FireIntent(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        world.LocalPlayer.SetSpawnRoomState(false);

        var player = world.LocalPlayer;
        Assert.True(player.HasExperimentalOffhandWeapon);
        player.EquipExperimentalOffhandWeapon();
        Assert.True(player.IsExperimentalOffhandSelected);

        var target = AddNetworkPlayer(
            world,
            2,
            PlayerClass.Heavy,
            PlayerTeam.Blue,
            player.X + 180f,
            player.Y);
        var combatTarget = new BotBrainCombatTarget(
            BotBrainCombatTargetKind.Player,
            target.Team,
            target.X,
            target.Y,
            Player: target);
        var memory = new CombatDecisionMemory();
        var ammoBefore = player.ExperimentalOffhandCurrentShells;

        var decision = CombatDecisionResolver.Resolve(
            world,
            player,
            combatTarget,
            healTarget: null,
            memory);

        Assert.True(decision.FirePrimary);
        Assert.True(decision.SelectSecondaryWeapon);
        var input = BotInputSynthesizer.Synthesize(
            player,
            default,
            target.X,
            target.Y,
            decision,
            default);
        Assert.True(input.FirePrimary);
        Assert.False(input.ToggleSecondaryWeapon);

        world.SetLocalInput(input);
        world.AdvanceOneTick();

        Assert.Equal(ammoBefore - 1, player.ExperimentalOffhandCurrentShells);
        Assert.True(
            world.Shots.Any(shot => shot.OwnerId == player.Id)
            || world.Needles.Any(needle => needle.OwnerId == player.Id));
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
        world.TrySetNetworkPlayerTeam(slot, team);
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        Assert.Equal(team, player.Team);
        player.TeleportTo(x, y);
        player.SetSpawnRoomState(false);
        return player;
    }
}
