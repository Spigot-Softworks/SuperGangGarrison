# OpenGarrison API

Small Linux-hosted API for:

- public server discovery at `/api/servers`
- anonymous client registration at `/api/client/register`
- friend presence at `/api/presence`
- direct-session rendezvous by OG2 friend code
- private two-peer Protocol64 relay sessions at `/api/relay`
- static updater files served by the reverse proxy under `/updates/`

The service uses SQLite and expects a reverse proxy such as Caddy or nginx in front of it.
Server discovery rows include `protocolVersion`, `buildVersion`, `releaseChannel`, and
`compatibilityKey` so stable and beta builds can share the registry without appearing to
incompatible clients.

Last to Die co-op creates a short-lived authenticated relay session before launching the
local child server. The server and guest both make outbound WebSocket connections through
the API, so neither player needs an inbound router mapping. Gameplay stays server
authoritative and uses the normal Protocol64 prediction/reconciliation path; the relay only
forwards complete binary frames.

Direct UDP advertisement remains available as a fallback when relay creation is unavailable.
The relay runtime is currently in-process, so deploy uvicorn with one worker. A service restart
drops active relay sockets, after which the child server retries until the session expires.

## Environment

```text
OPENGARRISON_API_DB=/var/lib/opengarrison-api/opengarrison.db
OPENGARRISON_API_CORS_ORIGINS=https://superganggarrison.com,https://www.superganggarrison.com,https://play.superganggarrison.com,https://unkind-dev.com,https://www.unkind-dev.com
OPENGARRISON_REGISTRY_TOKEN=optional-admin-token
OPENGARRISON_RELAY_PUBLIC_BASE_URL=https://api.superganggarrison.com
OPENGARRISON_RELAY_SESSION_TTL_SECONDS=43200
```

## Run Locally

```bash
python3 -m venv .venv
. .venv/bin/activate
pip install -r requirements.txt
uvicorn app:app --host 127.0.0.1 --port 8008
```

The reverse proxy must pass WebSocket upgrades for `/api/relay/ws/*`. Caddy's
`reverse_proxy` does this automatically. Keep a single API process until relay session
state is moved to a shared broker.
