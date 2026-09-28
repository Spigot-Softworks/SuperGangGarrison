"""Bake attached Elkondo Scout taunt frames into stock pack sprites."""
from __future__ import annotations

import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = Path(
    r"C:\Users\user\.cursor\projects\f-OpenGarrisonCode-OpenGarrison-Fork\assets"
)
PACK = ROOT / "Core/Content/Gameplay/stock.gg2"
SRC = ROOT / "SourceAssets/Sprites/PlayerSkins/Elkondo/Scout"

# Natural order: numbered frames, then lettered inserts for that number.
ORDER = ["1", "2", "3", "3a", "4", "5", "6", "7", "7a", "7b", "8", "8a", "9", "10"]

# Elkondo Scout red->blu shirt palette (from existing torso/legs pairs).
PALETTE = {
    (0x25, 0x0F, 0x0F): (0x1A, 0x16, 0x21),
    (0x35, 0x1C, 0x1C): (0x29, 0x23, 0x37),
    (0x40, 0x18, 0x18): (0x29, 0x23, 0x37),
    (0x54, 0x2C, 0x2C): (0x2C, 0x28, 0x35),
    (0x99, 0x15, 0x15): (0x22, 0x28, 0x7A),
    (0xC6, 0x1B, 0x1B): (0x28, 0x36, 0xCC),
}


def find_source(label: str) -> Path:
    matches = list(ASSETS.glob(f"*sprite-1-{label}-*.png"))
    if not matches:
        raise FileNotFoundError(f"Missing attached frame for label {label}")
    return matches[0]


def recolor(img: Image.Image) -> Image.Image:
    out = []
    for r, g, b, a in img.getdata():
        if a == 0:
            out.append((r, g, b, a))
            continue
        mapped = PALETTE.get((r, g, b))
        out.append((*mapped, a) if mapped else (r, g, b, a))
    result = Image.new("RGBA", img.size)
    result.putdata(out)
    return result


def scale2(img: Image.Image) -> Image.Image:
    return img.resize((img.width * 2, img.height * 2), Image.Resampling.NEAREST)


def write_sprite(name: str, frames: list[Image.Image], origin: tuple[int, int] = (32, 40)) -> None:
    rel = Path("assets/characters/player-skins") / name
    out_dir = PACK / rel
    out_dir.mkdir(parents=True, exist_ok=True)
    for old in out_dir.glob("*.png"):
        if old.stem.isdecimal():
            old.unlink()
    frame_paths = []
    for i, frame in enumerate(frames):
        path = rel / f"{i}.png"
        frame.save(PACK / path)
        frame_paths.append(path.as_posix())
    meta = {
        "id": name,
        "framePaths": frame_paths,
        "originX": origin[0],
        "originY": origin[1],
    }
    def_path = PACK / "sprites/characters/player-skins" / f"{name}.json"
    def_path.parent.mkdir(parents=True, exist_ok=True)
    def_path.write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {name}: {len(frames)} frames")


def main() -> None:
    red_src_dir = SRC / "Red" / "taunt"
    blu_src_dir = SRC / "Blue" / "taunt"
    red_src_dir.mkdir(parents=True, exist_ok=True)
    blu_src_dir.mkdir(parents=True, exist_ok=True)

    red_scaled: list[Image.Image] = []
    blu_scaled: list[Image.Image] = []
    for label in ORDER:
        src = find_source(label)
        red32 = Image.open(src).convert("RGBA")
        red32.save(red_src_dir / f"{label}.png")
        blu32 = recolor(red32)
        blu32.save(blu_src_dir / f"{label}.png")
        red_scaled.append(scale2(red32))
        blu_scaled.append(scale2(blu32))
        print(f"{label}: {src.name} -> {red32.size} x2")

    write_sprite("ElkondoScoutRedTauntS", red_scaled)
    write_sprite("ElkondoScoutBlueTauntS", blu_scaled)
    print("Done")


if __name__ == "__main__":
    main()
