# Player-hosted browser and desktop rooms

Last to Die Solo runs locally. In Last to Die and Practice co-op,
the creator's game runs the match for up to four humans. The existing API handles
room codes, admission and WebRTC signaling. It relays packets when a direct
connection is unavailable; it never launches a game process for these rooms.
Practice Singleplayer remains local. Only Practice, Last to Die and Settings
appear in the browser's main menu.

## Build the deployment artifacts

From the repository root, after installing the browser prerequisites in the
[browser guide](../../../../Client.Browser/README.md), run:

```powershell
python .\Tools\Browser\package-web-edition.py --version 1.0.2
```

Choose the release version you intend to publish; `1.0.2` is an example. The tool
generates `superganggarrison-browser-aot.zip`,
`superganggarrison-room-api-update.tar.gz`, and `release.json` under
`artifacts/browser-practice-ltd/`. These are generated outputs, not repository
files. Keep the website and API artifacts from the same build together.

## Update the API

1. Preserve the existing API database, environment, secrets, update manifests and
   account data. Keep a copy of the current API source and website for rollback.
2. Copy the Python files and requirements from the API update archive into the
   existing API installation. **Include `peer_rooms.py`** alongside `app.py`,
   `private_rooms.py`, `private_room_store.py` and `room_worker.py`; the latter
   files preserve compatibility with older clients.
3. Install requirements in the existing virtual environment and restart the
   existing API service. There is no new service, database migration, game-server
   executable or room worker required for the new room path.
4. Keep one API worker: room admission and signaling state live in that process.
   Keep the public origin set to `https://api.superganggarrison.com` and include
   the website's origin in the existing CORS setting. Merge the optional values
   from `peer-rooms.env.example` into the existing environment; do not replace it.
5. Forward `/api/peer-rooms/` to the existing API, including WebSocket upgrades.
   Merge the supplied Nginx location, then validate and reload Nginx. For Caddy,
   retain the existing API reverse proxy and merge the admission-log exclusion.

The managed room worker is not used by player-hosted rooms. It may remain running to
serve old clients. Once those rooms have drained, it can be stopped independently.
The old `OPENGARRISON_ROOM_CAPACITY` setting does not limit player-hosted rooms.
`OPENGARRISON_PEER_ROOM_LIMIT` defaults to 4096 lightweight room records and can be
raised after measuring API memory and fallback bandwidth. This is separate from
CPU and memory needed to run gameplay on the creator's device.

Direct connections use ICE. The default supplies a public STUN server; an optional
`OPENGARRISON_ICE_SERVERS` JSON array can supply STUN/TURN configuration. Without a
working direct connection, the existing authenticated WebSocket relay carries the
match. Relay bandwidth still uses the API host. WebRTC data channels and the
fallback preserve separate admission for each guest.

The host must keep the game open. If the host leaves, the room closes; host
migration is not included. Background browsers may throttle the creator's game.
LTD accepts new players in its lobby and reconnects existing participants during
a run. Practice also allows late joining. The host can return everyone to the
lobby, edit Practice settings and start another match with the same room code.

## Upload the website

Unzip `superganggarrison-browser-aot.zip` into a new static document root and switch
the `https://superganggarrison.com/` site to that directory atomically. Do not upload
the parent build directory or backend archive into the public document root. This
artifact is built for `/`, not a subdirectory.

Serve `.wasm` as `application/wasm`, `.js` as JavaScript and `.json` as JSON.
For Apache hosting, upload `apache-framework.htaccess` as `_framework/.htaccess`
inside the website root. This forces the MIME type for the final `.wasm` extension,
including assemblies whose names contain `.Xml` or `.Json`. Preserve any existing
configuration in that directory by merging the provided block. Verify a live
`.wasm` response reports `Content-Type: application/wasm` before publishing the
new start page. See [Apache ForceType documentation](https://httpd.apache.org/docs/2.4/mod/core.html#forcetype).
HTTPS and a WebGL2-capable browser are required. Revalidate index.html, release.json and boot manifests; assets with hashed
filenames can use a long immutable cache lifetime. Enable HTTP compression through
the webserver. A normal page refresh should pick up the new release.

## Matching native builds

Browser, Windows and Linux players must use matching protocol and content IDs.
The protocol version comes from [ProtocolVersion.cs](../../../../Protocol/ProtocolVersion.cs).
Use the `contentId` in `release.json` as `-RoomContentId` when running
`scripts/package.ps1`, or `-p:OpenGarrisonRoomContentId=...` for a direct client
publish. The native client reads this compiled metadata; a developer build with
`dev` identity cannot join a packaged room with a different identity.

The host's native jukebox reads its `config/jukebox` folder and `playlist.txt`.
The in-game Music Library button opens that folder; Refresh Tracks discovers
added files while preserving the existing playlist order. Browser tracks are
imported through Settings > Audio > Music > Music Library and saved in IndexedDB,
not cookies. The limit is 32 MB per track and 256 MB total. Clearing site data
removes the library. The room host controls playback; each listener controls their
own volume and mute. Last to Die has no map voting; Practice does.

## Verify and roll back

After updating the API and website, check the API health endpoint and logs. Verify
local Solo, Practice Singleplayer, four independent browser profiles joining one
room, survivor/reward selection, and all four entering the same stage. Verify
Practice teams, host settings Apply/Cancel, late joins, map voting, Return to Lobby,
and native/browser jukebox playback. Check guest reconnection and host departure.
Test a blocked direct connection so the WebSocket fallback is exercised too.

The API keeps a disconnected owner's room for 45 seconds; clients try to reconnect
for 30 seconds. Disconnected lobby guests retain their seat for 30 seconds. An API
restart loses its current room records; drain active rooms before an update when
possible. For rollback restore the prior matching website/native release and API
source. Preserve the database and secrets.

Solo and player-hosted Last to Die leaderboard submissions also require the
[run verification worker and reward authority configuration](../../README.md#verified-offline-and-player-hosted-last-to-die-runs).
Deploy the verifier with matching game content before enabling uploads.
