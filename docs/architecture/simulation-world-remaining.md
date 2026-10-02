# SimulationWorld: remaining work

Companion to [simulation-boundaries.md](simulation-boundaries.md). That document
describes the target and what is already enforced. This one lists what still lives
on `SimulationWorld` and a suggested order for moving it.

Numbers below come from a line count of `Core/Simulation/**/SimulationWorld*.cs`
after chunk 4 (structures, pickups, ability rules, network-player lifecycle,
objective rules). They are approximate and will drift.

## Where things stand

- 100 `SimulationWorld*.cs` files.
  - 77 hold rule logic (about 21.5k lines, including `SimulationWorld.cs` itself).
  - 23 are host, forwarder, or delegate glue (about 1.3k lines).
- State is already out of the world. It holds 2 instance fields (`_runtime`,
  `_playerCounts`) and exposes owned stores and systems as properties.
- Systems already extracted: match phase, entity phase, objectives, combat
  geometry, weapon fire, structures, pickups, objective rules, ability rules, and
  network players. `CombatSystem`, `SnapshotSystem`, `ProjectileSystem` and the
  movement system were extracted earlier. Their top-level files are in `Systems/`, but their partials are in `Projectiles/` and `Movement/`.
- The remaining work is rule logic, not state. Most of it still reads and writes
  the stores through `this`.

## Remaining areas

Sizes are line counts of the partials involved. "Coupling" is what makes the area
hard to move.

### 1. Last-to-die (done in chunk 5a)

- All eight `LastToDie/SimulationWorld.LastToDie*.cs` partials moved into
  `LastToDieRulesSystem` (`Systems/LastToDie/`), one partial per area: perks,
  status effects, medic links, medic javelin, sniper explosive tip, afterlife,
  survivor, objectives. The `LastToDie/` world folder is gone.
- `ILastToDieHost` (about 40 members) is the wide host this section predicted.
  It reuses `ISimulationWorldState`, `ISimulationPlayerDirectory` and
  `ISimulationPresentationEvents`.
- The spy-afterlife pending-death map moved off the world with it.
- `SimulationWorld.LastToDieRulesSystemForwarders.cs` keeps every member that
  Client, Server, tests or other world partials call, with the original names,
  signatures and access, so reflection-based tests still resolve.
- Explosion distance and direction math moved to `ExplosionGeometry`, shared by
  the world's explosions and the Last-to-Die blasts.
- Left for later: `GetLastToDieGameplaySettings` and
  `IsLastToDieGameplaySettingEnabled` are still called through world forwarders
  by the experimental rules and many hosts. Settle that boundary in chunk 5b.

### 2. Experimental gameplay rules (about 1.8k lines)

| File | Lines |
| --- | --- |
| `Combat/SimulationWorld.ExperimentalEngineer.cs` | 1042 |
| `Combat/SimulationWorld.ExperimentalGameplay.cs` | 553 |
| `Combat/SimulationWorld.ExperimentalRage.cs` | 196 |

- `ExperimentalEngineer` holds the sentry firing code (`FireExperimentalSentry`,
  `ApplyExperimentalSentryStructuralTargetDamage`, and related methods). The
  extracted `StructureSystem` calls these back through `IStructureHost`, so moving
  them removes several host members.
- Natural home: an engineer-rules or experimental-rules system next to
  `StructureSystem`, or folded into it.
- Coupling: shares settings (`ExperimentalGameplaySettings`) with last-to-die.
  Decide the settings boundary before doing both.

### 3. Explosions and rockets (about 1.2k lines)

- `Combat/SimulationWorld.Explosions.cs` (695), `RocketExplosions.cs` (460),
  `ExplosiveSplashBalance.cs` (16).
- Coupling: damage, knockback, damageable zones (`ApplyExplosiveDamageToDamageableZones`),
  and projectile removal. Depends on damage events and on the zones area below.

### 4. Medic and buffs (about 730 lines)

- `Combat/SimulationWorld.Medic.cs` (592), `BuffBanner.cs` (108),
  `HealingEvents.cs` (33).
- Medic constants already alias `MedicBeamDefaults`. The beam logic is still on
  the world. Pairs with last-to-die medic links, so extract them together or in
  quick succession.

### 5. Airblast and projectile interactions (about 730 lines)

- `Combat/SimulationWorld.Airblast.cs` (470), `ProjectileInteractions.cs` (207),
  `Boomstick.cs` (26), `ProjectilePresentationCollision.cs` (24).
- Coupling: close to `ProjectileSystem`. Check whether it belongs there rather than
  in a new system.

### 6. Hit detection and presentation collision (about 850 lines)

- `Combat/SimulationWorld.PlayerPresentationCollision.cs` (483),
  `HitDetection.cs` (264), `CombatPerformance.cs` (102).
- `PlayerPresentationCollision` holds static sprite-asset caches
  (`_gameMakerAssets`, `_resolvedPresentationSpriteAssets`, locks). These are
  process-wide mutable statics on the world type. Move them behind a small
  presentation-bounds service; they are not simulation state.
