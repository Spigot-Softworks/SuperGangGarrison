# OG2 networking rewrite blueprint

Status: architecture and implementation blueprint for the `network-test` branch.

Scope: the current OG2 codebase outside `Modern`. This document is intentionally a design and migration plan; it does not change the live protocol or gameplay code by itself.

## Executive decision

The reliability problems are symptoms of one missing boundary: protocol messages, transport behavior, and snapshot repair are currently coupled together. The replacement should make these separate contracts:

1. `OpenGarrison.Protocol` defines versioned event schemas and their delivery semantics.
2. A backend owns connection lifecycle, framing, stream/channel scheduling, backpressure, retries, and transport faults.
3. The gameplay replication layer owns authoritative state, completeness, identity, repair, and application acknowledgements.

The canonical backends should be:

- WebSocket for browser and explicitly selected cross-platform connections.
- QUIC for native clients and servers, using reliable streams for reliable delivery and multiple concurrent lanes for unordered delivery.

Raw UDP should remain only as a legacy compatibility backend while the migration is running. It must not be the reference implementation for the new protocol.

The first new wire contract should be the protocol-58 schema family requested here. The repository currently reports `ProtocolVersion.Current = 63`, so this needs an explicit versioning decision: either publish the new wire format as a reviewed successor version, or negotiate a `SchemaFamily = 58` capability inside a later protocol version. Do not silently give new bytes the old meaning of version 63.

## What is in the repository today

### Repository shape

The relevant runtime is already split into sensible broad areas:

- `Protocol`: message records, one central binary codec, compression, and snapshot delta types.
- `Server`: UDP/WebSocket transport, incoming dispatch, sessions, snapshot planning/broadcasting, and the simulation loop.
- `Client`: native UDP transport, connection state, message receive/send, and gameplay presentation.
- `Client.Browser`: JavaScript WebSocket interop and the browser transport implementation.
- `Core`: the simulation model and the client-side snapshot application path.
- `Tests/OpenGarrison.PluginHost.Tests`: protocol, transport, input queue, snapshot budget, session, and snapshot presentation tests.

`Modern` was excluded from the inventory as requested.

### Protocol and serialization

The current protocol is centered on `MessageType` and `IProtocolMessage` in `Protocol/MessageTypes.cs`. The codec in `Protocol/ProtocolCodec.cs` plus its snapshot partials uses a large switch for serialization and deserialization. The current message set includes handshake, input, snapshot, control, chat, profiles, plugins, ping, and snapshot acknowledgement messages.

The wire path currently works like this:

1. Serialize the whole message into a memory buffer.
2. Prefix a one-byte compression encoding (`none` or LZ4).
3. Put the result directly into a UDP datagram or one WebSocket binary message.
4. Read the entire received payload and attempt to deserialize one message.

`TryDeserialize` returns only success/failure. It does not report whether the failure was truncation, invalid compression, an unknown message type, an invalid field, an excessive length, or trailing bytes. The client and server generally ignore a `false` result. This is a direct observability and recovery gap.

`SnapshotMessage` is a large aggregate containing world state, player state, scoreboards, projectiles, transient events, removals, string-cache updates, baseline information, delta information, and completeness flags. It is not a collection of independently versioned events.

`ProtocolVersion.Current` is 63. No QUIC implementation or `System.Net.Quic` reference was found in the in-scope source, project files, or docs. The protocol project currently has LZ4 as its only direct package dependency.

### Native client transport

`Client/Networking/NetworkClientMessageTransport.cs` implements a raw `UdpClient` transport. It sends one serialized protocol payload per datagram, uses a non-blocking receive path, filters packets by endpoint, and has no reliability, ordering, retransmission, path-MTU discovery, fragmentation, or repair state machine.

`NetworkClientMessageTransportRegistry` selects the browser factory only for browser builds; native builds use UDP. `NetworkGameClient` then treats a received byte array as a complete protocol message.

The client disconnect watchdog is message-based: the normal connected timeout is five seconds. A missing snapshot, a dropped chat message, and a stream that is alive but temporarily silent are not distinguished.

### Browser WebSocket transport

