namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Context for handling a chat message, exposing server services and the sender's identity.
/// </summary>
/// <param name="ServerState">Read-only server state.</param>
/// <param name="AdminOperations">Admin operations the handler may invoke.</param>
/// <param name="Cvars">The server cvar registry.</param>
/// <param name="Scheduler">The server task scheduler.</param>
/// <param name="Identity">The sender's admin identity.</param>
public readonly record struct OpenGarrisonServerChatMessageContext(
    IOpenGarrisonServerReadOnlyState ServerState,
    IOpenGarrisonServerAdminOperations AdminOperations,
    IOpenGarrisonServerCvarRegistry Cvars,
    IOpenGarrisonServerScheduler Scheduler,
    OpenGarrisonServerAdminIdentity Identity)
{
    /// <summary>
    /// Gets whether the sender is an authenticated admin.
    /// </summary>
    public bool IsAuthenticatedAdmin => Identity.IsAuthenticated;
}
