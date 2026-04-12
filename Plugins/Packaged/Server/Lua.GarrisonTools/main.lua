local plugin = {}

local target_help_line = "[GT] targets | name | #userid | @me | @all | @alive | @dead | @red | @blue"
local seffect_help_line = "[GT] seffects | blind | earthquake | scale | speed | lowgrav | highgrav | clear"
local default_ban_minutes = 60
local default_burn_seconds = 10.0
local default_announcement_notice_ticks = 300
local client_effects_announcement_message_type = "announce.notice"
local help_page_size = 6
local command_catalog_count = 21
local command_category_count = 6
local help_page_count = 4
local admin_menu_client_plugin_id = "open-garrison.client.lua-garrison-tools-menu"
local admin_menu_client_source_plugin_id = "open-garrison.client.lua-garrison-tools-menu"
local admin_menu_open_message_type = "adminmenu.open"
local admin_menu_close_message_type = "adminmenu.close"
local admin_menu_select_message_type = "adminmenu.select"
local admin_menu_payload_prefix = "am1"
local admin_menu_root_screen = "root"

local function list(...)
    local items = {}
    local item_count = select("#", ...)
    for index = 1, item_count do
        items[index] = select(index, ...)
    end

    return items
end

local function menu_entry(token, label)
    return {
        token = token,
        label = label,
    }
end

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
    },
    speed = {
        defaultSeconds = 10.0,
        defaultValue = 3.0,
        minValue = 0.1,
        maxValue = 4.0
    },
    lowgrav = {
        defaultSeconds = 10.0,
        defaultValue = 0.5,
        minValue = 0.0,
        maxValue = 4.0
    },
    highgrav = {
        defaultSeconds = 10.0,
        defaultValue = 4.0,
        minValue = 0.0,
        maxValue = 4.0
    }
}

local config = default_config
local active_effects = {}
local admin_menu_sessions = {}
local get_sequential_count
local append_sequential
local find_command_spec_by_name
local get_admin_menu_action_title
local get_admin_menu_action_breadcrumb
local command_categories = {
    "Reference",
    "Communication",
    "Player Control",
    "Match Control",
    "Effects",
    "Session"
}
local command_specs = {}

