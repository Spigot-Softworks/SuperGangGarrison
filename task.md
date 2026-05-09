# Bot System Implementation Tasks

## Phase 1: Navigation
- [ ] `NavGraph.cs` — Waypoint graph data structure (nodes, edges, A* pathfinding)
- [ ] `NavGraphBuilder.cs` — Build NavGraph from SimpleLevel geometry
- [ ] `NavPath.cs` — Path result type with waypoint sequence

## Phase 2: Steering
- [ ] `SteeringMachine.cs` — State machine driver (Grounded/Airborne/Falling/Recovery)
- [ ] `GroundedSteering.cs` — Walk toward waypoint, edge detection, jump triggers
- [ ] `AirborneSteering.cs` — Air control, air-jump decisions
- [ ] `RecoverySteering.cs` — Post-knockback recovery

## Phase 3: Targeting & Combat
- [ ] `TargetSelector.cs` — Pick nearest visible enemy
- [ ] `AimResolver.cs` — Compute AimWorldX/Y

## Phase 4: Objectives
- [ ] `ObjectiveEvaluator.cs` — Game mode awareness, macro goal selection

## Phase 5: Assembly & Integration
- [ ] `BotInputSynthesizer.cs` — Combine steering + targeting → PlayerInputSnapshot
- [ ] `BotBrainController.cs` — Per-bot tick driver
- [ ] Server integration — Hook into tick loop

## Phase 6: Verification
- [ ] Build passes
- [ ] Manual traversal review on prototype level
