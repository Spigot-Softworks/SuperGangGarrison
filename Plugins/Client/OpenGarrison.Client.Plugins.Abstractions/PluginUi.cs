using System;
using System.Collections.Generic;

namespace OpenGarrison.Client.Plugins;

public enum ClientPluginMenuLocation
{
    MainMenuRoot = 0,
    InGameMenu = 1,
}

public interface IOpenGarrisonClientPluginUi
{
    void RegisterMenuEntry(string menuEntryId, string label, ClientPluginMenuLocation location, Action activate, int order = 0);

    void ShowNotice(string text, int durationTicks = 200, bool playSound = true);

    void ShowOverlayMenu(string title, string subtitle, string breadcrumb, IReadOnlyList<string> entries);

    void ShowOverlayPanel(ClientPluginOverlayPanel panel);

    void HideOverlayMenu();
}

public enum ClientPluginOverlayControlKind
{
    Text = 0,
    Button = 1,
    Toggle = 2,
    Slider = 3,
    Input = 4,
    Divider = 5,
}

public sealed record ClientPluginOverlayControl(
    string Id,
    string Label,
    ClientPluginOverlayControlKind Kind = ClientPluginOverlayControlKind.Text,
    string Value = "",
    bool IsEnabled = true);

public sealed record ClientPluginOverlayPanel(
    string Title,
    string Subtitle,
    string Breadcrumb,
    IReadOnlyList<ClientPluginOverlayControl> Controls);
