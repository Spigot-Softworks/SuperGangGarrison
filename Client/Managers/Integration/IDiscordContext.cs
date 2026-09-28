#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IDiscordContext
{
    string ResolveDiscordApplicationId();
    DiscordRPC.RichPresence BuildDiscordRichPresencePayload(System.DateTime startTimestampUtc);
    string BuildDiscordRichPresenceState();
}
