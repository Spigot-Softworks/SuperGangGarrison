# Simulation Boundaries

These are target ownership rules for the simulation architecture. The
[current status](#current-status) section records how far the code has come and
which rules `scripts/verify-architecture-boundaries.ps1` enforces today.

## World orchestration

- **Rule:** `SimulationWorld` is pure orchestration: it owns one `SimulationRuntime`, exposes systems, and holds no subsystem state of its own.
- **Rationale:** Keeping coordination separate from state makes subsystem ownership explicit and keeps the world replaceable.
- **Violation:** Adding a private projectile list, combat timer, or movement state directly to `SimulationWorld`.

## System state

- **Rule:** `EntityStore` owns entity lifetime; `CombatSystem` owns damage and hit resolution; `ProjectileSystem` owns projectiles; `MovementSystem` owns movement; and `SnapshotSystem` owns network snapshots.
- **Rationale:** Each piece of mutable simulation state has one authoritative owner.
- **Violation:** `SimulationWorld` or `CombatSystem` maintaining a second authoritative projectile collection.

## Narrow dependencies

- **Rule:** Systems depend on narrow interfaces such as `ICombatWorldView`, never on `SimulationWorld` internals or private fields.
- **Rationale:** Interface-sized dependencies prevent systems from coupling to unrelated orchestration details.
- **Violation:** A system accepting `SimulationWorld` so it can read a private entity map or call another system's implementation details.

## Back-references

- **Rule:** Do not add back-references from systems into `SimulationWorld`.
- **Rationale:** One-way ownership prevents systems from reaching around the runtime and creating hidden control flow.
- **Violation:** Giving `MovementSystem` a stored `SimulationWorld` reference so it can trigger world-level updates.

## Tick ordering

- **Rule:** `SimulationRuntime.Tick()` owns and documents system tick ordering.
- **Rationale:** A single runtime entry point makes update sequencing deterministic and reviewable.
- **Violation:** `CombatSystem.Tick()` directly invoking `ProjectileSystem.Tick()` to impose an order outside the runtime.

## Current status

`SimulationWorld` is not yet pure orchestration. It is still a partial class of
roughly 120 files that hosts most rule logic, but it no longer owns any mutable
state of its own, and no nested type stores a reference back to it. Enforcement
lives in `scripts/verify-architecture-boundaries.ps1`.

### Owned state (done)

| Owner | State it owns |
| --- | --- |
| `EntityStore` | Entity lifetime, identity, and the next-entity-ID counter |
| `CombatSystem`, `ProjectileSystem`, `MovementSystem`, `SnapshotSystem` | Damage and hit resolution, projectiles, movement, snapshots |
| `PresentationEventLog` | Sound, visual, gib, healing, and gameplay-ability events; combat traces; sniper aim indicators; kill feed |
| `WorldObjectStore` | Sentries, jump pads, turrets, generators, health packs, dropped weapons, bodies, gibs, blood |
| `ObjectiveStateStore` | Arena, control-point, KOTH, and SCR objective state |
| `NetworkPlayerRegistry`, `RemoteSnapshotPlayerRegistry` | Per-slot network player state, and client-side mirrors of players received in snapshots |
| `VipState`, `CompetitiveReadyUpState`, `PracticeDummyState`, `LastToDieState` | VIP assignments, ready-up phase, practice dummies, Last to Die runtimes |
| `MatchSettingsState` | Time/cap/respawn limits, tuning scales, speed clamps, class limits |
| `ClientSnapshotState` | Snapshot reconciliation sets, terminated/predicted projectile IDs, spectators, string cache |
| `MapRuntimeState` | Damageable zones, map-logic activators, sprite-sheet playback, special CTF flags |
| `CombatRuntimeState` | Queued rocket bursts, danger-close explosions, spread indices, hit-bounds cache |
| `LocalSimulationState`, `MatchLifecycleState` | Local input/class/join state; spawn rotation and pending map change |
| `SimulationRandomStreams` | The gameplay, death-cam, and bot-awareness random streams |

The only instance fields left on the world are the `SimulationRuntime`, two lazily
created helpers (`CombatResolver`, `WeaponFireHandler`), and `PlayerCountQueries`.

### Tick ordering and rules (done)

`SimulationRuntime.Tick()` is the only place that decides phase order. It reaches
the world through `ISimulationTickHost` and two phase systems:

- `MatchTickPhase` uses `IMatchPhaseHost`.
- `EntityTickPhase` uses `IEntityPhaseHost`.

Other rule sets that moved out of the world and now reach it only through a host
interface:

| System | Host interface | Location |
| --- | --- | --- |
| `MatchObjectiveSystem` and the per-mode objective/resolution controllers | `IMatchObjectiveHost` | `Systems/Objectives/` |
| `PlayerCountQueries` | `IPlayerCountHost` | `Systems/Objectives/` |
| `CombatResolver` (raycasts, line of sight, hit queries) | `ICombatGeometryHost` | `Systems/Combat/` |
| `WeaponFireHandler` (primary-weapon routing and firing) | `IWeaponFireHost` | `Systems/Combat/` |
| `StructureSystem` (sentries, Civil Defense turrets, jump pads) | `IStructureHost` | `Systems/Structures/` |
| `PickupSystem` (health packs, dropped weapons) | `IPickupHost` | `Systems/Pickups/` |
| `ObjectiveRulesSystem` (generators, intel, KOTH, SCR, control points) | `IObjectiveRulesHost` | `Systems/ObjectiveRules/` |
| `GameplayAbilitySystem` (ability rules, plugin ability operations) | `IGameplayAbilityHost` | `Systems/Abilities/` |
| `NetworkPlayerSystem` (network-player lifecycle, configuration, input state) | `INetworkPlayerHost` | `Systems/NetworkPlayers/` |
| `LastToDieRulesSystem` (perk builds and prediction profiles, status effects, medic links, medic javelin, sniper explosive tip, spy afterlife, survivor/stage rules) | `ILastToDieHost` | `Systems/LastToDie/` |

Pure geometry helpers shared by the world and these systems live in
`SimulationMath` and `ExplosionGeometry`, and constants both sides need live in `SimulationConstants`
(the world's own consts alias them). The systems are exposed on the world as
`Structures`, `Pickups`, `ObjectiveRules`, `Abilities`, `NetworkPlayerRules`, and
`LastToDieRules`.
The `SimulationWorld.*Forwarders.cs` files are thin delegating members kept for
existing callers; callers can move to the systems directly over time. The world builds `GameplayPrimaryWeaponContext` itself, because
that mod-facing type exposes the world.

### Known gaps

See [simulation-world-remaining.md](simulation-world-remaining.md) for the full outline of what is left and a suggested order.

- The world is still the home of the remaining rule logic (experimental
  engineer/gameplay/rage, explosions, medic, airblast, spawning,
  lifecycle, snapshots). The next step is moving those behind host interfaces the
  way combat, objectives, structures, pickups, abilities and network players were.
- Some host interfaces are wide: `IWeaponFireHost` has about 65 members and
  `IMatchObjectiveHost` about 35; the structure, pickup, ability and network-player
  hosts are similarly broad, and `ILastToDieHost` has about 40 because perks
  reach into damage, healing, network slots and objectives. They record what each rule set still needs from
  the world, and splitting them by concern is the way to shrink them.
- `FixedStepSimulator` is the external tick driver and holds the world on purpose;
  it is the single allowlist entry.

### Enforced rules

| Rule | Check |
| --- | --- |
| Systems do not reference `SimulationWorld` | Any code mention in `Core/Simulation/Systems` (including `Objectives/` and `Combat/`) fails |
| No duplicated projectile collections on the world | Reintroduced `_shots`, `_mines`, and similar fail |
| `SimulationRuntime` owns tick ordering | The type must exist, `AdvanceOneTick` must call `Tick()`, and the retired controllers may not return |
| World field-count ratchet | More than the current limit (4) fails; when state is extracted, lower the limit |
| No new `_world` back-references | A new file storing `SimulationWorld` outside the allowlist fails, and so does a stale allowlist entry |
