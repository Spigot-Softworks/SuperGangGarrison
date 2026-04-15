# Browser Networking

The browser client uses WebSocket for multiplayer. Desktop clients continue to use UDP.

## Current Shape

- `Protocol/ProtocolCodec.cs` is shared by desktop, browser, and server networking.
- Desktop clients use UDP through the shared message transport boundary.
- Browser clients use one binary WebSocket message per protocol payload.
- The server accepts both UDP peers and WebSocket peers through a composite message transport.
- Reliable stream transports coalesce snapshots so stale snapshots do not build up behind slow browser clients.

## Browser Server Setup

Enable the WebSocket listener with:

```bash
sh run-server.sh --websocket-port 8191
```

For HTTPS-hosted browser builds, expose the game socket as `wss://`. Either terminate TLS at a reverse proxy or start the built-in listener with a PKCS#12 certificate:

```bash
sh run-server.sh --websocket-port 8191 --websocket-cert cert.pfx
```

Browser registry entries can publish either `webSocketUrl` or `webSocketPort`. Browser clients prefer `webSocketUrl` when present. Use `webSocketPort` only when the browser can directly reach `ws://host:port/opengarrison/ws` or `wss://host:port/opengarrison/ws`.

For reverse-proxy production hosting, publish the external browser URL:

```bash
sh run-server.sh --websocket-port 8191 --public-websocket-url wss://server.example.com/opengarrison/ws
```

Desktop clients continue to use `udpPort`.

## Transport Policy

WebSocket is reliable and ordered, so it must not be treated as UDP. The server keeps reliable control messages bounded and keeps only the newest pending snapshot for each WebSocket peer. This avoids replaying stale world state after a browser client stalls.

Messages that must remain reliable include connection setup, password flow, chat, control acknowledgements, session changes, and plugin messages. Snapshots are latest-wins on WebSocket peers.
