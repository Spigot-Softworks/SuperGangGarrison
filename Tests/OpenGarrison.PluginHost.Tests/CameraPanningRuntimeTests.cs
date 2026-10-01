using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CameraPanningRuntimeTests
{
    [Fact]
    public void StationarySubpixelCorrectionsDoNotShakeCameraWithPanningDisabled()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var services = new ClientServiceContainer();
        SetField(game, "_services", services);
        var gameplayManager = new GameplayManager((IGameplayContext)game);
        services.Register(gameplayManager);
        gameplayManager.RuntimeSettings.CameraPanningEnabled = false;
        SetField(game, "_gameplayPresentationDeltaSeconds", 1f / 60f);
        var origin = new Vector2(400, 500);
        Assert.Equal(origin, game.AdvanceSmoothCameraTarget(origin, 0.5f));
        for (var frame = 1; frame <= 120; frame++)
        {
            SetField(game, "_networkInterpolationClockSeconds", frame / 60d);
            var jitter = new Vector2(frame % 2 == 0 ? 0.05f : -0.05f);
            Assert.Equal(origin, game.AdvanceSmoothCameraTarget(origin + jitter, 0.5f));
        }
        SetField(game, "_networkInterpolationClockSeconds", 3d);
        var moving = game.AdvanceSmoothCameraTarget(origin + new Vector2(20, 0), 0.5f);
        Assert.True(moving.X > origin.X);
        Assert.True(moving.X < origin.X + 20);
        Assert.Equal(origin.Y, moving.Y);
    }

    private static void SetField(Game1 game, string name, object value)
    {
        var field = typeof(Game1).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(game, value);
    }
}
