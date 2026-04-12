# Browser Networking Gap

The browser build currently supports offline practice. Multiplayer still needs a browser-safe transport.

## Current Shape

- `Protocol/ProtocolCodec.cs` is reusable: gameplay messages already serialize to and from byte arrays.
- The desktop client transport is UDP-specific in `Client/Networking/NetworkGameClient.cs`.
- The dedicated server binds UDP directly in `Server/Runtime/GameServer.Startup.cs`.
- Server packet ingress/egress is UDP and `IPEndPoint` based through `Server/Networking/ServerIncomingPacketPump.cs`, `Server/Networking/ServerOutboundMessaging.cs`, and `Server/Networking/ClientSession.cs`.
- Lobby browsing also has native socket paths that will need a browser-specific strategy.

## Preferred Direction

WebTransport is the best fit for browser gameplay because it supports client/server communication over HTTP/3 and can expose unreliable datagrams plus reliable streams. That is closer to the current UDP gameplay model than WebSockets.

Do not bolt WebTransport directly into the existing UDP classes. First extract a transport boundary that can carry protocol byte payloads and identify peers without assuming `IPEndPoint`.

Recommended sequence:

1. Introduce a small transport abstraction for datagram send/receive, peer identity, disconnect, and diagnostics.
2. Move the current UDP client/server code behind that abstraction without changing gameplay behavior.
3. Add a browser WebTransport client adapter that forwards `Uint8Array` datagrams into the same protocol codec path.
4. Add a server WebTransport adapter that maps WebTransport sessions to stable peer identities.
5. Decide whether WebSockets are a fallback-only path for networks or browsers without WebTransport support.
6. Add multiplayer smoke/soak tests for connect, join, input, snapshots, reconnect, timeout, and map transitions.

## Release Risk

WebTransport requires deployment work beyond game code: HTTPS, HTTP/3, certificate handling, hosting/proxy support, and fallback behavior. Treat browser multiplayer as a separate release track from offline browser practice until this transport work is complete.
