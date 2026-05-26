"""Generate site and app icons from freewrite.io official favicon."""
from __future__ import annotations

import urllib.request
from pathlib import Path

from PIL import Image

SOURCE_URL = "https://framerusercontent.com/images/Kg6A4mdqHKGu0ODXGhebtMoh00.png"


def load_source(root: Path) -> Image.Image:
    source_path = root / "Resources" / "app-icon-source.png"
    if not source_path.exists():
        source_path.parent.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(SOURCE_URL, source_path)
    return Image.open(source_path).convert("RGBA")


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

    base = load_source(root)
    # Upscale source for sharper ICO generation.
    base = base.resize((512, 512), Image.Resampling.LANCZOS)

    write_png(base.resize((32, 32), Image.Resampling.LANCZOS), site_icons / "icon-32.png")
    write_png(base.resize((16, 16), Image.Resampling.LANCZOS), site_icons / "icon-16.png")
    write_png(base.resize((180, 180), Image.Resampling.LANCZOS), site_icons / "apple-touch-icon.png")
    write_ico(base, site_icons / "favicon.ico")
    write_ico(base, root / "site" / "favicon.ico")
    write_ico(base, res / "app.ico")

    print("Generated icons from freewrite.io in", site_icons)


if __name__ == "__main__":
    main()
