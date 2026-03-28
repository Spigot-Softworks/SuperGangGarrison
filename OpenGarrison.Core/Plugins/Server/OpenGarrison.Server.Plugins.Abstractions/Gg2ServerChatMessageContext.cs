namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerChatMessageContext(
    IOpenGarrisonServerReadOnlyState ServerState,
    IOpenGarrisonServerAdminOperations AdminOperations);
