using Microsoft.Xna.Framework;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// The team of a client-side gameplay entity.
/// </summary>
public enum ClientPluginTeam : byte
{
    /// <summary>No team.</summary>
    None = 0,
    /// <summary>The red team.</summary>
    Red = 1,
    /// <summary>The blue team.</summary>
    Blue = 2,
}

/// <summary>
/// The playable class of a client-side gameplay entity.
/// </summary>
public enum ClientPluginClass : byte
{
    /// <summary>Unknown class.</summary>
    Unknown = 0,
    /// <summary>Scout.</summary>
    Scout = 1,
    /// <summary>Engineer.</summary>
    Engineer = 2,
    /// <summary>Pyro.</summary>
    Pyro = 3,
    /// <summary>Soldier.</summary>
    Soldier = 4,
    /// <summary>Demoman.</summary>
    Demoman = 5,
    /// <summary>Heavy.</summary>
    Heavy = 6,
    /// <summary>Sniper.</summary>
    Sniper = 7,
    /// <summary>Medic.</summary>
    Medic = 8,
    /// <summary>Spy.</summary>
    Spy = 9,
    /// <summary>Quote.</summary>
    Quote = 10,
}

/// <summary>
/// The kind of an objective marker.
/// </summary>
public enum ClientObjectiveMarkerKind : byte
{
    /// <summary>An attack objective.</summary>
    Attack = 1,
    /// <summary>A defend objective.</summary>
    Defend = 2,
    /// <summary>A control point.</summary>
    ControlPoint = 3,
    /// <summary>A generator.</summary>
    Generator = 4,
}

/// <summary>
/// The death animation used for a dead body.
/// </summary>
public enum ClientDeadBodyAnimationKind : byte
{
    /// <summary>The default death animation.</summary>
    Default = 0,
    /// <summary>The rifle death animation.</summary>
    Rifle = 1,
    /// <summary>The severe (gibbed) death animation.</summary>
    Severe = 2,
}

/// <summary>
/// The kind of bubble menu, keyed by the hotkey that opens it.
/// </summary>
public enum ClientBubbleMenuKind : byte
{
    /// <summary>No bubble menu.</summary>
    None = 0,
    /// <summary>The Z-key bubble menu.</summary>
    Z = 1,
    /// <summary>The X-key bubble menu.</summary>
    X = 2,
    /// <summary>The C-key bubble menu.</summary>
    C = 3,
}

/// <summary>
/// Client-side marker describing a player.
/// </summary>
/// <param name="PlayerId">The player id.</param>
/// <param name="Name">The player name.</param>
/// <param name="Team">The player's team.</param>
/// <param name="ClassId">The player's class.</param>
/// <param name="WorldPosition">The player's world position.</param>
/// <param name="Health">The player's current health.</param>
/// <param name="MaxHealth">The player's maximum health.</param>
/// <param name="IsAlive">Whether the player is alive.</param>
/// <param name="IsCarryingIntel">Whether the player is carrying the intelligence.</param>
/// <param name="IsLocalPlayer">Whether this is the local player.</param>
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

/// <summary>
/// Client-side marker describing a sentry.
/// </summary>
/// <param name="EntityId">The sentry entity id.</param>
/// <param name="OwnerPlayerId">The owning player id.</param>
/// <param name="Team">The sentry's team.</param>
/// <param name="WorldPosition">The sentry's world position.</param>
/// <param name="Health">The sentry's current health.</param>
/// <param name="MaxHealth">The sentry's maximum health.</param>
public sealed record ClientSentryMarker(
    int EntityId,
    int OwnerPlayerId,
    ClientPluginTeam Team,
    Vector2 WorldPosition,
    int Health,
    int MaxHealth);

/// <summary>
/// Client-side marker describing an objective.
/// </summary>
/// <param name="Kind">The objective kind.</param>
/// <param name="Team">The team owning the objective.</param>
/// <param name="WorldPosition">The objective's world position.</param>
/// <param name="Progress">The capture progress (0 to 1).</param>
/// <param name="IsLocked">Whether the objective is locked.</param>
public sealed record ClientObjectiveMarker(
    ClientObjectiveMarkerKind Kind,
    ClientPluginTeam Team,
    Vector2 WorldPosition,
    float Progress,
    bool IsLocked);

