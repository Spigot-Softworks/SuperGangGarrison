# GG2R Import Plan

## Purpose

This document turns the GG2 Randomizer repo into an actionable import map for OpenGarrison's gameplay pack and plugin system.

Reference source used for this pass:

- Temporary clone: `C:\Users\level\AppData\Local\Temp\GG2R-codex-13e6db4f705847bdb0428b24d4c273de`
- Source repo: `https://github.com/KevinKuntz255/GG2R`

This is an engineering plan, not a promise that every GG2R weapon should be ported exactly as-is.

## Ground Rules

- Treat GG2R as a content and behavior reference, not an architecture to copy.
- Do not port GG2R's event patching, script injection, packed numeric loadouts, or custom plugin-owned networking.
- Keep gameplay authority on the OpenGarrison server.
- Bring over weapons in tranches, with each tranche mapped to explicit runtime seams.
- Do not import assets until the target behavior and presentation contract exist in OpenGarrison.

## Current OpenGarrison Readiness

### Already Usable

These behavior families already exist in the runtime registry:

- `builtin.weapon.pellet_gun`
- `builtin.weapon.flamethrower`
- `builtin.weapon.rocket_launcher`
- `builtin.weapon.mine_launcher`
- `builtin.weapon.minigun`
- `builtin.weapon.rifle`
- `builtin.weapon.medigun`
- `builtin.weapon.revolver`
- `builtin.weapon.blade`
- `builtin.ability.engineer_pda`
- `builtin.ability.pyro_airblast`
- `builtin.ability.demoman_detonate`
- `builtin.ability.heavy_sandvich`
- `builtin.ability.sniper_scope`
- `builtin.ability.medic_needlegun`
- `builtin.ability.medic_uber`
- `builtin.ability.spy_cloak`
- `builtin.ability.quote_blade_throw`

### Seams We Have Now

- Gameplay packs and modular JSON item/class files.
- Runtime registry with built-in weapon and ability behavior binding.
- Server-authoritative loadout selection.
- Server-owned item ownership baseline.
- Server and Lua inspection/mutation seams for loadouts and owned items.

### Seams Still Missing

- Pack-declared gameplay asset registration for custom weapon sprites and HUD art.
- Item-level projectile behavior registration beyond stock projectile families.
- Persistent per-item meters and alternate fire state as first-class replicated gameplay state.
- Generalized wearable/consumable/throwable/action-item item model.
- Engineer building variants and wrangler-style control hooks.
- Medigun variants and meter persistence across weapon switches.
- Bow/crossbow projectile families.
- Jarate/Milk-style status projectile families.

## Import Categories

Each weapon is assigned one category.

- `Ready Now`
  - Can be represented with current item/loadout/ownership seams and an existing built-in behavior.
- `Needs Behavior`
  - Fits the current item model, but needs one or more new reusable behavior bindings or projectile families.
- `Needs Subsystem`
  - Depends on class-specific simulation, building control, persistent meters, or status systems that do not yet exist as reusable gameplay systems.
- `Defer`
  - Not a near-term port target, is unimplemented upstream, or should wait until broader systems are stable.

## Recommended Tranches

### Tranche 1: Data-Driven Variants On Existing Behavior

Target this first. These give the best return for the least new system work.

- Direct Hit
- Black Box
- Air Strike
- Brass Beast
- Tomislav
- Natascha
- Family Business
- Backburner
- Reserve Shooter
- Force-A-Nature
- Soda Popper
- Shortstop or equivalent pellet-gun variant
- Eyelander
- Pain Train
- Ubersaw
- Shiv
- Big Earner
- Diamondback
- Etranger
- Bazaar Bargain
- Machina
- Sydney Sleeper

### Tranche 2: New Reusable Projectile or Action Families

- Flare gun family
- Jarate/Mad Milk family
- Bow/crossbow family
- Laser projectile family
- Throwable consumables
- Banner/action-item family

### Tranche 3: Class Systems

