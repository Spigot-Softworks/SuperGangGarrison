namespace OpenGarrison.Core;

public static class RoomObjectMarkerExtensions
{
    public static bool IsSingleKothControlPoint(this RoomObjectMarker marker)
    {
        return marker.Type == RoomObjectType.ControlPoint
            && marker.SourceName.Equals("KothControlPoint", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDualKothControlPoint(this RoomObjectMarker marker)
    {
        return marker.IsRedKothControlPoint() || marker.IsBlueKothControlPoint();
    }

    public static bool IsRedKothControlPoint(this RoomObjectMarker marker)
    {
        return marker.Type == RoomObjectType.ControlPoint
            && marker.SourceName.Equals("KothRedControlPoint", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBlueKothControlPoint(this RoomObjectMarker marker)
    {
        return marker.Type == RoomObjectType.ControlPoint
            && marker.SourceName.Equals("KothBlueControlPoint", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLeftDoor(this RoomObjectMarker marker)
    {
        return marker.Type == RoomObjectType.PlayerWall
            && marker.SourceName.Equals("leftdoor", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRightDoor(this RoomObjectMarker marker)
    {
        return marker.Type == RoomObjectType.PlayerWall
            && marker.SourceName.Equals("rightdoor", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDirectionalDoor(this RoomObjectMarker marker)
    {
        return marker.IsLeftDoor() || marker.IsRightDoor();
    }

    public static bool IsDropdownPlatform(this RoomObjectMarker marker)
    {
        return marker.Type == RoomObjectType.DropdownPlatform;
    }

    public static bool ResetsMovementState(this RoomObjectMarker marker)
    {
        return marker.Value > 0.5f;
    }

    public static bool IsMoveBox(this RoomObjectMarker marker)
    {
        return marker.Type is RoomObjectType.MoveBoxUp
            or RoomObjectType.MoveBoxDown
            or RoomObjectType.MoveBoxLeft
            or RoomObjectType.MoveBoxRight;
    }

    public static (float X, float Y) GetMoveBoxImpulse(this RoomObjectMarker marker)
    {
        if (!marker.IsMoveBox())
        {
            return (0f, 0f);
        }

        return marker.Type switch
        {
            RoomObjectType.MoveBoxUp => (0f, -marker.Value),
            RoomObjectType.MoveBoxDown => (0f, marker.Value),
            RoomObjectType.MoveBoxLeft => (-marker.Value, 0f),
            RoomObjectType.MoveBoxRight => (marker.Value, 0f),
            _ => (0f, 0f),
        };
    }

    public static bool BlocksDirectionalMovement(
        this RoomObjectMarker marker,
        float previousLeft,
        float previousRight,
        float nextLeft,
        float nextRight)
    {
        if (marker.IsLeftDoor())
        {
            return nextLeft < previousLeft && previousLeft >= marker.Right - 2f;
        }

        if (marker.IsRightDoor())
        {
            return nextRight > previousRight && previousRight <= marker.Left + 2f;
        }

        return true;
    }
}
