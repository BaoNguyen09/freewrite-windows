"""Generate site and app icons from getfreewrite.com official artwork."""
from __future__ import annotations

import urllib.request
from pathlib import Path

from PIL import Image

SOURCE_URL = (
    "https://getfreewrite.com/cdn/shop/files/"
    "Freewrite_F_Modified_180x180.png?v=1613582291"
)
USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
)


def load_source(root: Path) -> Image.Image:
    bundled = root / "Resources" / "website-icon.png"
    if bundled.exists():
        return Image.open(bundled).convert("RGBA")

    request = urllib.request.Request(SOURCE_URL, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=30) as response:
        data = response.read()
    bundled.parent.mkdir(parents=True, exist_ok=True)
    bundled.write_bytes(data)
    return Image.open(bundled).convert("RGBA")


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
    # Flatten transparency onto dark gray so favicons read well on browser chrome.
    flat = Image.new("RGBA", base.size, "#1c1c1c")
    flat.alpha_composite(base)
    base = flat

    write_png(base, res / "app-icon-source.png")
    write_png(base.resize((32, 32), Image.Resampling.LANCZOS), site_icons / "icon-32.png")
    write_png(base.resize((16, 16), Image.Resampling.LANCZOS), site_icons / "icon-16.png")
    write_png(base.resize((180, 180), Image.Resampling.LANCZOS), site_icons / "apple-touch-icon.png")
    write_ico(base, site_icons / "favicon.ico")
    write_ico(base, root / "site" / "favicon.ico")
    write_ico(base, res / "app.ico")

    print("Generated icons from getfreewrite.com artwork in", site_icons)


if __name__ == "__main__":
    main()
