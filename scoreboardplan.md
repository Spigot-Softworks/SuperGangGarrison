# MVP Win Screen Plan

## Current Behavior

- The post-game win screen uses the existing MVP scoreboard as the foreground layer.
- MVP art is drawn behind the board when the `MVP Art` setting is enabled.
- `SHIFT` toggles the art visibility.
- The winner/middle portrait is drawn before the side portraits so it layers underneath them.
- Third place uses the side portrait pool and is flipped horizontally because side assets face right.
- Each player/rank picks one random frame from the correct portrait pool and keeps that frame for the presentation.

## Asset Convention

- Red team MVP portraits live in `Core/Content/Gameplay/stock.gg2/assets/mvp/red/<class>/`.
- Side portraits use `nonwinner_#.png` and are used for second and third place.
- First-place portraits use `winner_#.png`.
- Sprite definitions live in `Core/Content/Gameplay/stock.gg2/sprites/MvpRed<Class>S.json` and `MvpRed<Class>WinnerS.json`.
- Source handoff assets may be staged in root `WinBanners/<Class>/`.
- `WinBanners/<Class>/middle/` or `WinBanners/<Class>/Mid/` contains first-place-only frames.
- Root-level `WinBanners/<Class>/image #.png` files become side portrait frames.
- `template.png` files are not imported into runtime sprite pools.

## Rendering Notes

- Names above the art use the same in-game bitmap font as the MVP scoreboard.
- The top hint reads `Press SHIFT to hide`, uses the same font, and has a drop shadow.
- MVP art target scale is 25% larger than the previous implementation.
