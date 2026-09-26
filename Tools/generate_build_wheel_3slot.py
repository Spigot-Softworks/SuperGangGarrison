from pathlib import Path
from PIL import Image
import math

SIZE = 201
CX = CY = 100
BRIGHT = (214, 214, 162, 255)
DARK = (122, 122, 93, 255)
UNSELECTED = (0, 0, 0, 192)
SELECTED = (78, 78, 78, 192)

SECTOR_R_IN = 37
SECTOR_R_OUT = 98
DIVIDERS = (60.0, 180.0, 300.0)

STOCK = Path(r"Core/Content/Gameplay/stock.gg2/assets/hud/builder/BuildWheelS.images")
LEGACY = Path(r"Core/Content/Sprites/HUD/BuildWheelS.images")
BAK = Path(r"Tools/_buildwheel_bak")


def ang_cw_from_up(dx: float, dy: float) -> float:
    return (math.degrees(math.atan2(dx, -dy)) + 360.0) % 360.0


def in_sector(ang: float, start: float, end: float) -> bool:
    ang %= 360.0
    start %= 360.0
    end %= 360.0
    if start < end:
        return start <= ang < end
    return ang >= start or ang < end


def sector_range_for_frame_slot(frame_slot: int) -> tuple[float, float]:
    """Chrome frame slots: 1=top, 2=bottom-right, 3=bottom-left."""
    if frame_slot == 1:
        return 300.0, 60.0
    if frame_slot == 2:
        return 60.0, 180.0
    if frame_slot == 3:
        return 180.0, 300.0
    raise ValueError(frame_slot)


def blank() -> Image.Image:
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def plot(px, x: int, y: int, color: tuple[int, int, int, int]) -> None:
    if 0 <= x < SIZE and 0 <= y < SIZE:
        px[x, y] = color


def make_outline_from_original() -> Image.Image:
    orig = Image.open(BAK / "image 0.png").convert("RGBA")
    out = blank()
    pxo = orig.load()
    px = out.load()
    for y in range(SIZE):
        for x in range(SIZE):
            p = pxo[x, y]
            if p[3] == 0:
                continue
            dx = x - CX
            dy = y - CY
            r = math.hypot(dx, dy)
            ang = ang_cw_from_up(dx, dy)
            near_old_div = min(abs((ang - d + 180) % 360 - 180) for d in (45, 135, 225, 315)) < 3
            if near_old_div and 40 < r < 96:
                continue
            px[x, y] = p

    for angle in DIVIDERS:
        rad = math.radians(angle)
        sx = math.sin(rad)
        cy = -math.cos(rad)
        pts: list[tuple[int, int]] = []
        for r in range(36, 101):
            x = int(round(CX + sx * r))
            y = int(round(CY + cy * r))
            pts.append((x, y))
            plot(px, x, y, BRIGHT)
        for x, y in pts:
            if 0 <= x + 1 < SIZE and 0 <= y + 1 < SIZE and px[x + 1, y + 1][3] == 0:
                px[x + 1, y + 1] = DARK
    return out


def make_sector(frame_slot: int, selected: bool) -> Image.Image:
    start, end = sector_range_for_frame_slot(frame_slot)
    color = SELECTED if selected else UNSELECTED
    rin2 = SECTOR_R_IN * SECTOR_R_IN
    rout2 = SECTOR_R_OUT * SECTOR_R_OUT
    img = blank()
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            dx = x - CX
            dy = y - CY
            d2 = dx * dx + dy * dy
            if d2 < rin2 or d2 > rout2:
                continue
            if in_sector(ang_cw_from_up(dx, dy), start, end):
                px[x, y] = color
    return img


def save_both(name: str, img: Image.Image) -> None:
    img.save(STOCK / name)
    img.save(LEGACY / name)
    opaque = sum(1 for p in img.getdata() if p[3] > 0)
    print(f"wrote {name} opaque={opaque}")


def main() -> None:
    save_both("image 0.png", make_outline_from_original())

    # Preserve original center button art exactly.
    for src_name, dst_name in (("image 1.png", "image 1.png"), ("image 6.png", "image 5.png")):
        img = Image.open(BAK / src_name).convert("RGBA")
        save_both(dst_name, img)

    # Unselected sectors: 2=top, 3=bottom-right, 4=bottom-left
    save_both("image 2.png", make_sector(1, False))
    save_both("image 3.png", make_sector(2, False))
    save_both("image 4.png", make_sector(3, False))
    # Selected sectors
    save_both("image 6.png", make_sector(1, True))
    save_both("image 7.png", make_sector(2, True))
    save_both("image 8.png", make_sector(3, True))

    for i in (9, 10, 11, 12):
        save_both(f"image {i}.png", blank())

    # Restore cancel; wipe baked building icons.
    save_both("image 13.png", Image.open(BAK / "image 13.png").convert("RGBA"))
    for i in range(14, 42):
        save_both(f"image {i}.png", blank())


if __name__ == "__main__":
    main()
