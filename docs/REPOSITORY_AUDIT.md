# Repository audit

Audit date: 2026-08-21

This audit covers the tracked GitHub repository and the local build graph. `Modern/` is intentionally excluded.

## Executive summary

OpenGarrison is a multi-host .NET 10 application, not a single game executable. The desktop client, browser client, dedicated server, server launcher, updater, shared simulation, protocol, transport layer, modding contracts, plugin packages, maps, content pipeline, API service, and offline authoring tools all have distinct owners. Most root folders are active; the main problem was inconsistent grouping rather than large amounts of dead runtime code.

The safe first pass reduces the tracked root by three directories:

- `BotBrain.Tools/` moved to `Tools/BotBrain/`.
- `MotionProof.Tools/` moved to `Tools/MotionProof/`.
- `GameplayModding.Abstractions/` moved to `Plugins/GameplayModding.Abstractions/`.

It also removes two zero-byte C# files and stops tracking the generated `Tools/NavAuthoringLegacy/extracted/` snapshot. The pinned extraction script reproduces all 23 snapshot files byte-for-byte after newline normalization, so Git history remains the source of truth.

## Runtime and build topology

| Area | Role | Evidence | Decision |
| --- | --- | --- | --- |
| `Client/` | Desktop MonoGame host, rendering, menus, input, networking, hosting UI, and client plugin runtime | Executable project, solution member, CI build, packaging input | Keep |
| `Client.Browser/` | Blazor/KNI browser host | Solution member, CI restore/build, documented smoke path | Keep |
| `Client.Shared/` | Shared browser/desktop bootstrap, assets, configuration, and social contracts | Referenced by both client hosts and tools | Keep |
| `Core/` | Authoritative simulation, entities, maps, content metadata, bots, and shared gameplay | Referenced by client, server, tools, plugins, and tests | Keep |
| `Protocol/` | Network contracts and serialization | Referenced by client, server, networking, browser, and tests | Keep |
| `Networking/` | UDP/QUIC scheduling and connection primitives | Referenced by client/server and networking tests | Keep |
| `Server/` | Dedicated authoritative server and transports | Executable project, solution member, CI build, packaged | Keep |
| `ServerLauncher/` | Graphical server-launch mode hosted by the client runtime | Solution member and release packaging input | Keep |
| `Updater/` | Update verification, download, apply, and launch entrypoint | Solution member and release root entrypoint | Keep |
| `Bootstrap/` | Shared .NET prerequisite bootstrap source | Compile-linked into both client and updater | Keep at root until it becomes a dedicated shared project |
| `Plugins/` | Plugin host contracts, gameplay-mod contracts, CLR migration projects, Lua packages, templates, and docs | Runtime project references, build staging targets, packaging discovery | Keep; contracts consolidated here |
| `Maps/` | Distributable custom-map payload | Consumed by `MapPackageBuilder` and release packaging | Keep |
| `SourceAssets/` | Editable, non-runtime originals and retained source fixtures | Explicit source-of-truth policy in `docs/content-layout.md` | Keep; do not ship directly |
| `services/` | Server registry, presence, relay, and updater-manifest API | Live client/server endpoints and release workflow inputs | Keep |
| `Tests/` | Networking, gameplay, plugin-host, and browser smoke tests | CI test targets | Keep |
| `Tools/` | Asset builders, map publisher, replay diagnostic, API generator, BotBrain, MotionProof, traversal, and legacy extraction | Direct build targets, MSBuild hooks, scripts, and manual authoring workflows | Keep; consolidated |
| `scripts/` | Packaging and repository/content policy entrypoints | CI/release workflows and README commands | Keep |
| `packaging/` | Release documentation and default configuration payload | Release packaging input | Keep |
| `docs/` | Architecture, protocol, browser, registry, and design references | Developer documentation | Keep; review blueprints periodically |
| `.github/` | CI and release workflows | GitHub Actions | Keep |
| `.vscode/` | Shared launch/build configuration | Developer convenience, no runtime effect | Keep unless the team standardizes on editor-neutral setup |

## Deprecated, generated, and misleading areas

### Safe removals completed

- `Client/Game/Core/Game1.ViewState.cs` was emptied in commit `c36ee737`; SDK-style compilation gained nothing from retaining it.
- `Client/Game/Gameplay/Rendering/Core/Game1.HalvedWeaponSprites.cs` was introduced as an empty file and never contained implementation.
- `Tools/NavAuthoringLegacy/extracted/` was a generated, intentionally non-compiling historical snapshot. Its README identifies commit `14b0d1ff` as the source, and `extract-legacy-nav-authoring.ps1` recreates it exactly. The extractor and README remain tracked; generated output is ignored.

### Names that look obsolete but are active

- `Core/BotAI/` is still used by the live BotBrain graph builder, navigation editor, menus, server startup diagnostics, tests, and BotBrain tooling. Do not delete it based on its older namespace.
- Legacy replay, preference, map-import, and network compatibility code has live callers and tests. “Legacy” here means compatibility behavior, not dead code.
- `.opengarrison-auto-converted` map markers are written and read by the live legacy-map package converter.
- `SourceAssets/` is intentionally excluded from runtime packages but remains editable source material.
- `MotionProof` is not part of the game runtime, but it is a healthy, compiling navigation proof/bake tool; it belongs under `Tools/`, not in the trash.

## Repository weight

At the audited commit, the non-`Modern` tree contains roughly 9,383 files and 196 MB of logical content. The largest current categories are BotBrain JSON navigation data, audio source/runtime files, generated BotBrain binary navigation data, C# source, and sprite frames. Current cross-root exact duplicates account for only about 0.42 MB, so filename-level deduplication would add risk without meaningful savings.

The local Git object database is much larger (about 750 MB packed) because history contains accidentally committed caches and build artifacts, including `.dotnet-home/.nuget`, `.build-temp`, `_test_out`, and generated browser bundles. Deleting current files does not remove historical blobs.

History cleanup requires a separate, coordinated operation using `git filter-repo` or an equivalent tool, followed by a protected force-push and fresh clones for collaborators. Do not mix that operation with ordinary layout changes.

## Verification baseline

- Full solution build: succeeded with 0 errors.
- Networking tests: 17/17 passed.
- Gameplay/plugin-host tests: 1,910 passed and 2 failed in the pre-cleanup worktree. Both failures concern QUIC fallback expectations in `NetworkEndpointTests` and coincide with an in-progress local change to `Client/Networking/NetworkEndpoint.cs`; they are not caused by the repository moves.
- Gameplay-pack layout policy: passed.
- Packaged-content policy self-tests: passed.
- Lua API generator output was refreshed and its check was added to CI.
- Root-layout policy now evaluates tracked and unignored content and is run by CI.

## Recommended next phases

1. Land this low-risk organization pass independently from gameplay changes.
2. Review and remove the ignored local legacy roots only after their untracked canaries, MotionProof script, candidates, and logs are backed up or intentionally discarded.
3. Perform a clean-clone package and browser smoke test before release.
4. Decide separately whether to rewrite Git history to purge caches/build outputs. This is the only change that will materially reduce clone size.
5. Consider a future `src/` migration only from a clean worktree. Moving the active client/core/server projects now would overlap a large in-progress gameplay change set and create unnecessary merge risk.
