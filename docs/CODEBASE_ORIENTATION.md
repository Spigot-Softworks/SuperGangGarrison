# Codebase orientation

OpenGarrison shares its C# simulation across desktop, browser, dedicated-server,
and embedded-server hosts. Source paths below are relative to the repository root.

## Projects

| Area | Responsibility |
| --- | --- |
| `Core/` | Entities, fixed-step simulation, collision, combat, objectives, maps, gameplay definitions, bots, and common configuration |
| `Client/` | Desktop host and shared `Game1` presentation, input, and session code |
| `Client.Browser/` | Blazor WebAssembly/KNI host, browser storage, assets, transport, and audio |
| `Client.Shared/` | Client bootstrap, asset loading, identity, presence, and shared services |
| `Server/` | Authoritative sessions, transports, snapshots, rounds, administration, and server plugins |
| `SessionRuntime/` | Embedded server compiled from the server runtime for local hosting |
| `Protocol/`, `Networking/` | Message serialization, delivery rules, connection management, and scheduling |
| `Plugins/` | Plugin contracts, Lua packages, gameplay-mod APIs, and CLR implementations |
| `Updater/`, `Bootstrap/` | Release installation, launch, and runtime prerequisite handling |
| `services/opengarrison-api/` | Accounts, registry, presence, room admission, signaling, and relay |

The projects target .NET 10. The browser builds the client with
`OpenGarrisonClientAsLibrary=true` and uses AOT publishing. See the
[browser build guide](../Client.Browser/README.md) for the supported commands.

## Gameplay and sessions

`SimulationWorld`, split across `Core/Simulation/`, owns gameplay updates.
`PlayerEntity`, under `Core/Entities/Players/`, holds player state and behavior.
Definitions in `Core/Content/Gameplay/stock.gg2/` supply the stock classes, items,
and loadouts interpreted by the simulation.

The server owns damage, death, objectives, and round transitions. The client uses
the shared model for prediction and reconciles against server snapshots. Embedded
sessions use the server runtime through `SessionRuntime/`; some local Practice
paths schedule the simulation directly from the client.

Bot decisions run through `BotBrainPracticeBotController` and one
`BotBrainController` per bot. The adapter is shared by dedicated servers, embedded
sessions, and offline Practice despite its name. See the
[bot runtime guide](../Core/BotBrain/README.md) for navigation ownership.

## Client and networking

`Client/Game/Core/Game1.cs` owns the host lifecycle. Its partial classes and
controllers handle menus, input, sessions, prediction, HUDs, rendering, and audio.
Follow both a controller and its owning `Game1` fields when changing shared state.

`NetworkGameClient` connects client input and incoming messages to session and
snapshot handlers. `GameServer.Startup` assembles the server world, transports,
session manager, commands, and plugins. Incoming and outgoing dispatchers apply
message direction, authorization, and delivery rules.

The name *Protocol64* identifies a framing/transport implementation; it is not the
current compatibility version. The version required by a build is defined in
[ProtocolVersion.cs](../Protocol/ProtocolVersion.cs). Keep client and server
packages compatible when changing messages.

## Content and configuration

- `Core/Content/` contains runtime content and its metadata.
- `SourceAssets/` contains editable originals and development fixtures.
- `Maps/` contains distributable custom maps.
- `Tools/` builds content, browser manifests/atlases, and navigation assets.
- Build and publish outputs are generated; do not edit them as source files.

[RuntimePaths.cs](../Core/Configuration/RuntimePaths.cs) resolves application
assets and user data. Runtime configuration normally lives under the per-user
OpenGarrison directory. Use `--user-data-root <directory>` or
`OPENGARRISON_USER_DATA_ROOT` for isolated instances. Packaged default config files
are separate from a player's saved settings.

## Related guides and tests

[Plugin authoring](../Plugins/AUTHORING_GUIDE.md) describes the extension APIs.
[Gameplay authoring](../Plugins/GAMEPLAY_ABILITIES.md) covers stock data and
executors. [Voice chat and Jukebox](design/VOICE_CHAT_JUKEBOX.md) covers audio
controls, routing, and server playlists.

`Tests/OpenGarrison.PluginHost.Tests/` covers gameplay, client/server behavior,
protocols, and plugins despite its narrow name. Networking and updater checks
have separate projects under `Tests/`. Browser smoke tests live in
`Tests/BrowserSmoke/` and require their Node dependencies and Playwright browser.
