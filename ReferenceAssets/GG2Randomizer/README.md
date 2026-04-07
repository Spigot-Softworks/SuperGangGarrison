# GG2 Randomizer Reference Assets

This directory is a preserved import source for legacy GG2 Randomizer weapon and UI art.

Source:
- Imported from `C:\Users\level\Downloads\randomizer`

Purpose:
- Keep the original exported per-frame PNGs and XML metadata in-repo.
- Provide a stable source for future gameplay-asset imports.
- Avoid mixing raw third-party/reference assets directly into live runtime gameplay packs before they are normalized.

Usage guidance:
- Treat this directory as reference/source material, not authoritative runtime content.
- When importing a weapon or HUD asset, copy only the required normalized files into a gameplay pack under `Core/Content/Gameplay/...`.
- Preserve origin/metadata from the matching XML when possible.

Notes:
- Folder structure is intentionally preserved from the imported dump so class/weapon groupings remain easy to navigate.
- The current loader does not consume assets from this directory directly.
