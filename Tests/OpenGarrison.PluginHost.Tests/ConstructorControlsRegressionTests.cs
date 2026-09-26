using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ConstructorControlsRegressionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void SkillAndMouse2ToggleBuildMenuWithoutUsingAbilityOrSecondary()
    {
        var (game, _) = CreateSession(BuildMenuStyle.List);
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.True(IsOpen(game));
        Assert.False(IsClosing(game));
        // Holding skill must not re-toggle until released.
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.True(IsOpen(game));
        Assert.False(IsClosing(game));
        Step(game, default);
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.True(IsClosing(game));
        FinishClosing(game);
        Assert.False(IsOpen(game));

        var m2 = PressedSecondaryMouse();
        Assert.False(Step(game, default, m2).FireSecondary);
        Assert.True(IsOpen(game));
        Assert.False(IsClosing(game));
        Assert.False(Step(game, default, m2).FireSecondary);
        Assert.True(IsOpen(game));
        Step(game, default);
        Assert.False(Step(game, default, m2).FireSecondary);
        Assert.True(IsClosing(game));
        FinishClosing(game);
        Assert.False(IsOpen(game));
    }

    [Fact]
    public void NumberKeysBuildOrDestroyMatchingStructuresAndCloseMenu()
    {
        var (game, world) = CreateSession(BuildMenuStyle.List);
        world.LocalPlayer.AddMetal(200f);
        Step(game, new(Keys.Space));
        Assert.True(IsOpen(game));

        var buildSentry = Step(game, new(Keys.D1));
        Assert.True(buildSentry.BuildSentry);
        Assert.True(IsClosing(game));
        world.SetLocalInput(buildSentry);
        world.AdvanceOneTick();
        Assert.Single(world.Sentries).ForceBuilt();
        FinishClosing(game);

        Step(game, new(Keys.Space));
        var destroySentry = Step(game, new(Keys.D1));
        Assert.True(destroySentry.DestroySentry);
        Assert.False(destroySentry.BuildSentry);
        world.SetLocalInput(destroySentry);
        world.AdvanceOneTick();
        Assert.Empty(world.Sentries);
        FinishClosing(game);

        world.LocalPlayer.AddMetal(200f);
        Step(game, new(Keys.Space));
        var buildDispenser = Step(game, new(Keys.D2));
        Assert.True(buildDispenser.BuildDispenser);
        world.SetLocalInput(buildDispenser);
        world.AdvanceOneTick();
        Assert.Contains(world.Sentries, sentry => sentry.IsDispenser);
        FinishClosing(game);

        world.LocalPlayer.AddMetal(200f);
        Step(game, new(Keys.Space));
        var buildJumpPad = Step(game, new(Keys.D3));
        Assert.True(buildJumpPad.BuildJumpPad);
        Assert.False(buildJumpPad.UseAbility);
        world.SetLocalInput(buildJumpPad);
        world.AdvanceOneTick();
        Assert.NotEmpty(world.JumpPads);
    }

    [Fact]
    public void InsufficientMetalKeepsMenuOpenAndDoesNotIssueBuildCommand()
    {
        var (game, world) = CreateSession(BuildMenuStyle.List);
        world.LocalPlayer.SpendMetal(world.LocalPlayer.Metal);
        Step(game, new(Keys.Space));
        Assert.True(IsOpen(game));

        var result = Step(game, new(Keys.D1));
        Assert.False(result.BuildSentry);
        Assert.False(result.DestroySentry);
        Assert.True(IsOpen(game));
        Assert.False(IsClosing(game));
    }

    [Fact]
    public void NumberFourClosesBuildMenuWithoutBuilding()
    {
        var (game, _) = CreateSession(BuildMenuStyle.List);
        Step(game, new(Keys.Space));
        Assert.True(IsOpen(game));
        var result = Step(game, new(Keys.D4));
        Assert.False(result.BuildSentry);
        Assert.False(result.BuildDispenser);
        Assert.False(result.UseAbility);
        Assert.True(IsClosing(game));
        FinishClosing(game);
        Assert.False(IsOpen(game));
    }

    [Fact]
    public void WheelSkillAndMouse2ToggleWithoutDirectSentryBuild()
    {
        var (game, _) = CreateSession(BuildMenuStyle.Wheel);
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.True(IsOpen(game));
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.True(IsOpen(game));
        Step(game, default);
        Assert.False(Step(game, new(Keys.Space)).UseAbility);
        Assert.False(IsOpen(game));

        var m2 = PressedSecondaryMouse();
        Assert.False(Step(game, default, m2).FireSecondary);
        Assert.True(IsOpen(game));
        Assert.False(Step(game, default, m2).FireSecondary);
        Assert.True(IsOpen(game));
        Step(game, default);
        Assert.False(Step(game, default, m2).FireSecondary);
        Assert.False(IsOpen(game));
    }

    [Fact]
    public void OtherClassesKeepTheirSpaceAbility()
    {
        var (game, world) = CreateSession(BuildMenuStyle.List);
        world.CompleteLocalPlayerJoin(PlayerClass.Scout);
        Assert.True(Step(game, new(Keys.Space)).UseAbility);
        Assert.False(IsOpen(game));
    }

    private static (Game1 Game, SimulationWorld World) CreateSession(BuildMenuStyle style)
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

        var settings = new ClientSettings { BuildMenuStyle = style };
        typeof(Game1).GetField("_clientSettings", Private)!.SetValue(game, settings);
        typeof(Game1).GetField("_clientUpdateElapsedSeconds", Private)!.SetValue(game, 1f / 30f);

        var engineerHud = typeof(Game1).GetNestedType("GameplayEngineerHudController", Private)!;
        typeof(Game1).GetField("_gameplayEngineerHudController", Private)!
            .SetValue(
                game,
                Activator.CreateInstance(
                    engineerHud,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: [game],
                    culture: null));

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

    private static MouseState PressedSecondaryMouse()
        => new(0, 0, 0, ButtonState.Released, ButtonState.Released,
            ButtonState.Pressed, ButtonState.Released, ButtonState.Released);

    private static bool IsOpen(Game1 game) => (bool)typeof(Game1).GetProperty("_buildMenuOpen", Private)!.GetValue(game)!;
    private static bool IsClosing(Game1 game) => (bool)typeof(Game1).GetProperty("_buildMenuClosing", Private)!.GetValue(game)!;

    private static void FinishClosing(Game1 game)
    {
        for (var i = 0; i < 16 && IsOpen(game); i++)
        {
            Step(game, default);
        }

        Assert.False(IsOpen(game));
    }

    private static void Set(Game1 game, string name, object value)
    {
        if (typeof(Game1).GetField(name, Private) is { } field) field.SetValue(game, value);
        else typeof(Game1).GetProperty(name, Private)!.SetValue(game, value);
    }
}
