namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerCommandContext(
    IOpenGarrisonServerReadOnlyState ServerState,
    IOpenGarrisonServerAdminOperations AdminOperations);
