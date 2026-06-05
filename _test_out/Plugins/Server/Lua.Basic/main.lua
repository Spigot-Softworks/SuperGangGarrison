local plugin = {}

function plugin.initialize(host)
    plugin.host = host
    local manifest = host.get_manifest()
    host.log("Initialized Lua plugin " .. manifest.displayName .. " (" .. manifest.version .. ")")
end

function plugin.shutdown()
    if plugin.host ~= nil then
        plugin.host.log("Shutting down Lua plugin")
    end
end

function plugin.on_server_started()
    local state = plugin.host.get_server_state()
    plugin.host.log("Lua host ready on map " .. state.levelName .. " with mode " .. state.gameMode)
end

function plugin.on_player_joined(e)
    plugin.host.log("Player joined: " .. e.playerName .. " slot=" .. tostring(e.slot))
end

function plugin.on_round_ended(e)
    local winner = e.winnerTeam or "None"
    plugin.host.log("Round ended. Winner=" .. winner .. " red=" .. tostring(e.redCaps) .. " blue=" .. tostring(e.blueCaps))
end

return plugin
