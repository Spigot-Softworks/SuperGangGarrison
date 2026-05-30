using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BotBrainMedicHealTargetTests
{
    [Fact]
    public void MedicHealTargetPrefersHighestMaxHealthAllyWhenNoAllyIsCritical()
    {
        var world = CreateMedicTargetWorld(
            out var medic,
            out var scout,
            out var soldier,
            out var heavy);
        scout.ForceSetHealth(scout.MaxHealth);
        soldier.ForceSetHealth(soldier.MaxHealth);
        heavy.ForceSetHealth(heavy.MaxHealth);

        var target = CombatDecisionResolver.FindBestMedicHealTarget(world, medic, PlayerTeam.Red);

        Assert.Same(heavy, target);
    }

    [Fact]
    public void MedicHealTargetKeepsCriticalTriageAbovePocketPriority()
    {
        var world = CreateMedicTargetWorld(
            out var medic,
            out var scout,
            out _,
            out var heavy);
        scout.ForceSetHealth(Math.Max(1, (int)MathF.Floor(scout.MaxHealth * 0.4f)));
        heavy.ForceSetHealth(heavy.MaxHealth);

        var target = CombatDecisionResolver.FindBestMedicHealTarget(world, medic, PlayerTeam.Red);

        Assert.Same(scout, target);
    }

    [Fact]
    public void MedicHealTargetSkipsCloakedSpyEvenWhenCritical()
    {
        var world = CreateMedicTargetWorld(
            out var medic,
            out var scout,
            out var soldier,
            out var heavy);
        scout.ForceSetHealth(scout.MaxHealth);
        soldier.ForceSetHealth(soldier.MaxHealth);
        heavy.ForceSetHealth(heavy.MaxHealth);
        var spy = AddNetworkPlayer(world, 5, PlayerClass.Spy, medic.X + 50f, medic.Y);
        spy.ForceSetHealth(1);
        Assert.True(spy.TryToggleSpyCloak());

        var target = CombatDecisionResolver.FindBestMedicHealTarget(world, medic, PlayerTeam.Red);

        Assert.Same(heavy, target);
    }

    [Fact]
    public void MedicHealTargetPrioritizesHumanMedicCallOverPocketTarget()
    {
        var world = CreateMedicTargetWorld(
            out var medic,
            out var scout,
            out var soldier,
            out var heavy);
        scout.ForceSetHealth(scout.MaxHealth);
        soldier.ForceSetHealth(soldier.MaxHealth);
        heavy.ForceSetHealth(heavy.MaxHealth);
        scout.TriggerChatBubble(ChatBubbleFrameCatalog.Medic);

        var selection = CombatDecisionResolver.FindBestMedicHealTargetSelection(
            world,
            medic,
            PlayerTeam.Red,
            new Dictionary<byte, PlayerTeam> { [SimulationWorld.LocalPlayerSlot] = PlayerTeam.Red });

        Assert.Same(scout, selection.Target);
        Assert.Equal(MedicHealTargetSelectionKind.HumanMedicCall, selection.Kind);
    }

    [Fact]
    public void MedicHealTargetDoesNotTreatControlledBotMedicBubbleAsHumanCall()
    {
        var world = CreateMedicTargetWorld(
            out var medic,
            out var scout,
            out var soldier,
            out var heavy);
        scout.ForceSetHealth(scout.MaxHealth);
        soldier.ForceSetHealth(soldier.MaxHealth);
        heavy.ForceSetHealth(heavy.MaxHealth);
        scout.TriggerChatBubble(ChatBubbleFrameCatalog.Medic);

        var selection = CombatDecisionResolver.FindBestMedicHealTargetSelection(
            world,
            medic,
            PlayerTeam.Red,
            new Dictionary<byte, PlayerTeam>
            {
                [SimulationWorld.LocalPlayerSlot] = PlayerTeam.Red,
                [2] = PlayerTeam.Red,
            });

        Assert.Same(heavy, selection.Target);
        Assert.Equal(MedicHealTargetSelectionKind.Pocket, selection.Kind);
    }

    private static SimulationWorld CreateMedicTargetWorld(
        out PlayerEntity medic,
        out PlayerEntity scout,
        out PlayerEntity soldier,
        out PlayerEntity heavy)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Medic));
        SetCombatLevel(world);
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        medic = world.LocalPlayer;
        medic.TeleportTo(100f, 100f);

        scout = AddNetworkPlayer(world, 2, PlayerClass.Scout, medic.X + 30f, medic.Y);
        soldier = AddNetworkPlayer(world, 3, PlayerClass.Soldier, medic.X + 70f, medic.Y);
        heavy = AddNetworkPlayer(world, 4, PlayerClass.Heavy, medic.X + 130f, medic.Y);
        return world;
    }

    private static void SetCombatLevel(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "botbrain_medic_target_test",
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

    private static PlayerEntity AddNetworkPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        float x,
        float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        player.TeleportTo(x, y);
        return player;
    }
}
