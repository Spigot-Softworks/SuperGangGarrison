# OpenGarrison Server Registry

Deploy `og2servers.php` to:

```text
https://unkind-dev.com/API/og2servers.php
```

Optional: edit admin token before upload if you want manual remove/admin override:

```php
const REGISTRY_WRITE_TOKEN = 'change-me';
```

Use long random value. Clients do not need token. Dedicated servers do not need token for normal heartbeat.

## Client GET

```bash
curl https://unkind-dev.com/API/og2servers.php
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
sh run-server.sh --registry-url https://unkind-dev.com/API/og2servers.php --public-host server.example.com --websocket-port 8191
```

When the browser page is served over HTTPS, the game socket must be reachable as `wss://`. If the public browser URL is not `wss://server.example.com:8191/opengarrison/ws`, publish the external URL explicitly:

```bash
sh run-server.sh --registry-url https://unkind-dev.com/API/og2servers.php --public-host server.example.com --websocket-port 8191 --public-websocket-url wss://server.example.com/opengarrison/ws
```

Terminate TLS at a reverse proxy or pass `--websocket-cert cert.pfx` to the built-in listener.

If `--public-host` is omitted, PHP registry uses request IP. Use explicit host when server sits behind proxy, NAT, or DNS name.

Registry accepts public writes with guardrails:

- Hostname must resolve to request IP unless admin token is provided.
- Max 8 active servers per request IP.
- Heartbeat per server is rate-limited to one write per 10 seconds.
- Entries expire after 120 seconds.

```bash
curl -X POST https://unkind-dev.com/API/og2servers.php \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Test Server\",\"host\":\"server.example.com\",\"udpPort\":8190,\"webSocketPort\":8191,\"webSocketUrl\":\"wss://server.example.com/opengarrison/ws\",\"map\":\"ctf_orange\",\"mode\":\"CTF\",\"players\":2,\"maxPlayers\":16,\"spectators\":0,\"protocolVersion\":38}"
```

Admin remove needs `REGISTRY_WRITE_TOKEN` configured:

```bash
curl -X POST https://unkind-dev.com/API/og2servers.php \
  -H "Content-Type: application/json" \
  -d "{\"token\":\"YOUR_TOKEN\",\"action\":\"remove\",\"serverId\":\"test-1\"}"
```

## Storage

Script creates:

```text
api/og2servers-data/servers.json
api/og2servers-data/servers.json.lock
api/og2servers-data/.htaccess
```

No MySQL needed. `.htaccess` denies direct reads on Apache. Public API only exposes sanitized server fields.
