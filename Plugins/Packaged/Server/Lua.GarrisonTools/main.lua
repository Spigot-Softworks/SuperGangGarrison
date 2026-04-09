local plugin = {}

local target_help_line = "[GT] targets | name | #userid | @me | @all | @alive | @dead | @red | @blue"
local seffect_help_line = "[GT] seffects | blind | earthquake | scale | clear"
local default_ban_minutes = 60
local default_burn_seconds = 10.0
local help_page_size = 6

local default_config = {
    clientEffectsPluginId = "open-garrison.client.lua-garrison-tools-effects",
    blind = {
        defaultSeconds = 8.0,
        alpha = 220,
        innerRadiusPixels = 28
    },
    earthquake = {
        defaultSeconds = 6.0,
        amplitude = 10.0,
        frequency = 18.0
    },
    scale = {
        defaultSeconds = 10.0,
        defaultValue = 0.5,
        minValue = 0.25,
        maxValue = 4.0
    }
}

local config = default_config
local active_effects = {}
local command_categories = {
    "Reference",
    "Communication",
    "Player Control",
    "Match Control",
    "Effects",
    "Session"
}
local command_specs = {
    { name = "help", category = "Reference", usage = "!gt_help [page|search]", summary = "List commands, pages, or matching search results.", keywords = "commands search page docs", detail = "Use a page number for paged output or a search term like cvar or ban." },
    { name = "status", category = "Reference", usage = "!gt_status", summary = "Show server, admin, and player status details.", keywords = "players userid roster info" },
    { name = "cvars", category = "Reference", usage = "!gt_cvars [filter]", summary = "List server cvars, optionally filtered by text.", keywords = "config variables settings server" },
    { name = "cvar", category = "Reference", usage = "!gt_cvar <name> [value]", summary = "Read or update a single cvar.", keywords = "config variable set get protect server" },
    { name = "adminmenu", category = "Reference", usage = "!gt_adminmenu", summary = "Show the categorized admin command catalog.", keywords = "menu categories catalog ui" },
    { name = "say", category = "Communication", usage = "!gt_say <text>", summary = "Broadcast a server chat message.", keywords = "broadcast chat message announce" },
    { name = "psay", category = "Communication", usage = "!gt_psay <target> <message>", summary = "Send a private admin message to one target.", keywords = "private whisper tell target", usesTargets = true },
    { name = "kick", category = "Player Control", usage = "!gt_kick <target> [reason]", summary = "Kick one target from the server.", keywords = "disconnect remove player", usesTargets = true },
    { name = "ban", category = "Player Control", usage = "!gt_ban <target> [minutes|0] [reason]", summary = "Ban an active target by identity, 60 minutes by default.", keywords = "ban player timeout permanent", usesTargets = true },
    { name = "banip", category = "Player Control", usage = "!gt_banip <target|ip> [minutes|0] [reason]", summary = "Ban by target endpoint or raw IP with high-trust authority.", keywords = "ban address endpoint ip timeout permanent", usesTargets = true },
    { name = "unban", category = "Player Control", usage = "!gt_unban <ip>", summary = "Remove an IP ban.", keywords = "unban pardon address ip" },
    { name = "slay", category = "Player Control", usage = "!gt_slay <target>", summary = "Kill one or more live targets.", keywords = "kill suicide eliminate", usesTargets = true },
    { name = "burn", category = "Player Control", usage = "!gt_burn <target> [time]", summary = "Ignite one or more live targets for a duration.", keywords = "ignite fire afterburn", usesTargets = true },
    { name = "gag", category = "Player Control", usage = "!gt_gag <target>", summary = "Toggle chat gagging for one target.", keywords = "mute silence chat", usesTargets = true },
    { name = "rename", category = "Player Control", usage = "!gt_rename <target> <name>", summary = "Rename one target.", keywords = "name alias nick", usesTargets = true },
    { name = "map", category = "Match Control", usage = "!gt_map <map> [area]", summary = "Change the current map.", keywords = "level rotation change round" },
    { name = "nextmap", category = "Match Control", usage = "!gt_nextmap <map> [area]", summary = "Set the next-round map.", keywords = "level rotation next round future" },
    { name = "seffect", category = "Effects", usage = "!gt_seffect <effect> <target> [time]", summary = "Apply, scale, or clear bundled timed effects on targets.", keywords = "blind earthquake scale clear visual fx", usesTargets = true, showSeffectHelp = true },
    { name = "auth", category = "Session", usage = "!gt_auth <password>", summary = "Authenticate this admin session.", keywords = "login password rcon session" },
    { name = "logout", category = "Session", usage = "!gt_logout", summary = "End this admin session.", keywords = "log out unauthenticate session" },
}

local function trim(text)
    local normalized = (text or ""):gsub("^%s+", "")
    normalized = normalized:gsub("%s+$", "")
    return normalized
end

