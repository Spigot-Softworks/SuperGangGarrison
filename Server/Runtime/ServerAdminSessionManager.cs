using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerAdminSessionManager(
    string? rconPassword,
    Func<TimeSpan> uptimeGetter)
{
    private static readonly TimeSpan PendingCommandLifetime = TimeSpan.FromMinutes(2);
    private string? _rconPassword = NormalizePassword(rconPassword);

    public OpenGarrisonServerAdminIdentity ConsoleIdentity { get; } = new(
        "Console",
        OpenGarrisonServerAdminAuthority.HostConsole,
        OpenGarrisonServerAdminPermissions.FullAccess);

    public OpenGarrisonServerAdminIdentity AdminPipeIdentity { get; } = new(
        "AdminPipe",
        OpenGarrisonServerAdminAuthority.AdminPipe,
        OpenGarrisonServerAdminPermissions.FullAccess);

    public string RconPassword
    {
        get => _rconPassword ?? string.Empty;
        set => _rconPassword = NormalizePassword(value);
    }

    public static OpenGarrisonServerAdminIdentity GetClientIdentity(ClientSession client)
    {
        return client.AdminPermissions == OpenGarrisonServerAdminPermissions.None
            ? OpenGarrisonServerAdminIdentity.CreateUnauthenticated(client.Slot)
            : new OpenGarrisonServerAdminIdentity(
                client.Name,
                OpenGarrisonServerAdminAuthority.RconSession,
                client.AdminPermissions,
                client.Slot);
    }

    public bool BeginAuthentication(ClientSession client, string pendingCommand)
    {
        client.PendingAdminChatCommand = pendingCommand.Trim();
        client.PendingAdminChatCommandQueuedAt = uptimeGetter();
        return true;
    }

    public bool TryAuthenticate(ClientSession client, string submittedPassword, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(_rconPassword))
        {
            error = "Server rcon password is not configured.";
            return false;
        }

        if (!string.Equals(submittedPassword, _rconPassword, StringComparison.Ordinal))
        {
            error = "Incorrect admin password.";
            return false;
        }

        client.AdminPermissions = OpenGarrisonServerAdminPermissions.FullAccess;
        client.AdminAuthenticatedAt = uptimeGetter();
        return true;
    }

    public static void Logout(ClientSession client)
    {
        client.AdminPermissions = OpenGarrisonServerAdminPermissions.None;
        client.AdminAuthenticatedAt = TimeSpan.MinValue;
        client.PendingAdminChatCommand = string.Empty;
        client.PendingAdminChatCommandQueuedAt = TimeSpan.MinValue;
    }

    public bool TryConsumePendingCommand(ClientSession client, out string pendingCommand)
    {
        pendingCommand = string.Empty;
        if (string.IsNullOrWhiteSpace(client.PendingAdminChatCommand))
        {
            return false;
        }

        if (client.PendingAdminChatCommandQueuedAt > TimeSpan.MinValue
            && uptimeGetter() - client.PendingAdminChatCommandQueuedAt > PendingCommandLifetime)
        {
            client.PendingAdminChatCommand = string.Empty;
            client.PendingAdminChatCommandQueuedAt = TimeSpan.MinValue;
            return false;
        }

        pendingCommand = client.PendingAdminChatCommand;
        client.PendingAdminChatCommand = string.Empty;
        client.PendingAdminChatCommandQueuedAt = TimeSpan.MinValue;
        return true;
    }

    private static string? NormalizePassword(string? password)
    {
        return string.IsNullOrWhiteSpace(password) ? null : password;
    }
}
