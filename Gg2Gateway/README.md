# GG2 browser gateway

Browsers cannot open GG2's TCP game socket. This service connects to a public,
advertised GG2 server for each browser WebSocket, runs the same GG2 translator as
the desktop client, and relays the translated game messages. It also serves
verified custom map PNGs to the browser. The stock GG2 PNGs ship with the website.

Run locally from the repository root:

```powershell
dotnet run --project Gg2Gateway/OpenGarrison.Gg2Gateway.csproj
```

The default listener is `http://127.0.0.1:8768`. Set
`OPENGARRISON_GG2_PUBLIC_ORIGIN` to the browser-visible HTTPS API origin before
deploying. Reverse proxy `/api/gg2/ws/` with WebSocket upgrades and
`/api/gg2/maps/` to this listener. Leave `/api/gg2/servers` on the existing
Python API, or route it here. The gateway accepts only compatible public servers
in the GG2 lobby. `OPENGARRISON_GG2_ALLOW_LOOPBACK=true` permits local fake-server
tests and must not be set on a public deployment.

Other configuration:

- `OPENGARRISON_GG2_GATEWAY_URLS`: local listener URL.
- `OPENGARRISON_GG2_MAP_CACHE`: writable directory for verified GG2 custom maps.
- `OPENGARRISON_GG2_ALLOWED_ORIGINS`: comma-separated browser origins allowed to
  open the gateway WebSocket or fetch maps.

`python Tests/BrowserSmoke/gg2-gateway-smoke.py` checks a live GG2 handshake
through the gateway. With loopback testing enabled,
`python Tests/BrowserSmoke/gg2-gateway-custom-map-smoke.py` checks a GG2 map
download and the browser-visible PNG endpoint. Run the AOT browser smoke test against a locally served
limited browser build to check menu, join, spawn and movement.
