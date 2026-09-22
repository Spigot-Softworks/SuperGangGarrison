# Legacy nav authoring extraction

This folder contains a script for extracting a historical navigation editor.
The extracted code is a reference snapshot, not part of the current build.

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

From the repository root, generate the ignored snapshot with:

```powershell
.\Tools\NavAuthoringLegacy\extract-legacy-nav-authoring.ps1 -OutputDirectory Tools/NavAuthoringLegacy/extracted
```

The checkout must include commit `14b0d1ff`; shallow clones may need to fetch
older history first. Paths listed above refer to that commit.

The extracted files are intentionally not part of any project file. They depend
on the old `OpenGarrison.BotAI` namespace and need a port to the current
`OpenGarrison.Core.BotBrain` navigation model before being compiled again.
