namespace OpenGarrison.Core;

public readonly record struct RoomObjectMarker(
    RoomObjectType Type,
    float X,
    float Y,
    float Width,
    float Height,
    string SpriteName,
    PlayerTeam? Team = null,
    string SourceName = "")
{
    public float Left => X;

    public float Top => Y;

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public float CenterX => X + (Width / 2f);

    public float CenterY => Y + (Height / 2f);
}
