using System.Collections.Generic;
using System.Diagnostics;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed record ServerRuntimeBootstrap(
    LobbyServerRegistrar? LobbyRegistrar,
    SimulationWorld World,
    FixedStepSimulator Simulator,
    ServerClock Clock,
    TimeSpan Previous,
    Dictionary<byte, ClientSession> ClientsBySlot,
    ServerConnectionRateLimiter ConnectionRateLimiter,
    ServerRuntimeEventReporter EventReporter,
    ServerOutboundMessaging OutboundMessaging,
    ServerSessionManager SessionManager,
    AutoBalancer AutoBalancer,
    SnapshotBroadcaster SnapshotBroadcaster,
    MapRotationManager MapRotationManager,
    ServerBotManager BotManager,
    ServerDemoRecorder DemoRecorder);
