namespace OpenGarrison.Server;

internal sealed class ServerAdminChatRouter(
    ServerAdminSessionManager adminSessionManager,
    Func<PluginHost?> pluginHostGetter,
    Action<byte, string> sendPrivateMessage)
{
    public bool TryHandlePrivateChatCommand(ClientSession client, string text, bool teamOnly)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("!gt_auth", StringComparison.OrdinalIgnoreCase))
        {
            return HandleAuthenticationCommand(client, trimmed);
        }

        if (trimmed.Equals("!gt_logout", StringComparison.OrdinalIgnoreCase))
        {
            ServerAdminSessionManager.Logout(client);
            sendPrivateMessage(client.Slot, "Admin session cleared.");
            return true;
        }

        if (!trimmed.StartsWith("!gt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!ServerAdminSessionManager.GetClientIdentity(client).IsAuthenticated)
        {
            adminSessionManager.BeginAuthentication(client, trimmed);
            sendPrivateMessage(client.Slot, "Admin authentication required. Use !gt_auth <password>.");
            return true;
        }

        return DispatchPrivateAdminCommand(client, trimmed, teamOnly);
    }

    private bool HandleAuthenticationCommand(ClientSession client, string commandText)
    {
        var password = commandText.Length <= "!gt_auth".Length
            ? string.Empty
            : commandText["!gt_auth".Length..].Trim();
        if (password.Length == 0)
        {
            sendPrivateMessage(client.Slot, "Usage: !gt_auth <password>");
            return true;
        }

        if (!adminSessionManager.TryAuthenticate(client, password, out var error))
        {
            sendPrivateMessage(client.Slot, error);
            return true;
        }

        sendPrivateMessage(client.Slot, "Admin session granted.");
        if (adminSessionManager.TryConsumePendingCommand(client, out var pendingCommand))
        {
            DispatchPrivateAdminCommand(client, pendingCommand, teamOnly: false);
        }

        return true;
    }

    private bool DispatchPrivateAdminCommand(ClientSession client, string commandText, bool teamOnly)
    {
        var team = teamOnly
            ? TryResolveTeam(client)
            : null;
        var pluginHost = pluginHostGetter();
        var chatEvent = new OpenGarrison.Server.Plugins.ChatReceivedEvent(
            client.Slot,
            client.Name,
            commandText,
            team,
            teamOnly);
        if (pluginHost?.TryHandleChatMessage(chatEvent) == true)
        {
            return true;
        }

        sendPrivateMessage(client.Slot, BuildUnhandledCommandMessage(commandText, pluginHost));
        return true;
    }

    private static OpenGarrison.Core.PlayerTeam? TryResolveTeam(ClientSession client)
    {
        return ServerHelpers.IsSpectatorSlot(client.Slot)
            ? null
            : null;
    }

    private static string BuildUnhandledCommandMessage(string commandText, PluginHost? pluginHost)
    {
        if (pluginHost is null)
        {
            return "[GT] admin command unavailable: plugin host offline.";
        }

        var loadedPluginIds = pluginHost.LoadedPluginIds;
        var garrisonToolsLoaded = loadedPluginIds.Any(id =>
            string.Equals(id, "open-garrison.server.lua-garrison-tools", StringComparison.OrdinalIgnoreCase));
        if (!garrisonToolsLoaded)
        {
            return loadedPluginIds.Count == 0
                ? "[GT] admin command unavailable: no server plugins loaded."
                : "[GT] admin command unavailable: GarrisonTools is not loaded.";
        }

        return $"[GT] command not handled: {commandText}. Check server console for lua-plugin errors.";
    }
}
