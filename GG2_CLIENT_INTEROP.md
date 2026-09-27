# Vanilla GG2 client interoperability

Target the upstream Gang Garrison 2 v2.9.2 wire protocol first. The upstream
protocol UUID is `b31c2209-4256-9a19-d0ef-c71c5373bd75`, sent as 16 hex-pair
bytes after command `HELLO` (`0`). This protocol is separate from SGG's own
network protocol and does not change its version.

The requested live target is **Vindicator's - US West**. GG2's public lobby
listed it at `45.59.102.99:8190` on `koth_corinth` when checked on 2026-09-22.
A direct protocol hello confirmed its server name, map, and required plugin
list: `chat@bad8081d64f0b808a2bb3a6d88978fb5`. The address and map can
change, so resolve the lobby entry again before an integration test.

## Verified join sequence

The GG2 server accepts a TCP stream. It reads `HELLO` plus the UUID, may
request a length-prefixed password, then sends its own `HELLO` with the server
name, map name, map MD5, a required-plugin flag, and a two-byte-length-prefixed
plugin list. The client reserves a slot by sending `RESERVE_SLOT` (`60`), a
one-byte name length, and at most 20 name bytes. After the server acknowledges
the reservation, the client can request a custom map with `DOWNLOAD_MAP`
(`45`) and then send `PLAYER_JOIN` (`1`). The join state begins with
`JOIN_UPDATE`, `CHANGE_MAP`, the player roster, and `FULL_UPDATE`.

GG2's own `ClientReserveSlot` helper appends `PLAYER_JOIN` immediately, while
its later reservation handler sends that command again. The server code accepts
`PLAYER_JOIN` after the reservation and permits `DOWNLOAD_MAP` while waiting to
join. A new client can keep map download and join explicitly ordered.

GG2 input is command `INPUTSTATE` (`6`) followed by four bytes: key flags,
little-endian aim angle, and aim distance divided by two. The key bits are
`01` taunt, `02` down, `08` secondary, `10` primary, `20` right, `40` left,
and `80` jump. GG2 calculates its aim angle from the view center, with zero
degrees right and 90 degrees up. The server applies received input to its own
character simulation.

## Current adapter and connection rules

`NetworkGameClient` receives SGG protocol messages through
`INetworkClientMessageTransport`. `LegacyGg2NetworkClientTransport` is a
separate TCP adapter, started with `connectgg2 <host> [port]`. It handles the
GG2 hello, slot reservation, join state, stock map snapshots, team/class
selection, input, and the named chat plugin. It reuses the `.dsm` state
translator with exact live packet lengths. The adapter emits only GG2
commands; unsupported SGG controls are rejected locally.

`NetworkGameClient.IsLegacyGg2Connection` marks this session. The input path
retains movement, aim, taunt, primary fire, and GG2's secondary fire, while
clearing SGG utility abilities, alternate-weapon switching, interactions,
dispenser commands, and other SGG-only input. The client hides alternate
loadouts, plugin-only classes, the SGG build wheel, and configured ability HUD
rows. SGG local action and immediate-fire prediction are disabled because the
GG2 server is authoritative. Stock GG2 secondary actions use GG2's secondary
input bit; Constructor sentry, Overweight eating, Marksman zoom, and intel
drop also send their GG2 command bytes on press. Incoming players are assigned
the stock primary in every snapshot, and the initial spectator slot opens team
selection so they can join a team.

The Join Servers browser has SGG and GG2 tabs. The GG2 tab reads the official
GG2 lobby's native TCP listing at `ganggarrison.com:29944`, displays its
advertised address, map, occupancy, and version, and joins via the legacy TCP
adapter. Entries with a different protocol UUID or a password remain visible
but cannot be joined. It uses the game's existing classic body, weapon, and
HUD sprite fallback while connected to GG2; the saved Kelly/Elkondo preference
applies again after leaving the legacy session.

The local scripted TCP test verifies joining a KOTH map with an existing
Soldier, team and class commands, input, and chat framing. On 2026-09-23, a
one-off live test against Vindicator's US West server joined the smaller team,
spawned as Soldier, and sent primary-fire input. Across 297 translated
snapshots it observed 28 frames containing rockets, seven firing sounds
attributed to the local player, and seven explosions. Ammo changed from four
to zero and reloaded. These observations confirm the network translation and
snapshot encoder; rendering and audible playback still need an interactive
comparison against GG2. The public-server test is intentionally not retained
in the offline test suite.
On 2026-09-22 a separate one-off live lobby test parsed all five servers then
advertised by the official GG2 lobby. The permanent loopback test covers the
lobby request bytes, response framing, metadata, and protocol compatibility.

## Playability corrections after the first interactive session

The first interactive GG2 session exposed unreliable repeat jumping, unstable
weapon direction, a joining overlay on class changes, and SGG weapon UI. The
`Modern/GML-GG2-Modern` scripts show that `WEAPON_FIRE` ends with a random seed,
not an aim angle. Its velocity bytes use an 8.5 divisor. Incoming aim is taken
from the latest `INPUTSTATE`/state update, and outgoing GG2 input uses the view
center as its aim origin. GG2 has no SGG input-sequence acknowledgment, so the
client releases jump with the key instead of latching it until an acknowledgment.
The GG2 session also keeps its presentation world through class changes and
hides the SGG secondary-weapon panel and cabinet swap prompt.