`Client.Browser/Services/BrowserWebSocketMessageTransport.cs` and the JavaScript in `Client.Browser/wwwroot/index.html` provide one binary WebSocket connection. WebSocket message boundaries are preserved, and the server-side reader also assembles fragmented WebSocket messages before enqueueing them.

That solves message fragmentation, but it is not yet a protocol/backend boundary:

- the C# transport exposes only raw payloads;
- the browser send path can return `false` when `bufferedAmount` exceeds its limit, but the C# caller does not use that result as backpressure;
- there is no schema metadata, frame identity, retry state, repair request, or malformed-frame callback;
- reconnect behavior is a connection concern, not a protocol-level recovery state machine.

### Server transport and dispatch

`Server/Networking/ServerMessageTransport.cs` contains both the transport interface and concrete UDP/WebSocket behavior. The composite transport uses raw UDP for native peers and a WebSocket peer object for browser peers.

For WebSocket peers, the current implementation has:

- a bounded reliable queue of depth 64 and a maximum reliable queue byte budget;
- one latest-snapshot slot, which is intentionally coalesced;
- a writer that prioritizes the latest snapshot and then sends a bounded number of reliable messages.

When the reliable queue is full or over budget, the current code logs and drops a reliable payload. That lane is therefore not reliable under pressure. Snapshot classification is also passed into the transport as `MessageType?`; the backend has to know that a particular protocol type is a snapshot instead of receiving delivery semantics from a schema registry.

`Server/Networking/ServerIncomingPacketPump.cs` calls `ProtocolCodec.TryDeserialize` and continues when it returns `false`. There is no error-kind callback, frame quarantine, stream reset, retransmit request, or protocol-error disconnect. The incoming dispatcher then switches on concrete message records.

`Server/Networking/ServerOutboundMessaging.cs` serializes messages and passes the optional snapshot type to the transport. Transport acceptance is not delivery acknowledgement.

### Snapshot production and application

`Server/Snapshots/SnapshotBroadcaster.cs` builds per-client snapshots and remembers baseline state after the send callback returns. The payload target is approximately 1400 bytes for Internet UDP, 4 KiB for loopback, and 12 KiB for reliable streams. These are targets and diagnostics, not hard framing guarantees.

The live broadcaster currently calls `SnapshotDeltaBudgeter.BuildUntrimmedSnapshot`, so the live path does not use the budgeter's trimming/reduction path. The result can exceed the UDP target and be fragmented or discarded by the path. The budgeter tests cover trimming behavior, but that behavior is not the live default.

The snapshot protocol combines:

- full and delta player fields;
- `BaselineFrame` and `IsDelta`;
- per-collection removals;
- `EntityCollectionCompletenessFlags` for projectile collections;
- a server-side string cache and per-client “sent cache id” tracking.

The client resolves a delta against a stored baseline in `Client/Game/Multiplayer/Session/Game1.NetworkSnapshots.cs`. Missing baselines reject the snapshot. The client sends `SnapshotAckMessage` when a resolved snapshot is queued, before the eventual `SimulationWorld.ApplySnapshot` call has necessarily succeeded. A failed application can therefore be acknowledged as though the state were usable.

The client retains incomplete projectile collections according to the completeness flags, but the delta merger and the presentation path are separate concerns. There is no general protocol rule saying which event is complete, which state may be retained, and which state must be repaired.

### Input path

`NetworkGameClient` sends `InputStateMessage` with an incrementing sequence. `ClientSession.TrySetLatestInput` accepts newer sequences and records rising-edge actions in a bounded pending queue. `TryGetInputForNextTick` combines pending edges and applies the latest input to the server simulation.

This is a useful attempt to preserve UDP button taps, but it is not a complete command protocol:

- there is no receipt acknowledgement for an input sequence;
- there is no applied/consumed acknowledgement for a one-shot action;
- sequence gaps, duplicates, reordering, and wraparound are not represented as explicit protocol states;
- the pending edge queue is bounded;
- a snapshot is the indirect source of input progress, so loss of snapshots also obscures input progress;
- a jump is represented as a bit transition in a latest-state packet rather than as a durable command event.

That explains why a held movement state can work while a short jump edge can disappear without an obvious server fault.

