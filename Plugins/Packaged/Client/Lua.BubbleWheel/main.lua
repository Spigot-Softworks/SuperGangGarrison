local plugin = {}

local STRIP_FRAME_COUNT = 20
local STRIP_FRAME_SIZE = 201
local WHEEL_ORIGIN = { x = 100.0, y = 100.0 }
local WHITE = { r = 255, g = 255, b = 255, a = 255 }

local textures_loaded = false
local last_bubble_menu_kind = "None"
local last_bubble_menu_x_page_index = 0
local last_hovered_slot = -1

local function tinted_white(alpha)
    return { r = 255, g = 255, b = 255, a = math.floor(255 * alpha) }
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

local function ensure_textures_loaded()
    if textures_loaded then
        return
    end

    plugin.host.register_texture_atlas_asset(
        "bubblewheel-strip",
        "Resources/PrOF/BubbleWheel/BubbleWheelStrip.png",
        STRIP_FRAME_SIZE,
        STRIP_FRAME_SIZE)
    plugin.host.register_texture_asset("bubblewheel-z", "Resources/PrOF/BubbleWheel/MenuWheelZ.png")
    plugin.host.register_texture_asset("bubblewheel-x", "Resources/PrOF/BubbleWheel/MenuWheelX.png")
    plugin.host.register_texture_asset("bubblewheel-c", "Resources/PrOF/BubbleWheel/MenuWheelC.png")
    plugin.host.register_texture_asset("bubblewheel-x2r", "Resources/PrOF/BubbleWheel/MenuWheelX2R.png")
    plugin.host.register_texture_asset("bubblewheel-x2b", "Resources/PrOF/BubbleWheel/MenuWheelX2B.png")
    textures_loaded = true
end

local function reset_hover_selection_state()
    last_bubble_menu_kind = "None"
    last_bubble_menu_x_page_index = 0
    last_hovered_slot = -1
end

local function selected_slot_or_default(input_state)
    if input_state.distanceFromCenter < 30.0 then
        return 0
    end

    local aim_direction = input_state.aimDirectionDegrees + 90.0
    while aim_direction >= 360.0 do
        aim_direction = aim_direction - 360.0
    end

    return clamp(math.floor(aim_direction / 40.0) + 1, 1, 9)
end

local function resolve_wheel_selection(selected_slot, frame_base)
    if selected_slot <= 0 then
        return { clearBubbleSelection = true }
    end

    return { bubbleFrame = frame_base + selected_slot }
end

local function resolve_x_selection(input_state, selected_slot)
    if input_state.xPageIndex == 0 then
        if selected_slot == 0 or selected_slot == 1 or selected_slot == 2 then
            return { clearBubbleSelection = true }
        end

        if selected_slot >= 3 and selected_slot <= 9 then
            return { bubbleFrame = 26 + selected_slot }
        end

        return nil
    end

    if input_state.qPressed then
        return { bubbleFrame = input_state.xPageIndex == 2 and 48 or 47 }
    end

    local offset = input_state.xPageIndex == 2 and 10 or 0
    local bubble_frame = selected_slot == 0 and (9 + offset) or ((selected_slot - 1) + offset)
    return { bubbleFrame = bubble_frame }
end

local function get_menu_texture_asset_id(render_state)
    if render_state.kind == "Z" then
        return "bubblewheel-z"
    elseif render_state.kind == "C" then
        return "bubblewheel-c"
    elseif render_state.kind == "X" and render_state.xPageIndex == 1 then
        return "bubblewheel-x2r"
    elseif render_state.kind == "X" and render_state.xPageIndex == 2 then
        return "bubblewheel-x2b"
    elseif render_state.kind == "X" then
        return "bubblewheel-x"
    end

    return nil
end

function plugin.initialize(host)
    plugin.host = host
end

function plugin.shutdown()
    textures_loaded = false
    reset_hover_selection_state()
end

function plugin.on_client_started()
    ensure_textures_loaded()
end

function plugin.try_handle_bubble_menu_input(input_state)
    if input_state.kind == "None" then
        reset_hover_selection_state()
        return nil
    end

    if input_state.kind == "X"
        and input_state.pressedDigit ~= nil
        and input_state.pressedDigit >= 1
        and input_state.pressedDigit <= 3 then
        local requested_page_index = input_state.pressedDigit - 1
        if requested_page_index ~= input_state.xPageIndex then
            last_bubble_menu_kind = input_state.kind
            last_bubble_menu_x_page_index = requested_page_index
            last_hovered_slot = -1
            return {
                newXPageIndex = requested_page_index,
                clearBubbleSelection = true
            }
        end
    end

    local selected_slot = selected_slot_or_default(input_state)
    local menu_changed = input_state.kind ~= last_bubble_menu_kind or input_state.xPageIndex ~= last_bubble_menu_x_page_index
    local slot_changed = selected_slot ~= last_hovered_slot

    last_bubble_menu_kind = input_state.kind
    last_bubble_menu_x_page_index = input_state.xPageIndex

    if not menu_changed and not slot_changed and not input_state.qPressed then
        return nil
    end

    last_hovered_slot = selected_slot

    local result = nil
    if input_state.kind == "Z" then
        result = resolve_wheel_selection(selected_slot, 19)
    elseif input_state.kind == "C" then
        result = resolve_wheel_selection(selected_slot, 35)
    elseif input_state.kind == "X" then
        result = resolve_x_selection(input_state, selected_slot)
    end

    if result ~= nil and result.newXPageIndex ~= nil then
        last_bubble_menu_x_page_index = result.newXPageIndex
    end

    return result
end

function plugin.try_draw_bubble_menu(canvas, render_state)
    ensure_textures_loaded()

    local center = plugin.host.vec2(canvas.viewport_width / 2.0, canvas.viewport_height / 2.0)
    local tint = tinted_white(render_state.alpha)
    for index = 0, 9 do
        local frame_index = index == render_state.selectedSlot and (index + 10) or index
        canvas.draw_screen_texture_atlas_frame(
            "bubblewheel-strip",
            frame_index,
            center.x,
            center.y,
            tint,
            1.0,
            1.0,
            WHEEL_ORIGIN.x,
            WHEEL_ORIGIN.y,
            0.0)
    end

    local menu_texture_asset_id = get_menu_texture_asset_id(render_state)
    if menu_texture_asset_id == nil then
        return false
    end

    return canvas.draw_screen_texture(
        menu_texture_asset_id,
        center.x,
        center.y,
        tint,
        1.0,
        1.0,
        WHEEL_ORIGIN.x,
        WHEEL_ORIGIN.y,
        0.0)
end

return plugin
