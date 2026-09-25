"""Bake layered player art into the stock sprite/atlas pipeline (requires Pillow)."""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT / "Core/Content/Gameplay/stock.gg2"


def load_image(source: Path, spec: dict, team: dict) -> Image.Image:
    path = source / team["directory"] / spec["file"].replace("{color}", team["color"])
    image = Image.open(path).convert("RGBA")
    palette = {tuple(bytes.fromhex(k)): tuple(bytes.fromhex(v)) for k, v in team.get("palette", {}).items()}
    if palette:
        pixels = image.get_flattened_data() if hasattr(image, "get_flattened_data") else image.getdata()
        image.putdata([(*palette.get(pixel[:3], pixel[:3]), pixel[3]) for pixel in pixels])
    return image


def compose(source: Path, layers: list[dict], team: dict, size: list[int]) -> Image.Image:
    canvas = Image.new("RGBA", tuple(size))
    for layer in layers:
        layer = layer | team.get("layerOverrides", {}).get(layer["file"], {})
        image = load_image(source, layer, team)
        canvas.alpha_composite(image, tuple(layer.get("position", [0, 0])))
    return canvas


def write_sprite(name: str, images: list[Image.Image], origin: list[int], check: bool) -> None:
    if not re.fullmatch(r"[A-Za-z0-9_.-]+", name):
        raise ValueError(f"Invalid generated sprite name: {name}")
    relative = Path("assets/characters/player-skins") / name
    metadata = {"id": name, "framePaths": [(relative / f"{i}.png").as_posix() for i in range(len(images))],
                "originX": origin[0], "originY": origin[1]}
    definition = PACK / "sprites/characters/player-skins" / f"{name}.json"
    if check:
        if not definition.exists() or json.loads(definition.read_text(encoding="utf-8")) != metadata:
            raise ValueError(f"Stale sprite definition: {definition}")
    else:
        definition.parent.mkdir(parents=True, exist_ok=True)
        definition.write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    for path, image in zip(metadata["framePaths"], images):
        target = PACK / path
        if check:
            if not target.exists():
                raise ValueError(f"Missing frame: {target}")
            with Image.open(target) as actual:
                if actual.size != image.size or actual.convert("RGBA").tobytes() != image.tobytes():
                    raise ValueError(f"Stale frame: {target}")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target)
    for obsolete in (PACK / relative).glob("*.png"):
        if obsolete.stem.isdecimal() and int(obsolete.stem) >= len(images):
            if check:
                raise ValueError(f"Obsolete generated frame: {obsolete}")
            obsolete.unlink()


def build(catalog: Path, check: bool) -> None:
    data = json.loads(catalog.read_text(encoding="utf-8"))
    if data["version"] != 2:
        raise ValueError("Unsupported player skin version")
    for skin in data["skins"].values():
        for name, clip in skin["clips"].items():
            if not clip["frames"] or any(frame < 0 or frame >= len(skin["poses"]) for frame in clip["frames"]):
                raise ValueError(f"Invalid pose sequence in {skin['bodySprite']}: {name}")
        source = ROOT / "SourceAssets/Sprites/PlayerSkins" / skin["source"]
        for team_name, team in skin["teams"].items():
            name = lambda pattern: pattern.replace("{team}", team_name)
            scale = skin.get("pixelScale", 1)
            def write(pattern, images, origin):
                scaled = [image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST) for image in images]
                write_sprite(name(pattern), scaled, [v * scale for v in origin], check)
            body = [compose(source, pose["layers"], team, skin["canvas"]) for pose in skin["poses"]]
            write(skin["bodySprite"], body, skin["origin"])
            if "cloakedBodySprite" in skin:
                cloaked = [compose(source, pose["cloakedLayers"], team, skin["canvas"]) for pose in skin["poses"]]
                write(skin["cloakedBodySprite"], cloaked, skin["origin"])
            legs_sprite = skin.get("legsBodySprite")
            if legs_sprite:
                def legs_layers(pose: dict) -> list[dict]:
                    return [
                        layer
                        for layer in pose["layers"]
                        if str(layer.get("file", "")).replace("\\", "/").startswith("legs/")
                    ]

                if any(legs_layers(pose) for pose in skin["poses"]):
                    legs = [
                        compose(source, legs_layers(pose), team, skin["canvas"])
                        for pose in skin["poses"]
                    ]
                    write(legs_sprite, legs, skin["origin"])
            weapon = skin.get("weapon")
            if weapon is None:
                continue
            normal = []
            for pose in skin["poses"]:
                # The exported full-canvas weapon frames already contain the pose offset.
                # Normalize around a shared pivot; the runtime applies that offset once.
                image = compose(source, [pose["weapon"]], team, skin["canvas"])
                normalized = Image.new("RGBA", tuple(skin["canvas"]))
                normalized.alpha_composite(image, tuple(-v for v in pose["weaponOffset"]))
                normal.append(normalized)
            write(weapon["sprite"], normal, weapon["pivot"])
            for action in ("fire", "reload"):
                frames = weapon.get(action + "Frames", [])
                if frames:
                    images = [compose(source, [frame], team, skin["canvas"]) for frame in frames]
                    write(weapon[action + "Sprite"], images, weapon["pivot"])
    print("Player skin assets verified." if check else "Player skin assets imported.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=ROOT / "Core/Content/PlayerSkins.json")
    parser.add_argument("--check", action="store_true", help="Verify generated art without changing files")
    args = parser.parse_args()
    build(args.catalog, args.check)
