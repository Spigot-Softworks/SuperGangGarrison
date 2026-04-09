local plugin = {}

local SOURCE_PLUGIN_ID = "open-garrison.server.lua-garrison-tools"
local TARGET_PLUGIN_ID = "open-garrison.server.lua-garrison-tools"
local OPEN_MESSAGE_TYPE = "adminmenu.open"
local CLOSE_MESSAGE_TYPE = "adminmenu.close"
local SELECT_MESSAGE_TYPE = "adminmenu.select"
local STRUCTURED_PAYLOAD_PREFIX = "am1"
local MAX_VISIBLE_ENTRIES = 6

local menu_state = {
    isVisible = false,
    title = "",
    subtitle = "",
    breadcrumb = "",
    entries = {},
    optionCount = 0,
}

local close_hotkey_id = "adminmenu-close"

local function trim(text)
    local normalized = tostring(text or ""):gsub("^%s+", "")
    normalized = normalized:gsub("%s+$", "")
    return normalized
end

local function split_lines(text)
    local lines = {}
    for line in string.gmatch(tostring(text or "") .. "\n", "([^\n]*)\n") do
        lines[#lines + 1] = line
    end
    return lines
end

local function unescape_value(text)
    local value = tostring(text or "")
    value = value:gsub("%%7E", "~")
    value = value:gsub("%%7C", "|")
    value = value:gsub("%%3D", "=")
    value = value:gsub("%%09", "\t")
    value = value:gsub("%%0D", "\r")
    value = value:gsub("%%0A", "\n")
    value = value:gsub("%%25", "%%")
    return value
end

local function reset_menu_state()
    menu_state.isVisible = false
    menu_state.title = ""
    menu_state.subtitle = ""
    menu_state.breadcrumb = ""
    menu_state.entries = {}
    menu_state.optionCount = 0
    if plugin.host ~= nil then
        plugin.host.hide_overlay_menu()
        plugin.host.clear_hotkey_capture()
    end
end

local function apply_open_payload(payload)
    local raw_payload = tostring(payload or "")
    menu_state.isVisible = true
    menu_state.title = unescape_value(string.match(raw_payload, "t=([^|]+)") or "Admin Menu")
    menu_state.subtitle = unescape_value(string.match(raw_payload, "s=([^|]*)") or "")
    menu_state.breadcrumb = unescape_value(string.match(raw_payload, "b=([^|]*)") or "")
    menu_state.entries = {}
    menu_state.optionCount = 0

    local search_start = 1
    while menu_state.optionCount < MAX_VISIBLE_ENTRIES do
        local _, label_end = string.find(raw_payload, "|l=", search_start, true)
        if label_end == nil then
            break
        end

        menu_state.optionCount = menu_state.optionCount + 1
        search_start = label_end + 1
    end

    if menu_state.optionCount == 0 then
        local lines = split_lines(raw_payload)
        for index = 4, #lines do
            if menu_state.optionCount >= MAX_VISIBLE_ENTRIES then
                break
            end

            local label_text = unescape_value(lines[index] or "")
            if label_text ~= "" then
                menu_state.optionCount = menu_state.optionCount + 1
            end
        end
    end

    plugin.host.show_overlay_menu(
        menu_state.title,
        menu_state.subtitle,
        menu_state.breadcrumb,
        raw_payload)
    plugin.host.capture_hotkey_input()
end

local function send_selection(token)
    plugin.host.send_message_to_server(TARGET_PLUGIN_ID, SELECT_MESSAGE_TYPE, trim(token))
end

local function send_selection_for_index(index)
    if index < 1 or index > menu_state.optionCount then
        return false
    end

    send_selection("select:" .. tostring(index))
    return true
end

function plugin.initialize(host)
    plugin.host = host
    host.register_hotkey("adminmenu-slot-1", "Admin Menu 1", "D1")
    host.register_hotkey("adminmenu-slot-2", "Admin Menu 2", "D2")
    host.register_hotkey("adminmenu-slot-3", "Admin Menu 3", "D3")
    host.register_hotkey("adminmenu-slot-4", "Admin Menu 4", "D4")
    host.register_hotkey("adminmenu-slot-5", "Admin Menu 5", "D5")
    host.register_hotkey("adminmenu-slot-6", "Admin Menu 6", "D6")
    host.register_hotkey(close_hotkey_id, "Admin Menu Close", "Escape")
end

function plugin.shutdown()
    reset_menu_state()
end

function plugin.on_server_plugin_message(e)
    local payload = e.payload or e.Payload or ""
    if payload ~= "" then
        apply_open_payload(payload)
    else
        reset_menu_state()
    end
end

function plugin.on_client_frame(e)
    if not menu_state.isVisible then
        if plugin.host ~= nil then
            plugin.host.clear_hotkey_capture()
        end
        return
    end

    if plugin.host.was_hotkey_pressed(close_hotkey_id) then
        reset_menu_state()
        send_selection("close")
        return
    end

    if plugin.host.was_hotkey_pressed("adminmenu-slot-1") and send_selection_for_index(1) then
        return
    end
    if plugin.host.was_hotkey_pressed("adminmenu-slot-2") and send_selection_for_index(2) then
        return
    end
    if plugin.host.was_hotkey_pressed("adminmenu-slot-3") and send_selection_for_index(3) then
        return
    end
    if plugin.host.was_hotkey_pressed("adminmenu-slot-4") and send_selection_for_index(4) then
        return
    end
    if plugin.host.was_hotkey_pressed("adminmenu-slot-5") and send_selection_for_index(5) then
        return
    end
    if plugin.host.was_hotkey_pressed("adminmenu-slot-6") and send_selection_for_index(6) then
        return
    end
end

return plugin
