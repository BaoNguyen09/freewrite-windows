"""Inline site favicons as data URIs so CDN/browser cache cannot swap them."""
from __future__ import annotations

import base64
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ICONS = ROOT / "site" / "icons"
INDEX = ROOT / "site" / "index.html"

FAVICON_BLOCK = re.compile(
    r"    <link rel=\"icon\".*?\n"
    r"    <link rel=\"icon\".*?\n"
    r"    <link rel=\"icon\".*?\n"
    r"    <link rel=\"apple-touch-icon\".*?/>",
    re.DOTALL,
)


def data_uri(path: Path, mime: str) -> str:
    encoded = base64.standard_b64encode(path.read_bytes()).decode("ascii")
    return f"data:{mime};base64,{encoded}"


def main() -> None:
    png32 = data_uri(ICONS / "icon-32.png", "image/png")
    png16 = data_uri(ICONS / "icon-16.png", "image/png")

    new = f"""    <link rel="icon" type="image/png" sizes="32x32" href="{png32}" />
    <link rel="icon" type="image/png" sizes="16x16" href="{png16}" />
    <link rel="icon" href="/favicon.ico" sizes="any" />
    <link rel="apple-touch-icon" sizes="180x180" href="/icons/apple-touch-icon.png" />"""

    html = INDEX.read_text(encoding="utf-8")
    updated, count = FAVICON_BLOCK.subn(new, html, count=1)
    if count != 1:
        raise SystemExit("index.html favicon block not found; update embed script")
    INDEX.write_text(updated, encoding="utf-8")
    print("Embedded favicons in", INDEX)


if __name__ == "__main__":
    main()
