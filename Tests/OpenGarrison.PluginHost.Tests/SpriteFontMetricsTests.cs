using System.Collections;
using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SpriteFontMetricsTests
{
    [Fact]
    public void RepeatedLayoutMeasurementsReuseGlyphExtentsAndRetainFontSpacing()
    {
        var frames = new CountingFrames([
            new LoadedSpriteFrame(null!, new Rectangle(0, 0, 5, 9)),
            new LoadedSpriteFrame(null!, new Rectangle(0, 0, 7, 11))]);
        var sprite = new LoadedGameMakerSprite(frames, Point.Zero);
        var definition = new Game1.SpriteFontDefinition("font", '!', false, 0);
        Assert.Equal(new Game1.SpriteFontMetrics(7, 11), Game1.GetSpriteFontMetrics(definition, sprite));
        var reads = frames.Reads;
        for (var i = 0; i < 1000; i++)
            Assert.Equal(new Game1.SpriteFontMetrics(9, 11),
                Game1.GetSpriteFontMetrics(definition with { CharacterSeparator = 2 }, sprite));
        Assert.Equal(reads, frames.Reads);

        var reloaded = new LoadedGameMakerSprite([new LoadedSpriteFrame(null!, new Rectangle(0, 0, 12, 16))], Point.Zero);
        Assert.Equal(new Game1.SpriteFontMetrics(12, 16), Game1.GetSpriteFontMetrics(definition, reloaded));
    }

    private sealed class CountingFrames(LoadedSpriteFrame[] frames) : IReadOnlyList<LoadedSpriteFrame>
    {
        public int Reads { get; private set; }
        public int Count => frames.Length;
        public LoadedSpriteFrame this[int index] { get { Reads++; return frames[index]; } }
        public IEnumerator<LoadedSpriteFrame> GetEnumerator() => ((IEnumerable<LoadedSpriteFrame>)frames).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
