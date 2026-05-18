# OpenGarrison Server Registry

Production registry endpoint:

```text
https://api.unkind-dev.com/api/servers
```

The old `og2servers.php` script remains in this folder as a lightweight standalone fallback. The current deployed backend lives in `services/opengarrison-api` and also accepts the legacy `/API/og2servers.php` route for compatibility.

Clients do not need a token. Dedicated servers do not need a token for normal heartbeat.

## Client GET

```bash
curl https://api.unkind-dev.com/api/servers
```

Response shape:

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
      "protocolVersion": 38,
      "lastSeenIso": "2026-04-14T12:00:00+00:00"
    }
  ],
  "generatedAt": "2026-04-14T12:00:00+00:00"
}
```

## Server Heartbeat

Send every 30 seconds. Entries expire after 120 seconds.

Dedicated server can publish automatically:

```bash
sh run-server.sh --public-host server.example.com
```

Optional overrides:

```bash
sh run-server.sh --registry-url https://api.unkind-dev.com/api/servers --public-host server.example.com --websocket-port 8191
```

When the browser page is served over HTTPS, the game socket must be reachable as `wss://`. If the public browser URL is not `wss://server.example.com:8191/opengarrison/ws`, publish the external URL explicitly:

```bash
sh run-server.sh --registry-url https://api.unkind-dev.com/api/servers --public-host server.example.com --websocket-port 8191 --public-websocket-url wss://server.example.com/opengarrison/ws
```

Terminate TLS at a reverse proxy or pass `--websocket-cert cert.pfx` to the built-in listener.

If `--public-host` is omitted, the registry uses request IP. Use explicit host when server sits behind proxy, NAT, or DNS name.

Registry accepts public writes with guardrails:

- Max 8 active servers per request IP.
- Entries expire after 120 seconds.

```bash
curl -X POST https://api.unkind-dev.com/api/servers \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Test Server\",\"host\":\"server.example.com\",\"udpPort\":8190,\"webSocketPort\":8191,\"webSocketUrl\":\"wss://server.example.com/opengarrison/ws\",\"map\":\"ctf_orange\",\"mode\":\"CTF\",\"players\":2,\"maxPlayers\":16,\"spectators\":0,\"protocolVersion\":38}"
```

Admin remove needs `OPENGARRISON_REGISTRY_TOKEN` configured on the service:

```bash
curl -X POST https://api.unkind-dev.com/api/servers \
  -H "Content-Type: application/json" \
  -d "{\"token\":\"YOUR_TOKEN\",\"action\":\"remove\",\"serverId\":\"test-1\"}"
```

## Storage

The deployed service stores registry rows in:

```text
/var/lib/opengarrison-api/opengarrison.db
```

No MySQL needed. Public API only exposes sanitized server fields.
