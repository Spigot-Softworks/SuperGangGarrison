# Rare Item Ownership Plan

## Purpose

This document defines the intended ownership and trust model for OpenGarrison combat variants and future cosmetic rarity.

The target product model is:

- all standard gameplay weapons are available to all players by default
- special variants such as rare reskins, strange weapons, unusual cosmetics, or curated event rewards are server-authoritative and tightly controlled
- clients may request equipment changes, but clients never decide whether they own a rare item

This plan is meant to prevent the project from drifting into a state where rare items are easy to spoof or mint through client edits, careless plugins, or loosely defined server behavior.

## Product Goals

- Keep core gameplay fair by making standard combat equipment universally available.
- Preserve trust in rare items by making ownership server-authoritative and persistent.
- Avoid forcing every server to run a separate external itemserver just to support normal weapon selection.
- Create a clear distinction between `default gameplay access` and `rare entitlement ownership`.
- Ensure the same rules can later support both combat variants and cosmetic rarity.

## Non-Goals

- Do not make normal weapon ownership dependent on web logins or account services.
- Do not trust the client to report owned items.
- Do not let general-purpose gameplay plugins invent rare ownership rules ad hoc.
- Do not put backend account logic directly inside simulation code.
- Do not copy legacy GG2 itemserver architecture where gameplay, accounts, downloads, and rendering are mixed together.

## Current State

Today, OpenGarrison already has a usable first-stage authority model:

- item definitions can declare ownership metadata
- the server validates loadout and item selection
- the client cannot simply locally equip an unowned tracked item and have that become truth
- tracked ownership can exist in server memory for the current session

What we do **not** yet have is durable, strict rare-item ownership:

- tracked ownership is not yet persisted as a long-term inventory record
- grant and revoke policy is not yet isolated behind a dedicated entitlement boundary
- rare-item trust is not yet separated cleanly from ordinary gameplay pack logic

So the architecture is directionally correct, but not yet final-hardened for meaningful rarity.

## Core Principle

OpenGarrison should use a two-tier ownership model.

### Tier 1: Baseline Gameplay Access

For normal weapons and normal gameplay-meaningful loadouts:

- ownership is effectively universal
- items are marked `defaultGranted = true`
- no account or entitlement lookup is required
- equip validation still happens on the server, but ownership is trivial

This covers the majority of gameplay items:

- stock primaries
- normal alternate weapons intended for general public use
- mode-specific but non-rare gameplay unlocks if you want everyone to have them

### Tier 2: Rare Entitlement Ownership

For items that should carry rarity, provenance, or special status:

- ownership must be tracked
- ownership must be issued by a trusted server-side authority
- equip validation must require a durable entitlement record
- clients may display the item, but may never claim ownership on their own

This tier is appropriate for:

- rare combat reskins
- strange or stat-tracked variants
- unusual cosmetics
- event rewards
- donor/admin/curated collectible items

## Trust Boundary

The trust boundary should be explicit.

### Trusted

- authoritative game server
- future server-side inventory persistence store
- future entitlement service or signed inventory source
- trusted admin operations with audit visibility

### Untrusted

- client UI
- client save files
- client plugins
- local modded builds
- arbitrary gameplay plugins unless they are explicitly given rare-item issuance authority

The rule should be:

- the client can ask to equip item `X`
- only the server may answer whether player `P` owns item `X`

## Ownership Classes

Every item should fit one of these policy classes.

### `Universal`

Used for normal weapons and normal loadouts.

Characteristics:

- `defaultGranted = true`
- no persistence required
- available on every server unless the pack is disabled

Examples:

- stock rocket launcher
- Direct Hit if you intend it to be a normal selectable weapon
- other regular balance variants meant for broad access

### `Session Granted`

Used for temporary events, debug grants, or server-specific fun modes.

Characteristics:

- tracked by the server
- may be granted and revoked during a session
- may or may not persist
- not considered globally collectible

Examples:

- temporary event weapon
- admin-granted novelty test item
- match-earned item on a custom server

### `Persistent Rare`

Used for items where authenticity matters.

Characteristics:

- tracked ownership required
- durable persistence required
- grant source must be trusted
- equip allowed only if persistent ownership exists

Examples:

- strange variants
- unusual cosmetics
- founder items
- event collectibles

## Recommended Data Model

Rare ownership should not just be a boolean.

Suggested server-side record:

- `playerIdentity`
- `itemId`
- `ownershipClass`
- `grantSource`
- `grantKey`
- `grantedAtUtc`
- `revokedAtUtc`
- `isActive`
- `serverScope`
- `metadata`

### Notes

- `playerIdentity`
  - Should be an identity record, not a display name. Display names are not trustworthy.
- `grantSource`
  - Examples: `default`, `admin`, `event`, `migration`, `backend`
- `grantKey`
  - Stable entitlement or issuance key used to reconcile and audit grants
- `serverScope`
  - Allows per-server rarity if desired, while still supporting global rarity later
- `metadata`
  - Useful for future strange stats, unusual effect ids, provenance tags, or migration notes

## Identity Requirement

Persistent rare ownership depends on a real player identity.

Before durable rare inventories, OpenGarrison needs a stable identity model stronger than player name.

Minimum acceptable options:

- signed account id from a future backend
- platform identity if one ever exists
- server-issued identity token with secure persistence and anti-spoofing rules

Not acceptable as the primary durable key:

- display name
- IP address
- client-side config id alone

Without a trustworthy identity, rare ownership can only ever be server-local and semi-trusted.

## Entitlement Authority

Rare grants should flow through a dedicated authority boundary.

### Short-Term Recommended Model

Use a server-local ownership repository and a strict grant API.

The game server owns:

- entitlement lookup
- entitlement cache
- equip validation
- replication of owned/equipped rare items

The repository owns:

- persistence format
- reads on player join
- writes on grant/revoke
- audit trail

### Long-Term Recommended Model

If the project later needs stronger consistency across servers, add a dedicated entitlement service.

The game server should then:

- authenticate the player identity
- fetch owned rare entitlements from the service
- cache them for the session
- continue enforcing them locally during simulation

The simulation should never call web services directly in the hot path.

## Why Not a Separate Itemserver Right Now

A separate itemserver is not required for normal weapon access, and introducing one too early would likely slow the project down and increase operational complexity.

For your current product direction:

- universal weapons do not need external ownership service
- strict rarity only applies to a smaller item subset
- a server-local persistent inventory is enough for the first meaningful rare-item phase

That means the professional rollout is:

1. universal default ownership for normal weapons
2. tracked persistent ownership for rare variants
3. optional external entitlement service later if cross-server trust becomes necessary

## Validation Rules

### Normal Weapons

For universal gameplay weapons:

- item exists
- item is valid for class/slot/mode
- item is enabled on this server

Ownership does not block equip because ownership is universal by policy.

### Rare Variants

For persistent rare items:

- item exists
- item is marked as tracked ownership
- item is valid for class/slot/mode
- player identity is resolved
- active entitlement exists for that exact item id
- any rarity-specific constraints pass

If any of these fail, equip must be rejected server-side.

## Plugin Policy

This is important if you want rarity to stay trustworthy.

### Recommended Rule

General gameplay plugins should be allowed to:

- inspect ownership state
- react to owned/equipped items
- request standard equipment changes on behalf of gameplay logic

General gameplay plugins should **not** be allowed to:

- mint persistent rare items directly
- override rare ownership validation
- impersonate the entitlement authority

If plugins are ever allowed to grant rare items, that should be through a separate trusted capability with:

- explicit allow-listing
- audit logs
- narrow scope

Otherwise a single careless or malicious plugin becomes an item duping vector.

## Replication Rules

Clients do not need the full inventory database.

They need:

- whether a relevant item is owned
- whether it is equipped
- enough local state to render locked/owned/equipped UI

For remote players, clients usually only need:

- equipped item id
- cosmetic rarity presentation if visible

They do not need every owned rare item for every other player.

## Security Expectations

This model protects against:

- client UI edits
- client-side config tampering
- local spoofing of ownership state
- protocol-level attempts to equip unowned rare items

It does **not** fully protect against:

- running a malicious custom server
- admins intentionally granting themselves items
- compromised backend/service if one exists

That is expected. In a community-hosted game, the authoritative server is the trust anchor. If a server is malicious, rarity on that server cannot be trusted.

## Recommended Implementation Phases

### Phase 1: Policy Split

Make the distinction explicit in content and code:

- normal weapons are `Universal`
- rare variants are `Persistent Rare`

Success criteria:

- normal weapon flow is frictionless
- rare items are visibly separate in schema and UI

### Phase 2: Server-Local Persistent Repository

Add a durable ownership store on the server.

Recommended first implementation:

- SQLite or JSON-backed repository
- keyed by stable player identity
- loaded on join
- saved on grant/revoke

Success criteria:

- rare ownership survives reconnects and restarts
- no client action can self-grant persistent rare items

### Phase 3: Trusted Grant Pipeline

Limit who may issue rare items.

Recommended sources:

- admin command path
- migration/import tools
- future backend sync

Every grant should create an audit record.

### Phase 4: Cosmetic Rarity

Once the same model is proven on combat variants, extend it to hats, unusuals, and other cosmetics.

### Phase 5: Optional External Entitlement Service

Only if the project needs cross-server inventory trust:

- add account authentication
- fetch entitlement sets from a service
- keep simulation validation local

## Concrete Recommendation For OpenGarrison

Use this exact product rule for now:

- all regular weapons and regular gameplay variants are universal
- rare variants are tracked-only and persistent
- the server enforces all ownership and equip rules
- rare-item issuance is not part of ordinary gameplay plugins
- start with a server-local persistent repository before considering a separate itemserver

## Immediate Next Step

The next implementation step should be:

1. define ownership policy classes in the gameplay item schema
2. add a server-side persistent ownership repository abstraction
3. define a stable player identity record for persistence keys
4. route rare grant/revoke through a narrow audited authority API

That is the right next move because it gives you strict rare ownership without overengineering universal weapons.

## Summary

OpenGarrison does not need a separate itemserver to keep normal weapons honest.

It does need:

- server-authoritative validation
- a clean policy split between universal and rare items
- durable ownership persistence for rare variants
- a trusted, narrow grant authority

That model gives you the practical multiplayer baseline you want:

- everyone gets the normal weapons
- rare items actually mean something
- the client cannot simply edit itself into owning them
