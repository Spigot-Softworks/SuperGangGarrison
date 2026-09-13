using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public enum AreaTransitionDirection
{
    Next = 1,
    Previous = 2,
}

public readonly record struct AreaTransitionMarker(
    float X,
    float Y,
    AreaTransitionDirection Direction,
    string SourceName = "");

public static class AreaTransitionMetadata
{
    public static bool IsInArea(float y, int areaIndex, IReadOnlyList<float> boundaries)
    {
        if (boundaries.Count == 0 || y <= 0f)
        {
            return true;
        }

        var index = System.Math.Clamp(areaIndex, 1, boundaries.Count + 1);
        return (index == 1 || y >= boundaries[index - 2])
            && (index > boundaries.Count || y <= boundaries[index - 1]);
    }

    public static float[] BuildAreaBoundaries(IReadOnlyList<AreaTransitionMarker> markers)
    {
        var nextBoundaries = markers
            .Where(marker => marker.Direction == AreaTransitionDirection.Next)
            .Select(marker => marker.Y)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (nextBoundaries.Length > 0)
        {
            return nextBoundaries;
        }

        return markers
            .Where(marker => marker.Direction == AreaTransitionDirection.Previous)
            .Select(marker => marker.Y)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
    }
}
