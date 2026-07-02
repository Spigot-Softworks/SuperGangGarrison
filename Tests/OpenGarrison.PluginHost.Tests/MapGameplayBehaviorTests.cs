using System.Collections.Generic;
using OpenGarrison.Core;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class MapGameplayBehaviorTests
{
    [Fact]
    public void RuntimeImporterCreatesGameplayMessageMarker()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            GameplayMessageMetadata.EntityType,
            112f,
            144f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [GameplayMessageMetadata.TriggerPropertyKey] = "node:alarm",
                [GameplayMessageMetadata.TextPropertyKey] = "Hold the point",
                [GameplayMessageMetadata.StylePropertyKey] = "notification",
                [GameplayMessageMetadata.AnimationPropertyKey] = "fromBottom",
                [GameplayMessageMetadata.FontPropertyKey] = "timer",
                [GameplayMessageMetadata.FontScalePropertyKey] = "1.5",
                [GameplayMessageMetadata.AlignmentPropertyKey] = "right",
                [GameplayMessageMetadata.ChatTeamPropertyKey] = "blue",
                [GameplayMessageMetadata.ScreenXPropertyKey] = "0.25",
                [GameplayMessageMetadata.ScreenYPropertyKey] = "48",
                [GameplayMessageMetadata.WidthPropertyKey] = "420",
                [GameplayMessageMetadata.HeightPropertyKey] = "36",
                [GameplayMessageMetadata.DurationPropertyKey] = "4.25",
                [GameplayMessageMetadata.EndModePropertyKey] = "both",
                [GameplayMessageMetadata.InputPropertyKey] = "jump",
                [GameplayMessageMetadata.FreezeSimulationPropertyKey] = "true",
                [GameplayMessageMetadata.SoundPropertyKey] = "alert.ogg",
                [GameplayMessageMetadata.MusicPropertyKey] = "theme.ogg",
                [GameplayMessageMetadata.MusicCrossfadePropertyKey] = "false",
                [GameplayMessageMetadata.MusicCrossfadeSecondsPropertyKey] = "2.25",
                [GameplayMessageMetadata.MusicLoopPropertyKey] = "false",
                [GameplayMessageMetadata.MusicFadeAfterSecondsPropertyKey] = "12.5",
                [GameplayMessageMetadata.ImagePropertyKey] = "portrait",
                [GameplayMessageMetadata.ImageOffsetXPropertyKey] = "12",
                [GameplayMessageMetadata.ImageOffsetYPropertyKey] = "-8",
                [GameplayMessageMetadata.ImageWidthPropertyKey] = "96",
                [GameplayMessageMetadata.ImageHeightPropertyKey] = "80",
            },
            context));

        var marker = Assert.Single(context.GameplayMessages);
        Assert.Equal(112f, marker.X);
        Assert.Equal(144f, marker.Y);
        Assert.Equal("node:alarm", marker.TriggerRef);
        Assert.Equal("Hold the point", marker.Text);
        Assert.Equal(GameplayMessageStyle.Notification, marker.Style);
        Assert.Equal(GameplayMessageAnimation.FromBottom, marker.Animation);
        Assert.Equal(GameplayMessageFont.Timer, marker.Font);
        Assert.Equal(1.5f, marker.FontScale);
        Assert.Equal(GameplayMessageAlignment.Right, marker.Alignment);
        Assert.Equal(GameplayMessageChatTeam.Blue, marker.ChatTeam);
        Assert.Equal(0.25f, marker.ScreenX);
        Assert.Equal(48f, marker.ScreenY);
        Assert.Equal(420f, marker.Width);
        Assert.Equal(36f, marker.Height);
        Assert.Equal(4.25f, marker.DurationSeconds);
        Assert.Equal(GameplayMessageEndMode.AutoOrInput, marker.EndMode);
        Assert.Equal("jump", marker.InputBinding);
        Assert.True(marker.FreezeSimulation);
        Assert.Equal("alert.ogg", marker.SoundName);
        Assert.Equal("theme.ogg", marker.MusicName);
        Assert.False(marker.MusicCrossfade);
        Assert.Equal(2.25f, marker.MusicCrossfadeSeconds);
        Assert.False(marker.MusicLoop);
        Assert.Equal(12.5f, marker.MusicFadeAfterSeconds);
        Assert.Equal("portrait", marker.ImageResourceName);
        Assert.Equal(12f, marker.ImageOffsetX);
        Assert.Equal(-8f, marker.ImageOffsetY);
        Assert.Equal(96f, marker.ImageWidth);
        Assert.Equal(80f, marker.ImageHeight);
        Assert.Equal("Chat", GameplayMessageMetadata.GetStyleDisplayLabel("chat"));
        Assert.Equal("From bottom", GameplayMessageMetadata.GetAnimationDisplayLabel("fromBottom"));
    }

    [Fact]
    public void RuntimeImporterCreatesGameplaySoundMarker()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            GameplaySoundMetadata.EntityType,
            180f,
            96f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [GameplaySoundMetadata.TriggerPropertyKey] = "node:alarm",
                [GameplaySoundMetadata.SoundPropertyKey] = "alarm.ogg",
                [GameplaySoundMetadata.ModePropertyKey] = "music",
                [GameplaySoundMetadata.CrossfadePropertyKey] = "false",
                [GameplaySoundMetadata.CrossfadeSecondsPropertyKey] = "2.25",
            },
            context));

        var marker = Assert.Single(context.GameplaySounds);
        Assert.Equal(180f, marker.X);
        Assert.Equal(96f, marker.Y);
        Assert.Equal("node:alarm", marker.TriggerRef);
        Assert.Equal("alarm.ogg", marker.SoundName);
        Assert.Equal(GameplaySoundMode.Music, marker.Mode);
        Assert.False(marker.Crossfade);
        Assert.Equal(2.25f, marker.CrossfadeSeconds);
        Assert.Equal("Sound", GameplaySoundMetadata.GetModeDisplayLabel("sound"));
        Assert.Equal("Music", GameplaySoundMetadata.GetModeDisplayLabel("track"));
    }

    [Fact]
    public void RuntimeImporterKeepsLegacyGameplayMessageAnimationStylesCompatible()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            GameplayMessageMetadata.EntityType,
            20f,
            30f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [GameplayMessageMetadata.StylePropertyKey] = "typing",
            },
            context));

        var marker = Assert.Single(context.GameplayMessages);
        Assert.Equal(GameplayMessageStyle.Basic, marker.Style);
        Assert.Equal(GameplayMessageAnimation.Typing, marker.Animation);
    }

    [Fact]
    public void RuntimeImporterUsesEntityBoundsForGameplayMessagePlacement()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            GameplayMessageMetadata.EntityType,
            64f,
            96f,
            2f,
            1.5f,
            new Dictionary<string, string>
            {
                [GameplayMessageMetadata.TextPropertyKey] = GameplayMessageMetadata.DefaultText,
                ["xscale"] = "2",
                ["yscale"] = "1.5",
            },
            context));

        var marker = Assert.Single(context.GameplayMessages);
        Assert.Equal(64f, marker.ScreenX);
        Assert.Equal(96f, marker.ScreenY);
        Assert.Equal(GameplayMessageMetadata.DefaultWidth * 2f, marker.Width);
        Assert.Equal(GameplayMessageMetadata.DefaultHeight * 1.5f, marker.Height);
        Assert.Equal(GameplayMessageMetadata.DefaultText, marker.Text);
        Assert.Contains($"text={GameplayMessageMetadata.DefaultText}", GameplayMessageMetadata.DefaultProperties);
    }

    [Fact]
    public void RuntimeImporterUsesScaledGameplayMessageBoundsOverLegacyPlacementProperties()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            GameplayMessageMetadata.EntityType,
            128f,
            160f,
            0.25f,
            0.5f,
            new Dictionary<string, string>
            {
                [GameplayMessageMetadata.TextPropertyKey] = "Hello!",
                [GameplayMessageMetadata.ScreenXPropertyKey] = "0.5",
                [GameplayMessageMetadata.ScreenYPropertyKey] = "0.25",
                [GameplayMessageMetadata.WidthPropertyKey] = "320",
                [GameplayMessageMetadata.HeightPropertyKey] = "72",
            },
            context));

        var marker = Assert.Single(context.GameplayMessages);
        Assert.Equal(128f, marker.ScreenX);
        Assert.Equal(160f, marker.ScreenY);
        Assert.Equal(GameplayMessageMetadata.DefaultWidth * 0.25f, marker.Width);
        Assert.Equal(GameplayMessageMetadata.DefaultHeight * 0.5f, marker.Height);
    }

    [Fact]
    public void GameplayMessageOnEndTeleportResolvesTeleportExitReference()
    {
        var roomObjects = new List<RoomObjectMarker>();
        var exitProperties = new Dictionary<string, string>
        {
            [MapLogicMetadata.MapEntityIdPropertyKey] = "exit01",
        };
        var context = new CustomMapEntityImportContext
        {
            RoomObjects = roomObjects,
        };

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            TeleportMetadata.TeleportExitEntityType,
            100f,
            120f,
            1f,
            1f,
            exitProperties,
            context));

        var exitRef = MapLogicEntityReference.FormatEntityRef(
            TeleportMetadata.TeleportExitEntityType,
            "exit01");
        var message = GameplayMessageMetadata.FromProperties(
            20f,
            30f,
            new Dictionary<string, string>
            {
                [GameplayMessageMetadata.OnEndMapTeleportPropertyKey] = "true",
                [GameplayMessageMetadata.OnEndTeleportExitPropertyKey] = exitRef,
            });

        var resolved = GameplayMessageRuntimePatch.ResolveTriggerSignals(
            [message],
            new MapLogicGraph([], []),
            roomObjects,
            [new MapImportedEntity(TeleportMetadata.TeleportExitEntityType, 100f, 120f, exitProperties)]);
        var marker = Assert.Single(resolved);
        var exit = Assert.Single(roomObjects, candidate => candidate.Type == RoomObjectType.TeleportExit);
        Assert.Equal(exitRef, marker.OnEndTeleportExitRef);
        Assert.Equal(exit.CenterX, marker.OnEndTeleportX);
        Assert.Equal(exit.CenterY, marker.OnEndTeleportY);
    }

    [Fact]
    public void RuntimeImporterCreatesSpawnClassBehaviorMarker()
    {
        var context = new CustomMapEntityImportContext();

        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            SpawnClassBehaviorMetadata.EntityType,
            240f,
            220f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [SpawnClassBehaviorMetadata.TeamPropertyKey] = "red",
                [SpawnClassBehaviorMetadata.ForceClassPropertyKey] = "soldier",
                [SpawnClassBehaviorMetadata.ManualSpawnPropertyKey] = "true",
                [SpawnClassBehaviorMetadata.SkipTeamSelectPropertyKey] = "true",
                [SpawnClassBehaviorMetadata.AllowTeamChangePropertyKey] = "false",
                [SpawnClassBehaviorMetadata.AllowClassChangePropertyKey] = "false",
            },
            context));

        var marker = Assert.Single(context.SpawnClassBehaviors);
        Assert.Equal(240f, marker.X);
        Assert.Equal(220f, marker.Y);
        Assert.Equal(SpawnClassBehaviorTeam.Red, marker.Team);
        Assert.Equal(PlayerClass.Soldier, marker.ForcedClass);
        Assert.True(marker.ManualSpawn);
        Assert.True(marker.SkipTeamSelect);
        Assert.False(marker.AllowTeamChange);
        Assert.False(marker.AllowClassChange);
    }

    [Fact]
    public void SpawnClassBehaviorForcesPlayerClassAndManualSpawn()
    {
        var world = CreateWorldWithSpawnClassBehavior(
            new SpawnClassBehaviorMarker(
                240f,
                220f,
                SpawnClassBehaviorTeam.Red,
                PlayerClass.Soldier,
                ManualSpawn: true,
                SkipTeamSelect: true,
                AllowTeamChange: false,
                AllowClassChange: false));

        world.PrepareLocalPlayerJoin();
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);

        Assert.Equal(PlayerTeam.Red, world.LocalPlayer.Team);
        Assert.Equal(PlayerClass.Soldier, world.LocalPlayer.ClassId);
        Assert.Equal(240f, world.LocalPlayer.X);
        Assert.Equal(220f, world.LocalPlayer.Y);
        Assert.False(world.CanNetworkPlayerChangeTeamInCurrentMode(SimulationWorld.LocalPlayerSlot));
        Assert.False(world.CanNetworkPlayerSelectClassInCurrentMode(
            SimulationWorld.LocalPlayerSlot,
            CharacterClassCatalog.Scout));
    }

    [Fact]
    public void MapSpawnedServerBotBypassesPlayerSpawnClassBehavior()
    {
        var world = CreateWorldWithSpawnClassBehavior(
            new SpawnClassBehaviorMarker(
                40f,
                160f,
                SpawnClassBehaviorTeam.Any,
                PlayerClass.Soldier,
                ManualSpawn: true,
                SkipTeamSelect: false,
                AllowTeamChange: false,
                AllowClassChange: false));
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController());

        Assert.True(botManager.TrySpawnMapBot(
            PlayerTeam.Red,
            PlayerClass.Medic,
            BotSpawnKind.Bot,
            true,
            BotSpawnRespawnMode.NormalSpawn,
            BotSpawnNameMode.Random,
            string.Empty,
            false,
            false,
            240f,
            220f,
            out var slot));

        Assert.True(world.TryGetNetworkPlayer(slot, out var bot));
        Assert.Equal(PlayerTeam.Red, bot.Team);
        Assert.Equal(PlayerClass.Medic, bot.ClassId);
        Assert.Equal(240f, bot.X);
        Assert.Equal(220f, bot.Y);
    }

    private static SimulationWorld CreateWorldWithSpawnClassBehavior(SpawnClassBehaviorMarker behavior)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var fallbackSpawn = new SpawnPoint(40f, 160f);
        world.CombatTestSetLevel(new SimpleLevel(
            "map-gameplay-behavior-test",
            GameModeKind.TeamDeathmatch,
            new WorldBounds(512f, 512f),
            1f,
            null,
            1,
            1,
            fallbackSpawn,
            [fallbackSpawn],
            [fallbackSpawn],
            [],
            [],
            floorY: 512f,
            [],
            importedFromSource: false,
            spawnClassBehaviors: [behavior]));
        return world;
    }
}
