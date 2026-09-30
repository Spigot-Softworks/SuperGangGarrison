# Simulation Boundaries

These are target ownership rules for the simulation architecture.

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
