using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SpriteBatchQuadViewportVisibilityTests
{
    [Fact]
    public void KeepsEverySpriteBatchQuadThatTouchesTheViewportAfterWorldTransform()
    {
        const int viewportWidth = 320;
        const int viewportHeight = 180;
        var viewport = new Viewport(0, 0, viewportWidth, viewportHeight);
        var worldTransform = Matrix.CreateTranslation(-0.7f, -0.3f, 0f)
            * Matrix.CreateScale(2.25f, 2.25f, 1f);
        var cases = new[]
        {
            (Position: new Vector2(-10f, 14f), Size: new Point(32, 24), Origin: new Vector2(5f, 8f), Scale: new Vector2(1.2f, 0.8f), Rotation: 0.61f),
            (Position: new Vector2(142f, 79f), Size: new Point(25, 19), Origin: new Vector2(12f, 9f), Scale: new Vector2(-1.1f, 1.3f), Rotation: -0.42f),
            (Position: new Vector2(142f, 75f), Size: new Point(64, 40), Origin: new Vector2(31f, 17f), Scale: new Vector2(0.75f, -1.25f), Rotation: 1.47f),
            (Position: new Vector2(5f, 74f), Size: new Point(16, 30), Origin: Vector2.Zero, Scale: new Vector2(1f, 1f), Rotation: 0f),
        };

        foreach (var item in cases)
        {
            var corners = GetSpriteBatchCorners(
                item.Position,
                item.Size.X,
                item.Size.Y,
                item.Rotation,
                item.Origin,
                item.Scale,
                worldTransform);
            Assert.True(ReferenceAabbIntersects(corners, viewportWidth, viewportHeight));

            Assert.True(SpriteBatchQuadViewportVisibility.Intersects(
                item.Position,
                item.Size.X,
                item.Size.Y,
                item.Rotation,
                item.Origin,
                item.Scale,
                worldTransform,
                viewport));
        }
    }

    [Fact]
    public void RejectsQuadsMoreThanSafetyMarginOutsideViewportAndKeepsEdgeCases()
    {
        var viewport = new Viewport(0, 0, 100, 80);

        Assert.False(SpriteBatchQuadViewportVisibility.Intersects(
            new Vector2(-27f, 12f), 24, 18, 0f, Vector2.Zero, Vector2.One, null, viewport));
        Assert.True(SpriteBatchQuadViewportVisibility.Intersects(
            new Vector2(-25f, 12f), 24, 18, 0f, Vector2.Zero, Vector2.One, null, viewport));
        Assert.False(SpriteBatchQuadViewportVisibility.Intersects(
            new Vector2(103f, 12f), 24, 18, 0f, Vector2.Zero, Vector2.One, null, viewport));
        Assert.True(SpriteBatchQuadViewportVisibility.Intersects(
            new Vector2(101f, 12f), 24, 18, 0f, Vector2.Zero, Vector2.One, null, viewport));
    }

    [Fact]
    public void UsesViewportLocalCoordinatesWhenViewportHasAnOffset()
    {
        var offsetViewport = new Viewport(70, 45, 100, 80);
        var localViewport = new Viewport(0, 0, 100, 80);
        var args = (Position: new Vector2(-40f, 12f), Width: 24, Height: 18);

        Assert.Equal(
            SpriteBatchQuadViewportVisibility.Intersects(
                args.Position, args.Width, args.Height, 0f, Vector2.Zero, Vector2.One, null, localViewport),
            SpriteBatchQuadViewportVisibility.Intersects(
                args.Position, args.Width, args.Height, 0f, Vector2.Zero, Vector2.One, null, offsetViewport));
    }

    [Fact]
    public void FallsBackToVisibleForNonFiniteOrInvalidInputs()
    {
        var viewport = new Viewport(0, 0, 100, 80);
        Assert.True(SpriteBatchQuadViewportVisibility.Intersects(
            new Vector2(float.NaN, 0f), 24, 18, 0f, Vector2.Zero, Vector2.One, null, viewport));
        Assert.True(SpriteBatchQuadViewportVisibility.Intersects(
            Vector2.Zero, 24, 18, float.PositiveInfinity, Vector2.Zero, Vector2.One, null, viewport));

        var invalidTransform = Matrix.Identity;
        invalidTransform.M41 = float.NaN;
        Assert.True(SpriteBatchQuadViewportVisibility.Intersects(
            new Vector2(-1000f, 0f), 24, 18, 0f, Vector2.Zero, Vector2.One, invalidTransform, viewport));

        // Vector2.Transform ignores W and therefore cannot bound SpriteBatch's
        // projectively transformed quad; unknown transforms must fail open.
        var projectiveTransform = Matrix.Identity;
        projectiveTransform.M14 = 0.01f;
        Assert.True(SpriteBatchQuadViewportVisibility.Intersects(
            new Vector2(-1000f, 0f), 24, 18, 0f, Vector2.Zero, Vector2.One, projectiveTransform, viewport));

        Assert.True(SpriteBatchQuadViewportVisibility.Intersects(
            new Vector2(-1000f, 0f), 24, 18, 0f, Vector2.Zero, Vector2.One, null, new Viewport(0, 0, 0, 0)));
    }

    [Fact]
    public void RandomizedSpriteBatchCornerReferenceNeverCullsAnIntersectingQuad()
    {
        const int viewportWidth = 320;
        const int viewportHeight = 180;
        var viewport = new Viewport(0, 0, viewportWidth, viewportHeight);
        var random = new Random(0x51B47);
        var visibleCount = 0;
        var safelyOutsideCount = 0;

        for (var index = 0; index < 500; index += 1)
        {
            var size = new Point(random.Next(1, 90), random.Next(1, 70));
            var position = new Vector2(random.Next(-100, 420), random.Next(-90, 260));
            var origin = new Vector2(random.Next(-15, size.X + 16), random.Next(-15, size.Y + 16));
            var scale = new Vector2(NextScale(random), NextScale(random));
            var rotation = ((float)random.NextDouble() * 2f - 1f) * MathF.PI;
            var transform = Matrix.CreateRotationZ(((float)random.NextDouble() * 2f - 1f) * 0.2f)
                * Matrix.CreateTranslation(
                    (float)random.NextDouble() * 1.5f - 0.75f,
                    (float)random.NextDouble() * 1.5f - 0.75f,
                    0f)
                * Matrix.CreateScale(1.5f, 1.5f, 1f);

            var corners = GetSpriteBatchCorners(position, size.X, size.Y, rotation, origin, scale, transform);
            var result = SpriteBatchQuadViewportVisibility.Intersects(
                position, size.X, size.Y, rotation, origin, scale, transform, viewport);

            if (ReferenceAabbIntersects(corners, viewportWidth, viewportHeight))
            {
                visibleCount += 1;
                Assert.True(result, $"Visible reference quad was rejected at deterministic sample {index}.");
            }

            if (ReferenceAabbIsBeyondSafetyMargin(corners, viewportWidth, viewportHeight))
            {
                safelyOutsideCount += 1;
                Assert.False(result, $"Clearly offscreen reference quad was retained at deterministic sample {index}.");
            }
        }

        Assert.True(visibleCount > 0);
        Assert.True(safelyOutsideCount > 0);
    }

    private static float NextScale(Random random)
    {
        var magnitude = 0.25f + ((float)random.NextDouble() * 2.25f);
        return random.Next(2) == 0 ? magnitude : -magnitude;
    }

    // SpriteBatch scales origin and destination dimensions first, rotates each
    // corner around position, then SpriteEffect applies the batch transform.
    private static Vector2[] GetSpriteBatchCorners(
        Vector2 position,
        int width,
        int height,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        Matrix transform)
    {
        var sin = MathF.Sin(rotation);
        var cos = MathF.Cos(rotation);
        var left = -origin.X * scale.X;
        var top = -origin.Y * scale.Y;
        var right = left + (width * scale.X);
        var bottom = top + (height * scale.Y);

        return new[]
        {
            Transform(left, top),
            Transform(right, top),
            Transform(left, bottom),
            Transform(right, bottom),
        };

        Vector2 Transform(float x, float y)
        {
            var rotated = new Vector2(
                position.X + (x * cos) - (y * sin),
                position.Y + (x * sin) + (y * cos));
            return Vector2.Transform(rotated, transform);
        }
    }

    private static bool ReferenceAabbIntersects(Vector2[] corners, int width, int height)
    {
        var minX = corners.Min(point => point.X);
        var maxX = corners.Max(point => point.X);
        var minY = corners.Min(point => point.Y);
        var maxY = corners.Max(point => point.Y);
        return maxX > 0f && minX < width && maxY > 0f && minY < height;
    }

    private static bool ReferenceAabbIsBeyondSafetyMargin(Vector2[] corners, int width, int height)
    {
        var minX = corners.Min(point => point.X);
        var maxX = corners.Max(point => point.X);
        var minY = corners.Min(point => point.Y);
        var maxY = corners.Max(point => point.Y);
        const float margin = 2f;
        return maxX < -margin || minX > width + margin || maxY < -margin || minY > height + margin;
    }
}
