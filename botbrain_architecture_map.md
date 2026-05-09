## 1. EXECUTION MODEL (CRITICAL)
The BotBrain system runs synchronously every server tick per bot via `BotBrainController.Think()`. It converts world state into a simulated `PlayerInputSnapshot` that the game engine consumes as if it were a human player's input. 

**Main Loop Order of Operations:**
1. **Level Change Check:** If `world.Level` changes, the `NavGraph` is reloaded, and all pathing/steering state is wiped.
2. **Blocked Edge Decay:** A TTL map of failed `NavEdgeBlock` is decremented. Expired blocks are removed.
3. **Target Selection (`TargetSelector`):** Finds the nearest valid, visible enemy within 600 units every tick.
4. **Objective Evaluation (`ObjectiveEvaluator`):** Throttled to run every 60 ticks (2 seconds) OR instantly if a valid combat target is present. Determines a macro `(X, Y)` goal coordinate based on the game mode (e.g., intel base, control point, or map center).
5. **Pathfinding (`UpdatePath`):** Throttled to run every 30 ticks (1 second), OR instantly if the path is complete, `null`, or if the steering machine requested a repath. Uses A* to find a route from the closest node to the goal, actively avoiding blocked edges.
6. **Steering (`SteeringMachine`):** Runs every tick. Evaluates the current physical state against the active `NavEdge` from the path. Tracks stuck states. Outputs normalized `MoveDirection`, `Jump`, and `DropDown` intents. Can flag a `RequestRepath` if an edge times out or fails.
7. **Aim Resolution (`AimResolver`):** Computes a world `(X, Y)` coordinate to point the crosshair.
8. **Input Synthesis (`BotInputSynthesizer`):** Maps the steering intents and combat rules to the final boolean `PlayerInputSnapshot` array (Left, Right, Up, Down, FirePrimary).

**State Persistence:**
- Carries over `NavGraph`, `_currentPath`, `_objectiveReevalCooldown`, `_repathCooldownTicks`, `_blockedEdges` TTL map.
- The `SteeringMachine` is highly stateful, tracking stuck ticks, escape phases, edge execution phases, and commit ticks across frames.

---

## 2. CORE DATA STRUCTURES

**`NavGraph` & `NavPath`**
- **Role:** Spatial representation of the level. A `NavPath` is a sequence of node indices.
- **Updates:** Rebuilt on level change. `NavPath` is regenerated every 30 ticks or on repath requests.
- **Invariants:** Nodes must exist in the graph. `CurrentNode` must be updated as the bot physically reaches waypoints.

**`NavEdge`**
- **Fields:** `Kind` (Walk, Jump, Fall, Dropdown), `Completion` window (X/Y bounds), `LaunchRecipe` (required start speed/position/tick for jumps).
- **Role:** Defines the physical requirements to move between two nodes.
- **Invariants:** Completion windows must be physically reachable.

**`SteeringOutput`**
- **Fields:** `MoveDirection` (-1 to 1), `Jump`, `DropDown`, `RequestRepath`, `FailedEdge`.
- **Role:** The intermediary artifact between physical path following and final binary input generation.
- **Updates:** Recreated every tick by `SteeringMachine`. 

**`PlayerInputSnapshot`**
- **Role:** The final, network-ready input struct injected into the simulation.
- **Updates:** Synthesized at the end of every tick. Contains raw booleans (`Left`, `Right`, `FirePrimary`) and `AimWorldX`/`AimWorldY`.

---

## 3. MODULE / FILE RESPONSIBILITY MAP

- **`BotBrainController.cs`**
  - *Purpose:* The main driver and orchestrator.
  - *Inputs:* `PlayerEntity`, `SimulationWorld`, `PlayerTeam`.
  - *Outputs:* `PlayerInputSnapshot`.
  - *Dependencies:* All other modules below. Owns the throttling logic for pathing.

- **`ObjectiveEvaluator.cs`**
  - *Purpose:* Determines the macro-level world coordinate the bot wants to reach.
  - *Inputs:* Game Mode, Team, Intel State.
  - *Outputs:* Target `(X, Y)` position.
  - *Dependencies:* Map objects (Control Points, Generators, Intel Bases).

- **`TargetSelector.cs`**
  - *Purpose:* Identifies the best immediate combat threat.
  - *Inputs:* Network player slots, self state.
  - *Outputs:* Nearest valid `PlayerEntity` target.

- **`SteeringMachine.cs`**
  - *Purpose:* Converts abstract path edges into physical movement intents. Handles unstuck logic.
  - *Inputs:* Physical state, current `NavEdge`.
  - *Outputs:* `SteeringOutput`.

- **`AimResolver.cs`**
  - *Purpose:* Calculates where the bot should point its weapon.
  - *Inputs:* Combat target, `SteeringOutput`, current `NavPath`.
  - *Outputs:* `AimX`, `AimY`.

- **`BotInputSynthesizer.cs`**
  - *Purpose:* Final translation layer combining steering intent and combat firing logic into game inputs.
  - *Inputs:* `SteeringOutput`, `AimX/Y`, combat target distance.
  - *Outputs:* `PlayerInputSnapshot`.

---

