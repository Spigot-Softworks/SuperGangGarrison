# OpenGarrison Plugin Authoring Guide

This guide is for people writing OpenGarrison plugins. If you only want to install or enable plugins, see [USER_GUIDE.md](USER_GUIDE.md).

OpenGarrison plugins are small Lua projects with a `plugin.json` manifest and one Lua entry point. Plugins can run on the client or server:

- Client plugins add presentation and interaction features such as HUD widgets, scoreboard panels, vfx, hotkeys, sounds, menus, notices, prompts, chat filters, and overlays.
- Server plugins add game/server behavior such as commands, moderation tools, scheduling, state queries, replicated state, messaging, and cancellable gameplay decisions.

The current Lua host API version is `1.0`.

## Quick Start

Start by copying a template from [Templates](Templates):

- `ClientLua.ShowPing`: simple client HUD and scoreboard plugin.
- `ClientLua.TeamOnlyMinimap`: richer client HUD, hotkeys, options, and assets.
- `ClientLua.DamageIndicator`: local damage events and sound playback.
- `ClientLua.BubbleWheel`: custom client interaction UI.
- `ServerLua`: minimal server lifecycle and player event plugin.
- `ServerLua.ChatVoting`: commands, scheduling, map changes, and replicated voting state.
- `ServerLua.GameplayAbility`: server gameplay ability registration.
- `ServerLua.PrimaryWeapon`: server weapon behavior registration.

A minimal plugin folder looks like this:

```text
my.plugin.id/
  plugin.json
  main.lua
  config.schema.json
  Assets/
```

For development, place client plugins under the runtime client plugin folder and server plugins under the runtime server plugin folder. In a normal checkout, templates live in `Plugins/Templates`; packaged server plugins live in `Plugins/Server`.

## Manifest

Every plugin needs a `plugin.json` file:

```json
{
  "schemaVersion": 1,
  "id": "example.author.my-plugin",
  "displayName": "My Plugin",
  "version": "0.1.0",
  "type": "Server",
  "runtime": "Lua",
  "entryPoint": "main.lua",
  "description": "Short description of what the plugin does.",
  "compatibility": {
    "hostApiVersion": "1.0"
  },
  "configSchemaPath": "config.schema.json",
  "tags": ["server", "lua"]
}
```

Important fields:

- `id`: stable unique id. Use reverse-domain or author-prefixed names, such as `example.author.plugin-name`.
- `displayName`: user-visible name.
- `version`: your plugin version.
- `type`: `Client` or `Server`.
- `runtime`: currently `Lua`.
- `entryPoint`: Lua file to load, relative to the plugin folder.
- `compatibility.hostApiVersion`: required host API version. Use `1.0` for the current system.
- `assetDirectories`: optional asset folders, relative to the plugin folder.
- `configSchemaPath`: optional JSON schema used to describe configuration.
- `gameplayPacks`: optional JSON gameplay packs, relative to the plugin folder.
  Use this for plugin-owned classes, weapons, abilities, loadouts, and runtime
  class-slot bindings.
- `dependencies`, `optionalDependencies`, `conflicts`: plugin relationship metadata.
- `loadOrder`: optional `{ "before": [], "after": [] }` hints.
- `permissions`: optional declared permission metadata.
- `messageContracts`: optional declared client/server plugin message contracts.
- `tags`: optional discovery labels.

Keep all manifest paths inside the plugin folder. Do not use absolute paths or `..` segments to reach outside the plugin.

Gameplay packs are loaded before the Lua entry point initializes. A class pack
that intentionally owns an existing runtime slot, such as the Quote/Curly
example, must opt in explicitly:

```json
"gameplayPacks": [
  {
    "path": "Gameplay/quote-curly.gg2",
    "allowRuntimeClassBindingOverride": true
  }
]
```

## Lua Shape

The entry point should return a table. The host calls functions on that table when events happen.

```lua
local plugin = {}

function plugin.initialize(host)
    plugin.host = host
    local manifest = host.get_manifest()
    host.log("Initialized " .. manifest.displayName .. " " .. manifest.version)
end

function plugin.shutdown()
    if plugin.host ~= nil then
        plugin.host.log("Shutting down")
    end
end

return plugin
```

Store the `host` object during `initialize`. Most useful work happens through `host.*` functions.