Rocket impacts and removed mines now emit visual events as well as sound.
`Modern` uses `ExplosionS` for rockets and `ExplosionSmallS` for mines and
sentries; the live translator requests the corresponding explosion sizes.
The Medic's serialized heal target and charge now feed the existing healing
beam and charge HUD, which otherwise had no visible firing state.
Weapon fire and plugin packets are folded into the next server state snapshot
instead of each advancing the interpolation timeline. These changes have
targeted loopback and unit coverage, but still need an interactive retest for
cursor behavior, projectile fidelity, and all class-specific weapons.

GG2 aim angles increase upward, while SGG's sprite renderer uses screen-space
angles that increase downward. The live snapshot now converts that angle for
weapon presentation without changing GG2 projectile simulation. Desktop
content lookup is anchored to the executable, so a launch from the repository
root resolves packaged stock-map backgrounds instead of the sparse source PNG.
Stock-room backgrounds also fall back to the packaged runtime asset bundle when
their loose PNG is absent from a fast or compact desktop build.

For servers that advertise a map MD5, the client requests GG2's native TCP
map transfer before reserving a slot. It checks the byte count and MD5, validates
the embedded walkmask and spawns, and caches the PNG under the user data root's
`gg2-maps` directory by hash. The downloaded PNG supplies both the map art and
collision layout. A loopback server test verifies the request/transfer/reserve
order and completes a join with the downloaded map; a cache test verifies hash
rejection and reuse. The importer keeps the server's map name for game modes
whose type is encoded in that name. During rotation the adapter loads bundled
stock maps, or uses an auxiliary GG2 connection to fetch the next external
map while preserving the gameplay slot. Repeatable tests cover clearing old
projectiles on a stock-map transition and downloading an external rotation map
while keeping the gameplay connection. A live rotation and visual retest are
still needed.

Weapons that GG2 announces with `WEAPON_FIRE` now retain their projectile,
sound, and recoil edge across snapshot ticks. Pyro flames, Heavy bullets,
Medic needles, and Quote projectiles are reconstructed from GG2 input-state
packets because GG2 does not send `WEAPON_FIRE` for those attacks. Direct-fire
shots remain visible for two snapshots after a locally inferred collision.
Network sound serialization now preserves the firing player's ID, which the
client uses to prioritize local weapon audio and fire animation. Idle movement
stops between repeated stationary server positions instead of applying small
rounding errors as visible jitter. The bridge still approximates some effects;
class-by-class interactive comparison is required for exact fidelity.

The next audio and effects pass stops Heavy's minigun loop when its fire timer
expires, emits jump cues on actual jump edges, and uses the stock sentry floor,
build, alert, idle, and shotgun sounds. The sentry's target and shot cadence
are reconstructed from replicated positions because GG2 does not send a
separate turret-fire packet. Shell reload animation restarts on each stock
shotgun or scattergun ammo insertion, including the final shell. Medic beam
presentation can acquire a teammate from replicated aim and primary input
between full updates; GG2 only serializes the Medigun's heal target in full
updates. Explosion deaths now use GG2's 1-based damage-source IDs, and gib
spawn events use the codec's serialized event path. Non-explosive weapon
deaths request a blood effect. Focused packet tests cover these paths,
including network round-tripping of gibs. A live visual and audio comparison
remains necessary to confirm sound timing and Medic target choice.

## Remaining work

Required client-side GML plugins need explicit native support; the adapter does
not execute them. Internal GG2 maps advertise no MD5 and cannot be downloaded
through `DOWNLOAD_MAP`; these still require a matching bundled stock map.

The target chat archive was downloaded from GG2's official plugin source and
its MD5 matched the advertised hash. Its plugin packet ID is the zero-based
index in the server's plugin list (currently `0`). The client announces chat
support with `PLUGIN_PACKET` (`55`) carrying one data byte `0`. To send chat,
the data bytes are `1, length, text` for global chat or `2, length, text` for
team chat. The outer packet contains a little-endian two-byte length and the
plugin ID before these data bytes. The native implementation does not execute
GML. Chat's vote packets have different payloads and require their own decoder.

The remaining validation is an interactive GG2 2.9.2 session to compare the
server's state to the client rendering and audio, including aim, fire effects,
chat UI, map rotation, and class-specific actions.

## Upstream source

- [Protocol constants and UUID](https://github.com/Gang-Garrison-2/Gang-Garrison-2/blob/ea8d69511ee1a2f76bc8266bf890764f480c4993/Source/gg2/Constants.xml)
- [Server join state machine](https://github.com/Gang-Garrison-2/Gang-Garrison-2/blob/ea8d69511ee1a2f76bc8266bf890764f480c4993/Source/gg2/Scripts/GameServer/serviceJoiningPlayer.gml)
- [Server input handling](https://github.com/Gang-Garrison-2/Gang-Garrison-2/blob/ea8d69511ee1a2f76bc8266bf890764f480c4993/Source/gg2/Scripts/GameServer/processClientCommands.gml)
- [Client input writer](https://github.com/Gang-Garrison-2/Gang-Garrison-2/blob/ea8d69511ee1a2f76bc8266bf890764f480c4993/Source/gg2/Scripts/Message%20Builders/ClientInputstate.gml)
- [Client input flags](https://github.com/Gang-Garrison-2/Gang-Garrison-2/blob/ea8d69511ee1a2f76bc8266bf890764f480c4993/Source/gg2/Objects/InGameElements/PlayerControl.events/Begin%20Step.xml)
- [Public GG2 lobby status](https://www.ganggarrison.com/lobby/status)
- [Server-sent plugin source catalog](https://github.com/Gang-Garrison-2/gg2plugins)
