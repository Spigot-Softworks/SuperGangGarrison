namespace OpenGarrison.Client.Plugins;

public interface IOpenGarrisonClientPlugin
{
    string Id { get; }

    string DisplayName { get; }

    Version Version { get; }

    void Initialize(IOpenGarrisonClientPluginContext context);

    void Shutdown();
}