local function append_command_spec(spec)
    command_specs[#command_specs + 1] = spec
end

append_command_spec({ name = "help", category = "Reference", usage = "!gt_help [page|search]", summary = "List commands, pages, or matching search results.", keywords = "commands search page docs", detail = "Use a page number for paged output or a search term like cvar or ban." })
append_command_spec({ name = "status", category = "Reference", usage = "!gt_status", summary = "Show server, admin, and player status details.", keywords = "players userid roster info" })
append_command_spec({ name = "cvars", category = "Reference", usage = "!gt_cvars [filter]", summary = "List server cvars, optionally filtered by text.", keywords = "config variables settings server" })
append_command_spec({ name = "cvar", category = "Reference", usage = "!gt_cvar <name> [value]", summary = "Read or update a single cvar.", keywords = "config variable set get protect server", detail = "Use !gt_cvar protect <name> to add a cvar to the runtime protected list." })
append_command_spec({ name = "adminmenu", category = "Reference", usage = "!gt_adminmenu", summary = "Show the categorized admin command catalog.", keywords = "menu categories catalog ui", detail = "Admin menu reads the shared command catalog so text help and UI can stay in sync." })
append_command_spec({ name = "say", category = "Communication", usage = "!gt_say <text>", summary = "Broadcast a top-screen notice to all players for 5 seconds.", keywords = "broadcast announcement notice top screen" })
append_command_spec({ name = "psay", category = "Communication", usage = "!gt_psay <target> <message>", summary = "Send a private admin message to one target.", keywords = "private whisper tell target", usesTargets = true })
append_command_spec({ name = "kick", category = "Player Control", usage = "!gt_kick <target> [reason]", summary = "Kick one target from the server.", keywords = "disconnect remove player", usesTargets = true })
append_command_spec({ name = "ban", category = "Player Control", usage = "!gt_ban <target> [minutes|0] [reason]", summary = "Ban an active target by identity, 60 minutes by default.", keywords = "ban player timeout permanent", usesTargets = true })
append_command_spec({ name = "banip", category = "Player Control", usage = "!gt_banip <target|ip> [minutes|0] [reason]", summary = "Ban by target endpoint or raw IP with high-trust authority.", keywords = "ban address endpoint ip timeout permanent", usesTargets = true })
append_command_spec({ name = "unban", category = "Player Control", usage = "!gt_unban <ip>", summary = "Remove an IP ban.", keywords = "unban pardon address ip" })
append_command_spec({ name = "slay", category = "Player Control", usage = "!gt_slay <target>", summary = "Kill one or more live targets.", keywords = "kill suicide eliminate", usesTargets = true })
append_command_spec({ name = "burn", category = "Player Control", usage = "!gt_burn <target> [time]", summary = "Ignite one or more live targets for a duration.", keywords = "ignite fire afterburn", usesTargets = true })
append_command_spec({ name = "gag", category = "Player Control", usage = "!gt_gag <target>", summary = "Toggle chat gagging for one target.", keywords = "mute silence chat", usesTargets = true })
append_command_spec({ name = "rename", category = "Player Control", usage = "!gt_rename <target> <name>", summary = "Rename one target.", keywords = "name alias nick", usesTargets = true })
append_command_spec({ name = "bots", category = "Player Control", usage = "!gt_bots <list|add|remove|fill|clear> ...", summary = "Manage host bots through the authenticated GarrisonTools chat flow.", keywords = "bot bots add remove fill clear ai" })
append_command_spec({ name = "map", category = "Match Control", usage = "!gt_map <map> [area]", summary = "Change the current map.", keywords = "level rotation change round" })
append_command_spec({ name = "nextmap", category = "Match Control", usage = "!gt_nextmap <map> [area]", summary = "Set the next-round map.", keywords = "level rotation next round future" })
append_command_spec({ name = "seffect", category = "Effects", usage = "!gt_seffect <effect> <target> [time]", summary = "Apply, scale, or clear bundled timed effects on targets.", keywords = "blind earthquake scale speed lowgrav highgrav clear visual fx", usesTargets = true, showSeffectHelp = true })
append_command_spec({ name = "auth", category = "Session", usage = "!gt_auth <password>", summary = "Authenticate this admin session.", keywords = "login password rcon session" })
append_command_spec({ name = "logout", category = "Session", usage = "!gt_logout", summary = "End this admin session.", keywords = "log out unauthenticate session" })
local command_catalog = {
    help = { category = "Reference", usage = "!gt_help [page|search]", summary = "List commands, pages, or matching search results.", detail = "Use a page number for paged output or a search term like cvar or ban." },
    status = { category = "Reference", usage = "!gt_status", summary = "Show server, admin, and player status details." },
    cvars = { category = "Reference", usage = "!gt_cvars [filter]", summary = "List server cvars, optionally filtered by text." },
    cvar = { category = "Reference", usage = "!gt_cvar <name> [value]", summary = "Read or update a single cvar." },
    adminmenu = { category = "Reference", usage = "!gt_adminmenu", summary = "Show the categorized admin command catalog." },
    say = { category = "Communication", usage = "!gt_say <text>", summary = "Broadcast a top-screen notice to all players for 5 seconds." },
    psay = { category = "Communication", usage = "!gt_psay <target> <message>", summary = "Send a private admin message to one target.", usesTargets = true },
    kick = { category = "Player Control", usage = "!gt_kick <target> [reason]", summary = "Kick one target from the server.", usesTargets = true },
    ban = { category = "Player Control", usage = "!gt_ban <target> [minutes|0] [reason]", summary = "Ban an active target by identity, 60 minutes by default.", usesTargets = true },
    banip = { category = "Player Control", usage = "!gt_banip <target|ip> [minutes|0] [reason]", summary = "Ban by target endpoint or raw IP with high-trust authority.", usesTargets = true },
    unban = { category = "Player Control", usage = "!gt_unban <ip>", summary = "Remove an IP ban." },
    slay = { category = "Player Control", usage = "!gt_slay <target>", summary = "Kill one or more live targets.", usesTargets = true },
    burn = { category = "Player Control", usage = "!gt_burn <target> [time]", summary = "Ignite one or more live targets for a duration.", usesTargets = true },
    gag = { category = "Player Control", usage = "!gt_gag <target>", summary = "Toggle chat gagging for one target.", usesTargets = true },
    rename = { category = "Player Control", usage = "!gt_rename <target> <name>", summary = "Rename one target.", usesTargets = true },
    bots = { category = "Player Control", usage = "!gt_bots <list|add|remove|fill|clear> ...", summary = "Manage host bots through the authenticated GarrisonTools chat flow." },
    map = { category = "Match Control", usage = "!gt_map <map> [area]", summary = "Change the current map." },
    nextmap = { category = "Match Control", usage = "!gt_nextmap <map> [area]", summary = "Set the next-round map." },
    seffect = { category = "Effects", usage = "!gt_seffect <effect> <target> [time]", summary = "Apply, scale, or clear bundled timed effects on targets.", usesTargets = true, showSeffectHelp = true },
    auth = { category = "Session", usage = "!gt_auth <password>", summary = "Authenticate this admin session." },
    logout = { category = "Session", usage = "!gt_logout", summary = "End this admin session." },
}
local help_page_command_keys = {
    list("help", "status", "cvars", "cvar", "adminmenu", "say"),
    list("psay", "kick", "ban", "banip", "unban", "slay"),
    list("burn", "gag", "rename", "bots", "map", "nextmap"),
    list("seffect", "auth", "logout"),
}
local admin_category_command_keys = {
    { category = "Reference", keys = list("help", "status", "cvars", "cvar", "adminmenu") },
    { category = "Communication", keys = list("say", "psay") },
    { category = "Player Control", keys = list("kick", "ban", "banip", "unban", "slay", "burn", "gag", "rename", "bots") },
    { category = "Match Control", keys = list("map", "nextmap") },
    { category = "Effects", keys = list("seffect") },
    { category = "Session", keys = list("auth", "logout") },
}
local help_search_aliases = {
    ["help"] = "help",
    ["commands"] = "help",
    ["page"] = "help",
    ["search"] = "help",
    ["status"] = "status",
    ["players"] = "status",
    ["userid"] = "status",
    ["cvars"] = "cvars",
    ["cvar"] = "cvar",
    ["protect"] = "cvar",
    ["adminmenu"] = "adminmenu",
    ["menu"] = "adminmenu",
    ["say"] = "say",
    ["psay"] = "psay",
    ["kick"] = "kick",
    ["ban"] = "ban",
    ["banip"] = "banip",
    ["unban"] = "unban",
    ["slay"] = "slay",
    ["burn"] = "burn",
    ["gag"] = "gag",
    ["rename"] = "rename",
    ["bot"] = "bots",
    ["bots"] = "bots",
    ["map"] = "map",
    ["nextmap"] = "nextmap",
    ["seffect"] = "seffect",
    ["effect"] = "seffect",
    ["auth"] = "auth",
    ["logout"] = "logout",
}
local help_page_lines = {
    [1] = "[GT] commands | !gt_help [page|search] | !gt_status | !gt_cvars [filter] | !gt_cvar <name> [value] | !gt_adminmenu | !gt_say <text>",
    [2] = "[GT] commands | !gt_psay <target> <message> | !gt_kick <target> [reason] | !gt_ban <target> [minutes|0] [reason] | !gt_banip <target|ip> [minutes|0] [reason] | !gt_unban <ip> | !gt_slay <target>",
    [3] = "[GT] commands | !gt_burn <target> [time] | !gt_gag <target> | !gt_rename <target> <name> | !gt_bots <list|add|remove|fill|clear> ... | !gt_map <map> [area] | !gt_nextmap <map> [area]",
    [4] = "[GT] commands | !gt_seffect <effect> <target> [time] | !gt_auth <password> | !gt_logout",
}
local admin_menu_lines = {
    "[GT] category | Reference | count=5 | !gt_help [page|search] | !gt_status | !gt_cvars [filter] | !gt_cvar <name> [value] | !gt_adminmenu",
    "[GT] category | Communication | count=2 | !gt_say <text> | !gt_psay <target> <message>",
    "[GT] category | Player Control | count=9 | !gt_kick <target> [reason] | !gt_ban <target> [minutes|0] [reason] | !gt_banip <target|ip> [minutes|0] [reason] | !gt_unban <ip> | !gt_slay <target> | !gt_burn <target> [time] | !gt_gag <target> | !gt_rename <target> <name> | !gt_bots <list|add|remove|fill|clear> ...",
    "[GT] category | Match Control | count=2 | !gt_map <map> [area] | !gt_nextmap <map> [area]",
    "[GT] category | Effects | count=1 | !gt_seffect <effect> <target> [time]",
    "[GT] category | Session | count=2 | !gt_auth <password> | !gt_logout",
}
local function get_admin_menu_branch_title(branch_id)
    if branch_id == "server_management" then
        return "Server management"
    end
    if branch_id == "game_management" then
        return "Game management"
    end
    if branch_id == "player_management" then
        return "Player management"
    end
    if branch_id == "fun" then
        return "Fun"
    end

    return "Admin Menu"
end

local function get_admin_menu_branch_ordered_categories(branch_id)
    local categories = {}
    local category_count = 0
    if branch_id == "server_management" then
        category_count = category_count + 1
        categories[category_count] = "Reference"
        category_count = category_count + 1
        categories[category_count] = "Communication"
        category_count = category_count + 1
        categories[category_count] = "Session"
        return categories
    end
    if branch_id == "game_management" then
        category_count = category_count + 1
        categories[category_count] = "Match Control"
        return categories
    end
    if branch_id == "player_management" then
        category_count = category_count + 1
        categories[category_count] = "Player Control"
        return categories
    end

    return categories
end

local function get_admin_menu_branch_subtitle(branch_id)
    if branch_id == "server_management" then
        return "Reference, communication, and session commands."
    end
    if branch_id == "game_management" then
        return "Match and rotation commands."
    end
    if branch_id == "player_management" then
        return "Player control commands."
    end
    if branch_id == "fun" then
        return "Effects and modifiers."
    end

    return "Choose a branch."
end

local function get_admin_menu_command_mode(spec)
    local name = spec ~= nil and spec.name or ""
    if name == "adminmenu" or name == "auth" or name == "logout" or name == "seffect" then
        return "hidden"
    end
    if name == "help" or name == "status" or name == "cvars" then
        return "execute"
    end
    if name == "kick" or name == "slay" or name == "gag" or name == "burn" then
        return "action"
    end

    return "detail"
end

local function get_admin_menu_command_branch(spec)
    local category = spec ~= nil and spec.category or ""
    if category == "Reference" or category == "Communication" or category == "Session" then
        return "server_management"
    end
    if category == "Match Control" then
        return "game_management"
    end
    if category == "Player Control" then
        return "player_management"
    end

    return nil
end

local function get_admin_menu_command_label(spec)
    if spec == nil then
        return "Command"
    end

    if spec.name == "help" then
        return "Help"
    end
    if spec.name == "status" then
        return "Status"
    end
    if spec.name == "cvars" then
        return "Cvars"
    end
    if spec.name == "cvar" then
        return "Cvar"
    end
    if spec.name == "say" then
        return "Say"
    end
    if spec.name == "psay" then
        return "Private say"
    end
    if spec.name == "ban" then
        return "Ban"
    end
    if spec.name == "banip" then
        return "Ban IP"
    end
    if spec.name == "unban" then
        return "Unban"
    end
    if spec.name == "rename" then
        return "Rename"
    end
    if spec.name == "map" then
        return "Map"
    end
    if spec.name == "nextmap" then
        return "Next map"
    end
    if spec.name == "logout" then
        return "Logout"
    end

    return get_admin_menu_action_title(spec.name)
end

local function get_admin_menu_command_specs_for_category(branch_id, category)
    local specs = {}
    for index = 1, command_category_count do
        local category_entry = admin_category_command_keys[index]
        if category_entry ~= nil and category_entry.category == category then
            local keys = category_entry.keys or {}
            local spec_count = 0
            local key_index = 1
            while keys[key_index] ~= nil do
                local spec = find_command_spec_by_name(keys[key_index])
                if spec ~= nil
                    and get_admin_menu_command_branch(spec) == branch_id
                    and get_admin_menu_command_mode(spec) ~= "hidden" then
                    spec_count = spec_count + 1
                    specs[spec_count] = spec
                end

                key_index = key_index + 1
            end

            break
        end
    end

    return specs
end

local function get_admin_menu_branch_categories(branch_id)
    local ordered_categories = get_admin_menu_branch_ordered_categories(branch_id)
    local categories = {}
    local index = 1
    local category_count = 0
    while ordered_categories[index] ~= nil do
        local category = ordered_categories[index]
        local category_specs = get_admin_menu_command_specs_for_category(branch_id, category)
        if category_specs[1] ~= nil then
            category_count = category_count + 1
            categories[category_count] = category
        end

        index = index + 1
    end

    return categories
end

local function get_admin_menu_command_specs_for_branch(branch_id)
    local specs = {}
    local category_count = 0
    local ordered_categories = get_admin_menu_branch_ordered_categories(branch_id)
    local category_index = 1
    while ordered_categories[category_index] ~= nil do
        local category_specs = get_admin_menu_command_specs_for_category(branch_id, ordered_categories[category_index])
        local spec_index = 1
        while category_specs[spec_index] ~= nil do
            category_count = category_count + 1
            specs[category_count] = category_specs[spec_index]
            spec_index = spec_index + 1
        end

        category_index = category_index + 1
    end

    return specs
end
local send_private
local send_private_lines

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

local function is_whitespace(character)
    return character == " "
        or character == "\t"
        or character == "\r"
        or character == "\n"
end

local function tokenize_arguments(text)
    local normalized = trim(text)
    local tokens = {}
    local token_count = 0
    local length = #normalized
    local index = 1

    while index <= length do
        while index <= length and is_whitespace(normalized:sub(index, index)) do
            index = index + 1
        end

        if index > length then
            break
        end

        local token = ""
        local current = normalized:sub(index, index)
        local quoted = current == "\""
        if quoted then
            index = index + 1
        end

        while index <= length do
            current = normalized:sub(index, index)
            if current == "\\" and index < length then
                local escaped = normalized:sub(index + 1, index + 1)
                if escaped == "\"" or escaped == "\\" then
                    token = token .. escaped
                    index = index + 2
                else
                    token = token .. current
                    index = index + 1
                end
            elseif quoted then
                if current == "\"" then
                    index = index + 1
                    break
                end

                token = token .. current
                index = index + 1
            else
                if is_whitespace(current) then
                    break
                end

                token = token .. current
                index = index + 1
            end
        end

        token_count = token_count + 1
        tokens[token_count] = token
    end

    return tokens
end

local function join_tokens(tokens, start_index)
    local combined = ""
    local index = start_index or 1
    while tokens[index] ~= nil do
        if combined ~= "" then
            combined = combined .. " "
        end

        combined = combined .. tostring(tokens[index])
        index = index + 1
    end

    return combined
end

local function split_first_word(text)
    local tokens = tokenize_arguments(text)
    local first = tokens[1]
    if first == nil then
        return "", ""
    end

    return first, join_tokens(tokens, 2)
end

local function get_sorted_command_specs()
    return command_specs
end

find_command_spec_by_name = function(name)
    local normalized = normalize_command_lookup(name)
    for index = 1, command_catalog_count do
        local spec = command_specs[index]
        if spec ~= nil and spec.name == normalized then
            return spec
        end
    end

    local catalog_spec = command_catalog[normalized]
    if catalog_spec ~= nil then
        catalog_spec.name = catalog_spec.name or normalized
        return catalog_spec
    end

    return nil
end

local function command_matches_search(spec, search_text)
    local normalized = normalize_search_text(search_text)
    if normalized == "" then
        return true
    end

    return string.find(spec.name, normalized, 1, true) ~= nil
        or string.find(string.lower(spec.category), normalized, 1, true) ~= nil
        or string.find(string.lower(spec.usage), normalized, 1, true) ~= nil
        or string.find(string.lower(spec.summary), normalized, 1, true) ~= nil
        or string.find(string.lower(spec.keywords or ""), normalized, 1, true) ~= nil
        or string.find(string.lower(spec.detail or ""), normalized, 1, true) ~= nil
end

local function find_matching_command_specs(search_text)
    local matches = {}
    local normalized = normalize_search_text(search_text)
    local alias_name = help_search_aliases[normalize_command_lookup(search_text)] or help_search_aliases[normalized]
    if alias_name ~= nil then
        local alias_spec = find_command_spec_by_name(alias_name)
        if alias_spec ~= nil then
            table.insert(matches, alias_spec)
            return matches
        end
    end

    for index = 1, command_catalog_count do
        local spec = command_specs[index]
        if spec ~= nil and command_matches_search(spec, search_text) then
            table.insert(matches, spec)
        end
    end

    return matches
end

local function format_command_summary_text(spec)
    return spec.usage
end

local function send_command_summary_line(slot, spec)
    send_private(slot, "[GT] command | category=" .. spec.category .. " | " .. spec.usage)
    send_private(slot, "[GT] summary | " .. spec.summary)
end

local function send_command_detail(slot, spec)
    send_private(slot, "[GT] command | category=" .. spec.category .. " | " .. spec.usage)
    send_private(slot, "[GT] summary | " .. spec.summary)
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

local function send_batched_command_summaries(slot, commands, prefix)
    local batch = {}
    for index = 1, #commands do
        local spec = commands[index]
        if spec ~= nil then
            table.insert(batch, format_command_summary_text(spec))
            if #batch >= 3 then
                send_private(slot, prefix .. " | " .. table.concat(batch, " || "))
                batch = {}
            end
        end
    end

    if #batch > 0 then
        send_private(slot, prefix .. " | " .. table.concat(batch, " || "))
    end
end

local function join_command_usages(commands)
    local text = ""
    for index = 1, #commands do
        local spec = commands[index]
        if spec ~= nil then
            if text ~= "" then
                text = text .. " | "
            end
            text = text .. spec.usage
        end
    end

    return text
end

local function build_commands_by_category()
    local grouped = {}
    for index = 1, command_category_count do
        table.insert(grouped, {
            category = command_categories[index],
            commands = {}
        })
    end

    for index = 1, command_catalog_count do
        local spec = command_specs[index]
        if spec ~= nil then
            for category_index = 1, command_category_count do
                local entry = grouped[category_index]
                if entry.category == spec.category then
                    table.insert(entry.commands, spec)
                    break
                end
            end
        end
    end

    return grouped
end

local function get_command_specs_for_keys(keys)
    local specs = {}
    if keys == nil then
        return specs
    end

    for index = 1, #keys do
        local spec = find_command_spec_by_name(keys[index])
        if spec ~= nil then
            specs[#specs + 1] = spec
        end
    end

    return specs
end

local function get_command_specs_for_page(page_index)
    local specs = {}
    local start_index = ((page_index - 1) * help_page_size) + 1
    local end_index = math.min(start_index + help_page_size - 1, command_catalog_count)
    for index = start_index, end_index do
        local spec = command_specs[index]
        if spec ~= nil then
            table.insert(specs, spec)
        end
    end

    return specs
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

send_private = function(slot, text)
    plugin.host.send_system_message(slot, text)
end

send_private_lines = function(slot, lines)
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
    if normalized == "speed" or normalized == "fast" or normalized == "faster" then
        return "speed"
    end
    if normalized == "lowgrav" or normalized == "lowgravity" or normalized == "reducedgravity" then
        return "lowgrav"
    end
    if normalized == "highgrav" or normalized == "highgravity" or normalized == "stronggravity" then
        return "highgrav"
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
        },
        speed = {
            defaultSeconds = clamp(tonumber(loaded.speed and loaded.speed.defaultSeconds) or default_config.speed.defaultSeconds, 0.1, 600.0),
            minValue = clamp(tonumber(loaded.speed and loaded.speed.minValue) or default_config.speed.minValue, default_config.speed.minValue, default_config.speed.maxValue),
            maxValue = clamp(tonumber(loaded.speed and loaded.speed.maxValue) or default_config.speed.maxValue, default_config.speed.minValue, default_config.speed.maxValue),
            defaultValue = tonumber(loaded.speed and loaded.speed.defaultValue) or default_config.speed.defaultValue,
        },
        lowgrav = {
            defaultSeconds = clamp(tonumber(loaded.lowgrav and loaded.lowgrav.defaultSeconds) or default_config.lowgrav.defaultSeconds, 0.1, 600.0),
            minValue = clamp(tonumber(loaded.lowgrav and loaded.lowgrav.minValue) or default_config.lowgrav.minValue, default_config.lowgrav.minValue, default_config.lowgrav.maxValue),
            maxValue = clamp(tonumber(loaded.lowgrav and loaded.lowgrav.maxValue) or default_config.lowgrav.maxValue, default_config.lowgrav.minValue, default_config.lowgrav.maxValue),
            defaultValue = tonumber(loaded.lowgrav and loaded.lowgrav.defaultValue) or default_config.lowgrav.defaultValue,
        },
        highgrav = {
            defaultSeconds = clamp(tonumber(loaded.highgrav and loaded.highgrav.defaultSeconds) or default_config.highgrav.defaultSeconds, 0.1, 600.0),
            minValue = clamp(tonumber(loaded.highgrav and loaded.highgrav.minValue) or default_config.highgrav.minValue, default_config.highgrav.minValue, default_config.highgrav.maxValue),
            maxValue = clamp(tonumber(loaded.highgrav and loaded.highgrav.maxValue) or default_config.highgrav.maxValue, default_config.highgrav.minValue, default_config.highgrav.maxValue),
            defaultValue = tonumber(loaded.highgrav and loaded.highgrav.defaultValue) or default_config.highgrav.defaultValue,
        }
    }

    if normalized.scale.minValue > normalized.scale.maxValue then
        normalized.scale.minValue = default_config.scale.minValue
        normalized.scale.maxValue = default_config.scale.maxValue
    end
    if normalized.speed.minValue > normalized.speed.maxValue then
        normalized.speed.minValue = default_config.speed.minValue
        normalized.speed.maxValue = default_config.speed.maxValue
    end
    if normalized.lowgrav.minValue > normalized.lowgrav.maxValue then
        normalized.lowgrav.minValue = default_config.lowgrav.minValue
        normalized.lowgrav.maxValue = default_config.lowgrav.maxValue
    end
    if normalized.highgrav.minValue > normalized.highgrav.maxValue then
        normalized.highgrav.minValue = default_config.highgrav.minValue
        normalized.highgrav.maxValue = default_config.highgrav.maxValue
    end

    normalized.scale.defaultValue = clamp(normalized.scale.defaultValue, normalized.scale.minValue, normalized.scale.maxValue)
    normalized.speed.defaultValue = clamp(normalized.speed.defaultValue, normalized.speed.minValue, normalized.speed.maxValue)
    normalized.lowgrav.defaultValue = clamp(normalized.lowgrav.defaultValue, normalized.lowgrav.minValue, normalized.lowgrav.maxValue)
    normalized.highgrav.defaultValue = clamp(normalized.highgrav.defaultValue, normalized.highgrav.minValue, normalized.highgrav.maxValue)

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

