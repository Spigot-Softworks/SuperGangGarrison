using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenGarrison.PluginHost.Tests;

public sealed class GameplayMessageLayoutTests
{
    [Theory]
    [InlineData("basic")]
    [InlineData("ltd")]
    [InlineData("notification")]
    [InlineData("notification2")]
    public void LocalPlacementFollowsBuilderLocationEvenWithCenterAlignmentAndLegacyScreenCoordinates(string style)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        typeof(Game1).GetField(nameof(Game1._gameplaySessionState))!.SetValue(game, new Game1.GameplaySessionState());
        var characters = Enumerable.Range(32, 95).Select(i => (char)i).ToList();
        // Measuring text needs font metrics, but no GPU texture or audio device.
        var font = new SpriteFont(null!, characters.Select(_ => new Rectangle(0, 0, 8, 12)).ToList(),
            characters.Select(_ => new Rectangle(0, 0, 8, 12)).ToList(), characters, 12, 0,
            characters.Select(_ => new Vector3(0, 8, 0)).ToList(), '?');
        game._consoleFont = font;
        var resolver = typeof(Game1).GetMethod("ResolveGameplayMessageBounds", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var marker = GameplayMessageMetadata.FromProperties(4500, 3000,
            new Dictionary<string, string>
            {
                ["style"] = style, ["align"] = "center", ["placement"] = "local",
                ["screenX"] = "0.5", ["screenY"] = "0.25", ["width"] = "160", ["height"] = "48",
            });
        Rectangle Resolve(Vector2 camera) => (Rectangle)resolver.Invoke(game,
            [marker, "Captured!", 2f, camera, new Rectangle(0, 0, 640, 480), 0f])!;
        var first = Resolve(new Vector2(4400, 2800));
        var second = Resolve(new Vector2(4000, 2400));
        // Default gameplay zoom is 1.125: 400 world units move 450 HUD pixels.
        Assert.Equal(450, second.X - first.X);
        Assert.Equal(450, second.Y - first.Y);
        Assert.True(first.Width < 640);
        Assert.False(Game1.IsGameplayMessageScreenCentered(marker));
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void GameplayBoundsStayCenteredWhenTwoPlayersViewDifferentMapLocations(int width, int height)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var resolver = typeof(Game1).GetMethod("ResolveGameplayMessageBounds", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var viewport = new Rectangle(0, 0, width, height);
        var marker = GameplayMessageMetadata.FromProperties(4500, 3000,
            new Dictionary<string, string> { ["align"] = "center", ["animation"] = "spin" });
        Rectangle Resolve(GameplayMessageMarker message, Vector2 camera) => (Rectangle)resolver.Invoke(game,
            [message, "Captured!", 2f, camera, viewport, 0f])!;
        var first = Resolve(marker, new Vector2(4400, 2800));
        var second = Resolve(marker, new Vector2(100, 200));
        var moved = GameplayMessageMetadata.FromProperties(9000, 8000,
            new Dictionary<string, string> { ["align"] = "center", ["animation"] = "spin" });
        Assert.Equal(first, second);
        Assert.Equal(first, Resolve(moved, new Vector2(100, 200)));
        Assert.Equal(viewport.Center.X, first.Center.X);
        Assert.Equal(viewport.Y + (int)MathF.Round(viewport.Height * 0.12f) + 15, first.Top);
        Assert.InRange(first.Top, 1, height / 4);
    }

    [Theory]
    [InlineData("basic", "center", true)]
    [InlineData("ltd", "left", true)]
    [InlineData("ltd", "right", true)]
    [InlineData("basic", "left", false)]
    public void CenterAlignmentAndLtdStyleUseScreenPlacement(string style, string align, bool centered)
    {
        var marker = GameplayMessageMetadata.FromProperties(4500, 3000,
            new Dictionary<string, string> { ["style"] = style, ["align"] = align });
        Assert.Equal(centered, Game1.IsGameplayMessageScreenCentered(marker));
    }

    [Theory]
    [InlineData(0, 0, 640, 480)]
    [InlineData(0, 0, 1920, 1080)]
    [InlineData(200, 100, 800, 600)]
    public void TopMiddleAnchorUsesEachViewportAndIgnoresMarkerAndCameraPosition(int x, int y, int width, int height)
    {
        var viewport = new Rectangle(x, y, width, height);
        var first = Game1.ResolveGameplayMessageCenteredBounds(new Rectangle(8000, 4000, 240, 32), viewport);
        var second = Game1.ResolveGameplayMessageCenteredBounds(new Rectangle(-900, -300, 240, 32), viewport);
        Assert.Equal(first, second);
        Assert.Equal(viewport.Center.X, first.Center.X);
        Assert.InRange(first.Top, y + 1, y + height / 4);
        Assert.Equal(240, first.Width);
        Assert.Equal(32, first.Height);
    }
}
