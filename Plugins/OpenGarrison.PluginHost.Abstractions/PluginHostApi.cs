using System.Text.Json.Serialization;

namespace OpenGarrison.PluginHost;

[JsonConverter(typeof(JsonStringEnumConverter<OpenGarrisonPluginType>))]
public enum OpenGarrisonPluginType
{
    Client,
    Server,
    Gameplay,
}

[JsonConverter(typeof(JsonStringEnumConverter<OpenGarrisonPluginRuntimeKind>))]
public enum OpenGarrisonPluginRuntimeKind
{
    Clr,
    Lua,
}

public sealed record OpenGarrisonPluginHostCapabilities(
    bool ReadOnlyState,
    bool SemanticEvents,
    bool PluginMessaging,
    bool ReplicatedState,
    bool AssetRegistration,
    bool UiRegistration,
    bool Hotkeys,
    bool ScoreboardPanels,
    bool AdminOperations,
    bool LoadoutSelection);

public sealed record OpenGarrisonPluginRuntimeSurface(
    OpenGarrisonPluginRuntimeKind Runtime,
    string ApiVersion,
    IReadOnlyList<string> Functions);

public sealed record OpenGarrisonPluginHostApi(
    string ApiVersion,
    OpenGarrisonPluginType HostType,
    OpenGarrisonPluginHostCapabilities Capabilities,
    IReadOnlyList<OpenGarrisonPluginRuntimeSurface> RuntimeSurfaces)
{
    public static OpenGarrisonPluginHostApi CreateClientDefault()
    {
        return new OpenGarrisonPluginHostApi(
            "1.0",
            OpenGarrisonPluginType.Client,
            new OpenGarrisonPluginHostCapabilities(
                ReadOnlyState: true,
                SemanticEvents: true,
                PluginMessaging: true,
                ReplicatedState: false,
                AssetRegistration: true,
                UiRegistration: true,
                Hotkeys: true,
                ScoreboardPanels: true,
                AdminOperations: false,
                LoadoutSelection: false),
            [
                new OpenGarrisonPluginRuntimeSurface(
                    OpenGarrisonPluginRuntimeKind.Lua,
                    "1.0",
                    [
                        "log",
                        "random_int",
                        "random_float",
                        "vec2",
                        "color",
                        "load_json_config",
                        "save_json_config",
                        "get_manifest",
                        "get_host_api",
                        "get_client_state",
                        "try_get_player_world_position",
                        "is_player_visible",
                        "is_player_cloaked",
                        "register_sound_asset",
                        "register_texture_asset",
                        "register_texture_atlas_asset",
                        "register_texture_region_asset",
                        "register_legacy_animation_asset",
                        "play_sound",
                        "register_menu_entry",
                        "list_files",
                    ]),
            ]);
    }

    public static OpenGarrisonPluginHostApi CreateServerDefault()
    {
        return new OpenGarrisonPluginHostApi(
            "1.0",
            OpenGarrisonPluginType.Server,
            new OpenGarrisonPluginHostCapabilities(
                ReadOnlyState: true,
                SemanticEvents: true,
                PluginMessaging: true,
                ReplicatedState: true,
                AssetRegistration: false,
                UiRegistration: false,
                Hotkeys: false,
                ScoreboardPanels: false,
                AdminOperations: true,
                LoadoutSelection: true),
            [
                new OpenGarrisonPluginRuntimeSurface(
                    OpenGarrisonPluginRuntimeKind.Lua,
                    "1.0",
                    [
                        "log",
                        "get_utc_unix_time",
                        "load_json_config",
                        "save_json_config",
                        "get_manifest",
                        "get_host_api",
                        "get_server_state",
                        "get_players",
                        "try_resolve_level",
                        "get_available_gameplay_loadouts",
                        "broadcast_system_message",
                        "send_system_message",
                        "try_disconnect",
                        "try_move_to_spectator",
                        "try_set_team",
                        "try_set_class",
                        "try_set_gameplay_loadout",
                        "try_set_gameplay_equipped_slot",
                        "try_force_kill",
                        "try_set_cap_limit",
                        "try_change_map",
                        "try_set_next_round_map",
                        "send_message_to_client",
                        "broadcast_message_to_clients",
                        "set_player_replicated_state_int",
                        "set_player_replicated_state_float",
                        "set_player_replicated_state_bool",
                        "clear_player_replicated_state",
                    ]),
            ]);
    }
}
