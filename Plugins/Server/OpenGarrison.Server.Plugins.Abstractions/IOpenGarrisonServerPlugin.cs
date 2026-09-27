namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerPlugin
{
    string Id { get; }

    string DisplayName { get; }

    Version Version { get; }

    void Initialize(IOpenGarrisonServerPluginContext context);

    void Shutdown();
}
