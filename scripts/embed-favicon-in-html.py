"""Inline site favicons as data URIs so CDN/browser cache cannot swap them."""
from __future__ import annotations

import base64
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ICONS = ROOT / "site" / "icons"
INDEX = ROOT / "site" / "index.html"

OLD = """    <link rel="icon" href="/icons/favicon.svg" type="image/svg+xml" />
    <link rel="icon" type="image/png" sizes="32x32" href="/icons/icon-32.png" />
    <link rel="icon" type="image/png" sizes="16x16" href="/icons/icon-16.png" />
    <link rel="icon" href="/favicon.ico" sizes="any" />
    <link rel="apple-touch-icon" sizes="180x180" href="/icons/apple-touch-icon.png" />"""


def data_uri(path: Path, mime: str) -> str:
    encoded = base64.standard_b64encode(path.read_bytes()).decode("ascii")
    return f"data:{mime};base64,{encoded}"


def main() -> None:
    svg = data_uri(ICONS / "favicon.svg", "image/svg+xml")
    png32 = data_uri(ICONS / "icon-32.png", "image/png")
    png16 = data_uri(ICONS / "icon-16.png", "image/png")

    new = f"""    <link rel="icon" href="{svg}" type="image/svg+xml" />
    <link rel="icon" type="image/png" sizes="32x32" href="{png32}" />
    <link rel="icon" type="image/png" sizes="16x16" href="{png16}" />
    <link rel="apple-touch-icon" sizes="180x180" href="/icons/apple-touch-icon.png" />"""

    html = INDEX.read_text(encoding="utf-8")
    if OLD not in html:
        raise SystemExit("index.html favicon block not found; update embed script")
    INDEX.write_text(html.replace(OLD, new), encoding="utf-8")
    print("Embedded favicons in", INDEX)


if __name__ == "__main__":
    main()
