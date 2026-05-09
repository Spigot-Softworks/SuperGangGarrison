# Clean-Sheet Bot System for OpenGarrison

## Core Engine Analysis

Before proposing any design, here is what I learned about how this engine actually works — the contracts a bot system must honor.

---

### 1. Simulation Model

The game runs a **deterministic fixed-step simulation** at 30 ticks/second (configurable up to 120). Every game entity lives inside [SimulationWorld](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Simulation/Core/SimulationWorld.cs).

The tick loop in [RuntimeController](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Simulation/Runtime/SimulationWorld.RuntimeController.cs) is:

```
AdvanceOneTick():
  1. AdvancePrePlayerSimulationPhase()     — projectiles, gibs, transient entities
  2. AdvancePlayerSimulationPhase()        — for each network player slot: resolve input → advance
  3. AdvancePostPlayerSimulationPhase()    — health packs, sentries, jump pads, match state, scoring
  Frame++
```

### 2. The Input Contract — The Only Interface That Matters

Every player (human or otherwise) is driven exclusively by [PlayerInputSnapshot](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Simulation/Core/PlayerInputSnapshot.cs):

```csharp
record struct PlayerInputSnapshot(
    bool Left, bool Right, bool Up, bool Down,
    bool BuildSentry, bool DestroySentry, bool Taunt,
    bool FirePrimary, bool FireSecondary,
    float AimWorldX, float AimWorldY,
    bool DebugKill,
    bool DropIntel, bool FireSecondaryWeapon, bool InteractWeapon);
```

The simulation reads this snapshot via `_additionalNetworkPlayerInputs[slot]`. The `AdvancePlayableNetworkPlayer(slot)` method in [InputHandling.NetworkState](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Simulation/Runtime/SimulationWorld.InputHandling.NetworkState.cs) resolves the input and calls `AdvanceAlivePlayerWithInput()`.

> [!IMPORTANT]
> **A bot's entire interface to the game is writing a `PlayerInputSnapshot` into a network player slot each tick.** There is no special movement API, no teleportation, no pathfinding hook. Bots must synthesize the same Left/Right/Up/Down/Aim/Fire booleans a human would press. The simulation handles all physics, collision resolution, and weapon firing from that snapshot.

### 3. Movement Physics

Movement is implemented in [LegacyMovementModel](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Simulation/Core/LegacyMovementModel.cs) — a source-engine-inspired half-tick integration model:

| Property | Value |
|---|---|
| Source ticks/sec | 30 |
| Gravity | 0.6 per source tick (≈540 units/s²) |
| Blast gravity | 0.54 per source tick |
| Max fall speed | 10 per source tick (300 units/s) |
| Base control factor | 0.85 |
| Base friction factor | 1.15 |
| Step-up height | 6 units |
| Step-down height | 6 or 12 units |
| Max collision iterations | 10 per tick |

The player tick pipeline in [Movement.Runtime](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Entities/Players/Movement/PlayerEntity.Movement.Runtime.cs) is:

```
Advance(input, jumpPressed, level, team, deltaSeconds):
  1. AdvanceTickState()          — cooldowns, weapon state, afterburn
  2. PrepareMovement()           — horizontal speed from input, check grounded
  3. TryJumpIfPossible()         — consume Up edge-trigger
  4. CompleteMovement()           — gravity, MoveWithCollisions, step-up/down, ground snap
```

