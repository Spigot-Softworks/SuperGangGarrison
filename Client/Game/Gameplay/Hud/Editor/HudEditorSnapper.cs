#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal static class HudEditorSnapper
{
    public const float DefaultThresholdPixels = 6f;

    public static Vector2 SnapOrigin(
        Vector2 origin,
        HudElementLayout layout,
        IEnumerable<HudResolvedElement> otherElements,
        int viewportWidth,
        int viewportHeight,
        int gridSize,
        float threshold = DefaultThresholdPixels)
    {
        gridSize = Math.Max(1, gridSize);
        var bounds = layout.ResolveBounds(origin);
        var snapped = origin;

        SnapAxis(
            bounds.Left,
            bounds.Center.X,
            bounds.Right,
            BuildAxisCandidates(viewportWidth, gridSize, otherElements, horizontal: true),
            threshold,
            out var deltaX);
        SnapAxis(
            bounds.Top,
            bounds.Center.Y,
            bounds.Bottom,
            BuildAxisCandidates(viewportHeight, gridSize, otherElements, horizontal: false),
            threshold,
            out var deltaY);

        snapped.X += deltaX;
        snapped.Y += deltaY;
        return snapped;
    }

    private static List<float> BuildAxisCandidates(int viewportSize, int gridSize, IEnumerable<HudResolvedElement> otherElements, bool horizontal)
    {
        var candidates = new List<float>
        {
            0f,
            viewportSize / 2f,
            viewportSize,
        };

        for (var value = gridSize; value < viewportSize; value += gridSize)
        {
            candidates.Add(value);
        }

        foreach (var element in otherElements)
        {
            candidates.Add(horizontal ? element.Bounds.Left : element.Bounds.Top);
            candidates.Add(horizontal ? element.Bounds.Center.X : element.Bounds.Center.Y);
            candidates.Add(horizontal ? element.Bounds.Right : element.Bounds.Bottom);
        }

        return candidates;
    }

    private static void SnapAxis(float start, float center, float end, IReadOnlyList<float> candidates, float threshold, out float delta)
    {
        var resolvedDelta = 0f;
        var bestDistance = threshold;

        foreach (var candidate in candidates)
        {
            Consider(candidate - start);
            Consider(candidate - center);
            Consider(candidate - end);
        }

        void Consider(float candidateDelta)
        {
            var distance = MathF.Abs(candidateDelta);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                resolvedDelta = candidateDelta;
            }
        }

        delta = resolvedDelta;
    }
}
