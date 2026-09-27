"""Import the supplied class gibs and their idle-pose placements.

Usage: python SourceAssets/Sprites/Gibs/import_gibs.py
The checked-in Aseprite sheet supplies positions; PNGs in GIBS.zip supply pixels.
"""

import argparse
import io
import json
import re
import struct
import zipfile
import zlib
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
SHEET = Path(__file__).with_name("Gibs Sheet.aseprite")
PACK = ROOT / "Core/Content/Gameplay/stock.gg2"
CATALOG = ROOT / "Core/Entities/Effects/Remains/AuthoredPlayerGibPlacementCatalog.cs"
CLASSES = (
    ("0. Employer", "civilian", "Civilian", 0, 3),
    ("1. Runner", "scout", "Scout", 0, 0),
    ("2. Rocketman", "soldier", "Soldier", 0, 1),
    ("3. Firebug", "pyro", "Pyro", 0, 2),
    ("4. Detonator", "demoman", "Demoman", 1, 0),
    ("5. Overweight", "heavy", "Heavy", 1, 1),
    ("6. Constructor", "engineer", "Engineer", 1, 2),
    ("7. Healer", "medic", "Medic", 2, 0),
    ("8. Marksman", "sniper", "Sniper", 2, 1),
    ("9. Infiltrator", "spy", "Spy", 2, 2),
)
PART_LAYERS = {
    "leg_bottom_r": (0,), "leg_bottom_l": (1,),
    "leg_top_r": (2,), "leg_top_l": (3,),
    "leg_bottom": (0, 1), "leg_top": (2, 3),
    "chest_b": (4,), "chest_a": (5,), "chest": (5,),
    "head": (6,), "hat": (7,), "gun": (8,),
}
EXPECTED_PART_COUNTS = {
    "civilian": 8, "scout": 8, "soldier": 9, "pyro": 8,
    "demoman": 9, "heavy": 8, "engineer": 9, "medic": 8,
    "sniper": 9, "spy": 6,
}


def read_sheet(path: Path) -> list[Image.Image]:
    data = path.read_bytes()
    if struct.unpack_from("<H", data, 4)[0] != 0xA5E0:
        raise ValueError("Not an Aseprite file")
    width, height, depth = struct.unpack_from("<HHH", data, 8)
    frame_count = struct.unpack_from("<H", data, 6)[0]
    if (width, height, depth, frame_count) != (96, 128, 32, 1):
        raise ValueError("Unexpected gib-sheet layout")
    offset = 128
    layers: list[Image.Image] = []
    for _ in range(frame_count):
        frame_size, frame_magic, old_count = struct.unpack_from("<IHH", data, offset)
        if frame_magic != 0xF1FA:
            raise ValueError("Invalid Aseprite frame")
        count = struct.unpack_from("<I", data, offset + 12)[0] or old_count
        chunk_offset = offset + 16
        for _ in range(count):
            chunk_size, chunk_type = struct.unpack_from("<IH", data, chunk_offset)
            payload = data[chunk_offset + 6:chunk_offset + chunk_size]
            if chunk_type == 0x2004:
                layers.append(Image.new("RGBA", (width, height)))
            elif chunk_type == 0x2005:
                layer_index, x, y, _, cel_type = struct.unpack_from("<HhhBH", payload)
                if cel_type != 2:
                    raise ValueError("Gib sheet has an unsupported cel")
                cel_width, cel_height = struct.unpack_from("<HH", payload, 16)
                pixels = zlib.decompress(payload[20:])
                cel = Image.frombytes("RGBA", (cel_width, cel_height), pixels)
                layers[layer_index].alpha_composite(cel, (x, y))
            chunk_offset += chunk_size
        offset += frame_size
    if len(layers) != 9:
        raise ValueError("Expected nine body-part layers")
    return layers


def find_part_position(
    layers: list[Image.Image], part: str, source: Image.Image, cell_x: int, cell_y: int
) -> tuple[int, int]:
    for layer_index in PART_LAYERS[part]:
        layer = layers[layer_index]
        for y in range(cell_y, cell_y + 33 - source.height):
            for x in range(cell_x, cell_x + 33 - source.width):
                if layer.crop((x, y, x + source.width, y + source.height)).tobytes() == source.tobytes():
                    return x, y

    # The sheet predates two tiny pixel edits in the zip: Runner hat and Healer gun.
    # Both retain the exact same bounds. Do not alter the supplied PNGs.
    for layer_index in PART_LAYERS[part]:
        bounds = layers[layer_index].crop((cell_x, cell_y, cell_x + 32, cell_y + 32)).getchannel("A").getbbox()
        if bounds is None or (bounds[2] - bounds[0], bounds[3] - bounds[1]) != source.size:
            continue
        x, y = cell_x + bounds[0], cell_y + bounds[1]
        candidate = layers[layer_index].crop((x, y, x + source.width, y + source.height))
        differences = sum(a != b for a, b in zip(source.tobytes(), candidate.tobytes()))
        if differences <= 40:
            print(f"Sheet pixel variation for {part} at {x},{y}: {differences} channel bytes")
            return x, y
    raise ValueError(f"Could not locate {part} in sheet cell {cell_x},{cell_y}")