- Engineer building variants and wrangler.
- Medic alt medigun models and persistent uber/meter state.
- Spy special backstab and cloak interactions beyond current stock assumptions.
- Custom meter-heavy or movement-heavy weapons with persistent replicated state.

## Weapon Map

The notes below are partly direct evidence from GG2R scripts and partly engineering inference from weapon family and current OpenGarrison seams.

### Runner

| Weapon | Status | Why |
| --- | --- | --- |
| `scattergun` | Ready Now | Existing pellet-gun primary. Mostly stock content mapping. |
| `pistol` | Needs Behavior | We do not have a reusable pistol/SMG hitscan primary/secondary behavior yet. |
| `forceanature` | Needs Behavior | Pellet-gun base is fine, but knockback and hype-style behavior need reusable hooks. |
| `sodapopper` | Needs Behavior | Pellet-gun variant plus hype/meter behavior. |
| `sandman` | Needs Behavior | Melee plus throwable ball projectile and stun logic. |
| `madmilk` | Needs Behavior | Throwable status projectile plus heal-on-soaked-target system. |
| `bonk` | Needs Subsystem | Consumable action item with temporary invulnerability and meter/recharge. |
| `atomizer` | Needs Behavior | Blade/melee variant with movement modifier and extra jump. |
| `flashlight` | Defer | Not a standard TF2-style portability priority. |
| `rundown` | Defer | Upstream README/TODO already treats this as optional/non-finished territory. |

### Rocketman

| Weapon | Status | Why |
| --- | --- | --- |
| `rocketlauncher` | Ready Now | Existing rocket launcher primary. |
| `shotgun` | Ready Now | Existing pellet-gun secondary pattern once class secondary presentation is bridged. |
| `directhit` | Ready Now | Script evidence shows pure rocket stat changes and sprite swap. |
| `blackbox` | Ready Now | Rocket variant plus heal-on-hit, which can be expressed as a reusable damage callback or replicated state effect. |
| `airstrike` | Needs Behavior | Rocket variant, but kill scaling and salvo behavior need reusable behavior state. |
| `reserveshooter` | Needs Behavior | Pellet-gun variant with airborne minicrit rule. |
| `buffbanner` | Needs Subsystem | Metered action item and team buff system. |
| `cowmangler` | Needs Behavior | New laser/charged projectile family and sentry-specific rules. |
| `rbison` | Needs Behavior | Energy projectile family. |
| `equalizer` | Needs Behavior | Health-scaling melee damage/speed rule. |

### Firebug

| Weapon | Status | Why |
| --- | --- | --- |
| `flamethrower` | Ready Now | Existing flamethrower behavior. |
| `shotgun` | Ready Now | Existing pellet-gun secondary pattern. |
| `backburner` | Ready Now | Flamethrower variant with stat tuning and directional crit rule. |
| `flaregun` | Needs Behavior | New flare projectile family. |
| `detonator` | Needs Behavior | Flare projectile plus manual detonation. |
| `phlog` | Needs Subsystem | Metered action mode and crit-state activation. |
| `napalm` | Needs Behavior | Likely flare/area status extension. |
| `frostbite` | Needs Subsystem | Freeze/slow/status system with custom visual state. |
| `transmutator` | Needs Subsystem | Non-stock class mechanic with likely transformation/custom status rules. |
| `wrecker` | Needs Behavior | Melee variant; may be straightforward once generic melee modifiers exist. |

### Detonator

| Weapon | Status | Why |
| --- | --- | --- |
| `minegun` | Ready Now | Existing mine launcher behavior. |
| `grenadelauncher` | Needs Behavior | Separate grenade projectile family not yet exposed in OpenGarrison. |
| `eyelander` | Ready Now | Already partially modeled experimentally; should move into full item path. |
| `paintrain` | Ready Now | Already partially modeled experimentally; should move into full item path. |
| `doubletrouble` | Needs Behavior | Dual-grenade or multi-projectile grenade variant. |
| `grenade` | Needs Behavior | Melee/throw grenade hybrid behavior. |
| `scottishresistance` | Needs Behavior | Mine launcher variant with selective detonation and different trap caps. |
| `stickyjumper` | Needs Behavior | Non-damaging mine launcher variant with mobility rules. |
| `stickysticker` | Needs Behavior | Player-sticking mine behavior and custom collision rules. |
| `tigeruppercut` | Needs Subsystem | Charge/melee-special integration beyond current generalized model. |

