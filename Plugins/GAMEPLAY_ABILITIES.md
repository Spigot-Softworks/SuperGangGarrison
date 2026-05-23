# Gameplay Ability And Weapon Authoring

This is the current authoring guide for modular gameplay abilities and primary
weapon behavior. The stock game and plugins use the same gameplay item, loadout,
ability, HUD, and behavior IDs, but they do not use the same execution layer.

Stock/default gameplay content should be authored as JSON gameplay data plus
registered C# executors. Lua is for plugin and mod content, not for base game
stock content.

## Current Contract

Stock gameplay content is data-backed:

- Stock classes live in `Core/Content/Gameplay/stock.gg2/classes/*.json`.
- Stock items and weapons live in `Core/Content/Gameplay/stock.gg2/items/*.json`.
- Stock class runtime slot metadata lives in class JSON under `runtime`.
- Stock ability metadata lives on item JSON under `ability`.
- Stock primary weapon metadata lives on item JSON under `ammo`, `combat`, and
  `presentation`.
- Stock loadout attachment lives in class JSON loadouts.

C# still owns authoritative simulation behavior. A new stock action or primary
weapon behavior needs a registered built-in executor unless it can reuse an
existing executor. That is intentional: stock behavior must remain deterministic,
testable, replay-friendly, and safe for client prediction work.

Lua plugins can register abilities, ability executors, primary weapon behaviors,
weapon items, and loadouts during startup. Lua execution is server-authoritative
and constrained to host operations.

## Stock Content Flow

Use this flow for base game content:

1. Add or edit the gameplay item JSON.
2. Attach it to a class loadout in class JSON.
3. Reuse an existing built-in executor when the behavior already exists.
4. Add a C# built-in ability or primary weapon executor when the behavior is new.
5. Add tests for the loader, registry, and any changed simulation behavior.
6. Update client prediction, bot logic, replay support, or HUD code only if the
   new behavior needs first-class support there.

## Stock Class Template

Class JSON owns the stock class data and the runtime slot binding:

```json
{
  "id": "scout",
  "displayName": "Scout",
  "runtime": {
    "playerClass": "Scout",
    "supportsExperimentalAcquiredWeapon": true,
    "primaryWeaponKillFeedSprite": "ScatterKL"
  },
  "movement": {
    "maxHealth": 100,
    "collisionLeft": -6.0,
    "collisionTop": -10.0,
    "collisionRight": 7.0,
    "collisionBottom": 24.0,
    "runPower": 1.4,
    "jumpStrength": 8.3,
    "maxAirJumps": 1,
    "tauntLengthFrames": 8
  },
  "presentation": {
    "spritePrefix": "Scout",
    "standSuffix": "StandS",
    "runSuffix": "RunS",
    "jumpSuffix": "JumpS"
  },
  "loadouts": {
    "scout.stock": {
      "id": "scout.stock",
      "displayName": "Stock",
      "primaryItemId": "weapon.scattergun",
      "utilityItemId": "ability.scout-utility",
      "abilityItemIds": [ "ability.experimental-ltd-passive" ]
    }
  },
  "defaultLoadoutId": "scout.stock"
}
```

`runtime.playerClass` maps the data class onto the current network/runtime enum
slot. True new class slots beyond the current enum still require protocol,
class-select, server command, bot, replay, and UI work.

## Stock Ability Item Template

Use an ability item when the action is triggered by secondary fire, Spacebar,
taunt interception, or a passive tick:

```json
{
  "id": "ability.example-dash",
  "displayName": "Example Dash",
  "slot": "Utility",
  "behaviorId": "builtin.utility.example_dash",
  "ability": {
    "category": "utility",
    "activation": "pressed",
    "executorId": "builtin.ability.example_dash",
    "tags": [ "movement", "dash", "cooldown" ],
    "parameters": {
      "cooldownTicks": 180,
      "impulse": 12.0
    }
  },
  "ammo": {},
  "presentation": {
    "hud": {
      "displayKind": "meter",
      "stackGroup": "ability",
      "order": 90,
      "stateProvider": "abilityCooldown",
      "stateOwner": "core.ability",
      "cooldownKey": "example_dash_cooldown_ticks",
      "maxCooldown": 180,
      "hideWhenUnavailable": true
    }
  }
}
```

