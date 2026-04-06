local plugin = {}

local WORLD_INDICATOR_LIFETIME_SECONDS = 0.67
local HUD_INDICATOR_LIFETIME_SECONDS = 0.67
local ROLLUP_IDLE_SECONDS = 2.0
local WORLD_INDICATOR_RISE_SPEED = 150.0
local HUD_INDICATOR_RISE_SPEED = 150.0

local OFF_WHITE = { r = 217, g = 217, b = 183, a = 255 }
local WAREYA_GREEN = { r = 0, g = 255, b = 0, a = 255 }
local LORGAN_RED = { r = 255, g = 0, b = 0, a = 255 }
local AIRSHOT_YELLOW = { r = 255, g = 230, b = 64, a = 255 }

local default_config = {
    style = 0,
    playDing = true,
    moveCounterForHud = false,
    stereoDing = false
}

local config = default_config

local world_count = 0
local world_target_kind = {}
local world_target_entity_id = {}
local world_x = {}
local world_y = {}
local world_amount = {}
local world_airshot = {}
local world_age_seconds = {}
local world_y_offset = {}

local hud_count = 0
local hud_amount = {}
local hud_airshot = {}
local hud_age_seconds = {}
local hud_y_offset = {}

local rolling_damage = 0
local rolling_damage_timer_seconds = 0.0
local rolling_damage_airshot = false

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
    local loaded = plugin.host.load_json_config("damageindicator.json", default_config)
    local normalized = {
        style = clamp(tonumber(loaded.style) or default_config.style, 0, 1),
        playDing = loaded.playDing ~= false,
        moveCounterForHud = loaded.moveCounterForHud == true,
        stereoDing = loaded.stereoDing == true
    }
    plugin.host.save_json_config("damageindicator.json", normalized)
    return normalized
end

local function save_config()
    plugin.host.save_json_config("damageindicator.json", config)
end

local function clear_world_indicators()
    world_count = 0
    world_target_kind = {}
    world_target_entity_id = {}
    world_x = {}
    world_y = {}
    world_amount = {}
    world_airshot = {}
    world_age_seconds = {}
    world_y_offset = {}
end

local function clear_hud_indicators()
    hud_count = 0
    hud_amount = {}
    hud_airshot = {}
    hud_age_seconds = {}
    hud_y_offset = {}
end

local function reset_state()
    clear_world_indicators()
    clear_hud_indicators()
    rolling_damage = 0
    rolling_damage_timer_seconds = 0.0
    rolling_damage_airshot = false
end

local function add_hud_indicator(amount, airshot)
    hud_count = hud_count + 1
    hud_amount[hud_count] = amount
    hud_airshot[hud_count] = airshot
    hud_age_seconds[hud_count] = 0.0
    hud_y_offset[hud_count] = 0.0
end

local function alpha_color(color, alpha)
    return {
        r = color.r,
        g = color.g,
        b = color.b,
        a = math.floor((color.a or 255) * alpha)
    }
end

local function get_hud_anchor(y_offset)
    if config.moveCounterForHud then
        return plugin.host.vec2(64.0, 547.0 + y_offset), 2.0, true
    end

    return plugin.host.vec2(89.0, 567.0 + y_offset), 3.0, false
end

local function draw_text(canvas, text, position, color, scale, centered)
    if centered then
        canvas.draw_bitmap_text_centered(text, position.x, position.y, color, scale)
    else
        canvas.draw_bitmap_text(text, position.x, position.y, color, scale)
    end
end

local function draw_damage_text(canvas, text, position, scale, alpha, airshot, centered, bottom_aligned)
    local draw_position = plugin.host.vec2(position.x, position.y)
    local height = canvas.measure_bitmap_text_height(scale)
    if centered then
        draw_position.y = draw_position.y - (height / 2.0)
    elseif bottom_aligned then
        draw_position.y = draw_position.y - height
    end

    if config.style == 1 then
        local lorgan_color = airshot and AIRSHOT_YELLOW or LORGAN_RED
        draw_text(canvas, text, draw_position, alpha_color(lorgan_color, alpha), scale, centered)
        return
    end

    local fill_color = airshot and AIRSHOT_YELLOW or WAREYA_GREEN
    draw_text(canvas, text, plugin.host.vec2(draw_position.x + scale, draw_position.y + scale), alpha_color(OFF_WHITE, alpha), scale, centered)
    draw_text(canvas, text, draw_position, alpha_color(fill_color, alpha), scale, centered)
end

local function resolve_world_indicator_position(index)
    local target_kind = world_target_kind[index]
    local target_entity_id = world_target_entity_id[index]
    if target_kind == "Player" and plugin.host.is_player_visible(target_entity_id) and not plugin.host.is_player_cloaked(target_entity_id) then
        local position = plugin.host.try_get_player_world_position(target_entity_id)
        if position ~= nil then
            return position
        end
    end

    return plugin.host.vec2(world_x[index], world_y[index])
