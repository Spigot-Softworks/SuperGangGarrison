# OpenGarrison API

Small Linux-hosted API for:

- public server discovery at `/api/servers`
- anonymous client registration at `/api/client/register`
- friend presence at `/api/presence`
- recoverable accounts and player profiles at `/api/account`
- scoped gameplay sessions and idempotent stat awards
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

Each relay session receives a collision-checked four-character room code. Guests can resolve
the session with either `GET /api/relay/room/{roomCode}` or
`GET /api/relay/friend/{friendCode}`. Resolution remains unavailable until the host relay
WebSocket is connected, and lookup attempts are rate-limited per observed address. The short
room code is only a locator; the returned WebSocket URL carries the full guest bearer token.

Last to Die co-op requires a relay session; the client does not silently fall back to a
port-forwarded UDP room when relay creation is unavailable. Direct UDP advertisement remains
available for other explicitly direct-hosted sessions.
The relay runtime is currently in-process, so deploy uvicorn with one worker. A service restart
drops active relay sockets, after which the child server retries until the session expires.

## Accounts

New clients use eight-character friend codes such as `OG2-ABCD-EFGH`. Existing longer
codes remain valid aliases after migration. A client installation is a device, not the
account itself: every device keeps a separate random client secret.

`POST /api/account/protect` generates an eight-character recovery key and returns it once.
`POST /api/account/login` links a new device after checking that key. Recovery keys are
stored as salted scrypt hashes and failed login attempts are rate-limited per account and
observed address. Account profile responses contain the canonical friend code, player card,
lifetime points, wallet balance, and a monotonically increasing profile revision.

The legacy `clients` table is retained as migration input. Startup idempotently copies its
rows into `accounts`, `account_friend_codes`, and `client_devices`; no existing friend code
or device secret is invalidated.

`POST /api/game-session/create` exchanges device authentication for a six-hour scoped
gameplay token. Game servers may validate it with `POST /api/game-session/validate` and
submit bounded, idempotent awards through `POST /api/stats/award`. The persistent profile
is reconciled against the stat and wallet ledgers whenever it is read, so editing only the
cached totals cannot manufacture points or credits.

Authenticated players can read their points, rank, and accumulated stat counters through
`POST /api/stats/points`. The public positive-score leaderboard is paged through
`GET /api/stats/leaderboard?limit=10&offset=0`; tied scores share a rank.

Last to Die results use their own immutable run ledger. Authoritative game hosts submit one
result per account and run through `POST /api/last-to-die/run`; exact retries are idempotent
and conflicting reuse is rejected. `POST /api/last-to-die/rankings` returns the authenticated
player's best score, highest round, and both global record lists for the in-game Rankings
screen. External clients can page the public records with
`GET /api/last-to-die/leaderboard?sort=score|round&limit=10&offset=0`.

## Environment

```text
OPENGARRISON_API_DB=/var/lib/opengarrison-api/opengarrison.db
OPENGARRISON_API_CORS_ORIGINS=https://superganggarrison.com,https://www.superganggarrison.com,https://play.superganggarrison.com,https://unkind-dev.com,https://www.unkind-dev.com
OPENGARRISON_REGISTRY_TOKEN=optional-admin-token
OPENGARRISON_RELAY_PUBLIC_BASE_URL=https://api.superganggarrison.com
OPENGARRISON_RELAY_SESSION_TTL_SECONDS=43200
```

Dedicated game servers use `OPENGARRISON_API_BASE_URL` to override the account/stat API
origin; when omitted they use the normal OpenGarrison API default.

## Run Locally

```bash
python3 -m venv .venv
. .venv/bin/activate
pip install -r requirements.txt
uvicorn app:app --host 127.0.0.1 --port 8008
```

Run the API test suite from this directory with:

```bash
python -m unittest discover -p 'test_*.py' -v
```

The reverse proxy must pass WebSocket upgrades for `/api/relay/ws/*`. Caddy's
`reverse_proxy` does this automatically. Keep a single API process until relay session
state is moved to a shared broker.

The optional Practice/Last to Die browser edition adds managed private rooms at
`/api/private-rooms/*`. Enable them only with a matching native room release and
the separate worker. See [browser edition deployment](deploy/browser-edition/README.md).
Existing desktop relay endpoints and account tables remain available.
