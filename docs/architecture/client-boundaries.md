# Client Boundaries

These are target ownership rules for the client architecture.

## Game1 lifecycle

- **Rule:** `Game1` keeps only MonoGame lifecycle responsibilities: `Initialize`, `LoadContent`, `Update`, and `Draw` dispatch.
- **Rationale:** Keeping lifecycle dispatch thin prevents the framework entry point from becoming a feature-state owner.
- **Violation:** Storing menu selection, match state, or gameplay state in a `Game1` partial.

- **Rule:** `Game1` partials contain no feature state, no menu state, and no direct world access.
- **Rationale:** Feature ownership belongs to controllers and services that can be tested independently of MonoGame.
- **Violation:** A `Game1` partial reading `SimulationWorld.Entities` to decide which menu or HUD to show.

## Controller context

- **Rule:** Controllers receive a `ClientContext` containing the world, network, assets, and settings they need instead of receiving `Game1`.
- **Rationale:** Explicit context dependencies make controller boundaries visible and avoid coupling them to the application shell.
- **Violation:** Constructing a controller with `Game1 game` so it can reach `Content`, `GraphicsDevice`, or private client fields.

## Rendering input

- **Rule:** Rendering consumes a `RenderSnapshot` DTO and never consumes `SimulationWorld` directly.
- **Rationale:** A snapshot separates presentation timing and data shape from mutable simulation state.
- **Violation:** A renderer querying live entities or combat state from `SimulationWorld` during `Draw`.
