local plugin = {}

local plugin_id = "sample.server.lua-primary-weapon"
local nailgun_behavior_id = "plugin.sample.server.lua-primary-weapon.nailgun"
local nailgun_item_id = "weapon.sample-nailgun"
local toggle_item_id = "ability.sample-nailgun-toggle"

function plugin.initialize(host)
    plugin.host = host

    host.register_gameplay_primary_weapon_behavior({
        behaviorId = nailgun_behavior_id,
        fireSoundName = "NeedleSnd"
    })

    host.register_gameplay_weapon_item({
        itemId = nailgun_item_id,
        displayName = "Sample Nailgun",
        slot = "Secondary",
        behaviorId = nailgun_behavior_id,
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
        displayName = "Sample Nailgun",
        slot = "Utility",
        behaviorId = "builtin.ability.soldier_secondary_toggle",
        ability = {
            category = "utility",
            activation = "pressed",
            executorId = "builtin.ability.soldier_secondary_toggle",
            tags = { "sample", "weapon_swap" }
        },
        presentation = {
            hud = {
                displayKind = "prompt",
                stackGroup = "ability",
                order = 90,
                stateProvider = "none",
                hideWhenUnavailable = true
            }
        }
    })

    host.register_gameplay_loadout({
        classId = "scout",
        loadoutId = "scout.sample-nailgun",
        displayName = "Sample Nailgun",
        primaryItemId = "weapon.scattergun",
        secondaryItemId = nailgun_item_id,
        utilityItemId = toggle_item_id
    })

    host.log("Lua primary weapon template registered " .. nailgun_item_id)
end

function plugin.on_gameplay_primary_weapon_execute(e)
    if e.behaviorId ~= nailgun_behavior_id then
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