end

local function play_ding(target_world_position)
    if not config.playDing then
        return
    end

    local pan = 0.0
    if config.stereoDing then
        local client_state = plugin.host.get_client_state()
        local camera_top_left = client_state.cameraTopLeft or client_state.camera_top_left or plugin.host.vec2(0.0, 0.0)
        local viewport_width = client_state.viewportWidth or client_state.viewport_width or 0.0
        local clamped_screen_x = clamp((target_world_position.x or 0.0) - (camera_top_left.x or 0.0), 0.0, viewport_width)
        if viewport_width > 0.0 then
            pan = clamp((clamped_screen_x / (viewport_width / 2.0)) - 1.0, -1.0, 1.0)
        end
    end

    plugin.host.play_sound("ding", 1.0, 0.0, pan)
end

local function try_merge_world_indicator(event)
    local target_kind = event.targetKind or event.target_kind
    local target_entity_id = event.targetEntityId or event.target_entity_id
    local target_world_x = event.targetWorldX or event.target_world_x or 0.0
    local target_world_y = event.targetWorldY or event.target_world_y or 0.0
    local airshot = event.airshot == true or event.flagsAirshot == true or event.flags_airshot == true

    for index = 1, world_count do
        if world_target_kind[index] == target_kind
            and world_target_entity_id[index] == target_entity_id
            and world_airshot[index] == airshot
            and world_age_seconds[index] <= 0.15 then
            world_amount[index] = world_amount[index] + event.amount
            world_x[index] = target_world_x
            world_y[index] = target_world_y
            world_age_seconds[index] = 0.0
            world_y_offset[index] = 0.0
            return true
        end
    end

    return false
end

local function prune_world_indicators(delta_seconds)
    local write_index = 1
    for read_index = 1, world_count do
        local age_seconds = world_age_seconds[read_index] + delta_seconds
        local y_offset = world_y_offset[read_index] - (WORLD_INDICATOR_RISE_SPEED * delta_seconds)
        if age_seconds < WORLD_INDICATOR_LIFETIME_SECONDS then
            if write_index ~= read_index then
                world_target_kind[write_index] = world_target_kind[read_index]
                world_target_entity_id[write_index] = world_target_entity_id[read_index]
                world_x[write_index] = world_x[read_index]
                world_y[write_index] = world_y[read_index]
                world_amount[write_index] = world_amount[read_index]
                world_airshot[write_index] = world_airshot[read_index]
            end

            world_age_seconds[write_index] = age_seconds
            world_y_offset[write_index] = y_offset
            write_index = write_index + 1
        end
    end

    for index = write_index, world_count do
        world_target_kind[index] = nil
        world_target_entity_id[index] = nil
        world_x[index] = nil
        world_y[index] = nil
        world_amount[index] = nil
        world_airshot[index] = nil
        world_age_seconds[index] = nil
        world_y_offset[index] = nil
    end

    world_count = write_index - 1
end

local function prune_hud_indicators(delta_seconds)
    local write_index = 1
    for read_index = 1, hud_count do
        local age_seconds = hud_age_seconds[read_index] + delta_seconds
        local y_offset = hud_y_offset[read_index] - (HUD_INDICATOR_RISE_SPEED * delta_seconds)
        if age_seconds < HUD_INDICATOR_LIFETIME_SECONDS then
            if write_index ~= read_index then
                hud_amount[write_index] = hud_amount[read_index]
                hud_airshot[write_index] = hud_airshot[read_index]
            end

            hud_age_seconds[write_index] = age_seconds
            hud_y_offset[write_index] = y_offset
            write_index = write_index + 1
        end
    end

    for index = write_index, hud_count do
        hud_amount[index] = nil
        hud_airshot[index] = nil
        hud_age_seconds[index] = nil
        hud_y_offset[index] = nil
    end

    hud_count = write_index - 1
end

function plugin.initialize(host)
    plugin.host = host
    config = load_config()
    host.register_sound_asset("ding", "dingaling.wav")
    reset_state()
end

function plugin.shutdown()
    reset_state()
end

function plugin.on_client_frame(e)
    if not e.isGameplayActive then
        reset_state()
        return
    end

    prune_world_indicators(e.deltaSeconds)
    prune_hud_indicators(e.deltaSeconds)

    if rolling_damage <= 0 then
        return
    end

    rolling_damage_timer_seconds = rolling_damage_timer_seconds - e.deltaSeconds
    if rolling_damage_timer_seconds > 0.0 then
        return
    end

    add_hud_indicator(rolling_damage, rolling_damage_airshot)
    rolling_damage = 0
    rolling_damage_timer_seconds = 0.0
    rolling_damage_airshot = false