### Overweight

| Weapon | Status | Why |
| --- | --- | --- |
| `minigun` | Ready Now | Existing minigun behavior. |
| `shotgun` | Ready Now | Existing pellet-gun secondary pattern. |
| `brassbeast` | Ready Now | Minigun stat variant. |
| `tomislav` | Ready Now | Minigun stat variant. |
| `natacha` | Ready Now | Minigun variant with slow-on-hit; may require a small reusable hit effect hook, but still a near-term target. |
| `familybusiness` | Ready Now | Shotgun stat variant. |
| `sandvich` | Ready Now | Existing heavy sandvich ability seam. |
| `chocolate` | Needs Behavior | Consumable with temporary max-health style rule. |
| `iron` | Needs Behavior | Melee kill/crit style modifier. |
| `kgob` | Needs Behavior | Kill-based ammo/damage scaling rule. |

### Healer

| Weapon | Status | Why |
| --- | --- | --- |
| `needlegun` | Ready Now | Existing medic secondary ability and item seam. |
| `medigun` | Ready Now | Existing medigun and uber abilities. |
| `ubersaw` | Ready Now | Melee variant with uber gain on hit, once melee hooks are generalized. |
| `blutsauger` | Needs Behavior | Needlegun variant with heal-on-hit and regen tradeoff. |
| `kritzkrieg` | Needs Subsystem | Alternate uber model requires medigun effect variants and charge semantics. |
| `quickfix` | Needs Subsystem | Alternate medigun behavior, heal model, and charge semantics. |
| `overhealer` | Needs Subsystem | Explicit overheal rules still not implemented in GG2R README and not generalized in OpenGarrison. |
| `crossbow` | Needs Behavior | New bow/crossbow projectile family with heal-on-hit ally logic. |
| `potion` | Needs Behavior | Throwable or projectile heal family. |
| `terminalbreath` | Needs Subsystem | Multi-target or aura-like healing/action model. |

### Constructor

| Weapon | Status | Why |
| --- | --- | --- |
| `shotgun` | Ready Now | Existing pellet-gun pattern. |
| `wrench` | Needs Subsystem | Building interaction is still stock-specific. |
| `widowmaker` | Needs Subsystem | Uses metal as ammo; needs building/resource integration. |
| `frontierjustice` | Needs Subsystem | Revenge-crit mechanic depends on sentry kill tracking. |
| `pomson` | Needs Behavior | Projectile family plus uber drain/status interactions. |
| `nailgun` | Needs Behavior | New rapid projectile family. |
| `sheriff` | Needs Behavior | Pistol/revolver-adjacent alternate. |
| `stungun` | Needs Behavior | Stun weapon family. |
| `wrangler` | Needs Subsystem | Direct sentry control and shield logic. |
| `eurekaeffect` | Needs Subsystem | Teleport/building interaction and sentry state hooks. |

### Infiltrator

| Weapon | Status | Why |
| --- | --- | --- |
| `revolver` | Ready Now | Existing revolver behavior. |
| `knife` | Ready Now | Existing blade behavior plus current spy cloak seam. |
| `bigearner` | Needs Behavior | Melee variant with speed or cloak reward hooks. |
| `diamondback` | Needs Behavior | Revolver variant with earned crit storage. |
| `etranger` | Needs Behavior | Revolver variant with cloak gain-on-hit. |
| `diplomat` | Needs Behavior | Revolver stat variant. |
| `spycicle` | Needs Subsystem | Fire immunity/freeze/melee replacement logic. |
| `zapper` | Needs Subsystem | Engineer-building interaction beyond stock sapper path. |
| `medichain` | Needs Subsystem | Non-stock backstab/chain effect. |
| `nordicgold` | Needs Behavior | Revolver variant. |
| `shazia` | Needs Behavior | Revolver or melee-adjacent custom variant, likely still reusable. |

