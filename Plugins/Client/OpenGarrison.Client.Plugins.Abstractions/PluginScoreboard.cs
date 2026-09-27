using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// The location of a plugin-contributed scoreboard panel.
/// </summary>
public enum ClientScoreboardPanelLocation
{
    /// <summary>The header-left panel area.</summary>
    HeaderLeft = 0,
    /// <summary>The header-right panel area.</summary>
    HeaderRight = 1,
    /// <summary>The footer panel area.</summary>
    Footer = 2,
}

/// <summary>
/// Render state for the scoreboard.
/// </summary>
/// <param name="ScoreboardBounds">The scoreboard bounds in screen coordinates.</param>
/// <param name="Alpha">The scoreboard opacity.</param>
/// <param name="ServerLabel">The server label.</param>
/// <param name="MapLabel">The map label.</param>
/// <param name="RedPlayerCount">The red team player count.</param>
/// <param name="BluePlayerCount">The blue team player count.</param>
/// <param name="RedCenterText">The red team center text.</param>
/// <param name="BlueCenterText">The blue team center text.</param>
public sealed record ClientScoreboardRenderState(
    Rectangle ScoreboardBounds,
    float Alpha,
    string ServerLabel,
    string MapLabel,
    int RedPlayerCount,
    int BluePlayerCount,
    string RedCenterText,
    string BlueCenterText);

/// <summary>
/// Drawing surface for scoreboard content, extending the HUD canvas.
/// </summary>
public interface IOpenGarrisonClientScoreboardCanvas : IOpenGarrisonClientHudCanvas
{
    /// <summary>
    /// Draws bitmap text right-aligned to a screen position.
    /// </summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="position">The screen position to right-align to.</param>
    /// <param name="color">The text color.</param>
    /// <param name="scale">The text scale.</param>
    void DrawBitmapTextRightAligned(string text, Vector2 position, Color color, float scale = 1f);
}

/// <summary>
/// Hooks a client plugin can implement to draw scoreboard content.
/// </summary>
public interface IOpenGarrisonClientScoreboardHooks
{
    /// <summary>
    /// Gets the panel location for the plugin's scoreboard panel.
    /// </summary>
    ClientScoreboardPanelLocation ScoreboardPanelLocation => ClientScoreboardPanelLocation.Footer;

    /// <summary>
    /// Gets the draw order of the plugin's scoreboard panel.
    /// </summary>
    int ScoreboardPanelOrder => 0;

    /// <summary>
    /// Called to draw plugin content on the scoreboard.
    /// </summary>
    /// <param name="canvas">The scoreboard drawing canvas.</param>
    /// <param name="state">The current scoreboard render state.</param>
    void OnScoreboardDraw(IOpenGarrisonClientScoreboardCanvas canvas, ClientScoreboardRenderState state);
}

/// <summary>
/// Context for a plugin-contributed scoreboard player action.
/// </summary>
/// <param name="Slot">The player's slot.</param>
/// <param name="PlayerId">The player id.</param>
/// <param name="PlayerName">The player name.</param>
/// <param name="Team">The player's team.</param>
/// <param name="PlayerClass">The player's class.</param>
/// <param name="IsLocalPlayer">Whether this is the local player.</param>
public sealed record ClientScoreboardPlayerActionContext(
    byte Slot,
    int PlayerId,
    string PlayerName,
    ClientPluginTeam Team,
    ClientPluginClass PlayerClass,
    bool IsLocalPlayer);

/// <summary>
/// A plugin-contributed action shown for a scoreboard player row.
/// </summary>
/// <param name="Id">The action id.</param>
/// <param name="Label">The action label.</param>
/// <param name="Order">The action order.</param>
/// <param name="Activate">Invoked when the action is activated.</param>
public sealed record ClientScoreboardPlayerAction(
    string Id,
    string Label,
    int Order,
    Action<ClientScoreboardPlayerActionContext> Activate);

/// <summary>
/// Hooks a client plugin can implement to contribute scoreboard player actions.
/// </summary>
public interface IOpenGarrisonClientScoreboardPlayerActionHooks
{
    /// <summary>
    /// Gets the plugin's actions for a scoreboard player row.
    /// </summary>
    /// <param name="context">The player action context.</param>
    /// <returns>The plugin's actions for the player.</returns>
    IReadOnlyList<ClientScoreboardPlayerAction> GetScoreboardPlayerActions(ClientScoreboardPlayerActionContext context)
        => Array.Empty<ClientScoreboardPlayerAction>();
}
