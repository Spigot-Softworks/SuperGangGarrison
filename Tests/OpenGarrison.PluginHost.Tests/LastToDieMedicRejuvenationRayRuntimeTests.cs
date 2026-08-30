using System.Reflection;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieMedicRejuvenationRayRuntimeTests
{
    [Fact]
    public void RejuvenationRayAggregatesAndReplacesOnlyRegularUberInvulnerability()
    {
        var modifiers = LastToDieDerivedModifiers.FromPerks(
            [LastToDiePerkIds.Medic.RejuvenationRay]);
        Assert.True(modifiers.MedicRejuvenationRayEnabled);

        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red);
        medic.SetMedicHealingTarget(target);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.RejuvenationRay]));
        StartUber(medic);

        InvokeAdvanceMedicUberEffects(world);

        Assert.Equal(MedicUberDeliveryMode.RejuvenationRay, medic.MedicUberDeliveryMode);
        Assert.True(medic.IsMedicRegularUberDeliveryActive);
        Assert.True(medic.IsMedicRejuvenationRayDeliveryActive);
        Assert.True(medic.HasInfiniteAmmoFromUber);
        Assert.False(medic.IsUbered);
        Assert.False(target.IsUbered);
        var medicHealth = medic.Health;
        var targetHealth = target.Health;
        Assert.False(medic.ApplyDamage(10));
        Assert.False(target.ApplyDamage(10));
        Assert.Equal(medicHealth - 10, medic.Health);
        Assert.Equal(targetHealth - 10, target.Health);
    }

    [Fact]
    public void RejuvenationRayComposesWithTraumaAndHomeostasisUsingActualHealing()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var target = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [
                LastToDiePerkIds.Medic.RejuvenationRay,
                LastToDiePerkIds.Medic.TraumaSurgeon,
                LastToDiePerkIds.Medic.Homeostasis,
            ]));
        medic.ForceSetHealth(medic.MaxHealth - 50);
        StartUber(medic);

        var medicHealthBefore = medic.Health;
        for (var tick = 0; tick < 10; tick += 1)
        {
            target.ForceSetHealth(target.MaxHealth / 10);
            var targetHealthBefore = target.Health;
            InvokeApplyMedicHealing(world, medic, target);
            Assert.Equal(6, target.Health - targetHealthBefore);
        }

        Assert.Equal(21, medic.Health - medicHealthBefore);
    }

    [Fact]
    public void RejuvenationRayFollowsBeamBreakAndTargetSwap()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        var first = AddPlayer(world, 2, PlayerClass.Heavy, PlayerTeam.Red);
        var second = AddPlayer(world, 3, PlayerClass.Heavy, PlayerTeam.Red);
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.RejuvenationRay]));
        StartUber(medic);
        first.ForceSetHealth(first.MaxHealth - 20);
        second.ForceSetHealth(second.MaxHealth - 20);

        InvokeApplyMedicHealing(world, medic, first);
        Assert.Equal(first.MaxHealth - 18, first.Health);
        medic.ClearMedicHealingTarget();
        InvokeAdvanceMedicUberEffects(world);
        Assert.Equal(first.MaxHealth - 18, first.Health);
        Assert.Equal(second.MaxHealth - 20, second.Health);

        InvokeApplyMedicHealing(world, medic, second);
        Assert.Equal(first.MaxHealth - 18, first.Health);
        Assert.Equal(second.MaxHealth - 18, second.Health);
        Assert.Equal(second.Id, medic.MedicHealTargetId);
    }

    [Fact]
    public void RejuvenationRayKeepsInfiniteAmmoAndKritzIsExcluded()
    {
        var world = CreateMedicWorld();
        var medic = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.RejuvenationRay]));
        StartUber(medic);
        SetProperty(medic, nameof(PlayerEntity.CurrentShells), 0);

        Assert.True(medic.TryFireMedicNeedle());
        Assert.Equal(0, medic.CurrentShells);

        var kritzWorld = CreateMedicWorld();
        var kritzMedic = kritzWorld.LocalPlayer;
        Assert.True(kritzWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.RejuvenationRay]));
        Assert.True(kritzMedic.TrySelectGameplayPrimaryItem("weapon.medigun.crit"));
        StartUber(kritzMedic);
        Assert.Equal(MedicUberDeliveryMode.Kritz, kritzMedic.MedicUberDeliveryMode);
        Assert.False(kritzMedic.IsMedicRejuvenationRayDeliveryActive);
    }

    [Fact]
    public void RejuvenationRayCaptureRequiresFieldCommanderAndIntelActivationRuleIsUnchanged()
    {
        var withoutFieldCommander = CreateMedicWorld();
        Assert.True(withoutFieldCommander.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.RejuvenationRay]));
        StartUber(withoutFieldCommander.LocalPlayer);
        Assert.False(withoutFieldCommander.CanPlayerCaptureControlPointsWhileUbered(
            withoutFieldCommander.LocalPlayer));
        Assert.False(withoutFieldCommander.CanPlayerContributeToControlPoint(
            withoutFieldCommander.LocalPlayer));

        var withFieldCommander = CreateMedicWorld();
        Assert.True(withFieldCommander.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.RejuvenationRay, LastToDiePerkIds.Medic.FieldCommander]));
        StartUber(withFieldCommander.LocalPlayer);
        Assert.True(withFieldCommander.CanPlayerCaptureControlPointsWhileUbered(
            withFieldCommander.LocalPlayer));
        Assert.True(withFieldCommander.CanPlayerContributeToControlPoint(
            withFieldCommander.LocalPlayer));

        var intelCarrierWorld = CreateMedicWorld();
        Assert.True(intelCarrierWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.RejuvenationRay]));
        intelCarrierWorld.LocalPlayer.FillMedicUberCharge();
        SetProperty(intelCarrierWorld.LocalPlayer, nameof(PlayerEntity.IsCarryingIntel), true);
        Assert.False(intelCarrierWorld.LocalPlayer.TryStartMedicUber());
    }

    [Fact]
    public void RejuvenationRayRuntimeSurvivesPredictionLegacyAndProtocol64ResyncState()
    {
        var source = CreateMedicWorld();
        var medic = source.LocalPlayer;
        var target = AddPlayer(source, 2, PlayerClass.Heavy, PlayerTeam.Red);
        Assert.True(source.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Medic.RejuvenationRay]));
        medic.SetMedicHealingTarget(target);
        StartUber(medic);

        var predictionShadow = new PlayerEntity(90, CharacterClassCatalog.Medic, "prediction");
        predictionShadow.RestorePredictionState(medic.CapturePredictionState());
        Assert.True(predictionShadow.IsMedicRejuvenationRayDeliveryActive);
        Assert.Equal(target.Id, predictionShadow.MedicHealTargetId);
        Assert.Equal(medic.MedicUberCharge, predictionShadow.MedicUberCharge);

        var legacyPlayer = ServerHelpers.ToSnapshotPlayerState(
            source,
            SimulationWorld.LocalPlayerSlot,
            medic,
            medic,
            new SnapshotStringCache());
        var legacySnapshot = CreateSnapshot(legacyPlayer);
        var payload = ProtocolCodec.Serialize(legacySnapshot, ProtocolCompressionSettings.Disabled);
        Assert.True(ProtocolCodec.TryDeserialize(payload, out var decodedMessage));
        var decodedPlayer = Assert.Single(Assert.IsType<SnapshotMessage>(decodedMessage).Players);
        Assert.Equal(medic.MedicUberDeliveryState, decodedPlayer.MedicUberDeliveryState);
        Assert.Equal(target.Id, decodedPlayer.MedicHealTargetId);
        Assert.Equal(medic.MedicUberCharge, decodedPlayer.MedicUberCharge);

        var protocolState = Assert.Single(
            new Protocol64StatePublisher(source).BuildPlayerStateBatch(10).Players,
            player => player.Slot == SimulationWorld.LocalPlayerSlot);
        Assert.Equal(medic.MedicUberDeliveryState, protocolState.MedicUberDeliveryState);
        Assert.Equal(target.Id, protocolState.MedicHealTargetId);
        Assert.Equal(medic.MedicUberCharge, protocolState.MedicUberCharge);

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        Assert.True(receiver.ApplyProtocol64PlayerState(protocolState));
        Assert.True(receiver.LocalPlayer.IsMedicRejuvenationRayDeliveryActive);
        Assert.Equal(target.Id, receiver.LocalPlayer.MedicHealTargetId);
        Assert.Equal(medic.MedicUberCharge, receiver.LocalPlayer.MedicUberCharge);
    }

    private static SimulationWorld CreateMedicWorld()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Medic);
        return world;
    }

    private static PlayerEntity AddPlayer(
        SimulationWorld world,
        byte slot,
        PlayerClass playerClass,
        PlayerTeam team)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, team));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, playerClass));
        Assert.True(world.TryGetNetworkPlayer(slot, out var player));
        return player;
    }

    private static void StartUber(PlayerEntity medic)
    {
        medic.FillMedicUberCharge();
        Assert.True(medic.TryStartMedicUber());
    }

    private static void InvokeApplyMedicHealing(
        SimulationWorld world,
        PlayerEntity medic,
        PlayerEntity target)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyMedicHealing",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [medic, target]);
    }

    private static void InvokeAdvanceMedicUberEffects(SimulationWorld world)
    {
        var method = typeof(SimulationWorld).GetMethod(
            "AdvanceMedicUberEffects",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, null);
    }

    private static void SetProperty(PlayerEntity player, string propertyName, object value)
    {
        var property = typeof(PlayerEntity).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(player, value);
    }

    private static SnapshotMessage CreateSnapshot(SnapshotPlayerState player)
    {
        return new SnapshotMessage(
            Frame: 10,
            TickRate: 30,
            LevelName: "ctf_truefort",
            MapAreaIndex: 1,
            MapAreaCount: 1,
            GameMode: 1,
            MatchPhase: 1,
            WinnerTeam: 0,
            TimeRemainingTicks: 300,
            RedCaps: 0,
            BlueCaps: 0,
            SpectatorCount: 0,
            LastProcessedInputSequence: 0,
            RedIntel: new SnapshotIntelState(1, 0f, 0f, true, false, 0),
            BlueIntel: new SnapshotIntelState(2, 0f, 0f, true, false, 0),
            Players: [player],
            CombatTraces: [],
            SniperAimIndicators: [],
            Sentries: [],
            Shots: [],
            Bubbles: [],
            Blades: [],
            Needles: [],
            RevolverShots: [],
            Rockets: [],
            Flames: [],
            Flares: [],
            Mines: [],
            DeadBodies: [],
            ControlPointSetupTicksRemaining: 0,
            KothUnlockTicksRemaining: 0,
            KothRedTimerTicksRemaining: 0,
            KothBlueTimerTicksRemaining: 0,
            ControlPoints: [],
            Generators: [],
            LocalDeathCam: null,
            KillFeed: [],
            VisualEvents: [],
            DamageEvents: [],
            SoundEvents: []);
    }
}
