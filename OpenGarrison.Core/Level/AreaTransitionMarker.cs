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
