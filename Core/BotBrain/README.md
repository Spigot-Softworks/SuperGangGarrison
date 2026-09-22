# Bot runtime ownership

BotBrain is the current autonomous bot implementation. The `Practice` names in
its adapter and factory also apply to dedicated servers and embedded sessions.
`PracticeBotControllerFactory.Create` always creates a `BotBrainPracticeBotController`;
`OfflineBotControllerMode` currently has only the `BotBrain` value.

## Entry points

| Session | Bot input owner |
| --- | --- |
| Dedicated server | `ServerRuntimeBootstrapFactory` creates `ServerBotManager` with the BotBrain adapter. `GameServer.AdvanceAuthoritativeSimulation` feeds bot inputs before each simulation tick. |
| Embedded sessions, including player-facing solo Last to Die | `SessionRuntime/GameServer.Embedded.cs` uses the same server bootstrap and authoritative simulation method. |
| Local offline Practice | `Game1.GameplayOfflineSessionController` starts the session; `Game1.PracticeBots` schedules the same BotBrain adapter directly. |
| Older client-owned Last to Die performance path | Uses the local Practice bot plumbing. This separate session implementation is not the player-facing solo Last to Die launch path. |

The common decision path is:

```text
server or local Practice scheduler
  -> BotBrainPracticeBotController.BuildInputsForSlots
  -> per-slot BotBrainController.Think
  -> PlayerInputSnapshot
  -> SimulationWorld
```

Full decisions are staggered across ticks. Cached navigation can advance between
full decisions. On the server, mimic bots and follow-healer companions can have
their final inputs replaced by `ServerBotManager`; dummy bots are excluded from
its autonomous controlled-slot list.

## Navigation

OG2 navigation is the only controller mode. The controller reads a warmed graph
from `Og2NavigationGraphStore` or a compatible shipped graph. It never builds a
graph during a live bot tick. An injected `NavGraph` uses the same decision path.
Some current helpers and diagnostics retain the historical `Alpha` name.

If no usable graph exists, graphless objective seeking, local movement recovery,
and combat remain active. Map-specific helpers still reachable through this path
are retained, including Harvest spool recovery and Orange carrier finishing.
The default graph builder also uses movement validation and class profiles from
`Core/BotAI`; that directory is still a dependency.

## Retired navigation

September 2026 removed the opt-in legacy controller mode, objective tapes, verified
proof-route replay, and recovery branches reachable only through that mode. The
`BOTBRAIN_NAVIGATION_MODE` setting no longer selects an alternate controller.
Tape/proof stores, executors, shipped assets, warmups, metrics, packaging entries,
and their authoring/export commands were removed together. Map-specific branches
used by current OG2 or graphless behavior remain.

`Tools/BotBrain` retains current OG2 reports, graph validation, traversal soaks,
room-object inspection, and movement diagnostics. `Tools/MotionProof` retains
physics/path validation. VerifiedNav surface/candidate exploration and authored
corridor tooling remain independent of the retired tape/proof replay formats.

## Tests

The old `bot-score-matrix` CI job was retired: its final command only dumped room
objects and returned before navigation began. Tape/proof-only tests are retired
with their implementation. Current graph loading, graphless recovery, class
behavior, combat, targeting, and movement tests remain supported.

Legacy capture-strafe fixture expectations were also retired. Injected graph
fixtures now exercise the same navigation policy as live bots.
