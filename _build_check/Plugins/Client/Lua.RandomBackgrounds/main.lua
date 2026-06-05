local plugin = {}

local available_backgrounds = {}
local selected_background_path = nil
local selected_attribution_text = ""

local function get_attribution_text(file_name)
    local attributions = {
        Right_behind_you_Conan_png = "\"Right Behind You\" by Conan",
        The_red_gang_sy_png = "\"The Red Gang\" by sy",
        Just_within_reach_sven_png = "\"Just Within Reach\" by Sven",
        Soldier_finger_haxton_png = "\"Soldier pointing his finger\" by Haxton Sale",
        Outclasses_zaspai_png = "\"Outclassed\" by ZaSpai",
        Sentry_nest_conan_png = "\"Sentry Nest\" by Conan",
        archibaldes_no_melasos_png = "\"Archibaldes, no!\" by Melasos",
        hotline_garrison_poop_png = "\"Hotline Garrison 2\" by Poop",
        aftermath_natsu_png = "\"Aftermath\" by Natsu"
    }

    local normalized = string.gsub(file_name or "", "[^%w]", "_")
    return attributions[normalized] or ""
end

local function select_random_background()
    if #available_backgrounds == 0 then
        return
    end

    local index = plugin.host.random_int(#available_backgrounds) + 1
    selected_background_path = available_backgrounds[index]
    local file_name = string.match(selected_background_path, "[^\\\\/]+$")
    selected_attribution_text = get_attribution_text(file_name)
    plugin.host.log("selected random background: " .. (file_name or selected_background_path))
end

function plugin.initialize(host)
    plugin.host = host
    host.register_menu_entry("shuffle-background", "Shuffle Background", "MainMenuRoot", "select_random_background", 0)
    available_backgrounds = host.list_files("Resources/PrOF/Backgrounds", "*.png")
    if #available_backgrounds == 0 then
        host.log("no background images found in Resources/PrOF/Backgrounds")
        return
    end

    select_random_background()
end

function plugin.select_random_background()
    select_random_background()
end

function plugin.get_main_menu_background_override()
    if selected_background_path == nil then
        return nil
    end

    return {
        image_path = selected_background_path,
        attribution_text = selected_attribution_text
    }
end

return plugin
