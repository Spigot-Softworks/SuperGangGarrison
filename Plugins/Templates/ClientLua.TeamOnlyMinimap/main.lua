local plugin = {}

local OBJECTIVE_ATTACK_OVERLAY = { r = 80, g = 220, b = 110, a = 255 }
local OBJECTIVE_DEFEND_OVERLAY = { r = 220, g = 80, b = 80, a = 255 }
local WHITE = { r = 255, g = 255, b = 255, a = 255 }
local BLACK = { r = 0, g = 0, b = 0, a = 255 }
local RED = { r = 255, g = 0, b = 0, a = 255 }
local FALLBACK_BACKGROUND = { r = 34, g = 44, b = 60, a = 255 }

local SHOW_METHODS = { "Dots", "BigDots", "ClassBubbles" }
local FIT_MODES = { "Auto", "Width", "Height", "Reverse" }
local PLAYER_SCOPES = { "None", "Myself", "Allies" }
local SELF_COLORS = { "Red", "Yellow", "Green", "Blue", "White" }

local default_config = {
    enabled = true,
    showHealth = true,
    showObjective = true,
    showSentry = true,
    positionX = 0,
    positionY = 0,
    width = 200,
    height = 200,
    healingPositionX = 0,
    healingPositionY = 0,
    healingWidth = 200,
    healingHeight = 200,
    showMethod = "ClassBubbles",
    fitMode = "Auto",
    playersShown = "Allies",
    bubbleSizePercent = 40,
    selfColor = "Green",
    selfBubbleSizePercent = 40,
    alphaPercent = 100,
    objectiveBubbleSizePercent = 80,
    zoomKey = "R",
    zoomRangeTenths = 20,
    moveNearHealingHud = false
}

local config = default_config
local zooming = false
local zoom_hotkey_id = "zoom-toggle"

local function clamp(value, minimum, maximum)
    if value < minimum then
        return minimum
    end
    if value > maximum then
        return maximum
    end
    return value
end

