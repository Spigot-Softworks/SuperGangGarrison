local plugin = {}

local LEGACY_ANIMATION_SPEED_PER_TICK = 0.33
local WHITE = { r = 255, g = 255, b = 255, a = 255 }

local definitions = {
    { key = "demoman", fileName = "demoman.gif", frameCount = 9, originX = 33.0, originY = 40.0 },
    { key = "demoman2", fileName = "demoman2.png", frameCount = 9, originX = 33.0, originY = 40.0 },
    { key = "engineer", fileName = "engineer.gif", frameCount = 10, originX = 32.0, originY = 40.0 },
    { key = "engineer2", fileName = "engineer2.png", frameCount = 10, originX = 32.0, originY = 40.0 },
    { key = "heavy", fileName = "heavy.gif", frameCount = 13, originX = 19.0, originY = 40.0 },
    { key = "heavy2", fileName = "heavy2.png", frameCount = 13, originX = 19.0, originY = 40.0 },
    { key = "heavy3", fileName = "heavy3.gif", frameCount = 18, originX = 19.0, originY = 40.0 },
    { key = "heavy4", fileName = "heavy4.png", frameCount = 18, originX = 19.0, originY = 40.0 },
    { key = "medic", fileName = "medic.gif", frameCount = 11, originX = 27.0, originY = 40.0 },
    { key = "medic2", fileName = "medic2.png", frameCount = 11, originX = 27.0, originY = 40.0 },
    { key = "medic3", fileName = "medic3.gif", frameCount = 22, originX = 27.0, originY = 40.0 },
    { key = "medic4", fileName = "medic4.png", frameCount = 22, originX = 27.0, originY = 40.0 },
    { key = "pyro", fileName = "pyro.gif", frameCount = 10, originX = 31.0, originY = 40.0 },
    { key = "pyro2", fileName = "pyro2.png", frameCount = 10, originX = 31.0, originY = 40.0 },
    { key = "scout", fileName = "scout.png", frameCount = 10, originX = 30.0, originY = 40.0 },
    { key = "scout2", fileName = "scout2.png", frameCount = 10, originX = 30.0, originY = 40.0 },
    { key = "sniper", fileName = "sniper.gif", frameCount = 13, originX = 26.0, originY = 40.0 },
    { key = "sniper2", fileName = "sniper2.png", frameCount = 13, originX = 26.0, originY = 40.0 },
    { key = "soldier", fileName = "soldier.gif", frameCount = 11, originX = 30.0, originY = 40.0 },
    { key = "soldier2", fileName = "soldier2.png", frameCount = 11, originX = 30.0, originY = 40.0 },
    { key = "spy", fileName = "spy.gif", frameCount = 30, originX = 28.0, originY = 40.0 },
    { key = "spy2", fileName = "spy2.png", frameCount = 30, originX = 28.0, originY = 40.0 }
}

local loaded_animations = {}
local next_definition_index = 1
local animations_preloaded = false

local function clamp(value, minimum, maximum)
    if value < minimum then
        return minimum
    end

    if value > maximum then
        return maximum
    end

    return value
end

local function reset_animation_load_state()
    loaded_animations = {}
    next_definition_index = 1
    animations_preloaded = false
end

local function load_next_animation()
    if animations_preloaded then
        return
    end

    local definition = definitions[next_definition_index]
    if definition == nil then
        animations_preloaded = true
        return
    end

    local loaded_frame_count = plugin.host.register_legacy_animation_asset(
        definition.key,
        "Animations/" .. definition.fileName,
        definition.frameCount,
        true)
    if loaded_frame_count and loaded_frame_count > 0 then
        loaded_animations[definition.key] = {
            frameCount = loaded_frame_count,
            origin = plugin.host.vec2(definition.originX, definition.originY)
        }
    else
        plugin.host.log("failed to load animation asset: " .. definition.fileName)
    end

    next_definition_index = next_definition_index + 1
    if definitions[next_definition_index] == nil then
        animations_preloaded = true
    end
end

local function try_resolve_animation_key(dead_body)
    if dead_body.team == "Red" then
        if dead_body.classId == "Demoman" then
            return "demoman"
        elseif dead_body.classId == "Engineer" then
            return "engineer"
        elseif dead_body.classId == "Heavy" then
            return dead_body.animationKind == "Severe" and "heavy3" or "heavy"
        elseif dead_body.classId == "Medic" then
            return dead_body.animationKind == "Severe" and "medic3" or "medic"
        elseif dead_body.classId == "Pyro" then
            return "pyro"
        elseif dead_body.classId == "Scout" then
            return "scout"
        elseif dead_body.classId == "Sniper" then
            return "sniper"
        elseif dead_body.classId == "Soldier" then
            return "soldier"
        elseif dead_body.classId == "Spy" then
            return "spy2"
        end
    elseif dead_body.team == "Blue" then
        if dead_body.classId == "Demoman" then
            return "demoman2"
        elseif dead_body.classId == "Engineer" then
            return "engineer2"
        elseif dead_body.classId == "Heavy" then
            return dead_body.animationKind == "Severe" and "heavy4" or "heavy2"
        elseif dead_body.classId == "Medic" then
            return dead_body.animationKind == "Severe" and "medic4" or "medic2"
        elseif dead_body.classId == "Pyro" then
            return "pyro2"
        elseif dead_body.classId == "Scout" then
            return "scout2"
        elseif dead_body.classId == "Sniper" then
            return "sniper2"
        elseif dead_body.classId == "Soldier" then
            return "soldier2"
        elseif dead_body.classId == "Spy" then
            return "spy"
        end
    end

    return nil
end

function plugin.initialize(host)
    plugin.host = host
    reset_animation_load_state()
end

function plugin.shutdown()
    reset_animation_load_state()
end

function plugin.on_client_started()
end

function plugin.on_client_frame(e)
    load_next_animation()
end

function plugin.try_draw_dead_body(canvas, dead_body)
    if dead_body.animationKind == "Default" or dead_body.classId == "Quote" then
        return false
    end

    local animation_key = try_resolve_animation_key(dead_body)
    local animation = animation_key and loaded_animations[animation_key] or nil
    if animation == nil or animation.frameCount <= 0 then
        return false
    end

    local elapsed_ticks = math.max(0, 300 - dead_body.ticksRemaining)
    local frame_index = clamp(math.floor(elapsed_ticks * LEGACY_ANIMATION_SPEED_PER_TICK), 0, animation.frameCount - 1)
    local scale_x = dead_body.facingLeft and -1.0 or 1.0
    return canvas.draw_world_animation_frame(
        animation_key,
        frame_index,
        dead_body.worldPosition.x,
        dead_body.worldPosition.y,
        WHITE,
        scale_x,
        1.0,
        animation.origin.x,
        animation.origin.y,
        0.0)
end

return plugin
