using System;
using System.Globalization;

namespace OpenGarrison.Core;

public static class ControlPointMarkerIndex
{
    public static bool TryGetIndex(RoomObjectMarker marker, out int index)
    {
        index = 0;
        if (string.IsNullOrWhiteSpace(marker.SourceName))
        {
            return false;
        }

        return TryParseSourceName(marker.SourceName, out index);
    }

    public static bool TryParseSourceName(string sourceName, out int index)
    {
        index = 0;
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return false;
        }

        const string prefix = "ControlPoint";
        if (!sourceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = sourceName[prefix.Length..];
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return false;
        }

        return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
            && index >= ControlPointIndexMetadata.MinIndex;
    }

    public static int CompareMarkersForGameplayOrder(RoomObjectMarker left, RoomObjectMarker right)
    {
        var leftHasIndex = TryGetIndex(left, out var leftIndex);
        var rightHasIndex = TryGetIndex(right, out var rightIndex);
        if (leftHasIndex && rightHasIndex)
        {
            var byIndex = leftIndex.CompareTo(rightIndex);
            if (byIndex != 0)
            {
                return byIndex;
            }
        }
        else if (leftHasIndex != rightHasIndex)
        {
            return leftHasIndex ? -1 : 1;
        }

        var byX = left.CenterX.CompareTo(right.CenterX);
        return byX != 0 ? byX : left.CenterY.CompareTo(right.CenterY);
    }
}
