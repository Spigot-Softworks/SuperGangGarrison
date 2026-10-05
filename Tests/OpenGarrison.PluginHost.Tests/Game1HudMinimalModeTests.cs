using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class Game1HudMinimalModeTests
{
    [Theory]
    [InlineData("hud", "minimal", (int)GameplayHudVisibilityMode.Visible, (int)GameplayHudVisibilityMode.Minimal)]
    [InlineData("hud", "off", (int)GameplayHudVisibilityMode.Minimal, (int)GameplayHudVisibilityMode.Hidden)]
    [InlineData("hud", "on", (int)GameplayHudVisibilityMode.Hidden, (int)GameplayHudVisibilityMode.Visible)]
    [InlineData("hide_hud", "on", (int)GameplayHudVisibilityMode.Visible, (int)GameplayHudVisibilityMode.Hidden)]
    [InlineData("hide_hud", "off", (int)GameplayHudVisibilityMode.Hidden, (int)GameplayHudVisibilityMode.Visible)]
    public void HudConsoleCommandsResolveModeTransitions(
        string command,
        string option,
        int current,
        int expected)
    {
        Assert.True(GameplayHudVisibilityRules.TryApplyConsoleCommand(
            command,
            option,
            (GameplayHudVisibilityMode)current,
            out var actual));
        Assert.Equal((GameplayHudVisibilityMode)expected, actual);
    }

    [Fact]
    public void HudStatusAndToggleRespectMinimalMode()
    {
        Assert.Equal("hud is minimal", GameplayHudVisibilityRules.GetStatusText(GameplayHudVisibilityMode.Minimal));
        Assert.False(GameplayHudVisibilityRules.TryApplyConsoleCommand(
            "hud", "status", GameplayHudVisibilityMode.Minimal, out var unchanged));
        Assert.Equal(GameplayHudVisibilityMode.Minimal, unchanged);

        Assert.True(GameplayHudVisibilityRules.TryApplyConsoleCommand(
            "hud", "toggle", GameplayHudVisibilityMode.Minimal, out var toggledOff));
        Assert.Equal(GameplayHudVisibilityMode.Hidden, toggledOff);
        Assert.True(GameplayHudVisibilityRules.TryApplyConsoleCommand(
            "hud", "toggle", toggledOff, out var toggledOn));
        Assert.Equal(GameplayHudVisibilityMode.Visible, toggledOn);
        Assert.False(GameplayHudVisibilityRules.ShouldDrawBattleCursor(toggledOff, otherwiseEligible: true));
        Assert.True(GameplayHudVisibilityRules.ShouldDrawBattleCursor(GameplayHudVisibilityMode.Minimal, otherwiseEligible: true));
    }

    [Fact]
    public void MinimalModeShowsOnlyEligibleFloatingPlayerHealthBarsEvenWhenSettingIsOff()
    {
        Assert.True(GameplayHudVisibilityRules.ShouldDrawFloatingHealthBar(
            showHealthBarEnabled: false,
            mode: GameplayHudVisibilityMode.Minimal,
            forceHealthBar: false,
            visibilityAlpha: 1f,
            isLocalPlayer: false,
            sharesLocalPlayerTeam: true));

        Assert.False(GameplayHudVisibilityRules.ShouldDrawFloatingHealthBar(
            showHealthBarEnabled: false,
            mode: GameplayHudVisibilityMode.Minimal,
            forceHealthBar: false,
            visibilityAlpha: 1f,
            isLocalPlayer: false,
            sharesLocalPlayerTeam: false));
        Assert.False(GameplayHudVisibilityRules.ShouldDrawFloatingHealthBar(
            showHealthBarEnabled: false,
            mode: GameplayHudVisibilityMode.Minimal,
            forceHealthBar: false,
            visibilityAlpha: 0f,
            isLocalPlayer: false,
            sharesLocalPlayerTeam: true));
        Assert.True(GameplayHudVisibilityRules.ShouldDrawFloatingHealthBar(
            showHealthBarEnabled: false,
            mode: GameplayHudVisibilityMode.Minimal,
            forceHealthBar: true,
            visibilityAlpha: 1f,
            isLocalPlayer: false,
            sharesLocalPlayerTeam: false));
        Assert.False(GameplayHudVisibilityRules.ShouldDrawFloatingHealthBar(
            showHealthBarEnabled: true,
            mode: GameplayHudVisibilityMode.Hidden,
            forceHealthBar: true,
            visibilityAlpha: 1f,
            isLocalPlayer: true,
            sharesLocalPlayerTeam: true));

        Assert.False(GameplayHudVisibilityRules.ShouldDrawFloatingHealthBar(
            showHealthBarEnabled: false,
            mode: GameplayHudVisibilityMode.Visible,
            forceHealthBar: false,
            visibilityAlpha: 1f,
            isLocalPlayer: true,
            sharesLocalPlayerTeam: true));
        Assert.True(GameplayHudVisibilityRules.ShouldDrawFloatingHealthBar(
            showHealthBarEnabled: true,
            mode: GameplayHudVisibilityMode.Visible,
            forceHealthBar: false,
            visibilityAlpha: 1f,
            isLocalPlayer: true,
            sharesLocalPlayerTeam: true));
    }

    [Fact]
    public void FloatingHealthBarsMoveUpThreePixelsWithoutMovingShieldBars()
    {
        var bounds = new Rectangle(20, 80, 30, 40);

        Assert.Equal(63, Game1.GetPlayerMeterBarY(bounds, placeBelow: false));
        Assert.Equal(128, Game1.GetPlayerMeterBarY(bounds, placeBelow: true));
    }
}
