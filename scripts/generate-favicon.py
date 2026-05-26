"""Generate Freewrite Windows F-brand favicons for site/ and Resources/."""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

BG = "#1a1a1a"
FG = "#ffffff"


def draw_f_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), BG)
    draw = ImageDraw.Draw(img)
    # Draw a bold "F" with rectangles so it stays crisp at 16px.
    m = max(2, size // 8)
    bar = max(2, size // 5)
    top = m
    left = m
    height = size - 2 * m
    width = size - 2 * m
    mid = top + height // 2

    # Vertical stem
    draw.rectangle([left, top, left + bar, top + height], fill=FG)
    # Top horizontal
    draw.rectangle([left, top, left + width, top + bar], fill=FG)
    # Middle horizontal (~38% down)
    draw.rectangle([left, mid - bar // 2, left + int(width * 0.72), mid + bar // 2], fill=FG)
    return img


def write_png(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG", optimize=True)


def write_ico(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    sizes = [16, 32, 48, 256]
    imgs = [img.resize((s, s), Image.Resampling.LANCZOS) for s in sizes]
    imgs[-1].save(
        path,
        format="ICO",
        sizes=[(s, s) for s in sizes],
    )


def write_svg(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
  <rect width="32" height="32" fill="#1a1a1a"/>
  <path fill="#fff" d="M7 7h18v5H13v4h10v5H13v4H7z"/>
</svg>
""",
        encoding="utf-8",
    )


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    site_icons = root / "site" / "icons"
    res = root / "Resources"

    base = draw_f_icon(256)
    write_svg(site_icons / "favicon.svg")
    write_png(base.resize((32, 32), Image.Resampling.LANCZOS), site_icons / "icon-32.png")
    write_png(base.resize((16, 16), Image.Resampling.LANCZOS), site_icons / "icon-16.png")
    write_png(base.resize((180, 180), Image.Resampling.LANCZOS), site_icons / "apple-touch-icon.png")
    write_ico(base, site_icons / "favicon.ico")
    write_ico(base, root / "site" / "favicon.ico")
    write_ico(base, res / "app.ico")

    # Remove legacy filenames so CDN cannot serve stale paths.
    site = root / "site"
    for legacy in (
        "favicon-16.png",
        "favicon-32.png",
        "apple-touch-icon.png",
        "favicon.ico",
    ):
        legacy_path = site / legacy
        if legacy_path.exists() and legacy != "favicon.ico":
            legacy_path.unlink()

    print("Generated icons in", site_icons)


if __name__ == "__main__":
    main()
