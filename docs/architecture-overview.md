# Architecture Overview

## Solution Shape

The solution is organized around a shared deterministic gameplay simulation.

- `Core/` contains the simulation, entities, gameplay definitions, map import, and asset metadata.
- `Client/` hosts MonoGame presentation, menu flow, input, prediction, hosting UI, and client plugins.
- `Server/` hosts dedicated-server networking, sessions, snapshots, admin commands, plugin hosting, and map rotation.
- `Protocol/` defines the binary wire format and message contracts shared by client and server.
- `BotAI/` and `BotAI.Tools/` handle runtime bot behavior and offline navigation asset generation.

## Runtime Composition

### Client

The client enters through `Game1` and uses partial classes to organize major areas such as:

- core game loop
- menus and startup flow
- gameplay update and rendering
- HUD and overlays
- multiplayer and lobby browser
- hosting and server launcher UI
- developer tools
- client plugin integration

`Game1` reuses the shared `SimulationWorld` for offline play and local presentation logic.

### Server

The dedicated server builds a `SimulationWorld`, wraps it in a fixed-step simulator, and composes:

- UDP transport
- session management
- connection/password rate limiting
- snapshot broadcasting
- lobby registration
- auto-balance
- map rotation
- event logging
- server plugins

This keeps the authoritative gameplay rules in `Core` while letting the server own transport and administration.

## Simulation Model

`SimulationWorld` is the central gameplay authority for:

- players and entities
- combat and projectiles
- match rules and match state
- map changes
- control points, KOTH, generators, and CTF objectives
- transient sound, visual, damage, and healing events
- network-player state and snapshot application

The simulation advances per tick, then mode-specific objective logic runs on top of the common player/combat/runtime systems.

## Content and Maps

The current codebase preserves a GameMaker-oriented content pipeline:

- asset manifests are imported from the packaged content tree
- stock maps and custom maps are imported into shared level structures
- packaged content is copied into release outputs by the existing scripts

This area is operationally important and should be changed carefully.

## Extensibility

The repo currently has two extension seams:

- server and client plugin abstractions in `Plugins/`
- gameplay modding abstractions in `GameplayModding.Abstractions/`