Lua receives DTO-style tables from the host. They are snapshots, not engine objects. Prefer reading fields from the event/state table you were given instead of assuming live engine state.

## Discovering The Host API

Use `host.get_host_api()` from Lua to inspect what the current host exposes:

```lua
function plugin.initialize(host)
    plugin.host = host
    local api = host.get_host_api()
    host.log("Host type: " .. api.hostType .. ", API: " .. api.apiVersion)
end
```

Client and server expose different functions. Use the manifest `type` that matches the APIs you need.

Common APIs on both sides:

- `get_manifest()`
- `get_host_api()`
- `log(message)`
- `load_json_config(path)`
- `save_json_config(path, value)`

## Client Plugins

Use a client plugin when the feature is local presentation or interaction. Client plugins should not be trusted for authoritative game rules.

Common client callbacks:

- `on_client_starting()`, `on_client_started()`
- `on_client_stopping()`, `on_client_stopped()`
- `on_client_frame(e)`
- `on_gameplay_hud_draw(canvas)`
- `on_scoreboard_draw(canvas, state)`
- `on_world_sound(e)`
- `on_local_damage(e)`
- `on_heal(e)`
- `on_server_plugin_message(e)`
- `get_camera_offset()`
- `get_options_sections()`
- `shutdown()`

Useful client host APIs include:

- State: `get_client_state`, `get_client_runtime_state`, `is_connected`, `is_gameplay_active`, `is_spectator`.
- Local player: `get_local_player_id`, `get_local_player_team`, `get_local_player_class`, `get_local_player_health`, `try_get_local_player_world_position`.
- Markers: `get_player_markers`, `get_sentry_markers`, `get_objective_markers`.
- Drawing/assets: `register_texture_asset`, `register_sound_asset`, `play_sound`, `color`, `vec2`.
- UI: `register_hud_widget`, `register_scoreboard_panel`, `register_scoreboard_player_action`, `show_overlay_panel`, `show_overlay_menu`, `hide_overlay_menu`, `show_prompt`, `show_notice`.
- Input: `register_hotkey`, `capture_hotkey_input`, `clear_hotkey_capture`, `was_hotkey_pressed`.
- Chat: `register_chat_filter`, `register_chat_command`.
- Messaging: `send_message_to_server`.

### HUD Drawing

`on_gameplay_hud_draw(canvas)` is the simplest way to draw client UI:

```lua
function plugin.on_gameplay_hud_draw(canvas)
    local host = plugin.host
    local text = "Ping: " .. tostring(host.get_local_ping_milliseconds()) .. " ms"
    canvas.draw_bitmap_text(text, 12, 12, host.color(255, 255, 255, 255))
end
```

Draw callbacks run often. Keep them fast, avoid file I/O, and cache anything expensive.

### Client Options

Implement `get_options_sections()` to expose settings in plugin options. A section contains option items with labels and callbacks. The existing templates are the best reference for supported option item shapes.

Typical option kinds include simple toggles, numeric choices, enum choices, and hotkey capture entries. Save user configuration with `host.save_json_config(...)`.

### Client Chat And UI Registration

Prefer structured registration APIs over ad hoc event parsing:

```lua
function plugin.initialize(host)
    plugin.host = host

    host.register_chat_command({
        name = "localping",
        usage = "/localping",
        handler = function(command)
            host.show_notice("Ping: " .. tostring(host.get_local_ping_milliseconds()) .. " ms", 160, false)
            return true
        end
    })
end
```

Use scoreboard player actions, HUD widgets, prompts, and overlay panels when your plugin adds reusable UI instead of drawing everything in one global callback.

## Server Plugins

Use a server plugin when the feature needs authority: commands, moderation, game rules, scheduling, state changes, voting, replicated data, or client/server plugin messages.

Common server callbacks:

- `on_server_starting()`, `on_server_started()`
- `on_server_stopping()`, `on_server_stopped()`
- `on_server_heartbeat(seconds)`
- `on_client_connected(e)`, `on_client_disconnected(e)`
- `on_player_joined(e)`, `on_player_left(e)`
- `on_player_spawned(e)`, `on_player_respawned(e)`
- `on_player_team_changed(e)`, `on_player_class_changed(e)`
- `on_chat_received(e)`
- `on_score_changed(e)`, `on_round_ended(e)`
- `on_map_changing(e)`, `on_map_changed(e)`
- `on_damage(e)`, `on_death(e)`, `on_assist(e)`
- `on_build(e)`, `on_destroy(e)`
- `on_client_plugin_message(e)`
- `shutdown()`

