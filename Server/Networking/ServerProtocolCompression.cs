using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

/// <summary>
/// Manages protocol compression settings for the server.
/// </summary>
internal static class ServerProtocolCompression
{
    private static ProtocolCompressionSettings _settings = ProtocolCompressionSettings.Default;

    /// <summary>
    /// Gets the current compression settings used by the server.
    /// </summary>
    public static ProtocolCompressionSettings Settings => _settings;

    /// <summary>
    /// Configure compression settings based on server configuration.
    /// </summary>
    public static void Configure(bool enabled)
    {
        _settings = enabled
            ? ProtocolCompressionSettings.Default
            : ProtocolCompressionSettings.Disabled;
    }
}