local function is_client_effect(effect_id)
    return effect_id == "blind" or effect_id == "earthquake"
end

local function find_conflicting_effect_entry(slot, effect_id)
    if effect_id == "lowgrav" or effect_id == "highgrav" then
        local lowgrav_entry = active_effects[create_effect_key(slot, "lowgrav")]
        if lowgrav_entry ~= nil then
            return lowgrav_entry
        end

        local highgrav_entry = active_effects[create_effect_key(slot, "highgrav")]
        if highgrav_entry ~= nil then
            return highgrav_entry
        end

        return nil
    end

    return active_effects[create_effect_key(slot, effect_id)]
end

local clear_effect_entry

local function clear_conflicting_effect_entries(slot, effect_id)
    if effect_id == "lowgrav" or effect_id == "highgrav" then
        clear_effect_entry(slot, "lowgrav", false)
        clear_effect_entry(slot, "highgrav", false)
        return
    end

    clear_effect_entry(slot, effect_id, false)
end

local function restore_effect_state(entry)
    if entry == nil then
        return
    end

    if entry.effectId == "scale" then
        if entry.restoreScale ~= nil then
            plugin.host.try_set_player_scale(entry.slot, entry.restoreScale)
        end
        return
    end

    if entry.effectId == "speed" then
        if entry.restoreMovementSpeedUsesGlobal then
            plugin.host.try_clear_player_movement_speed_scale(entry.slot)
        elseif entry.restoreMovementSpeedScale ~= nil then
            plugin.host.try_set_player_movement_speed_scale(entry.slot, entry.restoreMovementSpeedScale)
        end
        return
    end

    if entry.effectId == "lowgrav" or entry.effectId == "highgrav" then
        if entry.restoreGravityUsesGlobal then
            plugin.host.try_clear_player_gravity_scale(entry.slot)
        elseif entry.restoreGravityScale ~= nil then
            plugin.host.try_set_player_gravity_scale(entry.slot, entry.restoreGravityScale)
        end
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