The dispatcher does not create cooldown behavior by itself. The executor writes
state, and HUD metadata reads that state.

### Stock/Data HUD Widgets

Stock abilities and alternate weapons should use data-backed HUD metadata before
adding class-specific HUD code:

- `displayKind: "meter"` with `stackGroup: "ability"` draws the standard
  cooldown/charge meter from replicated ability state.
- `displayKind: "ammoPanel"` with `stackGroup: "weapon"` draws the reusable
  weapon ammo panel for alternate weapons.
- `displayKind: "custom"` can point at a reusable stock renderer by `widgetId`.
  Current built-in widget IDs are `abilityCooldownMeter` and `weaponAmmoPanel`.

Adding a new stock HUD shape should mean adding one reusable renderer ID and
referencing it from item JSON, not hardcoding a class-specific HUD branch.

## Stock Weapon Item Template

Use a weapon item when the player fires through the primary weapon path. A
secondary or utility slot can still be a weapon if the loadout equips and toggles
it:

```json
{
  "id": "weapon.example-nailgun",
  "displayName": "Nailgun",
  "slot": "Secondary",
  "behaviorId": "builtin.weapon.scout_nailgun",
  "ammo": {
    "maxAmmo": 30,
    "ammoPerUse": 1,
    "projectilesPerUse": 1,
    "useDelaySourceTicks": 4,
    "reloadSourceTicks": 2,
    "spreadDegrees": 2.0,
    "minProjectileSpeed": 9.0,
    "additionalProjectileSpeed": 0.0,
    "autoReloads": true,
    "ammoRegenPerTick": 0,
    "refillsAllAtOnce": false
  },
  "combat": {
    "fireSoundName": "NeedleSnd",
    "directHitDamage": 6.0
  },
  "presentation": {
    "worldSpriteName": "NeedlegunS",
    "recoilSpriteName": "NeedlegunFS",
    "hudSpriteName": "NeedleAmmoS",
    "weaponOffsetX": -7.0,
    "weaponOffsetY": 0.0,
    "recoilDurationSourceTicks": 4,
    "reloadDurationSourceTicks": 2,
    "hud": {
      "displayKind": "ammoPanel",
      "stackGroup": "weapon",
      "order": 60,
      "stateProvider": "secondaryAmmo",
      "hideWhenUnavailable": true
    }
  }
}
```

`behaviorId` must have a registered primary weapon behavior. Existing stock
behavior IDs include pellet guns, flamethrower, rockets, mines, grenades,
minigun, rifle, medigun, revolver, and blade.

## Stock Example: Scout Nailgun Spacebar Ability

This uses Scout and the Nailgun only as an example of the stock-content flow.
The same sequence applies to any class and any stock ability/weapon pair.

1. First choose the class that receives the new option. Here that is Scout, so
   the loadout edit happens in
   `Core/Content/Gameplay/stock.gg2/classes/scout.json`.
2. Add each new item as stock gameplay JSON under
   `Core/Content/Gameplay/stock.gg2/items/`. A weapon that fires through the
   weapon path is a weapon item. A Spacebar action is an ability item.
3. Decide whether the new action reuses an existing executor or needs a new
   built-in C# executor. Lua is not required or desired for stock gameplay.
4. Attach the items to a loadout. The loadout is what makes the class able to
   equip the weapon and trigger the Spacebar ability.
5. Register any new built-in behavior IDs and executors in the stock registry.
6. Add loader, registry, and simulation tests that prove the data and behavior
   are both wired.

For Scout's Nailgun, the stock implementation would look like this.

