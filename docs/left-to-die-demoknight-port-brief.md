# Left to Die Demoknight Port Brief

## Scope

This document summarizes:

- the legacy `OpenGarrison-Fork` codebase outside `Modern`
- the current Left to Die implementation constraints
- the Demoknight-related implementation in the GG2 `RM` branch
- the recommended integration path for bringing Demoknight into Left to Die cleanly

Reference clone used for RM research:

- `C:\Users\level\AppData\Local\Temp\Gang-Garrison-2-RM`

## Legacy OpenGarrison Architecture

### High-level layout

- `Core`: authoritative gameplay model, simulation, entities, combat, class definitions, match state
- `Client`: game session orchestration, menus, HUD, rendering, local offline modes such as Left to Die
- `Server`: dedicated server runtime
- `Protocol`: network messages and shared protocol types
- `BotAI`: bot behavior support
- `GameplayModding.Abstractions`: data-driven gameplay definitions for classes, loadouts, and items

The important architectural pattern is that both `SimulationWorld` and `Game1` are split across many partial files. The game is not organized around a single feature module per mode or class; it is organized around engine subsystems and gameplay subsystems.

### Class and loadout model

Relevant files:

- `Core/Gameplay/PlayerClass.cs`
- `Core/Gameplay/CharacterClassCatalog.cs`
- `Core/Gameplay/StockGameplayModCatalog.cs`
- `GameplayModding.Abstractions/GameplayClassDefinition.cs`
- `GameplayModding.Abstractions/GameplayClassLoadoutDefinition.cs`
- `GameplayModding.Abstractions/BuiltInGameplayBehaviorIds.cs`

Observations:

- `PlayerClass` currently contains stock classes plus `Quote`. There is no `Demoknight`.
- `CharacterClassCatalog` exposes classes as immutable definitions with movement, health, collision, and primary-weapon identity.
- `StockGameplayModCatalog` already contains a richer data model than the runtime currently uses. It supports class loadouts and items.
- The stock Demoman definition is already represented as a loadout-driven class:
  - class id: `demoman`
  - default primary item: `weapon.minelauncher`
  - default secondary item: `ability.demoman-detonate`
- Built-in loadout behavior support is minimal. The only Demoman-specific built-in secondary behavior currently wired through the stock catalog is `builtin.ability.demoman_detonate`.

Implication:

- OpenGarrison already has the beginnings of a professional loadout architecture.
- It does not yet have the runtime behavior surface necessary to express a Demoknight-style alternate Demoman loadout cleanly.

### Runtime simulation seams

Relevant files:

- `Core/Simulation/Core/SimulationWorld.cs`
- `Core/Simulation/Runtime/SimulationWorld.InputHandling.cs`
- `Core/Simulation/Combat/SimulationWorld.WeaponFireHandler.cs`
- `Core/Simulation/Networking/SimulationWorld.NetworkPlayerConfiguration.cs`
- `Core/Simulation/Runtime/SimulationWorld.Spawning.cs`
- `Core/Entities/Players/Core/PlayerEntity.cs`

Observations:

- `SimulationWorld` owns player slots, spawning, combat dispatch, match flow, and per-player runtime configuration.
- `SyncExperimentalGameplayLoadout` is the main existing seam for attaching extra per-player equipment state, but today it only covers narrow cases such as Soldier shotgun behavior and dropped weapon pickups.
- `PlayerEntity` contains a large amount of explicit class-specific state for built-in classes.
- There is no generic runtime state for:
  - melee charge
  - shield meter
  - loadout-specific mobility overrides
  - loadout-specific melee crit rules
  - weapon-granted passives such as altered cap strength
- Demoman input is currently hard-coded around stock behavior:
  - primary fire uses the mine launcher
  - secondary fire detonates owned mines

Implication:

- Demoknight cannot be added by data alone.
- The port needs explicit runtime support for a Demoman alternate loadout, or a new survivor wrapper built on top of Demoman rules.

## Left to Die Architecture

Relevant files:

- `Client/Game/Gameplay/LastToDie/Game1.LastToDieSession.cs`
- `Client/Game/Gameplay/Practice/Game1.PracticeBots.cs`
- `Client/Game/Gameplay/Hud/Menus/Game1.ClassSelect.cs`

Observations:

- Left to Die is effectively a client-driven offline mode layered on top of the same simulation stack.
- `BeginLastToDieStage` hard-codes the local survivor to `PlayerClass.Soldier`.
- The mode flow currently assumes one playable survivor archetype.
- Left to Die perk plumbing is partially Soldier-centric.
- Friendly bot count is currently `0`; the mode is not already structured around a second allied survivor.
- Enemy bot orchestration is done through practice-bot style slot management and class assignment.

