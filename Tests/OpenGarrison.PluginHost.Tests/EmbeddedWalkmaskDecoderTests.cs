using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class EmbeddedWalkmaskDecoderTests
{
    [Fact]
    public void TryGetDimensionsRejectsInvalidSection()
    {
        Assert.False(EmbeddedWalkmaskDecoder.TryGetDimensions(string.Empty, out var width, out var height));
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }
}
