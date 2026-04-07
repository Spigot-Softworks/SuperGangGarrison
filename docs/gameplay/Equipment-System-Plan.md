# Equipment System Plan

## Purpose

This document defines the intended OpenGarrison equipment system for combat items, cosmetics, ownership, and selection.

The goal is a professional multiplayer model:

- one authoritative server decides what each player owns and has equipped
- clients render replicated state and request changes, but do not decide validity
- combat equipment and cosmetic equipment follow the same trust model, but remain separate systems

This plan is designed to align with the gameplay pack and loadout work already in progress.

## Design Goals

- Keep gameplay authority on the server at all times.
- Keep combat logic data-driven where practical and code-driven where necessary.
- Separate combat equipment from cosmetic equipment so hats and skins do not distort gameplay rules.
- Support stock content and modded content through the same registry and validation path.
- Make ownership, equip state, and presentation explicit and inspectable.
- Avoid legacy plugin patterns that mix networking, rendering, account logic, and gameplay mutation in one runtime blob.

## Non-Goals

- Do not implement a remote account service as part of the first equipment phases.
- Do not let clients authoritatively grant themselves items.
- Do not merge hats, weapons, overlays, and gameplay abilities into one generic untyped item bag.
- Do not adopt old GG2 itemserver transport or packed array formats.

## Terms

- `Ownership`
  - Whether the server recognizes a player as having access to an item.
- `Equipped`
  - The current server-approved selection for a valid equipment slot.
- `Combat Equipment`
  - Equipment that changes gameplay behavior or replicated combat state.
- `Cosmetic Equipment`
  - Equipment that only changes presentation.
- `Gameplay Pack`
  - A validated content pack that defines items, classes, loadouts, and presentation assets.

## Core Architecture

### Server Authority

The server must own:

- item ownership state
- equipped combat loadouts
- equipped cosmetic selections
- class and slot compatibility validation
- replication of equipment state to other clients
- persistence integration if a future account backend is added

The client may:

- query available equipment and ownership state
- request an equipment change
- present equipped and locked states in UI
- render replicated results

The client may not:

- grant ownership
- equip incompatible or unowned items locally as truth
- alter combat-impacting item state without server approval

### Two Equipment Lanes

OpenGarrison should formally split equipment into two lanes.

#### Combat Equipment

Applies to:

- class loadouts
- primary weapons
- secondary items
- utility/action items
- future alternate primaries, subclass items, and mode-specific equipment

This lane affects:

- simulation
- damage
- movement interactions
- meters
- abilities
- HUD and in-world combat presentation

#### Cosmetic Equipment

Applies to:

- hats
- body overlays
- projectile skins
- death sprites
- taunt overlays
- future nameplates, trails, announcer packs, and similar presentation-only equipment

This lane affects:

- rendering only
- local and remote presentation

This lane must not alter:

- hitboxes
- timings
- damage
- movement
- gameplay authority

## Why Separate Lanes

The old GG2 itemserver plugin at `C:\Users\level\Downloads\itemserver@166f810446041dbc6bdffbf0657de6a2` is useful as a reminder that hats, projectile skins, and loadout replication are real product needs, but it also shows the risk of merging account flow, asset download, inventory, networking, and rendering into one monolithic plugin.

The right OpenGarrison answer is:

- shared server-authoritative ownership principles
- separate typed schemas
- separate equip validation rules
- shared replication and UI patterns where helpful

## Data Model

### Ownership Record

Each player should eventually have server-owned equipment ownership records with at least:

- `playerId`
- `itemId`
- `equipmentLane`
- `ownershipState`
  - default granted
  - granted
  - revoked
  - unavailable
- `grantSource`
  - stock default
  - server admin
  - plugin
  - progression
  - entitlement
- `grantKey`
  - optional stable external or internal identifier
- `isPersistent`
- `lastModifiedUtc`

The current gameplay ownership baseline is enough to start combat loadouts, but not yet enough for a full long-term inventory service. This document defines the target direction.

### Equipped State

Combat equipment state should be represented separately from ownership.

Suggested combat equipped state:

- `classId`
- `selectedLoadoutId`
- `selectedPrimaryOverrideId`
- `selectedSecondaryOverrideId`
- `selectedUtilityOverrideId`
- `selectedModeSpecificOverrideIds`

Suggested cosmetic equipped state:

- `equippedHatId`
- `equippedBodyOverlayId`
- `equippedProjectileSkinId`
- `equippedDeathSpriteId`
- `equippedTauntOverlayId`

### Catalog Definitions

Combat items should continue living in gameplay packs.

Cosmetics should eventually have a typed cosmetic pack schema with:

- `id`
- `displayName`
- `slot`
- `presentation assets`
- `supportedClasses`
- `supportedTeams`
- `render layer rules`
- `special presentation flags`

Cosmetic content should not be forced into the combat gameplay item schema.

## Validation Rules

### Ownership Validation

Before equip:

- item must exist
- item must belong to the correct lane
- player must own it, or it must be default-granted

### Combat Validation

Before equip:

- item must be valid for the player class
- item must be valid for the target slot
- item must be valid for the selected game mode if mode-restricted
- item must not violate any mutually exclusive rules
- if item uses a custom behavior family, that behavior must be registered and available

### Cosmetic Validation

Before equip:

- cosmetic must be valid for the target cosmetic slot
- class restrictions must pass
- team restrictions must pass
- render dependencies must exist locally in packaged content or approved cache

## Replication Model

### Combat Replication

Combat replication must be explicit and deterministic.

Clients need enough replicated state to render:

- active loadout id
- currently equipped items by slot
- current combat presentation assets
- meter-affecting or mode-affecting item state where applicable

This should remain tightly controlled because combat replication is simulation-adjacent.

### Cosmetic Replication

Cosmetic replication should be lighter weight.

Clients need enough replicated state to render:

- equipped cosmetic ids per visible player
- any resolved render variants needed for class/team state

The server should replicate selected cosmetic ids, not raw downloaded asset blobs.

## Client UX Contract

The client needs a clean authoritative contract for equipment UI.

### Required Read Surfaces

For the local player, the client should be able to query:

- available classes
- available loadouts for class
- owned combat items
- owned cosmetic items
- locked combat items with visible reasons
- locked cosmetic items with visible reasons
- current equipped combat state
- current equipped cosmetic state

### Required Write Surfaces

The client should be able to request:

- change selected loadout
- change selected combat item override
- clear a combat override
- equip a cosmetic
- clear a cosmetic slot

Each request should return one of:

- accepted
- rejected invalid slot
- rejected not owned
- rejected incompatible with class
- rejected incompatible with current mode
- rejected item unavailable

### UI Principles

- Show stock/default equipment clearly.
- Show owned and equipped states separately.
- Show locked items without pretending they are selectable.
- Never let the client appear to equip something that the server has not approved.
- Prefer clear slot-driven layouts over one giant undifferentiated inventory screen.

## Persistence Strategy

### Phase 1

No external persistence is required.

Ownership may be:

- stock defaults
- server-granted debug/admin items
- plugin-granted session items

This is enough to validate the architecture.

### Phase 2

Introduce optional persistence behind a dedicated service boundary.

That boundary should own:

- account identity mapping
- durable owned item records
- durable equipped selections
- entitlement reconciliation

The gameplay server should consume persistence, not embed web account logic directly into gameplay plugins.

## Asset Strategy

### Packaged First

For gameplay-critical content:

- use packaged assets staged with the game
- resolve assets from authoritative pack declarations

### Cached Optional

For future cosmetic ecosystems:

- client-side cached downloads may be acceptable
- only for presentation-only content
- only behind validation, versioning, and cache rules

Do not make combat item functionality depend on ad hoc remote downloads.

## Recommended Phases

### Phase A: Combat Vertical Slice

Ship a minimal authoritative combat equipment loop first.

Scope:

- local player can inspect available loadouts
- local player can inspect owned combat items
- local player can request loadout selection
- server validates and replicates equipped combat state
- client UI supports one working slice
  - Soldier stock
  - Soldier Direct Hit

Success criteria:

- Direct Hit can be selected in-client through real UI
- server rejects invalid or unowned selections
- remote clients see the correct equipped presentation

### Phase B: General Combat Equipment UI

Broaden the combat slice to all supported classes and override slots.

Scope:

- per-class loadout UI
- owned vs locked display
- secondary and utility selection
- mode-aware item availability

### Phase C: Cosmetic Lane Foundation

Add a separate cosmetic registry and equip model.

Scope:

- cosmetic schema
- cosmetic ownership records
- cosmetic equip requests
- replicated cosmetic selections
- simple hat slot proof of concept

### Phase D: Persistence Boundary

Add durable ownership and equipment persistence without contaminating simulation code.

Scope:

- account mapping
- ownership repository
- equipped-state repository
- reconciliation on player join

## Immediate Next Step

The next implementation step should be `Phase A: Combat Vertical Slice`.

Concretely:

1. Add client-readable local-player equipment catalog and equipped-state surfaces.
2. Add request/response flow for authoritative loadout selection.
3. Build a minimal in-client loadout picker for Soldier.
4. Validate the full loop using `soldier.stock` and `soldier.direct-hit`.

This is the correct next move because it proves the server-authoritative equipment model before we spend time on a broader inventory UI or cosmetic system.

## Summary

OpenGarrison should become a server-authoritative equipment game, not a client-side plugin patchwork.

The right structure is:

- one authoritative server ownership model
- one authoritative equip-validation path
- separate combat and cosmetic lanes
- packaged, typed content for gameplay
- optional persistence behind a service boundary
- client UI built on explicit replicated state and server-approved requests

That gives us a clean path from Direct Hit loadouts today to hats, projectile skins, and a real multiplayer ownership system later without rewriting the foundation.
