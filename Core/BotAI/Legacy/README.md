# Legacy bot-navigation compatibility boundary

This directory is a quarantine for the old `OpenGarrison.BotAI` navigation
asset/authoring model. It is retained for compatibility only and must not be
extended with new navigation behavior. New runtime navigation ownership lives
under `Core/BotBrain/Navigation/` and is consumed through the BotBrain provider
boundary.

The namespace intentionally remains `OpenGarrison.BotAI` so existing serialized
asset compatibility tests and the current shared movement helpers do not change
their public names or runtime behavior.

## Why this surface remains

- `Core/BotBrain/Navigation/Og2NavigationGraphBuilder.cs` still depends on
  `BotNavigationProfiles` and `BotNavigationMovementValidator` for the shared
  class-profile mapping and jump-tape validation used while building the OG2
  graph.
- `Tools/BotBrain/Og2AlphaNavigationDiagnostics.cs` uses the same profile and
  movement-validation helpers for diagnostics.
- `Tests/OpenGarrison.PluginHost.Tests/BotBrainCompressedAssetTests.cs` aliases
  `BotNavigationAsset` and `BotNavigationAssetStore` explicitly to verify the
  old BotAI asset format and compatibility-loading failure paths.
- The remaining old asset, hint, builder, store, validator, runtime-graph,
  point-builder, and geometry files form the transitive implementation of that
  compatibility store and its tests. They are not the OG2 runtime graph model.

The old authoring UI was shelved: `Client/Game/Developer/Game1.BotNavEditor.cs`
and its input, HUD, window-gutter, console, and practice-bot hooks were removed.
The old graph editor, debug planner, graph repairer, and score-route asset/store
were also removed after repository-wide reference checks found no remaining code
consumers.

Do not add new callers here. If a compatibility dependency can be removed,
delete it only after updating the explicit tests/tools that prove the old-format
boundary.
