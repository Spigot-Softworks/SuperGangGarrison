# OpenGarrison Fork

OpenGarrison is a C# and MonoGame reimplementation of the Gang Garrison 2 gameplay stack.

This repository contains the OpenGarrison solution and supporting tools.

## Repo Layout

- `Core/`: shared gameplay, simulation, entities, map import, content metadata, and common runtime logic.
- `Client/`: MonoGame client, menus, HUD, rendering, networking, hosting UI, and client plugin host.
- `Server/`: dedicated server runtime, networking, sessions, snapshots, admin commands, plugins, and map rotation.
- `Protocol/`: network message contracts and binary serialization.
- `BotAI/`: bot behavior and navigation runtime.
- `BotAI.Tools/`: offline bot navigation asset generation tools.
- `Plugins/`: client and server plugin abstractions, Lua plugin packages, and legacy CLR migration references.
- `GameplayModding.Abstractions/`: early gameplay-mod support contracts.
- `ServerLauncher/`: launcher-focused entry point built on the client runtime.
- `packaging/`: release packaging notes and default packaged config files.
- `scripts/`: packaging entry points.
- `docs/`: focused design and reference notes.

## Build

From the repo root:

```powershell
dotnet build .\OpenGarrison.sln -c Debug
```

OpenGarrison targets .NET 10.

## Plugins

- The plugin system now supports manifest-driven packaged plugins with
  `plugin.json`.
- Lua is the default direction for plugin authoring going forward.
- Packaged runtime outputs now ship the Lua plugin packages by default rather
  than the legacy CLR plugin implementations.
- Barebones Lua hosts exist on both server and client.
- Server Lua currently covers event-driven plugins, bounded mutation surfaces,
  replicated state, and plugin messaging.
- Client Lua currently covers menu/main-menu integration, HUD and scoreboard
  drawing, lightweight audio playback, config-backed options, camera offsets,
  dead-body rendering, and bubble-menu overrides for presentation plugins.
- See [Plugins/README.md] and [Plugins/Templates/README.md] for plugin conventions, authoring templates, and packaged examples.

## Run

Client:

```powershell
dotnet run --project .\Client\OpenGarrison.Client.csproj
```

Dedicated server:

```powershell
dotnet run --project .\Server\OpenGarrison.Server.csproj
```

Server launcher:

```powershell
dotnet run --project .\ServerLauncher\OpenGarrison.ServerLauncher.csproj
```

## Packaging

Packaging is handled by the existing scripts in `scripts/` and docs in `packaging/`.

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

Linux or macOS with PowerShell:

```bash
pwsh ./scripts/package.ps1
```

Bash wrapper:

```bash
bash ./scripts/build.sh linux-x64
```

See [packaging/DISTRO_QUICKSTART.txt] and [packaging/README.txt] for current packaging details.
