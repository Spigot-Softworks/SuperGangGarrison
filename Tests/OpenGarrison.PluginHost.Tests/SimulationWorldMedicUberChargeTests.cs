using OpenGarrison.Core;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SimulationWorldMedicUberChargeTests
{
    private static readonly MethodInfo CombatTestSetLevelMethod = GetRequiredSimulationWorldMethod("CombatTestSetLevel");
    private static readonly MethodInfo UpdateMedicHealingMethod = GetRequiredSimulationWorldMethod("UpdateMedicHealing");

    [Theory]
    [InlineData(false, 1.75f)]
    [InlineData(true, 2.5f)]
    public void MedigunUberChargeUsesModernTargetHealthRates(bool damagedTarget, float expectedCharge)
    {
        var world = CreateMedicHealWorld(out var medic, out var target);
        target.ForceSetHealth(damagedTarget ? target.MaxHealth - 20 : target.MaxHealth);

        InvokeUpdateMedicHealing(world, medic, target);

        Assert.Equal(expectedCharge, medic.MedicUberCharge);
        Assert.True(medic.IsMedicHealing);
        Assert.Equal(target.Id, medic.MedicHealTargetId);
    }

    private static SimulationWorld CreateMedicHealWorld(out PlayerEntity medic, out PlayerEntity target)
    {
        var world = new SimulationWorld();
        SetOpenCombatLevel(world);
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Medic);
        medic = world.LocalPlayer;
        medic.TeleportTo(100f, 100f);

        target = AddNetworkPlayer(world, 2, PlayerClass.Scout, PlayerTeam.Red, 180f, 100f);
        return world;
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

    private static void SetOpenCombatLevel(SimulationWorld world)
    {
        CombatTestSetLevelMethod.Invoke(
            world,
            [
                new SimpleLevel(
                    name: "medigun_uber_charge_test",
                    mode: GameModeKind.CaptureTheFlag,
                    bounds: new WorldBounds(1024f, 768f),
                    mapScale: 1f,
                    backgroundAssetName: null,
                    mapAreaIndex: 1,
                    mapAreaCount: 1,
                    localSpawn: new SpawnPoint(100f, 100f),
                    redSpawns: [new SpawnPoint(100f, 100f)],
                    blueSpawns: [new SpawnPoint(900f, 100f)],
                    intelBases:
                    [
                        new IntelBaseMarker(PlayerTeam.Red, 100f, 100f),
                        new IntelBaseMarker(PlayerTeam.Blue, 900f, 100f),
                    ],
                    roomObjects: [],
                    floorY: 768f,
                    solids: [],
                    importedFromSource: false),
            ]);
    }

    private static void InvokeUpdateMedicHealing(SimulationWorld world, PlayerEntity medic, PlayerEntity target)
    {
        UpdateMedicHealingMethod.Invoke(world, [medic, target.X, target.Y]);
    }

    private static MethodInfo GetRequiredSimulationWorldMethod(string name)
    {
        return typeof(SimulationWorld).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find SimulationWorld.{name}.");
    }
}