/// <summary>
/// Render state for a dead body drawn by plugins.
/// </summary>
/// <param name="Id">The dead body id.</param>
/// <param name="ClassId">The victim's class.</param>
/// <param name="Team">The victim's team.</param>
/// <param name="WorldPosition">The world position.</param>
/// <param name="Width">The render width.</param>
/// <param name="Height">The render height.</param>
/// <param name="FacingLeft">Whether the body faces left.</param>
/// <param name="TicksRemaining">The ticks remaining before the body disappears.</param>
/// <param name="AnimationKind">The death animation kind.</param>
/// <param name="GameplayClassId">The gameplay class id.</param>
public sealed record ClientDeadBodyRenderState(
    int Id,
    ClientPluginClass ClassId,
    ClientPluginTeam Team,
    Vector2 WorldPosition,
    float Width,
    float Height,
    bool FacingLeft,
    int TicksRemaining,
    ClientDeadBodyAnimationKind AnimationKind,
    string GameplayClassId = "");

/// <summary>
/// Input state for a bubble menu.
/// </summary>
/// <param name="Kind">The bubble menu kind.</param>
/// <param name="XPageIndex">The current X-menu page index.</param>
/// <param name="AimDirectionDegrees">The aim direction in degrees.</param>
/// <param name="DistanceFromCenter">The cursor distance from the menu center.</param>
/// <param name="LeftMousePressed">Whether the left mouse button was pressed this frame.</param>
/// <param name="LeftMouseDown">Whether the left mouse button is held down.</param>
/// <param name="LeftMouseReleased">Whether the left mouse button was released this frame.</param>
/// <param name="PressedDigit">The digit key pressed this frame, if any.</param>
/// <param name="QPressed">Whether Q was pressed this frame.</param>
public sealed record ClientBubbleMenuInputState(
    ClientBubbleMenuKind Kind,
    int XPageIndex,
    float AimDirectionDegrees,
    float DistanceFromCenter,
    bool LeftMousePressed,
    bool LeftMouseDown,
    bool LeftMouseReleased,
    int? PressedDigit,
    bool QPressed);

/// <summary>
/// Render state for a bubble menu.
/// </summary>
/// <param name="Kind">The bubble menu kind.</param>
/// <param name="Alpha">The menu opacity.</param>
/// <param name="XPageIndex">The current X-menu page index.</param>
/// <param name="AimDirectionDegrees">The aim direction in degrees.</param>
/// <param name="SelectedSlot">The currently selected slot.</param>
public sealed record ClientBubbleMenuRenderState(
    ClientBubbleMenuKind Kind,
    float Alpha,
    int XPageIndex,
    float AimDirectionDegrees,
    int SelectedSlot);

/// <summary>
/// The result of handling bubble menu input.
/// </summary>
/// <param name="BubbleFrame">The selected bubble frame, if any.</param>
/// <param name="NewXPageIndex">The new X-menu page index, if changed.</param>
/// <param name="CloseMenu">Whether the menu should close.</param>
/// <param name="ClearBubbleSelection">Whether the bubble selection should be cleared.</param>
public sealed record ClientBubbleMenuUpdateResult(
    int? BubbleFrame = null,
    int? NewXPageIndex = null,
    bool CloseMenu = false,
    bool ClearBubbleSelection = false);

/// <summary>
/// Hooks a client plugin can implement to handle and draw bubble menus.
/// </summary>
public interface IOpenGarrisonClientBubbleMenuHooks
{
    /// <summary>
    /// Handles bubble menu input for the current frame.
    /// </summary>
    /// <param name="inputState">The bubble menu input state.</param>
    /// <returns>The input handling result, or null when unhandled.</returns>
    ClientBubbleMenuUpdateResult? TryHandleBubbleMenuInput(ClientBubbleMenuInputState inputState);

    /// <summary>
    /// Draws the bubble menu.
    /// </summary>
    /// <param name="canvas">The HUD drawing canvas.</param>
    /// <param name="renderState">The bubble menu render state.</param>
    /// <returns>True when the plugin drew the menu.</returns>
    bool TryDrawBubbleMenu(IOpenGarrisonClientHudCanvas canvas, ClientBubbleMenuRenderState renderState);
}

/// <summary>
/// Hooks a client plugin can implement to draw dead bodies.
/// </summary>
public interface IOpenGarrisonClientDeadBodyHooks
{
    /// <summary>
    /// Draws a dead body.
    /// </summary>
    /// <param name="canvas">The HUD drawing canvas.</param>
    /// <param name="deadBody">The dead body render state.</param>
    /// <returns>True when the plugin drew the dead body.</returns>
    bool TryDrawDeadBody(IOpenGarrisonClientHudCanvas canvas, ClientDeadBodyRenderState deadBody);
}
