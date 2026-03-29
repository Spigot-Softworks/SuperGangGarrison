using System.Collections.Generic;
using System.Diagnostics;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed record ServerRuntimeBootstrap(
    LobbyServerRegistrar? LobbyRegistrar,
    SimulationWorld World,
    FixedStepSimulator Simulator,
    Stopwatch Clock,
    TimeSpan Previous,
    Dictionary<byte, ClientSession> ClientsBySlot,
    ServerConnectionRateLimiter ConnectionRateLimiter,
    ServerRuntimeEventReporter EventReporter,
    ServerOutboundMessaging OutboundMessaging,
    ServerSessionManager SessionManager,
    AutoBalancer AutoBalancer,
    SnapshotBroadcaster SnapshotBroadcaster,
    MapRotationManager MapRotationManager);
