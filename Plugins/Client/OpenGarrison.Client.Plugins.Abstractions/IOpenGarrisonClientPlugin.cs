namespace OpenGarrison.Client.Plugins;

/// <summary>
/// The entry point of a client plugin loaded by the plugin host.
/// </summary>
public interface IOpenGarrisonClientPlugin
{
    /// <summary>
    /// Gets the unique plugin identifier used for routing and addressing.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable plugin name shown in menus and diagnostics.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the plugin version used for compatibility checks.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Initializes the plugin with access to client services and state.
    /// </summary>
    /// <param name="context">The client plugin context exposing host services.</param>
    void Initialize(IOpenGarrisonClientPluginContext context);

    /// <summary>
    /// Releases plugin resources when the plugin is unloaded.
    /// </summary>
    void Shutdown();
}