### Class identity and string cache

`SnapshotBroadcaster` assigns cache IDs to strings including `GameplayClassId`. `ClientSnapshotStringCache` resolves those IDs when applying a player snapshot. The server marks IDs as sent when composing/enqueuing a snapshot, not when the client has acknowledged applying the corresponding cache entry. I found no call that clears the client snapshot string cache on a new session/epoch.

This creates a credible path to class schizophrenia:

1. A cache update is lost or a snapshot is rejected.
2. A later player state refers to an ID the client does not have, or has from an earlier server/cache epoch.
3. The client falls back to stale or default class identity while other fields, including abilities and health, continue to arrive.

This is a high-confidence design defect even if it is not the only cause of the reported examples. Correctness-critical identity must not depend on an unreliable, unacknowledged, epoch-less dictionary.

## Failure diagnosis

| Report | Current mechanism that permits it | Required invariant in the replacement |
| --- | --- | --- |
| Player state disappears while chat works | Large state is sent as lossy UDP; live snapshots can exceed the intended MTU; deltas require a baseline; decode failures are silently discarded | State events have explicit delivery semantics and completeness. A missing state frame either arrives via reliable delivery or triggers an explicit full repair. |
| Snapshot timeout on distant/high-hop paths | Raw UDP, no PMTU/retransmit logic, no fragmentation contract, and a five-second generic watchdog | QUIC/WS are the canonical containers. No application state depends on an IP-fragmented datagram. Connection health, frame freshness, and protocol faults are separate signals. |
| Class schizophrenia | `GameplayClassId` is an unreliable string-cache reference with no epoch and no apply acknowledgement | Class identity is self-contained in the authoritative player record, or comes from a reliable epoch-scoped dictionary before any reference can be applied. |
| Jump/input edge has no effect | Latest-state UDP input plus inferred edge queue; no durable command ID or applied acknowledgement | One-shot actions are reliable ordered commands, deduplicated by command ID and acknowledged only after simulation consumption. |
| WebSocket “reliable” message disappears under load | Bounded queue drops payloads when full; browser send backpressure is ignored | A reliable enqueue either remains owned by the backend until sent/acknowledged or fails the connection/session explicitly. It is never silently dropped. |
| Bad frame harms later frames or vanishes silently | Codec returns a boolean; receive loops ignore failure; no frame boundary/schema error taxonomy | Backend validates a complete frame before dispatch and routes decode failures to an optional callback with a default warning. Stream recovery has a defined next-frame rule. |

## Target architecture

### Layer 1: protocol schema library

Keep message contracts in `OpenGarrison.Protocol`, but replace the central message switch with a schema registry. A schema should own:

- stable event ID and schema revision;
- direction (`C2S` or `S2C`);
- maximum encoded and decoded size;
- serialization and deserialization of exactly one event body;
- delivery annotation;
- whether the body is a complete state, a delta, a command, or a notification;
- validation and a diagnostic kind for failures.

The requested metadata should be expressible directly on each schema, for example:

```csharp
[ProtocolEvent(58, EventId.PlayerStateBatch, Direction.S2C)]
[ReliableUnordered(ChannelType.State)]
public sealed class PlayerStateBatchSchema : IEventSchema<PlayerStateBatch> { ... }
```

The three required annotations are:

```csharp
[ReliableOrdered(ChannelType.Control)]
[ReliableUnordered(ChannelType.State)]
[LastWins]                 // no preferred channel
[LastWins(ChannelType.State)]
```

`LastWins` is the replacement for a separate `Unreliable` primitive. It means the event is a complete, repairable state where stale instances may be discarded. It does not mean that a partial delta or a one-shot gameplay event is allowed to disappear.

Recommended metadata types:

```csharp
public enum ChannelType
{
    Control,
    Input,
    State,
    GameplayEvents,
    Chat,
    Social,
    Plugin,
}

public enum DeliveryKind
{
    ReliableOrdered,
    ReliableUnordered,
    LastWins,
}
```

The attributes should be compile-time metadata; the backend should consume a normalized `DeliveryDescriptor` from the registry. A call site must not be able to mark a snapshot reliable or unreliable ad hoc by passing `MessageType?` into a transport.

