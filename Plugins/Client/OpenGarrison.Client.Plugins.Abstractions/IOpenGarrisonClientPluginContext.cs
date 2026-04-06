using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientPluginContext : IOpenGarrisonPluginHostContext
{
    GraphicsDevice GraphicsDevice { get; }

    IOpenGarrisonClientReadOnlyState ClientState { get; }

    IOpenGarrisonClientPluginAssets Assets { get; }

    IOpenGarrisonClientPluginHotkeys Hotkeys { get; }

    IOpenGarrisonClientPluginUi Ui { get; }

    void SendMessageToServer(
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion);

    void SendMessageToServer(string targetPluginId, string messageType, string payload)
    {
        SendMessageToServer(targetPluginId, messageType, payload, PluginMessagePayloadFormat.Text, schemaVersion: 1);
    }
}
