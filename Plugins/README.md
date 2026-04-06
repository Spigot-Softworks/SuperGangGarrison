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
- `scripts/package.ps1` copies manifest-driven packaged Lua plugins from `Plugins/Packaged/Client/...` and `Plugins/Packaged/Server/...` into the shipped runtime `Plugins/Client/...` and `Plugins/Server/...` folders.
- `scripts/package.ps1` does not ship legacy CLR plugins by default.
- Legacy CLR plugin projects can still be published explicitly with `-IncludeLegacyClrPlugins`, and they are staged under `LegacyPlugins/Client/...` and `LegacyPlugins/Server/...` so they do not collide with the canonical Lua plugin ids in the live runtime plugin directories.
- Do not add app-project references to bundled/sample plugins just to get them copied into builds or packages.
- App projects may reference plugin abstraction projects when they need shared plugin interfaces.

## Runtime Conventions

- Client plugins are loaded from `Plugins/Client`.
- Server plugins are loaded from `Plugins/Server`.
- Both hosts support manifest-driven packaged plugins via `plugin.json`.
- CLR plugins still load through the existing interface-based runtime contracts.
- Lua plugins now have first-pass hosts on both client and server, but the
  exposed surfaces are intentionally bounded and engine-shaped rather than raw
  engine object access.
- Client plugin config should live under `config/plugins/client/<pluginId>/`.
- Server plugin config should live under `config/plugins/server/<pluginId>/`.

## Lua-First Policy

- Lua is the default plugin language for OpenGarrison.
- New plugin proposals should start from the assumption that they will be implemented as Lua plugins.
- When a plugin author needs a capability that Lua does not expose yet, the default response is to design and add a bounded reusable Lua seam rather than approve a one-off CLR plugin.
- CLR plugins are the exception path and should be reserved for engine-internal, unsafe-to-expose, or clearly performance-critical extensions.
- The goal is one coherent modding surface for contributors and modders, not parallel Lua and CLR ecosystems with overlapping responsibilities.

## Seam Expansion Rubric

- Add a new Lua seam when the requested capability is reusable, engine-shaped, and likely to benefit more than one plugin.
- Add a new Lua seam when the capability can be expressed as a stable callback, service, DTO, asset registration surface, or bounded command.
- Do not add a seam that simply hands Lua broad access to unstable engine internals, raw object graphs, or authority it cannot safely own.
- Prefer service-style APIs over exposing engine objects directly.
- Prefer validated and bounded operations over arbitrary mutation.
- If a requested feature would make the host API materially messier for everyone else while only serving one niche plugin, stop and reassess the design before exposing it.

## CLR Exception Criteria

- A CLR plugin is acceptable only when exposing the required capability to Lua would be architecturally unsound.
- Typical reasons include deep engine-internal rendering ownership, deterministic simulation-core behavior that should remain native, platform/native interop, or a clearly demonstrated performance constraint that cannot be solved with a bounded Lua seam.
- “Lua cannot do this today” is not, by itself, enough reason to approve a CLR plugin.

## Lua Plugin Scope

- Client Lua is currently suited for presentation plugins such as menu helpers,
  background overrides, HUD widgets, scoreboard panels, simple audio cues, and
  config-backed options.
- Server Lua is currently suited for passive/event-driven plugins plus bounded
  mutations through admin operations, replicated state, and client messaging.
- Lua authoring templates live under `Plugins/Templates/`.
- Ready-to-run packaged Lua examples live under `Plugins/Packaged/` and are copied into packaged runtime outputs.

## Deprecation Direction

- Existing bundled CLR plugins should be treated as migration references where a Lua equivalent now exists.
- When a bundled CLR plugin reaches feature parity with a packaged Lua version, prefer the Lua version for documentation, examples, and future seam growth.
- Packaged runtime outputs should ship the canonical Lua plugin versions by default, while the old CLR projects remain source-side migration references.
- Native bundled plugins should shrink over time toward engine-adjacent reference implementations rather than remain the primary modding path.

## Repo Rules

- Do not create new top-level `OpenGarrison.*.Plugins.*` projects outside this tree.
- Keep sample/bundled plugins here so source layout, build layout, and package layout stay aligned.