Implication:

- "Second playable character" in Left to Die is not only a weapon port.
- The mode needs survivor-selection support and a clean way to map the chosen survivor to simulation loadout/runtime behavior.

## GG2 RM Demoknight Findings

### Core conclusion

RM does not implement Demoknight as a standalone class.

It implements Demoknight as a Demoman alternate loadout composed from weapon choices and passive effects.

That is the most important finding from the investigation.

### RM files inspected

- `Source/gg2/Scripts/randomizer/weapon_init.gml`
- `Source/gg2/Scripts/randomizer/givePassiveEffect.gml`
- `Source/gg2/Objects/InGameElements/Character.events/Create.xml`
- `Source/gg2/Objects/InGameElements/Character.events/Begin Step.xml`
- `Source/gg2/Objects/InGameElements/Character.events/Step.xml`
- `Source/gg2/Objects/InGameElements/Character.events/End Step.xml`
- `Source/gg2/Objects/Weapons/Templates/MeleeT.events/Create.xml`
- `Source/gg2/Objects/Weapons/Templates/MeleeT.events/Alarm 1.xml`
- `Source/gg2/Objects/Weapons/Demoman/Eyelander.events/Create.xml`
- `Source/gg2/Objects/Weapons/Demoman/Paintrain.events/Create.xml`
- `Source/gg2/Objects/Weapons/Demoman/Paintrain.events/Destroy.xml`
- `Source/gg2/Objects/Weapons/Demoman/GrenadeHand.events/Create.xml`
- `Source/gg2/Objects/Menus/Loadout Menu Elements/LoadoutMenu.events/User Event 2.xml`
- `Source/gg2/Objects/Map elements/Gamemode-specific/CP/ControlPoint.events/Step.xml`

### Demoman loadout registration

RM registers multiple alternate Demoman weapons in the loadout system. The Demoman slot is not split into a separate "Demoknight" class.

Relevant loadout pieces found:

- sticky/mine-side options include `Minegun`, `ScottishResistance`, `StickyJumper`, and others
- secondary/melee-side options include `GrenadeLauncher`, `Eyelander`, `Paintrain`, and `GrenadeHand`

### Eyelander behavior

Relevant file:

- `Source/gg2/Objects/Weapons/Demoman/Eyelander.events/Create.xml`

Observed behavior:

- melee weapon
- base hit damage set above the generic melee template
- damage source specific to Eyelander
- ability assigned to `ABILITY_DASH`
- right-click activates `CHARGE`
- charge meter starts full and recharges back to full

Charge runtime behavior comes from shared character and melee template logic rather than from Eyelander alone.

Shared dash logic is implemented in:

- `Source/gg2/Objects/InGameElements/Character.events/Begin Step.xml`
- `Source/gg2/Objects/InGameElements/Character.events/Step.xml`
- `Source/gg2/Objects/Weapons/Templates/MeleeT.events/Alarm 1.xml`

Observed charge behavior:

- right-click with a full meter starts charge
- charge modifies movement and acceleration
- meter drains over time while active
- hitting during charge upgrades melee damage to minicrit or crit depending on remaining charge
- the hit consumes and cancels the charge
- manual cancel is also supported

### Paintrain behavior

Relevant files:

- `Source/gg2/Objects/Weapons/Demoman/Paintrain.events/Create.xml`
- `Source/gg2/Objects/Weapons/Demoman/Paintrain.events/Destroy.xml`
- `Source/gg2/Scripts/randomizer/givePassiveEffect.gml`
- `Source/gg2/Objects/Map elements/Gamemode-specific/CP/ControlPoint.events/Step.xml`

Observed behavior:

- weapon sets `owner.capStrength = 2` while equipped
- destroy path restores cap strength
- passive effect script grants `+30` max HP in flag/intelligence contexts
- control point capture logic respects `capStrength`

Important caveat:

- RM loadout UI text claims Paintrain pierces generator shields.
- I did not find a clear explicit implementation of that shield-piercing behavior in the inspected source.
- Treat that specific bullet as unverified until proven in a later targeted search.

### GrenadeHand behavior

Relevant files:

- `Source/gg2/Objects/Weapons/Demoman/GrenadeHand.events/Create.xml`
- its step/alarm user events

Observed behavior:

- special projectile/melee hybrid
- supports hold-to-delay-throw behavior
- appears to be a separate experimental alternate, not required for a core Demoknight port

### Passive-effect pattern

RM applies weapon-granted passives during character creation by iterating equipped weapons and running `givePassiveEffect`.

This is a clean design signal for OpenGarrison:

- Demoknight in RM is not "a new body"
- it is "Demoman with alternate equipment that grants runtime passives and alternate input behavior"

## Recommended Port Strategy

### Recommendation

Do not add a new global `PlayerClass.Demoknight` as the first implementation.

Instead:

1. treat Demoknight as a Left to Die survivor option
2. back that survivor with Demoman-derived runtime behavior and equipment
3. add the minimum loadout/runtime systems needed to support that survivor cleanly

### Why this is the right direction

- It matches the original RM implementation model.
- It avoids duplicating class definitions, UI, rendering, networking, and mod catalog entries for a class that is conceptually an alternate Demoman loadout.
- It reduces regression risk across the rest of the game.
- It aligns with established game-development practice: build the alternate playstyle as a loadout/archetype layer, not as a second copy of the entire class unless the design truly demands it.

### What "second playable character" should mean in OpenGarrison

In Left to Die, "second playable character" should be implemented as a survivor choice presented to the player, for example:

- Soldier
- Demoknight

Under the hood:

- Soldier remains the existing path
- Demoknight maps to Demoman-based class data plus new alternate runtime behavior

This keeps the user-facing fantasy intact without bloating the base class system.

## Required OpenGarrison Work Areas

### 1. Left to Die survivor selection

Primary file:

- `Client/Game/Gameplay/LastToDie/Game1.LastToDieSession.cs`

Needed changes:

- replace the hard-coded `PlayerClass.Soldier` join path with a survivor-selection path
- preserve Soldier as a default/fallback survivor
- keep Left to Die stage setup independent from the exact survivor chosen

### 2. Survivor-to-runtime mapping

Primary files:

- `Core/Simulation/Core/SimulationWorld.cs`
- `Core/Simulation/Networking/SimulationWorld.NetworkPlayerConfiguration.cs`
- `Core/Gameplay/StockGameplayModCatalog.cs`

Needed changes:

- introduce a clean way to express alternate loadout/equipment for the local Left to Die survivor
- extend loadout sync beyond the current narrow experimental special cases
- add Demoknight-equivalent equipment identifiers and behaviors

### 3. Demoman alternate input behavior

Primary files:

- `Core/Simulation/Runtime/SimulationWorld.InputHandling.cs`
- `Core/Simulation/Combat/SimulationWorld.WeaponFireHandler.cs`
- `Core/Entities/Players/Core/PlayerEntity.cs`

Needed changes:

- decouple Demoman secondary input from unconditional mine detonation
- support a Demoknight secondary action that starts or cancels charge
- add runtime state for charge activity, meter, and any melee crit windows
- dispatch melee hits through charge-aware damage rules

### 4. Passives and movement overrides

Primary files:

- `Core/Entities/Players/Core/PlayerEntity.cs`
- movement/combat partials adjacent to `PlayerEntity`
- relevant `SimulationWorld` movement/combat systems

Needed changes:

- charge movement rules
- charge meter depletion and recharge
- loadout-specific passives such as altered cap strength or health modifiers if they are required for Left to Die

### 5. HUD and player feedback

Primary files:

- Left to Die HUD partials under `Client/Game/Gameplay/LastToDie`
- overlay/HUD partials under `Client/Game/Gameplay/Hud`

Needed changes:

- show charge meter if Demoknight is selected
- make mode-specific feedback survivor-aware rather than Soldier-only

## Suggested Implementation Sequence

1. Add a lightweight Left to Die survivor concept with Soldier and Demoknight.
2. Route Left to Die stage start through the chosen survivor instead of hard-coded Soldier.
3. Add a Demoman alternate-loadout runtime flag or definition path.
4. Split Demoman secondary behavior into stock detonate versus Demoknight charge.
5. Add charge state, meter rules, and melee crit handling.
6. Add HUD support for the charge meter.
7. Revisit optional passives such as Paintrain-style capture modifiers only if they make sense in Left to Die.

## Risks

- The current runtime is still class-centric, not fully loadout-centric. Some Demoknight behavior will require explicit code, not just catalog entries.
- If a new global `PlayerClass.Demoknight` is added too early, the change surface expands into menus, catalogs, networking, assets, bots, and every class switch path.
- Left to Die already contains mode-specific assumptions around Soldier and current perk plumbing; those assumptions need to be removed carefully rather than patched around.

## Practical Conclusion

The clean professional path is:

- keep Demoknight as a Left to Die survivor identity
- implement it on top of Demoman-derived loadout/runtime behavior
- avoid creating a new global player class unless later requirements prove that necessary

That approach matches RM, limits regression surface, and gives OpenGarrison a better foundation for future alternate loadouts instead of another one-off class fork.