local function starts_with(text, prefix)
    return text:sub(1, #prefix) == prefix
end

local function normalize_search_text(text)
    return string.lower(trim(text))
end

local function normalize_command_lookup(text)
    local normalized = normalize_search_text(text)
    normalized = normalized:gsub("^!+", "")
    if starts_with(normalized, "gt_") then
        normalized = normalized:sub(4)
    end
    return normalized
end

local function split_first_word(text)
    local normalized = trim(text)
    local space_index = normalized:find("%s")
    if space_index == nil then
        return normalized, ""
    end

    return normalized:sub(1, space_index - 1), trim(normalized:sub(space_index + 1))
end

local function get_category_order(category_name)
    for index, category in ipairs(command_categories) do
        if category == category_name then
            return index
        end
    end

    return #command_categories + 1
end

local function get_sorted_command_specs()
    local sorted = {}
    for _, spec in ipairs(command_specs) do
        table.insert(sorted, spec)
    end

    table.sort(sorted, function(left, right)
        local left_order = get_category_order(left.category)
        local right_order = get_category_order(right.category)
        if left_order ~= right_order then
            return left_order < right_order
        end

        return left.name < right.name
    end)

    return sorted
end

local function find_command_spec_by_name(name)
    local normalized = normalize_command_lookup(name)
    for _, spec in ipairs(command_specs) do
        if spec.name == normalized then
            return spec
        end
    end

    return nil
end

local function command_matches_search(spec, search_text)
    local normalized = normalize_search_text(search_text)
    if normalized == "" then
        return true
    end

    local haystacks = {
        spec.name,
        spec.category,
        spec.usage,
        spec.summary,
        spec.keywords or "",
        spec.detail or "",
    }
    for _, value in ipairs(haystacks) do
        if string.find(string.lower(value), normalized, 1, true) ~= nil then
            return true
        end
    end

    return false
end

local function find_matching_command_specs(search_text)
    local matches = {}
    for _, spec in ipairs(get_sorted_command_specs()) do
        if command_matches_search(spec, search_text) then
            table.insert(matches, spec)
        end
    end

    return matches
end

local function send_command_summary_line(slot, spec)
    send_private(slot, "[GT] command | category=" .. spec.category .. " | " .. spec.usage .. " | " .. spec.summary)
end

local function send_command_detail(slot, spec)
    send_command_summary_line(slot, spec)
    if spec.detail ~= nil and spec.detail ~= "" then
        send_private(slot, "[GT] details | " .. spec.detail)
    end
    if spec.usesTargets then
        send_private(slot, target_help_line)
    end
    if spec.showSeffectHelp then
        send_private(slot, seffect_help_line)
    end
end

local function build_commands_by_category()
    local grouped = {}
    for _, category in ipairs(command_categories) do
        grouped[category] = {}
    end

    for _, spec in ipairs(get_sorted_command_specs()) do
        grouped[spec.category] = grouped[spec.category] or {}
        table.insert(grouped[spec.category], spec)
    end

    return grouped
end

local function clamp(value, minimum, maximum)
    if value < minimum then
        return minimum
    end
    if value > maximum then
        return maximum
    end
    return value
end

local function format_seconds(seconds)
    local rounded = math.floor(seconds + 0.5)
    if math.abs(seconds - rounded) <= 0.0001 then
        return tostring(rounded)
    end

    return string.format("%.1f", seconds)
end

local function looks_like_ip_literal(text)
    local normalized = trim(text)
    if normalized == "" then
        return false
    end

    return normalized:find("[.:]") ~= nil
        and normalized:match("^[%x%.:]+$") ~= nil
end

local function can_ban_arbitrary_ip(context)
    local authority = tostring(context.identity and context.identity.authority or "")
    return authority == "RconSession"
        or authority == "HostConsole"
        or authority == "AdminPipe"
end

local function parse_ban_minutes_and_reason(text)
    local normalized = trim(text)
    if normalized == "" then
        return default_ban_minutes, "", nil
    end

    local maybe_minutes, remainder = split_first_word(normalized)
    local parsed = tonumber(maybe_minutes)
    if parsed == nil then
        return default_ban_minutes, normalized, nil
    end

    if parsed < 0 or parsed ~= math.floor(parsed) then
        return nil, nil, "Ban time must be a non-negative whole number of minutes."
    end

    return clamp(parsed, 0, 5256000), trim(remainder), nil
end

local function format_ban_duration(minutes)
    if minutes == 0 then
        return "permanently"
    end

    return "for " .. tostring(minutes) .. " minute(s)"
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

local function parse_target_and_optional_argument(arguments)
    local target_text, remainder = split_first_word(arguments)
    target_text = trim(target_text)
    if target_text == "" then
        return nil, nil
    end

    return target_text, remainder
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

local function resolve_targets(source_slot, target_text, options)
    local allow_multiple = true
    if options ~= nil and options.allowMultiple ~= nil then
        allow_multiple = options.allowMultiple
    end

    local resolved = plugin.host.resolve_targets(target_text, {
        sourceSlot = source_slot,
        allowMultiple = allow_multiple,
        requireAlive = options ~= nil and options.requireAlive or false,
        includeSpectators = options == nil or options.includeSpectators ~= false,
    })

    if resolved == nil or not resolved.success then
        return nil, (resolved ~= nil and resolved.errorMessage) or ("Unable to resolve target \"" .. tostring(target_text) .. "\".")
    end

    return resolved.targets, nil
end

local function resolve_single_target(source_slot, target_text, options)
    local resolved_options = {
        allowMultiple = false,
        requireAlive = options ~= nil and options.requireAlive or false,
        includeSpectators = options == nil or options.includeSpectators ~= false,
    }
    local targets, error_text = resolve_targets(source_slot, target_text, resolved_options)
    if targets == nil then
        return nil, error_text
    end

    return targets[1], nil
end

local function describe_player(player)
    local team_name = player.team or (player.isSpectator and "Spectator" or "Unassigned")
    local class_name = player.playerClass or "-"
    local state_name = player.isSpectator and "spectator" or (player.isAlive and "alive" or "dead")
    return "[GT] player | userid=" .. tostring(player.userId)
        .. " | slot=" .. tostring(player.slot)
        .. " | name=" .. player.name
        .. " | team=" .. tostring(team_name)
        .. " | class=" .. tostring(class_name)
        .. " | state=" .. state_name
        .. " | gagged=" .. (player.isGagged and "yes" or "no")
        .. " | auth=" .. (player.isAuthorized and "yes" or "pending")
end

local function normalize_effect_id(effect_text)
    local normalized = string.lower(trim(effect_text))
    if normalized == "blind" then
        return "blind"
    end
    if normalized == "earthquake" or normalized == "quake" then
        return "earthquake"
    end
    if normalized == "scale" or normalized == "size" then
        return "scale"
    end
    if normalized == "clear" or normalized == "off" or normalized == "remove" then
        return "clear"
    end

    return nil
end

local function load_config()
    local loaded = plugin.host.load_json_config("seffects.json", default_config)
    local normalized = {
        clientEffectsPluginId = trim(loaded.clientEffectsPluginId or default_config.clientEffectsPluginId),
        blind = {
            defaultSeconds = clamp(tonumber(loaded.blind and loaded.blind.defaultSeconds) or default_config.blind.defaultSeconds, 0.1, 600.0),
            alpha = math.floor(clamp(tonumber(loaded.blind and loaded.blind.alpha) or default_config.blind.alpha, 0, 255)),
            innerRadiusPixels = math.floor(clamp(tonumber(loaded.blind and loaded.blind.innerRadiusPixels) or default_config.blind.innerRadiusPixels, 6, 240)),
        },
        earthquake = {
            defaultSeconds = clamp(tonumber(loaded.earthquake and loaded.earthquake.defaultSeconds) or default_config.earthquake.defaultSeconds, 0.1, 600.0),
            amplitude = clamp(tonumber(loaded.earthquake and loaded.earthquake.amplitude) or default_config.earthquake.amplitude, 0.0, 64.0),
            frequency = clamp(tonumber(loaded.earthquake and loaded.earthquake.frequency) or default_config.earthquake.frequency, 0.1, 60.0),
        },
        scale = {
            defaultSeconds = clamp(tonumber(loaded.scale and loaded.scale.defaultSeconds) or default_config.scale.defaultSeconds, 0.1, 600.0),
            minValue = clamp(tonumber(loaded.scale and loaded.scale.minValue) or default_config.scale.minValue, default_config.scale.minValue, default_config.scale.maxValue),
            maxValue = clamp(tonumber(loaded.scale and loaded.scale.maxValue) or default_config.scale.maxValue, default_config.scale.minValue, default_config.scale.maxValue),
            defaultValue = tonumber(loaded.scale and loaded.scale.defaultValue) or default_config.scale.defaultValue,
        }
    }

    if normalized.scale.minValue > normalized.scale.maxValue then
        normalized.scale.minValue = default_config.scale.minValue
        normalized.scale.maxValue = default_config.scale.maxValue
    end

    normalized.scale.defaultValue = clamp(normalized.scale.defaultValue, normalized.scale.minValue, normalized.scale.maxValue)

    if normalized.clientEffectsPluginId == "" then
        normalized.clientEffectsPluginId = default_config.clientEffectsPluginId
    end

    plugin.host.save_json_config("seffects.json", normalized)
    return normalized
end

local function create_effect_key(slot, effect_id)
    return tostring(slot) .. "|" .. effect_id
end

local function build_effect_apply_payload(effect_id, duration_seconds)
    if effect_id == "blind" then
        return effect_id
            .. "|"
            .. string.format("%.3f", duration_seconds)
            .. "|"
            .. tostring(config.blind.alpha or default_config.blind.alpha)
            .. "|"
            .. tostring(config.blind.innerRadiusPixels or default_config.blind.innerRadiusPixels)
    end

    if effect_id == "earthquake" then
        return effect_id
            .. "|"
            .. string.format("%.3f", duration_seconds)
            .. "|"
            .. string.format("%.3f", config.earthquake.amplitude or default_config.earthquake.amplitude)
            .. "|"
            .. string.format("%.3f", config.earthquake.frequency or default_config.earthquake.frequency)
    end

    return effect_id
end

local function send_effect_apply(slot, effect_id, duration_seconds)
    plugin.host.send_message_to_client(
        slot,
        config.clientEffectsPluginId,
        "seffect.apply",
        build_effect_apply_payload(effect_id, duration_seconds))
end

local function send_effect_clear(slot, effect_id)
    plugin.host.send_message_to_client(
        slot,
        config.clientEffectsPluginId,
        "seffect.clear",
        effect_id or "all")
end

local function cancel_effect_timer(timer_id)
    if timer_id ~= nil and timer_id ~= "" then
        plugin.host.cancel_scheduled_task(timer_id)
    end
end

local function collect_active_effect_keys(predicate)
    local keys = {}
    for key, entry in pairs(active_effects) do
        if predicate == nil or predicate(entry) then
            table.insert(keys, key)
        end
    end
    return keys
end

local function clear_effect_entry(slot, effect_id, notify_client)
    local effect_key = create_effect_key(slot, effect_id)
    local entry = active_effects[effect_key]
    if entry == nil then
        return false
    end

    cancel_effect_timer(entry.timerId)
    if effect_id == "scale" and entry.restoreScale ~= nil then
        plugin.host.try_set_player_scale(slot, entry.restoreScale)
    end
    active_effects[effect_key] = nil
    if notify_client and (effect_id == "blind" or effect_id == "earthquake") then
        send_effect_clear(slot, effect_id)
    end

    return true
end

local function clear_effects_for_slot(slot, notify_client)
    local keys = collect_active_effect_keys(function(entry)
        return entry.slot == slot
    end)
    local cleared_count = 0
    for _, key in ipairs(keys) do
        local entry = active_effects[key]
        if entry ~= nil then
            cancel_effect_timer(entry.timerId)
            if entry.effectId == "scale" and entry.restoreScale ~= nil then
                plugin.host.try_set_player_scale(entry.slot, entry.restoreScale)
            end
            active_effects[key] = nil
            if notify_client and (entry.effectId == "blind" or entry.effectId == "earthquake") then
                send_effect_clear(entry.slot, entry.effectId)
            end
            cleared_count = cleared_count + 1
        end
    end
    return cleared_count
end

local function clear_effects_for_slot_and_optional_effect(slot, effect_id, notify_client)
    if effect_id ~= nil then
        return clear_effect_entry(slot, effect_id, notify_client) and 1 or 0
    end

    return clear_effects_for_slot(slot, notify_client)
end

local function clear_all_effects(notify_client)
    local keys = collect_active_effect_keys(nil)
    for _, key in ipairs(keys) do
        local entry = active_effects[key]
        if entry ~= nil then
            cancel_effect_timer(entry.timerId)
            if entry.effectId == "scale" and entry.restoreScale ~= nil then
                plugin.host.try_set_player_scale(entry.slot, entry.restoreScale)
            end
            active_effects[key] = nil
            if notify_client and (entry.effectId == "blind" or entry.effectId == "earthquake") then
                send_effect_clear(entry.slot, entry.effectId)
            end
        end
    end
end

local function get_default_effect_duration(effect_id)
    if effect_id == "blind" then
        return config.blind.defaultSeconds
    end
    if effect_id == "earthquake" then
        return config.earthquake.defaultSeconds
    end
    if effect_id == "scale" then
        return config.scale.defaultSeconds
    end
    return nil
end

local function parse_duration_seconds(text, effect_id)
    local normalized = trim(text)
    if normalized == "" then
        return get_default_effect_duration(effect_id), nil
    end

    local parsed = tonumber(normalized)
    if parsed == nil then
        return nil, "Duration must be a number of seconds."
    end

    if parsed <= 0 then
        return nil, "Duration must be greater than zero."
    end

    return clamp(parsed, 0.1, 600.0), nil
end

local function parse_scale_value_and_duration(text)
    local normalized = trim(text)
    if normalized == "" then
        return config.scale.defaultValue, config.scale.defaultSeconds, nil
    end

    local scale_text, remainder = split_first_word(normalized)
    local parsed_scale = tonumber(scale_text)
    if parsed_scale == nil then
        return nil, nil, "Scale must be a number."
    end

    if parsed_scale < config.scale.minValue or parsed_scale > config.scale.maxValue then
        return nil, nil, "Scale must be between " .. tostring(config.scale.minValue) .. " and " .. tostring(config.scale.maxValue) .. "."
    end

    local duration_seconds, duration_error = parse_duration_seconds(remainder, "scale")
    if duration_seconds == nil then
        return nil, nil, duration_error
    end

    return parsed_scale, duration_seconds, nil
end

local function apply_effect_to_slot(slot, effect_id, duration_seconds, parameters)
    clear_effect_entry(slot, effect_id, false)

    local restore_scale = nil
    if effect_id == "scale" then
        restore_scale = parameters ~= nil and parameters.restoreScale or nil
        local next_scale = parameters ~= nil and parameters.scale or config.scale.defaultValue
        if not plugin.host.try_set_player_scale(slot, next_scale) then
            return false
        end
    else
        send_effect_apply(slot, effect_id, duration_seconds)
    end

    local effect_key = create_effect_key(slot, effect_id)
    local timer_id = nil
    timer_id = plugin.host.schedule_once(duration_seconds, function()
        local entry = active_effects[effect_key]
        if entry == nil or entry.timerId ~= timer_id then
            return
        end

        clear_effect_entry(slot, effect_id, true)
    end, "gt_seffect " .. effect_id .. " slot " .. tostring(slot))

    active_effects[effect_key] = {
        slot = slot,
        effectId = effect_id,
        timerId = timer_id,
        restoreScale = restore_scale,
    }

    return true
end

local function handle_help(event, arguments)
    local search_text = trim(arguments)
    local exact_spec = find_command_spec_by_name(search_text)
    if exact_spec ~= nil then
        send_private(event.slot, "[GT] help | match=" .. exact_spec.name)
        send_command_detail(event.slot, exact_spec)
        return true
    end

    local page_number = tonumber(search_text)
    if search_text ~= "" and page_number ~= nil and page_number == math.floor(page_number) then
        local commands = get_sorted_command_specs()
        local total_count = #commands
        local total_pages = math.max(1, math.ceil(total_count / help_page_size))
        local page_index = clamp(page_number, 1, total_pages)
        local start_index = (page_index - 1) * help_page_size + 1
        local end_index = math.min(start_index + help_page_size - 1, total_count)

        send_private(event.slot, "[GT] help | page=" .. tostring(page_index) .. "/" .. tostring(total_pages) .. " | commands=" .. tostring(total_count))
        for index = start_index, end_index do
            send_command_summary_line(event.slot, commands[index])
        end
        send_private(event.slot, "[GT] usage | !gt_help <page> | !gt_help <search>")
        send_private(event.slot, target_help_line)
        return true
    end

    if search_text ~= "" then
        local matches = find_matching_command_specs(search_text)
        if #matches == 0 then
            send_private(event.slot, "[GT] help | search=\"" .. search_text .. "\" | matches=0")
            return true
        end

        send_private(event.slot, "[GT] help | search=\"" .. search_text .. "\" | matches=" .. tostring(#matches))
        for _, spec in ipairs(matches) do
            send_command_summary_line(event.slot, spec)
        end
        if #matches == 1 then
            send_command_detail(event.slot, matches[1])
        end
        return true
    end

    local commands = get_sorted_command_specs()
    local total_pages = math.max(1, math.ceil(#commands / help_page_size))
    send_private(event.slot, "[GT] help | page=1/" .. tostring(total_pages) .. " | commands=" .. tostring(#commands))
    for index = 1, math.min(help_page_size, #commands) do
        send_command_summary_line(event.slot, commands[index])
    end
    send_private(event.slot, "[GT] usage | !gt_help <page> | !gt_help <search>")
    send_private(event.slot, target_help_line)
    return true
end

local function handle_status(context, event)
    local summary = plugin.host.get_admin_summary()
    local players, _ = resolve_targets(event.slot, "@all", { allowMultiple = true, includeSpectators = true })
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
    if players ~= nil then
        local index = 1
        while players[index] ~= nil do
            send_private(event.slot, describe_player(players[index]))
            index = index + 1
        end
    end
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
        send_private(event.slot, "[GT] usage: !gt_cvar protect <name>")
        return true
    end

    if string.lower(name) == "protect" then
        local protected_name = trim(value)
        if protected_name == "" then
            send_private(event.slot, "[GT] usage: !gt_cvar protect <name>")
            return true
        end

        local protected_cvar = plugin.host.protect_cvar(protected_name)
        if protected_cvar == nil then
            send_private(event.slot, "[GT] unable to protect cvar \"" .. protected_name .. "\".")
            return true
        end

        if protected_cvar.success == false then
            send_private(event.slot, "[GT] unable to protect cvar \"" .. protected_name .. "\": " .. tostring(protected_cvar.errorMessage))
            return true
        end

        send_private(event.slot, "[GT] cvar " .. tostring(protected_cvar.name) .. " is now protected.")
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

local function handle_seffect(event, arguments)
    local effect_text, remainder = split_first_word(arguments)
    local effect_id = normalize_effect_id(effect_text)
    if effect_id == nil then
        send_private(event.slot, "[GT] usage: !gt_seffect <effect> <target> [time]")
        send_private(event.slot, "[GT] usage: !gt_seffect scale <target> [scale] [time]")
        send_private(event.slot, seffect_help_line)
        return true
    end

    local target_text, trailing = parse_target_and_optional_argument(remainder)
    if target_text == nil then
        send_private(event.slot, "[GT] usage: !gt_seffect <effect> <target> [time]")
        send_private(event.slot, "[GT] usage: !gt_seffect scale <target> [scale] [time]")
        return true
    end

    local targets, error_text = resolve_targets(event.slot, target_text, {
        allowMultiple = true,
        includeSpectators = true,
    })
    if targets == nil then
        send_private(event.slot, "[GT] " .. error_text)
        return true
    end

    if targets[1] == nil then
        local single_target, single_error_text = resolve_single_target(event.slot, target_text, { includeSpectators = true })
        if single_target == nil then
            send_private(event.slot, "[GT] " .. (single_error_text or ("Unable to resolve target \"" .. target_text .. "\".")))
            return true
        end

        targets = { single_target }
    end

    if effect_id == "clear" then
        local clear_effect_id = nil
        if trim(trailing) ~= "" then
            clear_effect_id = normalize_effect_id(trailing)
            if clear_effect_id == nil or clear_effect_id == "clear" then
                send_private(event.slot, "[GT] clear expects a specific effect or no trailing effect name.")
                return true
            end
        end

        local cleared_count = 0
        local index = 1
        while targets[index] ~= nil do
            local target = targets[index]
            cleared_count = cleared_count + clear_effects_for_slot_and_optional_effect(target.slot, clear_effect_id, true)
            index = index + 1
        end

        if clear_effect_id ~= nil then
            send_private(event.slot, "[GT] cleared " .. clear_effect_id .. " on " .. tostring(cleared_count) .. " active target(s).")
        else
            send_private(event.slot, "[GT] cleared " .. tostring(cleared_count) .. " active effect(s).")
        end
        return true
    end

    local duration_seconds = nil
    local effect_parameters = nil
    if effect_id == "scale" then
        local scale_value, parsed_duration_seconds, scale_error = parse_scale_value_and_duration(trailing)
        if scale_value == nil then
            send_private(event.slot, "[GT] " .. scale_error)
            return true
        end

        duration_seconds = parsed_duration_seconds
        effect_parameters = { scale = scale_value }
    else
        local duration_error = nil
        duration_seconds, duration_error = parse_duration_seconds(trailing, effect_id)
        if duration_seconds == nil then
            send_private(event.slot, "[GT] " .. duration_error)
            return true
        end
    end

    local applied_count = 0
    local index = 1
    while targets[index] ~= nil do
        local target = targets[index]
        local parameters = effect_parameters
        if effect_id == "scale" then
            local active_entry = active_effects[create_effect_key(target.slot, effect_id)]
            parameters = {
                scale = effect_parameters.scale,
                restoreScale = active_entry ~= nil and active_entry.restoreScale or target.playerScale,
            }
        end

        if apply_effect_to_slot(target.slot, effect_id, duration_seconds, parameters) then
            applied_count = applied_count + 1
        end
        index = index + 1
    end

    if effect_id == "scale" then
        send_private(
            event.slot,
            "[GT] applied scale "
                .. tostring(effect_parameters.scale)
                .. " to " .. tostring(applied_count)
                .. " target(s) for " .. format_seconds(duration_seconds) .. "s.")
        return true
    end

    send_private(
        event.slot,
        "[GT] applied " .. effect_id
            .. " to " .. tostring(applied_count)
            .. " target(s) for " .. format_seconds(duration_seconds) .. "s.")
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

local function handle_psay(event, arguments)
    local target_text, message = parse_target_and_optional_argument(arguments)
    if target_text == nil or trim(message) == "" then
        send_private(event.slot, "[GT] usage: !gt_psay <target> <message>")
        return true
    end

    local target, error_text = resolve_single_target(event.slot, target_text, { includeSpectators = true })
    if target == nil then
        send_private(event.slot, "[GT] " .. error_text)
        return true
    end

    plugin.host.send_system_message(target.slot, trim(message))
    send_private(event.slot, "[GT] sent private message to " .. target.name .. " (#" .. tostring(target.userId) .. ").")
    return true
end

local function handle_kick(event, arguments)
    local target_text, reason = parse_target_and_optional_argument(arguments)
    if target_text == nil then
        send_private(event.slot, "[GT] usage: !gt_kick <target> [reason]")
        return true
    end

    local target, error_text = resolve_single_target(event.slot, target_text, { includeSpectators = true })
    if target == nil then
        send_private(event.slot, "[GT] " .. error_text)
        return true
    end

    local final_reason = trim(reason) ~= "" and trim(reason) or "Kicked by admin."
    if plugin.host.try_disconnect(target.slot, final_reason) then
        send_private(event.slot, "[GT] kicked " .. target.name .. " (#" .. tostring(target.userId) .. ", slot " .. tostring(target.slot) .. ").")
    else
        send_private(event.slot, "[GT] no connected client for " .. target_text .. ".")
    end

    return true
end

local function handle_ban(event, arguments)
    local target_text, remainder = parse_target_and_optional_argument(arguments)
    if target_text == nil then
        send_private(event.slot, "[GT] usage: !gt_ban <target> [minutes|0] [reason]")
        return true
    end

    local duration_minutes, reason, parse_error = parse_ban_minutes_and_reason(remainder)
    if duration_minutes == nil then
        send_private(event.slot, "[GT] " .. parse_error)
        return true
    end

    local target, error_text = resolve_single_target(event.slot, target_text, { includeSpectators = true })
    if target == nil then
        send_private(event.slot, "[GT] " .. error_text)
        return true
    end

    local final_reason = trim(reason) ~= "" and trim(reason) or "Banned by admin."
    local result = plugin.host.try_ban_player(target.slot, duration_minutes, final_reason)
    if result ~= nil and result.success then
        send_private(
            event.slot,
            "[GT] banned " .. target.name
                .. " (#" .. tostring(target.userId) .. ", ip " .. tostring(result.address) .. ") "
                .. format_ban_duration(duration_minutes) .. ".")
    else
        send_private(event.slot, "[GT] " .. ((result and result.errorMessage) or ("Unable to ban " .. target.name .. ".")))
    end

    return true
end

local function handle_banip(context, event, arguments)
    local target_text, remainder = parse_target_and_optional_argument(arguments)
    if target_text == nil then
        send_private(event.slot, "[GT] usage: !gt_banip <target|ip> [minutes|0] [reason]")
        return true
    end

    local duration_minutes, reason, parse_error = parse_ban_minutes_and_reason(remainder)
    if duration_minutes == nil then
        send_private(event.slot, "[GT] " .. parse_error)
        return true
    end

    local final_reason = trim(reason) ~= "" and trim(reason) or "Banned by admin."
    local target, target_error_text = resolve_single_target(event.slot, target_text, { includeSpectators = true })
    if target ~= nil then
        local result = plugin.host.try_ban_player(target.slot, duration_minutes, final_reason)
        if result ~= nil and result.success then
            send_private(
                event.slot,
                "[GT] banned ip " .. tostring(result.address)
                    .. " for " .. target.name
                    .. " (#" .. tostring(target.userId) .. ") "
                    .. format_ban_duration(duration_minutes) .. ".")
        else
            send_private(event.slot, "[GT] " .. ((result and result.errorMessage) or ("Unable to ban " .. target.name .. ".")))
        end
        return true
    end

    if not looks_like_ip_literal(target_text) then
        send_private(event.slot, "[GT] " .. (target_error_text or ("Unable to resolve target \"" .. target_text .. "\".")))
        return true
    end

    if not can_ban_arbitrary_ip(context) then
        send_private(event.slot, "[GT] arbitrary IP bans require rcon access.")
        return true
    end

    local result = plugin.host.try_ban_ip_address(target_text, duration_minutes, final_reason)
    if result ~= nil and result.success then
        send_private(event.slot, "[GT] banned ip " .. tostring(result.address) .. " " .. format_ban_duration(duration_minutes) .. ".")
    else
        send_private(event.slot, "[GT] " .. ((result and result.errorMessage) or ("Unable to ban ip \"" .. target_text .. "\".")))
    end

    return true
end

local function handle_unban(event, arguments)
    local ip_text = trim(arguments)
    if ip_text == "" then
        send_private(event.slot, "[GT] usage: !gt_unban <ip>")
        return true
    end

    local result = plugin.host.try_unban_ip_address(ip_text)
    if result ~= nil and result.success then
        send_private(event.slot, "[GT] unbanned ip " .. tostring(result.address) .. ".")
    else
        send_private(event.slot, "[GT] " .. ((result and result.errorMessage) or ("Unable to unban ip \"" .. ip_text .. "\".")))
    end

    return true
end

local function handle_slay(event, arguments)
    local target_text = trim(arguments)
    if target_text == "" then
        send_private(event.slot, "[GT] usage: !gt_slay <target>")
        return true
    end

    local targets, error_text = resolve_targets(event.slot, target_text, {
        allowMultiple = true,
        requireAlive = true,
        includeSpectators = false,
    })
    if targets == nil then
        send_private(event.slot, "[GT] " .. error_text)
        return true
    end

    local affected = 0
    local index = 1
    while targets[index] ~= nil do
        if plugin.host.try_force_kill(targets[index].slot) then
            affected = affected + 1
        end
        index = index + 1
    end

    if affected > 0 then
        send_private(event.slot, "[GT] slayed " .. tostring(affected) .. " player(s).")
    else
        send_private(event.slot, "[GT] no players were slayed.")
    end

    return true
end

local function parse_burn_duration_seconds(text)
    local normalized = trim(text)
    if normalized == "" then
        return default_burn_seconds, nil
    end

    local parsed = tonumber(normalized)
    if parsed == nil then
        return nil, "Burn time must be a number of seconds."
    end

    if parsed <= 0 then
        return nil, "Burn time must be greater than zero."
    end

    return clamp(parsed, 0.1, 60.0), nil
end

local function handle_burn(event, arguments)
    local target_text, remainder = parse_target_and_optional_argument(arguments)
    if target_text == nil then
        send_private(event.slot, "[GT] usage: !gt_burn <target> [time]")
        return true
    end

    local duration_seconds, duration_error = parse_burn_duration_seconds(remainder)
    if duration_seconds == nil then
        send_private(event.slot, "[GT] " .. duration_error)
        return true
    end

    local targets, error_text = resolve_targets(event.slot, target_text, {
        allowMultiple = true,
        requireAlive = true,
        includeSpectators = false,
    })
    if targets == nil then
        send_private(event.slot, "[GT] " .. error_text)
        return true
    end

    local affected = 0
    local index = 1
    while targets[index] ~= nil do
        if plugin.host.try_ignite_player(targets[index].slot, duration_seconds) then
            affected = affected + 1
        end
        index = index + 1
    end

    if affected > 0 then
        send_private(event.slot, "[GT] ignited " .. tostring(affected) .. " player(s) for " .. format_seconds(duration_seconds) .. "s.")
    else
        send_private(event.slot, "[GT] no players were ignited.")
    end

    return true
end

local function handle_gag(event, arguments)
    local target_text = trim(arguments)
    if target_text == "" then
        send_private(event.slot, "[GT] usage: !gt_gag <target>")
        return true
    end

    local target, error_text = resolve_single_target(event.slot, target_text, { includeSpectators = true })
    if target == nil then
        send_private(event.slot, "[GT] " .. error_text)
        return true
    end

    local new_gag_state = not target.isGagged
    if not plugin.host.try_set_player_gagged(target.slot, new_gag_state) then
        send_private(event.slot, "[GT] unable to update gag state for " .. target.name .. ".")
        return true
    end

    if new_gag_state then
        plugin.host.send_system_message(target.slot, "[GT] You have been gagged.")
        send_private(event.slot, "[GT] gagged " .. target.name .. " (#" .. tostring(target.userId) .. ").")
    else
        plugin.host.send_system_message(target.slot, "[GT] You are no longer gagged.")
        send_private(event.slot, "[GT] ungagged " .. target.name .. " (#" .. tostring(target.userId) .. ").")
    end

    return true
end

local function handle_rename(event, arguments)
    local target_text, new_name = parse_target_and_optional_argument(arguments)
    if target_text == nil or trim(new_name) == "" then
        send_private(event.slot, "[GT] usage: !gt_rename <target> <name>")
        return true
    end

    local target, error_text = resolve_single_target(event.slot, target_text, { includeSpectators = true })
    if target == nil then
        send_private(event.slot, "[GT] " .. error_text)
        return true
    end

    local trimmed_name = trim(new_name)
    if plugin.host.try_set_player_name(target.slot, trimmed_name) then
        send_private(event.slot, "[GT] renamed " .. target.name .. " to " .. trimmed_name .. ".")
    else
        send_private(event.slot, "[GT] unable to rename " .. target.name .. ".")
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
    local grouped = build_commands_by_category()
    send_private(event.slot, "[GT] admin menu | categories=" .. tostring(#command_categories) .. " | commands=" .. tostring(#command_specs))
    for _, category in ipairs(command_categories) do
        local commands = grouped[category]
        if commands ~= nil and commands[1] ~= nil then
            send_private(event.slot, "[GT] category | " .. category .. " | count=" .. tostring(#commands))
            for _, spec in ipairs(commands) do
                send_command_summary_line(event.slot, spec)
            end
        end
    end
    send_private(event.slot, "[GT] admin menu UI is not available yet; showing the shared command catalog.")
    return true
end

function plugin.initialize(host)
    plugin.host = host
    config = load_config()
    host.log("GarrisonTools initialized")
end

function plugin.shutdown()
    active_effects = {}
end

function plugin.on_map_changing(e)
    clear_all_effects(true)
end

function plugin.on_client_disconnected(e)
    if e ~= nil and e.slot ~= nil then
        clear_effects_for_slot(e.slot, false)
    end
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
        return handle_help(e, arguments)
    elseif command_name == "status" then
        return handle_status(context, e)
    elseif command_name == "cvars" then
        return handle_cvars(e, arguments)
    elseif command_name == "cvar" then
        return handle_cvar(e, arguments)
    elseif command_name == "seffect" then
        return handle_seffect(e, arguments)
    elseif command_name == "say" then
        return handle_say(e, arguments)
    elseif command_name == "psay" then
        return handle_psay(e, arguments)
    elseif command_name == "kick" then
        return handle_kick(e, arguments)
    elseif command_name == "ban" then
        return handle_ban(e, arguments)
    elseif command_name == "banip" then
        return handle_banip(context, e, arguments)
    elseif command_name == "unban" then
        return handle_unban(e, arguments)
    elseif command_name == "slay" then
        return handle_slay(e, arguments)
    elseif command_name == "burn" then
        return handle_burn(e, arguments)
    elseif command_name == "gag" then
        return handle_gag(e, arguments)
    elseif command_name == "rename" then
        return handle_rename(e, arguments)
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
