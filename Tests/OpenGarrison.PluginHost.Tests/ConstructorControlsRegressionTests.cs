using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ConstructorControlsRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void SpaceOpensAndClosesWheelWithoutUsingJumpPadAbility()
    {
        var (game, _) = CreateSession();
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.True(IsOpen(game));
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.True(IsOpen(game));
        Assert.False(Step(game, default).UseAbility);
        Assert.True(IsOpen(game));
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.False(IsOpen(game));
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.False(IsOpen(game));
    }

    [Fact]
    public void M2BuildsThenDestroysSentryAndNeverOpensWheel()
    {
        var (game, world) = CreateSession();
        var m2 = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released,
            ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
        world.SetLocalInput(Step(game, default, m2));
        world.AdvanceOneTick();
        Assert.Single(world.Sentries).ForceBuilt();
        Assert.False(IsOpen(game));
        world.SetLocalInput(Step(game, default));
        world.AdvanceOneTick();
        world.SetLocalInput(Step(game, default, m2));
        world.AdvanceOneTick();
        Assert.Empty(world.Sentries);
        Assert.False(IsOpen(game));
    }

    [Fact]
    public void BuildingFromWheelDoesNotChangeNextM2OrSpaceAction()
    {
        var (game, world) = CreateSession();
        Step(game, new(Keys.Space));
        var selection = Step(game, new(Keys.Space, Keys.D1));
        Assert.True(selection.BuildSentry);
        Assert.False(selection.UseAbility);
        Assert.False(IsOpen(game));
        world.SetLocalInput(selection);
        world.AdvanceOneTick();
        Assert.Single(world.Sentries).ForceBuilt();
        // Continuing to hold Space after selecting must not build a jump pad or reopen the wheel.
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.False(IsOpen(game));
        Step(game, default);
        Step(game, new(Keys.Space));
        Assert.True(IsOpen(game));
        var m2 = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released,
            ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
        var direct = Step(game, default, m2);
        Assert.False(IsOpen(game));
        Assert.True(direct.FireSecondary);
        Assert.False(direct.BuildSentry);
        world.SetLocalInput(direct);
        world.AdvanceOneTick();
        Assert.Empty(world.Sentries);
        Assert.Empty(world.JumpPads);
    }

    [Fact]
    public void OtherClassesKeepTheirSpaceAbility()
    {
        var (game, world) = CreateSession();
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        Assert.True(Step(game, new(Keys.Space)).UseAbility);
        Assert.False(IsOpen(game));
    }

    private static (Game1 Game, SimulationWorld World) CreateSession()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        var level = new SimpleLevel("constructor_input_test", GameModeKind.TeamDeathmatch,
            new WorldBounds(1024f, 768f), 1f, null, 1, 1, new SpawnPoint(500f, 676f),
            [new SpawnPoint(100f, 676f)], [new SpawnPoint(900f, 676f)], [], [], 700f, [], false);
        typeof(SimulationWorld).GetMethod("CombatTestSetLevel", Private)!.Invoke(world, [level]);
        world.TeleportLocalPlayer(500f, 676f);
        world.LocalPlayer.SetSpawnRoomState(false);
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        Set(game, "_world", world);
        foreach (var name in new[] { "_uiShellState", "_gameplaySessionState", "_networkClient",
                     "_hudLayoutProfile", "_hudResolvedElements", "_hudElementLastBounds" })
        {
            var field = typeof(Game1).GetField(name, Private)!;
            field.SetValue(game, Activator.CreateInstance(field.FieldType, true));
        }
        var controller = typeof(Game1).GetField("_gameplayOverlayController", Private)!;
        controller.SetValue(game, Activator.CreateInstance(controller.FieldType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [game], null));
        Set(game, "_mainMenuOpen", false);
        Set(game, "_teamSelectOpen", false);
        Set(game, "_classSelectOpen", false);
        return (game, world);
    }

    private static PlayerInputSnapshot Step(Game1 game, KeyboardState keyboard, MouseState mouse = default)
    {
        var input = KeyboardInputMapper.BuildGameplaySnapshot(new InputBindingsSettings(), keyboard, mouse, 0, 0, 0, 0);
        typeof(Game1).GetMethod("UpdateBuildMenuState", Private)!.Invoke(game, [keyboard, mouse, input]);
        var result = (PlayerInputSnapshot)typeof(Game1).GetMethod("ApplyBuildMenuInputSelection", Private)!.Invoke(game, [input])!;
        Set(game, "_previousKeyboard", keyboard);
        Set(game, "_previousMouse", mouse);
        return result;
    }

    private static bool IsOpen(Game1 game) => (bool)typeof(Game1).GetProperty("_buildMenuOpen", Private)!.GetValue(game)!;
    private static void Set(Game1 game, string name, object value)
    {
        if (typeof(Game1).GetField(name, Private) is { } field) field.SetValue(game, value);
        else typeof(Game1).GetProperty(name, Private)!.SetValue(game, value);
    }
}
