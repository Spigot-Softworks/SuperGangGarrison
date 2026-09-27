namespace OpenGarrison.Server.Plugins;

/// <summary>
/// The entry point of a server plugin loaded by the plugin host.
/// </summary>
public interface IOpenGarrisonServerPlugin
{
    /// <summary>
    /// Gets the unique plugin identifier used for routing and addressing.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable plugin name shown in diagnostics.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the plugin version used for compatibility checks.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Initializes the plugin with access to server services and state.
    /// </summary>
    /// <param name="context">The server plugin context exposing host services.</param>
    void Initialize(IOpenGarrisonServerPluginContext context);

    /// <summary>
    /// Releases plugin resources when the plugin is unloaded.
    /// </summary>
    void Shutdown();
}