clear_effect_entry = function(slot, effect_id, notify_client)
    local effect_key = create_effect_key(slot, effect_id)
    local entry = active_effects[effect_key]
    if entry == nil then
        return false
    end

    cancel_effect_timer(entry.timerId)
    restore_effect_state(entry)
    active_effects[effect_key] = nil
    if notify_client and is_client_effect(entry.effectId) then
        send_effect_clear(slot, entry.effectId)
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
            restore_effect_state(entry)
            active_effects[key] = nil
            if notify_client and is_client_effect(entry.effectId) then
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
            restore_effect_state(entry)
            active_effects[key] = nil
            if notify_client and is_client_effect(entry.effectId) then
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
    if effect_id == "speed" then
        return config.speed.defaultSeconds
    end
    if effect_id == "lowgrav" then
        return config.lowgrav.defaultSeconds
    end
    if effect_id == "highgrav" then
        return config.highgrav.defaultSeconds
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

local function get_numeric_effect_config(effect_id)
    if effect_id == "scale" then
        return config.scale, "Scale"
    end
    if effect_id == "speed" then
        return config.speed, "Speed scale"
    end
    if effect_id == "lowgrav" or effect_id == "highgrav" then
        return effect_id == "lowgrav" and config.lowgrav or config.highgrav, "Gravity scale"
    end

    return nil, nil
end

local function parse_numeric_effect_value_and_duration(effect_id, text)
    local effect_config, value_label = get_numeric_effect_config(effect_id)
    if effect_config == nil then
        return nil, nil, "Unsupported effect value parser."
    end

    local normalized = trim(text)
    if normalized == "" then
        return effect_config.defaultValue, effect_config.defaultSeconds, nil
    end

    local value_text, remainder = split_first_word(normalized)
    local parsed_value = tonumber(value_text)
    if parsed_value == nil then
        return nil, nil, value_label .. " must be a number."
    end

    if parsed_value < effect_config.minValue or parsed_value > effect_config.maxValue then
        return nil, nil, value_label .. " must be between " .. tostring(effect_config.minValue) .. " and " .. tostring(effect_config.maxValue) .. "."
    end

    local duration_seconds, duration_error = parse_duration_seconds(remainder, effect_id)
    if duration_seconds == nil then
        return nil, nil, duration_error
    end

    return parsed_value, duration_seconds, nil
end

local function get_target_player_scale(target)
    return target.playerScale or target.PlayerScale or 1
end

local function get_target_movement_speed_scale(target)
    return target.movementSpeedScale or target.MovementSpeedScale or 1
end

local function get_target_has_movement_speed_scale_override(target)
    local value = target.hasMovementSpeedScaleOverride
    if value == nil then
        value = target.HasMovementSpeedScaleOverride
    end

    return value == true
end

local function get_target_gravity_scale(target)
    return target.gravityScale or target.GravityScale or 1
end

local function get_target_has_gravity_scale_override(target)
    local value = target.hasGravityScaleOverride
    if value == nil then
        value = target.HasGravityScaleOverride
    end

    return value == true
end

