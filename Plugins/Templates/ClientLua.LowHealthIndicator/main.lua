local plugin = {}

local LEGACY_FRAMES_PER_SECOND = 30.0

local default_config = {
    warningVolumePercent = 70,
    warningTimerFrames = 60,
    warningHealthThreshold = 40,
    usePercentageThreshold = false
}

local config = default_config
local warning_elapsed_frames = 0.0

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
    local loaded = plugin.host.load_json_config("lowhealthindicator.json", default_config)
    local normalized = {
        warningVolumePercent = clamp(tonumber(loaded.warningVolumePercent) or default_config.warningVolumePercent, 0, 100),
        warningTimerFrames = clamp(tonumber(loaded.warningTimerFrames) or default_config.warningTimerFrames, 0, 180),
        warningHealthThreshold = clamp(tonumber(loaded.warningHealthThreshold) or default_config.warningHealthThreshold, 0, 100),
        usePercentageThreshold = loaded.usePercentageThreshold == true
    }
    plugin.host.save_json_config("lowhealthindicator.json", normalized)
    return normalized
end

local function save_config()
    plugin.host.save_json_config("lowhealthindicator.json", config)
end

local function should_play_warning(health, max_health)
    if config.usePercentageThreshold then
        local threshold_health = math.floor(max_health * (config.warningHealthThreshold / 100.0))
        return health <= threshold_health
    end

    return health <= config.warningHealthThreshold
end

function plugin.initialize(host)
    plugin.host = host
    config = load_config()
    host.register_sound_asset("warning", "Resources/PrOF/boop.wav")
end

function plugin.on_client_frame(e)
    local state = plugin.host.get_client_state()
    if not e.isGameplayActive
        or state.isSpectator
        or not state.isLocalPlayerAlive
        or state.localPlayerHealth < 0
        or state.localPlayerMaxHealth <= 0 then
        warning_elapsed_frames = 0.0
        return
    end

    warning_elapsed_frames = warning_elapsed_frames + (e.deltaSeconds * LEGACY_FRAMES_PER_SECOND)
    if warning_elapsed_frames < config.warningTimerFrames then
        return
    end

    if not should_play_warning(state.localPlayerHealth, state.localPlayerMaxHealth) then
        return
    end

    plugin.host.play_sound("warning", config.warningVolumePercent / 100.0, 0.0, 0.0)
    warning_elapsed_frames = 0.0
end

function plugin.get_options_sections()
    return {
        {
            title = "Low Health Indicator",
            items = {
                {
                    kind = "integer",
                    label = "Warning volume",
                    get_value_label = "get_warning_volume_label",
                    activate = "advance_warning_volume"
                },
                {
                    kind = "integer",
                    label = "Warning delay",
                    get_value_label = "get_warning_delay_label",
                    activate = "advance_warning_delay"
                },
                {
                    kind = "boolean",
                    label = "Percentage based calculations?",
                    get_value_label = "get_threshold_mode_label",
                    activate = "toggle_threshold_mode"
                },
                {
                    kind = "integer",
                    label = "Warning threshold",
                    get_value_label = "get_warning_threshold_label",
                    activate = "advance_warning_threshold"
                }
            }
        }
    }
end

function plugin.get_warning_volume_label()
    return tostring(config.warningVolumePercent) .. "%"
end

function plugin.advance_warning_volume()
    config.warningVolumePercent = config.warningVolumePercent + 5
    if config.warningVolumePercent > 100 then
        config.warningVolumePercent = 0
    end
    save_config()
end

function plugin.get_warning_delay_label()
    return tostring(config.warningTimerFrames) .. "f"
end

function plugin.advance_warning_delay()
    config.warningTimerFrames = config.warningTimerFrames + 5
    if config.warningTimerFrames > 180 then
        config.warningTimerFrames = 0
    end
    save_config()
end

function plugin.get_threshold_mode_label()
    return config.usePercentageThreshold and "Yes" or "No"
end

function plugin.toggle_threshold_mode()
    config.usePercentageThreshold = not config.usePercentageThreshold
    save_config()
end

function plugin.get_warning_threshold_label()
    if config.usePercentageThreshold then
        return tostring(config.warningHealthThreshold) .. "%"
    end

    return tostring(config.warningHealthThreshold)
end

function plugin.advance_warning_threshold()
    config.warningHealthThreshold = config.warningHealthThreshold + 5
    if config.warningHealthThreshold > 100 then
        config.warningHealthThreshold = 0
    end
    save_config()
end

return plugin
