namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Context for executing a server command, exposing server services and the invoker's identity.
/// </summary>
/// <param name="ServerState">Read-only server state.</param>
/// <param name="AdminOperations">Admin operations the command may invoke.</param>
/// <param name="Cvars">The server cvar registry.</param>
/// <param name="Scheduler">The server task scheduler.</param>
/// <param name="Identity">The invoker's admin identity.</param>
/// <param name="Source">Where the command was invoked from.</param>
public readonly record struct OpenGarrisonServerCommandContext(
    IOpenGarrisonServerReadOnlyState ServerState,
    IOpenGarrisonServerAdminOperations AdminOperations,
    IOpenGarrisonServerCvarRegistry Cvars,
    IOpenGarrisonServerScheduler Scheduler,
    OpenGarrisonServerAdminIdentity Identity,
    OpenGarrisonServerCommandSource Source)
{
    /// <summary>
    /// Gets whether the invoker has the given admin permissions.
    /// </summary>
    /// <param name="permissions">The required permissions.</param>
    /// <returns>True when the invoker has all required permissions.</returns>
    public bool HasPermission(OpenGarrisonServerAdminPermissions permissions)
    {
        return Identity.HasPermission(permissions);
    }
}
