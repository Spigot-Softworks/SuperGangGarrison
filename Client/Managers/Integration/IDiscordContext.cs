#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IDiscordContext
{
#if !BROWSER_KNI
    string ResolveDiscordApplicationId();
    DiscordRPC.RichPresence BuildDiscordRichPresencePayload(System.DateTime startTimestampUtc);
    string BuildDiscordRichPresenceState();
#endif
}