### Layer 2: complete protocol frames

Every backend receives and emits complete protocol frames, never arbitrary byte fragments. The frame envelope should include at least:

- protocol/schema family and schema revision;
- stable event ID;
- frame ID scoped to a connection epoch;
- encoded payload length and bounded decoded length;
- compression encoding and decoded length, if compression is used;
- integrity check sufficient to distinguish corruption from a schema rejection.

The transport stream ID, WebSocket message fragmentation, and QUIC stream ID remain backend details. The frame ID, event ID, and delivery metadata are protocol/backend coordination data used for diagnostics and repair.

The reader must:

1. reject a header before allocating if its lengths exceed configured limits;
2. read exactly the declared body;
3. decompress into a bounded buffer;
4. require the schema decoder to consume exactly the body;
5. validate the event before exposing it to gameplay;
6. return a typed result such as `CompleteFrame`, `UnknownSchema`, `TruncatedFrame`, `InvalidPayload`, or `OversizedFrame`.

The old `ProtocolCodec.TryDeserialize(byte[], out message)` can remain as a legacy adapter during migration, but the new connection path must not treat `false` as an explanation.

### Layer 3: connection container/backend

Introduce a backend-neutral contract in a new networking assembly or a narrowly scoped `Network` folder shared by client and server. The contract should be asynchronous internally even if the current game loop exposes a polling adapter.

Conceptually:

```csharp
public interface IConnectionContainer : IAsyncDisposable
{
    ConnectionCapabilities Capabilities { get; }
    ValueTask OpenAsync(CancellationToken cancellationToken);
    ValueTask SendAsync(ProtocolFrame frame, DeliveryDescriptor delivery,
        CancellationToken cancellationToken);
    ValueTask<ReceiveResult> ReceiveAsync(CancellationToken cancellationToken);
    ValueTask RequestRepairAsync(RepairRequest request, CancellationToken cancellationToken);
    ValueTask CloseAsync(DisconnectReason reason, CancellationToken cancellationToken);
}
```

The concrete backend must own:

- connection and stream lifecycle;
- frame boundaries;
- per-channel queues and ordering;
- retry/retransmit policy;
- flow control and backpressure;
- peer/session identity and connection epoch;
- transport-level diagnostics;
- malformed-frame and stream-fault transitions.

The gameplay layer should only see validated protocol events and explicit delivery/application status.

### Canonical WebSocket backend

WebSocket is one ordered byte/message connection. The backend should present logical channels above that connection:

- reliable ordered channels use a never-drop FIFO;
- reliable unordered channels use independent logical queues and a scheduler that can emit the next complete frame without allowing a stale state frame to block control;
- `LastWins` channels retain only the newest complete frame per logical state key;
- writes honor browser/server backpressure and report a failed reliable send instead of discarding it.

WebSocket has no native stream reset. Therefore “reopen” means reset the protocol reader/connection session according to one explicit policy:

1. close the affected WebSocket with an internal protocol-recovery reason;
2. reconnect within the same authenticated session if the handshake can prove the connection epoch and last acknowledged frame;
3. retry parsing/recovery at most `MaxWsRetries ?? 2` times;
4. after retries, emit `MalformedS2CException` for the offending frame, discard it, and continue only if the next complete frame validates;
5. if the next frame also fails, send/record a protocol-error disconnect.

Because an already received WebSocket message cannot be repaired in place, a recovery request must identify the event/frame and ask the server for a replacement frame. The replacement must not depend on the malformed frame.

### Canonical QUIC backend

Use `System.Net.Quic` where the runtime/platform supports it. The first backend version should expose:

- one reliable ordered bidirectional `Control` stream;
- one reliable ordered `Input` stream for command events;
- a pool of concurrent streams for each reliable unordered channel;
- an explicit latest-state lane for `LastWins` events, preferably QUIC datagrams when available, with a stream-based fallback that cancels stale sends.

For reliable unordered delivery, “unordered” means semantic unorderedness, not byte-level disorder: allocate N streams for the purpose and schedule each complete frame to the lane with the lowest current head-of-line delay. The receiver can deliver complete frames as they arrive and the event schema decides whether sequence ordering matters.

