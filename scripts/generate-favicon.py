"""Generate site and app icons: cream tile, black F, red dot (Freewrite app look)."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

# Freewrite-style palette
OUTER = "#1c1c1c"
CREAM = "#eee8dc"
BORDER = "#2f2f2f"
INK = "#111111"
DOT = "#d94848"


def _load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for name in ("Georgia Bold.ttf", "georgiab.ttf", "Arial Bold.ttf", "arialbd.ttf", "Arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def draw_freewrite_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), OUTER)
    draw = ImageDraw.Draw(img)

    pad = max(2, size // 14)
    inner = size - 2 * pad
    radius = max(2, size // 10)

    # Cream tile with rounded corners
    tile_box = [pad, pad, pad + inner, pad + inner]
    draw.rounded_rectangle(tile_box, radius=radius, fill=CREAM, outline=BORDER, width=max(1, size // 64))

    # Inner frame line (double-border look)
    inset = max(1, size // 32)
    frame = [pad + inset, pad + inset, pad + inner - inset, pad + inner - inset]
    draw.rounded_rectangle(frame, radius=max(1, radius - inset), outline=BORDER, width=1)

    # Black "F"
    font_size = int(inner * 0.58)
    font = _load_font(font_size)
    text = "F"
    bbox = draw.textbbox((0, 0), text, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    tx = pad + (inner - tw) // 2 - bbox[0] - int(inner * 0.02)
    ty = pad + (inner - th) // 2 - bbox[1] - int(inner * 0.04)
    draw.text((tx, ty), text, fill=INK, font=font)

    # Red dot (bottom-right of tile)
    dot_r = max(2, size // 18)
    dot_cx = pad + inner - int(inner * 0.18)
    dot_cy = pad + inner - int(inner * 0.16)
    draw.ellipse(
        [dot_cx - dot_r, dot_cy - dot_r, dot_cx + dot_r, dot_cy + dot_r],
        fill=DOT,
    )
    return img


def write_png(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG", optimize=True)


def write_ico(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    sizes = [16, 32, 48, 256]
    imgs = [img.resize((s, s), Image.Resampling.LANCZOS) for s in sizes]
    imgs[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes])


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    site_icons = root / "site" / "icons"
    res = root / "Resources"

    base = draw_freewrite_icon(512)
    write_png(base, res / "app-icon-source.png")

    write_png(base.resize((32, 32), Image.Resampling.LANCZOS), site_icons / "icon-32.png")
    write_png(base.resize((16, 16), Image.Resampling.LANCZOS), site_icons / "icon-16.png")
    write_png(base.resize((180, 180), Image.Resampling.LANCZOS), site_icons / "apple-touch-icon.png")
    write_ico(base, site_icons / "favicon.ico")
    write_ico(base, root / "site" / "favicon.ico")
    write_ico(base, res / "app.ico")

    print("Generated Freewrite-style icons in", site_icons)


if __name__ == "__main__":
    main()
