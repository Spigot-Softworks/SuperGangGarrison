namespace OpenGarrison.Core;

public enum MovingPlatformMotionState : byte
{
    Stopped = 0,
    MovingOut = 1,
    Returning = 2,
}

public sealed class MovingPlatformRuntimeState
{
    public MovingPlatformRuntimeState(int index, MovingPlatformMarker marker)
    {
        Index = index;
        Source = marker;
        StartX = marker.X;
        StartY = marker.Y;
        X = marker.X;
        Y = marker.Y;
        Width = marker.Width;
        Height = marker.Height;
        TravelX = marker.TravelX;
        TravelY = marker.TravelY;
        UpSpeed = marker.UpSpeed;
        DownSpeed = marker.DownSpeed;
        TriggerMode = marker.TriggerMode;
        ResetMovementState = marker.ResetMovementState;
        ResourceName = marker.ResourceName;
        State = TriggerMode == 0 ? MovingPlatformMotionState.MovingOut : MovingPlatformMotionState.Stopped;
    }

    public int Index { get; }

    public MovingPlatformMarker Source { get; }

    public float StartX { get; }

    public float StartY { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float Width { get; }

    public float Height { get; }

    public float TravelX { get; }

    public float TravelY { get; }

    public float UpSpeed { get; }

    public float DownSpeed { get; }

    public int TriggerMode { get; }

    public bool ResetMovementState { get; }

    public string ResourceName { get; }

    public MovingPlatformMotionState State { get; private set; }

    public float Left => X;

    public float Top => Y;

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public bool IsStopped => State == MovingPlatformMotionState.Stopped;

    public void Reset()
    {
        X = StartX;
        Y = StartY;
        State = TriggerMode == 0 ? MovingPlatformMotionState.MovingOut : MovingPlatformMotionState.Stopped;
    }

    public void TryTrigger(bool carryingIntel)
    {
        if (State != MovingPlatformMotionState.Stopped)
        {
            return;
        }

        if (TriggerMode == 1 || (TriggerMode == 2 && carryingIntel))
        {
            State = MovingPlatformMotionState.MovingOut;
        }
    }

    public (float DeltaX, float DeltaY) Advance(double deltaSeconds)
    {
        if (State == MovingPlatformMotionState.Stopped || deltaSeconds <= 0d)
        {
            return (0f, 0f);
        }

        var totalDistance = MathF.Sqrt((TravelX * TravelX) + (TravelY * TravelY));
        if (totalDistance <= 0.0001f)
        {
            X = StartX;
            Y = StartY;
            State = MovingPlatformMotionState.Stopped;
            return (0f, 0f);
        }

        var previousX = X;
        var previousY = Y;
        var speed = State == MovingPlatformMotionState.MovingOut ? UpSpeed : DownSpeed;
        var distance = MathF.Max(0f, speed) * LegacyMovementModel.SourceTicksPerSecond * (float)deltaSeconds;
        var directionSign = State == MovingPlatformMotionState.MovingOut ? 1f : -1f;
        X += (TravelX / totalDistance) * distance * directionSign;
        Y += (TravelY / totalDistance) * distance * directionSign;

        var progress = (((X - StartX) * TravelX) + ((Y - StartY) * TravelY)) / totalDistance;
        if (State == MovingPlatformMotionState.Returning && progress <= 0f)
        {
            X = StartX;
            Y = StartY;
            State = TriggerMode == 0 ? MovingPlatformMotionState.MovingOut : MovingPlatformMotionState.Stopped;
        }
        else if (State == MovingPlatformMotionState.MovingOut && progress >= totalDistance)
        {
            X = StartX + TravelX;
            Y = StartY + TravelY;
            State = MovingPlatformMotionState.Returning;
        }

        return (X - previousX, Y - previousY);
    }
}
