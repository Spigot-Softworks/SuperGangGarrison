# OpenGarrison Fork

OpenGarrison is a C# and MonoGame reimplementation of the Gang Garrison 2 gameplay stack.

This repository contains the OpenGarrison solution and supporting tools.

## Repo Layout

- `Core/`: shared gameplay, simulation, entities, map import, content metadata, and common runtime logic.
- `Client/`: MonoGame client, menus, HUD, rendering, networking, hosting UI, and client plugin host.
- `Client.Browser/`: Blazor WebAssembly/KNI browser host for offline browser gameplay and future browser networking.
- `Client.Shared/`: shared runtime bootstrap and browser/desktop asset-loading seams used by the client hosts.
- `Server/`: dedicated server runtime, networking, sessions, snapshots, admin commands, plugins, and map rotation.
- `Protocol/`: network message contracts and binary serialization.
- `Core/BotBrain/`: authoritative bot behavior and navigation runtime.
- `BotBrain.Tools/`: offline BotBrain navigation asset generation and validation tools.
- `Plugins/`: client and server plugin abstractions, Lua plugin packages, and legacy CLR migration references.
- `GameplayModding.Abstractions/`: early gameplay-mod support contracts.
- `ServerLauncher/`: launcher-focused entry point built on the client runtime.
- `packaging/`: release packaging notes and default packaged config files.
- `scripts/`: packaging entry points.
- `Tools/Browser*/`: browser publish and atlas/manifest generation tools.
- `Tests/BrowserSmoke/`: Playwright smoke test for the browser host.
- `docs/`: focused design and reference notes.

## Build

From the repo root:

```powershell
dotnet build .\OpenGarrison.sln -c Debug
```

OpenGarrison targets .NET 10.


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

Browser AOT publish:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Browser\publish-browser.ps1
python -m http.server 5014 -d .\artifacts\browser-publish-aot\wwwroot
```

Then open `http://127.0.0.1:5014/`. 
## Packaging

Packaging is handled by the existing scripts in `scripts/` and docs in `packaging/`.

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

Release packages should pass an explicit updater version:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Platforms win-x64 -Version 1.0.1
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
