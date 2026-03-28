using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed record ServerPluginRuntime(
    PluginCommandRegistry CommandRegistry,
    PluginHost PluginHost,
    IOpenGarrisonServerReadOnlyState ServerState,
    IOpenGarrisonServerAdminOperations AdminOperations);