Add `Core/Content/Gameplay/stock.gg2/items/weapon.scout-nailgun.json`:

```json
{
  "id": "weapon.scout-nailgun",
  "displayName": "Nailgun",
  "slot": "Secondary",
  "behaviorId": "builtin.weapon.scout_nailgun",
  "ammo": {
    "maxAmmo": 30,
    "ammoPerUse": 1,
    "projectilesPerUse": 1,
    "useDelaySourceTicks": 4,
    "reloadSourceTicks": 2,
    "spreadDegrees": 2.0,
    "minProjectileSpeed": 9.0,
    "additionalProjectileSpeed": 0.0,
    "autoReloads": true,
    "ammoRegenPerTick": 0,
    "refillsAllAtOnce": false
  },
  "combat": {
    "fireSoundName": "NeedleSnd",
    "directHitDamage": 6.0
  },
  "presentation": {
    "worldSpriteName": "NeedlegunS",
    "recoilSpriteName": "NeedlegunFS",
    "hudSpriteName": "NeedleAmmoS",
    "weaponOffsetX": -7.0,
    "weaponOffsetY": 0.0,
    "recoilDurationSourceTicks": 4,
    "reloadDurationSourceTicks": 2,
    "hud": {
      "displayKind": "ammoPanel",
      "stackGroup": "weapon",
      "order": 60,
      "stateProvider": "secondaryAmmo",
      "hideWhenUnavailable": true
    }
  }
}
```

Add `Core/Content/Gameplay/stock.gg2/items/ability.scout-nailgun-toggle.json`:

```json
{
  "id": "ability.scout-nailgun-toggle",
  "displayName": "Nailgun",
  "slot": "Utility",
  "behaviorId": "builtin.utility.scout_nailgun_toggle",
  "ability": {
    "category": "utility",
    "activation": "pressed",
    "executorId": "builtin.ability.soldier_secondary_toggle",
    "tags": [ "weapon_swap" ]
  },
  "ammo": {},
  "presentation": {}
}
```

Add a Scout loadout variant in
`Core/Content/Gameplay/stock.gg2/classes/scout.json`:

```json
"scout.nailgun": {
  "id": "scout.nailgun",
  "displayName": "Nailgun",
  "primaryItemId": "weapon.scattergun",
  "secondaryItemId": "weapon.scout-nailgun",
  "utilityItemId": "ability.scout-nailgun-toggle",
  "abilityItemIds": [ "ability.experimental-ltd-passive" ]
}
```

Add built-in behavior IDs in
`GameplayModding.Abstractions/BuiltInGameplayBehaviorIds.cs`:

```csharp
public const string ScoutNailgun = "builtin.weapon.scout_nailgun";
public const string ScoutNailgunToggle = "builtin.utility.scout_nailgun_toggle";
```

Register the weapon behavior in
`Core/Gameplay/GameplayRuntimeRegistry.Stock.cs`:

```csharp
RegisterPrimaryWeaponBehavior(new GameplayPrimaryWeaponRuntimeBinding(
    BuiltInGameplayBehaviorIds.ScoutNailgun,
    PrimaryWeaponKind.Custom,
    Executor: new DelegateGameplayPrimaryWeaponExecutor(static context =>
        context.World.ExecuteScoutNailgunPrimaryWeapon(context))));
```

Then implement the C# executor on `SimulationWorld`. The executor should read
`context.Weapon`, spawn the intended projectile, and return handled. If the
weapon needs prediction, replay, bot, or first-class HUD behavior beyond generic
ammo HUD, prefer adding a first-class `PrimaryWeaponKind.NeedleGun` and routing
case instead of a custom executor.

Add tests that prove:

- the stock pack loads the Nailgun item and Scout loadout,
- the runtime registry resolves `builtin.weapon.scout_nailgun`,
- firing the equipped Nailgun spawns the expected projectile,
- the Spacebar toggle equips and stows the secondary weapon.

## Categories And Inputs