For a QUIC stream fault:

1. close and reopen the offending stream;
2. send a reliable `RetransmitRequest` over `ChannelType.Control` naming the connection epoch, event ID, frame ID, channel, and state/domain;
3. retransmit a replacement on a dedicated recovery stream, never by putting the offending bytes on Control;
4. if the recovery request cannot be decoded, emit/log non-fatal `MalformedS2CException` and ignore that frame;
5. if the next frame after recovery also fails validation, close the connection with a protocol-error disconnect.

The backend must make these transitions observable and testable without needing a real Internet path.

## Protocol event split

The first schema family should replace the monolithic `SnapshotMessage` with events that have independently reviewable completeness. A reasonable initial matrix is:

| Event family | Direction | Delivery | Completeness rule |
| --- | --- | --- | --- |
| Hello, Welcome, ConnectionDenied, password/auth, session assignment | Both | ReliableOrdered(Control) | Complete handshake/session records |
| ControlCommand, ControlAck, map/match lifecycle, repair request/response | Both | ReliableOrdered(Control) | Complete command/result; never coalesced |
| Held input state | C2S | LastWins(Input) | Complete current held/aim state; stale state may be replaced |
| One-shot input command (jump, fire edge, ability activation, interact, taunt) | C2S | ReliableOrdered(Input) | Durable command with command ID and result semantics |
| InputConsumed/InputRejected | S2C | ReliableOrdered(Input) | Names the command ID and simulation tick/result |
| Authoritative player identity/loadout/class | S2C | ReliableUnordered(State) | Self-contained player record; includes class identity, health, and generation |
| Player motion/quick state | S2C | LastWins(State) | Complete current motion state for the named player/domain |
| Projectile lifecycle/spawn/despawn and gameplay-critical events | S2C | ReliableUnordered(GameplayEvents) | Complete event record with entity ID, generation, and kind |
| Projectile current motion | S2C | LastWins(State) or reliable state batch | Complete current state; no delta may require a missing frame |
| Chat | Both | ReliableOrdered(Chat) | Preserve chat order and never drop under queue pressure |
| Profiles, social state, plugin messages | Both | ReliableUnordered(Social/Plugin) | Complete message body; explicit max size and validation |
| Ping | Both | ReliableOrdered(Control) or a backend health probe | Must not be used as the only gameplay freshness signal |

The exact event names are implementation choices; the properties are not. In particular, `GameplayClassId`, class enum, health, ability state, and entity kind must be transmitted as authoritative identity/data, not inferred from a cache entry or a collection position.

### Snapshot strategy

For the first canonical backend release, prefer self-contained domain state over the current cross-frame delta chain:

- `PlayerStateBatch` is complete for all player records included by its scope.
- `ProjectileStateBatch` is complete for its scope, or lifecycle events are reliable and motion is complete LastWins state.
- `RosterState` explicitly identifies removals and generations.
- A `StateResync` response can rebuild the complete client state without any missing baseline.

The current `SnapshotDelta` implementation can remain behind a legacy adapter until the new events are proven. It must not be the only way to recover from a dropped state frame on a canonical connection.

If bandwidth later requires deltas, make the base explicit and enforce it as a reliable state machine: the receiver must ACK the exact base it applied, the sender must retain it until that ACK, and a missing base must produce a repair request rather than a silent rejection. `LastWins` payloads must never be partial deltas.

### String and class identity

The safest protocol-58 rule is to remove string-cache indirection from correctness-critical records. Send bounded UTF-8 identifiers for gameplay class, loadout, mod pack, and item identity, then rely on frame compression rather than an unreliable dictionary.

If a dictionary is retained for bandwidth, it must become its own reliable protocol:

- dictionary epoch is negotiated per connection;
- entries are reliable ordered on Control;
- entry IDs include the epoch or are rejected across epochs;
- a state record cannot be applied until all referenced entries are present;
- cache entries are acknowledged as applied, not merely enqueued;
- reconnect/session reset clears the dictionary;
- an unknown ID raises a typed validation diagnostic and requests repair.

The first implementation should choose the simpler self-contained form for class identity.

### State application invariants

