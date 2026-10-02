using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SubpixelWorldPresentationTests
{
    private const float Zoom = 1.125f;
    private static readonly Vector2 TargetScale = new(2f, 2f);

    [Fact]
    public void SplitCameraReturnsWholePixelCameraAndResidualInUnitRange()
    {
        var drawCamera = SubpixelWorldPresentation.SplitCamera(new Vector2(120.75f, -3.25f), out var residual);

        Assert.Equal(new Vector2(120f, -4f), drawCamera);
        Assert.Equal(0.75f, residual.X, 5);
        Assert.Equal(0.75f, residual.Y, 5);
    }

    [Fact]
    public void LegacyPassUsesZoomOnlyTransform()
    {
        var presentation = new SubpixelWorldPresentation();
        presentation.SetCameraResidual(new Vector2(0.5f, 0.25f));
        presentation.BeginWorldPass(useSubpixel: false, TargetScale, rendersToWorldTarget: true);

        Assert.Equal(Matrix.CreateScale(Zoom, Zoom, 1f), presentation.GetWorldTransform(Zoom));
        Assert.False(presentation.RendersToWorldTarget);
        Assert.Equal(Vector2.Zero, presentation.GetHudOffset(Zoom));
    }

    [Fact]
    public void WalkingScrollAdvancesTheBackgroundEvenlyAtPresentationResolution()
    {
        // ~3.45 world px per 60 Hz frame (a Soldier at full run). With a
        // whole-pixel camera this scrolls 6/10/6/10 screen px per frame.
        const float worldPixelsPerFrame = 3.45f;
        var presentation = new SubpixelWorldPresentation();
        var steps = new List<int>();
        int? previousScreenX = null;
        for (var frame = 0; frame < 60; frame += 1)
        {
            var exactCamera = new Vector2(100f + (frame * worldPixelsPerFrame), 0f);
            var drawCamera = SubpixelWorldPresentation.SplitCamera(exactCamera, out var residual);
            presentation.SetCameraResidual(residual);
            presentation.BeginWorldPass(useSubpixel: true, TargetScale, rendersToWorldTarget: true);

            // A map pixel at world x = 0 is drawn at (0 - drawCamera) and rasterized to a whole screen pixel.
            var screen = Vector2.Transform(new Vector2(-drawCamera.X, 0f), presentation.GetWorldTransform(Zoom));
            var screenX = (int)MathF.Round(screen.X);
            if (previousScreenX.HasValue)
            {
                steps.Add(previousScreenX.Value - screenX);
            }

            previousScreenX = screenX;
            presentation.EndWorldPass();
        }

        var ideal = worldPixelsPerFrame * Zoom * TargetScale.X;
        Assert.All(steps, step => Assert.InRange(step, (int)MathF.Floor(ideal), (int)MathF.Ceiling(ideal)));
    }

    [Fact]
    public void LocalPlayerResidualKeepsThePlayerFixedRelativeToTheCamera()
    {
        var presentation = new SubpixelWorldPresentation();
        var halfView = new Vector2(426.5f, 240f);
        for (var frame = 0; frame < 40; frame += 1)
        {
            var player = new Vector2(500.3f + (frame * 3.45f), 300.6f + (frame * 0.7f));
            var drawCamera = SubpixelWorldPresentation.SplitCamera(player - halfView, out var residual);
            presentation.SetCameraResidual(residual);
            presentation.BeginWorldPass(useSubpixel: true, Vector2.One, rendersToWorldTarget: false);
            presentation.SetObjectResidual(SubpixelWorldPresentation.GetObjectResidual(player));

            // Player sprites are drawn at round(render - camera), as GetPlayerSpriteScreenOrigin does.
            var spriteOrigin = new Vector2(
                MathF.Round(player.X - drawCamera.X, MidpointRounding.AwayFromZero),
                MathF.Round(player.Y - drawCamera.Y, MidpointRounding.AwayFromZero));
            var drawn = Vector2.Transform(spriteOrigin, presentation.GetWorldTransform(1f));

            Assert.Equal(halfView.X, drawn.X, 3);
            Assert.Equal(halfView.Y, drawn.Y, 3);
            presentation.EndWorldPass();
        }
    }

    [Fact]
    public void ObjectResidualIsIgnoredOutsideASubpixelPass()
    {
        var presentation = new SubpixelWorldPresentation();
        presentation.SetObjectResidual(new Vector2(0.4f, 0.2f));

        Assert.Equal(Vector2.Zero, presentation.ObjectResidual);
    }

    [Fact]
    public void ScissorRectanglesMapThroughTheWorldTransform()
    {
        var presentation = new SubpixelWorldPresentation();
        presentation.BeginWorldPass(useSubpixel: false, Vector2.One, rendersToWorldTarget: false);
        Assert.Equal(new Rectangle(9, 18, 12, 3), presentation.TransformWorldRectangle(new Rectangle(8, 16, 10, 2), Zoom));

        presentation.SetCameraResidual(new Vector2(0.5f, 0f));
        presentation.BeginWorldPass(useSubpixel: true, TargetScale, rendersToWorldTarget: true);
        Assert.Equal(new Rectangle(16, 36, 24, 5), presentation.TransformWorldRectangle(new Rectangle(8, 16, 10, 2), Zoom));
    }

    [Fact]
    public void HudOffsetFollowsTheCameraResidualOnlyAfterASubpixelPass()
    {
        var presentation = new SubpixelWorldPresentation();
        presentation.SetCameraResidual(new Vector2(0.5f, 0.25f));
        Assert.Equal(Vector2.Zero, presentation.GetHudOffset(Zoom));

        presentation.BeginWorldPass(useSubpixel: true, TargetScale, rendersToWorldTarget: true);
        presentation.EndWorldPass();

        var offset = presentation.GetHudOffset(Zoom);
        Assert.Equal(-0.5625f, offset.X, 5);
        Assert.Equal(-0.28125f, offset.Y, 5);
    }
}
