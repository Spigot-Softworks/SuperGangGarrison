local plugin = {}

local SOURCE_PLUGIN_ID = "open-garrison.server.lua-garrison-tools"
local APPLY_MESSAGE_TYPE = "seffect.apply"
local CLEAR_MESSAGE_TYPE = "seffect.clear"

local blind_state = {
    remainingSeconds = 0.0,
    alpha = 220,
    innerRadiusPixels = 28.0,
}

local earthquake_state = {
    remainingSeconds = 0.0,
    totalSeconds = 0.0,
    elapsedSeconds = 0.0,
    amplitude = 10.0,
    frequency = 18.0,
}

local current_camera_offset = nil

local function clamp(value, minimum, maximum)
    if value < minimum then
        return minimum
    end
    if value > maximum then
        return maximum
    end
    return value
end

local function split_pipe(text)
    local parts = {}
    for part in string.gmatch(tostring(text or ""), "([^|]+)") do
        table.insert(parts, part)
    end
    return parts
end

local function parse_number(text, fallback)
    local value = tonumber(text)
    if value == nil then
        return fallback
    end
    return value
end

local function reset_blind()
    blind_state.remainingSeconds = 0.0
end

local function reset_earthquake()
    earthquake_state.remainingSeconds = 0.0
    earthquake_state.totalSeconds = 0.0
    earthquake_state.elapsedSeconds = 0.0
    earthquake_state.amplitude = 10.0
    earthquake_state.frequency = 18.0
    current_camera_offset = plugin.host.vec2(0.0, 0.0)
end

local function reset_all()
    reset_blind()
    reset_earthquake()
end

local function apply_blind(parts)
    local duration_seconds = clamp(parse_number(parts[2], 8.0), 0.05, 600.0)
    blind_state.remainingSeconds = duration_seconds
    blind_state.alpha = math.floor(clamp(parse_number(parts[3], 220.0), 0.0, 255.0))
    blind_state.innerRadiusPixels = clamp(parse_number(parts[4], 28.0), 6.0, 240.0)
end

local function apply_earthquake(parts)
    local duration_seconds = clamp(parse_number(parts[2], 6.0), 0.05, 600.0)
    earthquake_state.remainingSeconds = duration_seconds
    earthquake_state.totalSeconds = duration_seconds
    earthquake_state.elapsedSeconds = 0.0
    earthquake_state.amplitude = clamp(parse_number(parts[3], 10.0), 0.0, 64.0)
    earthquake_state.frequency = clamp(parse_number(parts[4], 18.0), 0.1, 60.0)
end

local function handle_apply_payload(payload)
    local parts = split_pipe(payload)
    local effect_id = string.lower(parts[1] or "")
    if effect_id == "blind" then
        apply_blind(parts)
    elseif effect_id == "earthquake" then
        apply_earthquake(parts)
    end
end

local function handle_clear_payload(payload)
    local effect_id = string.lower(tostring(payload or ""))
    if effect_id == "" or effect_id == "all" then
        reset_all()
        return
    end

    if effect_id == "blind" then
        reset_blind()
    elseif effect_id == "earthquake" then
        reset_earthquake()
    end
end

local function draw_blind_overlay(canvas, state)
    local color = { r = 0, g = 0, b = 0, a = blind_state.alpha }
    if blind_state.remainingSeconds <= 0.0 then
        return
    end

    local viewport_width = canvas.viewport_width or state.viewportWidth or 0
    local viewport_height = canvas.viewport_height or state.viewportHeight or 0
    if viewport_width <= 0 or viewport_height <= 0 then
        return
    end

    if not state.hasLocalPlayerPosition then
        canvas.fill_screen_rectangle(0, 0, viewport_width, viewport_height, color)
        return
    end

    local local_position = plugin.host.vec2(state.localPlayerWorldX, state.localPlayerWorldY)
    local screen_position = canvas.world_to_screen(local_position)
    local half_width = blind_state.innerRadiusPixels
    local half_height = blind_state.innerRadiusPixels * 1.4
    local hole_left = math.floor(clamp(screen_position.x - half_width, 0.0, viewport_width))
    local hole_top = math.floor(clamp(screen_position.y - half_height, 0.0, viewport_height))
    local hole_right = math.floor(clamp(screen_position.x + half_width, 0.0, viewport_width))
    local hole_bottom = math.floor(clamp(screen_position.y + half_height, 0.0, viewport_height))

    if hole_top > 0 then
        canvas.fill_screen_rectangle(0, 0, viewport_width, hole_top, color)
    end
    if hole_left > 0 and hole_bottom > hole_top then
        canvas.fill_screen_rectangle(0, hole_top, hole_left, hole_bottom - hole_top, color)
    end
    if hole_right < viewport_width and hole_bottom > hole_top then
        canvas.fill_screen_rectangle(hole_right, hole_top, viewport_width - hole_right, hole_bottom - hole_top, color)
    end
    if hole_bottom < viewport_height then
        canvas.fill_screen_rectangle(0, hole_bottom, viewport_width, viewport_height - hole_bottom, color)
    end
end

function plugin.initialize(host)
    plugin.host = host
    current_camera_offset = host.vec2(0.0, 0.0)
end

function plugin.shutdown()
    reset_all()
end

function plugin.on_client_frame(e)
    if not e.isGameplayActive then
        reset_all()
        return
    end

    if blind_state.remainingSeconds > 0.0 then
        blind_state.remainingSeconds = math.max(0.0, blind_state.remainingSeconds - e.deltaSeconds)
    end

    if earthquake_state.remainingSeconds > 0.0 then
        earthquake_state.elapsedSeconds = earthquake_state.elapsedSeconds + e.deltaSeconds
        earthquake_state.remainingSeconds = math.max(0.0, earthquake_state.remainingSeconds - e.deltaSeconds)

        local duration_ratio = earthquake_state.totalSeconds > 0.0
            and (earthquake_state.remainingSeconds / earthquake_state.totalSeconds)
            or 0.0
        local amplitude = earthquake_state.amplitude * clamp(duration_ratio, 0.0, 1.0)
        local angle = earthquake_state.elapsedSeconds * earthquake_state.frequency * math.pi * 2.0
        current_camera_offset = plugin.host.vec2(
            math.sin(angle * 1.31) * amplitude,
            math.cos(angle * 1.73) * amplitude)

        if earthquake_state.remainingSeconds <= 0.0 then
            reset_earthquake()
        end
    else
        current_camera_offset = plugin.host.vec2(0.0, 0.0)
    end
end

function plugin.on_server_plugin_message(e)
    local source_plugin_id = e.sourcePluginId or e.SourcePluginId or e.source_plugin_id or ""
    local message_type = e.messageType or e.MessageType or e.message_type or e.messageTypeName or e.MessageTypeName or ""
    local payload = e.payload or e.Payload or ""
    if source_plugin_id ~= SOURCE_PLUGIN_ID then
        return
    end

    if message_type == APPLY_MESSAGE_TYPE then
        handle_apply_payload(payload)
    elseif message_type == CLEAR_MESSAGE_TYPE then
        handle_clear_payload(payload)
    end
end

function plugin.get_camera_offset()
    return current_camera_offset or plugin.host.vec2(0.0, 0.0)
end

function plugin.on_gameplay_hud_draw(canvas)
    local state = plugin.host.get_client_state()
    if not state.isGameplayActive then
        return
    end

    draw_blind_overlay(canvas, state)
end

return plugin