- `HitDetection` should sit with `CombatResolver`.

### 7. Combat odds and ends (about 400 lines)

- `DamageEvents.cs` (156), `Afterburn.cs` (61), `WhippingCordTerrain.cs` (61),
  `ServerCombatRules.cs` (56), `Domination.cs` (44), `WeaponFiring.cs` (24), and
  several files under 30 lines.
- Low risk. Fold into whichever combat system absorbs damage handling. Damage
  handling is the piece many other areas need, so give it a clear owner early.

### 8. Match, map, and mode rules (about 2.3k lines)

| File | Lines | Note |
| --- | --- | --- |
| `Match/SimulationWorld.Vip.cs` | 436 | Reads and writes `VipState` |
| `Match/SimulationWorld.ControlPointStateSystem.cs` | 417 | Private nested static class taking `SimulationWorld world` |
| `Match/SimulationWorld.MapLifecycle.cs` | 289 | Level load, round restart, pending map change |
| `Match/SimulationWorld.MapLogic.cs` | 260 | Map logic graph evaluation and triggers |
| `Match/SimulationWorld.CompetitiveReadyUp.cs` | 234 | Reads and writes `ReadyUpState` |
| `Match/SimulationWorld.DamageableZones.cs` | 221 | Zone health and damage triggers |
| `Match/SimulationWorld.ControlPointSetupSystem.cs` | 213 | Nested static class taking the world |
| `Match/SimulationWorld.ForegroundSprites.cs`, `Spritesheets.cs` | about 125 | Presentation-adjacent |

- `ControlPointStateSystem` and `ControlPointSetupSystem` are named like systems
  but are still world partials that pass the world around. They are the nested
  static classes behind `SimulationWorld.ControlPointEntryPoints.cs`.
- `ObjectiveRulesSystem` already owns control-point rules, so these two are the
  natural next additions to it.
- `MapLifecycle` is the riskiest of the group because it resets nearly every
  store. Treat it as a coordinator that stays near the world until the rest is out.
- Suggested systems: `VipRulesSystem`, `ReadyUpSystem`, `MapLogicSystem` (with
  damageable zones), and `MapLifecycleCoordinator`.

### 9. Snapshots and protocol state (about 2.8k lines)

| File | Lines |
| --- | --- |
| `Networking/SimulationWorld.Snapshots.cs` | 1986 |
| `Networking/SimulationWorld.Protocol64State.cs` | 431 |
| `Networking/SimulationWorld.SnapshotWorldState.cs` | 158 |
| `Networking/SimulationWorld.SnapshotPlayers.cs` | 108 |
| `Networking/SimulationWorld.SnapshotEvents.cs` | 68 |

- A `SnapshotSystem` already exists. The world side is still the biggest single
  file in the world: `ApplySnapshot*` for every entity kind, plus protocol 64
  entity creation and hydration.
- Coupling: it touches every other system (health packs, turrets, jump pads,
  sentries, projectiles, players). After chunk 4 these have systems to call, so
  this is now feasible.
- Do this late. It benefits most from the other systems existing, and it has the
  strongest network and replay risk. Split by entity kind before moving anything.

### 10. Runtime: input, lifecycle, spawning, and the rest (about 5.5k lines)

| File | Lines |
| --- | --- |
| `Runtime/SimulationWorld.InputHandling.Actions.cs` | 659 |
| `Runtime/SimulationWorld.LifecycleEffects.cs` | 569 |
| `Runtime/SimulationWorld.Spawning.cs` | 560 |
| `Runtime/SimulationWorld.InputHandling.PlayerAdvance.cs` | 532 |
| `Runtime/SimulationWorld.Dummies.cs` | 518 |
| `Runtime/SimulationWorld.Lifecycle.DeathAndRespawn.cs` | 466 |
| `Runtime/SimulationWorld.Helpers.cs` | 322 |
| `Runtime/SimulationWorld.ServerTuning.cs` | 261 |
| `Runtime/SimulationWorld.Lifecycle.KillFeed.cs` | 224 |
| `Runtime/SimulationWorld.Environment.cs` | 216 |
| `Runtime/SimulationWorld.SpawnClassBehavior.cs`, `ClassLimits.cs` | about 235 |
| `Runtime/SimulationWorld.Scorekeeping.cs`, `AdminCheats.cs`, `CivvieMoney.cs` | about 210 |

- Input: `InputHandling.Actions` routes primary and secondary fire for the network
  player and is the main caller of `WeaponFireHandler` and the ability system.
  `PlayerAdvance` is the per-player tick step and contains slow-phase tracing
  statics. Together they would form a `PlayerInputSystem`.
- Lifecycle and spawning: death, respawn, kill feed, gibs and blood effects, spawn
  selection. Gibs and blood (`LifecycleEffects`) are mostly presentation and could
  be a separate effects system. Death and respawn plus spawning plus kill feed is
  a `PlayerLifecycleSystem`.
- Dummies: practice dummies and the DPS meter. Self-contained, with state already
  in `PracticeDummyState`. Low risk and a good warm-up.
