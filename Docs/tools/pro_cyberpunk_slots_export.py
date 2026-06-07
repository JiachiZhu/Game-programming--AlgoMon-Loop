"""
Export Payload inventory slot states from the licensed PRO Cyberpunk HUD source.

Source sprite:
AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD/slot_item_bg.png

The source pack's local license allows personal/commercial use and modification.
This script creates color/state variants for the Payload grid while keeping the
runtime filenames stable.

Run: python Docs/tools/pro_cyberpunk_slots_export.py
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parent
SOURCE = PROJECT / "AlgoMon" / "Assets" / "_AlgoMon" / "Sprites" / "UI" / "MainTerminal" / "CyberpunkHUD" / "slot_item_bg.png"
OUT_DIR = PROJECT / "AlgoMon" / "Assets" / "_AlgoMon" / "Sprites" / "UI" / "MainTerminal" / "InventorySlots"

SIZE = 92
INNER_PAD = 18

STATE_COLORS = {
    "normal": (0, 220, 235),
    "hover": (70, 250, 255),
    "selected": (255, 54, 205),
    "locked": (116, 116, 142),
    "rare": (0, 182, 255),
    "epic": (192, 68, 255),
    "legendary": (255, 140, 38),
}


def alpha_bounds(image):
    alpha = image.getchannel("A")
    return alpha.getbbox()


def prepare_source():
    image = Image.open(SOURCE).convert("RGBA")
    bbox = alpha_bounds(image)
    if bbox is None:
        raise RuntimeError(f"No visible pixels found in {SOURCE}")

    image = image.crop(bbox)
    image.thumbnail((SIZE - 4, SIZE - 4), Image.LANCZOS)

    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    x = (SIZE - image.width) // 2
    y = (SIZE - image.height) // 2
    canvas.alpha_composite(image, (x, y))
    return canvas


def recolor(base, accent):
    alpha = base.getchannel("A")
    gray = ImageEnhance.Contrast(base.convert("L")).enhance(1.35)
    colored = Image.new("RGBA", base.size, (*accent, 0))
    colored.putalpha(alpha)

    cyan_mask = ImageChops.multiply(gray, alpha)
    accent_layer = Image.new("RGBA", base.size, (*accent, 255))
    accent_layer.putalpha(cyan_mask.point(lambda p: int(p * 0.92)))

    dark = Image.new("RGBA", base.size, (2, 8, 13, 0))
    dark.putalpha(alpha.point(lambda p: int(p * 0.82)))
    return Image.alpha_composite(dark, accent_layer)


def add_glow(image, accent, radius=4, strength=0.75):
    alpha = image.getchannel("A")
    glow_alpha = alpha.filter(ImageFilter.GaussianBlur(radius)).point(lambda p: int(p * strength))
    glow = Image.new("RGBA", image.size, (*accent, 0))
    glow.putalpha(glow_alpha)
    return Image.alpha_composite(glow, image)


def draw_diamond(draw, cx, cy, size, color):
    draw.polygon(
        [(cx, cy - size), (cx + size, cy), (cx, cy + size), (cx - size, cy)],
        fill=color,
    )


def decorate(image, state, accent):
    draw = ImageDraw.Draw(image)
    color = (*accent, 235)
    soft = (*accent, 125)

    if state in {"selected", "legendary"}:
        length = 16
        width = 3
        for x, y, sx, sy in ((12, 12, 1, 1), (80, 12, -1, 1), (12, 80, 1, -1), (80, 80, -1, -1)):
            draw.line((x, y, x + sx * length, y), fill=color, width=width)
            draw.line((x, y, x, y + sy * length), fill=color, width=width)

    if state in {"rare", "epic", "legendary"}:
        draw_diamond(draw, 72, 20, 5, color)
    if state in {"epic", "legendary"}:
        draw_diamond(draw, 20, 72, 5, color)

    if state == "locked":
        draw.rectangle((INNER_PAD, INNER_PAD, SIZE - INNER_PAD, SIZE - INNER_PAD), fill=(5, 7, 12, 105))
        draw.line((28, 64, 64, 28), fill=soft, width=4)
        draw.line((30, 66, 66, 30), fill=(0, 0, 0, 155), width=1)

    return image


def export_state(base, state):
    accent = STATE_COLORS[state]
    image = recolor(base, accent)
    glow_strength = 0.35 if state == "normal" else 0.62
    if state in {"selected", "legendary"}:
        glow_strength = 0.82
    image = add_glow(image, accent, radius=4, strength=glow_strength)
    image = decorate(image, state, accent)
    return image


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    base = prepare_source()
    for state in STATE_COLORS:
        out = OUT_DIR / f"cyber_slot_{state}.png"
        export_state(base, state).save(out)
        print(f"wrote {out}")


if __name__ == "__main__":
    main()
