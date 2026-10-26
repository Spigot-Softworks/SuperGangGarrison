using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using OpenGarrison.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuilderPersistenceRegressionTests : IDisposable
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapScriptNamesAndGroupsSurviveDraftAndExport(bool package)
    {
        var source = "-- quotes: \"Hello\"; braces: { }\nfunction on_start()\n  print('map loaded')\nend";
        var named = CustomMapBuilderEntity.Create("medCabinet", 40, 50,
            new Dictionary<string, string> { [MapObjectMetadata.NameKey] = "red_cabinet", [MapObjectMetadata.GroupsKey] = "red_spawn, cabinets" });
        var document = _document with { Entities = [.. _document.Entities, named],
            Metadata = new Dictionary<string, string>(_document.Metadata) { [MapScriptMetadata.SourceKey] = source } };
        var draft = Path.Combine(_root, "script.ogmap");
        BuilderProjectStore.Save(document, draft);
        var loaded = BuilderProjectStore.Load(draft, Path.Combine(_root, "script-cache"));
        Assert.Equal(source, MapScriptMetadata.Read(loaded.Metadata));
        var path = Path.Combine(_root, package ? "scripted/map.json" : "scripted.png");
        if (package) CustomMapPackageExporter.Export(loaded, path); else CustomMapPngExporter.Export(loaded, path);
        var room = (package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!).Room;
        Assert.Equal(source, room.MapScriptSource);
        var marker = Assert.Single(room.RoomObjects.Where(marker => marker.Type == RoomObjectType.HealingCabinet));
        Assert.Equal("red_cabinet", marker.ObjectName);
        Assert.Equal("red_spawn, cabinets", marker.ObjectGroups);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverlappingLegacyCabinetsKeepIndependentStableTargets(bool package)
    {
        var first = CustomMapBuilderEntity.Create("medCabinet", 80, 100,
            new Dictionary<string, string> { ["mapEntityId"] = "first-cabinet" });
        var second = first with { Properties = new Dictionary<string, string> { ["mapEntityId"] = "second-cabinet" } };
        var disable = CustomMapBuilderEntity.Create("logicActivator", 20, 20,
            new Dictionary<string, string> { ["logicKey"] = "disable-cabinet", ["activatorBehavior"] = "disable",
                ["activateOnStart"] = "true", ["activatorEntity"] = MapLogicEntityReference.FormatEntityRef(second) });
        var document = _document with { Entities = [.. _document.Entities, first, second, disable] };
        var path = Path.Combine(_root, package ? "cabinets/map.json" : "cabinets.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var room = (package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!).Room;
        var activator = Assert.Single(room.LogicActivators.Activators);
        Assert.Equal("second-cabinet", room.RoomObjects[activator.TargetRoomObjectIndex].MapEntityId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OverlappingSpritesKeepIndependentMapStartActivatorTargets(bool package, bool reverseOrder)
    {
        using var image = new Image<Rgba32>(8, 8);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var red = CustomMapBuilderEntity.Create("logicCustomSprite", 80, 100,
            new Dictionary<string, string> { ["mapEntityId"] = "red-sprite", ["image"] = "red", ["scale"] = "6" });
        var blue = CustomMapBuilderEntity.Create("logicCustomSprite", 80, 100,
            new Dictionary<string, string> { ["mapEntityId"] = "blue-sprite", ["image"] = "blue", ["scale"] = "6" });
        var enableRed = CustomMapBuilderEntity.Create("logicActivator", 20, 20,
            new Dictionary<string, string> { ["logicKey"] = "enable-red", ["activatorBehavior"] = "enable",
                ["activateOnStart"] = "true", ["activatorEntity"] = MapLogicEntityReference.FormatEntityRef(red) });
        var disableBlue = CustomMapBuilderEntity.Create("logicActivator", 40, 20,
            new Dictionary<string, string> { ["logicKey"] = "disable-blue", ["activatorBehavior"] = "disable",
                ["activateOnStart"] = "true", ["activatorEntity"] = MapLogicEntityReference.FormatEntityRef(blue) });
        var resources = new Dictionary<string, CustomMapBuilderResource>
        {
            ["red"] = new("red", "", CustomMapBuilderResourceKind.CustomSprite, stream.ToArray()),
            ["blue"] = new("blue", "", CustomMapBuilderResourceKind.CustomSprite, stream.ToArray()),
        };
        var document = _document with
        {
            Entities = [.. _document.Entities, .. (reverseOrder ? new[] { blue, red } : new[] { red, blue }), enableRed, disableBlue],
            Resources = resources,
        };
        var path = Path.Combine(_root, package ? "overlap/map.json" : "overlap.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var room = (package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!).Room;
        Assert.Equal(2, room.LogicActivators.Activators.Count);
        Assert.Equal(2, room.LogicActivators.Activators.Select(activator => activator.TargetRoomObjectIndex).Distinct().Count());
        var mask = Enumerable.Repeat(true, room.RoomObjects.Count).ToArray();
        var startApplied = new bool[room.LogicActivators.Activators.Count];
        var state = MapLogicActivatorRuntimeState.CreateForActivatorCount(startApplied.Length);
        for (var tick = 0; tick < 3; tick++)
        {
            MapLogicActivatorRuntime.Apply(room.LogicGraph, room.LogicActivators, mask, startApplied, state, room.RoomObjects);
            for (var index = 0; index < room.RoomObjects.Count; index++)
            {
                if (room.RoomObjects[index].Type != RoomObjectType.CustomMapSprite) continue;
                Assert.Equal(room.RoomObjects[index].CustomMapSprite.ImageResourceName == "red", mask[index]);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundEndAndDestroyWhenConnectionsSurviveExportAndRuntimeImport(bool package)
    {
        var source = CustomMapBuilderEntity.Create("logicGate", 20, 20,
            new Dictionary<string, string> { ["logicKey"] = "condition", ["gateType"] = "or" });
        var completed = CustomMapBuilderEntity.Create("logicGate", 40, 20,
            new Dictionary<string, string> { ["logicKey"] = "completed", ["gateType"] = "or" });
        var roundEnd = CustomMapBuilderEntity.Create(MapRoundEndMetadata.EntityType, 60, 20,
            new Dictionary<string, string> { ["logicKey"] = "roundEnd", ["logicInput"] = "node:condition",
                ["winnerTeam"] = "blue", ["countdownDelay"] = "3", ["countdownSeconds"] = "8", ["onComplete"] = "node:completed" });
        var zone = CustomMapBuilderEntity.Create(DamageableMetadata.DamageableEntityType, 80, 20,
            new Dictionary<string, string> { ["mapEntityId"] = "zone", ["destroyWhen"] = "node:completed" });
        var document = _document with { Entities = [.. _document.Entities, source, completed, roundEnd, zone] };
        var path = Path.Combine(_root, package ? "round-end/map.json" : "round-end.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var reopened = package ? CustomMapPackageImporter.ImportDocument(path)! : CustomMapBuilderPngImporter.Import(path)!;
        Assert.Equal("node:completed", reopened.Entities.Single(entity => entity.Type == MapRoundEndMetadata.EntityType).Properties["onComplete"]);
        var runtime = package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!;
        var graph = runtime.Room.LogicGraph;
        var definition = Assert.Single(graph.RoundEnds);
        Assert.Equal(PlayerTeam.Blue, definition.Winner);
        Assert.Equal(3f, definition.DelaySeconds);
        Assert.Equal(8f, definition.CountdownSeconds);
        Assert.Equal(graph.NodeIndexByKey["completed"], definition.OnCompleteNodeIndex);
        Assert.Equal(graph.NodeIndexByKey["condition"], graph.Nodes[definition.NodeIndex].InputNodeIndex);
        Assert.Equal(graph.NodeIndexByKey["completed"],
            runtime.Room.RoomObjects.Single(marker => marker.Type == RoomObjectType.DamageableZone).DamageableZone.DestroyWhenNodeIndex);
    }

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 6)]
    [InlineData(false, 20)]
    [InlineData(true, 20)]
    public void ConquestPointsBeyondFiveSurvivePngAndPackageRoundTrips(bool package, int pointCount)
    {
        var entities = new List<CustomMapBuilderEntity>
        {
            CustomMapBuilderEntity.Create("redspawn", 0, 0),
            CustomMapBuilderEntity.Create("bluespawn", 300, 0),
        };
        for (var index = 1; index <= pointCount; index++)
        {
            entities.Add(CustomMapBuilderEntity.Create("controlPoint", index * 10, 100,
                new Dictionary<string, string> { ["index"] = index.ToString() }));
            entities.Add(CustomMapBuilderEntity.Create("CapturePoint", index * 10, 100));
        }
        var document = _document with
        {
            Name = "cq_many", Entities = entities,
            Metadata = new Dictionary<string, string> { ["gameMode"] = "cq" },
        };
        var path = Path.Combine(_root, package ? "cq_many/cq_many.json" : "cq_many.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var imported = package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!;
        var points = imported.Room.RoomObjects.Where(marker => marker.Type == RoomObjectType.ControlPoint).ToArray();
        Assert.Equal(GameModeKind.Conquest, SimpleLevelFactory.DetectMode(imported.Room));
        Assert.Equal(pointCount, points.Length);
        Assert.Equal(Enumerable.Range(1, pointCount), points.Select(point =>
        { Assert.True(ControlPointMarkerIndex.TryGetIndex(point, out var index)); return index; }).Order());
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void FireAndPlayerTeamColoursSaveIndependentlyInPngAndPackages(bool package, bool fire, bool players)
    {
        var metadata = new Dictionary<string, string>(_document.Metadata);
        MapLightingMetadata.Write(metadata, MapLightingMetadata.GetPresetDefaults(MapLightingPreset.Night) with
            { FireTeamColor = fire, PlayerTeamColor = players });
        var document = _document with { Metadata = metadata };
        var path = Path.Combine(_root, package ? "team-lights/map.json" : "team-lights.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var runtime = package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!;
        Assert.Equal(fire, runtime.Room.Lighting.FireTeamColor);
        Assert.Equal(players, runtime.Room.Lighting.PlayerTeamColor);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DestructiblePropsSurvivePngAndPackageAndPackagesKeepSoundBytes(bool package, bool bareId)
    {
        using var image = new Image<Rgba32>(8, 8);
        image[3, 3] = new Rgba32(255, 255, 255, 255);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var zone = CustomMapBuilderEntity.Create("logicDamageable", 80, 100, new Dictionary<string, string>
        { ["mapEntityId"] = "destructible-zone", ["health"] = "125", ["respawnTime"] = "3", ["onRedDestroyed"] = "node:red-destroy" });
        var zoneRef = MapLogicEntityReference.FormatEntityRef(zone);
        if (bareId) zoneRef = zone.Properties[MapLogicMetadata.MapEntityIdPropertyKey];
        var trigger = CustomMapBuilderEntity.Create("logicDamageTrigger", 160, 120, new Dictionary<string, string>
        { ["damageableEntity"] = zoneRef, ["onDestruction"] = "breakApart", ["destroyedOpacity"] = "25",
          ["onRespawn"] = "staticWarp", ["destructionTransitionTime"] = "0.75",
          ["destructionSound"] = "prop_break", ["respawnSound"] = "prop_spawn" });
        var signal = CustomMapBuilderEntity.Create("logicLatch", 200, 120, new Dictionary<string, string> { ["logicKey"] = "red-destroy" });
        var sprite = CustomMapBuilderEntity.Create("logicCustomSprite", 240, 120, new Dictionary<string, string>
        { ["image"] = "prop_image", ["scale"] = "2", ["damageableEntity"] = zoneRef });
        var foreground = CustomMapBuilderEntity.Create("logicForegroundSprite", 280, 120, new Dictionary<string, string>
        { ["image"] = "prop_image", ["scale"] = "2", ["damageableEntity"] = zoneRef });
        var light = CustomMapBuilderEntity.Create("light", 320, 120, new Dictionary<string, string>
        { ["mask"] = "prop_image", ["damageableEntity"] = zoneRef });
        var resources = new Dictionary<string, CustomMapBuilderResource>
        {
            ["prop_image"] = new("prop_image", "", CustomMapBuilderResourceKind.GenericImage, stream.ToArray()),
            ["prop_break"] = new("prop_break", "", CustomMapBuilderResourceKind.MessageSound, Encoding.ASCII.GetBytes("OggS break sound")),
            ["prop_spawn"] = new("prop_spawn", "", CustomMapBuilderResourceKind.MessageSound, Encoding.ASCII.GetBytes("OggS spawn sound")),
        };
        if (!package)
        {
            resources.Remove("prop_break");
            resources.Remove("prop_spawn");
            trigger = trigger with { Properties = new Dictionary<string, string>(trigger.Properties)
                { ["destructionSound"] = "CPCapturedSnd", ["respawnSound"] = "CPBeginCapSnd" } };
        }
        var document = _document with { Entities = [.. _document.Entities, zone, trigger, signal, sprite, foreground, light], Resources = resources };
        var path = Path.Combine(_root, package ? "destructible/props.json" : "destructible.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var runtime = package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!;
        Assert.NotNull(runtime);
        var room = runtime.Room;
        var zoneIndex = Enumerable.Range(0, room.RoomObjects.Count).Single(i => room.RoomObjects[i].Type == RoomObjectType.DamageableZone);
        var marker = room.RoomObjects[zoneIndex];
        Assert.Equal(3, marker.DamageableZone.RespawnSeconds);
        Assert.Equal(125, marker.DamageableZone.MaxHealth);
        Assert.Equal(room.LogicGraph.NodeIndexByKey["red-destroy"], marker.DamageableZone.OnRedDestroyedNodeIndex);
        Assert.Equal(PropDestructionEffect.BreakApart, marker.DestructionSettings.Destruction);
        Assert.Equal(PropRespawnEffect.StaticWarp, marker.DestructionSettings.Respawn);
        Assert.Equal(0.25f, marker.DestructionSettings.MinimumOpacity);
        Assert.Equal(0.75f, marker.DestructionSettings.TransitionSeconds);
        Assert.Equal(2, room.RoomObjects.Count(m => m.DestructibleZoneIndex == zoneIndex));
        Assert.Equal(zoneIndex, Assert.Single(room.MapLights).DestructibleZoneIndex);
        if (package)
        {
            Assert.Equal(resources["prop_break"].EmbeddedBytes, room.CustomMapVisuals.SoundResources["prop_break"].Bytes);
            Assert.Equal(resources["prop_spawn"].EmbeddedBytes, room.CustomMapVisuals.SoundResources["prop_spawn"].Bytes);
        }
        else
        {
            Assert.Equal("CPCapturedSnd", marker.DestructionSettings.DestructionSound);
            Assert.Equal("CPBeginCapSnd", marker.DestructionSettings.RespawnSound);
        }
    }

    [Theory]
    [InlineData(false, "team")]
    [InlineData(true, "team")]
    [InlineData(false, "40e0ff")]
    [InlineData(true, "40e0ff")]
    public void BloodColourAndGlowSurviveMapSave(bool package, string color)
    {
        var metadata = new Dictionary<string, string>(_document.Metadata)
            { ["lighting"] = "night", ["lightBloodColor"] = color, ["lightBloodGlow"] = "65" };
        var document = _document with { Metadata = metadata };
        var path = Path.Combine(_root, package ? "blood-lighting/map.json" : "blood-lighting.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var reopened = package ? CustomMapPackageImporter.ImportDocument(path)! : CustomMapBuilderPngImporter.Import(path)!;
        var lighting = MapLightingMetadata.Parse(reopened.Metadata);
        Assert.Equal(color == "team", lighting.BloodTeamColor);
        Assert.Equal(65, lighting.BloodGlow);
        var runtime = package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!;
        Assert.Equal(lighting, runtime.Room.Lighting);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LightColourTransitionSurvivesSaveAndRuntimeImport(bool package)
    {
        var light = CustomMapBuilderEntity.Create(MapLightMetadata.EntityType, 20, 30,
            new Dictionary<string, string> { ["colorTransition"] = "warmup", ["colorTransitionTime"] = "0.5",
                ["transition"] = "blink", ["fadeTime"] = "2" });
        var document = _document with { Entities = [.. _document.Entities, light] };
        var path = Path.Combine(_root, package ? "colour-transition/map.json" : "colour-transition.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var reopened = package ? CustomMapPackageImporter.ImportDocument(path)! : CustomMapBuilderPngImporter.Import(path)!;
        Assert.Equal("warmup", reopened.Entities[^1].Properties[MapLightMetadata.ColorTransitionKey]);
        Assert.Equal("0.5", reopened.Entities[^1].Properties[MapLightMetadata.ColorTransitionTimeKey]);
        var runtime = package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!;
        var marker = Assert.Single(runtime.Room.MapLights);
        Assert.Equal(MapLightTransition.Warmup, marker.Style.ColorTransition);
        Assert.Equal(0.5f, marker.Style.ColorTransitionTime);
        Assert.Equal(MapLightTransition.Blink, marker.Style.Transition);
        Assert.Equal(2, marker.Style.FadeTime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThirdAndInputSurvivesSaveAndRuntimeImport(bool package)
    {
        var source = CustomMapBuilderEntity.Create(MapLogicMetadata.CpTriggerEntityType, 10, 10,
            new Dictionary<string, string> { ["logicKey"] = "third" });
        var gate = CustomMapBuilderEntity.Create(MapLogicMetadata.GateEntityType, 20, 20,
            new Dictionary<string, string> { ["logicKey"] = "gate", ["gateType"] = "and",
                ["logicInput1"] = "node:third", ["logicInput2"] = "node:third", ["logicInput3"] = "node:third" });
        var document = _document with { Entities = [.. _document.Entities, source, gate] };
        var path = Path.Combine(_root, package ? "third-input/map.json" : "third-input.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var reopened = package ? CustomMapPackageImporter.ImportDocument(path)! : CustomMapBuilderPngImporter.Import(path)!;
        Assert.Equal("node:third", reopened.Entities[^1].Properties[MapLogicMetadata.LogicInput3PropertyKey]);
        var runtime = package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!;
        var graph = runtime.Room.LogicGraph;
        Assert.Equal(graph.NodeIndexByKey["third"], graph.Nodes[graph.NodeIndexByKey["gate"]].InputNodeIndex3);
        graph.Evaluate([]);
        Assert.False(graph.GetOutput(graph.NodeIndexByKey["gate"]));
        graph.PulseExternalOutput(graph.NodeIndexByKey["third"]);
        graph.EvaluateCombinatorialPropagators();
        Assert.True(graph.GetOutput(graph.NodeIndexByKey["gate"]));
        var broken = gate with { Properties = new Dictionary<string, string>(gate.Properties) { ["logicInput3"] = "node:missing" } };
        var validation = CustomMapBuilderValidator.Validate(document with { Entities = [.. document.Entities.Take(document.Entities.Count - 1), broken] });
        Assert.Contains(validation.Issues, issue => issue.Code == "missing_logic_reference" && issue.Message.Contains("logicInput3"));
    }

    [Theory]
    [InlineData(false, "screen", "global")]
    [InlineData(false, "screen", "local")]
    [InlineData(false, "local", "global")]
    [InlineData(false, "local", "local")]
    [InlineData(true, "screen", "global")]
    [InlineData(true, "screen", "local")]
    [InlineData(true, "local", "global")]
    [InlineData(true, "local", "local")]
    public void MessagePlacementAndSoundScopeSurviveSaveReopenAndUniformScaling(bool package, string placement, string soundScope)
    {
        var entity = CustomMapBuilderEntity.Create("gameplayMessage", 20, 30,
            new Dictionary<string, string>
            {
                ["style"] = "ltd", ["align"] = "center", ["placement"] = placement,
                ["soundScope"] = soundScope, ["sound"] = "IntelPutSnd", ["onEndSound"] = "CPCapturedSnd",
            });
        var document = _document with { Entities = [.. _document.Entities, entity] };
        var path = Path.Combine(_root, package ? "message-settings/settings.json" : "message-settings.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var reopened = package ? CustomMapPackageImporter.ImportDocument(path)! : CustomMapBuilderPngImporter.Import(path)!;
        Assert.Equal(placement, reopened.Entities[^1].Properties["placement"]);
        Assert.Equal(soundScope, reopened.Entities[^1].Properties["soundScope"]);
        var runtime = package ? CustomMapPackageImporter.Import(path)! : CustomMapPngImporter.Import(path)!;
        var marker = Assert.Single(runtime.Room.GameplayMessages);
        Assert.Equal(placement == "local" ? GameplayMessagePlacement.Local : GameplayMessagePlacement.Screen, marker.Placement);
        Assert.Equal(soundScope == "local" ? GameplayMessageSoundScope.Local : GameplayMessageSoundScope.Global, marker.SoundScope);
        var level = new SimpleLevel("message-settings", GameModeKind.ControlPoint,
            new WorldBounds(1024, 768), 1, null, 1, 1, new SpawnPoint(100, 100),
            [new SpawnPoint(100, 100)], [new SpawnPoint(900, 100)], [], [], 768, [], false,
            gameplayMessages: [marker]);
        var scaled = Assert.Single(SimpleLevelScaling.ApplyUniformScale(level, 2).GameplayMessages);
        Assert.Equal(marker.X * 2, scaled.X);
        Assert.Equal(marker.Y * 2, scaled.Y);
        Assert.Equal(marker.Placement, scaled.Placement);
        Assert.Equal(marker.SoundScope, scaled.SoundScope);
        Assert.Equal(marker.SoundName, scaled.SoundName);
        Assert.Equal(marker.OnEndSoundName, scaled.OnEndSoundName);
    }

    [Theory]
    [InlineData("sound")]
    [InlineData("music")]
    [InlineData("onEndSound")]
    public void ClonedMessageSoundReplacementIsIndependentAndSurvivesPackageReload(string property)
    {
        var originalBytes = Encoding.ASCII.GetBytes("OggS original sound");
        var replacementBytes = Encoding.ASCII.GetBytes("OggS replacement sound");
        var original = CustomMapBuilderEntity.Create("gameplayMessage", 20, 20,
            new Dictionary<string, string> { [property] = "message_sound", ["text"] = "Original" });
        var clone = original with { X = 120, Properties = new Dictionary<string, string>(original.Properties) };
        var resources = new Dictionary<string, CustomMapBuilderResource>(StringComparer.OrdinalIgnoreCase)
        {
            ["message_sound"] = new("message_sound", "", CustomMapBuilderResourceKind.MessageSound, originalBytes),
            ["MESSAGE_SOUND_2"] = new("MESSAGE_SOUND_2", "", CustomMapBuilderResourceKind.MessageSound, originalBytes),
        };
        var replacementName = CustomMapBuilderResourceCodec.CreateUnusedName("message_sound", resources);
        Assert.Equal("message_sound_3", replacementName);
        resources[replacementName] = new(replacementName, "", CustomMapBuilderResourceKind.MessageSound, replacementBytes);
        var cloneProperties = new Dictionary<string, string>(clone.Properties) { [property] = replacementName };
        clone = clone with { Properties = cloneProperties };
        var path = Path.Combine(_root, "messages", "messages.json");
        CustomMapPackageExporter.Export(_document with { Entities = [.. _document.Entities, original, clone], Resources = resources }, path);
        var runtime = CustomMapPackageImporter.Import(path)!;
        var messages = runtime.Room.GameplayMessages;
        Assert.Equal(2, messages.Count);
        string Sound(GameplayMessageMarker marker) => property switch
        {
            "music" => marker.MusicName, "onEndSound" => marker.OnEndSoundName, _ => marker.SoundName,
        };
        Assert.Equal("message_sound", Sound(messages[0]));
        Assert.Equal(replacementName, Sound(messages[1]));
        Assert.Equal(originalBytes, runtime.Room.CustomMapVisuals.SoundResources[Sound(messages[0])].Bytes);
        Assert.Equal(replacementBytes, runtime.Room.CustomMapVisuals.SoundResources[Sound(messages[1])].Bytes);
        // Replace the clone again, then publish over the existing manifest. Neither
        // an old package asset nor the original message may supply the new sound.
        var nextBytes = Encoding.ASCII.GetBytes("OggS updated replacement");
        resources[replacementName] = new(replacementName, "", CustomMapBuilderResourceKind.MessageSound, nextBytes);
        CustomMapPackageExporter.Export(_document with { Entities = [.. _document.Entities, original, clone], Resources = resources }, path);
        runtime = CustomMapPackageImporter.Import(path)!;
        Assert.Equal(nextBytes, runtime.Room.CustomMapVisuals.SoundResources[replacementName].Bytes);
        Assert.Equal(originalBytes, runtime.Room.CustomMapVisuals.SoundResources["message_sound"].Bytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LightMaskGlowAndThirdColourSurviveExportAndReopen(bool package, bool painted)
    {
        using var image = new Image<Rgba32>(8, 8);
        for (var i = 0; i < 8; i++) image[i, i] = new Rgba32(255, 255, 255, 255);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        var light = CustomMapBuilderEntity.Create("light", 20, 20, new Dictionary<string, string>
        {
            [MapLightMetadata.MaskKey] = "painted_mask", [MapLightMetadata.MaskPixelSizeKey] = painted ? "2" : "0",
            [MapLightMetadata.MaskGlowRadiusKey] = "12",
            [MapLightMetadata.ColorInputKey] = "node:secondary", [MapLightMetadata.TertiaryColorInputKey] = "node:tertiary",
            [MapLightMetadata.ColorKey] = "ff0000", [MapLightMetadata.AltColorKey] = "0000ff",
            [MapLightMetadata.TriggerColorKey] = "00ff00", [MapLightMetadata.ColorActionKey] = "trigger",
        });
        var document = _document with
        {
            Entities = [.. _document.Entities, light],
            Resources = new Dictionary<string, CustomMapBuilderResource>
            {
                ["painted_mask"] = new("painted_mask", string.Empty, CustomMapBuilderResourceKind.GenericImage, stream.ToArray()),
            },
        };
        var path = Path.Combine(_root, package ? "painted/painted.json" : "painted.png");
        CustomMapBuilderDocument reopened;
        CustomMapPngImporter.Result runtime;
        if (package)
        {
            CustomMapPackageExporter.Export(document, path);
            reopened = CustomMapPackageImporter.ImportDocument(path)!;
            runtime = CustomMapPackageImporter.Import(path)!;
        }
        else
        {
            CustomMapPngExporter.Export(document, path);
            reopened = CustomMapBuilderPngImporter.Import(path)!;
            runtime = CustomMapPngImporter.Import(path)!;
        }
        var marker = Assert.Single(runtime.Room.MapLights);
        Assert.Equal(painted ? 2 : 0, marker.MaskPixelSize);
        Assert.Equal(12, marker.MaskGlowRadius);
        Assert.Equal("node:secondary", marker.Style.ColorInput);
        Assert.Equal("node:tertiary", marker.Style.TertiaryColorInput);
        Assert.Equal("12", reopened.Entities[^1].Properties[MapLightMetadata.MaskGlowRadiusKey]);
        Assert.Equal(new MapLightingColor(0, 255, 0), marker.Style.TriggerColor);
        Assert.Equal(MapLightColorAction.Trigger, marker.Style.ColorAction);
        Assert.Equal("painted_mask", reopened.Entities[^1].Properties[MapLightMetadata.MaskKey]);
        using var decoded = Image.Load<Rgba32>(CustomMapBuilderResourceCodec.GetResourceBytes(reopened.Resources["painted_mask"]));
        Assert.Equal(255, decoded[4, 4].A);
        Assert.Equal(0, decoded[4, 3].A);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InactivePaintedLayerSurvivesMapSaveAndReload(bool package)
    {
        using var image = new Image<Rgba32>(8, 8);
        image[3, 4] = new Rgba32(255, 255, 255, 255);
        using var stream = new MemoryStream(); image.SaveAsPng(stream);
        var properties = new Dictionary<string, string>
        {
            [MapLightMetadata.MaskKey] = "painted_mask", [MapLightMetadata.MaskPixelSizeKey] = "1",
            [OpenGarrison.ClientShared.BuilderPaintedLightMask.BrushSizeKey] = "3",
        };
        OpenGarrison.ClientShared.BuilderPaintedLightMask.RememberActive(properties);
        properties[MapLightMetadata.MaskKey] = string.Empty;
        properties.Remove(MapLightMetadata.MaskPixelSizeKey);
        var document = _document with
        {
            Entities = [.. _document.Entities, CustomMapBuilderEntity.Create("light", 20, 20, properties)],
            Resources = new Dictionary<string, CustomMapBuilderResource>
            {
                ["painted_mask"] = new("painted_mask", string.Empty, CustomMapBuilderResourceKind.GenericImage, stream.ToArray()),
            },
        };
        var path = Path.Combine(_root, package ? "inactive/inactive.json" : "inactive.png");
        if (package) CustomMapPackageExporter.Export(document, path); else CustomMapPngExporter.Export(document, path);
        var reopened = package ? CustomMapPackageImporter.ImportDocument(path)! : CustomMapBuilderPngImporter.Import(path)!;
        var layer = OpenGarrison.ClientShared.BuilderPaintedLightMask.GetLayer(reopened.Entities[^1].Properties);
        Assert.Equal(("painted_mask", 1f), layer);
        Assert.Equal(3, OpenGarrison.ClientShared.BuilderPaintedLightMask.GetBrushSize(reopened.Entities[^1].Properties));
        using var decoded = Image.Load<Rgba32>(CustomMapBuilderResourceCodec.GetResourceBytes(reopened.Resources[layer.Resource!]));
        Assert.Equal(255, decoded[3, 4].A);
        Assert.Null(MapLightMetadata.FromProperties(0, 0, reopened.Entities[^1].Properties).Mask);
    }

    [Theory]
    [InlineData("cp_dirtbowl")]
    [InlineData("ctf_2dfort")]
    [InlineData("koth_harvest")]
    public void StockMapsLoadEditSaveReopenAndRun(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Core", "Content", "StockMaps"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = Path.Combine(directory!.FullName, "Core", "Content", "StockMaps", name + ".png");
        var before = File.ReadAllBytes(source);
        var document = CustomMapBuilderPngImporter.Import(source)!.NormalizeForEditing();
        Assert.NotEmpty(document.Entities);
        BuilderImageValidation.Validate(before);
        var validation = CustomMapBuilderValidator.Validate(document);
        Assert.True(validation.IsValid, string.Join("; ", validation.Issues.Select(issue => issue.Message)));
        var entities = document.Entities.ToArray();
        entities[0] = (entities[0] with { X = entities[0].X + 72 }).NormalizeForEditing();
        var edited = document with { Entities = [..entities.SkipLast(1), CustomMapBuilderEntity.Create("medCabinet", 102, 60)] };
        var output = Path.Combine(_root, name + ".png");
        CustomMapPngExporter.Export(edited, output);
        var reopened = CustomMapBuilderPngImporter.Import(output)!;
        Assert.Equal(entities[0].X, reopened.Entities[0].X);
        Assert.Equal("medCabinet", reopened.Entities[^1].Type);
        Assert.NotNull(CustomMapPngImporter.Import(output));
        Assert.True(EmbeddedWalkmaskDecoder.TryDecodeSolidCells(document.EmbeddedWalkmaskSection, out var w, out var h, out var cells));
        Assert.True(EmbeddedWalkmaskDecoder.TryDecodeSolidCells(reopened.EmbeddedWalkmaskSection, out var w2, out var h2, out var cells2));
        Assert.Equal((w, h), (w2, h2)); Assert.Equal(cells, cells2);
        CustomMapPngExporter.Export(reopened, output);
        var package = Path.Combine(_root, name, name + ".json");
        CustomMapPackageExporter.Export(reopened, package);
        Assert.Equal(entities[0].X, CustomMapPackageImporter.ImportDocument(package)!.Entities[0].X);
        Assert.NotNull(CustomMapPackageImporter.Import(package));
        Assert.Equal(before, File.ReadAllBytes(source));
    }

    [Fact]
    public void RemovedLayerStaysRemovedButReusableResourceRemains()
    {
        var resource = CustomMapBuilderResourceCodec.FromFile("sky", _document.BackgroundImagePath, CustomMapBuilderResourceKind.ParallaxLayer);
        var original = _document with { Resources = new Dictionary<string, CustomMapBuilderResource> { ["sky"] = resource }, ParallaxLayers = [new(1, "sky", .2f, .4f)] };
        var png = Path.Combine(_root, "layer.png"); CustomMapPngExporter.Export(original, png);
        var imported = CustomMapBuilderPngImporter.Import(png)!;
        Assert.Single(imported.ParallaxLayers);
        CustomMapPngExporter.Export(imported with { ParallaxLayers = [] }, png);
        var edited = CustomMapBuilderPngImporter.Import(png)!;
        Assert.Empty(edited.ParallaxLayers);
        Assert.Contains("sky", edited.Resources.Keys);
    }

    [Fact]
    public void StaticGifIsTranscodedAndAnimatedGifIsKeptAsAnimatedPng()
    {
        var gif = Path.Combine(_root, "static.gif");
        using (var image = new Image<Rgba32>(4, 4, new Rgba32(255, 0, 0, 255))) image.SaveAsGif(gif);
        var document = _document with { Resources = new Dictionary<string, CustomMapBuilderResource> { ["sprite"] = CustomMapBuilderResourceCodec.FromFile("sprite", gif) } };
        var output = Path.Combine(_root, "gif-map.json"); CustomMapPackageExporter.Export(document, output);
        Assert.EndsWith(".png", CustomMapPackageImporter.ImportDocument(output)!.Resources["sprite"].SourcePath);
        using (var image = new Image<Rgba32>(4, 4)) { using var other = new Image<Rgba32>(4, 4, new Rgba32(0,255,0,255)); image.Frames.AddFrame(other.Frames[0]); image.SaveAsGif(gif); }
        document = document with { Resources = new Dictionary<string, CustomMapBuilderResource> { ["sprite"] = CustomMapBuilderResourceCodec.FromFile("sprite", gif) } };
        CustomMapPackageExporter.Export(document, output);
        var animated = CustomMapPackageImporter.ImportDocument(output)!.Resources["sprite"];
        Assert.EndsWith(".png", animated.SourcePath);
        var bytes = CustomMapBuilderResourceCodec.GetResourceBytes(animated);
        Assert.Equal((byte)137, bytes[0]);
        using var reloaded = Image.Load<Rgba32>(bytes);
        Assert.Equal(2, reloaded.Frames.Count);
        Assert.Equal(new Rgba32(0, 255, 0, 255), reloaded.Frames[1][1, 1]);
    }

    [Fact]
    public void MalformedInputsFailCleanlyAndCannotReplaceSavedDraft()
    {
        var png = Path.Combine(_root, "bad.png"); File.WriteAllBytes(png, [137,80,78,71,13,10,26,10,0,0]);
        Assert.ThrowsAny<IOException>(() => CustomMapBuilderPngImporter.Import(png));
        Assert.Null(CustomMapPngImporter.Import(png));
        var manifest = Path.Combine(_root, "bad.json"); File.WriteAllText(manifest, "{\"entities\":[null]}");
        Assert.Null(CustomMapPackageImporter.ImportDocument(manifest));
        Assert.False(EmbeddedWalkmaskDecoder.TryDecodeSolidCells("9999999999 9999999999\n_", out _, out _, out _));
        var path = Path.Combine(_root, "draft.ogmap"); BuilderProjectStore.Save(_document, path); var before = File.ReadAllBytes(path);
        Assert.Throws<InvalidDataException>(() => BuilderProjectStore.Save(_document with { Entities = [null!] }, path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-regression-" + Guid.NewGuid().ToString("N"));
    private readonly CustomMapBuilderDocument _document;

    [Fact]
    public void AlreadyBrokenSaveRecoversNewestValidExplicitPayload()
    {
        var legacy = Path.Combine(_root, "broken.png"); WriteLegacy(legacy, "zTXt");
        var bytes = File.ReadAllBytes(legacy);
        var edited = _document with { Entities = [CustomMapBuilderEntity.Create("redspawn", 72, 18)] };
        using (var output = File.Create(legacy))
        {
            output.Write(bytes.AsSpan(0, bytes.Length - 12));
            Chunk(output, "tEXt", Encoding.Latin1.GetBytes("OpenGarrisonLevelData\0" + CustomMapPngExporter.BuildLevelData(edited)));
            // An incomplete later attempt must not displace the intact newer save.
            Chunk(output, "tEXt", Encoding.Latin1.GetBytes("OpenGarrisonLevelData\0{ENTITIES}broken{END ENTITIES}{WALKMASK}broken{END WALKMASK}"));
            output.Write(bytes.AsSpan(bytes.Length - 12));
        }
        Assert.Equal(72, CustomMapBuilderPngImporter.Import(legacy, out var warning)!.Entities[0].X);
        Assert.NotEmpty(warning); Assert.NotNull(CustomMapPngImporter.Import(legacy));
        var before = File.ReadAllBytes(legacy);
        CustomMapPngExporter.Export(edited, legacy);
        Assert.Equal(before, File.ReadAllBytes(legacy + ".bak"));
    }

    [Fact]
    public void EmptyNonSixDivisibleCollisionSurvivesBothFormats()
    {
        var wm = Path.Combine(_root, "empty-wm.png"); using (var image = new Image<Rgba32>(5,7)) image.SaveAsPng(wm);
        var doc = _document with { WalkmaskImagePath = wm, Metadata = new Dictionary<string,string>(_document.Metadata) { [MapMovementModeMetadata.MovementModePropertyKey] = MapMovementModeMetadata.TopDownPropertyValue } };
        var output = Path.Combine(_root, "empty.png"); CustomMapPngExporter.Export(doc, output);
        var reopened = CustomMapBuilderPngImporter.Import(output)!;
        Assert.True(EmbeddedWalkmaskDecoder.TryDecodeSolidCells(reopened.EmbeddedWalkmaskSection, out var w, out var h, out var cells));
        Assert.Equal((5,7), (w,h)); Assert.All(cells, c => Assert.False(c));
        var package = Path.Combine(_root, "empty.json"); CustomMapPackageExporter.Export(reopened, package);
        using var saved = Image.Load<Rgba32>(CustomMapPackageImporter.ImportDocument(package)!.WalkmaskImagePath);
        Assert.Equal(5, saved.Width); Assert.Equal(7, saved.Height); Assert.Equal((byte)0, saved[4,6].A);
    }

    [Fact]
    public void LockedWindowsDestinationPreservesThePreviousCompleteSave()
    {
        if (!OperatingSystem.IsWindows()) return; // Unix file locks do not prevent renaming an open file.
        var output = Path.Combine(_root, "locked.png"); CustomMapPngExporter.Export(_document, output);
        var before = File.ReadAllBytes(output);
        using (var locked = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => CustomMapPngExporter.Export(_document with { Entities = [] }, output));
        Assert.Equal(before, File.ReadAllBytes(output));
        var package = Path.Combine(_root, "locked.json"); CustomMapPackageExporter.Export(_document, package); before = File.ReadAllBytes(package);
        using (var locked = new FileStream(package, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => CustomMapPackageExporter.Export(_document with { Entities = [] }, package));
        Assert.Equal(before, File.ReadAllBytes(package)); Assert.NotNull(CustomMapPackageImporter.Import(package));
    }

    public BuilderPersistenceRegressionTests()
    {
        Directory.CreateDirectory(_root);
        var bg = Path.Combine(_root, "source-bg.png");
        var wm = Path.Combine(_root, "source-wm.png");
        using (var image = new Image<Rgba32>(24, 16, new Rgba32(64, 128, 192, 255))) image.SaveAsPng(bg);
        using (var image = new Image<Rgba32>(24, 16)) { image[0, 0] = new Rgba32(255, 255, 255, 255); image.SaveAsPng(wm); }
        _document = CustomMapBuilderDocument.CreateEmpty("map") with
        {
            BackgroundImagePath = bg, WalkmaskImagePath = wm,
            Entities = [CustomMapBuilderEntity.Create("redspawn", 12, 18), CustomMapBuilderEntity.Create("bluespawn", 42, 18)],
        };
    }

    [Theory]
    [InlineData("tEXt", false)] [InlineData("tEXt", true)]
    [InlineData("zTXt", false)] [InlineData("zTXt", true)]
    [InlineData("iTXt", false)] [InlineData("iTXt", true)]
    [InlineData("iTXt-compressed", false)] [InlineData("iTXt-compressed", true)]
    [InlineData("split", false)] [InlineData("split", true)]
    public void EditedLegacyMapSurvivesRepeatedSaveAndRuntimeLoad(string encoding, bool overwrite)
    {
        var source = Path.Combine(_root, "legacy.png");
        WriteLegacy(source, encoding);
        var loaded = CustomMapBuilderPngImporter.Import(source)!;
        var edited = loaded with { Entities = [CustomMapBuilderEntity.Create("redspawn", 72, 18), CustomMapBuilderEntity.Create("medCabinet", 60, 30)] };
        var output = overwrite ? source : Path.Combine(_root, "edited.png");
        CustomMapPngExporter.Export(edited, output);
        var reopened = CustomMapBuilderPngImporter.Import(output)!;
        Assert.Equal(72, reopened.Entities[0].X);
        Assert.Equal("medCabinet", reopened.Entities[1].Type);
        Assert.NotNull(CustomMapPngImporter.Import(output));
        CustomMapPngExporter.Export(reopened, output);
        Assert.Equal(72, CustomMapBuilderPngImporter.Import(output)!.Entities[0].X);
        Assert.True(EmbeddedWalkmaskDecoder.TryDecodeSolidCells(reopened.EmbeddedWalkmaskSection, out _, out _, out var cells));
        Assert.True(cells[0]); Assert.Equal(1, cells.Count(c => c));
    }

    [Fact]
    public void FailedPngSaveLeavesPreviousMapIntact()
    {
        var path = Path.Combine(_root, "map.png"); CustomMapPngExporter.Export(_document, path);
        var before = File.ReadAllBytes(path);
        var bad = Path.Combine(_root, "bad.png"); File.WriteAllText(bad, "not a PNG");
        Assert.ThrowsAny<Exception>(() => CustomMapPngExporter.Export(_document with { BackgroundImagePath = bad }, path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void FailedPackageSaveLeavesManifestAndAllReferencedAssetsIntact()
    {
        var path = Path.Combine(_root, "package", "map.json"); CustomMapPackageExporter.Export(_document, path);
        var before = CustomMapHashService.ComputePackageSha256(path);
        var bg = Path.Combine(_root, "green.png"); using (var image = new Image<Rgba32>(24, 16, new Rgba32(0,255,0,255))) image.SaveAsPng(bg);
        var bad = _document with { BackgroundImagePath = bg, Resources = new Dictionary<string, CustomMapBuilderResource> { ["missing"] = new("missing", Path.Combine(_root, "missing.png"), CustomMapBuilderResourceKind.GenericImage) } };
        Assert.ThrowsAny<Exception>(() => CustomMapPackageExporter.Export(bad, path));
        Assert.Equal(before, CustomMapHashService.ComputePackageSha256(path));
        CustomMapPackageExporter.Export(_document with { BackgroundImagePath = bg }, path);
        Assert.NotEqual(before, CustomMapHashService.ComputePackageSha256(path));
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void DefaultsUnicodeAndEmptyEntityListsRoundTrip()
    {
        var entity = CustomMapBuilderEntity.Create("SetupGate", 20, 20, xScale: 2, yScale: 2).NormalizeForEditing();
        entity = (entity with { XScale = 1, YScale = 1 }).NormalizeForEditing();
        var path = Path.Combine(_root, "map.png");
        CustomMapPngExporter.Export(_document with { Entities = [entity], Metadata = new Dictionary<string,string> { ["author"] = "Ω 日本語" } }, path);
        var read = CustomMapBuilderPngImporter.Import(path)!;
        Assert.Equal(1, read.Entities[0].XScale); Assert.Equal("Ω 日本語", read.Metadata["author"]);
        CustomMapPngExporter.Export(_document with { Entities = [] }, path);
        Assert.Empty(CustomMapBuilderPngImporter.Import(path)!.Entities);
    }

    [Fact]
    public void DraftsPreserveIncompleteMapsAndEmbeddedAssets()
    {
        var path = Path.Combine(_root, "draft.ogmap");
        BuilderProjectStore.Save(CustomMapBuilderDocument.CreateEmpty(), path);
        Assert.Empty(BuilderProjectStore.Load(path, Path.Combine(_root, "cache")).Entities);
        BuilderProjectStore.Save(_document, path);
        var loaded = BuilderProjectStore.Load(path, Path.Combine(_root, "cache"));
        Assert.Equal(File.ReadAllBytes(_document.BackgroundImagePath), File.ReadAllBytes(loaded.BackgroundImagePath));
        Assert.Equal(_document.Entities.Count, loaded.Entities.Count);
    }

    [Fact]
    public void ConvertedPackageRefreshesOnlyWhenItsOwnContentIsUnchanged()
    {
        var source = Path.Combine(_root, "converted.png"); CustomMapPngExporter.Export(_document, source);
        Assert.True(CustomMapLegacyPackageAutoConverter.TryConvertLegacyPng(source, out var package, out var error), error);
        var edited = _document with { Entities = [CustomMapBuilderEntity.Create("redspawn", 72, 18)] };
        CustomMapPngExporter.Export(edited, source);
        Assert.True(CustomMapLegacyPackageAutoConverter.TryConvertLegacyPng(source, out _, out error), error);
        Assert.Equal(72, CustomMapPackageImporter.ImportDocument(package)!.Entities[0].X);
        CustomMapPackageExporter.Export(edited with { Entities = [CustomMapBuilderEntity.Create("redspawn", 90,18)] }, package);
        CustomMapPngExporter.Export(_document, source);
        Assert.False(CustomMapLegacyPackageAutoConverter.TryConvertLegacyPng(source, out _, out _));
        Assert.Equal(90, CustomMapPackageImporter.ImportDocument(package)!.Entities[0].X);
    }

    [Fact]
    public void ExplicitManifestPathDoesNotChange()
    {
        var path = Path.Combine(_root, "collection", "map.json");
        Assert.Equal(path, CustomMapPackageExporter.ResolvePackageManifestPath(_document, path));
    }

    [Fact]
    public void MissingSoundsCannotBeSilentlyDiscardedByPngExport()
    {
        var document = _document with { Resources = new Dictionary<string,CustomMapBuilderResource> { ["sound"] = new("sound", "", CustomMapBuilderResourceKind.MessageSound, Encoding.ASCII.GetBytes("OggS")) } };
        Assert.Throws<InvalidOperationException>(() => CustomMapPngExporter.Export(document, Path.Combine(_root, "sound.png")));
    }

    private void WriteLegacy(string path, string format)
    {
        var background = File.ReadAllBytes(_document.BackgroundImagePath);
        using var output = File.Create(path); output.Write(background.AsSpan(0, background.Length - 12));
        var data = CustomMapPngExporter.BuildLevelData(_document);
        if (format == "split")
        {
            Chunk(output, "tEXt", Encoding.Latin1.GetBytes("Entities\0" + CustomMapPngExporter.BuildEntitiesSection(_document)));
            Chunk(output, "tEXt", Encoding.Latin1.GetBytes("Walkmask\0" + CustomMapPngExporter.BuildWalkmaskSection(_document.WalkmaskImagePath)));
        }
        else
        {
            var type = format.StartsWith("iTXt") ? "iTXt" : format;
            var payload = format.StartsWith("iTXt") ? Encoding.UTF8.GetBytes(data) : Encoding.Latin1.GetBytes(data);
            var compress = format is "zTXt" or "iTXt-compressed";
            if (compress) { using var m = new MemoryStream(); using (var z = new ZLibStream(m, CompressionLevel.Optimal, true)) z.Write(payload); payload = m.ToArray(); }
            var header = format switch { "tEXt" => "Level\0", "zTXt" => "Level\0\0", "iTXt" => "Level\0\0\0\0\0", _ => "Level\0\x01\0\0\0" };
            Chunk(output, type, Encoding.ASCII.GetBytes(header).Concat(payload).ToArray());
        }
        output.Write(background.AsSpan(background.Length - 12));
    }
    private static void Chunk(Stream stream, string type, byte[] data)
    {
        Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, data.Length); stream.Write(b);
        var name = Encoding.ASCII.GetBytes(type); stream.Write(name); stream.Write(data); uint crc = 0xffffffff;
        foreach (var v in name.Concat(data)) { crc ^= v; for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? 0xedb88320 ^ (crc >> 1) : crc >> 1; }
        BinaryPrimitives.WriteUInt32BigEndian(b, crc ^ 0xffffffff); stream.Write(b);
    }
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
