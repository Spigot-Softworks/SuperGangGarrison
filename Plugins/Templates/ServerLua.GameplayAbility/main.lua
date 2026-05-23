local plugin = {}

local plugin_id = "sample.server.lua-gameplay-ability"
local dash_executor_id = "plugin.sample.server.lua-gameplay-ability.force_dash"
local passive_executor_id = "plugin.sample.server.lua-gameplay-ability.cooldown_tick"
local dash_item_id = "ability.sample-force-dash"
local passive_item_id = "ability.sample-force-dash-passive"
local cooldown_key = "sample_force_dash_cooldown"

local function get_number(parameters, key, fallback)
    if parameters == nil or parameters[key] == nil then
        return fallback
    end

    return parameters[key]
end

function plugin.initialize(host)
    plugin.host = host

    host.register_gameplay_ability_executor(dash_executor_id)
    host.register_gameplay_ability_executor(passive_executor_id)

    host.register_gameplay_ability({
        itemId = dash_item_id,
        displayName = "Sample Force Dash",
        slot = "Utility",
        behaviorId = dash_executor_id,
        ability = {
            category = "utility",
            activation = "pressed",
            executorId = dash_executor_id,
            tags = { "sample", "movement", "dash", "cooldown" },
            parameters = {
                cooldownTicks = 180,
                impulseX = 12,
                impulseY = -4
            }
        },
        presentation = {
            hud = {
                displayKind = "meter",
                stackGroup = "ability",
                order = 90,
                stateProvider = "abilityCooldown",
                stateOwner = plugin_id,
                cooldownKey = cooldown_key,
                maxCooldown = 180,
                hideWhenUnavailable = true
            }
        }
    })

    host.register_gameplay_ability({
        itemId = passive_item_id,
        displayName = "Sample Force Dash Passive",
        slot = "Utility",
        behaviorId = passive_executor_id,
        ability = {
            category = "passive",
            activation = "passive_tick",
            executorId = passive_executor_id,
            tags = { "sample", "passive", "cooldown_tick" }
        },
        presentation = {}
    })

    host.register_gameplay_loadout({
        classId = "heavy",
        loadoutId = "heavy.sample-force-dash",
        displayName = "Sample Force Dash",
        primaryItemId = "weapon.minigun",
        secondaryItemId = "ability.heavy-sandvich",
        utilityItemId = dash_item_id,
        abilityItemIds = { passive_item_id }
    })

    host.log("Lua gameplay ability template registered " .. dash_item_id)
end

function plugin.on_gameplay_ability_execute(e)
    if e.executorId == dash_executor_id then
        local cooldown = plugin.host.get_player_replicated_state_int(e.playerId, plugin_id, cooldown_key) or 0
        if cooldown > 0 then
            return { handled = false, consumedInput = true }
        end

        local cooldown_ticks = get_number(e.parameters, "cooldownTicks", 180)
        local impulse_x = get_number(e.parameters, "impulseX", 12)
        local impulse_y = get_number(e.parameters, "impulseY", -4)

        plugin.host.try_apply_gameplay_impulse(e.playerId, impulse_x, impulse_y)
        plugin.host.try_set_gameplay_ability_cooldown(e.playerId, cooldown_key, cooldown_ticks)
        return { handled = true, consumedInput = true }
    end

    if e.executorId == passive_executor_id then
        local cooldown = plugin.host.get_player_replicated_state_int(e.playerId, plugin_id, cooldown_key) or 0
        if cooldown > 0 then
            plugin.host.try_set_gameplay_ability_cooldown(e.playerId, cooldown_key, cooldown - 1)
        end

        return { handled = true, consumedInput = false }
    end

    return false
end

return plugin