local function apply_effect_to_slot(slot, effect_id, duration_seconds, parameters)
    clear_conflicting_effect_entries(slot, effect_id)

    local restore_scale = nil
    local restore_movement_speed_scale = nil
    local restore_movement_speed_uses_global = false
    local restore_gravity_scale = nil
    local restore_gravity_uses_global = false
    if effect_id == "scale" then
        restore_scale = parameters ~= nil and parameters.restoreScale or nil
        local next_scale = parameters ~= nil and parameters.scale or config.scale.defaultValue
        if not plugin.host.try_set_player_scale(slot, next_scale) then
            return false
        end
    elseif effect_id == "speed" then
        restore_movement_speed_scale = parameters ~= nil and parameters.restoreMovementSpeedScale or nil
        restore_movement_speed_uses_global = parameters ~= nil and parameters.restoreMovementSpeedUsesGlobal or false
        local next_scale = parameters ~= nil and parameters.scale or config.speed.defaultValue
        if not plugin.host.try_set_player_movement_speed_scale(slot, next_scale) then
            return false
        end
    elseif effect_id == "lowgrav" or effect_id == "highgrav" then
        restore_gravity_scale = parameters ~= nil and parameters.restoreGravityScale or nil
        restore_gravity_uses_global = parameters ~= nil and parameters.restoreGravityUsesGlobal or false
        local next_scale = parameters ~= nil and parameters.scale or (effect_id == "lowgrav" and config.lowgrav.defaultValue or config.highgrav.defaultValue)
        if not plugin.host.try_set_player_gravity_scale(slot, next_scale) then
            return false
        end
    else
        send_effect_apply(slot, effect_id, duration_seconds)
    end

    local effect_key = create_effect_key(slot, effect_id)
    local timer_id = nil
    if duration_seconds ~= nil and duration_seconds > 0.0 then
        timer_id = plugin.host.schedule_once(duration_seconds, function()
            local entry = active_effects[effect_key]
            if entry == nil then
                return
            end

            clear_effect_entry(slot, effect_id, true)
        end, "gt_seffect " .. effect_id .. " slot " .. tostring(slot))
    end

    active_effects[effect_key] = {
        slot = slot,
        effectId = effect_id,
        timerId = timer_id,
        restoreScale = restore_scale,
        restoreMovementSpeedScale = restore_movement_speed_scale,
        restoreMovementSpeedUsesGlobal = restore_movement_speed_uses_global,
        restoreGravityScale = restore_gravity_scale,
        restoreGravityUsesGlobal = restore_gravity_uses_global,
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
        local page_index = clamp(page_number, 1, help_page_count)
        send_private(event.slot, "[GT] help | page=" .. tostring(page_index) .. "/" .. tostring(help_page_count) .. " | commands=" .. tostring(command_catalog_count))
        send_private(event.slot, help_page_lines[page_index] or help_page_lines[1])
        send_private(event.slot, "[GT] usage | !gt_help <page> | !gt_help <search> | targets: name | #userid | @me | @all | @alive | @dead | @red | @blue")
        return true
    end

    if search_text ~= "" then
        if normalize_search_text(search_text) == "protect" then
            send_private(event.slot, "[GT] help | search=\"" .. search_text .. "\" | matches=1")
            send_private(event.slot, "[GT] usage: !gt_cvar protect <name>")
            send_command_detail(event.slot, find_command_spec_by_name("cvar"))
            return true
        end

        local match_count = 0
        local first_match = nil
        for index = 1, command_catalog_count do
            local spec = command_specs[index]
            if spec ~= nil and command_matches_search(spec, search_text) then
                match_count = match_count + 1
                if first_match == nil then
                    first_match = spec
                end
            end
        end

        if match_count == 0 then
            send_private(event.slot, "[GT] help | search=\"" .. search_text .. "\" | matches=0")
            return true
        end

        send_private(event.slot, "[GT] help | search=\"" .. search_text .. "\" | matches=" .. tostring(match_count))
        if match_count == 1 and first_match ~= nil then
            send_command_detail(event.slot, first_match)
        elseif first_match ~= nil then
            send_private(event.slot, "[GT] matches | " .. first_match.usage)
        end
        return true
    end

    send_private(event.slot, "[GT] help | page=1/" .. tostring(help_page_count) .. " | commands=" .. tostring(command_catalog_count))
    send_private(event.slot, help_page_lines[1])
    send_private(event.slot, "[GT] usage | !gt_help <page> | !gt_help <search> | targets: name | #userid | @me | @all | @alive | @dead | @red | @blue")
    return true
end

local function handle_status(context, event)
    local summary = plugin.host.get_admin_summary()
    local players, _ = resolve_targets(event.slot, "@all", { allowMultiple = true, includeSpectators = true })
    local identity = context ~= nil and (context.identity or context.Identity) or nil
    local identity_display_name = identity ~= nil and (identity.displayName or identity.DisplayName) or "Admin"
    local identity_authority = identity ~= nil and (identity.authority or identity.Authority) or "Unknown"
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
        "[GT] admin | identity=" .. tostring(identity_display_name)
            .. " | authority=" .. tostring(identity_authority)
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
        send_private(event.slot, "[GT] usage: !gt_seffect scale|speed|lowgrav|highgrav <target> [value] [time]")
        send_private(event.slot, seffect_help_line)
        return true
    end

    local target_text, trailing = parse_target_and_optional_argument(remainder)
    if target_text == nil then
        send_private(event.slot, "[GT] usage: !gt_seffect <effect> <target> [time]")
        send_private(event.slot, "[GT] usage: !gt_seffect scale|speed|lowgrav|highgrav <target> [value] [time]")
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
    if effect_id == "scale" or effect_id == "speed" or effect_id == "lowgrav" or effect_id == "highgrav" then
        local effect_value, parsed_duration_seconds, effect_value_error = parse_numeric_effect_value_and_duration(effect_id, trailing)
        if effect_value == nil then
            send_private(event.slot, "[GT] " .. effect_value_error)
            return true
        end

        duration_seconds = parsed_duration_seconds
        effect_parameters = { scale = effect_value }
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
            local active_entry = find_conflicting_effect_entry(target.slot, effect_id)
            parameters = {
                scale = effect_parameters.scale,
                restoreScale = active_entry ~= nil and active_entry.restoreScale or get_target_player_scale(target),
            }
        elseif effect_id == "speed" then
            local active_entry = find_conflicting_effect_entry(target.slot, effect_id)
            parameters = {
                scale = effect_parameters.scale,
                restoreMovementSpeedScale = active_entry ~= nil and active_entry.restoreMovementSpeedScale or get_target_movement_speed_scale(target),
                restoreMovementSpeedUsesGlobal = active_entry ~= nil and active_entry.restoreMovementSpeedUsesGlobal or not get_target_has_movement_speed_scale_override(target),
            }
        elseif effect_id == "lowgrav" or effect_id == "highgrav" then
            local active_entry = find_conflicting_effect_entry(target.slot, effect_id)
            parameters = {
                scale = effect_parameters.scale,
                restoreGravityScale = active_entry ~= nil and active_entry.restoreGravityScale or get_target_gravity_scale(target),
                restoreGravityUsesGlobal = active_entry ~= nil and active_entry.restoreGravityUsesGlobal or not get_target_has_gravity_scale_override(target),
            }
        end

        if apply_effect_to_slot(target.slot, effect_id, duration_seconds, parameters) then
            applied_count = applied_count + 1
        end
        index = index + 1
    end

    if effect_id == "scale" or effect_id == "speed" or effect_id == "lowgrav" or effect_id == "highgrav" then
        send_private(
            event.slot,
            "[GT] applied " .. effect_id .. " "
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
    local message_text = trim(arguments)
    if message_text == "" then
        send_private(event.slot, "[GT] usage: !gt_say <text>")
        return true
    end

    plugin.host.broadcast_message_to_clients(
        config.clientEffectsPluginId,
        client_effects_announcement_message_type,
        message_text,
        "Text",
        1)
    send_private(event.slot, "[GT] announcement sent.")
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

local function send_bots_usage(slot)
    send_private(slot, "[GT] usage: !gt_bots")
    send_private(slot, "[GT] usage: !gt_bots list")
    send_private(slot, "[GT] usage: !gt_bots add <slot> <red|blue> <class> [name]")
    send_private(slot, "[GT] usage: !gt_bots remove <slot>")
    send_private(slot, "[GT] usage: !gt_bots fill <count> [red|blue] [class]")
    send_private(slot, "[GT] usage: !gt_bots clear")
end

local function parse_bot_slot(text)
    local value = tonumber(trim(text))
    if value == nil or value < 1 or value ~= math.floor(value) then
        return nil
    end

    return value
end

local function normalize_bot_team(text)
    local normalized = string.lower(trim(text))
    if normalized == "red" then
        return "Red"
    end
    if normalized == "blue" or normalized == "blu" then
        return "Blue"
    end

    return nil
end

local function normalize_bot_class(text)
    local normalized = string.lower(trim(text))
    if normalized == "engi" then
        return "Engineer"
    end
    if normalized == "solly" then
        return "Soldier"
    end
    if normalized == "demo" then
        return "Demoman"
    end

    local class_map = {
        scout = "Scout",
        engineer = "Engineer",
        pyro = "Pyro",
        soldier = "Soldier",
        demoman = "Demoman",
        heavy = "Heavy",
        sniper = "Sniper",
        medic = "Medic",
        spy = "Spy",
        quote = "Quote",
    }

    return class_map[normalized]
end

local function handle_bots(event, arguments)
    local subcommand, remainder = split_first_word(arguments)
    subcommand = string.lower(trim(subcommand))
    if subcommand == "" or subcommand == "list" then
        local bot_slots = plugin.host.get_bot_slots() or {}
        if bot_slots[1] == nil then
            send_private(event.slot, "[GT] no bots active.")
            return true
        end

        send_private(event.slot, "[GT] bots | count=" .. tostring(#bot_slots))
        for index = 1, #bot_slots do
            local bot = bot_slots[index]
            send_private(
                event.slot,
                "[GT] bot | slot=" .. tostring(bot.slot or bot.Slot)
                    .. " | team=" .. tostring(bot.team or bot.Team)
                    .. " | class=" .. tostring(bot.playerClass or bot.PlayerClass)
                    .. " | name=" .. tostring(bot.displayName or bot.DisplayName))
        end

        return true
    end

    if subcommand == "add" then
        local slot_text, add_remainder = split_first_word(remainder)
        local team_text, add_remainder2 = split_first_word(add_remainder)
        local class_text, display_name = split_first_word(add_remainder2)
        local slot = parse_bot_slot(slot_text)
        local team = normalize_bot_team(team_text)
        local class_name = normalize_bot_class(class_text)
        local trimmed_display_name = trim(display_name)
        if slot == nil or team == nil or class_name == nil then
            send_bots_usage(event.slot)
            return true
        end

        if trimmed_display_name == "" then
            trimmed_display_name = team .. " Bot " .. tostring(slot)
        end

        if plugin.host.try_add_bot(slot, team, class_name, trimmed_display_name) then
            send_private(event.slot, "[GT] bot added at slot " .. tostring(slot) .. ".")
        else
            send_private(event.slot, "[GT] unable to add bot at slot " .. tostring(slot) .. ".")
        end
        return true
    end

    if subcommand == "remove" then
        local slot = parse_bot_slot(remainder)
        if slot == nil then
            send_bots_usage(event.slot)
            return true
        end

        if plugin.host.try_remove_bot(slot) then
            send_private(event.slot, "[GT] bot removed from slot " .. tostring(slot) .. ".")
        else
            send_private(event.slot, "[GT] no bot at slot " .. tostring(slot) .. ".")
        end
        return true
    end

    if subcommand == "fill" then
        local count_text, fill_remainder = split_first_word(remainder)
        local count = tonumber(trim(count_text))
        local team_text, class_text = split_first_word(fill_remainder)
        if count == nil or count < 1 or count ~= math.floor(count) then
            send_bots_usage(event.slot)
            return true
        end

        local maybe_team = normalize_bot_team(team_text)
        local maybe_class = normalize_bot_class(class_text)
        if maybe_team == nil and trim(class_text) == "" then
            maybe_class = normalize_bot_class(team_text)
        end
        if maybe_team == nil and trim(team_text) ~= "" and maybe_class == nil then
            send_bots_usage(event.slot)
            return true
        end

        local added_count
        if maybe_team ~= nil then
            added_count = plugin.host.try_fill_bot_team(maybe_team, count, maybe_class or "Soldier")
            send_private(event.slot, "[GT] filled " .. tostring(added_count) .. " bot slots on " .. maybe_team .. " team.")
        else
            added_count = plugin.host.try_fill_bots(count, maybe_class or "Soldier")
            send_private(event.slot, "[GT] filled " .. tostring(added_count) .. " bot slots total (" .. tostring(count) .. " per team).")
        end
        return true
    end

    if subcommand == "clear" then
        local removed = plugin.host.try_clear_all_bots()
        send_private(event.slot, "[GT] removed " .. tostring(removed) .. " bots.")
        return true
    end

    send_bots_usage(event.slot)
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

local admin_menu_scale_values = { "1.0", "0.9", "0.8", "0.7", "0.6", "0.5", "0.4", "0.3", "0.2", "0.1" }
local admin_menu_speed_values = { "0.5", "0.75", "1.0", "1.25", "1.5", "2.0", "2.5", "3.0", "4.0" }
local admin_menu_gravity_values = { "0.1", "0.25", "0.5", "0.75", "1.0", "1.5", "2.0", "3.0", "4.0" }

local function shallow_copy_table(source)
    local copy = {}
    if source == nil then
        return copy
    end

    for key, value in pairs(source) do
        copy[key] = value
    end

    return copy
end

get_sequential_count = function(items)
    if items == nil then
        return 0
    end

    local count = 0
    while items[count + 1] ~= nil do
        count = count + 1
    end

    return count
end

append_sequential = function(items, value)
    local next_index = get_sequential_count(items) + 1
    items[next_index] = value
    return next_index
end

local function escape_admin_menu_value(text)
    local value = tostring(text or "")
    value = value:gsub("%%", "%%25")
    value = value:gsub("|", "%%7C")
    value = value:gsub("=", "%%3D")
    value = value:gsub("~", "%%7E")
    value = value:gsub("\t", "%%09")
    value = value:gsub("\r", "%%0D")
    value = value:gsub("\n", "%%0A")
    return value
end

local function build_admin_menu_payload(screen)
    local entries = screen ~= nil and screen.entries or {}
    local payload = admin_menu_payload_prefix
    payload = payload .. "|t=" .. escape_admin_menu_value(screen ~= nil and screen.title or "")
    payload = payload .. "|s=" .. escape_admin_menu_value(screen ~= nil and screen.subtitle or "")
    payload = payload .. "|b=" .. escape_admin_menu_value(screen ~= nil and screen.breadcrumb or "")
    for index = 1, get_sequential_count(entries) do
        local entry = entries[index]
        if entry ~= nil then
            payload = payload .. "|l=" .. escape_admin_menu_value(entry.label or entry.token or "")
        end
    end

    return payload
end

local function make_admin_menu_screen(title, subtitle, breadcrumb, entries)
    return {
        title = title or "Admin Menu",
        subtitle = subtitle or "",
        breadcrumb = breadcrumb or "",
        entries = entries or {},
    }
end

local function send_admin_menu_screen(slot, screen)
    plugin.host.send_message_to_client(
        slot,
        admin_menu_client_plugin_id,
        admin_menu_open_message_type,
        build_admin_menu_payload(screen))
end

local function close_all_admin_menu_sessions(notify_client)
    for slot, _ in pairs(admin_menu_sessions) do
        if notify_client then
            plugin.host.send_message_to_client(
                slot,
                admin_menu_client_plugin_id,
                admin_menu_close_message_type,
                "")
        end
    end

    admin_menu_sessions = {}
end

local function close_admin_menu_session(slot)
    admin_menu_sessions[slot] = nil
    plugin.host.send_message_to_client(
        slot,
        admin_menu_client_plugin_id,
        admin_menu_close_message_type,
        "")
end

local function create_admin_menu_session(context, slot)
    local identity = nil
    local is_authenticated_admin = false
    if context ~= nil then
        identity = context.identity or context.Identity
        is_authenticated_admin = context.isAuthenticatedAdmin == true or context.IsAuthenticatedAdmin == true
    end

    return {
        slot = slot,
        commandContext = {
            identity = identity,
            isAuthenticatedAdmin = is_authenticated_admin,
        },
        state = {
            screen = admin_menu_root_screen,
        },
        stack = {},
    }
end

local function push_admin_menu_state(session, next_state)
    append_sequential(session.stack, shallow_copy_table(session.state))
    session.state = next_state
end

local function pop_admin_menu_state(session)
    local stack_count = get_sequential_count(session.stack)
    if stack_count == 0 then
        return false
    end

    session.state = session.stack[stack_count]
    session.stack[stack_count] = nil
    return true
end

local function paginate_admin_menu_items(items, requested_page, page_size)
    local item_count = get_sequential_count(items)
    local total_pages = math.max(1, math.ceil(item_count / page_size))
    local page_index = clamp(requested_page or 1, 1, total_pages)
    local page_items = {}
    local start_index = ((page_index - 1) * page_size) + 1
    local end_index = math.min(start_index + page_size - 1, item_count)
    local page_item_count = 0
    for index = start_index, end_index do
        page_item_count = page_item_count + 1
        page_items[page_item_count] = items[index]
    end

    return page_items, page_index, total_pages
end

local function next_admin_menu_page(page_index, total_pages)
    if total_pages <= 1 then
        return 1
    end

    return page_index >= total_pages and 1 or (page_index + 1)
end

local function get_admin_menu_players()
    local resolved = plugin.host.resolve_targets("@all", {
        sourceSlot = nil,
        allowMultiple = true,
        includeSpectators = true,
    })
    if resolved == nil or not resolved.success or resolved.targets == nil then
        return {}
    end

    local players = {}
    local index = 1
    while resolved.targets[index] ~= nil do
        players[#players + 1] = resolved.targets[index]
        index = index + 1
    end

    table.sort(players, function(left, right)
        local left_name = string.lower(left.name or "")
        local right_name = string.lower(right.name or "")
        if left_name == right_name then
            return (left.userId or 0) < (right.userId or 0)
        end

        return left_name < right_name
    end)
    return players
end

local function get_admin_menu_value_options(action_kind)
    if action_kind == "scale" then
        return admin_menu_scale_values
    end
    if action_kind == "speed" then
        return admin_menu_speed_values
    end
    if action_kind == "gravity" then
        return admin_menu_gravity_values
    end

    return {}
end

get_admin_menu_action_title = function(action_kind)
    if action_kind == "kick" then
        return "Kick"
    end
    if action_kind == "slay" then
        return "Slay"
    end
    if action_kind == "gag" then
        return "Gag"
    end
    if action_kind == "scale" then
        return "Scale"
    end
    if action_kind == "speed" then
        return "Movement speed"
    end
    if action_kind == "gravity" then
        return "Gravity"
    end
    if action_kind == "blind" then
        return "Blind"
    end
    if action_kind == "burn" then
        return "Burn"
    end
    if action_kind == "earthquake" or action_kind == "earthquake_all" then
        return "Earthquake"
    end
    if action_kind == "blind_all" then
        return "Blind"
    end

    return "Action"
end

get_admin_menu_action_breadcrumb = function(action_kind)
    if action_kind == "kick" or action_kind == "slay" or action_kind == "gag" then
        return "Player management > " .. get_admin_menu_action_title(action_kind)
    end
    if action_kind == "scale" or action_kind == "speed" or action_kind == "gravity" then
        return "Fun > Player effects > Modifiers > " .. get_admin_menu_action_title(action_kind)
    end
    if action_kind == "blind" or action_kind == "burn" or action_kind == "earthquake" then
        return "Fun > Player effects > Status effects > " .. get_admin_menu_action_title(action_kind)
    end
    if action_kind == "blind_all" or action_kind == "earthquake_all" then
        return "Fun > Game effects > " .. get_admin_menu_action_title(action_kind)
    end

    return "Admin Menu"
end

local function format_admin_menu_duration_label(duration_seconds)
    if duration_seconds ~= nil and duration_seconds <= 0 then
        return "until cleared"
    end

    return "for " .. format_seconds(duration_seconds or 0) .. "s"
end

local function execute_menu_seffect_action(slot, effect_id, target_text, effect_value, duration_seconds)
    local targets, error_text = resolve_targets(slot, target_text, {
        allowMultiple = true,
        includeSpectators = true,
    })
    if targets == nil then
        send_private(slot, "[GT] " .. error_text)
        return false
    end

    local applied_count = 0
    local index = 1
    while targets[index] ~= nil do
        local target = targets[index]
        local parameters = nil
        if effect_id == "scale" then
            local active_entry = find_conflicting_effect_entry(target.slot, effect_id)
            parameters = {
                scale = effect_value,
                restoreScale = active_entry ~= nil and active_entry.restoreScale or get_target_player_scale(target),
            }
        elseif effect_id == "speed" then
            local active_entry = find_conflicting_effect_entry(target.slot, effect_id)
            parameters = {
                scale = effect_value,
                restoreMovementSpeedScale = active_entry ~= nil and active_entry.restoreMovementSpeedScale or get_target_movement_speed_scale(target),
                restoreMovementSpeedUsesGlobal = active_entry ~= nil and active_entry.restoreMovementSpeedUsesGlobal or not get_target_has_movement_speed_scale_override(target),
            }
        elseif effect_id == "lowgrav" or effect_id == "highgrav" then
            local active_entry = find_conflicting_effect_entry(target.slot, effect_id)
            parameters = {
                scale = effect_value,
                restoreGravityScale = active_entry ~= nil and active_entry.restoreGravityScale or get_target_gravity_scale(target),
                restoreGravityUsesGlobal = active_entry ~= nil and active_entry.restoreGravityUsesGlobal or not get_target_has_gravity_scale_override(target),
            }
        end

        if apply_effect_to_slot(target.slot, effect_id, duration_seconds, parameters) then
            applied_count = applied_count + 1
        end
        index = index + 1
    end

    if applied_count <= 0 then
        send_private(slot, "[GT] no targets were updated.")
        return false
    end

    local value_suffix = effect_value ~= nil and (" " .. tostring(effect_value)) or ""
    send_private(
        slot,
        "[GT] applied " .. effect_id .. value_suffix
            .. " to " .. tostring(applied_count)
            .. " target(s) " .. format_admin_menu_duration_label(duration_seconds) .. ".")
    return true
end

local function execute_admin_menu_action(session, action_kind, target_text, selected_value, duration_seconds)
    local event = { slot = session.slot }
    if action_kind == "kick" then
        return handle_kick(event, target_text)
    end
    if action_kind == "slay" then
        return handle_slay(event, target_text)
    end
    if action_kind == "gag" then
        return handle_gag(event, target_text)
    end
    if action_kind == "blind" then
        return execute_menu_seffect_action(session.slot, "blind", target_text, nil, duration_seconds)
    end
    if action_kind == "blind_all" then
        return execute_menu_seffect_action(session.slot, "blind", "@all", nil, duration_seconds)
    end
    if action_kind == "earthquake" then
        return execute_menu_seffect_action(session.slot, "earthquake", target_text, nil, duration_seconds)
    end
    if action_kind == "earthquake_all" then
        return execute_menu_seffect_action(session.slot, "earthquake", "@all", nil, duration_seconds)
    end
    if action_kind == "burn" then
        return handle_burn(event, target_text .. " " .. tostring(duration_seconds or default_burn_seconds))
    end
    if action_kind == "scale" then
        return execute_menu_seffect_action(session.slot, "scale", target_text, tonumber(selected_value), duration_seconds)
    end
    if action_kind == "speed" then
        return execute_menu_seffect_action(session.slot, "speed", target_text, tonumber(selected_value), duration_seconds)
    end
    if action_kind == "gravity" then
        local gravity_value = tonumber(selected_value) or 1.0
        local gravity_effect = gravity_value > 1.0 and "highgrav" or "lowgrav"
        return execute_menu_seffect_action(session.slot, gravity_effect, target_text, gravity_value, duration_seconds)
    end
    if action_kind == "status" then
        return handle_status(session.commandContext, event)
    end
    if action_kind == "help" then
        return handle_help(event, "")
    end
    if action_kind == "cvars" then
        return handle_cvars(event, "")
    end

    return false
end

local function build_admin_menu_branch_screen(branch_id, requested_page)
    local specs = get_admin_menu_command_specs_for_branch(branch_id)
    local page_items, page_index, total_pages = paginate_admin_menu_items(specs, requested_page or 1, 3)
    local should_prefix_category = branch_id == "server_management"
    local entries = {}
    for index = 1, get_sequential_count(page_items) do
        local spec = page_items[index]
        local label = get_admin_menu_command_label(spec)
        if should_prefix_category then
            label = tostring(spec.category) .. ": " .. label
        end

        append_sequential(entries, {
            token = "command:" .. spec.name,
            label = label,
        })
    end

    if total_pages > 1 then
        append_sequential(entries, {
            token = "page:" .. tostring(next_admin_menu_page(page_index, total_pages)),
            label = "Next",
        })
    end

    append_sequential(entries, { token = "back", label = "Back" })
    if get_sequential_count(entries) < 6 then
        append_sequential(entries, { token = "close", label = "Close" })
    end

    return make_admin_menu_screen(
        get_admin_menu_branch_title(branch_id),
        get_admin_menu_branch_subtitle(branch_id) .. " | Page " .. tostring(page_index) .. "/" .. tostring(total_pages),
        get_admin_menu_branch_title(branch_id),
        entries)
end

local function build_admin_menu_command_category_screen(state)
    local specs = get_admin_menu_command_specs_for_category(state.branchId, state.category)
    local page_items, page_index, total_pages = paginate_admin_menu_items(specs, state.page or 1, 4)
    local entries = {}
    for index = 1, get_sequential_count(page_items) do
        local spec = page_items[index]
        append_sequential(entries, {
            token = "command:" .. spec.name,
            label = get_admin_menu_command_label(spec),
        })
    end

    if total_pages > 1 then
        append_sequential(entries, {
            token = "page:" .. tostring(next_admin_menu_page(page_index, total_pages)),
            label = "Next",
        })
    end

    append_sequential(entries, { token = "back", label = "Back" })
    if get_sequential_count(entries) < 6 then
        append_sequential(entries, { token = "close", label = "Close" })
    end

    return make_admin_menu_screen(
        state.category or "Commands",
        "Page " .. tostring(page_index) .. "/" .. tostring(total_pages),
        get_admin_menu_branch_title(state.branchId) .. " > " .. tostring(state.category or "Commands"),
        entries)
end

local function build_admin_menu_command_detail_screen(state)
    local spec = find_command_spec_by_name(state.commandName)
    if spec == nil then
        return make_admin_menu_screen("Command", "Unknown command.", get_admin_menu_branch_title(state.branchId), {
            { token = "back", label = "Back" },
            { token = "close", label = "Close" },
        })
    end

    local subtitle = spec.usage or spec.summary or "Use chat for this command."
    if spec.summary ~= nil and spec.summary ~= "" and spec.summary ~= subtitle then
        subtitle = subtitle .. " | " .. spec.summary
    end

    return make_admin_menu_screen(
        get_admin_menu_command_label(spec),
        subtitle,
        get_admin_menu_branch_title(state.branchId) .. " > " .. tostring(state.category or spec.category) .. " > " .. get_admin_menu_command_label(spec),
        {
            { token = "showinfo:" .. spec.name, label = "Show usage in chat" },
            { token = "back", label = "Back" },
            { token = "close", label = "Close" },
        })
end

local function build_admin_menu_target_screen(state)
    local players = get_admin_menu_players()
    local page_items, page_index, total_pages = paginate_admin_menu_items(players, state.page or 1, 3)
    local entries = {}
    for index = 1, get_sequential_count(page_items) do
        local player = page_items[index]
        append_sequential(entries, {
            token = "target:#" .. tostring(player.userId),
            label = tostring(player.name) .. " (#" .. tostring(player.userId) .. ")",
        })
    end

    if total_pages > 1 then
        append_sequential(entries, {
            token = "page:" .. tostring(next_admin_menu_page(page_index, total_pages)),
            label = "Next",
        })
    end

    append_sequential(entries, { token = "back", label = "Back" })
    append_sequential(entries, { token = "close", label = "Close" })

    return make_admin_menu_screen(
        "Select target",
        "Page " .. tostring(page_index) .. "/" .. tostring(total_pages),
        state.breadcrumb or "Admin Menu",
        entries)
end

local function build_admin_menu_value_screen(state)
    local options = get_admin_menu_value_options(state.actionKind)
    local page_items, page_index, total_pages = paginate_admin_menu_items(options, state.page or 1, 4)
    local entries = {}
    for index = 1, get_sequential_count(page_items) do
        local value = tostring(page_items[index])
        append_sequential(entries, {
            token = "value:" .. value,
            label = value,
        })
    end

    if total_pages > 1 then
        append_sequential(entries, {
            token = "page:" .. tostring(next_admin_menu_page(page_index, total_pages)),
            label = "Next",
        })
    end

    append_sequential(entries, { token = "back", label = "Back" })
    if get_sequential_count(entries) < 6 then
        append_sequential(entries, { token = "close", label = "Close" })
    end

    return make_admin_menu_screen(
        "Select " .. string.lower(get_admin_menu_action_title(state.actionKind) or "value"),
        "Page " .. tostring(page_index) .. "/" .. tostring(total_pages),
        state.breadcrumb or "Admin Menu",
        entries)
end

local function build_admin_menu_duration_screen(state)
    local entries = {}
    if state.includeInfinite then
        append_sequential(entries, { token = "duration:0", label = "Infinite" })
    end
    append_sequential(entries, { token = "duration:180", label = "3 min" })
    append_sequential(entries, { token = "duration:30", label = "30 seconds" })
    append_sequential(entries, { token = "duration:10", label = "10 seconds" })
    append_sequential(entries, { token = "back", label = "Back" })
    append_sequential(entries, { token = "close", label = "Close" })
    return make_admin_menu_screen(
        "Duration",
        "Pick how long the effect should last.",
        state.breadcrumb or "Admin Menu",
        entries)
end

local function build_admin_menu_screen_for_session(session)
    local state = session.state
    if state.screen == admin_menu_root_screen then
        return make_admin_menu_screen("Admin Menu", "Choose a branch.", "", list(
            menu_entry("nav:server_management", "Server management"),
            menu_entry("nav:game_management", "Game management"),
            menu_entry("nav:player_management", "Player management"),
            menu_entry("nav:fun", "Fun"),
            menu_entry("close", "Close")))
    end
    if state.screen == "server_management" then
        return build_admin_menu_branch_screen("server_management", state.page)
    end
    if state.screen == "game_management" then
        return build_admin_menu_branch_screen("game_management", state.page)
    end
    if state.screen == "player_management" then
        return build_admin_menu_branch_screen("player_management", state.page)
    end
    if state.screen == "fun" then
        return make_admin_menu_screen("Fun", "Choose an effect branch.", "Fun", list(
            menu_entry("nav:fun_player_effects", "Player effects"),
            menu_entry("nav:fun_game_effects", "Game effects"),
            menu_entry("back", "Back"),
            menu_entry("close", "Close")))
    end
    if state.screen == "fun_player_effects" then
        return make_admin_menu_screen("Player effects", "Choose a player effect branch.", "Fun > Player effects", list(
            menu_entry("nav:fun_player_modifiers", "Modifiers"),
            menu_entry("nav:fun_player_status", "Status effects"),
            menu_entry("back", "Back"),
            menu_entry("close", "Close")))
    end
    if state.screen == "fun_game_effects" then
        return make_admin_menu_screen("Game effects", "Whole-match effect presets.", "Fun > Game effects", list(
            menu_entry("action:earthquake_all", "Earthquake (all players)"),
            menu_entry("action:blind_all", "Blind (all players)"),
            menu_entry("back", "Back"),
            menu_entry("close", "Close")))
    end
    if state.screen == "fun_player_modifiers" then
        return make_admin_menu_screen("Modifiers", "Choose a modifier.", "Fun > Player effects > Modifiers", list(
            menu_entry("action:scale", "Scale"),
            menu_entry("action:gravity", "Gravity"),
            menu_entry("action:speed", "Movement speed"),
            menu_entry("back", "Back"),
            menu_entry("close", "Close")))
    end
    if state.screen == "fun_player_status" then
        return make_admin_menu_screen("Status effects", "Choose a status effect.", "Fun > Player effects > Status effects", list(
            menu_entry("action:blind", "Blind"),
            menu_entry("action:burn", "Burn"),
            menu_entry("action:earthquake", "Earthquake"),
            menu_entry("back", "Back"),
            menu_entry("close", "Close")))
    end
    if state.screen == "select_target" then
        return build_admin_menu_target_screen(state)
    end
    if state.screen == "select_value" then
        return build_admin_menu_value_screen(state)
    end
    if state.screen == "select_duration" then
        return build_admin_menu_duration_screen(state)
    end
    if state.screen == "command_category" then
        return build_admin_menu_command_category_screen(state)
    end
    if state.screen == "command_detail" then
        return build_admin_menu_command_detail_screen(state)
    end

    return make_admin_menu_screen("Admin Menu", "Unknown menu state.", "", list(
        menu_entry("close", "Close")))
end

local function refresh_admin_menu_session(session)
    local screen = build_admin_menu_screen_for_session(session)
    local payload = build_admin_menu_payload(screen)
    plugin.host.send_message_to_client(
        session.slot,
        admin_menu_client_plugin_id,
        admin_menu_open_message_type,
        payload)
end

local function begin_admin_menu_action(session, action_kind)
    local title = get_admin_menu_action_title(action_kind)
    local breadcrumb = get_admin_menu_action_breadcrumb(action_kind)
    if action_kind == "kick" or action_kind == "slay" or action_kind == "gag"
        or action_kind == "scale" or action_kind == "speed" or action_kind == "gravity"
        or action_kind == "blind" or action_kind == "burn" or action_kind == "earthquake" then
        push_admin_menu_state(session, {
            screen = "select_target",
            actionKind = action_kind,
            title = title,
            breadcrumb = breadcrumb,
            page = 1,
        })
        return
    end

    if action_kind == "blind_all" or action_kind == "earthquake_all" then
        push_admin_menu_state(session, {
            screen = "select_duration",
            actionKind = action_kind,
            title = title,
            breadcrumb = breadcrumb,
            includeInfinite = false,
            targetText = "@all",
        })
        return
    end
end

local function process_admin_menu_selection(session, payload)
    local token = trim(payload)
    if token == "" then
        return
    end

    if starts_with(token, "select:") then
        local selected_index = tonumber(token:sub(8))
        if selected_index == nil then
            return
        end

        local screen = build_admin_menu_screen_for_session(session)
        local entries = screen ~= nil and screen.entries or {}
        local selected_entry = entries[selected_index]
        if selected_entry == nil or selected_entry.token == nil then
            return
        end

        token = selected_entry.token
    end

    if token == "close" then
        close_admin_menu_session(session.slot)
        return
    end

    if token == "back" then
        if not pop_admin_menu_state(session) then
            close_admin_menu_session(session.slot)
            return
        end

        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "page:") then
        local next_page = tonumber(token:sub(6))
        if next_page ~= nil then
            session.state.page = next_page
        end
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "nav:") then
        local destination = token:sub(5)
        push_admin_menu_state(session, { screen = destination })
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "category:") then
        local category_payload = token:sub(10)
        local separator_index = string.find(category_payload, "|", 1, true)
        if separator_index == nil then
            return
        end

        local branch_id = category_payload:sub(1, separator_index - 1)
        local category = category_payload:sub(separator_index + 1)
        push_admin_menu_state(session, {
            screen = "command_category",
            branchId = branch_id,
            category = category,
            page = 1,
        })
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "info:") then
        send_private(session.slot, "[GT] Map selection is not wired into the admin menu yet. Use !gt_map or !gt_nextmap for now.")
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "showinfo:") then
        local spec = find_command_spec_by_name(token:sub(10))
        if spec ~= nil then
            send_command_detail(session.slot, spec)
        end
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "execute:") then
        execute_admin_menu_action(session, token:sub(9), nil, nil, nil)
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "command:") then
        local command_name = token:sub(9)
        local spec = find_command_spec_by_name(command_name)
        if spec == nil then
            return
        end

        local menu_mode = get_admin_menu_command_mode(spec)
        if menu_mode == "execute" then
            execute_admin_menu_action(session, spec.name, nil, nil, nil)
            refresh_admin_menu_session(session)
            return
        end
        if menu_mode == "action" then
            begin_admin_menu_action(session, spec.name)
            refresh_admin_menu_session(session)
            return
        end

        push_admin_menu_state(session, {
            screen = "command_detail",
            branchId = get_admin_menu_command_branch(spec),
            category = spec.category,
            commandName = spec.name,
        })
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "action:") then
        begin_admin_menu_action(session, token:sub(8))
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "target:") then
        local target_text = token:sub(8)
        local action_kind = session.state.actionKind
        if action_kind == "kick" or action_kind == "slay" or action_kind == "gag" then
            execute_admin_menu_action(session, action_kind, target_text, nil, nil)
            session.stack = {}
            session.state = { screen = admin_menu_root_screen }
            refresh_admin_menu_session(session)
            return
        end

        if action_kind == "scale" or action_kind == "speed" or action_kind == "gravity" then
            push_admin_menu_state(session, {
                screen = "select_value",
                actionKind = action_kind,
                targetText = target_text,
                breadcrumb = session.state.breadcrumb,
                page = 1,
            })
            refresh_admin_menu_session(session)
            return
        end

        if action_kind == "blind" or action_kind == "burn" or action_kind == "earthquake" then
            push_admin_menu_state(session, {
                screen = "select_duration",
                actionKind = action_kind,
                targetText = target_text,
                breadcrumb = session.state.breadcrumb,
                includeInfinite = false,
            })
            refresh_admin_menu_session(session)
            return
        end
    end

    if starts_with(token, "value:") then
        local selected_value = token:sub(7)
        push_admin_menu_state(session, {
            screen = "select_duration",
            actionKind = session.state.actionKind,
            targetText = session.state.targetText,
            selectedValue = selected_value,
            breadcrumb = session.state.breadcrumb,
            includeInfinite = true,
        })
        refresh_admin_menu_session(session)
        return
    end

    if starts_with(token, "duration:") then
        local duration_seconds = tonumber(token:sub(10)) or 0
        execute_admin_menu_action(
            session,
            session.state.actionKind,
            session.state.targetText,
            session.state.selectedValue,
            duration_seconds)
        session.stack = {}
        session.state = { screen = admin_menu_root_screen }
        refresh_admin_menu_session(session)
    end
