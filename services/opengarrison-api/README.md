# OpenGarrison API

API service for:

- public server discovery at `/api/servers`
- anonymous client registration at `/api/client/register`
- friend presence at `/api/presence`
- recoverable accounts and player profiles at `/api/account`
- scoped gameplay sessions and idempotent stat awards
- direct-session rendezvous by OG2 friend code
- player-hosted room admission, signaling, and fallback relay at `/api/peer-rooms`
- legacy two-peer Protocol64 relay sessions at `/api/relay`
- static updater files served by the reverse proxy under `/updates/`

The service uses SQLite and expects a reverse proxy such as Caddy or nginx in front of it.
Server discovery rows include `protocolVersion`, `buildVersion`, `releaseChannel`, and
`compatibilityKey` so stable and beta builds can share the registry without appearing to
incompatible clients.

## Rooms and relay

Player-hosted Practice and Last to Die use `/api/peer-rooms` for room codes,
admission, WebRTC signaling, and a WebSocket fallback. The creator's client runs
the match; the API does not launch a game server. See the
[room deployment guide](deploy/browser-edition/README.md) for matching browser and
native releases.

The older `/api/relay` endpoints remain available for two-peer sessions. In that
path the host and guest both connect outbound through the API, which forwards
complete binary frames.

Each relay session receives a collision-checked four-character room code. Guests can resolve
the session with either `GET /api/relay/room/{roomCode}` or
`GET /api/relay/friend/{friendCode}`. Resolution remains unavailable until the host relay
WebSocket is connected, and lookup attempts are rate-limited per observed address. The short
room code is only a locator; the returned WebSocket URL carries the full guest bearer token.

Direct UDP advertisement remains available for direct-hosted sessions.
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
submit bounded, idempotent awards through `POST /api/stats/award` only when they also
provide the server-only reward authority key. A player token cannot authorize rewards. The persistent profile
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

## Run locally

From the repository root, create a development environment and a writable local database:

```bash
cd services/opengarrison-api
mkdir -p ../../.local/api
python3 -m venv ../../.local/api/venv
. ../../.local/api/venv/bin/activate
pip install -r requirements.txt
export OPENGARRISON_API_DB="$PWD/../../.local/api/opengarrison-dev.db"
uvicorn app:app --host 127.0.0.1 --port 8008
```

Run the API test suite from this directory with:

```bash
pip install -r requirements-test.txt
python -m unittest discover -p 'test_*.py' -v
```

The reverse proxy must pass WebSocket upgrades for `/api/relay/ws/*`. Caddy's
`reverse_proxy` does this automatically. Keep a single API process until relay session
state is moved to a shared broker.

The older managed-room API at `/api/private-rooms/*` requires its separate room
worker. It is a compatibility path for older clients; player-hosted rooms use
`/api/peer-rooms/*` and do not require that worker.

## Verified offline and player-hosted Last to Die runs

Solo and player-hosted co-op use the same Last to Die leaderboard as trusted servers.
The client records starting settings and gameplay inputs, saves the completed recording
locally, and uploads it when the API is available. Browser recordings use IndexedDB;
desktop recordings use `pending-runs` inside the user data directory. Successful
verification removes the local pending copy. Rejected recordings stay local with a reason.
Clearing browser site data also removes pending recordings.

The API queues recordings; a separate worker replays them in the installed game runtime
and writes the computed result to the existing run ledger. The host submits one recording;
co-op guests claim their own result after it is verified. A gameplay token identifies the
submitting device but supplies no trusted score. Existing leaderboard entries are retained.

### Server setup

1. Set `OPENGARRISON_REWARD_AUTHORITY_KEY` to a strong random secret on the API and each
   trusted dedicated server. Keep it out of clients, archives and source control. The API
   requires `X-OpenGarrison-Reward-Key` on `/api/stats/award` and `/api/last-to-die/run`;
   leaving the secret unset disables those direct reward endpoints.
2. Set `RELEASE_VERSION` to the exact release version passed to the client packaging
   script. Publish the verifier from that same source release, including its content:

   ```bash
   dotnet publish Tools/RunVerifier/OpenGarrison.Tools.RunVerifier.csproj -c Release -r linux-x64 --self-contained true -p:InformationalVersion="$RELEASE_VERSION" -p:IncludeSourceRevisionInInformationalVersion=false -o artifacts/run-verifier
   artifacts/run-verifier/OpenGarrison.Tools.RunVerifier --ruleset
   ```

3. Install that directory outside the web root. Set `OPENGARRISON_REPLAY_RULESET` to the
   exact output above in both the API and worker environments. Start the worker from
   `services/opengarrison-api` with the same `OPENGARRISON_API_DB` as the API:

   ```bash
   python run_verification_worker.py --verifier /opt/opengarrison/run-verifier/OpenGarrison.Tools.RunVerifier
   ```

   Use `--once` to process at most one job for a deployment check. Supervise the continuous
   worker as a service with an unprivileged account, a writable temporary directory,
   read-only verifier/content files, and a memory limit appropriate to the host. The
   worker needs database access; the game subprocess needs no API credentials or network.
4. Allow 16 MiB request bodies on `/api/last-to-die/recordings`. The supplied
   [Nginx locations](deploy/browser-edition/nginx-locations.conf) include this route.
   Deploy the updated API modules, worker and matching client together. Until configured,
   the API returns 503 for uploads and clients retain recordings for retry.

Uploads are limited to 16 MiB compressed, 128 MiB expanded, two million events and eight
hours of session history. Host console commands invalidate ranked recording for that
session. Custom maps and modified rules are not accepted by the stock verifier. Each job
has a one-hour execution timeout, retry leases and idempotent publication. Monitor jobs
with `failed` status; they retain the uploaded recording for operator recovery. After
fixing an infrastructure problem, an operator can reset those jobs to `pending` and
`attempts=0` in the database.

Keep older verifier installations for offline runs completed before an update. The API's
`OPENGARRISON_REPLAY_RULESETS` accepts additional comma-separated ruleset IDs; run one
worker per supported release, each with its own `OPENGARRISON_REPLAY_RULESET` and matching
executable. Change the build version when releasing gameplay or content changes. An
unsupported recording remains on the client rather than being awarded a guessed score.

Replay verification checks that a result follows the installed rules. It does not prove
that a human played in real time or prevent tool-assisted inputs. The API never accepts
uploaded executables, plugins, maps or caller-supplied totals as proof of a score.
