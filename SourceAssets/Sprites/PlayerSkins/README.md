# Player skins

Graphics settings offers **Sprites: Kelly / Elkondo**, saved across sessions.
Elkondo is the default and supplies idle and eight-frame run cycles for all
nine main classes. Kelly supplies Healer, Infiltrator, and Rocketman for the
`medic`, `spy`, and `soldier` classes. Elkondo supplies idle and eight-frame run
  cycles for all nine main classes, including a cloaked knife variant for Spy.
  Its jumps use frames 1, 2, 3, and 4 for pre-jump, rising, falling, and landing.
Classes and actions absent from a set retain their existing art.

The editable PNGs live here. [sprites.txt](sprites.txt) preserves Kelly's supplied
frame and attachment instructions. [Elkondo/README.md](Elkondo/README.md) describes
the archive's layer mapping and author notes.

The shared [catalog](../../../Core/Content/PlayerSkins.json), schema version 2,
controls the named sets, class assignments, clips, layers, and weapon attachments.
The client loads `Content/PlayerSkins.json` on desktop, with the embedded catalog
as a fallback. Browser builds embed the same file, so catalog changes require a
browser rebuild. Restart the desktop client after changing its catalog.

## Changing a skin

1. Edit an existing skin, or copy its definition under a new key in `skins`.
   Give new sprites unique names. Put editable PNGs in a new folder here and
   set `source` to that folder name.
2. Set `sets.<set name>.<gameplay class id>` to the skin key. Removing a class from
   a set restores its existing presentation. This also works for custom
   gameplay class IDs; unrelated classes are unaffected.
3. Run `python scripts/import-player-skins.py` from the repository root, using
   Python with Pillow installed. Commit the catalog, source art, and generated
   files under `Core/Content/Gameplay/stock.gg2/{assets,sprites}/characters/player-skins`.
4. Build/package normally. The existing desktop and browser atlas pipeline
   includes these sprites. No local `Resprite` folder is needed.

Run `python scripts/import-player-skins.py --check` to detect stale generated
frames or metadata without rewriting them. Changes to clip timing, frame order,
default selection, and `weapon.attachmentOffset` need no art import. Changes to layers, pixel scale,
origins, palettes, or weapon offsets do.

## Coordinates and poses

All authored coordinates are in the original PNG's pixels. `canvas` is the
composition size. `pixelScale: 2` doubles pixels with nearest-neighbor sampling
to match the game's existing art. The supplied skins use `origin: [16, 20]`;
their feet stay 24 world pixels below the player anchor, matching the existing
characters. Collision bounds and movement are unchanged.

Each entry in `poses` has:

- `layers`: PNGs in back-to-front order, with optional `[x, y]` positions.
  Full-canvas Healer and Infiltrator layers start at `[0, 0]`. Rocketman's
  cropped body and hat use the positions and bobbing in the supplied notes.
- `weapon`: a PNG and optional position on the same canvas, when the skin
  replaces a weapon.
- `cloakedLayers`: an alternate complete body composition, when the skin has
  `cloakedBodySprite`. Spy includes the knife in this composition; the client
  hides the separate weapon while drawing it and retains normal cloak opacity.
- `equipmentOffset`: optional vertical bob in source pixels for existing
  weapons and carried equipment. It defaults to zero. A skin-level
  `equipmentOffset` applies an additional constant adjustment to every pose;
  Elkondo uses `-1` to move the legacy arm/weapon sprites up two rendered pixels
  at its 2x pixel scale.
- `weaponOffset`: the pose's attachment displacement from the idle attachment.
  Full-canvas weapon exports already contain this displacement. The importer
  removes it, then the renderer applies it once around the shared pivot.

Body layers are combined during import so existing shadows, outlines, cloaking,
and recorded afterimages all use the same complete silhouette. Weapons remain
separate and rotate around `weapon.pivot`. The pivot is an absolute coordinate
on the canvas: add the supplied weapon top-left position to its rotation point.
For example, Healer uses `[9 + 8, 20 + 4] = [17, 24]`.
An optional `weapon.muzzle` gives the muzzle's canvas coordinate in the normalized
idle image. Healer uses it to attach the healing beam to the barrel when aiming
in either direction, regardless of transparent padding.
`weapon.attachmentOffset` shifts the entire weapon relative to the player in
source pixels (default `[0, 0]`). It applies to every pose, firing, and reload,
and mirrors with facing. Rocketman uses `[2, 0]` to sit two pixels farther forward.

## Animation clips

Each clip supplies pose indices in `frames`, a `framesPerSecond` rate, and a
`loop` flag. Required clips are `idle` and `run`. Optional clips are `jumpStart`,
`rise`, `fall`, `land`, `runBackward`, `blastStart`, `blastRise`, `blastFall`,
and `blastLand`. Missing airborne clips use the class's existing jump art;
missing start/land clips transition directly to the next movement state.

Clips with `loop: false` hold their final frame until the animation state changes.
Infiltrator's fall uses this to play its transition once, then hold the falling pose.

Running advances by distance using `pixelsPerRunFrame`, keeping the stride
consistent as movement speed changes. A backward run is selected when movement
opposes facing; when a skin has no dedicated backward strip, its normal run
strip is sampled backward from the current frame. Other clips use their frame
rate; jump-start and landing clips play for their configured duration after the
corresponding movement state is reached. Blast poses apply
after explosive knockback or a shot while airborne and persist through landing.
The supplied notes do not specify rates, so start/land timing is configurable.
Kelly currently uses 18/12 frames per second; Elkondo uses 18/15 for slightly
shorter pre-jump and landing holds.

Rocketman's `fireFrames` and `reloadFrames` bake into separate weapon strips.
Those strips follow the existing weapon firing/reload timers, including speed
modifiers. `weapon.firePlaybackRate` defaults to `1`; Rocketman uses `1.6` to
play the firing strip 60% faster. This changes only the animation duration,
not the weapon's firing rate or reload timing. Skins without those strips keep
their pose-specific weapon image.
`weapon.itemId` and `matchSprite` restrict replacement to that item's actual
presentation. The entire `weapon` object can be omitted for body-only skins
such as Elkondo. Alternate weapons retain their own sprites. Taunts, humiliation,
backstabs, banner deployment, and corpses retain their existing dedicated art.

## Teams

`teams` maps `Red` and `Blue` to a source `directory` and a `color` placeholder
for filenames such as `rocket_{color}/idle.png`. `{team}` in generated sprite
names becomes `Red` or `Blue`.

A team's optional `layerOverrides` maps layer filenames to replacement layer
specifications (`file` and/or `position`). Elkondo's Blue Detonator uses this to
reuse its single torso PNG with the same bob as the Red cycle.

Only RED Infiltrator art was supplied. Its BLU variant uses the RED PNGs with an
explicit suit-color `palette`, based on the existing Infiltrator team colors;
skin, weapon, and cigarette colors are preserved. To use supplied BLU art later,
add its folder, change the Blue `directory`/`color`, and remove the palette.

## Checking changes

`PlayerSkinTests` covers catalog loading, generated sprite assets, backward
running, and normal/boosted jump transitions. For an in-game check, serve a
published browser build, set `OG_BROWSER_URL` to its address, and run
`node Tests/BrowserSmoke/player-skins.mjs` with the BrowserSmoke dependencies
installed. It checks all three classes on both teams, body/weapon pose
synchronization, and Rocketman's firing/reload cycle, and saves screenshots
under `artifacts/browser-player-skins`.
