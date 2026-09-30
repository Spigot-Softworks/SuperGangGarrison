"""Check that split player layers reproduce every authored composite (requires Pillow)."""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Core/Content/Gameplay/stock.gg2/assets/characters/player-skins"


def main():
    catalog = json.loads((ROOT / "Core/Content/PlayerSkins.json").read_text())
    checked = 0
    for skin in catalog["skins"].values():
        for team in skin["teams"]:
            for prefix in ("", "cloaked"):
                key = "bodySprite" if not prefix else "cloakedBodySprite"
                if key not in skin:
                    continue
                legs = "legsBodySprite" if not prefix else "cloakedLegsBodySprite"
                torso = "torsoBodySprite" if not prefix else "cloakedTorsoBodySprite"
                for pose in range(len(skin["poses"])):
                    def load(field):
                        with Image.open(ASSETS / skin[field].replace("{team}", team) / f"{pose}.png") as image:
                            return image.convert("RGBA")
                    body, lower, upper = load(key), load(legs), load(torso)
                    assert lower.getbbox(), (key, team, pose, "missing legs")
                    assert upper.getbbox(), (key, team, pose, "missing torso")
                    combined = Image.alpha_composite(lower, upper)
                    assert combined.tobytes() == body.tobytes(), (skin[key], team, pose, "layer alignment")
                    checked += 1
    print(f"Verified {checked} poses: separate torso/legs match the composite, including cloak and rocket-jump frames.")


if __name__ == "__main__":
    main()
