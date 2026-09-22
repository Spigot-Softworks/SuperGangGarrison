# Plugins

Use the [user guide](USER_GUIDE.md) to install plugins and the
[authoring guide](AUTHORING_GUIDE.md) to write them. Gameplay abilities and custom
weapons have a separate [authoring guide](GAMEPLAY_ABILITIES.md).

## Source layout

- `Client/` and `Server/` contain host contracts and CLR plugin projects.
- `GameplayModding.Abstractions/` contains shared gameplay-mod contracts.
- [Templates](Templates/README.md) provide authoring examples.
- [Packaged](Packaged/README.md) contains the Lua plugins distributed with the game.

Keep plugin projects in this tree. Use
`OpenGarrison.Client.Plugins.<PluginName>` or
`OpenGarrison.Server.Plugins.<PluginName>` for project and assembly names;
contract projects end in `.Abstractions`.

## Runtime and packaging

Both hosts support `plugin.json` manifests. Runtime plugins load from
`Plugins/Client/` or `Plugins/Server/` relative to the application directory.
In a release archive that directory is under `app/`.

Normal application builds and `scripts/package.ps1` copy the packaged Lua plugins
into those runtime directories. Application projects should reference plugin
contracts, not sample implementations merely to copy them into an output.

CLR implementations with Lua replacements remain as migration references.
Packaging them requires `-IncludeLegacyClrPlugins`, which places them under
`LegacyPlugins/Client/` and `LegacyPlugins/Server/`. For migration testing,
`StageLegacyPluginRuntimeOutput=true` enables their older live output layout.

Plugin configuration belongs under `config/plugins/client/<pluginId>/` or
`config/plugins/server/<pluginId>/` in the application's user-data directory.

## Extending the API

Lua is the default language for new plugins. Client APIs cover presentation,
input callbacks, HUDs, audio, and settings. Server APIs cover events, validated
commands, replicated state, messaging, and registered gameplay behaviors.

When Lua lacks a capability, prefer a reusable host API with explicit ownership,
permissions, callback timing, and resource limits. Do not expose mutable engine
objects or add a special case for a single plugin. CLR-only extensions are
appropriate when native interop, engine-private access, or a demonstrated
performance requirement cannot be served by the Lua API.

The [host contract](PLUGIN_HOST_CONTRACT.md) defines these requirements and the
limits of in-process execution. Prefer the packaged Lua version when a CLR
implementation has an equivalent replacement.

## Profiling

Set `OG2_CLIENT_PLUGIN_PROFILE=1` before launching the client. The host reports
aggregate hook timings every five seconds, including the plugin, hook, call
count, total time, average time, and maximum time. Profile the same plugin layout
that will be distributed.