## 4. CONTROL FLOW / DECISION LOGIC

**Steering Finite State Machine (`SteeringMachine.cs`)**
- **Phase 1: State Resolution.** The bot is classified as `Grounded`, `Airborne`, `Falling`, or `Recovery` (if in a locked animation).
- **Phase 2: Stuck Detection.** If the bot moves < 2 units over 15 ticks, `_stuckEscapePhase` escalates (1->3). 
  - *Phase 1:* Jump.
  - *Phase 2:* Reverse direction + Jump.
  - *Phase 3:* Reverse + Jump + Request Repath.
- **Phase 3: Edge Execution.** Applies logic based on `NavEdgeKind`:
  - *Walk:* Move toward node. Jump if a wall/cliff is detected.
  - *Jump:* Wait for `JumpTriggerTick`. Check `LaunchRecipe` (must have specific horizontal speed and X/Y window). Execute jump when conditions are met.
  - *Fall/Dropdown:* Move over the edge, suppress jumping.
- **Priority Rules:** Stuck escape logic OVERRIDES standard path steering. Combat firing OVERRIDES passive traversing, but only if weapons are off cooldown and safe to fire.

---

## 5. CROSS-MODULE INTERACTIONS

- **Targeting influences Pathing:** If a combat target is acquired, the `ObjectiveEvaluator` cooldown is bypassed. However, in many modes (like TDM), `ObjectiveEvaluator` just returns the center of the map, meaning target acquisition currently just forces the bot to re-evaluate pathing toward the map center faster.
- **Steering influences Pathing (Blacklisting):** If `SteeringMachine` fails an edge (e.g., missed a jump completion window), it sends a `RequestRepath` with `FailedEdge` data. `BotBrainController` caches this failed edge in `_blockedEdges` for 900 ticks (30 seconds) and forces `UpdatePath` to route around it.
- **Steering influences Aim:** If traversing, `AimResolver` points the crosshair ahead of the bot based on the `SteeringMachine`'s `MoveDirection`.

---

## 6. HOTSPOTS (HIGH-RISK AREAS)

1. **Edge Launch Verification (`SteeringMachine.cs`)**
   - *Risk:* `NavEdgeLaunchRecipe` requires highly specific physics state (speed, X/Y bounds) to trigger a jump. If the game physics change slightly, bots will wait forever at a ledge for a speed/position that never occurs, eventually timing out.
   - *Assumptions:* Assumes map generation outputs perfectly accurate physics trajectories.
2. **Stuck Detection Overlap**
   - *Risk:* A bot might get stuck because it's waiting for a jump `LaunchRecipe` to become valid. The Stuck Detection will see the bot isn't moving and force a random jump, which aborts the edge and causes an edge failure, leading to edge blacklisting.
3. **Explosive Weapon Range (`BotInputSynthesizer.cs`)**
   - *Risk:* Soldiers/Demomen will not fire if the target is < 60 units away. If an enemy facehugs an explosive bot, the bot will simply stop firing completely.

---

## 7. EDGE CASES & FAILURE MODES

- **Objective Congregation:** In Game Modes without clear objectives (TDM), `EvaluateGoal` defaults to `(MapWidth * 0.5f, MapHeight * 0.5f)`. All bots will relentlessly swarm the exact mathematical center of the level unless blocked.
- **Repath Thrashing:** If the goal node is constantly shifting (e.g., following a moving intel carrier), the path is wiped and recomputed every 30 ticks. If pathing takes too long, bots will stutter every second.
- **Certified Runups:** Heavy classes carrying intel have special "Certified Runup" rules to ensure they can clear jumps. If they bump a teammate during this runup, their horizontal speed drops, the recipe fails, and they fall into a pit.

---

## 8. ASSUMPTIONS & UNCERTAINTIES

- **LIKELY:** The `BotBrain` is designed to be completely decoupled from the game's physics step. It only knows about the world via snapshots and only affects the world via injected controller inputs.
- **LIKELY:** The `NavGraph` generation tool (external to this code) simulates physics drops/jumps to bake the `LaunchRecipe` parameters. 
- **UNCERTAIN:** It is unclear if `BotBrainController` is instantiated per-bot or pooled. The code implies one instance per bot (`The per-bot tick driver`), but does not show the lifecycle management.
- **UNCERTAIN:** If `TargetSelector` finds an enemy, `ObjectiveEvaluator` doesn't actually seem to navigate *to* the enemy in TDM. It just resets the pathing timer. Bots rely entirely on randomly stumbling into range to engage.

---

## 9. MINIMAL MENTAL MODEL

To understand BotBrain quickly:
It is an **input-synthesizer loop**. Every tick, it looks at the map, runs A* to a macro objective, and looks at its immediate footing to decide if it should press `Left`, `Right`, or `Jump`. 
1. **Controller** sets the tempo and holds the A* path.
2. **Objective/Targeting** tells it where it wants to end up.
3. **SteeringMachine** handles the micro-physics of not falling in pits (state machine tracking runups and jumps).
4. **InputSynthesizer** converts it all into fake keyboard/mouse presses. 
If a physical jump fails, the edge is banned for 30 seconds and it recalculates a new route.