Built-in simulation dispatch currently calls these categories:

| Category | Input path | Typical use |
| --- | --- | --- |
| `secondary` | Secondary fire / right click | Airblast, scope, cloak, weapon-item secondary actions |
| `utility` | Use ability / Spacebar | Dash, superjump, jump pad, utility actions |
| `taunt` | Taunt button before normal taunt starts | Rage or taunt replacement abilities |
| `passive` | Central per-player passive tick loop | Passive effects and cooldown ticking |

Reserved categories can be used in data, but stock simulation only executes
them if a caller dispatches them:

| Category | Status |
| --- | --- |
| `movement` | Reserved semantic category; use `utility` or `passive` unless a caller dispatches it |
| `primary_alt` | Reserved semantic category; no built-in input hook yet |
| `status` | Reserved semantic category; usually implemented through passive/status operations |

## Activations

| Activation | Meaning |
| --- | --- |
| `pressed` | One-shot action on the press edge |
| `held` | Continuous/charge action while held; also receives press/release phases |
| `released` | Release-only action |
| `passive_tick` | Tick-only action from the passive dispatcher |

## Current Stock Examples

- Spacebar action with cooldown HUD:
  `Core/Content/Gameplay/stock.gg2/items/ability.heavy-utility.json`
- Secondary-fire action:
  `Core/Content/Gameplay/stock.gg2/items/ability.pyro-airblast.json`
- Weapon item with its own secondary ability:
  `Core/Content/Gameplay/stock.gg2/items/weapon.soldier-shotgun.json`
- Hidden passive:
  `Core/Content/Gameplay/stock.gg2/items/ability.experimental-ltd-passive.json`
- Taunt interception:
  `Core/Content/Gameplay/stock.gg2/items/ability.experimental-ltd-rage.json`
- Class runtime binding:
  `Core/Content/Gameplay/stock.gg2/classes/soldier.json`
- Data-backed blade limits and cost:
  `Core/Content/Gameplay/stock.gg2/items/weapon.blade.json` and
  `Core/Content/Gameplay/stock.gg2/items/ability.quote-blade-throw.json`

## Lua Ability Flow

Use Lua for plugin/mod content that is registered during server plugin startup.

Register executors and ability items during `initialize`:

```lua
local executor_id = "plugin.example.dash"

function plugin.initialize(host)
    plugin.host = host
    host.register_gameplay_ability_executor(executor_id)
    host.register_gameplay_ability({
        itemId = "ability.example-dash",
        displayName = "Example Dash",
        slot = "Utility",
        behaviorId = executor_id,
        ability = {
            category = "utility",
            activation = "pressed",
            executorId = executor_id,
            tags = { "movement", "dash", "cooldown" },
            parameters = { cooldownTicks = 180, impulseX = 12, impulseY = -4 }
        },
        presentation = {
            hud = {
                displayKind = "meter",
                stackGroup = "ability",
                stateProvider = "abilityCooldown",
                stateOwner = "example.plugin",
                cooldownKey = "example_dash_cooldown",
                maxCooldown = 180
            }
        }
    })
end
```

Implement `on_gameplay_ability_execute`:

```lua
function plugin.on_gameplay_ability_execute(e)
    if e.executorId ~= "plugin.example.dash" then
        return false
    end

    local cooldown = plugin.host.get_player_replicated_state_int(
        e.playerId,
        "example.plugin",
        "example_dash_cooldown") or 0

    if cooldown > 0 then
        return { handled = false, consumedInput = true }
    end

    plugin.host.try_apply_gameplay_impulse(e.playerId, 12, -4)
    plugin.host.try_set_gameplay_ability_cooldown(e.playerId, "example_dash_cooldown", 180)
    return { handled = true, consumedInput = true }
end
```

`try_set_gameplay_ability_cooldown` writes cooldown state under the current
plugin id. Match `presentation.hud.stateOwner` to the plugin id so the client
HUD can read it.