end

local function handle_admin_menu(context, event)
    if event == nil or event.slot == nil then
        return false
    end

    local session = create_admin_menu_session(context, event.slot)
    admin_menu_sessions[event.slot] = session
    refresh_admin_menu_session(session)
    return true
end

function plugin.initialize(host)
    plugin.host = host
    config = load_config()
    host.log("GarrisonTools initialized")
end

function plugin.shutdown()
    active_effects = {}
    close_all_admin_menu_sessions(false)
end

function plugin.on_map_changing(e)
    clear_all_effects(true)
    close_all_admin_menu_sessions(true)
end

function plugin.on_client_disconnected(e)
    if e ~= nil and e.slot ~= nil then
        clear_effects_for_slot(e.slot, false)
        admin_menu_sessions[e.slot] = nil
    end
end

function plugin.on_client_plugin_message(e)
    local source_plugin_id = e.sourcePluginId or e.SourcePluginId or e.source_plugin_id or ""
    local message_type = e.messageType or e.MessageType or e.message_type or ""
    local payload = e.payload or e.Payload or ""
    local source_slot = e.sourceSlot or e.SourceSlot or e.source_slot
    if source_plugin_id ~= admin_menu_client_source_plugin_id then
        return
    end

    if message_type ~= admin_menu_select_message_type then
        return
    end

    if source_slot == nil then
        return
    end

    local session = admin_menu_sessions[source_slot]
    if session == nil then
        return
    end

    process_admin_menu_selection(session, payload)
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
    elseif command_name == "bots" then
        return handle_bots(e, arguments)
    elseif command_name == "map" then
        return handle_map(e, arguments)
    elseif command_name == "nextmap" then
        return handle_nextmap(e, arguments)
    elseif command_name == "adminmenu" then
        return handle_admin_menu(context, e)
    end

    return false
end

return plugin
