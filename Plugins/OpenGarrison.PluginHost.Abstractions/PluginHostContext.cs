namespace OpenGarrison.PluginHost;

/// <summary>
/// The host context handed to a plugin at load time.
/// </summary>
public interface IOpenGarrisonPluginHostContext
{
    /// <summary>
    /// Gets the plugin id.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Gets the plugin directory.
    /// </summary>
    string PluginDirectory { get; }

    /// <summary>
    /// Gets the config directory.
    /// </summary>
    string ConfigDirectory { get; }

    /// <summary>
    /// Gets the plugin manifest.
    /// </summary>
    OpenGarrisonPluginManifest Manifest { get; }

    /// <summary>
    /// Gets the host API description.
    /// </summary>
    OpenGarrisonPluginHostApi HostApi { get; }

    /// <summary>
    /// Logs a message to the host log.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Log(string message);
}
