using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientPluginContext
{
    string PluginId { get; }

    string PluginDirectory { get; }

    string ConfigDirectory { get; }

    GraphicsDevice GraphicsDevice { get; }

    IOpenGarrisonClientReadOnlyState ClientState { get; }

    void Log(string message);
}
