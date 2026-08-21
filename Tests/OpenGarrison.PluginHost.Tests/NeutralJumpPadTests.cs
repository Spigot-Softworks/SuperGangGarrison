using System.Collections.Generic;
using System.Reflection;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class NeutralJumpPadTests
{
    private static readonly MethodInfo RestartCurrentRoundMethod = typeof(SimulationWorld)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(method => method.Name == "RestartCurrentRound" && method.GetParameters().Length == 2);

    [Fact]
    public void BuilderNeutralJumpPadImportsAsMapSpawn()
    {
        Assert.True(CustomMapBuilderEntityCatalog.TryGetDefinition(JumpPadMetadata.EntityType, out var definition));
        Assert.Equal(JumpPadMetadata.NeutralTeamPropertyValue, definition.DefaultProperties[JumpPadMetadata.NeutralTeamPropertyKey]);

        var context = new CustomMapEntityImportContext();
        Assert.True(CustomMapEntityRuntimeRegistry.TryImport(
            JumpPadMetadata.EntityType,
            100f,
            120f,
            1f,
            1f,
            new Dictionary<string, string>
            {
                [JumpPadMetadata.NeutralTeamPropertyKey] = JumpPadMetadata.NeutralTeamPropertyValue,
            },
            context));

        var marker = Assert.Single(context.JumpPadSpawns);
        Assert.Equal(100f, marker.X);
        Assert.Equal(120f, marker.Y);
    }

    [Fact]
    public void MapNeutralJumpPadLaunchesBothTeamsAndSurvivesOwnerValidation()
    {
        var world = CreateWorldWithNeutralJumpPad();
        var pad = Assert.Single(world.JumpPads);
        Assert.True(pad.IsNeutral);
        Assert.Equal(PlayerTeam.Neutral, pad.Team);
        Assert.Equal(0, pad.OwnerPlayerId);
        Assert.True(pad.IsBuilt);
        Assert.True(pad.HasLanded);

        world.PrepareLocalPlayerJoin();
        world.SetLocalPlayerTeam(PlayerTeam.Red);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        world.LocalPlayer.SetSpawnRoomState(false);
        Assert.True(world.TryApplyJumpPadJumpBoostForPrediction(
            PlacePlayerOnPad(world.LocalPlayer, pad),
            jumped: true));
        Assert.True(world.LocalPlayer.VerticalSpeed < -world.LocalPlayer.JumpSpeed);

        Assert.True(world.TryPrepareNetworkPlayerJoin(2));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(2, out var bluePlayer));
        bluePlayer.SetSpawnRoomState(false);
        PlacePlayerOnPad(bluePlayer, pad);
        Assert.True(world.TryApplyJumpPadJumpBoostForPrediction(bluePlayer, jumped: true));
        Assert.True(bluePlayer.VerticalSpeed < -bluePlayer.JumpSpeed);

        world.AdvanceOneTick();
        Assert.Contains(pad, world.JumpPads);
    }

    [Fact]
    public void RoundResetRecreatesMapNeutralJumpPads()
    {
        var world = CreateWorldWithNeutralJumpPad();
        var oldPad = Assert.Single(world.JumpPads);
        oldPad.TakeDamage(JumpPadEntity.MaxHealth);
        Assert.True(oldPad.IsDead);

        RestartCurrentRoundMethod.Invoke(world, [true, false]);

        var newPad = Assert.Single(world.JumpPads);
        Assert.NotSame(oldPad, newPad);
        Assert.True(newPad.IsNeutral);
        Assert.True(newPad.IsBuilt);
        Assert.True(newPad.HasLanded);
        Assert.Equal(JumpPadEntity.MaxHealth, newPad.Health);
    }

    private static SimulationWorld CreateWorldWithNeutralJumpPad()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var spawn = new SpawnPoint(256f, 256f);
        world.CombatTestSetLevel(new SimpleLevel(
            name: "neutral-jump-pad-test",
            mode: GameModeKind.TeamDeathmatch,
            bounds: new WorldBounds(512f, 512f),
            mapScale: 1f,
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [spawn],
            intelBases: [],
            roomObjects: [],
            floorY: 512f,
            solids: [],
            importedFromSource: false,
            jumpPadSpawns: [new JumpPadSpawnMarker(spawn.X, spawn.Y)]));
        return world;
    }

    private static PlayerEntity PlacePlayerOnPad(PlayerEntity player, JumpPadEntity pad)
    {
        player.TeleportTo(pad.X, pad.Y);
        player.ApplyVelocityImpulse(0f, -1f);
        return player;
    }
}
