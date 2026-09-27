using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.PluginHost;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// Services and state exposed to a client plugin by the plugin host.
/// </summary>
public interface IOpenGarrisonClientPluginContext : IOpenGarrisonPluginHostContext
{
    /// <summary>
    /// Gets the game's graphics device for plugin rendering.
    /// </summary>
    GraphicsDevice GraphicsDevice { get; }

    /// <summary>
    /// Gets read-only client state such as connection status and the local player.
    /// </summary>
    IOpenGarrisonClientReadOnlyState ClientState { get; }

    /// <summary>
    /// Gets the asset registry used to register and look up plugin textures and sounds.
    /// </summary>
    IOpenGarrisonClientPluginAssets Assets { get; }

    /// <summary>
    /// Gets the hotkey registry used to register and poll plugin hotkeys.
    /// </summary>
    IOpenGarrisonClientPluginHotkeys Hotkeys { get; }

    /// <summary>
    /// Gets the UI service used to register menus and show notices or overlays.
    /// </summary>
    IOpenGarrisonClientPluginUi Ui { get; }

    /// <summary>
    /// Sends a plugin message to the matching server-side plugin.
    /// </summary>
    /// <param name="targetPluginId">The server plugin identifier that should receive the message.</param>
    /// <param name="messageType">The message type understood by the receiving plugin.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="payloadFormat">The payload format (text or JSON).</param>
    /// <param name="schemaVersion">The message schema version.</param>
    void SendMessageToServer(
        string targetPluginId,
        string messageType,
        string payload,
        PluginMessagePayloadFormat payloadFormat,
        ushort schemaVersion);

    /// <summary>
    /// Sends a text plugin message to the matching server-side plugin using schema version 1.
    /// </summary>
    /// <param name="targetPluginId">The server plugin identifier that should receive the message.</param>
    /// <param name="messageType">The message type understood by the receiving plugin.</param>
    /// <param name="payload">The text message payload.</param>
    void SendMessageToServer(string targetPluginId, string messageType, string payload)
    {
        SendMessageToServer(targetPluginId, messageType, payload, PluginMessagePayloadFormat.Text, schemaVersion: 1);
    }
}
