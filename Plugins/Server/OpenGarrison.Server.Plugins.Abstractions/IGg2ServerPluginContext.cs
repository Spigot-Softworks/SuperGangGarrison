namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerPluginContext
{
    string PluginId { get; }

    string PluginDirectory { get; }

    string ConfigDirectory { get; }

    string MapsDirectory { get; }

    IOpenGarrisonServerReadOnlyState ServerState { get; }

    IOpenGarrisonServerAdminOperations AdminOperations { get; }

    void RegisterCommand(IOpenGarrisonServerCommand command);

    void Log(string message);
}
