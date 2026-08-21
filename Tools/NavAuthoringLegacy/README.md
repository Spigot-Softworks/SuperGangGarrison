# Legacy nav authoring extraction

This folder preserves the last intact bot-navigation authoring stack from repo
history so it can be ported back deliberately.

## Source point

- Commit: `14b0d1ff`
- Reason: this is the last inspected commit that still contains the old
  `BotAI` authoring support and the full `Client/Game/Developer/Game1.BotNavEditor.cs`.
- Removal point: `ff362e17`, which purged the deprecated bot compatibility paths
  and collapsed the live nav editor into the current no-op shell.

## Extracted areas

The generated `extracted/` directory is intentionally ignored and is not kept in the repository. Git commit `14b0d1ff` remains the source of truth.

- `Client/Game/Developer/Game1.BotNavEditor.cs`
- `BotAI/BotNavigationModernGraphEditor.cs`
- `BotAI/BotNavigationHintAsset.cs`
- `BotAI/BotNavigationHintStore.cs`
- `BotAI/BotNavigationScoreRouteAsset.cs`
- `BotAI/BotNavigationScoreRouteStore.cs`
- `BotAI/BotNavigationDebugPlanner.cs`
- `BotAI/ClientBotNavPoints.cs`
- Supporting old `BotAI` navigation model/build/runtime files needed to make the
  editor logic understandable in isolation.

Run `.\extract-legacy-nav-authoring.ps1` from this directory, or from the repo
root with `-OutputDirectory Tools/NavAuthoringLegacy/extracted`, to refresh the
snapshot from git history.

The extracted files are intentionally not part of any project file. They depend
on the old `OpenGarrison.BotAI` namespace and need a port to the current
`OpenGarrison.Core.BotBrain` navigation model before being compiled again.