The client should maintain independent monotonic sequence/epoch tracking for control, roster/player state, projectile state, and input results. Applying one event should be atomic at the domain level:

1. validate every record and identity reference;
2. build a new domain view;
3. resolve the local player and required class definition;
4. commit the view in one operation;
5. only then acknowledge application.

If a complete state event is missing, retain the last valid domain view and mark it stale. Do not interpret absence from one partial event as entity removal. Removal must be explicit and generation-aware. A `PlayerKey` should include at least slot/identity plus a generation; a projectile ID reused for another kind must be rejected unless its generation changes.

## Input rewrite

Split input into two event classes:

1. `InputState` is the latest held state and aim, delivered as `LastWins(ChannelType.Input)`.
2. `InputCommand` is a durable one-shot command with `CommandId`, client input sequence, action kind, optional client tick, and the input state/aim needed for that action. It is `ReliableOrdered(ChannelType.Input)`.

The server must keep a command ledger per session:

- duplicate command IDs return the original result and do not execute again;
- gaps are retained or explicitly rejected according to a documented policy;
- accepted means admitted to the simulation;
- consumed means the simulation applied it;
- rejected includes a reason and authoritative tick;
- the result is sent as `InputConsumed` or `InputRejected` and retained until the client acknowledges it.

The snapshot/state stream may include `lastAppliedCommandId` as a diagnostic and prediction aid, but it is not the only acknowledgement. Jump, fire edges, ability activation, interact, taunt, build/destroy, and similar transitions must no longer depend on observing a rising bit in a lossy latest-state packet.

## Malformed-frame and completeness state machine

### Decode result and callback

Add an optional callback by fault/event kind, for example `IProtocolFaultSink` with a default implementation that logs a warning and ignores the frame. The callback should receive:

- direction and backend;
- connection/session epoch;
- event ID/schema revision if readable;
- frame ID/channel/stream if known;
- fault kind and bounded exception details;
- encoded and decoded lengths;
- whether the frame was delivered by the transport before decoding failed.

`MalformedS2CException` should be non-fatal by default and used for an invalid server-to-client event after the backend has delivered a complete frame. It must be distinct from a connection-level protocol error.

### QUIC recovery

The QUIC stream state is `Healthy -> Faulted -> Reopened -> AwaitingRepair -> Healthy`. A failed frame is quarantined. The Control stream carries only a structured repair request, never the malformed payload. A second decode failure after recovery transitions to `ProtocolError` and disconnects.

### WebSocket recovery

The WebSocket state is `Healthy -> Reconnecting/Retrying -> Healthy` for at most `MaxWsRetries` attempts. A failed frame is quarantined. After the retry budget is exhausted, emit `MalformedS2CException`, ignore that frame, and continue only if the next complete frame is valid. A second consecutive invalid frame transitions to `ProtocolError` and disconnects.

### Failure policy

Never silently use a default message, default class, empty player list, or partially decoded state as a substitute for an invalid frame. Defaults are acceptable only when the schema explicitly defines them and validation has succeeded.

## Staged implementation on `network-test`

The branch has been created locally. Keep the existing legacy UDP path intact until each stage has a working canary and rollback switch.

### Stage 0 — instrumentation and reproduction

Add no semantic behavior changes yet. Add:

- decode-failure counters by fault kind and message/event ID;
- outgoing encoded size histograms, including compressed and uncompressed sizes;
- actual UDP payloads over the configured target, with the live snapshot builder called out;
- snapshot baseline missing/rejected/applied/acknowledged counters;
- string-cache reference misses and cache epoch/session counters;
- input received, deduplicated, queued, consumed, and unacknowledged counters;
- WebSocket queue drops, queue bytes, browser send-backpressure results;
- connection freshness separate from snapshot freshness;
- bounded, privacy-safe wire diagnostics keyed by connection/frame ID.

Add deterministic fault-injection tests for loss, duplication, reordering, truncation, oversized length, stale cache, and delayed input. This stage should make the four reported symptoms measurable before the rewrite changes their frequency.

### Stage 1 — schema registry and protocol-58 event family

Create dedicated schema types and registry entries for every current packet type, even before splitting all snapshot contents. The registry must be able to return the delivery descriptor and max sizes without knowing whether the connection is UDP, WebSocket, or QUIC.

