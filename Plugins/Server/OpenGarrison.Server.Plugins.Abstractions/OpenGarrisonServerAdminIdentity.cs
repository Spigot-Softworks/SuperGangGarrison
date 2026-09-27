using System;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Admin permissions that can be granted to a command or message source.
/// </summary>
[Flags]
public enum OpenGarrisonServerAdminPermissions
{
    /// <summary>No permissions.</summary>
    None = 0,
    /// <summary>May view server state.</summary>
    ViewServerState = 1 << 0,
    /// <summary>May manage players (kick, ban, mute).</summary>
    ManagePlayers = 1 << 1,
    /// <summary>May manage the match (map changes, limits).</summary>
    ManageMatch = 1 << 2,
    /// <summary>May manage server configuration (cvars).</summary>
    ManageServerConfiguration = 1 << 3,
    /// <summary>May manage plugins.</summary>
    ManagePlugins = 1 << 4,
    /// <summary>May manage the scheduler.</summary>
    ManageScheduler = 1 << 5,
    /// <summary>All admin permissions.</summary>
    FullAccess = ViewServerState
        | ManagePlayers
        | ManageMatch
        | ManageServerConfiguration
        | ManagePlugins
        | ManageScheduler,
}

/// <summary>
/// The authority that authenticated an admin identity.
/// </summary>
public enum OpenGarrisonServerAdminAuthority
{
    /// <summary>No authority (unauthenticated).</summary>
    None = 0,
    /// <summary>The host console.</summary>
    HostConsole,
    /// <summary>The admin pipe.</summary>
    AdminPipe,
    /// <summary>An RCON session.</summary>
    RconSession,
    /// <summary>The plugin host.</summary>
    PluginHost,
    /// <summary>Server configuration (for example configured admins).</summary>
    ServerConfiguration,
}

/// <summary>
/// Where a server command was invoked from.
/// </summary>
public enum OpenGarrisonServerCommandSource
{
    /// <summary>The server console.</summary>
    Console = 0,
    /// <summary>The admin pipe.</summary>
    AdminPipe,
    /// <summary>Private chat.</summary>
    PrivateChat,
    /// <summary>A plugin.</summary>
    Plugin,
    /// <summary>Internal server use.</summary>
    Internal,
}

/// <summary>
/// The admin identity of a command or chat message source.
/// </summary>
/// <param name="DisplayName">The display name.</param>
/// <param name="Authority">The authenticating authority.</param>
/// <param name="Permissions">The granted permissions.</param>
/// <param name="SourceSlot">The source player slot, if any.</param>
public readonly record struct OpenGarrisonServerAdminIdentity(
    string DisplayName,
    OpenGarrisonServerAdminAuthority Authority,
    OpenGarrisonServerAdminPermissions Permissions,
    byte? SourceSlot = null)
{
    /// <summary>
    /// Gets whether the identity is authenticated (has any permissions).
    /// </summary>
    public bool IsAuthenticated => Permissions != OpenGarrisonServerAdminPermissions.None;

    /// <summary>
    /// Gets whether the identity has all of the given permissions.
    /// </summary>
    /// <param name="permissions">The required permissions.</param>
    /// <returns>True when all required permissions are granted.</returns>
    public bool HasPermission(OpenGarrisonServerAdminPermissions permissions)
    {
        return permissions == OpenGarrisonServerAdminPermissions.None
            || (Permissions & permissions) == permissions;
    }

    /// <summary>
    /// Creates an unauthenticated identity.
    /// </summary>
    /// <param name="sourceSlot">The source player slot, if any.</param>
    /// <returns>An unauthenticated identity.</returns>
    public static OpenGarrisonServerAdminIdentity CreateUnauthenticated(byte? sourceSlot = null)
    {
        return new OpenGarrisonServerAdminIdentity(
            "Unauthenticated",
            OpenGarrisonServerAdminAuthority.None,
            OpenGarrisonServerAdminPermissions.None,
            sourceSlot);
    }
}
