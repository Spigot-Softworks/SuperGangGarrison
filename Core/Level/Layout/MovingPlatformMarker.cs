namespace OpenGarrison.Core;

public readonly record struct MovingPlatformMarker(
    float X,
    float Y,
    float Width,
    float Height,
    float TravelX,
    float TravelY,
    float UpSpeed,
    float DownSpeed,
    int TriggerMode,
    bool ResetMovementState,
    string ResourceName = "",
    string SourceName = "")
{
    public float Left => X;

    public float Top => Y;

    public float Right => X + Width;

    public float Bottom => Y + Height;
}