Implement complete frame envelopes, typed decode results, exact-consumption validation, bounded decompression, and golden fixtures. Add a legacy adapter so the current `MessageType` codec can be used through the new registry during transition.

Exit criteria:

- every existing packet has one schema entry;
- no new code passes `MessageType?` to choose reliability;
- malformed and trailing-byte fixtures produce typed failures;
- old and new codecs round-trip agreed fixtures;
- version negotiation cannot confuse the current v63 wire with the schema-58 family.

### Stage 2 — backend-neutral connection API and WebSocket backend

Move transport lifecycle behind the backend contract. Refactor the existing WebSocket implementation first because it is available in-browser and already preserves complete message boundaries.

The WebSocket backend must implement logical channels, reliable no-drop queues, LastWins coalescing only for explicitly annotated complete state, backpressure, frame IDs, reconnect/retry policy, repair requests, and fault callbacks. Add a server-side retransmit cache for recent reliable frames and complete state resyncs.

The legacy `NetworkGameClient` polling loop can be adapted to consume validated events while the internal backend remains asynchronous.

Exit criteria:

- a full reliable queue never silently drops a message;
- queue overflow produces backpressure or explicit session failure;
- a malformed frame invokes the callback and follows the next-frame rule;
- chat/control/input-command delivery remains correct while state frames coalesce;
- browser tests cover fragmented WebSocket messages, reconnect, and backpressure.

### Stage 3 — QUIC backend

Add the native QUIC container using the platform-supported .NET QUIC API. Negotiate QUIC capability separately from protocol schema version. Implement the Control stream, Input stream, reliable unordered stream pool, LastWins lane, stream health, stream reopen, repair request, and protocol-error disconnect transitions.

Use an in-memory deterministic stream scheduler in tests so stream selection, HOL behavior, and repair are testable without requiring a QUIC-capable host. Then add an opt-in real loopback/integration test where the platform supports it.

Exit criteria:

- reliable ordered events retain order;
- reliable unordered events can arrive from multiple streams without semantic ordering assumptions;
- stale LastWins state is safely discarded and a later complete state repairs it;
- a reset stream does not kill the whole connection unless the recovery path fails;
- a second post-recovery decode failure disconnects with protocol error.

### Stage 4 — authoritative state rewrite

Introduce the split player, roster, projectile, lifecycle, and transient-event schemas. Make state records self-contained, generation-aware, and explicit about completeness. Remove `GameplayClassId` from the correctness-critical cache path. Add `StateResyncRequest/Response` and domain freshness tracking.

Keep `SnapshotMessage`, `SnapshotDelta`, `SnapshotBaselineState`, and the current snapshot budgeter as a legacy adapter until all canonical clients use the new events. Do not delete the old path until replay and compatibility tests pass.

Exit criteria:

- a client can rebuild players/projectiles from a resync without a prior baseline;
- class identity cannot be changed by a stale cache ID;
- state application is atomic and acknowledgement means applied;
- a dropped or malformed state event produces a repair/freshness signal, not a blind client;
- large states are framed by QUIC/WS rather than forced into UDP MTU assumptions.

### Stage 5 — durable input commands

Introduce `InputCommand`, `InputConsumed`, and `InputRejected`. Keep held state LastWins, but move all one-shot actions to reliable ordered commands. Replace the inferred edge-only server behavior with a command ledger and exactly-once simulation application.

Exit criteria:

- a dropped jump command is retransmitted and consumed once;
- duplicate/reordered commands are deterministic;
- the client can distinguish received, applied, and rejected;
- snapshots are no longer needed to prove that a jump was consumed.

### Stage 6 — canary and default selection

Run dual-stack compatibility behind configuration, for example:

- backend: `legacy-udp`, `websocket`, or `quic`;
- schema family: legacy codec or protocol-58 schema registry;
- state mode: legacy snapshot or split authoritative events;
- input mode: legacy input state or durable commands.

Roll out in this order: loopback, browser WebSocket, native QUIC on representative regions, then long-hop/high-loss canaries. Keep automatic fallback to the legacy path only when negotiation fails; do not silently downgrade a successfully negotiated new connection because of a malformed event.