Useful server host APIs include:

- State queries: `get_server_state`, `get_match_state`, `get_players`, `get_player_state`.
- World queries: `get_objectives`, `get_buildables`, `get_projectiles`, `get_recent_events`, `get_map_region`, `has_line_of_sight`.
- Commands: `register_command`, `resolve_targets`.
- Chat: `broadcast_system_message`, `send_system_message`.
- Scheduling: `schedule_once`, `schedule_repeating`, `cancel_scheduled_task`.
- Safe mutation: `enqueue_action` and `try_*` mutation APIs.
- Replication: `set_player_replicated_state_bool`, `set_player_replicated_state_int`, `set_player_replicated_state_float`, clear/read variants.
- Messaging: `send_message_to_client`, `broadcast_message_to_clients`.
- Admin/server tools: cvars, bots, map changes, demo recording, bans, player scale/speed/gravity, respawn timing.
- Gameplay extension: register abilities, loadouts, slot items, weapon items, primary weapon behavior, and ability executors.

### Commands

Prefer `host.register_command(...)` over parsing chat manually. Registered commands route through the host command system and can declare permissions.

```lua
local plugin = {}

function plugin.initialize(host)
    plugin.host = host

    host.register_command({
        name = "where",
        aliases = { "pos" },
        usage = "/where",
        permission = "ViewServerState",
        handler = function(context)
            context.require_permission("ViewServerState")

            local identity = context.identity
            local slot = identity ~= nil and (identity.sourceSlot or identity.SourceSlot or identity.source_slot) or nil
            if slot == nil then
                return "This command is only available from player chat."
            end

            local state = host.get_player_state(slot)
            if state == nil then
                return "No player state is available."
            end

            return "Position: " .. tostring(state.worldX) .. ", " .. tostring(state.worldY)
        end
    })
end

return plugin
```

Command handlers receive `(context, arguments)` and return nil, a string, or a table of strings as the command response. The context exposes `identity`, `source`, `arguments`, `has_permission(...)`, and `require_permission(...)`.

Command permissions use the server's built-in admin permission flags: `ViewServerState`, `ManagePlayers`, `ManageMatch`, `ManageServerConfiguration`, `ManagePlugins`, `ManageScheduler`, and `FullAccess`. Permission names are case-insensitive and can be combined with spaces, commas, pipes, or semicolons.

### Cancellable Decision Hooks

Server plugins can participate in decisions before the host applies them. Use these for rules, restrictions, voting gates, or custom game modes.

Available decision hooks include:

- `before_chat_message(e)`
- `before_team_change(e)`
- `before_class_change(e)`
- `before_loadout_change(e)`
- `before_spawn(e)`
- `before_damage(e)`
- `before_death(e)`
- `before_pickup(e)`
- `before_score(e)`
- `before_round_end(e)`
- `before_map_change(e)`

Return the result shape expected by the hook. Typical decisions are "allow", "cancel", or "replace/modify selected fields" depending on the event. Keep decision hooks fast and deterministic. Do not do slow file I/O or long-running work inside them.

### Safe Mutation

Some callbacks run while the engine is already processing an event. When possible, request mutations through `host.enqueue_action(...)` or a validated `try_*` API so the host can run the change at a safe point.

Examples of server mutation APIs include team/class changes, map changes, bot changes, respawn timing, cvar updates, bans, demo recording, gameplay item grants, damage/healing/impulses, status effects, and projectile spawning.

If a `try_*` API returns false or a failure result, treat that as authoritative. The host rejected the request.

### Read-Only State

Use query APIs instead of keeping your own stale model:

```lua
local players = plugin.host.get_players()
local match = plugin.host.get_match_state()
local objectives = plugin.host.get_objectives()
local events = plugin.host.get_recent_events()
```

State tables are snapshots. Query again when you need fresh data.

## Client/Server Messaging

Client and server plugins can exchange plugin messages:

- Client to server: `host.send_message_to_server(targetPluginId, messageType, payload)`
- Server to one client: `host.send_message_to_client(slot, targetPluginId, messageType, payload)`
- Server broadcast: `host.broadcast_message_to_clients(targetPluginId, messageType, payload)`

