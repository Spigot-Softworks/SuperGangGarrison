using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class WindowInputFocusTests
{
    private static MouseState Mouse(int x = 10, int wheel = 0, ButtonState button = ButtonState.Released) =>
        new(x, 20, wheel, button, button, button, button, button);

    [Fact]
    public void BackgroundInputAndActivationClickStayReleasedUntilFreshPress()
    {
        var filter = new WindowInputFilter();
        var keyboard = new KeyboardState();
        var mouse = Mouse();
        filter.Filter(true, ref keyboard, ref mouse);
        keyboard = new KeyboardState(Keys.D);
        mouse = Mouse(40, 120, ButtonState.Pressed);
        filter.Filter(true, ref keyboard, ref mouse);
        Assert.True(keyboard.IsKeyDown(Keys.D));
        Assert.Equal(ButtonState.Pressed, mouse.LeftButton);
        for (var frame = 0; frame < 5; frame++)
        {
            keyboard = new KeyboardState(Keys.D);
            mouse = Mouse(400, 1200, ButtonState.Pressed);
            filter.Filter(false, ref keyboard, ref mouse);
            Assert.Empty(keyboard.GetPressedKeys());
            Assert.Equal(Mouse(40, 120), mouse);
        }
        for (var frame = 0; frame < 5; frame++)
        {
            keyboard = new KeyboardState(Keys.D);
            mouse = Mouse(50, 1200, ButtonState.Pressed);
            filter.Filter(true, ref keyboard, ref mouse);
            Assert.Empty(keyboard.GetPressedKeys());
            Assert.Equal(Mouse(50, 120), mouse);
        }
        // A newly pressed unrelated key works while the old key remains held.
        keyboard = new KeyboardState(Keys.D, Keys.W);
        mouse = Mouse(50, 1200);
        filter.Filter(true, ref keyboard, ref mouse);
        Assert.False(keyboard.IsKeyDown(Keys.D));
        Assert.True(keyboard.IsKeyDown(Keys.W));
        keyboard = default;
        mouse = Mouse(50, 1200);
        filter.Filter(true, ref keyboard, ref mouse);
        keyboard = new KeyboardState(Keys.D);
        mouse = Mouse(60, 1320, ButtonState.Pressed);
        filter.Filter(true, ref keyboard, ref mouse);
        Assert.True(keyboard.IsKeyDown(Keys.D));
        Assert.Equal(ButtonState.Pressed, mouse.LeftButton);
        Assert.Equal(240, mouse.ScrollWheelValue);
    }

    [Fact]
    public void FocusLostAndRegainedBetweenFramesStillBlocksHeldControls()
    {
        var filter = new WindowInputFilter();
        var keyboard = new KeyboardState();
        var mouse = Mouse();
        filter.Filter(true, ref keyboard, ref mouse);
        filter.LoseFocus();
        keyboard = new KeyboardState(Keys.Space);
        mouse = Mouse(button: ButtonState.Pressed);
        filter.Filter(true, ref keyboard, ref mouse);
        Assert.Empty(keyboard.GetPressedKeys());
        Assert.Equal(ButtonState.Released, mouse.LeftButton);
    }

    [Fact]
    public void ControllerWaitsForNeutralAfterFocusLoss()
    {
        var filter = new WindowInputFilter();
        var held = new GamePadState(Vector2.UnitX, Vector2.Zero, 0, 1, Buttons.A);
        Assert.Equal(default, filter.FilterController(false, held, true));
        Assert.Equal(default, filter.FilterController(true, held, true));
        filter.FilterController(true, default, false);
        Assert.Equal(held, filter.FilterController(true, held, true));
        filter.LoseFocus();
        Assert.Equal(default, filter.FilterController(true, held, true));
    }

    [Fact]
    public void FocusLossClearsQueuedGameplayActionsBeforeNextTick()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        void Set(string field, object value) => typeof(Game1).GetField(field, flags)!.SetValue(game, value);
        object Get(string field) => typeof(Game1).GetField(field, flags)!.GetValue(game)!;
        Set("_world", new SimulationWorld(new SimulationConfig { EnableLocalDummies = false }));
        var visuals = typeof(Game1).GetField("_predictedWeaponFireVisuals", flags)!;
        visuals.SetValue(game, Activator.CreateInstance(visuals.FieldType));
        foreach (var field in new[] { "_pendingPredictedPrimaryPress", "_pendingPredictedJumpPress", "_pendingImmediateWeaponFirePresentation", "_pendingOfflineBuildSentry" })
            Set(field, true);
        Set("_pendingPredictedBuildSentryTicksRemaining", 4);
        Set("_latchedJumpPressSequence", 12u);
        Set("_latestPredictedLocalInput", default(PlayerInputSnapshot) with { FirePrimary = true, Up = true });
        typeof(Game1).GetMethod("ReleaseGameplayInputForFocusLoss", flags)!.Invoke(game, null);
        foreach (var field in new[] { "_pendingPredictedPrimaryPress", "_pendingPredictedJumpPress", "_pendingImmediateWeaponFirePresentation", "_pendingOfflineBuildSentry" })
            Assert.False((bool)Get(field));
        Assert.Equal(0, Get("_pendingPredictedBuildSentryTicksRemaining"));
        Assert.Equal(0u, Get("_latchedJumpPressSequence"));
        Assert.Equal(default(PlayerInputSnapshot), Get("_latestPredictedLocalInput"));
    }

    [Fact]
    public void BrowserDropsLateBackgroundEventsAndQueuedScroll()
    {
        BrowserInputBridge.SetFocus(true);
        BrowserInputBridge.BeginFrame();
        var before = BrowserInputBridge.GetMouseState();
        BrowserInputBridge.AddWheelDelta(120);
        BrowserInputBridge.SetFocus(false);
        BrowserInputBridge.SetKey(Keys.W, true);
        BrowserInputBridge.SetMouseButton(0, true);
        BrowserInputBridge.AddWheelDelta(120);
        BrowserInputBridge.EnqueueTextInput('x');
        BrowserInputBridge.SetFocus(true);
        BrowserInputBridge.BeginFrame();
        Assert.Empty(BrowserInputBridge.GetKeyboardState().GetPressedKeys());
        Assert.Empty(BrowserInputBridge.DrainTextInput());
        Assert.Equal(ButtonState.Released, BrowserInputBridge.GetMouseState().LeftButton);
        Assert.Equal(before.ScrollWheelValue, BrowserInputBridge.GetMouseState().ScrollWheelValue);
        BrowserInputBridge.SetFocus(false);
    }
}
