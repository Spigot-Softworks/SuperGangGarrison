namespace OpenGarrison.Server.Plugins;

public readonly record struct OpenGarrisonServerCommandContext(
    IOpenGarrisonServerReadOnlyState ServerState,
    IOpenGarrisonServerAdminOperations AdminOperations,
    IOpenGarrisonServerCvarRegistry Cvars,
    IOpenGarrisonServerScheduler Scheduler,
    OpenGarrisonServerAdminIdentity Identity,
    OpenGarrisonServerCommandSource Source)
{
    public bool HasPermission(OpenGarrisonServerAdminPermissions permissions)
    {
        return Identity.HasPermission(permissions);
    }
}