### Rifleman

| Weapon | Status | Why |
| --- | --- | --- |
| `rifle` | Ready Now | Existing rifle behavior. |
| `smg` | Needs Behavior | We still need a reusable SMG/pistol behavior family. |
| `kukri` | Ready Now | Existing blade/melee family. |
| `bazaarbargain` | Needs Behavior | Rifle variant with scoped charge/headshot scaling. |
| `machina` | Needs Behavior | Rifle variant with penetration or damage rule changes. |
| `sydneysleeper` | Needs Behavior | Rifle variant with status application instead of headshot rules. |
| `shiv` | Ready Now | Melee variant, likely simple stat/status swap once generic melee modifiers exist. |
| `jarate` | Needs Behavior | Throwable status projectile family. |
| `huntsman` | Needs Behavior | Bow projectile family with draw/charge logic. |
| `rocketboots` | Needs Subsystem | Movement/action subsystem rather than weapon-only logic. |

### Q/C

| Weapon | Status | Why |
| --- | --- | --- |
| `blade` | Ready Now | Existing blade family. |
| `machinegun` | Needs Behavior | New sustained-fire bullet family. |

## Asset Import Guidance

GG2R assets are useful, but only after the gameplay presentation seam is formalized.

### Sprite Patterns Worth Supporting

The most common pattern in `randomizer_sprites` is:

- `*S.png`: idle
- `*FS.png`: firing/recoil
- `*FRS.png`: reload

Additional useful patterns also exist:

- `*ClipS.png`: ammo HUD art
- `*HandS.png`: consumable/throwable hand presentation
- projectile sprites such as `ArrowS`, `LaserShotS`, `GrenadeS`, `BallS`, `MilkS`, `PissS`

### Target Presentation Contract

Before copying assets into OpenGarrison, the gameplay item presentation model should support:

- `worldSpriteName`
- `recoilSpriteName`
- `reloadSpriteName`
- `hudSpriteName`
- optional `ammoHudSpriteName`
- optional projectile sprite ids
- pack-declared texture source and atlas metadata

## Code Patterns To Reuse Carefully

Useful ideas from GG2R:

- Shared weapon-family inheritance by projectile/action type.
- Explicit per-weapon stat deltas over a shared base.
- Strong coupling between weapon description text and weapon identity.

Do not reuse directly:

- `execute_file` bootstrap and object event monkey-patching in `plugin.gml` and `loadCode.gml`.
- Packed numeric loadouts in `weapon_init.gml`.
- Custom plugin-layer replication in `networking.gml`.

## Immediate Next Work In OpenGarrison

### Phase A

- Build pack-declared gameplay asset registration.
- Allow gameplay item presentation to bind custom sprites and HUD art from pack assets.
- Add a reusable `pistol_smg` bullet behavior family.

### Phase B

- Add reusable projectile families:
  - flare
  - grenade
  - bow/crossbow
  - throwable status projectile
  - energy projectile

### Phase C

- Add reusable item action families:
  - banner/action item
  - consumable with recharge
  - throwable consumable

### Phase D

- Build subsystem seams:
  - engineer building variants and wrangler control
  - medigun effect variants and persistent charge semantics
  - generalized status effects for milk/jarate/freeze
  - persistent replicated item meter state

## Recommended First Imports

Once custom gameplay assets are wired, start with these weapons:

- Direct Hit
- Black Box
- Backburner
- Reserve Shooter
- Brass Beast
- Tomislav
- Family Business
- Eyelander
- Pain Train
- Ubersaw
- Big Earner
- Diamondback
- Bazaar Bargain

These are the best first wave because they mostly preserve stock simulation shape while validating our new item, ownership, loadout, and presentation seams.
