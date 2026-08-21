using OpenGarrison.Core;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class BuilderWindowCacheRegressionTests
{
    [Theory]
    [InlineData("logicPlayerTrigger")]
    [InlineData("logicDamageable")]
    [InlineData("logicArea")]
    public void CenterAnchoredBuilderBoundsUseEntityCenter(string entityType)
    {
        var origin = Game1.ResolveGarrisonBuilderAnchorSizedBoundsOrigin(
            entityType,
            x: 100f,
            y: 80f,
            width: 42f,
            height: 24f);

        Assert.Equal(79f, origin.Left);
        Assert.Equal(68f, origin.Top);
    }

    [Fact]
    public void TopLeftAnchoredBuilderBoundsKeepEntityOrigin()
    {
        var origin = Game1.ResolveGarrisonBuilderAnchorSizedBoundsOrigin(
            "teleport",
            x: 100f,
            y: 80f,
            width: 42f,
            height: 24f);

        Assert.Equal(100f, origin.Left);
        Assert.Equal(80f, origin.Top);
    }

    [Theory]
    [InlineData("logicPlayerTrigger", 4, 140f, 80f, 79f, 68f, 61f, 24f)]
    [InlineData("logicPlayerTrigger", 1, 60f, 50f, 60f, 50f, 61f, 42f)]
    [InlineData("logicDamageable", 4, 140f, 80f, 79f, 68f, 61f, 24f)]
    [InlineData("logicDamageable", 7, 60f, 110f, 60f, 68f, 61f, 42f)]
    public void CenterAnchoredResizePreservesOppositeEdgesAndUpdatesCenterAndScale(
        string entityType,
        int handleValue,
        float dragX,
        float dragY,
        float expectedLeft,
        float expectedTop,
        float expectedWidth,
        float expectedHeight)
    {
        var handle = (Game1.GarrisonBuilderResizeHandle)handleValue;
        var bounds = Game1.ResolveGarrisonBuilderResizeDragBounds(
            handle,
            startLeft: 79f,
            startTop: 68f,
            startWidth: 42f,
            startHeight: 24f,
            dragX: dragX,
            dragY: dragY);

        Assert.Equal(expectedLeft, bounds.Left);
        Assert.Equal(expectedTop, bounds.Top);
        Assert.Equal(expectedWidth, bounds.Width);
        Assert.Equal(expectedHeight, bounds.Height);

        var placement = Game1.ResolveGarrisonBuilderResizePlacement(
            entityType,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            metricsWidth: 42f,
            metricsHeight: 24f,
            originX: 0f,
            originY: 0f);

        Assert.Equal(expectedLeft + (expectedWidth * 0.5f), placement.X);
        Assert.Equal(expectedTop + (expectedHeight * 0.5f), placement.Y);
        Assert.Equal(expectedWidth / 42f, placement.XScale);
        Assert.Equal(expectedHeight / 24f, placement.YScale);
    }

    [Theory]
    [InlineData(DisplayModeKind.Fullscreen, DisplayModeKind.Fullscreen, true)]
    [InlineData(DisplayModeKind.Borderless, DisplayModeKind.Borderless, true)]
    [InlineData(DisplayModeKind.BorderlessWindow, DisplayModeKind.BorderlessWindow, false)]
    public void BuilderDialogDisplayModeTransitionPreservesPriorMode(
        DisplayModeKind activeMode,
        DisplayModeKind requestedMode,
        bool expectedTemporaryWindowed)
    {
        var transition = Game1.ResolveGarrisonBuilderDialogDisplayMode(activeMode, requestedMode);

        Assert.Equal(activeMode, transition.NormalizedMode);
        Assert.Equal(requestedMode, transition.RequestedMode);
        Assert.Equal(expectedTemporaryWindowed, transition.TemporarilyWindowed);
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, true)]
    public void FullscreenToggleDefersDuringStartupOrLoading(
        bool startupSplashOpen,
        bool loadingOverlayVisible,
        bool contentBootstrapComplete,
        bool loadingPresentationPending)
    {
        Assert.True(Game1.ShouldDeferFullscreenToggle(
            startupSplashOpen,
            loadingOverlayVisible,
            contentBootstrapComplete,
            loadingPresentationPending));
    }

    [Fact]
    public void FullscreenToggleIsAllowedAfterLoadingPresentationCompletes()
    {
        Assert.False(Game1.ShouldDeferFullscreenToggle(
            startupSplashOpen: false,
            loadingOverlayVisible: false,
            contentBootstrapComplete: true,
            loadingPresentationPending: false));
    }
}
