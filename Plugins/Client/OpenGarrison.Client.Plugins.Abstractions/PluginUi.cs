using System;

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
}
