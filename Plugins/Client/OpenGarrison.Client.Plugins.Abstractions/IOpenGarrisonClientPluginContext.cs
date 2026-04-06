using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientPluginContext
{
    string PluginId { get; }

    string PluginDirectory { get; }

    string ConfigDirectory { get; }

    GraphicsDevice GraphicsDevice { get; }

    IOpenGarrisonClientReadOnlyState ClientState { get; }

    IOpenGarrisonClientPluginAssets Assets { get; }

    IOpenGarrisonClientPluginHotkeys Hotkeys { get; }

    IOpenGarrisonClientPluginUi Ui { get; }

    void SendMessageToServer(string targetPluginId, string messageType, string payload);

    void Log(string message);
}