local function cycle_option(options, current)
    for index, value in ipairs(options) do
        if value == current then
            return options[(index % #options) + 1]
        end
    end
    return options[1]
end

local function contains_option(options, value, fallback)
    for _, option in ipairs(options) do
        if option == value then
            return option
        end
    end
    return fallback
end

local function with_alpha(color, alpha)
    return {
        r = color.r,
        g = color.g,
        b = color.b,
        a = math.floor((color.a or 255) * clamp(alpha, 0.0, 1.0))
    }
end

local function lerp_color(left, right, amount)
    local t = clamp(amount, 0.0, 1.0)
    return {
        r = math.floor(left.r + ((right.r - left.r) * t)),
        g = math.floor(left.g + ((right.g - left.g) * t)),
        b = math.floor(left.b + ((right.b - left.b) * t)),
        a = math.floor((left.a or 255) + (((right.a or 255) - (left.a or 255)) * t))
    }
end

local function normalize_config(loaded)
    return {
        enabled = loaded.enabled ~= false,
        showHealth = loaded.showHealth ~= false,
        showObjective = loaded.showObjective ~= false,
        showSentry = loaded.showSentry ~= false,
        positionX = clamp(tonumber(loaded.positionX) or default_config.positionX, 0, 4000),
        positionY = clamp(tonumber(loaded.positionY) or default_config.positionY, 0, 4000),
        width = clamp(tonumber(loaded.width) or default_config.width, 20, 1000),
        height = clamp(tonumber(loaded.height) or default_config.height, 20, 1000),
        healingPositionX = clamp(tonumber(loaded.healingPositionX) or default_config.healingPositionX, 0, 4000),
        healingPositionY = clamp(tonumber(loaded.healingPositionY) or default_config.healingPositionY, 0, 4000),
        healingWidth = clamp(tonumber(loaded.healingWidth) or default_config.healingWidth, 20, 1000),
        healingHeight = clamp(tonumber(loaded.healingHeight) or default_config.healingHeight, 20, 1000),
        showMethod = contains_option(SHOW_METHODS, loaded.showMethod, default_config.showMethod),
        fitMode = contains_option(FIT_MODES, loaded.fitMode, default_config.fitMode),
        playersShown = contains_option(PLAYER_SCOPES, loaded.playersShown, default_config.playersShown),
        bubbleSizePercent = clamp(tonumber(loaded.bubbleSizePercent) or default_config.bubbleSizePercent, 10, 200),
        selfColor = contains_option(SELF_COLORS, loaded.selfColor, default_config.selfColor),
        selfBubbleSizePercent = clamp(tonumber(loaded.selfBubbleSizePercent) or default_config.selfBubbleSizePercent, 10, 200),
        alphaPercent = clamp(tonumber(loaded.alphaPercent) or default_config.alphaPercent, 0, 100),
        objectiveBubbleSizePercent = clamp(tonumber(loaded.objectiveBubbleSizePercent) or default_config.objectiveBubbleSizePercent, 10, 200),
        zoomKey = loaded.zoomKey or default_config.zoomKey,
        zoomRangeTenths = clamp(tonumber(loaded.zoomRangeTenths) or default_config.zoomRangeTenths, 2, 50),
        moveNearHealingHud = loaded.moveNearHealingHud == true
    }
end

local function save_config()
    plugin.host.save_json_config("teamonlyminimap.json", config)
end

local function refresh_zoom_hotkey()
    local registered = plugin.host.register_hotkey(zoom_hotkey_id, "Zoom toggle key", config.zoomKey)
    if registered ~= nil and registered ~= "" then
        config.zoomKey = registered
        save_config()
    end
end

local function load_config()
    config = normalize_config(plugin.host.load_json_config("teamonlyminimap.json", default_config))
    save_config()
    refresh_zoom_hotkey()
end

local function toggle_from_menu()
    config.enabled = not config.enabled
    save_config()
    plugin.host.show_notice = plugin.host.show_notice or function() end
end

local function get_alpha()
    return config.alphaPercent / 100.0
end

local function get_marker_color(name)
    if name == "Red" then
        return RED
    elseif name == "Yellow" then
        return { r = 255, g = 255, b = 0, a = 255 }
    elseif name == "Blue" then
        return { r = 0, g = 0, b = 255, a = 255 }
    elseif name == "White" then
        return WHITE
    end

    return { r = 50, g = 205, b = 50, a = 255 }
end

local function format_percent(value)
    return tostring(value) .. "%"
end

local function format_zoom(value)
    return string.format("%.1fx", value / 10.0)
end

local function format_key_label()
    return plugin.host.format_key_display_name(config.zoomKey)
end

local function resolve_layout(state)
    if config.moveNearHealingHud and state.isLocalPlayerHealing then
        return {
            x = config.healingPositionX,
            y = config.healingPositionY,
            width = config.healingWidth,
            height = config.healingHeight
        }
    end

    return {
        x = config.positionX,
        y = config.positionY,
        width = config.width,
        height = config.height
    }
end

local function clamp_viewport_origin(requested, world_size, visible_size)
    return clamp(requested, 0.0, math.max(0.0, world_size - visible_size))
end

local function resolve_view(state, local_player_world_position, layout)
    local world_width = math.max(1.0, state.levelWidth)
    local world_height = math.max(1.0, state.levelHeight)
    local layout_width = math.max(20.0, layout.width)
    local layout_height = math.max(20.0, layout.height)
    local x_scale = layout_width / world_width
    local y_scale = layout_height / world_height

    local visible_world_width
    local visible_world_height
    local scale
    local left
    local top

    if zooming then
        local zoom_factor = math.max(0.2, config.zoomRangeTenths / 10.0)
        visible_world_width = math.max(1.0, world_width / zoom_factor)
        visible_world_height = math.max(1.0, world_height / zoom_factor)
        scale = math.min(layout_width / visible_world_width, layout_height / visible_world_height)
        left = clamp_viewport_origin(local_player_world_position.x - (visible_world_width / 2.0), world_width, visible_world_width)
        top = clamp_viewport_origin(local_player_world_position.y - (visible_world_height / 2.0), world_height, visible_world_height)
    elseif config.fitMode == "Width" then
        scale = x_scale
        visible_world_width = world_width
        visible_world_height = math.min(world_height, layout_height / math.max(scale, 0.0001))
        left = 0.0
        top = clamp_viewport_origin(local_player_world_position.y - (visible_world_height / 2.0), world_height, visible_world_height)
    elseif config.fitMode == "Height" then
        scale = y_scale
        visible_world_width = math.min(world_width, layout_width / math.max(scale, 0.0001))
        visible_world_height = world_height
        left = clamp_viewport_origin(local_player_world_position.x - (visible_world_width / 2.0), world_width, visible_world_width)
        top = 0.0
    elseif config.fitMode == "Reverse" then
        scale = math.max(x_scale, y_scale)
        visible_world_width = math.min(world_width, layout_width / math.max(scale, 0.0001))
        visible_world_height = math.min(world_height, layout_height / math.max(scale, 0.0001))
        left = clamp_viewport_origin(local_player_world_position.x - (visible_world_width / 2.0), world_width, visible_world_width)
        top = clamp_viewport_origin(local_player_world_position.y - (visible_world_height / 2.0), world_height, visible_world_height)
    else
        scale = math.min(x_scale, y_scale)
        visible_world_width = world_width
        visible_world_height = world_height
        left = 0.0
        top = 0.0
    end

    return {
        bounds = {
            x = math.floor(layout.x + 0.5),
            y = math.floor(layout.y + 0.5),
            width = math.max(1, math.floor((visible_world_width * scale) + 0.5)),
            height = math.max(1, math.floor((visible_world_height * scale) + 0.5))
        },
        left = left,
        top = top,
        visibleWorldWidth = visible_world_width,
        visibleWorldHeight = visible_world_height,
        scale = scale
    }
end

local function try_project_to_screen(view, world_position)
    if world_position.x < view.left
        or world_position.x > view.left + view.visibleWorldWidth
        or world_position.y < view.top
        or world_position.y > view.top + view.visibleWorldHeight then
        return nil
    end

    return plugin.host.vec2(
        view.bounds.x + ((world_position.x - view.left) * view.scale),
        view.bounds.y + ((world_position.y - view.top) * view.scale))
end

local function get_player_bubble_frame(class_id, team)
    local team_offset = team == "Blue" and 10 or 0
    if class_id == "Scout" then
        return 0 + team_offset
    elseif class_id == "Pyro" then
        return 1 + team_offset
    elseif class_id == "Soldier" then
        return 2 + team_offset
    elseif class_id == "Heavy" then
        return 3 + team_offset
    elseif class_id == "Demoman" then
        return 4 + team_offset
    elseif class_id == "Medic" then
        return 5 + team_offset
    elseif class_id == "Engineer" then
        return 6 + team_offset
    elseif class_id == "Spy" then
        return team == "Blue" and 17 or 7
    elseif class_id == "Sniper" then
        return 8 + team_offset
    elseif class_id == "Quote" then
        return team == "Blue" and 48 or 47
    end

    return team == "Blue" and 10 or 0
end

local function draw_dot(canvas, position, size, color, alpha, big)
    local pixel_size = big and math.max(4, math.floor((8.0 * size) + 0.5)) or math.max(2, math.floor((4.0 * size) + 0.5))
    local rectangle = {
        x = math.floor(position.x - (pixel_size / 2.0) + 0.5),
        y = math.floor(position.y - (pixel_size / 2.0) + 0.5),
        width = pixel_size,
        height = pixel_size
    }
    canvas.fill_screen_rectangle(rectangle.x, rectangle.y, rectangle.width, rectangle.height, with_alpha(color, alpha))
    if big then
        canvas.draw_screen_rectangle_outline(rectangle.x, rectangle.y, rectangle.width, rectangle.height, with_alpha(BLACK, alpha * 0.35), 1)
    end
end

local function draw_marker(canvas, bubble_frame, position, size, base_color, overlay_color, overlay_amount)
    local alpha = get_alpha()
    local overlay = clamp(overlay_amount, 0.0, 1.0)
    if config.showMethod == "Dots" then
        draw_dot(canvas, position, size, lerp_color(base_color, overlay_color, overlay), alpha, false)
        return
    elseif config.showMethod == "BigDots" then
        draw_dot(canvas, position, size, lerp_color(base_color, overlay_color, overlay), alpha, true)
        return
    end

    if not canvas.draw_screen_sprite("BubblesS", bubble_frame, position.x, position.y, with_alpha(base_color, alpha), size, size) then
        draw_dot(canvas, position, size, base_color, alpha, true)
        return
    end

    if overlay > 0.01 then
        canvas.draw_screen_sprite("BubblesS", bubble_frame, position.x, position.y, with_alpha(overlay_color, overlay * alpha), size, size)
    end
end

local function draw_background(canvas, state, view)
    local alpha = get_alpha()
    canvas.fill_screen_rectangle(view.bounds.x, view.bounds.y, view.bounds.width, view.bounds.height, with_alpha(WHITE, alpha * 0.2))
    if not canvas.draw_level_background_view(
        view.bounds.x,
        view.bounds.y,
        view.bounds.width,
        view.bounds.height,
        view.left,
        view.top,
        view.visibleWorldWidth,
        view.visibleWorldHeight,
        state.levelWidth,
        state.levelHeight,
        with_alpha(WHITE, alpha * 0.75)) then
        canvas.fill_screen_rectangle(view.bounds.x, view.bounds.y, view.bounds.width, view.bounds.height, with_alpha(FALLBACK_BACKGROUND, alpha))
    end
    canvas.draw_screen_rectangle_outline(view.bounds.x, view.bounds.y, view.bounds.width, view.bounds.height, with_alpha(BLACK, alpha), 1)
end

local function draw_player_markers(canvas, state, view)
    local local_team = state.localPlayerTeam
    for _, marker in ipairs(state.playerMarkers or {}) do
        if marker.team == local_team and (config.playersShown ~= "Myself" or marker.isLocalPlayer) then
            local screen_position = try_project_to_screen(view, marker.worldPosition)
            if screen_position ~= nil then
                local frame_index = get_player_bubble_frame(marker.classId, marker.team)
                local base_color = marker.isLocalPlayer and get_marker_color(config.selfColor) or WHITE
                local size = (marker.isLocalPlayer and config.selfBubbleSizePercent or config.bubbleSizePercent) / 100.0
                local damage_ratio = config.showHealth and (1.0 - (marker.health / math.max(1, marker.maxHealth))) or 0.0
                draw_marker(canvas, frame_index, screen_position, size, base_color, RED, damage_ratio)
            end
        end
    end
end

local function draw_sentry_markers(canvas, state, view)
    local local_team = state.localPlayerTeam
    local local_player_id = state.localPlayerId
    for _, marker in ipairs(state.sentryMarkers or {}) do
        if marker.team == local_team then
            local is_local_owned = state.hasLocalPlayerId and marker.ownerPlayerId == local_player_id
            if config.playersShown ~= "Myself" or is_local_owned then
                local screen_position = try_project_to_screen(view, marker.worldPosition)
                if screen_position ~= nil then
                    local base_color = is_local_owned and get_marker_color(config.selfColor) or WHITE
                    local size = (is_local_owned and config.selfBubbleSizePercent or config.bubbleSizePercent) / 100.0
                    local damage_ratio = config.showHealth and (1.0 - (marker.health / math.max(1, marker.maxHealth))) or 0.0
                    draw_marker(canvas, 31, screen_position, size, base_color, RED, damage_ratio)
                end
            end
        end
    end
end

local function draw_objective_markers(canvas, state, view)
    local local_team = state.localPlayerTeam
    local size = config.objectiveBubbleSizePercent / 100.0
    for _, marker in ipairs(state.objectiveMarkers or {}) do
        local screen_position = try_project_to_screen(view, marker.worldPosition)
        if screen_position ~= nil then
            if marker.kind == "Attack" then
                draw_marker(canvas, 41, screen_position, size, WHITE, WHITE, 0.0)
            elseif marker.kind == "Defend" then
                draw_marker(canvas, 42, screen_position, size, WHITE, WHITE, 0.0)
            elseif marker.kind == "ControlPoint" then
                if not marker.isLocked then
                    local is_local = marker.team == local_team
                    draw_marker(canvas, is_local and 42 or 41, screen_position, size, WHITE, is_local and OBJECTIVE_DEFEND_OVERLAY or OBJECTIVE_ATTACK_OVERLAY, clamp(marker.progress, 0.0, 1.0))
                end
            elseif marker.kind == "Generator" then
                draw_marker(canvas, marker.team == local_team and 42 or 41, screen_position, size, WHITE, WHITE, 0.0)
            end
        end
    end
end

function plugin.initialize(host)
    plugin.host = host
    load_config()
    host.register_menu_entry("toggle-minimap", "Toggle Minimap", "InGameMenu", "toggle_minimap_from_menu")
end

function plugin.toggle_minimap_from_menu()
    config.enabled = not config.enabled
    save_config()
    plugin.host.show_notice(config.enabled and "Team minimap enabled." or "Team minimap disabled.", 160, false)
end

function plugin.on_client_frame(e)
    local state = plugin.host.get_client_state()
    if not e.isGameplayActive then
        return
    end

    if not config.enabled or state.isSpectator or state.isGameplayInputBlocked then
        return
    end

    if plugin.host.was_hotkey_pressed(zoom_hotkey_id) then
        zooming = not zooming
    end
end

function plugin.on_gameplay_hud_draw(canvas)
    local state = plugin.host.get_client_state()
    if not config.enabled
        or not state.isGameplayActive
        or state.isSpectator
        or state.localPlayerTeam == "None"
        or state.levelWidth <= 1.0
        or state.levelHeight <= 1.0
        or not state.hasLocalPlayerPosition then
        return
    end

    local local_player_world_position = plugin.host.vec2(state.localPlayerWorldX, state.localPlayerWorldY)
    local layout = resolve_layout(state)
    local view = resolve_view(state, local_player_world_position, layout)
    if view.bounds.width <= 0 or view.bounds.height <= 0 then
        return
    end

    draw_background(canvas, state, view)
    if config.playersShown == "None" then
        return
    end

    draw_player_markers(canvas, state, view)
    if config.showSentry then
        draw_sentry_markers(canvas, state, view)
    end
    if config.showObjective then
        draw_objective_markers(canvas, state, view)
    end
end

function plugin.get_options_sections()
    return {
        {
            title = "Team Only Minimap",
            items = {
                { kind = "boolean", label = "Minimap", get_value_label = "get_enabled_label", activate = "toggle_enabled" },
                { kind = "choice", label = "Players shown", get_value_label = "get_players_shown_label", activate = "advance_players_shown" },
                { kind = "boolean", label = "Show health", get_value_label = "get_show_health_label", activate = "toggle_show_health" },
                { kind = "boolean", label = "Show objective", get_value_label = "get_show_objective_label", activate = "toggle_show_objective" },
                { kind = "boolean", label = "Show sentries", get_value_label = "get_show_sentry_label", activate = "toggle_show_sentry" },
                { kind = "choice", label = "Show method", get_value_label = "get_show_method_label", activate = "advance_show_method" },
                { kind = "choice", label = "Fit method", get_value_label = "get_fit_mode_label", activate = "advance_fit_mode" },
                { kind = "key", label = "Zoom toggle key", get_value_label = "get_zoom_key_label", get_key = "get_zoom_key", set_key = "set_zoom_key" },
                { kind = "integer", label = "Zoom range", get_value_label = "get_zoom_range_label", activate = "advance_zoom_range" },
                { kind = "integer", label = "Opacity", get_value_label = "get_alpha_label", activate = "advance_alpha" },
                { kind = "boolean", label = "Move to Healing HUD", get_value_label = "get_move_near_healing_hud_label", activate = "toggle_move_near_healing_hud" }
            }
        },
        {
            title = "Layout",
            items = {
                { kind = "integer", label = "X position", get_value_label = "get_position_x_label", activate = "advance_position_x" },
                { kind = "integer", label = "Y position", get_value_label = "get_position_y_label", activate = "advance_position_y" },
                { kind = "integer", label = "Width", get_value_label = "get_width_label", activate = "advance_width" },
                { kind = "integer", label = "Height", get_value_label = "get_height_label", activate = "advance_height" },
                { kind = "integer", label = "Healing X position", get_value_label = "get_healing_position_x_label", activate = "advance_healing_position_x" },
                { kind = "integer", label = "Healing Y position", get_value_label = "get_healing_position_y_label", activate = "advance_healing_position_y" },
                { kind = "integer", label = "Healing width", get_value_label = "get_healing_width_label", activate = "advance_healing_width" },
                { kind = "integer", label = "Healing height", get_value_label = "get_healing_height_label", activate = "advance_healing_height" }
            }
        },
        {
            title = "Markers",
            items = {
                { kind = "integer", label = "Bubble size", get_value_label = "get_bubble_size_label", activate = "advance_bubble_size" },
                { kind = "integer", label = "My bubble size", get_value_label = "get_self_bubble_size_label", activate = "advance_self_bubble_size" },
                { kind = "choice", label = "My color", get_value_label = "get_self_color_label", activate = "advance_self_color" },
                { kind = "integer", label = "Objective bubble size", get_value_label = "get_objective_bubble_size_label", activate = "advance_objective_bubble_size" }
            }
        }
    }
end

function plugin.get_enabled_label() return config.enabled and "Enabled" or "Disabled" end
function plugin.toggle_enabled() config.enabled = not config.enabled; save_config() end
function plugin.get_players_shown_label() return config.playersShown end
function plugin.advance_players_shown() config.playersShown = cycle_option(PLAYER_SCOPES, config.playersShown); save_config() end
function plugin.get_show_health_label() return config.showHealth and "Yes" or "No" end
function plugin.toggle_show_health() config.showHealth = not config.showHealth; save_config() end
function plugin.get_show_objective_label() return config.showObjective and "Yes" or "No" end
function plugin.toggle_show_objective() config.showObjective = not config.showObjective; save_config() end
function plugin.get_show_sentry_label() return config.showSentry and "Yes" or "No" end
function plugin.toggle_show_sentry() config.showSentry = not config.showSentry; save_config() end
function plugin.get_show_method_label() return config.showMethod end
function plugin.advance_show_method() config.showMethod = cycle_option(SHOW_METHODS, config.showMethod); save_config() end
function plugin.get_fit_mode_label() return config.fitMode end
function plugin.advance_fit_mode() config.fitMode = cycle_option(FIT_MODES, config.fitMode); save_config() end
function plugin.get_zoom_key_label() return format_key_label() end
function plugin.get_zoom_key() return config.zoomKey end
function plugin.set_zoom_key(value) config.zoomKey = value; refresh_zoom_hotkey() end
function plugin.get_zoom_range_label() return format_zoom(config.zoomRangeTenths) end
function plugin.advance_zoom_range() config.zoomRangeTenths = config.zoomRangeTenths + 2; if config.zoomRangeTenths > 50 then config.zoomRangeTenths = 2 end; save_config() end
function plugin.get_alpha_label() return format_percent(config.alphaPercent) end
function plugin.advance_alpha() config.alphaPercent = config.alphaPercent + 5; if config.alphaPercent > 100 then config.alphaPercent = 0 end; save_config() end
function plugin.get_move_near_healing_hud_label() return config.moveNearHealingHud and "Yes" or "No" end
function plugin.toggle_move_near_healing_hud() config.moveNearHealingHud = not config.moveNearHealingHud; save_config() end
function plugin.get_position_x_label() return tostring(config.positionX) end
function plugin.advance_position_x() config.positionX = (config.positionX + 10) % 4010; if config.positionX > 4000 then config.positionX = 0 end; save_config() end
function plugin.get_position_y_label() return tostring(config.positionY) end
function plugin.advance_position_y() config.positionY = (config.positionY + 10) % 4010; if config.positionY > 4000 then config.positionY = 0 end; save_config() end
function plugin.get_width_label() return tostring(config.width) end
function plugin.advance_width() config.width = config.width + 20; if config.width > 800 then config.width = 20 end; save_config() end
function plugin.get_height_label() return tostring(config.height) end
function plugin.advance_height() config.height = config.height + 20; if config.height > 800 then config.height = 20 end; save_config() end
function plugin.get_healing_position_x_label() return tostring(config.healingPositionX) end
function plugin.advance_healing_position_x() config.healingPositionX = (config.healingPositionX + 10) % 4010; if config.healingPositionX > 4000 then config.healingPositionX = 0 end; save_config() end
function plugin.get_healing_position_y_label() return tostring(config.healingPositionY) end
function plugin.advance_healing_position_y() config.healingPositionY = (config.healingPositionY + 10) % 4010; if config.healingPositionY > 4000 then config.healingPositionY = 0 end; save_config() end
function plugin.get_healing_width_label() return tostring(config.healingWidth) end
function plugin.advance_healing_width() config.healingWidth = config.healingWidth + 20; if config.healingWidth > 800 then config.healingWidth = 20 end; save_config() end
function plugin.get_healing_height_label() return tostring(config.healingHeight) end
function plugin.advance_healing_height() config.healingHeight = config.healingHeight + 20; if config.healingHeight > 800 then config.healingHeight = 20 end; save_config() end
function plugin.get_bubble_size_label() return format_percent(config.bubbleSizePercent) end
function plugin.advance_bubble_size() config.bubbleSizePercent = config.bubbleSizePercent + 10; if config.bubbleSizePercent > 200 then config.bubbleSizePercent = 10 end; save_config() end
function plugin.get_self_bubble_size_label() return format_percent(config.selfBubbleSizePercent) end
function plugin.advance_self_bubble_size() config.selfBubbleSizePercent = config.selfBubbleSizePercent + 10; if config.selfBubbleSizePercent > 200 then config.selfBubbleSizePercent = 10 end; save_config() end
function plugin.get_self_color_label() return config.selfColor end
function plugin.advance_self_color() config.selfColor = cycle_option(SELF_COLORS, config.selfColor); save_config() end
function plugin.get_objective_bubble_size_label() return format_percent(config.objectiveBubbleSizePercent) end
function plugin.advance_objective_bubble_size() config.objectiveBubbleSizePercent = config.objectiveBubbleSizePercent + 10; if config.objectiveBubbleSizePercent > 200 then config.objectiveBubbleSizePercent = 10 end; save_config() end

return plugin
