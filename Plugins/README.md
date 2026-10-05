# Plugins

Use the [user guide](USER_GUIDE.md) to install plugins and the
[authoring guide](AUTHORING_GUIDE.md) to write them. Gameplay abilities and custom
weapons have a separate [authoring guide](GAMEPLAY_ABILITIES.md).

## Source layout

- `Client/` and `Server/` contain the host contracts. The Lua hosts implement them;
  they are not a public C# plugin API.
- `GameplayModding.Abstractions/` contains shared gameplay-mod contracts.
- [Templates](Templates/README.md) provide authoring examples.
- [Packaged](Packaged/README.md) contains the Lua plugins distributed with the game.

Contract projects end in `.Abstractions`. Plugins themselves are Lua folders
(`plugin.json` plus scripts), not projects.

## Runtime and packaging

Both hosts load Lua plugins from `plugin.json` manifests. Runtime plugins load from
`Plugins/Client/` or `Plugins/Server/` relative to the application directory.
In a release archive that directory is under `app/`.

Normal application builds and `scripts/package.ps1` copy the packaged Lua plugins
into those runtime directories. Application projects should reference plugin
contracts, not sample implementations merely to copy them into an output.

Lua is the only plugin runtime. The hosts never load plugin assemblies: a
manifest with another `runtime` is skipped with a log line, and DLLs in the plugin
folders are ignored. The old C# sample plugins were removed once their Lua
versions shipped.

Plugin configuration belongs under `config/plugins/client/<pluginId>/` or
`config/plugins/server/<pluginId>/` in the application's user-data directory.

## Extending the API

Plugins are written in Lua. Client APIs cover presentation,
input callbacks, HUDs, audio, and settings. Server APIs cover events, validated
commands, replicated state, messaging, and registered gameplay behaviors.

When Lua lacks a capability, prefer a reusable host API with explicit ownership,
permissions, callback timing, and resource limits. Do not expose mutable engine
objects or add a special case for a single plugin. Behavior that Lua cannot
serve (native interop, engine-private state, hot paths) belongs in the engine as
a built-in, exposed to Lua through a validated operation if plugins need it.

The [host contract](PLUGIN_HOST_CONTRACT.md) defines these requirements and the
limits of in-process execution.

## Profiling

Set `OG2_CLIENT_PLUGIN_PROFILE=1` before launching the client. The host reports
aggregate hook timings every five seconds, including the plugin, hook, call
count, total time, average time, and maximum time. Profile the same plugin layout
that will be distributed.
