using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LegacyGg2LeanPoseTests
{
    [Theory]
    [InlineData(true, true, true, true, 0)]
    [InlineData(false, false, false, false, 0)]
    [InlineData(true, true, false, false, 2)]
    [InlineData(false, false, true, true, 1)]
    [InlineData(true, false, true, true, 0)]
    [InlineData(true, true, false, true, 0)]
    public void OnlyAnEdgeWithSupportOnTheOtherSideCanLean(
        bool nearLeftSupported,
        bool farLeftSupported,
        bool nearRightSupported,
        bool farRightSupported,
        int expected)
    {
        Assert.Equal(expected, (int)LegacyGg2LeanPose.Resolve(
            nearLeftSupported,
            farLeftSupported,
            nearRightSupported,
            farRightSupported));
    }
}
