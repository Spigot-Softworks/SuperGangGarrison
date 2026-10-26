using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ConquestPointHudLayoutTests
{
    [Theory]
    [InlineData(3, 800, 600)]
    [InlineData(4, 800, 600)]
    [InlineData(12, 800, 600)]
    [InlineData(20, 640, 480)]
    [InlineData(300, 640, 480)]
    [InlineData(300, 1920, 1080)]
    public void AllPointsFitInsideTheViewportAndWrapAboveTheOriginalAnchor(int count, int width, int height)
    {
        var origin = new Vector2(width / 2f, height - 40f);
        var layout = ConquestPointHudLayout.Create(count, origin, width, height, 1f);
        Assert.InRange(layout.Scale, 0.001f, 1f);
        var positions = Enumerable.Range(0, count).Select(layout.GetPosition).ToArray();
        Assert.Equal(count, positions.Distinct().Count());
        Assert.All(positions, position =>
        {
            Assert.InRange(position.X, 16f + 24f * layout.Scale, width - 16f - 24f * layout.Scale);
            Assert.InRange(position.Y, 16f + 24f * layout.Scale, origin.Y);
        });
        Assert.Equal(origin.Y, positions[^1].Y);
        Assert.True(layout.Bounds.Height <= 181);
        if (count == 3)
        {
            Assert.Equal(1f, layout.Scale);
            Assert.Equal(new Vector2(origin.X - 60, origin.Y), positions[0]);
            Assert.Equal(origin, positions[1]);
            Assert.Equal(new Vector2(origin.X + 60, origin.Y), positions[2]);
        }
    }
}