- `Helpers` is a grab bag. It still holds `DistanceBetween` and
  `PointDirectionDegrees` copies next to `SimulationMath`, combat traces, blood
  and visual-effect registration, and aim indicators. Move each piece to its owner
  and delete the file.
- `ServerTuning` is per-slot override setters and getters. It belongs in
  `NetworkPlayerSystem` or a small `MatchTuning` system, and is mostly mechanical.
- `Environment` covers healing cabinets, spawn-room state, and room hazards for the
  per-player tick. It becomes a `RoomEffectsSystem`.

### 11. The world itself (about 700 lines)

- `Core/SimulationWorld.cs` (476): constants, stores and system properties, the
  public property surface (many `Configured*` pass-throughs), and the constructor.
- `Core/SimulationWorld.Decisions.cs` (235): decision-interceptor delegates for
  plugins and the `ShouldCancel*` helpers. These are an extension point, not rules.
  A `DecisionGate` object owned by the world would keep the plugin surface stable.
- World constants that are still private (`DefaultRespawnSeconds`,
  `KillFeedLifetimeTicks`, and others) should move to `SimulationConstants` or to
  their owning system as each area leaves.

## Cross-cutting issues

- **Forwarders and wide hosts.** The 23 glue files are the cost of doing this
  incrementally. As callers (Client, Server, bots, tests) move to the systems
  directly, forwarders can be deleted. The host interfaces are broad, so split them
  by concern as their rule sets settle.
- **Callers outside the simulation.** About 35 Client files, 32 Server files, 3
  `SessionRuntime` files, and 3 tools reference `SimulationWorld`. Core has about 30
  non-partial files that mention it (bots, gameplay runtimes, map logic).
  `GameplayPrimaryWeaponContext` and `GameplayAbilityContext` expose `World` to
  mods, so the world cannot be fully hidden without a mod-API decision.
- **Tests by reflection.** About 113 test lines use `typeof(SimulationWorld).Get*`
  on private members. Each extraction can break them silently at runtime (chunk 4
  broke one: `GetNestedType("SentryTarget")`). Prefer the existing
  `CombatTest*` seams, or add internal test hooks, when touching these.
- **System partials outside `Systems/`.** The architecture script only scans
  `Core/Simulation/Systems`. The `ProjectileSystem` and `MovementSystem` partials
  live in `Core/Simulation/Projectiles` and `Core/Simulation/Movement`, so the rule
  does not check them. The `ProjectileSystem` partials use
  `using SimulationWorld = OpenGarrison.Core.ProjectileSystem;` to reuse old code.
  That alias is a shim to remove, and those folders should come under the same rule.
- **Static state on the world type.** Presentation sprite caches
  (`PlayerPresentationCollision`) and slow-phase tracing (`PlayerAdvance`) are
  process-wide statics. They are not replay-relevant but they are shared mutable
  state; move them out with their areas.
- **Nested static helpers that take the world.** `ControlPointStateSystem` and
  `ControlPointSetupSystem` pass `SimulationWorld world` to each other. These need
  a host before they can leave.

## Suggested order

Each step is sized to be one reviewable chunk.

| # | Chunk | Why now |
| --- | --- | --- |
| 5a | Last-to-die rules (done) | Largest cohesive block |
| 5b | Experimental engineer, gameplay, and rage rules | Removes sentry callbacks from `IStructureHost`; settle the experimental-settings boundary with `LastToDieRulesSystem` |
| 6 | Medic and buffs, explosions and rockets, airblast and projectile interactions, damage events | Finishes combat; needs a clear damage-handling owner |
| 7 | Match: VIP, ready-up, control-point state and setup, map logic and damageable zones | Mostly state-backed already; control-point classes join `ObjectiveRulesSystem` |
| 8 | Runtime: dummies, server tuning, environment, helpers cleanup, then player lifecycle and spawning, then input handling | Dummies and tuning are warm-ups; lifecycle and input are the highest-traffic code |
| 9 | Snapshots and protocol 64 world side | Last, once every entity kind has a system to call; split by entity kind first |
| 10 | World cleanup: `Decisions`, constants, `Configured*` pass-throughs, forwarder removal, bring the Projectile and Movement partials under the rule | Closes the gaps and shrinks the world to orchestration |

Chunks 5 to 8 can swap order. Chunk 9 should stay late, and chunk 10 last.

## Definition of done

- `SimulationWorld` holds only construction, the stores and systems it
  composes, the public API surface, and the plugin decision gate.
- No rule logic in `SimulationWorld*.cs`. Remaining partials are host
  implementations and forwarders only.
- All system files, including partials, live under `Core/Simulation/Systems/` and pass the "no
  `SimulationWorld` in code" rule, with no `using SimulationWorld = ...` aliases.
- Forwarders deleted or reduced to the public API that Client, Server, and mods
  genuinely need.
- Tests use supported seams rather than reflection on private world members.
- Replay goldens, the architecture script, and the full CI gate are green.
