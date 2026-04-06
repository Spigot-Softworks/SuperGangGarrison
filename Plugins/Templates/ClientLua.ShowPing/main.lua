local plugin = {}

local default_config = {
    positionX = 170,
    positionY = 560,
    sizeTenths = 10
}

local config = default_config

local function clamp(value, minimum, maximum)
    if value < minimum then
        return minimum
    end

    if value > maximum then
        return maximum
    end

    return value
end

local function load_config()
    local loaded = plugin.host.load_json_config("showping.json", default_config)
    local normalized = {
        positionX = clamp(tonumber(loaded.positionX) or default_config.positionX, 0, 4000),
        positionY = clamp(tonumber(loaded.positionY) or default_config.positionY, 0, 4000),
        sizeTenths = clamp(tonumber(loaded.sizeTenths) or default_config.sizeTenths, 10, 100)
    }
    plugin.host.save_json_config("showping.json", normalized)
    return normalized
end

local function save_config()
    plugin.host.save_json_config("showping.json", config)
end

local function get_display_state(ping)
    if ping < 0 then
        return "TIMEOUT", { r = 128, g = 128, b = 128, a = 255 }
    end

    if ping < 135 then
        return tostring(ping), { r = 0, g = 255, b = 0, a = 255 }
    end

    if ping < 275 then
        return tostring(ping), { r = 255, g = 255, b = 0, a = 255 }
    end

    return tostring(ping), { r = 255, g = 0, b = 0, a = 255 }
end

local function multiply_alpha(color, alpha)
    return {
        r = color.r,
        g = color.g,
        b = color.b,
        a = math.floor((color.a or 255) * alpha)
    }
end

function plugin.initialize(host)
    plugin.host = host
    config = load_config()
end

function plugin.on_gameplay_hud_draw(canvas)
    local state = plugin.host.get_client_state()
    if not state.isGameplayActive or not state.isConnected then
        return
    end

    local label, color = get_display_state(state.localPingMilliseconds)
    canvas.draw_bitmap_text_centered(label, config.positionX, config.positionY, color, config.sizeTenths / 10.0)
end

function plugin.get_scoreboard_panel_location()
    return "HeaderRight"
end

function plugin.get_scoreboard_panel_order()
    return 0
end

function plugin.on_scoreboard_draw(canvas, state)
    local client_state = plugin.host.get_client_state()
    if not client_state.isConnected then
        return
    end

    local label, color = get_display_state(client_state.localPingMilliseconds)
    local tinted = multiply_alpha(color, state.alpha)
    canvas.draw_bitmap_text_right_aligned(
        "Ping: " .. label,
        state.scoreboardBounds.right - 12,
        state.scoreboardBounds.y + 48,
        tinted,
        1.0)
end

function plugin.get_options_sections()
    return {
        {
            title = "Show Ping",
            items = {
                {
                    kind = "integer",
                    label = "Text size",
                    get_value_label = "get_size_label",
                    activate = "advance_size"
                },
                {
                    kind = "integer",
                    label = "X position",
                    get_value_label = "get_position_x_label",
                    activate = "advance_position_x"
                },
                {
                    kind = "integer",
                    label = "Y position",
                    get_value_label = "get_position_y_label",
                    activate = "advance_position_y"
                }
            }
        }
    }
end

function plugin.get_size_label()
    return string.format("%.1fx", config.sizeTenths / 10.0)
end

function plugin.get_position_x_label()
    return tostring(config.positionX)
end

function plugin.get_position_y_label()
    return tostring(config.positionY)
end

function plugin.advance_size()
    config.sizeTenths = config.sizeTenths + 1
    if config.sizeTenths > 100 then
        config.sizeTenths = 10
    end
    save_config()
end

function plugin.advance_position_x()
    config.positionX = config.positionX + 10
    if config.positionX > 2000 then
        config.positionX = 0
    end
    save_config()
end

function plugin.advance_position_y()
    config.positionY = config.positionY + 10
    if config.positionY > 2000 then
        config.positionY = 0
    end
    save_config()
end

return plugin
