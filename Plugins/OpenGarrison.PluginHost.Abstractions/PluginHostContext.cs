namespace OpenGarrison.PluginHost;

public interface IOpenGarrisonPluginHostContext
{
    string PluginId { get; }

    string PluginDirectory { get; }

    string ConfigDirectory { get; }

    OpenGarrisonPluginManifest Manifest { get; }

    OpenGarrisonPluginHostApi HostApi { get; }

    void Log(string message);
}