end

function plugin.on_local_damage(e)
    local dealt = e.dealtByLocalPlayer or e.dealt_by_local_player
    local assisted = e.assistedByLocalPlayer or e.assisted_by_local_player
    if not dealt and not assisted then
        return
    end

    if e.amount <= 1 then
        return
    end

    if try_merge_world_indicator(e) then
        rolling_damage = rolling_damage + e.amount
        if e.airshot == true or e.flagsAirshot == true or e.flags_airshot == true then
            rolling_damage_airshot = true
        end
        rolling_damage_timer_seconds = ROLLUP_IDLE_SECONDS
        return
    end

    local target_kind = e.targetKind or e.target_kind
    local target_entity_id = e.targetEntityId or e.target_entity_id
    local target_world_x = e.targetWorldX or e.target_world_x or 0.0
    local target_world_y = e.targetWorldY or e.target_world_y or 0.0
    local airshot = e.airshot == true or e.flagsAirshot == true or e.flags_airshot == true

    world_count = world_count + 1
    world_target_kind[world_count] = target_kind
    world_target_entity_id[world_count] = target_entity_id
    world_x[world_count] = target_world_x
    world_y[world_count] = target_world_y
    world_amount[world_count] = e.amount
    world_airshot[world_count] = airshot
    world_age_seconds[world_count] = 0.0
    world_y_offset[world_count] = 0.0

    rolling_damage = rolling_damage + e.amount
    if airshot then
        rolling_damage_airshot = true
    end
    rolling_damage_timer_seconds = ROLLUP_IDLE_SECONDS
    play_ding(plugin.host.vec2(target_world_x, target_world_y))
end

function plugin.on_gameplay_hud_draw(canvas)
    for index = 1, world_count do
        local position = resolve_world_indicator_position(index)
        local scale = 1.0 + math.floor(world_amount[index] / 100.0)
        draw_damage_text(
            canvas,
            "-" .. tostring(world_amount[index]),
            plugin.host.vec2(position.x or 0.0, (position.y or 0.0) + world_y_offset[index]),
            scale,
            clamp(1.0 - (world_age_seconds[index] / WORLD_INDICATOR_LIFETIME_SECONDS), 0.0, 1.0),
            world_airshot[index],
            true,
            false
        )
    end

    for index = 1, hud_count do
        local position, scale, bottom_aligned = get_hud_anchor(hud_y_offset[index])
        draw_damage_text(
            canvas,
            "-" .. tostring(hud_amount[index]),
            position,
            scale,
            clamp(1.0 - (hud_age_seconds[index] / HUD_INDICATOR_LIFETIME_SECONDS), 0.0, 1.0),
            hud_airshot[index],
            false,
            bottom_aligned
        )
    end

    if rolling_damage > 0 then
        local position, scale, bottom_aligned = get_hud_anchor(0.0)
        draw_damage_text(canvas, "-" .. tostring(rolling_damage), position, scale, 1.0, rolling_damage_airshot, false, bottom_aligned)
    end
end

function plugin.get_options_sections()
    return {
        {
            title = "Damage Indicator",
            items = {
                {
                    kind = "choice",
                    label = "Damage indicator style",
                    get_value_label = "get_style_label",
                    activate = "advance_style"
                },
                {
                    kind = "boolean",
                    label = "Ding sound on hit",
                    get_value_label = "get_play_ding_label",
                    activate = "toggle_play_ding"
                },
                {
                    kind = "boolean",
                    label = "Move counter for HUDs",
                    get_value_label = "get_move_counter_label",
                    activate = "toggle_move_counter"
                },
                {
                    kind = "boolean",
                    label = "Ding sound is stereo",
                    get_value_label = "get_stereo_ding_label",
                    activate = "toggle_stereo_ding"
                }
            }
        }
    }
end

function plugin.get_style_label()
    return config.style == 0 and "Wareya's" or "Lorgan's"
end

function plugin.advance_style()
    config.style = config.style == 0 and 1 or 0
    save_config()
end

function plugin.get_play_ding_label()
    return config.playDing and "Enabled" or "Disabled"
end

function plugin.toggle_play_ding()
    config.playDing = not config.playDing
    save_config()
end

function plugin.get_move_counter_label()
    return config.moveCounterForHud and "Enabled" or "Disabled"
end

function plugin.toggle_move_counter()
    config.moveCounterForHud = not config.moveCounterForHud
    save_config()
end

function plugin.get_stereo_ding_label()
    return config.stereoDing and "Enabled" or "Disabled"
end

function plugin.toggle_stereo_ding()
    config.stereoDing = not config.stereoDing
    save_config()
end

return plugin
