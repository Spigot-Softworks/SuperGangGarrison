local plugin = {}

local SOURCE_PLUGIN_ID = "open-garrison.server.lua-garrison-tools"
local TARGET_PLUGIN_ID = "open-garrison.server.lua-garrison-tools"
local OPEN_MESSAGE_TYPE = "adminmenu.open"
local CLOSE_MESSAGE_TYPE = "adminmenu.close"
local SELECT_MESSAGE_TYPE = "adminmenu.select"
local MAX_VISIBLE_ENTRIES = 6
local PANEL_MARGIN_LEFT = 18
local PANEL_BOTTOM_OFFSET = 188
local PANEL_WIDTH = 360
local PANEL_PADDING_X = 8
local PANEL_PADDING_Y = 6
local PANEL_LINE_SPACING = 3
local PANEL_BACKGROUND = { r = 0, g = 0, b = 0, a = 220 }
local PANEL_BORDER = { r = 49, g = 45, b = 26, a = 220 }
local TITLE_COLOR = { r = 255, g = 245, b = 210, a = 255 }
local SUBTITLE_COLOR = { r = 220, g = 220, b = 220, a = 255 }
local BREADCRUMB_COLOR = { r = 196, g = 182, b = 126, a = 255 }
local ENTRY_COLOR = { r = 235, g = 235, b = 235, a = 255 }
local FOOTER_COLOR = { r = 180, g = 180, b = 180, a = 255 }

local menu_state = {
    isVisible = false,
    title = "",
    subtitle = "",
    breadcrumb = "",
    entries = {},
}

local hotkeys = {
    { id = "adminmenu-slot-1", key = "D1" },
    { id = "adminmenu-slot-2", key = "D2" },
    { id = "adminmenu-slot-3", key = "D3" },
    { id = "adminmenu-slot-4", key = "D4" },
    { id = "adminmenu-slot-5", key = "D5" },
    { id = "adminmenu-slot-6", key = "D6" },
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
    value = value:gsub("%%09", "\t")
    value = value:gsub("%%0D", "\r")
    value = value:gsub("%%0A", "\n")
    value = value:gsub("%%25", "%%")
    return value
end

local function split_token_line(text)
    local separator_index = string.find(text, "\t", 1, true)
    if separator_index == nil then
        return text, text
    end

    return string.sub(text, 1, separator_index - 1), string.sub(text, separator_index + 1)
end

local function reset_menu_state()
    menu_state.isVisible = false
    menu_state.title = ""
    menu_state.subtitle = ""
    menu_state.breadcrumb = ""
    menu_state.entries = {}
end

local function apply_open_payload(payload)
    local lines = split_lines(payload)
    menu_state.isVisible = true
    menu_state.title = unescape_value(lines[1] or "Admin Menu")
    menu_state.subtitle = unescape_value(lines[2] or "")
    menu_state.breadcrumb = unescape_value(lines[3] or "")
    menu_state.entries = {}

    for index = 4, #lines do
        local token_text, label_text = split_token_line(lines[index])
        if token_text ~= nil and token_text ~= "" and #menu_state.entries < MAX_VISIBLE_ENTRIES then
            menu_state.entries[#menu_state.entries + 1] = {
                token = unescape_value(token_text),
                label = unescape_value(label_text),
            }
        end
    end
end

local function get_message_field(e, lower_name, upper_name)
    return e[lower_name] or e[upper_name] or e[string.lower(lower_name)] or ""
end

local function send_selection(token)
    plugin.host.send_message_to_server(TARGET_PLUGIN_ID, SELECT_MESSAGE_TYPE, trim(token))
end

local function send_selection_for_index(index)
    local entry = menu_state.entries[index]
    if entry == nil or entry.token == nil or trim(entry.token) == "" then
        return false
    end

    send_selection(entry.token)
    return true
end

local function measure_menu_height(canvas)
    local line_height = canvas.measure_bitmap_text_height(1.0)
    local row_count = 2 + #menu_state.entries
    if menu_state.breadcrumb ~= "" then
        row_count = row_count + 1
    end
    row_count = row_count + 1
    return (PANEL_PADDING_Y * 2)
        + (row_count * line_height)
        + ((row_count - 1) * PANEL_LINE_SPACING)
end

local function draw_line(canvas, text, x, y, color)
    if text == nil or text == "" then
        return y
    end

    canvas.draw_bitmap_text(text, x, y, color, 1.0)
    return y + canvas.measure_bitmap_text_height(1.0) + PANEL_LINE_SPACING
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
    local source_plugin_id = get_message_field(e, "sourcePluginId", "SourcePluginId")
    local message_type = get_message_field(e, "messageType", "MessageType")
    local payload = e.payload or e.Payload or ""
    if source_plugin_id ~= SOURCE_PLUGIN_ID then
        return
    end

    if message_type == OPEN_MESSAGE_TYPE then
        apply_open_payload(payload)
    elseif message_type == CLOSE_MESSAGE_TYPE then
        reset_menu_state()
    end
end

function plugin.on_client_frame(e)
    if not menu_state.isVisible then
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

function plugin.on_gameplay_hud_draw(canvas)
    if not menu_state.isVisible then
        return
    end

    local panel_height = measure_menu_height(canvas)
    local panel_x = PANEL_MARGIN_LEFT
    local panel_y = math.max(12, canvas.viewport_height - PANEL_BOTTOM_OFFSET - panel_height)
    local panel_width = math.min(PANEL_WIDTH, math.max(280, canvas.viewport_width - (PANEL_MARGIN_LEFT * 2)))

    canvas.fill_screen_rectangle(panel_x - 6, panel_y - 4, panel_width, panel_height, PANEL_BACKGROUND)
    canvas.draw_screen_rectangle_outline(panel_x - 6, panel_y - 4, panel_width, panel_height, PANEL_BORDER, 1)

    local text_x = panel_x + PANEL_PADDING_X
    local text_y = panel_y + PANEL_PADDING_Y
    text_y = draw_line(canvas, menu_state.title, text_x, text_y, TITLE_COLOR)

    if menu_state.breadcrumb ~= "" then
        text_y = draw_line(canvas, menu_state.breadcrumb, text_x, text_y, BREADCRUMB_COLOR)
    end

    text_y = draw_line(canvas, menu_state.subtitle, text_x, text_y, SUBTITLE_COLOR)

    for index = 1, math.min(MAX_VISIBLE_ENTRIES, #menu_state.entries) do
        local entry = menu_state.entries[index]
        text_y = draw_line(canvas, tostring(index) .. ".) " .. tostring(entry.label or ""), text_x, text_y, ENTRY_COLOR)
    end

    draw_line(canvas, "Esc closes", text_x, text_y, FOOTER_COLOR)
end

return plugin
