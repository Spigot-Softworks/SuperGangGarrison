# Post-Game MVP Win Screen Plan

## Goal

Replace the plain round-end win banner/scoreboard presentation with a post-game MVP showcase:

- Reuse the existing scoreboard/MVP-board visual language.
- Layer three class/team MVP art sprites behind the board.
- Show the winning team's top three MVPs.
- Animate art in sequence:
  - 3rd place enters first from the right and anchors on the left, facing left.
  - 2nd place enters second from the left and anchors on the right, facing right.
  - 1st place enters third from below and anchors high middle.
- Pressing Shift toggles the art layer hidden/visible while leaving the board visible.

## Existing Code Anchors

- Live C# scoreboard: `Client/Game/Gameplay/Hud/Overlays/Game1.ScoreboardHud.cs`
- Current simple win banner: `Client/Game/Gameplay/Hud/Overlays/Game1.MatchOverlayHud.cs`
- HUD draw order: `Client/Game/Gameplay/Hud/Core/Game1.GameplayHudRendering.cs`
- Scoreboard input state: `Client/Game/Gameplay/Hud/Core/Game1.Hud.cs`
- Previous-frame input finalization: `Client/Game/Gameplay/Runtime/Game1.GameplayUpdate.cs`
- Player score/class/team data: `Core/Entities/Players/Core/PlayerEntity.cs`
- Legacy MVP ranking/draw reference:
  - `Core/Content/Scripts/serverArenaEndRound.gml`
  - `Core/Content/Objects/Overlays/ArenaHUD.events/Draw.xml`

## Asset Layout

Do not leave MVP assets at repo root. Store them in the stock gameplay pack so native and browser builds both pick them up:

`Core/Content/Gameplay/stock.gg2/assets/mvp/{team}/{class}/`

Suggested names:

- Non-winner frames: `nonwinner_0.png`, `nonwinner_1.png`, ...
- Winner frames: `winner_0.png`, `winner_1.png`, ...

Sprite definitions belong in:

`Core/Content/Gameplay/stock.gg2/sprites/`

Naming convention:

- `MvpRedMedicS.json` for non-first art.
- `MvpRedMedicWinnerS.json` for first-place art.
- Future blue support mirrors this as `MvpBlueMedicS.json` and `MvpBlueMedicWinnerS.json`.

## Ranking

The live scoreboard sorts by `PlayerEntity.Points`. Legacy Arena MVP ranking used kills + heal points + stab kills, where one heal point was awarded for each 200 HP healed.

Current C# already folds kills, assists, objectives, stab bonuses, and other bonuses into `Points`; it also exposes `HealPoints`.

Use:

`mvpScore = floor(player.Points) + floor(player.HealPoints / 200f)`

Tie-breakers:

1. Higher MVP score.
2. Higher raw points.
3. Higher kills.
4. Lower deaths.
5. Display name ascending.

## Rendering

Add a dedicated partial:

`Client/Game/Gameplay/Hud/Overlays/Game1.PostGameMvpWinScreen.cs`

Responsibilities:

- Detect ended match with a non-null winner team.
- Build winning-team MVP entries from local + remote/offline renderable players.
- Draw art behind the MVP board.
- Draw `MVPBannerS` for the winner team.
- Draw top-three name/kills/healing/score rows.
- Keep drawing the normal score panel above the board if needed.

Use `MVPBannerS` frames:

- Red winner board: frame `0`
- Blue winner board: frame `2`

Use `SpriteEffects.FlipHorizontally` only for the 3rd-place non-winner art.

## Animation

Use client ticks, not wall time, so animation is deterministic with the existing UI loop.

Suggested sequence:

- 3rd place starts at tick 12, animates for 28 ticks from right to left.
- 2nd place starts at tick 38, animates for 28 ticks from left to right.
- 1st place starts at tick 64, animates for 32 ticks from below to high middle.

Each sprite fades in while sliding. Use a simple ease-out curve.

Art frame selection:

- If a sprite has multiple frames, choose one random frame when the MVP screen appears and keep that pose stable.
- Non-winner frames are the pool for 2nd and 3rd place; winner frames are the pool for 1st place.
- If a sprite has one frame, draw frame 0.

## Shift Toggle

Add a post-game-only Shift edge toggle:

- LeftShift or RightShift pressed this frame toggles `_postGameMvpArtHidden`.
- Toggle affects only art.
- MVP board/text remain visible.
- Avoid fighting the scoreboard binding, because old migrated configs can still bind Show Scores to Shift.

## Tests / Verification

Minimum verification:

- `dotnet test Tests/OpenGarrison.PluginHost.Tests/OpenGarrison.PluginHost.Tests.csproj`
- Confirm stock gameplay pack loads new sprite definitions.
- Manual visual test native or browser when practical:
  - Red winner with available art.
  - Fallback when a class/team art sprite is missing.
  - Shift hides/unhides art.

## Implementation Notes

- For now only red art exists for Engineer, Heavy, Medic, and Sniper.
- Missing classes/blue team should fail gracefully by skipping art for that MVP, not the board.
- Keep new code isolated from generic scoreboard hover/player-card behavior.
- If context is lost, re-read this file first, then inspect the code anchors above.

## Implementation Status

Completed in this pass:

- Moved root MVP PNGs into `Core/Content/Gameplay/stock.gg2/assets/mvp/red/{class}/`.
- Added stock sprite definitions for red Engineer, Heavy, Medic, and Sniper MVP art.
- Added `Client/Game/Gameplay/Hud/Overlays/Game1.PostGameMvpWinScreen.cs`.
- Wired post-game MVP state update from `UpdateGameplayPresentation`.
- Wired post-game MVP drawing into the gameplay HUD layer after the old win-banner slot.
- Suppressed the old `WinBannerS` whenever the MVP win screen is active.
- Added stock gameplay pack test assertions for MVP sprite registration.
- Verified with `dotnet test Tests/OpenGarrison.PluginHost.Tests/OpenGarrison.PluginHost.Tests.csproj`.
- Changed MVP portraits from cycling animation frames to one cached random pose per MVP screen/player/rank.
- Increased portrait scale and anchored side portraits to the MVP board edges.
- Nudged MVP row text upward.
- Lowered portrait anchors so the MVP board occludes the lower body instead of the art sitting above the score tabs/table.
- Draw winner portrait underneath the side portraits while preserving the existing entrance timing.
- Added MVP art drop shadows and shadowed player-name labels above each portrait.
- Switched medic healing/healer and Superburst labels to in-game bitmap font sizing.
- Moved the Heavy ghost dash meter above the sandvich HUD icon.
- Raised the winner portrait and slowed the slide/fade entrance durations.
- Reduced MVP board row text scale while keeping its row/column positions.

Remaining follow-up:

- Manual in-game visual pass for exact board/text placement and art anchors.
- Add blue-team art and sprite JSONs when assets exist.
- Add missing classes as their art arrives.
