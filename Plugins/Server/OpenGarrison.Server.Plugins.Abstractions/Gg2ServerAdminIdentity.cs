using System;

namespace OpenGarrison.Server.Plugins;

[Flags]
public enum OpenGarrisonServerAdminPermissions
{
    None = 0,
    ViewServerState = 1 << 0,
    ManagePlayers = 1 << 1,
    ManageMatch = 1 << 2,
    ManageServerConfiguration = 1 << 3,
    ManagePlugins = 1 << 4,
    ManageScheduler = 1 << 5,
    FullAccess = ViewServerState
        | ManagePlayers
        | ManageMatch
        | ManageServerConfiguration
        | ManagePlugins
        | ManageScheduler,
}

public enum OpenGarrisonServerAdminAuthority
{
    None = 0,
    HostConsole,
    AdminPipe,
    RconSession,
    PluginHost,
}

public enum OpenGarrisonServerCommandSource
{
    Console = 0,
    AdminPipe,
    PrivateChat,
    Plugin,
    Internal,
}

public readonly record struct OpenGarrisonServerAdminIdentity(
    string DisplayName,
    OpenGarrisonServerAdminAuthority Authority,
    OpenGarrisonServerAdminPermissions Permissions,
    byte? SourceSlot = null)
{
    public bool IsAuthenticated => Permissions != OpenGarrisonServerAdminPermissions.None;

    public bool HasPermission(OpenGarrisonServerAdminPermissions permissions)
    {
        return permissions == OpenGarrisonServerAdminPermissions.None
            || (Permissions & permissions) == permissions;
    }

    public static OpenGarrisonServerAdminIdentity CreateUnauthenticated(byte? sourceSlot = null)
    {
        return new OpenGarrisonServerAdminIdentity(
            "Unauthenticated",
            OpenGarrisonServerAdminAuthority.None,
            OpenGarrisonServerAdminPermissions.None,
            sourceSlot);
    }
}
