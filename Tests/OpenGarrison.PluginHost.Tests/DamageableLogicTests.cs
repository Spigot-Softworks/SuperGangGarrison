using System.Reflection;

using OpenGarrison.Core;

using Xunit;



namespace OpenGarrison.PluginHost.Tests;



public sealed class DamageableLogicTests

{

    [Fact]

    public void DamageTriggerFiresOnceWhenHealthCrossesThresholdDownward()

    {

        var graph = BuildThresholdTrigger();

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));



        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f));

        Assert.True(graph.GetOutput(nodeIndex));



        graph.EvaluateCombinatorial([]);

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.3f));

        Assert.True(graph.GetOutput(nodeIndex));

    }



    [Fact]

    public void DamageTriggerRearmsAfterHealthRisesAboveThreshold()

    {

        var graph = BuildThresholdTrigger();

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f));

        Assert.True(graph.GetOutput(nodeIndex));



        graph.EvaluateCombinatorial([]);

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.8f));

        Assert.False(graph.GetOutput(nodeIndex));



        graph.EvaluateCombinatorial([]);

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f));

        Assert.True(graph.GetOutput(nodeIndex));

    }



    [Fact]

    public void DamageTriggerDoesNotUseThresholdWhenBelowThresholdModeIsOff()

    {

        var graph = MapLogicGraphBuilder.Build(

        [

            new MapLogicNodeDefinition

            {

                LogicKey = "dmgTrigger",

                Kind = MapLogicNodeKind.DamageTrigger,

                DamageableRoomObjectIndex = 0,

                TriggerBelowPercent = 50,

                TriggerBelowThreshold = false,

            },

        ]);



        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f));

        Assert.False(graph.GetOutput(nodeIndex));

    }



    [Fact]

    public void DamageTriggerOnAnyDamageFiresWhenHealthDecreases()

    {

        var graph = MapLogicGraphBuilder.Build(

        [

            new MapLogicNodeDefinition

            {

                LogicKey = "dmgTrigger",

                Kind = MapLogicNodeKind.DamageTrigger,

                DamageableRoomObjectIndex = 0,

                TriggerOnAnyDamage = true,

            },

        ]);



        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.8f));

        Assert.True(graph.GetOutput(nodeIndex));



        graph.EvaluateCombinatorial([]);

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.7f));

        Assert.True(graph.GetOutput(nodeIndex));

    }



    [Fact]

    public void DamageTriggerOnAnyDamagePulseExpiresAfterTrueTime()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
                TrueTimeSeconds = 0.25f,
            },
        ]);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.8f));
        Assert.True(graph.GetOutput(nodeIndex));

        graph.AdvanceDamageTriggers(0.1f);
        Assert.True(graph.GetOutput(nodeIndex));

        graph.AdvanceDamageTriggers(0.2f);
        Assert.False(graph.GetOutput(nodeIndex));
    }

    [Fact]
    public void DamageTriggerOnAnyDamageCooldownSuppressesImmediateRetrigger()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
                SignalMode = MapLogicSignalMode.Impulse,
                AnyDamageCooldownSeconds = 0.25f,
            },
        ]);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.9f));
        Assert.True(graph.GetOutput(nodeIndex));

        graph.EvaluateCombinatorial([]);
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.8f));
        Assert.False(graph.GetOutput(nodeIndex));
    }

    [Fact]
    public void DamageTriggerOnAnyDamageCooldownZeroAllowsImmediateRetrigger()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
                SignalMode = MapLogicSignalMode.Impulse,
                AnyDamageCooldownSeconds = 0f,
            },
        ]);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.9f));
        Assert.True(graph.GetOutput(nodeIndex));

        graph.EvaluateCombinatorial([]);
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.8f));
        Assert.True(graph.GetOutput(nodeIndex));
    }

    [Fact]
    public void DamageTriggerOnAnyDamageCooldownExpiresAfterAdvance()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
                SignalMode = MapLogicSignalMode.Impulse,
                AnyDamageCooldownSeconds = 0.25f,
            },
        ]);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.9f));
        Assert.True(graph.GetOutput(nodeIndex));

        graph.AdvanceDamageTriggers(0.3f);
        graph.EvaluateCombinatorial([]);
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.8f));
        Assert.True(graph.GetOutput(nodeIndex));
    }

    [Fact]
    public void ParseAnyDamageCooldownDefaultsToZeroWhenPropertyMissing()
    {
        Assert.Equal(0f, DamageTriggerMetadata.ParseAnyDamageCooldownSeconds(null));
        Assert.Equal(0f, DamageTriggerMetadata.ParseAnyDamageCooldownSeconds(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        Assert.Equal(0.25f, DamageTriggerMetadata.ParseAnyDamageCooldownSeconds(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DamageTriggerMetadata.AnyDamageCooldownPropertyKey] = "0.25",
            }));
        Assert.Equal(0f, DamageTriggerMetadata.ParseAnyDamageCooldownSecondsValue("0"));
    }



    [Fact]

    public void DamageTriggerWhenDestroyedHoldsUntilHealed()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerWhenDestroyed = true,
            },
        ]);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0f));
        Assert.True(graph.GetOutput(nodeIndex));

        graph.EvaluateCombinatorial([]);
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0f));
        Assert.True(graph.GetOutput(nodeIndex));

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 1f));
        Assert.False(graph.GetOutput(nodeIndex));
    }



    [Fact]

    public void DamageTriggerOnAnyDamageDoesNotFireWhenHealthIncreases()

    {

        var graph = MapLogicGraphBuilder.Build(

        [

            new MapLogicNodeDefinition

            {

                LogicKey = "dmgTrigger",

                Kind = MapLogicNodeKind.DamageTrigger,

                DamageableRoomObjectIndex = 0,

                TriggerOnAnyDamage = true,

            },

        ]);



        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 0.5f));

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 1f));

        Assert.False(graph.GetOutput(nodeIndex));

    }



    [Fact]

    public void DamageTriggerOnHealDoesNotFireAtMapStart()

    {

        var graph = MapLogicGraphBuilder.Build(

        [

            new MapLogicNodeDefinition

            {

                LogicKey = "healTrigger",

                Kind = MapLogicNodeKind.DamageTrigger,

                DamageableRoomObjectIndex = 0,

                TriggerOnHeal = true,

            },

        ]);



        var nodeIndex = graph.NodeIndexByKey["healTrigger"];

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 1f));

        Assert.False(graph.GetOutput(nodeIndex));

    }



    [Fact]

    public void DamageTriggerOnHealFiresWhenHealthReturnsToMax()

    {

        var graph = MapLogicGraphBuilder.Build(

        [

            new MapLogicNodeDefinition

            {

                LogicKey = "healTrigger",

                Kind = MapLogicNodeKind.DamageTrigger,

                DamageableRoomObjectIndex = 0,

                TriggerOnHeal = true,

                SignalMode = MapLogicSignalMode.Impulse,

            },

        ]);



        var nodeIndex = graph.NodeIndexByKey["healTrigger"];

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 0.5f));

        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 1f));

        Assert.True(graph.GetOutput(nodeIndex));

    }



    [Fact]

    public void DamageableZoneStopsBlockingProjectilesWhenDestroyedAndReEnablesOnHeal()

    {

        var zone = CreateDamageableZone(0, 100f, disableWhenDestroyed: true);

        Assert.True(DamageableMetadata.BlocksProjectiles(zone.DamageableZone, 50f));

        Assert.False(DamageableMetadata.BlocksProjectiles(zone.DamageableZone, 0f));

        Assert.True(DamageableMetadata.BlocksProjectiles(zone.DamageableZone, 100f));

    }



    [Fact]

    public void DamageableZoneHealWhenSignalRestoresBlockingAndDamageIntake()

    {

        var graph = MapLogicGraphBuilder.Build(

        [

            new MapLogicNodeDefinition

            {

                LogicKey = "healSignal",

                Kind = MapLogicNodeKind.Not,

                InputRef = "node:never",

            },

        ]);

        var healWhenNodeIndex = graph.NodeIndexByKey["healSignal"];

        var zone = CreateDamageableZone(

            0,

            100f,

            healWhenNodeIndex: healWhenNodeIndex,

            blockPlayers: true,

            disableWhenDestroyed: true);

        var world = CreateWorld([zone], graph);



        world.Level.LogicGraph.EvaluateCombinatorial([], null);

        Assert.True(world.Level.LogicGraph.GetOutput(healWhenNodeIndex));

        Assert.True(world.BlocksProjectileDamageableZone(0));



        Assert.True(world.TryApplyDamageableZoneDamage(0, 100f));

        Assert.Equal(0f, world.GetDamageableZoneHealth(0));

        Assert.False(world.BlocksProjectileDamageableZone(0));

        Assert.False(world.TryApplyDamageableZoneDamage(0, 10f));

        Assert.False(DamageableMetadata.BlocksPlayers(

            zone.DamageableZone,

            world.Level.GetDamageableZoneCurrentHealth(0, zone)));



        world.TickMapLogicTimers();



        Assert.Equal(100f, world.GetDamageableZoneHealth(0));

        Assert.True(world.BlocksProjectileDamageableZone(0));

        Assert.True(DamageableMetadata.BlocksPlayers(

            zone.DamageableZone,

            world.Level.GetDamageableZoneCurrentHealth(0, zone)));

        Assert.True(world.TryApplyDamageableZoneDamage(0, 25f));

        Assert.Equal(75f, world.GetDamageableZoneHealth(0));

    }



    [Fact]
    public void DamageableImporterUsesCenterPlacementAnchor()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var context = new CustomMapEntityImportContext
        {
            RedSpawns = [],
            BlueSpawns = [],
            RoomObjects = roomObjects,
            UseCenterOrigin = false,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            DamageableMetadata.DamageableEntityType,
            100f,
            120f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [DamageableMetadata.HealthPropertyKey] = "100",
            },
            context));

        var marker = Assert.Single(roomObjects);
        Assert.Equal(RoomObjectType.DamageableZone, marker.Type);
        Assert.Equal(79f, marker.X, precision: 3);
        Assert.Equal(99f, marker.Y, precision: 3);
        Assert.Equal(42f, marker.Width, precision: 3);
        Assert.Equal(42f, marker.Height, precision: 3);
    }

    [Fact]
    public void DestroyedDamageableZoneStopsBlockingProjectiles()
    {
        var zone = CreateDamageableZone(0, 100f, disableWhenDestroyed: true);
        var world = CreateWorld([zone]);
        Assert.True(world.BlocksProjectileDamageableZone(0));
        Assert.True(world.TryApplyDamageableZoneDamage(0, 100f));
        Assert.False(world.BlocksProjectileDamageableZone(0));
        Assert.False(world.TryApplyDamageableZoneDamage(0, 10f));
    }

    [Fact]
    public void ExplosiveDamageReducesDamageableZoneHealthInBlastRadius()
    {
        var zone = CreateDamageableZone(0, 100f);
        var world = CreateWorld([zone]);
        world.ApplyExplosiveDamageToDamageableZones(21f, 21f, 65f, 30f, splashThresholdFactor: 0.25f);
        Assert.True(world.GetDamageableZoneHealth(0) < 100f);
        Assert.True(world.GetDamageableZoneHealth(0) > 0f);
    }

    [Fact]
    public void DamageTriggerThresholdOnlyFiresForMatchingTeam()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerBelowThreshold = true,
                TriggerBelowPercent = 50,
                SignalMode = MapLogicSignalMode.Impulse,
                DamageTriggerTeamFilter = PlayerTriggerTeamFilter.Red,
            },
        ]);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f, _ => PlayerTeam.Blue));
        Assert.False(graph.GetOutput(nodeIndex));

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.4f, _ => PlayerTeam.Red));
        Assert.True(graph.GetOutput(nodeIndex));
    }

    [Fact]
    public void DamageTriggerOnAnyDamageOnlyFiresForMatchingTeam()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
                SignalMode = MapLogicSignalMode.Impulse,
                DamageTriggerTeamFilter = PlayerTriggerTeamFilter.Blue,
            },
        ]);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.8f, _ => PlayerTeam.Red));
        Assert.False(graph.GetOutput(nodeIndex));

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0.8f, _ => PlayerTeam.Blue));
        Assert.True(graph.GetOutput(nodeIndex));
    }

    [Fact]
    public void DamageTriggerWhenDestroyedOnlyFiresForMatchingTeam()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerWhenDestroyed = true,
                SignalMode = MapLogicSignalMode.Impulse,
                DamageTriggerTeamFilter = PlayerTriggerTeamFilter.Red,
            },
        ]);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0f, _ => PlayerTeam.Blue));
        Assert.False(graph.GetOutput(nodeIndex));

        graph.ResetDamageTriggerStates(new DamageTriggerEvaluationContext(_ => 1f));
        graph.EvaluateDamageTriggers(new DamageTriggerEvaluationContext(_ => 0f, _ => PlayerTeam.Red));
        Assert.True(graph.GetOutput(nodeIndex));
    }

    [Fact]
    public void TryApplyDamageableZoneDamageRecordsDamagingTeamForTriggerEvaluation()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
                SignalMode = MapLogicSignalMode.Impulse,
                DamageTriggerTeamFilter = PlayerTriggerTeamFilter.Red,
            },
        ]);
        var world = CreateWorld([CreateDamageableZone(0, 100f)], graph);
        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];

        Assert.True(world.TryApplyDamageableZoneDamage(0, 10f, PlayerTeam.Blue));
        Assert.False(world.Level.LogicGraph.GetOutput(nodeIndex));

        Assert.True(world.TryApplyDamageableZoneDamage(0, 10f, PlayerTeam.Red));
        Assert.True(world.Level.LogicGraph.GetOutput(nodeIndex));
    }

    [Fact]
    public void SentryStructuralDamageUsesSentryTeamForDamageTriggers()
    {
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
                SignalMode = MapLogicSignalMode.Impulse,
                DamageTriggerTeamFilter = PlayerTriggerTeamFilter.Blue,
            },
        ]);
        var world = CreateWorld([CreateDamageableZone(0, 100f)], graph);
        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);

        var blueSentry = new SentryEntity(
            id: 9001,
            ownerPlayerId: world.LocalPlayer.Id,
            team: PlayerTeam.Blue,
            x: 10f,
            y: 10f,
            startDirectionX: 1f);
        blueSentry.ForceBuilt();
        var addSentry = typeof(SimulationWorld).GetMethod(
            "CombatTestAddSentry",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(addSentry);
        addSentry.Invoke(world, [blueSentry]);

        InvokeApplyExperimentalSentryStructuralTargetDamage(
            world,
            blueSentry,
            damageableZoneRoomObjectIndex: 0,
            targetX: 50f,
            targetY: 50f,
            owner: world.LocalPlayer,
            damage: SentryEntity.HitDamage);

        var nodeIndex = graph.NodeIndexByKey["dmgTrigger"];
        Assert.True(world.Level.LogicGraph.GetOutput(nodeIndex));
    }

    private static void InvokeApplyExperimentalSentryStructuralTargetDamage(
        SimulationWorld world,
        SentryEntity sentry,
        int damageableZoneRoomObjectIndex,
        float targetX,
        float targetY,
        PlayerEntity owner,
        float damage)
    {
        var sentryTargetType = typeof(SimulationWorld).GetNestedType("SentryTarget", BindingFlags.NonPublic);
        Assert.NotNull(sentryTargetType);
        var target = Activator.CreateInstance(
            sentryTargetType,
            null,
            null,
            null,
            null,
            damageableZoneRoomObjectIndex,
            targetX,
            targetY,
            null);
        var method = typeof(SimulationWorld).GetMethod(
            "ApplyExperimentalSentryStructuralTargetDamage",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(world, [sentry, target, owner, damage]);
    }

    private static MapLogicGraph BuildThresholdTrigger()

    {

        return MapLogicGraphBuilder.Build(

        [

            new MapLogicNodeDefinition

            {

                LogicKey = "dmgTrigger",

                Kind = MapLogicNodeKind.DamageTrigger,

                DamageableRoomObjectIndex = 0,

                TriggerBelowPercent = 50,

                TriggerBelowThreshold = true,

            },

        ]);

    }



    private static RoomObjectMarker CreateDamageableZone(

        int index,

        float maxHealth,

        int healWhenNodeIndex = -1,

        bool blockPlayers = false,

        bool disableWhenDestroyed = true,

        bool stabbable = false)

    {

        return new RoomObjectMarker(

            RoomObjectType.DamageableZone,

            0f,

            0f,

            42f,

            42f,

            string.Empty,

            SourceName: $"damageable-{index}",

            DamageableZone: new DamageableZoneConfiguration(

                maxHealth,

                healWhenNodeIndex,

                ShowHealthBar: false,

                blockPlayers,

                disableWhenDestroyed,

                SentryTarget: true,

                Stabbable: stabbable));

    }



    [Fact]
    public void ParseSentryTargetDefaultsToEnabled()
    {
        Assert.True(DamageableMetadata.ParseSentryTarget(null));
        Assert.True(DamageableMetadata.ParseSentryTarget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        Assert.False(DamageableMetadata.ParseSentryTarget(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DamageableMetadata.SentryTargetPropertyKey] = "false",
            }));
    }

    [Fact]
    public void IsSentryTargetRequiresEnabledFlagAndRemainingHealth()
    {
        var enabled = new DamageableZoneConfiguration(100f, -1, false, false, true, SentryTarget: true, Stabbable: false);
        var disabled = enabled with { SentryTarget = false };

        Assert.True(DamageableMetadata.IsSentryTarget(enabled, 100f));
        Assert.False(DamageableMetadata.IsSentryTarget(enabled, 0f));
        Assert.False(DamageableMetadata.IsSentryTarget(disabled, 100f));
    }

    [Fact]
    public void ParseStabbableDefaultsToDisabled()
    {
        Assert.False(DamageableMetadata.ParseStabbable(null));
        Assert.False(DamageableMetadata.ParseStabbable(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        Assert.True(DamageableMetadata.ParseStabbable(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DamageableMetadata.StabbablePropertyKey] = "true",
            }));
    }

    [Fact]
    public void IsStabbableTargetRequiresEnabledFlagAndRemainingHealth()
    {
        var enabled = new DamageableZoneConfiguration(100f, -1, false, false, true, SentryTarget: true, Stabbable: true);
        var disabled = enabled with { Stabbable = false };

        Assert.True(DamageableMetadata.IsStabbableTarget(enabled, 100f));
        Assert.False(DamageableMetadata.IsStabbableTarget(enabled, 0f));
        Assert.False(DamageableMetadata.IsStabbableTarget(disabled, 100f));
    }

    [Fact]
    public void DamageTriggerOnAnyDamageDisablesLinkedSpriteThroughActivator()
    {
        var zone = CreateDamageableZone(0, 100f);
        var sprite = new RoomObjectMarker(
            RoomObjectType.CustomMapSprite,
            10f,
            10f,
            20f,
            20f,
            string.Empty,
            SourceName: CustomMapCustomSpriteMetadata.CustomSpriteEntityType,
            CustomMapSprite: new CustomMapSpriteConfiguration("icon", CustomMapSpriteLayerKind.Bg, 0, 1f));
        var graph = MapLogicGraphBuilder.Build(
        [
            new MapLogicNodeDefinition
            {
                LogicKey = "dmgTrigger",
                Kind = MapLogicNodeKind.DamageTrigger,
                DamageableRoomObjectIndex = 0,
                TriggerOnAnyDamage = true,
            },
        ]);
        var activators = new MapLogicActivatorSet(
        [
            new MapLogicActivator(
                graph.NodeIndexByKey["dmgTrigger"],
                targetRoomObjectIndex: 1,
                MapLogicActivatorBehavior.Disable,
                activateOnStart: false),
        ]);
        var world = CreateWorld([zone, sprite], graph, activators);

        world.EvaluateMapLogicGraph();
        Assert.True(world.Level.IsRoomObjectActive(1));

        Assert.True(world.TryApplyDamageableZoneDamage(0, 25f));
        var triggerIndex = graph.NodeIndexByKey["dmgTrigger"];
        Assert.True(world.Level.LogicGraph.GetOutput(triggerIndex));
        Assert.False(world.Level.IsRoomObjectActive(1));

        world.Level.LogicGraph.EvaluateCombinatorial([]);
        world.TickMapLogicTimers();
        Assert.False(world.Level.IsRoomObjectActive(1));
    }

    private static SimulationWorld CreateWorld(

        IReadOnlyList<RoomObjectMarker> roomObjects,

        MapLogicGraph? logicGraph = null,

        MapLogicActivatorSet? logicActivators = null)

    {

        var world = new SimulationWorld();

        var setLevel = typeof(SimulationWorld).GetMethod(

            "CombatTestSetLevel",

            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(setLevel);

        setLevel.Invoke(

            world,

            [

                new SimpleLevel(

                    "damageable-heal-test",

                    GameModeKind.TeamDeathmatch,

                    new WorldBounds(512f, 512f),

                    1f,

                    null,

                    0,

                    1,

                    new SpawnPoint(0f, 0f),

                    [],

                    [],

                    [],

                    roomObjects,

                    0f,

                    [],

                    importedFromSource: false,

                    logicGraph: logicGraph,

                    logicActivators: logicActivators),

            ]);

        return world;

    }

}


