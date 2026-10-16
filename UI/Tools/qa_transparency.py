"""Create light/dark transparency QA sheets and report alpha coverage."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "UI"
WORK = UI / "Tools/.work"
FONT = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 16)


def checker(size: tuple[int, int], cell: int = 16) -> Image.Image:
    image = Image.new("RGB", size, "#d7dce1")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill="#87929d")
    return image


def fit(asset: Image.Image, size: tuple[int, int]) -> Image.Image:
    copy = asset.copy()
    copy.thumbnail(size, Image.Resampling.LANCZOS)
    return copy


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=WORK / "partial_manifest.json")
    parser.add_argument("--sources", nargs="*", type=int, default=[7, 24])
    args = parser.parse_args()

    manifest = json.loads(args.manifest.read_text(encoding="utf8"))
    assets = [a for a in manifest["assets"] if a["source_number"] in args.sources]
    cols, card_w, card_h = 3, 420, 250
    rows = (len(assets) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * card_w, rows * card_h), "#202b35")
    draw = ImageDraw.Draw(sheet)
    report = []

    for index, record in enumerate(assets):
        asset = Image.open(UI / record["path"]).convert("RGBA")
        alpha = np.asarray(asset.getchannel("A"))
        transparent = float(np.mean(alpha == 0))
        partial = float(np.mean((alpha > 0) & (alpha < 255)))
        center = int(alpha[alpha.shape[0] // 2, alpha.shape[1] // 2])
        report.append(
            {
                "path": record["path"],
                "size": list(asset.size),
                "transparent_fraction": round(transparent, 6),
                "partial_alpha_fraction": round(partial, 6),
                "center_alpha": center,
            }
        )

        x = index % cols * card_w
        y = index // cols * card_h
        preview = fit(asset, (card_w // 3 - 12, card_h - 52))
        backgrounds = [Image.new("RGB", preview.size, "white"), Image.new("RGB", preview.size, "black"), checker(preview.size)]
        for column, background in enumerate(backgrounds):
            background.paste(preview, (0, 0), preview)
            sheet.paste(background, (x + 6 + column * (card_w // 3), y + 6))
        draw.text((x + 8, y + card_h - 40), Path(record["path"]).stem, font=FONT, fill="white")
        draw.text(
            (x + 8, y + card_h - 20),
            f"transparent {transparent:.1%}  partial {partial:.1%}  center A={center}",
            font=FONT,
            fill="#bdcbd8",
        )

    output = WORK / "qa_transparency_07_24.jpg"
    sheet.save(output, quality=92)
    (WORK / "qa_transparency_07_24.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf8"
    )
    print(output)


if __name__ == "__main__":
    main()
