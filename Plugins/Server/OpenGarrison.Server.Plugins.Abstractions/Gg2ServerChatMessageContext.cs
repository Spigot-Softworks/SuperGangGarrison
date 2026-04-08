namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerChatMessageContext(
    IOpenGarrisonServerReadOnlyState ServerState,
    IOpenGarrisonServerAdminOperations AdminOperations,
    IOpenGarrisonServerCvarRegistry Cvars,
    IOpenGarrisonServerScheduler Scheduler,
    OpenGarrisonServerAdminIdentity Identity)
{
    public bool IsAuthenticatedAdmin => Identity.IsAuthenticated;
}
