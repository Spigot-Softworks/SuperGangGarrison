# Plugin Conventions

`Plugins` is the only source tree for plugin projects in this repo.

## Layout

- Client plugin abstractions live under `Plugins/Client/OpenGarrison.Client.Plugins.Abstractions/`.
- Client plugin implementations live under `Plugins/Client/OpenGarrison.Client.Plugins.<PluginName>/`.
- Server plugin abstractions live under `Plugins/Server/OpenGarrison.Server.Plugins.Abstractions/`.
- Server plugin implementations live under `Plugins/Server/OpenGarrison.Server.Plugins.<PluginName>/`.

## Naming

- Project and assembly names should follow `OpenGarrison.Client.Plugins.<PluginName>` or `OpenGarrison.Server.Plugins.<PluginName>`.
- Abstraction projects should end in `.Abstractions`.
- The runtime plugin folder name should match the suffix after `OpenGarrison.{Client|Server}.Plugins.` when possible.

## Build And Packaging

- Plugin implementation projects own their own output path into the runtime plugin folders under `Client/bin/.../Plugins/Client/<PluginName>/` or `Server/bin/.../Plugins/Server/<PluginName>/`.
- `scripts/package.ps1` auto-discovers non-abstraction plugin projects under this tree and publishes them into packaged `Plugins/Client/...` or `Plugins/Server/...`.
- Do not add app-project references to bundled/sample plugins just to get them copied into builds or packages.
- App projects may reference plugin abstraction projects when they need shared plugin interfaces.

## Runtime Conventions

- Client plugins are loaded from `Plugins/Client`.
- Server plugins are loaded from `Plugins/Server`.
- Client plugin config should live under `config/plugins/client/<pluginId>/`.
- Server plugin config should live under `config/plugins/server/<pluginId>/`.

## Repo Rules

- Do not create new top-level `OpenGarrison.*.Plugins.*` projects outside this tree.
- Keep sample/bundled plugins here so source layout, build layout, and package layout stay aligned.