Lua abilities can also declare a custom ability-owned HUD widget in the same
ability registration. The server ability owns state; the paired client Lua
plugin owns drawing:

```lua
host.register_gameplay_ability({
    itemId = "ability.example-dash",
    displayName = "Example Dash",
    slot = "Utility",
    behaviorId = "plugin.example.dash",
    ability = {
        category = "utility",
        activation = "pressed",
        executorId = "plugin.example.dash"
    },
    presentation = {
        hud = {
            displayKind = "custom",
            stackGroup = "ability",
            order = 90,
            stateOwner = "example.server.plugin",
            cooldownKey = "example_dash_cooldown",
            widgetId = "example-dash-hud",
            widgetOwner = "example.client.plugin",
            widgetCallback = "draw_example_dash_hud",
            anchor = "bottom_right"
        }
    }
})
```

The matching client plugin only defines the named callback:

```lua
function plugin.draw_example_dash_hud(canvas, ability)
    local host = plugin.host
    local player_id = ability.localPlayerId
    local cooldown = host.get_player_replicated_state_int(
        player_id,
        ability.stateOwner,
        ability.cooldownKey) or 0

    canvas.draw_bitmap_text(
        "Dash " .. tostring(cooldown),
        16,
        canvas.viewport_height - 32,
        host.color(255, 255, 255, 255))
end
```

For client-only widgets that cannot change the ability JSON, use
`host.register_gameplay_ability_hud_widget({ itemId = "...", draw = function(...) end })`.
Unlike generic `register_hud_widget`, this only draws when the local player has
that gameplay item.

## Lua Loadout Attachment

Use `secondaryItemId` for right-click abilities and secondary weapon items. Use
`utilityItemId` for Spacebar abilities. Use hidden `abilityItemIds` for passives,
taunt interceptors, and abilities that should not replace a visible slot.

Register a full loadout:

```lua
host.register_gameplay_loadout({
    classId = "heavy",
    loadoutId = "heavy.example-dash",
    displayName = "Example Dash",
    primaryItemId = "weapon.minigun",
    secondaryItemId = "ability.heavy-sandvich",
    utilityItemId = "ability.example-dash",
    abilityItemIds = { "ability.example-passive" }
})
```

Or add one item as a loadout variant:

```lua
host.register_gameplay_slot_item({
    classId = "heavy",
    slot = "Utility",
    itemId = "ability.example-dash",
    loadoutId = "heavy.example-dash",
    displayName = "Example Dash",
    baseLoadoutId = "heavy.stock"
})
```

## Lua Primary Weapon Behaviors

Lua can register brand-new primary weapon behavior for plugins. This is the mod
path for weapons that do not reuse stock pellet, rocket, flame, mine, grenade,
minigun, rifle, medigun, revolver, or blade behavior.

The flow is:

1. Register a primary weapon behavior during `initialize`.
2. Register a weapon item whose `behaviorId` points at that behavior.
3. Attach the item through a loadout or slot item registration.
4. Implement `plugin.on_gameplay_primary_weapon_execute(e)`.

Example:

```lua
local plugin = {}
local behavior_id = "plugin.example.nailgun.primary"
local nailgun_item_id = "weapon.example-nailgun"
local toggle_item_id = "ability.example-nailgun-toggle"

function plugin.initialize(host)
    plugin.host = host

    host.register_gameplay_primary_weapon_behavior({
        behaviorId = behavior_id,
        fireSoundName = "NeedleSnd"
    })

    host.register_gameplay_weapon_item({
        itemId = nailgun_item_id,
        displayName = "Nailgun",
        slot = "Secondary",
        behaviorId = behavior_id,
        ammo = {
            maxAmmo = 30,
            ammoPerUse = 1,
            projectilesPerUse = 1,
            useDelaySourceTicks = 4,
            reloadSourceTicks = 2,
            minProjectileSpeed = 9
        },
        presentation = {
            hud = {
                displayKind = "ammoPanel",
                stackGroup = "weapon",
                stateProvider = "secondaryAmmo"
            }
        }
    })

    host.register_gameplay_ability({
        itemId = toggle_item_id,
        displayName = "Nailgun",
        slot = "Utility",
        behaviorId = "builtin.ability.soldier_secondary_toggle",
        ability = {
            category = "utility",
            activation = "pressed",
            executorId = "builtin.ability.soldier_secondary_toggle",
            tags = { "weapon_swap" }
        }
    })

    host.register_gameplay_loadout({
        classId = "scout",
        loadoutId = "scout.example-nailgun",
        displayName = "Nailgun",
        primaryItemId = "weapon.scattergun",
        secondaryItemId = nailgun_item_id,
        utilityItemId = toggle_item_id
    })
end

function plugin.on_gameplay_primary_weapon_execute(e)
    if e.behaviorId ~= behavior_id then
        return false
    end

    plugin.host.try_spawn_gameplay_projectile({
        ownerPlayerId = e.playerId,
        kind = "needle",
        x = e.sourceX,
        y = e.sourceY,
        velocityX = e.directionX * e.weapon.minShotSpeed,
        velocityY = e.directionY * e.weapon.minShotSpeed,
        killFeedWeaponSpriteName = e.killFeedWeaponSpriteName
    })

    return { handled = true }
end

return plugin
```

Primary weapon execution events expose `playerId`, `classId`, `team`, `itemId`,
`behaviorId`, `weapon`, `sourceX`, `sourceY`, `aimWorldX`, `aimWorldY`,
`directionX`, `directionY`, `directionRadians`, and
`killFeedWeaponSpriteName`.

Custom primary weapon callbacks run after the normal ammo/cooldown gate. A
custom behavior that returns `false` or `nil` does not fall back to stock pellet
fire.

## Passive Cooldowns

Lua ability cooldowns are ordinary replicated state. If a plugin cooldown needs
to tick down every simulation tick, add a hidden passive ability:

```lua
host.register_gameplay_ability({
    itemId = "ability.example-passive",
    displayName = "Example Passive",
    slot = "Utility",
    behaviorId = "plugin.example.passive",
    ability = {
        category = "passive",
        activation = "passive_tick",
        executorId = "plugin.example.passive",
        tags = { "passive", "cooldown_tick" }
    }
})
```

Attach it through `abilityItemIds` and decrement plugin-owned cooldown state
inside the passive executor.

## Lua Stock Overrides

Plugins can patch existing ability metadata before the runtime registry seals:

```lua
host.override_gameplay_ability("ability.heavy-utility", {
    activation = "pressed",
    parameters = {
        cooldownSeconds = 10,
        movementDurationSeconds = 0.45,
        impulse = 70,
        useMomentum = true
    }
})
```

Patches replace supplied fields. If you supply `parameters` or `tags`, include
the full replacement set you want to keep.

## Lua Executor Operations

Server Lua ability and primary weapon executors can use these bounded
operations:

- `try_apply_gameplay_impulse(playerId, velocityX, velocityY)`
- `try_set_gameplay_ability_cooldown(playerId, cooldownKey, ticks)`
- `try_apply_gameplay_damage(targetPlayerId, amount, attackerPlayerId, weaponSpriteName)`
- `try_apply_gameplay_healing(playerId, amount)`
- `try_apply_gameplay_status_effect(playerId, statusEffectId, ticks, value)`
- `try_spawn_gameplay_projectile(request)`

The executor return value controls dispatcher outcome:

- `true` means handled and input consumed.
- `false` or `nil` means ignored.
- `{ handled = true, consumedInput = true }` gives explicit control.

## Supported Templates

- `Plugins/Templates/ServerLua.GameplayAbility` is the starting point for a Lua
  server ability plugin.
- `Plugins/Templates/ServerLua.PrimaryWeapon` is the starting point for a Lua
  server primary weapon plugin.
