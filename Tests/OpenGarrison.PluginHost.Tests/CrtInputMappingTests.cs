using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CrtInputMappingTests
{
    [Fact]
    public void CrtMaskPassesGameplayClicksOnlyForFinitePointsInsideTheOutput()
    {
        var borderOutputUv = new Vector2(0.02f, 0.5f);
        var croppedSourceUv = new Vector2(-0.08f, 0.5f);

        Assert.True(Game1.ShouldPassMaskedCrtPointerToGameplay(
            pointerVisible: false,
            outputUv: borderOutputUv,
            sourceUv: croppedSourceUv,
            gameplayInputAllowed: true));
        Assert.False(Game1.ShouldPassMaskedCrtPointerToGameplay(
            pointerVisible: false,
            outputUv: borderOutputUv,
            sourceUv: croppedSourceUv,
            gameplayInputAllowed: false));
        Assert.False(Game1.ShouldPassMaskedCrtPointerToGameplay(
            pointerVisible: true,
            outputUv: borderOutputUv,
            sourceUv: croppedSourceUv,
            gameplayInputAllowed: true));
        Assert.False(Game1.ShouldPassMaskedCrtPointerToGameplay(
            pointerVisible: false,
            outputUv: new Vector2(1.01f, 0.5f),
            sourceUv: croppedSourceUv,
            gameplayInputAllowed: true));
        Assert.False(Game1.ShouldPassMaskedCrtPointerToGameplay(
            pointerVisible: false,
            outputUv: borderOutputUv,
            sourceUv: new Vector2(float.NaN, 0.5f),
            gameplayInputAllowed: true));
    }

    [Fact]
    public void CrtGameplayBorderClickPassesWhileMenuBorderClickWaitsForRelease()
    {
        var outputUv = new Vector2(0.02f, 0.5f);
        var sourceUv = new Vector2(-0.08f, 0.5f);
        var gameplayPointerVisible = Game1.ShouldPassMaskedCrtPointerToGameplay(
            pointerVisible: false,
            outputUv: outputUv,
            sourceUv: sourceUv,
            gameplayInputAllowed: true);
        var suppressUntilRelease = false;

        Assert.Equal(ButtonState.Pressed, Game1.GetCrtAwareButtonState(
            ButtonState.Pressed,
            ref suppressUntilRelease,
            gameplayPointerVisible));
        Assert.False(suppressUntilRelease);

        var menuPointerVisible = Game1.ShouldPassMaskedCrtPointerToGameplay(
            pointerVisible: false,
            outputUv: outputUv,
            sourceUv: sourceUv,
            gameplayInputAllowed: false);
        Assert.Equal(ButtonState.Released, Game1.GetCrtAwareButtonState(
            ButtonState.Pressed,
            ref suppressUntilRelease,
            menuPointerVisible));
        Assert.True(suppressUntilRelease);
        Assert.Equal(ButtonState.Released, Game1.GetCrtAwareButtonState(
            ButtonState.Pressed,
            ref suppressUntilRelease,
            pointerVisible: true));
        Assert.Equal(ButtonState.Released, Game1.GetCrtAwareButtonState(
            ButtonState.Released,
            ref suppressUntilRelease,
            pointerVisible: true));
        Assert.False(suppressUntilRelease);
        Assert.Equal(ButtonState.Pressed, Game1.GetCrtAwareButtonState(
            ButtonState.Pressed,
            ref suppressUntilRelease,
            pointerVisible: true));
    }
}
