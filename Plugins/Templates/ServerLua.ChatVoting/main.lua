local plugin = {}

local default_config = {
    enabled = true,
    preservePlayerStats = false
}

local config = default_config

local function get_command_slot(context)
    local identity = context ~= nil and context.identity or nil
    if identity == nil then
        return nil
    end

    return identity.sourceSlot or identity.SourceSlot or identity.source_slot
end

local function register_restart_vote(host)
    return host.register_vote_kind({
        id = "restart-map",
        displayName = "Restart Current Map",
        description = "Reload the current map and area.",
        targetKind = "None",
        validate = function(_)
            local state = host.get_server_state()
            if state.levelName == nil or state.levelName == "" then
                return {
                    accepted = false,
                    error = "No active map can be restarted."
                }
            end

            return {
                accepted = true,
                subject = "restart " .. tostring(state.levelName)
            }
        end,
        apply = function(_)
            local state = host.get_server_state()
            return host.try_change_map(
                state.levelName,
                state.mapAreaIndex,
                config.preservePlayerStats)
        end
    })
end

local function register_restart_alias(host)
    host.register_command({
        name = "voterestart",
        usage = "!voterestart",
        handler = function(context)
            local slot = get_command_slot(context)
            if slot == nil then
                return "This command is only available from player chat."
            end

            if host.try_start_vote("restart-map", slot, "") then
                return nil
            end

            return "The restart vote could not be started. Use !votes for current status."
        end
    })
end

function plugin.initialize(host)
    plugin.host = host
    config = host.load_json_config("chat-voting.json", default_config)
    if config.enabled == nil then
        config.enabled = default_config.enabled
    end
    if config.preservePlayerStats == nil then
        config.preservePlayerStats = default_config.preservePlayerStats
    end
    host.save_json_config("chat-voting.json", config)

    if config.enabled ~= true then
        host.log("Native restart vote is disabled")
        return
    end

    if not register_restart_vote(host) then
        error("Native restart vote registration was rejected")
    end

    register_restart_alias(host)
    host.log("Native restart vote registered")
end

function plugin.shutdown()
end

return plugin
