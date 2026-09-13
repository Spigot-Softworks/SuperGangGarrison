#nullable enable

using OpenGarrison.Protocol;

namespace OpenGarrison.Server;

internal sealed partial class ServerOutboundMessaging
{
    private PlayerStatsService? _statsService;

    public void ConfigureStats(PlayerStatsService statsService)
    {
        _statsService = statsService;
    }

    public bool TryHandlePointsChatCommand(ClientSession client, string text)
        => _statsService?.TryHandleChatCommand(client, text) == true;

    public void SendStatsMessage(ClientSession client, IProtocolMessage message)
    {
        if (client.IsAuthorized)
        {
            TrySendMessage(client.Peer, message, "stats message");
        }
    }

    public void SendStatsSystemMessage(byte slot, string text) => SendSystemMessage(slot, text);
}
