# OpenGarrison Server Registry

Production registry endpoint:

```text
https://api.superganggarrison.com/api/servers
```

The [API service](../../services/opengarrison-api/README.md) implements the registry
and accepts the legacy `/API/og2servers.php` route. The PHP script in this
directory is a standalone compatibility fallback.

Clients do not need a token. Dedicated servers do not need a token for normal heartbeat.

Clients hide registry rows whose `protocolVersion`, `releaseChannel`, and `compatibilityKey`
do not match the running package. This lets stable and beta servers share one public registry
without advertising incompatible builds to each other.

## Client GET

```bash
curl https://api.superganggarrison.com/api/servers
```

A request without compatibility filters defaults to the stable channel. Clients
should send their package's channel, protocol version, and compatibility key.
For example, with values supplied by a beta package:

```bash
curl --get https://api.superganggarrison.com/api/servers \
  --data-urlencode "releaseChannel=beta" \
  --data-urlencode "protocolVersion=$PROTOCOL_VERSION" \
  --data-urlencode "compatibilityKey=$COMPATIBILITY_KEY"
```

`PROTOCOL_VERSION` and `COMPATIBILITY_KEY` above must match the package you are
querying for. The source protocol version is defined in
[ProtocolVersion.cs](../../Protocol/ProtocolVersion.cs). *Protocol64* is the
framing implementation's name, not the current compatibility version.

Illustrative response (the version and timestamp are example values):

```json
{
  "servers": [
    {
      "name": "Test Server",
      "host": "server.example.com",
      "udpPort": 8190,
      "webSocketPort": 8191,
      "webSocketUrl": "wss://server.example.com/opengarrison/ws",
      "private": false,
      "map": "ctf_orange",
      "mode": "CTF",
      "players": 2,
      "maxPlayers": 16,
      "spectators": 0,
      "protocolVersion": 102,
      "buildVersion": "1.0.2",
      "releaseChannel": "stable",
      "compatibilityKey": "stable:1.0.2:102",
      "lastSeenIso": "2026-09-19T12:00:00+00:00"
    }
  ],
  "generatedAt": "2026-09-19T12:00:00+00:00"
}
```

## Server Heartbeat

Send every 30 seconds. Entries expire after 120 seconds.

From an extracted Linux release package, the dedicated server can publish automatically:

```bash
sh run-server.sh --public-host server.example.com
```

Optional overrides:

```bash
sh run-server.sh --registry-url https://api.superganggarrison.com/api/servers --public-host server.example.com --websocket-port 8191
```

When the browser page is served over HTTPS, the game socket must be reachable as `wss://`. If the public browser URL is not `wss://server.example.com:8191/opengarrison/ws`, publish the external URL explicitly:

```bash
sh run-server.sh --registry-url https://api.superganggarrison.com/api/servers --public-host server.example.com --websocket-port 8191 --public-websocket-url wss://server.example.com/opengarrison/ws
```

Terminate TLS at a reverse proxy or pass `--websocket-cert cert.pfx` to the built-in listener.

If `--public-host` is omitted, the registry uses request IP. Use explicit host when server sits behind proxy, NAT, or DNS name.

Registry accepts public writes with guardrails:

- Max 8 active servers per request IP.
- Entries expire after 120 seconds.

To register manually, save the server fields from the example to a local
`heartbeat.json` file, replace the illustrative values with the running server's
identity, then send:

```bash
curl -X POST https://api.superganggarrison.com/api/servers \
  -H "Content-Type: application/json" \
  --data-binary @heartbeat.json
```

Admin remove needs `OPENGARRISON_REGISTRY_TOKEN` configured on the service:

```bash
curl -X POST https://api.superganggarrison.com/api/servers \
  -H "Content-Type: application/json" \
  -d "{\"token\":\"YOUR_TOKEN\",\"action\":\"remove\",\"serverId\":\"test-1\"}"
```

## Storage

The service's default SQLite path is:

```text
/var/lib/opengarrison-api/opengarrison.db
```

Set `OPENGARRISON_API_DB` to override this path. Public responses expose the
server fields used for discovery, not account credentials.
