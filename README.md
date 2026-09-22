# OpenGarrison Fork

OpenGarrison is a C# reimplementation of Gang Garrison 2, with a MonoGame desktop
client, a browser client, and a dedicated server.

## Build and run

Install the .NET 10 SDK. Run these commands from the repository root:

```powershell
dotnet workload restore .\OpenGarrison.sln
dotnet build .\OpenGarrison.sln -c Debug
```

Launch the desktop client, dedicated server, or graphical server launcher:

```powershell
dotnet run --project .\Client\OpenGarrison.Client.csproj
dotnet run --project .\Server\OpenGarrison.Server.csproj
dotnet run --project .\ServerLauncher\OpenGarrison.ServerLauncher.csproj
```

The browser uses an AOT publish. See the [browser guide](Client.Browser/README.md)
for workload setup, local serving, and smoke tests.

## Repository layout

| Directory | Purpose |
| --- | --- |
| `Core/` | Shared simulation, entities, map loading, gameplay content, and bots |
| `Client/` | Desktop host and shared game presentation/input code |
| `Client.Browser/` | Blazor WebAssembly/KNI host and browser services |
| `Client.Shared/` | Shared client bootstrap, assets, settings, and social services |
| `Server/` | Dedicated server, sessions, administration, and server plugins |
| `SessionRuntime/` | Embedded server used by locally hosted sessions |
| `Protocol/`, `Networking/` | Message contracts, serialization, and transports |
| `ServerLauncher/` | Graphical server launch mode |
| `Updater/`, `Bootstrap/` | Update installation and runtime prerequisites |
| `Plugins/` | Plugin APIs, Lua packages, templates, and CLR implementations |
| `Maps/`, `SourceAssets/` | Distributable custom maps and editable source assets |
| `Tools/` | Content builders, navigation diagnostics, and authoring tools |
| `Tests/` | Gameplay, networking, updater, and browser checks |
| `services/` | Account, server registry, presence, and room API |
| `scripts/`, `packaging/` | Release scripts, packaging instructions, and default config |
| `docs/` | Contributor and server administration guides |

## Documentation

- [Codebase orientation](docs/CODEBASE_ORIENTATION.md)
- [Server management and player titles](docs/SERVER_MANAGEMENT.md)
- [Voice chat and Jukebox](docs/design/VOICE_CHAT_JUKEBOX.md)
- [Bot runtime and navigation](Core/BotBrain/README.md)
- [Player skins and animation authoring](SourceAssets/Sprites/PlayerSkins/README.md)
- [Installing plugins](Plugins/USER_GUIDE.md) and [writing plugins](Plugins/AUTHORING_GUIDE.md)
- [API setup](services/opengarrison-api/README.md)
- [Documentation index](docs/README.md)

## Packaging

Run packaging from the repository root. Supply your release version explicitly;
the version below is an example, not the current release number.

```powershell
$releaseVersion = "1.0.2"
.\scripts\package.ps1 -Platforms win-x64 -Version $releaseVersion
```

On Linux with PowerShell:

```bash
pwsh ./scripts/package.ps1 -Platforms linux-x64 -Version 1.0.2
```

Linux packaging requires a compatible `libmsquic.so.2`. Cross-builds can supply it
with `-LinuxMsQuicLibraryPath`. See the [packaging quickstart](packaging/DISTRO_QUICKSTART.txt).

Packaged players launch `Super Gang Garrison.exe` on Windows or `./OG2` on
Linux/macOS. The updater starts the game from the package's `app/` directory.
See the [packaged installation notes](packaging/README.txt).

To prepare delta updates against the currently published packages:

```powershell
.\scripts\fetch-update-delta-bases.ps1 -Platforms @("linux-x64", "win-x64") -Channel stable -OutputDirectory .\dist\delta-bases
.\scripts\package.ps1 -Platforms @("linux-x64", "win-x64") -Version $releaseVersion -DeltaBaseDirectory .\dist\delta-bases
```

The packager writes delta and full-package entries into the update manifest.
Clients verify the base files before applying a delta and fall back to the full
archive if the installed files do not match. Packaging does not run the test suite.
