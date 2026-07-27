# Content layout

OpenGarrison keeps editable source assets separate from the files shipped to players. Loose sprite frames make development and atlas generation convenient, but release packages render sprites from generated atlases and must not contain those loose frames.

## Repository ownership

- `Core/Content/Gameplay/stock.gg2/` owns stock gameplay definitions and their editable gameplay sprite sources. Classes and items refer to stable sprite IDs. A stock gameplay sprite must not reach into `Core/Content/Sprites/` for its frames.
- `Core/Content/Sprites/` owns application, menu, updater, editor, map-import, and other non-gameplay or legacy GameMaker sprite sources.
- `SourceAssets/` owns editable source material that is not loaded directly at runtime, such as audio stems, soundfonts, sprite templates, unreferenced/alternate stock frames, concept art, and retained map demonstrations.
- `Maps/` is the intentional custom-map payload root. Its maps are copied into distributions and may be loaded by users and servers.
- `packaging/config/` owns configuration examples copied into release packages.
- `.local/` is ignored scratch space for local experiments and uncommitted source drops. New loose assets must not be placed at the repository root.

## Gameplay sprite source layout

The target stock-pack layout groups each sprite definition with the frames it describes while preserving stable sprite IDs:

```text
Core/Content/Gameplay/stock.gg2/
  classes/
  items/
    abilities/
    experimental/
    weapons/
  sprites/
    characters/
    weapons/
    projectiles/
    hud/
    world/
    shared/
  assets/
    characters/
    weapons/
    projectiles/
    hud/
    world/
    shared/
```

Sprite definitions may be discovered recursively. Frame paths are pack-relative, cannot escape the pack, and must use the same casing as the files on disk.

## Build and packaging contract

Development and editor workflows may read loose sprite sources. Atlas generation compiles those sources into texture pages and manifests containing frame rectangles, origins, collision masks, and other runtime metadata.

Release packages contain generated atlases and manifests, gameplay class/item data, maps, sounds, and other required runtime data. `Gameplay/stock.gg2/runtime.json` is generated during atlas compilation and retains the sprite IDs, dimensions, origins, and masks needed at runtime without retaining source frame paths. The pre-baked pixel-perfect weapon rotation strips under `Sprites/WeaponsRotated` are also generated strip atlases with per-frame manifests; they are runtime outputs, not editable loose source frames. Release packages do not contain `stock.gg2/assets`, `stock.gg2/sprites`, loose non-collision `.images` frame directories, GameMaker sprite XML used only as source metadata, design notes, or raw working assets. Packaging validation must fail if excluded source files appear in a release payload or if runtime sprite IDs differ from the stock atlas manifest.

## Adding content

1. Choose the owning subsystem before adding a file.
2. Put gameplay-owned sprites inside their gameplay pack; put application/editor sprites in the general content source tree.
3. Put non-runtime originals under `SourceAssets/`, with a descriptive feature directory.
4. Put distributable custom maps under `Maps/`; keep experiments under `.local/`.
5. Generate and verify atlases, then confirm referenced sprite IDs exist in the atlas manifest.

Run `scripts/verify-repository-root-layout.ps1` to reject new unowned root entries, `scripts/verify-gameplay-pack-layout.ps1` to validate source definitions/frames, and `scripts/verify-packaged-content.ps1` against a distribution to enforce the runtime boundary.
