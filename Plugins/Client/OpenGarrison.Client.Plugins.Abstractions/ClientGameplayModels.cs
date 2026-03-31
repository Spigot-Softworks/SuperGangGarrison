using Microsoft.Xna.Framework;

namespace OpenGarrison.Client.Plugins;

public enum ClientPluginTeam : byte
{
    None = 0,
    Red = 1,
    Blue = 2,
}

public enum ClientPluginClass : byte
{
    Unknown = 0,
    Scout = 1,
    Engineer = 2,
    Pyro = 3,
    Soldier = 4,
    Demoman = 5,
    Heavy = 6,
    Sniper = 7,
    Medic = 8,
    Spy = 9,
    Quote = 10,
}

public enum ClientObjectiveMarkerKind : byte
{
    Attack = 1,
    Defend = 2,
    ControlPoint = 3,
    Generator = 4,
}

public enum ClientDeadBodyAnimationKind : byte
{
    Default = 0,
    Rifle = 1,
    Severe = 2,
}

public enum ClientBubbleMenuKind : byte
{
    None = 0,
    Z = 1,
    X = 2,
    C = 3,
}

public sealed record ClientPlayerMarker(
    int PlayerId,
    string Name,
    ClientPluginTeam Team,
    ClientPluginClass ClassId,
    Vector2 WorldPosition,
    int Health,
    int MaxHealth,
    bool IsAlive,
    bool IsCarryingIntel,
    bool IsLocalPlayer);

public sealed record ClientSentryMarker(
    int EntityId,
    int OwnerPlayerId,
    ClientPluginTeam Team,
    Vector2 WorldPosition,
    int Health,
    int MaxHealth);

public sealed record ClientObjectiveMarker(
    ClientObjectiveMarkerKind Kind,
    ClientPluginTeam Team,
    Vector2 WorldPosition,
    float Progress,
    bool IsLocked);

public sealed record ClientDeadBodyRenderState(
    int Id,
    ClientPluginClass ClassId,
    ClientPluginTeam Team,
    Vector2 WorldPosition,
    float Width,
    float Height,
    bool FacingLeft,
    int TicksRemaining,
    ClientDeadBodyAnimationKind AnimationKind);

public sealed record ClientBubbleMenuInputState(
    ClientBubbleMenuKind Kind,
    int XPageIndex,
    float AimDirectionDegrees,
    float DistanceFromCenter,
    bool LeftMousePressed,
    int? PressedDigit,
    bool QPressed);

public sealed record ClientBubbleMenuRenderState(
    ClientBubbleMenuKind Kind,
    float Alpha,
    int XPageIndex,
    float AimDirectionDegrees,
    int SelectedSlot);

public sealed record ClientBubbleMenuUpdateResult(
    int? BubbleFrame = null,
    int? NewXPageIndex = null,
    bool CloseMenu = false);

public interface IOpenGarrisonClientBubbleMenuHooks
{
    ClientBubbleMenuUpdateResult? TryHandleBubbleMenuInput(ClientBubbleMenuInputState inputState);

    bool TryDrawBubbleMenu(IOpenGarrisonClientHudCanvas canvas, ClientBubbleMenuRenderState renderState);
}

public interface IOpenGarrisonClientDeadBodyHooks
{
    bool TryDrawDeadBody(IOpenGarrisonClientHudCanvas canvas, ClientDeadBodyRenderState deadBody);
}
