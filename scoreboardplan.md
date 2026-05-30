# MVP Win Screen Plan

## Current Behavior

- The post-game win screen uses the existing MVP scoreboard as the foreground layer.
- MVP art is drawn behind the board when the `MVP Art` setting is enabled.
- `SHIFT` toggles the art visibility.
- The winner/middle portrait is drawn before the side portraits so it layers underneath them.
- Third place uses the side portrait pool and is flipped horizontally because side assets face right.
- Each player/rank picks one random frame from the correct portrait pool and keeps that frame for the presentation.

## Asset Convention

- Runtime MVP portraits live in `Core/Content/Gameplay/stock.gg2/assets/mvp/<team>/<class>/`.
- Sprite definitions live in `Core/Content/Gameplay/stock.gg2/sprites/Mvp<Team><Class>S.json` and `Mvp<Team><Class>WinnerS.json`.
- Side portraits use `nonwinner_#.png` and are used for second and third place.
- First-place portraits use `winner_#.png`.
- The current source handoff folder is `WinBanners_both_colors`.
- Red source assets are in `WinBanners_both_colors/<Class>/`.
- Blue source assets are in `WinBanners_both_colors/Blue/<Class>/`.
- `middle` or `Mid` contains first-place-only frames.
- Root-level `image #.png` files become side portrait frames.
- `template.png` files are not imported into runtime sprite pools.
- `Civvie` source folders are imported as the game `Quote` class because `PlayerClass` has `Quote` and no `Civvie`.

## Rendering Notes

- Names above the art use the same in-game bitmap font as the MVP scoreboard.
- Name labels are positioned from the visible top of the portrait art; the winner label is also constrained below the `Press SHIFT to hide` hint.
- Side portraits align their lower visible silhouette edge to the outer MVP board edge, so transparent padding and flipped third-place art do not skew placement.
- The top hint reads `Press SHIFT to hide`, uses the same font, and has a drop shadow.
- MVP art target scale is 25% larger than the original implementation.