def import_assets(zip_path: Path, layers: list[Image.Image]) -> list[tuple[str, str, int, int]]:
    placements = []
    with zipfile.ZipFile(zip_path) as archive:
        for source_dir, class_id, class_name, col, row in CLASSES:
            entries = sorted(
                name for name in archive.namelist()
                if name.startswith(source_dir + "/") and name.lower().endswith(".png")
            )
            if not entries:
                raise ValueError(f"No gib PNGs for {source_dir}")
            grouped: dict[str, dict[str, tuple[str, Image.Image]]] = {}
            for name in entries:
                stem = Path(name).stem
                match = re.fullmatch(r"(.+?)([12])?", stem)
                assert match is not None
                part, variant = match.groups()
                variant = variant or "shared"
                if part not in PART_LAYERS:
                    raise ValueError(f"Unexpected part {name}")
                raw = archive.read(name)
                image = Image.open(io.BytesIO(raw)).convert("RGBA")
                grouped.setdefault(part, {})[variant] = (name, image)

                suffix = {"1": "Red", "2": "Blue", "shared": ""}[variant]
                part_name = "".join(piece.capitalize() for piece in part.split("_"))
                sprite_id = f"PlayerGib{class_name}{suffix}{part_name}S"
                asset_path = PACK / f"assets/characters/gibs/{sprite_id}/0.png"
                sprite_path = PACK / f"sprites/characters/gibs/{sprite_id}.json"
                asset_path.parent.mkdir(parents=True, exist_ok=True)
                sprite_path.parent.mkdir(parents=True, exist_ok=True)
                asset_path.write_bytes(raw)
                sprite_path.write_text(json.dumps({
                    "id": sprite_id,
                    "framePaths": [f"assets/characters/gibs/{sprite_id}/0.png"],
                    "originX": image.width // 2,
                    "originY": image.height // 2,
                }, indent=2) + "\n", encoding="utf-8")

            if len(grouped) != EXPECTED_PART_COUNTS[class_id]:
                raise ValueError(f"Wrong number of {class_id} pieces: {len(grouped)}")
            for part, variants in sorted(grouped.items()):
                if "shared" in variants:
                    if len(variants) != 1:
                        raise ValueError(f"Mixed shared and team versions: {class_id}/{part}")
                    source = variants["shared"][1]
                else:
                    if set(variants) != {"1", "2"} or variants["1"][1].size != variants["2"][1].size:
                        raise ValueError(f"Incomplete or differently sized team versions: {class_id}/{part}")
                    source = variants["1"][1]
                x, y = find_part_position(layers, part, source, col * 32, row * 32)
                part_name = "".join(piece.capitalize() for piece in part.split("_"))
                # The idle body sprites use a 64x64 frame with origin (32,40).
                # Gib PNGs are source-resolution and rendered at 2x.
                offset_x = (x - col * 32 + source.width // 2 - 16) * 2
                offset_y = (y - row * 32 + source.height // 2 - 20) * 2
                placements.append((class_id, part_name, offset_x, offset_y))
    return placements


def write_placement_catalog(placements: list[tuple[str, str, int, int]]) -> None:
    lines = [
        "namespace OpenGarrison.Core;",
        "",
        "// Generated from SourceAssets/Sprites/Gibs/Gibs Sheet.aseprite by import_gibs.py.",
        "// Coordinates are world pixels at the normal 2x player/gib art scale.",
        "internal static class AuthoredPlayerGibPlacementCatalog",
        "{",
        "    private static readonly Dictionary<(string ClassId, string PartName), (float X, float Y)> Offsets = new()",
        "    {",
    ]
    for class_id, part, x, y in placements:
        lines.append(f'        [("{class_id}", "{part}")] = ({x}f, {y}f),')
    lines += [
        "    };",
        "",
        "    public static (float X, float Y) Get(string classId, string partName)",
        "        => Offsets[(classId, partName)];",
        "}",
    ]
    CATALOG.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("zip_path", nargs="?", type=Path, default=SHEET.with_name("GIBS.zip"))
    arguments = parser.parse_args()
    placements = import_assets(arguments.zip_path, read_sheet(SHEET))
    write_placement_catalog(placements)
    print(f"Imported {len(placements)} placed parts across {len(CLASSES)} classes")


if __name__ == "__main__":
    main()
