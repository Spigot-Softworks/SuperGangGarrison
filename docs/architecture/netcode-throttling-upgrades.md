# Network pressure upgrades

This page covers six non-Protocol64 upgrades for snapshot bandwidth, baseline
recovery, and UDP delivery. Protocol version **108** includes the new snapshot
and input extensions; client and server need matching versions to use them.
Protocol64-specific state and message formats are outside this work.

The changes preserve simulation and input cadence and do not remove fields from
protocol record definitions or change existing serialization and quantization.
Live snapshot broadcasts use the untrimmed path, so all planned player, entity,
and transient-event contributions are sent even when the measured payload is
above its target. The older snapshot reduction path remains for tests and
compatibility callers, but is disabled for live broadcasts.

## 1. Scoreboard delta records

Delta snapshots carry the complete scoreboard slot order and patches only for
changed canonical player records. A patch reuses the shared prefix and suffix
of the old and current serialized records and sends replacement bytes for the
changed middle. New players and slot identity changes carry a complete record;
omitted slots represent removals. Reordering sends only the new order, and an
empty order explicitly clears a nonempty baseline roster.

Every scoreboard field continues to use the established record serializer,
including mod state, hidden spy state, viewer-specific domination state, and
late status fields. Full snapshots keep their existing wire layout. The delta
extension is used only when patches fit its limits and its encoded bytes are
smaller than sending the full roster. A legal record larger than the 64 KiB
patch limit falls back to the complete legacy roster instead of being rejected
or truncated. Patch indices, order, identities, lengths, and UTF-8 are checked
when resolving a delta.

## 2. Snapshot acknowledgements in input

Protocol 108 can attach the newest pending snapshot acknowledgement to an input
message. A newly advanced frame is attached to the next input immediately;
retry throttling applies only when retransmitting the same frame. If input
traffic does not carry an acknowledgement, a standalone fallback sends it
after the server-tick interval. The client keeps the frame pending until a
server delta proves that the server advanced to that baseline. Same-frame
retries use an RTT-based interval clamped to 100–1,000 ms. Input cadence and
configured input delay are preserved.

## 3. Coordinated baseline pins

Both peers keep rolling snapshot history and retain the exact baseline still
referenced across acknowledgement delays. The client history holds 96 frames
and pins the baseline referenced by the latest received delta until a later
snapshot proves a switch. The server history adapts between 12 and 48 frames,
with a separate reference to the last acknowledged baseline. This lets either
side resolve valid in-flight deltas after older history entries roll out of the
rolling window.

## 4. Acknowledged snapshot string cache

Per-client string mappings are repeated in snapshots until a snapshot carrying
the mapping is acknowledged. The client applies mapping updates immediately
after resolving a received snapshot, before queueing world application, so a
later snapshot can use the mapping even if an older queued snapshot is dropped.
Cache state is reset when the connection generation changes. Player strings
and their uncached fallback representation keep the same wire rules.

## 5. UDP fragmentation, selective repair, and path MTU

UDP datagrams are sent with Don't Fragment enabled where the platform supports
it: IPv4 uses the socket DF setting, and IPv6 requests the corresponding option
when available. Larger protocol payloads are split into checksummed,
generation-tagged fragments and reassembled by peer and message ID. Receivers
request missing chunks selectively; each NACK names at most eight chunks. The
repair queue and completed-message tracking are bounded.

Before peer admission, the server only reassembles Hello messages. This path is
limited to a 4 KiB declared message, four assemblies and 16 KiB per source,
2 MiB globally, 512 completed IDs, and a 1.5 second assembly lifetime. Only a
complete, decoded Hello reaches normal admission; other framed messages are
discarded. Unknown sources can receive at most one acknowledgement per second
for a 548- or 1,200-byte MTU probe. That uses a 256-source table with ten-second
expiry and does not create active peer state. Active path probing starts after
a Welcome, PasswordRequest, or Snapshot, or when an outgoing payload is large
enough to fragment. A large one-off response gets bounded, short-lived repair
and path state; small status or details replies do not activate peer state.

Protocol UDP messages are limited to 16 MiB. Regular reassembly expires after
five seconds and is bounded to 128 incomplete messages and 16 MiB per peer,
2,048 assemblies and 32 MiB globally. The sender retains at most 128 repairable
messages per peer for five seconds, with a 32 MiB aggregate payload cache.
Active peer state expires after ten seconds without traffic, clearing related
MTU, reassembly, send cache, and repair-queue state.

Path MTU discovery tracks each direction independently. It starts at 1,200
bytes and probes up through 1,400 bytes; acknowledgements must match the active
peer nonce and probe size. After two failed initial probes, the path confirms a
548-byte fallback. The ceiling falls to 548 only after that probe is
acknowledged; failed upward probes keep the previous confirmed ceiling.

## 6. Burst and control retry reduction

Normal input can bundle up to 16 due control commands, reducing standalone
command datagrams. Commands remain pending until acknowledged and retry at an
RTT-based interval clamped to 100–1,000 ms. When configured input delay would
delay a command's first send, that command stays standalone so its timing is
preserved. The snapshot acknowledgement shares the same regular input traffic.

UDP repair output is paced to one datagram per peer every 20 ms. Fresh outgoing
messages are sent before queued repairs are serviced, so retransmissions for
older messages do not delay current gameplay traffic. Queued repairs expire
after five seconds and are checked against the active assembly or message
generation before sending. Probe and repair datagrams are best-effort; their
socket errors do not replace an error from a fresh payload send.

## Validation status

**Passed:** 31 Core tests and 9 Networking tests. **Pending:** aggregate
protocol and PluginHost results after the remaining shared changes settle.
Focused coverage includes scoreboard equivalence, add/remove/reorder, identity
replacement, explicit empty rosters, oversized legal records, malformed patch
data, acknowledgement timing and baseline pinning, cache lifecycle, fragment
loss and expiry, queue bounds, and MTU probing.

**Pending:** record the measured compressed LZ4 sizes for the representative
24-player movement and late-status scenario and its full-roster comparison.
No compression or performance result is claimed before that run completes.
