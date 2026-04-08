local plugin = {}

local help_line = "[GT] commands | !gt_help | !gt_status | !gt_cvars [filter] | !gt_cvar <name> [value] | !gt_say <text> | !gt_kick <slot> [reason] | !gt_map <map> [area] | !gt_nextmap <map> [area] | !gt_auth <password> | !gt_logout | !gt_adminmenu"

local function trim(text)
    local normalized = (text or ""):gsub("^%s+", "")
    normalized = normalized:gsub("%s+$", "")
    return normalized
end

local function starts_with(text, prefix)
    return text:sub(1, #prefix) == prefix
end

local function split_first_word(text)
    local normalized = trim(text)
    local space_index = normalized:find("%s")
    if space_index == nil then
        return normalized, ""
    end

    return normalized:sub(1, space_index - 1), trim(normalized:sub(space_index + 1))
end

local function send_private(slot, text)
    plugin.host.send_system_message(slot, text)
end

local function send_private_lines(slot, lines)
    for _, line in ipairs(lines) do
        send_private(slot, line)
    end
end

local function parse_command(text)
    local normalized = trim(text)
    if normalized == "" then
        return nil, nil
    end

    if string.lower(normalized) == "!gt" then
        return "help", ""
    end

    if not starts_with(string.lower(normalized), "!gt_") then
        return nil, nil
    end

    local command_name, arguments = split_first_word(normalized:sub(5))
    if command_name == "" then
        return "help", ""
    end

    return string.lower(command_name), arguments
end

local function parse_slot(text)
    local slot = tonumber(trim(text))
    if slot == nil or slot < 1 or slot > 255 or slot ~= math.floor(slot) then
        return nil
    end

    return slot
end

local function parse_slot_and_optional_argument(arguments)
    local slot_text, remainder = split_first_word(arguments)
    local slot = parse_slot(slot_text)
    if slot == nil then
        return nil, nil
    end

    return slot, remainder
end

local function parse_map_arguments(arguments)
    local level_name, area_text = split_first_word(arguments)
    level_name = trim(level_name)
    area_text = trim(area_text)
    if level_name == "" then
        return nil, nil
    end

    local area_index = 1
    if area_text ~= "" then
        local maybe_area = tonumber(area_text)
        if maybe_area == nil or maybe_area < 1 or maybe_area ~= math.floor(maybe_area) then
            return nil, nil
        end

        area_index = maybe_area
    end

    return level_name, area_index
end

local function handle_help(event)
    send_private(event.slot, help_line)
    return true
end

local function handle_status(context, event)
    local summary = plugin.host.get_admin_summary()
    send_private(
        event.slot,
        "[GT] status | server=" .. summary.serverName
            .. " | map=" .. summary.levelName
            .. " area " .. tostring(summary.mapAreaIndex)
            .. "/" .. tostring(summary.mapAreaCount)
            .. " | mode=" .. summary.gameMode
            .. " | phase=" .. summary.matchPhase)
    send_private(
        event.slot,
        "[GT] players | total=" .. tostring(summary.playerCount)
            .. " | active=" .. tostring(summary.activePlayerCount)
            .. " | spectators=" .. tostring(summary.spectatorCount)
            .. " | authorized=" .. tostring(summary.authorizedPlayerCount)
            .. " | score=" .. tostring(summary.redCaps)
            .. "-" .. tostring(summary.blueCaps))
    send_private(
        event.slot,
        "[GT] admin | identity=" .. context.identity.displayName
            .. " | authority=" .. context.identity.authority
            .. " | timers=" .. tostring(summary.scheduledTaskCount)
            .. " | uptime=" .. string.format("%.0fs", tonumber(summary.uptimeSeconds) or 0))
    return true
end

local function handle_cvars(event, arguments)
    local filter = trim(arguments)
    local result = plugin.host.find_cvars(filter, 12)
    local count = tonumber(result.count) or 0
    if count == 0 then
        send_private(event.slot, "[GT] cvars | count=0 | filter=" .. string.lower(filter))
        return true
    end

    send_private(event.slot, "[GT] cvars | count=" .. tostring(count))
    for index = 1, count do
        local cvar = result.items[index]
        if cvar == nil then
            break
        end

        local bounds = ""
        if cvar.minimumNumericValue ~= nil or cvar.maximumNumericValue ~= nil then
            local min_value = cvar.minimumNumericValue ~= nil and tostring(cvar.minimumNumericValue) or "-"
            local max_value = cvar.maximumNumericValue ~= nil and tostring(cvar.maximumNumericValue) or "-"
            bounds = " | bounds=" .. min_value .. ".." .. max_value
        end

        send_private(
            event.slot,
            "[GT] cvar | name=" .. cvar.name
                .. " | value=" .. tostring(cvar.currentValue)
                .. " | type=" .. tostring(cvar.valueType)
                .. " | default=" .. tostring(cvar.defaultValue)
                .. " | flags=" .. (cvar.isReadOnly and "readonly" or "mutable")
                .. "," .. (cvar.isProtected and "protected" or "public")
                .. bounds)
    end
    return true
end

local function handle_cvar(event, arguments)
    local name, value = split_first_word(arguments)
    if name == "" then
        send_private(event.slot, "[GT] usage: !gt_cvar <name> [value]")
        return true
    end

    local cvar = plugin.host.get_cvar(name)
    if cvar == nil then
        send_private(event.slot, "[GT] unknown cvar \"" .. name .. "\".")
        return true
    end

    if value == "" then
        send_private(
            event.slot,
            "[GT] cvar | name=" .. cvar.name
                .. " | value=" .. tostring(cvar.currentValue)
                .. " | default=" .. tostring(cvar.defaultValue)
                .. " | type=" .. tostring(cvar.valueType)
                .. " | protected=" .. (cvar.isProtected and "yes" or "no")
                .. " | readonly=" .. (cvar.isReadOnly and "yes" or "no"))
        return true
    end

    if not plugin.host.set_cvar(name, value) then
        local updated = plugin.host.get_cvar(name) or cvar
        send_private(event.slot, "[GT] unable to set cvar \"" .. name .. "\".")
        send_private(
            event.slot,
            "[GT] cvar | name=" .. updated.name
                .. " | value=" .. tostring(updated.currentValue)
                .. " | type=" .. tostring(updated.valueType)
                .. " | readonly=" .. (updated.isReadOnly and "yes" or "no"))
        return true
    end

    local updated = plugin.host.get_cvar(name) or cvar
    if updated.isProtected then
        send_private(event.slot, "[GT] cvar " .. updated.name .. " updated.")
    else
        send_private(event.slot, "[GT] cvar " .. updated.name .. " set to " .. tostring(updated.currentValue) .. ".")
    end

    return true
end

local function handle_say(event, arguments)
    if trim(arguments) == "" then
        send_private(event.slot, "[GT] usage: !gt_say <text>")
        return true
    end

    plugin.host.broadcast_system_message(arguments)
    send_private(event.slot, "[GT] system message sent.")
    return true
end

local function handle_kick(event, arguments)
    local slot, reason = parse_slot_and_optional_argument(arguments)
    if slot == nil then
        send_private(event.slot, "[GT] usage: !gt_kick <slot> [reason]")
        return true
    end

    local final_reason = trim(reason) ~= "" and trim(reason) or "Kicked by admin."
    if plugin.host.try_disconnect(slot, final_reason) then
        send_private(event.slot, "[GT] kicked slot " .. tostring(slot) .. ".")
    else
        send_private(event.slot, "[GT] no client at slot " .. tostring(slot) .. ".")
    end

    return true
end

local function handle_map(event, arguments)
    local level_name, area_index = parse_map_arguments(arguments)
    if level_name == nil then
        send_private(event.slot, "[GT] usage: !gt_map <map> [area]")
        return true
    end

    if plugin.host.try_change_map(level_name, area_index, false) then
        send_private(event.slot, "[GT] changed map to " .. level_name .. " area " .. tostring(area_index) .. ".")
    else
        send_private(event.slot, "[GT] unable to change map to " .. level_name .. " area " .. tostring(area_index) .. ".")
    end

    return true
end

local function handle_nextmap(event, arguments)
    local level_name, area_index = parse_map_arguments(arguments)
    if level_name == nil then
        send_private(event.slot, "[GT] usage: !gt_nextmap <map> [area]")
        return true
    end

    if plugin.host.try_set_next_round_map(level_name, area_index) then
        send_private(event.slot, "[GT] next map set to " .. level_name .. " area " .. tostring(area_index) .. ".")
    else
        send_private(event.slot, "[GT] unable to set next map to " .. level_name .. " area " .. tostring(area_index) .. ".")
    end

    return true
end

local function handle_admin_menu(event)
    send_private(event.slot, "[GT] admin menu is not available yet.")
    return true
end

function plugin.initialize(host)
    plugin.host = host
    host.log("GarrisonTools initialized")
end

function plugin.shutdown()
end

function plugin.try_handle_chat_message(context, e)
    local command_name, arguments = parse_command(e.text)
    if command_name == nil then
        return false
    end

    if not context.isAuthenticatedAdmin then
        send_private(e.slot, "[GT] admin authentication required.")
        return true
    end

    if command_name == "help" then
        return handle_help(e)
    elseif command_name == "status" then
        return handle_status(context, e)
    elseif command_name == "cvars" then
        return handle_cvars(e, arguments)
    elseif command_name == "cvar" then
        return handle_cvar(e, arguments)
    elseif command_name == "say" then
        return handle_say(e, arguments)
    elseif command_name == "kick" then
        return handle_kick(e, arguments)
    elseif command_name == "map" then
        return handle_map(e, arguments)
    elseif command_name == "nextmap" then
        return handle_nextmap(e, arguments)
    elseif command_name == "adminmenu" then
        return handle_admin_menu(e)
    end

    return false
end

return plugin
