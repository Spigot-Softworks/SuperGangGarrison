using System;
using System.Collections.Generic;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// The menu a plugin menu entry is registered in.
/// </summary>
public enum ClientPluginMenuLocation
{
    /// <summary>The main menu root.</summary>
    MainMenuRoot = 0,
    /// <summary>The in-game menu.</summary>
    InGameMenu = 1,
}

/// <summary>
/// UI service plugins use to register menus and show notices or overlays.
/// </summary>
public interface IOpenGarrisonClientPluginUi
{
    /// <summary>
    /// Registers a menu entry that invokes an action when selected.
    /// </summary>
    /// <param name="menuEntryId">The menu entry id.</param>
    /// <param name="label">The menu entry label.</param>
    /// <param name="location">The menu the entry is registered in.</param>
    /// <param name="activate">Invoked when the entry is selected.</param>
    /// <param name="order">The entry order.</param>
    void RegisterMenuEntry(string menuEntryId, string label, ClientPluginMenuLocation location, Action activate, int order = 0);

    /// <summary>
    /// Shows a transient notice to the player.
    /// </summary>
    /// <param name="text">The notice text.</param>
    /// <param name="durationTicks">The notice duration in ticks.</param>
    /// <param name="playSound">Whether to play a notification sound.</param>
    void ShowNotice(string text, int durationTicks = 200, bool playSound = true);

    /// <summary>
    /// Shows an overlay menu with a list of entries.
    /// </summary>
    /// <param name="title">The overlay title.</param>
    /// <param name="subtitle">The overlay subtitle.</param>
    /// <param name="breadcrumb">The breadcrumb text.</param>
    /// <param name="entries">The menu entries.</param>
    void ShowOverlayMenu(string title, string subtitle, string breadcrumb, IReadOnlyList<string> entries);

    /// <summary>
    /// Shows an overlay panel with plugin-defined controls.
    /// </summary>
    /// <param name="panel">The overlay panel.</param>
    void ShowOverlayPanel(ClientPluginOverlayPanel panel);

    /// <summary>
    /// Hides the current overlay panel (same as hiding the overlay menu).
    /// </summary>
    void HideOverlayPanel() => HideOverlayMenu();

    /// <summary>
    /// Hides the current overlay menu.
    /// </summary>
    void HideOverlayMenu();
}

/// <summary>
/// The kind of a plugin overlay control.
/// </summary>
public enum ClientPluginOverlayControlKind
{
    /// <summary>Static text.</summary>
    Text = 0,
    /// <summary>A button.</summary>
    Button = 1,
    /// <summary>A toggle.</summary>
    Toggle = 2,
    /// <summary>A slider.</summary>
    Slider = 3,
    /// <summary>A text input.</summary>
    Input = 4,
    /// <summary>A visual divider.</summary>
    Divider = 5,
}

/// <summary>
/// A single control in a plugin overlay panel.
/// </summary>
/// <param name="Id">The control id.</param>
/// <param name="Label">The control label.</param>
/// <param name="Kind">The control kind.</param>
/// <param name="Value">The control value.</param>
/// <param name="IsEnabled">Whether the control is enabled.</param>
public sealed record ClientPluginOverlayControl(
    string Id,
    string Label,
    ClientPluginOverlayControlKind Kind = ClientPluginOverlayControlKind.Text,
    string Value = "",
    bool IsEnabled = true);

/// <summary>
/// An overlay panel with plugin-defined controls.
/// </summary>
/// <param name="Title">The panel title.</param>
/// <param name="Subtitle">The panel subtitle.</param>
/// <param name="Breadcrumb">The breadcrumb text.</param>
/// <param name="Controls">The panel controls.</param>
public sealed record ClientPluginOverlayPanel(
    string Title,
    string Subtitle,
    string Breadcrumb,
    IReadOnlyList<ClientPluginOverlayControl> Controls);
