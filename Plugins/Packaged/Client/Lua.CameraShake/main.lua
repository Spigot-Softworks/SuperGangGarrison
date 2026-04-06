local plugin = {}

local SHAKE_DECAY_PER_REFERENCE_FRAME = 0.8
local REFERENCE_FRAMES_PER_SECOND = 30.0
local INTENSITY_EPSILON = 0.0005
local SOURCE_CLASS_DETECTION_DISTANCE_SQUARED = 32 * 32
local LOCAL_SOURCE_DISTANCE_SQUARED = 20 * 20

local default_config = {
    shakeLevel = 4
}

local config = default_config
local current_shake_intensity = 0.0
local current_camera_offset = { x = 0.0, y = 0.0 }

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
    local loaded = plugin.host.load_json_config("camerashake.json", default_config)
    local normalized = {
        shakeLevel = clamp(tonumber(loaded.shakeLevel) or default_config.shakeLevel, 0, 8)
    }
    plugin.host.save_json_config("camerashake.json", normalized)
    return normalized
end

local function save_config()
    plugin.host.save_json_config("camerashake.json", config)
end

local function reset_shake_state()
    current_shake_intensity = 0.0
    current_camera_offset = plugin.host.vec2(0.0, 0.0)
end

local function random_range(minimum, maximum)
    return minimum + (plugin.host.random_float() * (maximum - minimum))
end

local function create_random_offset(intensity)
    if config.shakeLevel <= 0 or intensity <= INTENSITY_EPSILON then
        return plugin.host.vec2(0.0, 0.0)
    end

    return plugin.host.vec2(
        random_range(-intensity, intensity) * config.shakeLevel,
        random_range(-intensity, intensity) * config.shakeLevel)
end

local function distance_squared(left, right)
    local dx = left.x - right.x
    local dy = left.y - right.y
    return (dx * dx) + (dy * dy)
end

local function distance(left, right)
    return math.sqrt(distance_squared(left, right))
end

local function get_camera_center()
    local state = plugin.host.get_client_state()
    return plugin.host.vec2(state.cameraTopLeftX + (state.viewportWidth * 0.5), state.cameraTopLeftY + (state.viewportHeight * 0.5))
end

local function get_distance_to_camera_center(world_position)
    return math.max(1.0, distance(world_position, get_camera_center()))
end

local function resolve_likely_shooter_class(sound_position)
    local state = plugin.host.get_client_state()
    local closest_class = "Unknown"
    local closest_distance_squared = SOURCE_CLASS_DETECTION_DISTANCE_SQUARED
    for _, marker in ipairs(state.playerMarkers or {}) do
        if marker.isAlive then
            local marker_position = marker.worldPosition
            local candidate_distance_squared = distance_squared(marker_position, sound_position)
            if candidate_distance_squared <= closest_distance_squared then
                closest_distance_squared = candidate_distance_squared
                closest_class = marker.classId
            end
        end
    end

    return closest_class
end

local function is_local_source(sound_position, expected_class)
    local state = plugin.host.get_client_state()
    if state.localPlayerClass ~= expected_class or not state.hasLocalPlayerPosition then
        return false
    end

    local local_position = plugin.host.vec2(state.localPlayerWorldX, state.localPlayerWorldY)
    return distance_squared(local_position, sound_position) <= LOCAL_SOURCE_DISTANCE_SQUARED
end

local function is_local_scoped_sniper_source(sound_position)
    local state = plugin.host.get_client_state()
    return state.isLocalPlayerScoped and is_local_source(sound_position, "Sniper")
end

local function get_shotgun_shake_cap(sound_position)
    return resolve_likely_shooter_class(sound_position) == "Scout" and 0.3 or 0.2
end

local function get_rifle_shake_contribution(sound_position, sound_distance)
    local local_scoped_bonus = is_local_scoped_sniper_source(sound_position) and 2.0 or 0.0
    local base_contribution = 0.25 + local_scoped_bonus
    if is_local_source(sound_position, "Sniper") then
        return base_contribution
    end

    return math.min(base_contribution, 40.0 / sound_distance)
end

local function get_shake_contribution(event)
    local sound_distance = get_distance_to_camera_center(event.worldPosition)
    if event.soundName == "ExplosionSnd" then
        return 40.0 / sound_distance
    elseif event.soundName == "ChaingunSnd" then
        return math.min(0.1, 30.0 / sound_distance)
    elseif event.soundName == "ShotgunSnd" then
        return math.min(get_shotgun_shake_cap(event.worldPosition), 30.0 / sound_distance)
    elseif event.soundName == "RevolverSnd" then
        return math.min(0.1, 30.0 / sound_distance)
    elseif event.soundName == "RifleSnd" then
        return get_rifle_shake_contribution(event.worldPosition, sound_distance)
    end

    return 0.0
end

function plugin.initialize(host)
    plugin.host = host
    config = load_config()
    reset_shake_state()
end

function plugin.shutdown()
    reset_shake_state()
end

function plugin.on_client_frame(e)
    if not e.isGameplayActive or config.shakeLevel <= 0 then
        reset_shake_state()
        return
    end

    if current_shake_intensity <= INTENSITY_EPSILON then
        reset_shake_state()
        return
    end

    current_camera_offset = create_random_offset(current_shake_intensity)
    local reference_frames = math.max(0.0, e.deltaSeconds * REFERENCE_FRAMES_PER_SECOND)
    current_shake_intensity = current_shake_intensity * (SHAKE_DECAY_PER_REFERENCE_FRAME ^ reference_frames)
    if current_shake_intensity <= INTENSITY_EPSILON then
        current_shake_intensity = 0.0
    end
end

function plugin.on_world_sound(e)
    local state = plugin.host.get_client_state()
    if not state.isGameplayActive or config.shakeLevel <= 0 then
        return
    end

    local contribution = get_shake_contribution(e)
    if contribution <= 0.0 then
        return
    end

    current_shake_intensity = current_shake_intensity + contribution
    current_camera_offset = create_random_offset(current_shake_intensity)
end

function plugin.get_camera_offset()
    return config.shakeLevel > 0 and current_camera_offset or plugin.host.vec2(0.0, 0.0)
end

function plugin.get_options_sections()
    return {
        {
            title = "Camera Shake",
            items = {
                {
                    kind = "integer",
                    label = "Shake level",
                    get_value_label = "get_shake_level_label",
                    activate = "advance_shake_level"
                }
            }
        }
    }
end

function plugin.get_shake_level_label()
    return config.shakeLevel <= 0 and "Off" or tostring(config.shakeLevel)
end

function plugin.advance_shake_level()
    config.shakeLevel = config.shakeLevel + 1
    if config.shakeLevel > 8 then
        config.shakeLevel = 0
    end
    save_config()
    if config.shakeLevel <= 0 then
        reset_shake_state()
    end
end

return plugin