### Stage 7 — retirement

After an agreed soak period, make QUIC the native default and WebSocket the browser/default portable container. Retire raw UDP from the normal endpoint, remove the old central codec and snapshot delta dependency, and leave a read-only legacy adapter only if replay or old-server compatibility requires it.

## Test matrix and acceptance criteria

### Protocol tests

- one schema round-trip per event and per protocol revision;
- exact body consumption and trailing bytes;
- unknown event/schema behavior;
- truncated header/body and impossible lengths;
- compressed payload expansion limits and invalid compression;
- invalid enum/identity/generation fields;
- per-event maximum size;
- delivery annotations are present and match the registry;
- version/capability negotiation between legacy, schema-58, and future versions.

### Backend tests

- complete frames across WebSocket fragmentation;
- no reliable queue drop under sustained load;
- LastWins retains only the newest complete state;
- reliable ordered delivery under concurrent writes;
- reliable unordered delivery across N logical/QUIC streams;
- loss, duplication, reordering, delay, and stream reset;
- QUIC stream repair and dedicated retransmit stream;
- WebSocket retry budget and second-failure disconnect;
- frame ID/epoch correlation and protocol-error close;
- backpressure and cancellation behavior.

### Gameplay integration tests

- a full player state survives loss of an intermediate motion update;
- a missing state event triggers a full player/projectile repair;
- a player can never change class because a stale or missing cache entry resolved to another class;
- projectile kind and identity remain stable across spawn, update, and reuse;
- health, abilities, and class identity commit atomically;
- jump/fire/ability/interact commands survive drop and reordering and apply once;
- acknowledgement occurs after application, not after queueing;
- an invalid server frame is logged, ignored, repaired when applicable, and does not corrupt the next valid frame.

### Path and soak tests

- payloads larger than 1400 bytes over the legacy backend are measured and rejected from unsafe lanes;
- high-hop/low-MTU fault injection has no blind-input state on QUIC/WS;
- sustained browser backpressure does not lose control/chat/input-command events;
- multi-hour matches with reconnect, stream reset, state repair, and class changes;
- mixed legacy/new clients during the migration window;
- server restart/session epoch reset proves no cache identity leaks across sessions.

## Observability required for the rollout

Every connection should expose counters and recent samples for:

- frames sent/received by event, backend, channel, and delivery kind;
- bytes before/after compression;
- reliable queue depth/bytes and backpressure duration;
- LastWins replacements and stale-frame discards;
- decode failures by typed fault kind;
- stream resets, reconnect attempts, repair requests, and repair success;
- protocol-error disconnects;
- state domain age and last successful full repair;
- input command received/applied/rejected/duplicated and command latency;
- class identity validation failures and cache/dictionary misses.

The key dashboards should answer “is the connection alive?”, “is each state domain fresh?”, and “was this input applied?” independently. A single “last message received” timestamp is not sufficient.

## Implementation guardrails

- Do not build new reliability behavior into `ProtocolCodec` based on `MessageType` switches.
- Do not call a transport reliable because it has a queue; define ownership and acknowledgement semantics.
- Do not use `LastWins` for commands, lifecycle events, removals, chat, or partial deltas.
- Do not acknowledge a state or command before the consuming layer has committed/applied it.
- Do not let a cache reference determine class/entity identity without an epoch and a reliable apply contract.
- Do not return an empty/default state after a decode failure.
- Do not treat a payload target as a hard MTU guarantee; the backend determines safe framing.
- Keep all migration switches removable and test both paths until retirement.

## Recommended first implementation slice

The highest-value first slice is not QUIC itself. It is the schema/frame/backend seam plus deterministic fault tests:

1. Add typed frame decode results and the fault callback.
2. Add schema delivery metadata and registry entries.
3. Refactor WebSocket to a no-drop reliable channel implementation with frame IDs and repair.
4. Send a self-contained authoritative player-state event with class identity inline.
5. Send jump as a durable input command with an applied acknowledgement.

That slice directly addresses all four reported symptoms and creates the interfaces the QUIC backend can implement without another protocol rewrite.