The simple Lua forms use text payloads with schema version `1`. Server Lua can also pass trailing `payloadFormat` and `schemaVersion` arguments for server-to-client messages. Client Lua currently sends text payloads.

Declare stable message contracts in `plugin.json` when another plugin or the opposite side is expected to understand your messages:

```json
{
  "messageContracts": [
    {
      "targetPluginId": "example.author.my-plugin",
      "messageType": "vote_state",
      "payloadFormat": "Text",
      "schemaVersion": 1,
      "direction": "ServerToClient"
    }
  ]
}
```

Use versioned message names or `schemaVersion` when you change payload structure.

## Configuration

Use JSON config for user-editable settings:

```lua
local defaults = {
    enabled = true,
    opacity = 0.8
}

local function load_config(host)
    local loaded = host.load_json_config("settings.json", defaults)
    if loaded == nil then
        return defaults
    end

    if loaded.enabled == nil then
        loaded.enabled = defaults.enabled
    end
    if loaded.opacity == nil then
        loaded.opacity = defaults.opacity
    end
    return loaded
end
```

Keep config files small and plugin-scoped. Use `config.schema.json` to document settings and help future UI/schema validation.

## Assets

Register assets during initialization or another safe setup callback. Use paths relative to your plugin folder or declared asset directories.

Client asset APIs include:

- `register_texture_asset`
- `register_texture_atlas_asset`
- `register_texture_region_asset`
- `register_legacy_animation_asset`
- `register_sound_asset`
- `play_sound`

Do not assume assets can be loaded from outside the plugin folder.

## Permissions

Server command permissions are enforced through the built-in admin permission flags. Plugins can also declare permission metadata in the manifest so admins and future tooling can understand what the plugin may require:

```json
{
  "permissions": [
    {
      "id": "ViewServerState",
      "description": "Allows read-only server state commands.",
      "required": false
    }
  ]
}
```

For admin-style actions, set `permission` on `host.register_command(...)` and call `context.require_permission(...)` inside command handlers. Avoid duplicating authentication logic by parsing chat yourself.

## Performance And Safety

Follow these rules:

- Keep draw callbacks and decision hooks fast.
- Do not block the game loop with long file operations, network calls, or large loops.
- Prefer host query APIs over cached assumptions.
- Treat all event/state objects as read-only snapshots.
- Use registered commands and host permission checks for admin behavior.
- Use `enqueue_action` or validated `try_*` APIs for mutations.
- Keep plugin files, configs, and assets inside the plugin's own directories.
- Log clear messages with `host.log`, especially during initialization.
- Design message payloads and config files so old versions can be ignored or migrated.

Lua runs in-process with the game. The host validates many operations, but plugins should still be written as trusted local code.

## Debugging

Start with these checks:

1. Confirm `plugin.json` is valid JSON.
2. Confirm `entryPoint` points to an existing Lua file inside the plugin folder.
3. Confirm `type` matches where you installed the plugin: `Client` for client plugins, `Server` for server plugins.
4. Confirm `compatibility.hostApiVersion` is `1.0`.
5. Add `host.log(...)` calls in `initialize` and key callbacks.
6. Call `host.get_host_api()` if you are unsure whether a function exists on the current host.
7. Compare your code with the closest template under [Templates](Templates).

For client UI issues, first verify the plugin initializes, then verify the callback runs, then verify coordinates/colors/assets. For server behavior, verify command registration and permissions before debugging mutation APIs.

## Packaging Checklist

Before sharing a plugin:

- `plugin.json` has a stable `id`, `displayName`, `version`, `type`, `runtime`, `entryPoint`, and `compatibility.hostApiVersion`.
- The plugin folder contains only files the plugin needs.
- All paths are relative and stay inside the plugin folder.
- Config defaults are documented and safe.
- Commands are registered through `host.register_command`.
- Permissions are declared and enforced where needed.
- Message contracts are declared for cross-plugin or client/server messages.
- The plugin was tested after a fresh restart.
- Logs are useful but not spammy.

## Where To Look Next

- [Templates](Templates): copy these first.
- [USER_GUIDE.md](USER_GUIDE.md): installation and enabling/disabling plugins.
- [PLUGIN_HOST_CONTRACT.md](PLUGIN_HOST_CONTRACT.md): deeper host contract notes for engine contributors.
- `host.get_host_api()`: runtime truth for the current client/server API surface.