Key movement details:
- **Jumping** requires `Up` to be newly pressed (edge-triggered: `input.Up && !previousInput.Up`)
- **Drop-through platforms** require holding `Down`
- **Air jumps** are class-dependent (Scout gets extra, most get 0)
- **Horizontal speed** converges to a class-specific max via `RunPower` and friction
- **Collision** is resolved via sub-pixel stepping in [MoveContact](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Entities/Players/Movement/PlayerEntity.Movement.Contact.cs#L7-L58) — the player tries to move in 1-unit steps with 8 levels of sub-pixel precision

### 4. Level Geometry

Levels are loaded from GameMaker room XML files or custom PNG maps via [SimpleLevelFactory](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Level/Layout/SimpleLevelFactory.cs). A [SimpleLevel](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Level/Layout/SimpleLevel.cs) contains:

| Component | Type | Description |
|---|---|---|
| **Solids** | `IReadOnlyList<LevelSolid>` | Axis-aligned rectangles (x, y, width, height) — the collision mesh |
| **RoomObjects** | `IReadOnlyList<RoomObjectMarker>` | Typed rects: team gates, player walls, dropdown platforms, move boxes, healing cabinets, spawn rooms, kill/frag boxes, control points |
| **RedSpawns / BlueSpawns** | `IReadOnlyList<SpawnPoint>` | (X, Y) spawn positions |
| **IntelBases** | `IReadOnlyList<IntelBaseMarker>` | Team intel home locations |
| **Bounds** | `WorldBounds` | Total playable width × height |

Collision queries use a [SpatialSolidIndex](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Level/Layout/SimpleLevel.cs#L240-L356) — a spatial hash grid with 128-unit cells. `IntersectsSolid(left, top, right, bottom)` and `FindBlockingSolidTop(...)` are the primary collision primitives.

Player occupancy is checked via `CanOccupy(level, team, x, y)` which tests against solids, team gates, and player walls.

### 5. Character Classes

10 classes defined in [PlayerClass](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Gameplay/PlayerClass.cs): Scout, Engineer, Pyro, Soldier, Demoman, Heavy, Sniper, Medic, Spy, Quote.

Each has distinct [CharacterClassDefinition](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Gameplay/CharacterClassDefinition.cs) parameters: Width, Height, CollisionLeft/Top/Right/Bottom, RunPower, JumpStrength, MaxRunSpeed, GroundAcceleration, Gravity, JumpSpeed, MaxAirJumps, MaxHealth.

### 6. Game Modes

7 modes in [GameModeKind](file:///c:/Users/level/Desktop/OpenGarrison%20Active/OpenGarrison-Fork/Core/Gameplay/GameModeKind.cs): CaptureTheFlag, Arena, ControlPoint, Generator, KingOfTheHill, DoubleKingOfTheHill, TeamDeathmatch.

Each mode has objective markers already embedded in the level data (intel bases, control points, generators, arena points). The simulation reads these directly — no special assets needed.

### 7. Network Player Slots

Up to 20 playable network player slots (bytes 1–20). Bot players occupy `_additionalNetworkPlayersBySlot[slot]` slots and have their inputs set via `_additionalNetworkPlayerInputs[slot]`. The existing server pipeline in `ServerBotManager` demonstrates that the hook point is: **set the input dictionary entry for a bot slot before the tick advances**.

---

## Proposed Bot System Architecture

### Design Philosophy

> The bot is not a special entity. It is an input generator. It reads the world state and writes a `PlayerInputSnapshot`. The simulation does the rest.

This means:
- **No special movement APIs.** The bot presses Left/Right/Up/Down and lets the physics engine move it.
- **No teleportation.** If the bot gets stuck, it recovers by pressing different keys — exactly like a human would.
- **No new entity types.** Bots use the same `PlayerEntity` via the same network player slot system.

---

### Module Structure

```
Core/BotBrain/                         ← New directory, inside Core project
├── BotBrainController.cs              ← Per-bot tick driver: reads world → writes input
├── Navigation/
│   ├── NavGraph.cs                    ← Waypoint graph built from level geometry at map load
│   ├── NavGraphBuilder.cs             ← Builds NavGraph from SimpleLevel (solids + room objects)
│   └── NavPath.cs                     ← A* path result with waypoint sequence
├── Steering/
│   ├── SteeringMachine.cs             ← State machine: Grounded → Airborne → Falling → Recovery
│   ├── GroundedSteering.cs            ← Walk toward next waypoint, detect edges/walls, trigger jump
│   ├── AirborneSteering.cs            ← Air control toward landing target, air-jump decisions
│   └── RecoverySteering.cs            ← Post-knockback re-orientation, path re-acquisition
├── Targeting/
│   ├── TargetSelector.cs              ← Pick nearest visible enemy, or objective target
│   └── AimResolver.cs                 ← Compute AimWorldX/Y for target, with optional imprecision
├── Objectives/
│   └── ObjectiveEvaluator.cs          ← Read game mode + state → pick macro goal (attack/defend/capture)
└── BotInputSynthesizer.cs             ← Final assembly: steering + targeting → PlayerInputSnapshot
```

> [!NOTE]
> **Zero new per-map assets.** The NavGraph is built entirely from the `SimpleLevel` data that already exists: `Solids`, `RoomObjects` (dropdown platforms, player walls, move boxes), `RedSpawns`, `BlueSpawns`, `IntelBases`, and control point markers. No navigation meshes, no waypoint files, no per-class instrumentation.

---

### Component Details

#### 1. NavGraph — Waypoint Navigation from Level Geometry

The NavGraph is the core data structure that enables traversal. It is built **once per map load** from the existing `SimpleLevel` data.

**How it's built (NavGraphBuilder):**

```
For each solid in Level.Solids:
    Place waypoints at the top-left and top-right corners (offset inward by player half-width)
    These are "ledge" waypoints — walkable surface endpoints

For each DropdownPlatform in Level.RoomObjects:
    Place waypoints at the left and right edges of the platform surface

For spawn points, intel bases, control points, healing cabinets:
    Place waypoints at their positions (these become objective/interest waypoints)

Connect waypoints:
    Two waypoints are connected by a "walk" edge if:
        - They are on the same solid surface (horizontal walk)
        - A simulated ground-level sweep from one to the other doesn't hit a gap wider than step-down tolerance
    
    Two waypoints are connected by a "jump" edge if:
        - The horizontal + vertical distance is reachable by the SLOWEST class's jump arc
        - A coarse ballistic trace (using LegacyMovementModel physics constants) confirms clearance
    
    Two waypoints are connected by a "fall" edge if:
        - One is directly above the other with no blocking solids in between
        - The fall won't land in a kill box
    
    Two waypoints are connected by a "dropdown" edge if:
        - One is on a dropdown platform and the other is reachable by falling through it
```

**Why this works for all classes:**
- Jump edges are computed for the worst-case class (heaviest, lowest jump). If Heavy can make the jump, everyone can.
- The player collision bounds used for occupancy checks are the class definition's bounds. At pathfinding time, we use a conservative (largest) collision hull for edge validation. At runtime steering, the actual class's physics handle the rest since we're just pressing Left/Right/Up.

**Cost:** Building this graph is a one-time O(S² + S·R) operation where S = number of solids and R = number of room objects. For typical maps (100–500 solids), this takes < 10ms.

#### 2. SteeringMachine — Input Synthesis from Path

The steering machine is the heart of the traversal system. It takes the bot's current path (sequence of waypoints) and its current `PlayerEntity` state, and outputs the Left/Right/Up/Down booleans.

**States:**

| State | Entry Condition | Behavior | Exit |
|---|---|---|---|
| **Grounded** | `player.IsGrounded` | Walk toward next waypoint. If waypoint is above and at a wall: press Up. If waypoint is across a gap: approach edge then press Up. If waypoint is below on a dropdown platform: press Down. | Leaves ground → Airborne |
| **Airborne** | `!player.IsGrounded && VerticalSpeed < 0` | Air-control toward landing target (press Left/Right to steer). If class has air jumps and target is higher: consider air jump. | Lands → Grounded; Speed > 0 → Falling |
| **Falling** | `!player.IsGrounded && VerticalSpeed > 0` | Air-control toward landing target. Prepare for ground contact. | Lands → Grounded |
| **Recovery** | Knockback detected (`MovementState != None`) | Re-evaluate path from current position. Air-control toward nearest safe ground. Don't fight the knockback — work with it. | `MovementState == None && IsGrounded` → Grounded |

**Anti-oscillation:**
- Each steering state tracks a `commitDirection` and `commitTicks` counter. Once the bot commits to moving in a direction, it holds for a minimum of 3 ticks before reconsidering.
- If the bot's X position hasn't changed by more than 2 units over 15 ticks while trying to move, it enters a "stuck" sub-state that tries: (1) jump, (2) reverse direction, (3) re-path.

**Edge detection (cliff awareness):**
- Before walking off a ledge, the bot probes `CanOccupy(level, team, nextX, nextY + stepDownMax)`. If the ground drops away further than the step-down tolerance (12 units) and the next waypoint isn't below, the bot stops and jumps instead.
- This uses the same `CanOccupy` probe the engine already uses — no new collision queries.

#### 3. AimResolver — Weapon Aiming

Simple per-tick computation:
- If engaging an enemy: `AimWorldX = target.X`, `AimWorldY = target.Y - (target.Height * 0.3)` (aim center-mass, slightly high)
- If traversing: `AimWorldX = nextWaypoint.X`, `AimWorldY = nextWaypoint.Y` (aim in movement direction for natural facing)
- Optional: add small random offset (±3 units) to prevent robotic snap-aiming

#### 4. ObjectiveEvaluator — Macro Decision Making

Reads the current `MatchRules.Mode` and game state to decide the bot's macro goal:

| Mode | Goal Logic |
|---|---|
| **CaptureTheFlag** | If enemy intel is at base → go grab it. If carrying intel → go to own base. If own intel is carried → chase carrier. |
| **Arena** | Move toward arena control point. Fight enemies encountered. |
| **ControlPoint / KotH** | Move toward uncaptured or contested control point. |
| **Generator** | Move toward generator. |
| **TeamDeathmatch** | Seek nearest enemy. |

The evaluator outputs a **goal position** (world X, Y) which the NavGraph A* pathfinder routes to.

#### 5. BotBrainController — Per-Bot Tick Driver

```csharp
public class BotBrainController
{
    public PlayerInputSnapshot Think(
        PlayerEntity self,
        SimulationWorld world,
        PlayerTeam team,
        PlayerInputSnapshot previousInput)
    {
        // 1. Evaluate objective → get goal position
        // 2. Find/update path via NavGraph A*
        // 3. Run steering machine → get Left/Right/Up/Down
        // 4. Run target selector → get nearest enemy
        // 5. Run aim resolver → get AimWorldX/Y
        // 6. Decide FirePrimary based on target + weapon state
        // 7. Assemble and return PlayerInputSnapshot
    }
}
```

This is called **once per tick** for each bot slot, immediately before the simulation reads the input.

---

### Integration Point

The bot system integrates at exactly one point: **where `_additionalNetworkPlayerInputs[slot]` is written**.

```csharp
// In the server tick loop, before AdvanceOneTick():
foreach (var botSlot in activeBotSlots)
{
    var player = GetNetworkPlayer(botSlot);
    var team = GetNetworkPlayerTeam(botSlot);
    var previousInput = GetPreviousNetworkInput(botSlot);
    var input = botBrains[botSlot].Think(player, world, team, previousInput);
    SetNetworkPlayerInput(botSlot, input);
}
```

This is the same pattern the server already uses for human network players — we're just providing the input from a brain instead of from a network packet.

---

### Asset Requirements

| Asset | Count | Notes |
|---|---|---|
| Per-map nav data | **0** | Built automatically from existing SimpleLevel geometry |
| Per-class config | **0** | All class differences are already in CharacterClassDefinition |
| Per-team config | **0** | Team is just a slot assignment |
| New entity types | **0** | Bots use PlayerEntity via network slots |

> [!TIP]
> **Total new assets per map: 0.** The NavGraph is computed at map load from data that already exists.

---

## Open Questions

> [!IMPORTANT]
> **Where should the bot tick hook live?** The cleanest option is to have the bot brain controller called from the server's tick loop (in `GameServer.cs` or equivalent), right before `world.AdvanceOneTick()`. This is where human player inputs are already committed. Is there a preferred integration point, or should I place it wherever makes the most sense architecturally?

> [!IMPORTANT]
> **Should bots participate in all 7 game modes from the start, or should I prioritize a subset?** The objective evaluator can handle all modes, but combat and traversal are the hard problems. I'd recommend getting traversal + basic combat working on CaptureTheFlag and TeamDeathmatch first, then extending.

> [!IMPORTANT]
> **How many bots need to run simultaneously?** The nav graph A* and steering machine are lightweight (< 0.1ms per bot per tick), but knowing the target count helps me optimize appropriately. The engine supports up to 20 network player slots total.

## Verification Plan

### Automated Tests
- Unit test NavGraphBuilder against known level geometries (the fallback prototype level, stock maps)
- Unit test steering machine state transitions with mock PlayerEntity positions
- Integration test: run 100 ticks with a bot on the prototype level, verify it doesn't get stuck (X position changes over time), doesn't die from kill boxes, and arrives at a target waypoint

### Manual Verification
- Run the server with bots, observe traversal on multiple maps
- Verify bots can navigate between spawn points without oscillation
- Verify bots recover from rocket knockback without getting stuck
- Verify bots can traverse dropdown platforms and step-up geometry
