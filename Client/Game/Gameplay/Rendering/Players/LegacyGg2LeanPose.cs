namespace OpenGarrison.Client;

internal static class LegacyGg2LeanPose
{
    internal enum Direction
    {
        None,
        Left,
        Right,
    }

    internal static Direction Resolve(
        bool nearLeftSupported,
        bool farLeftSupported,
        bool nearRightSupported,
        bool farRightSupported)
    {
        var leftOpen = !nearLeftSupported && !farLeftSupported;
        var rightOpen = !nearRightSupported && !farRightSupported;

        // A real edge has ground on one side and open space on the other.
        // If both sides read open, the floor sample is uncertain; posing as a
        // lean would place the foot below the visible floor.
        if (leftOpen == rightOpen)
        {
            return Direction.None;
        }

        return leftOpen ? Direction.Left : Direction.Right;
    }
}
