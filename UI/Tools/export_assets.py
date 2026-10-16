"""Export complete EPS widgets using native outlines and rendered colors."""
from __future__ import annotations

import argparse
from concurrent.futures import ProcessPoolExecutor, as_completed
import hashlib
import json
import math
import pickle
from pathlib import Path
import re
import shutil
import sys
import tempfile

import numpy as np
from PIL import Image

from native_mask import alpha_mask, paths_for_item

sys.path.insert(0, str(Path(__file__).resolve().parent / ".python"))
import pymupdf as fitz


ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "UI"
WORK = UI / "Tools/.work"
CATEGORIES = {
    "panel": "文本框与数据面板",
    "button": "点击按钮",
    "icon": "图标",
    "gauge": "圆形仪表",
    "decoration": "边框与装饰",
}
LABELS = {
    "panel": "Panel",
    "button": "Button",
    "icon": "Icon",
    "gauge": "Gauge",
    "decoration": "Decoration",
}
TARGET = {
    "panel": 1024,
    "button": 768,
    "icon": 384,
    "gauge": 512,
    "decoration": 1024,
}
GENERATED_NAME = re.compile(
    r"^EPS\d{2}_(?:Panel|Button|Icon|Gauge|Decoration)_\d+\.png$"
)


def page_to_native(rect, data):
    native_bbox = data["native_bbox"]
    width, height = data["page_size"]
    scale_x = (native_bbox[2] - native_bbox[0]) / width
    scale_y = (native_bbox[3] - native_bbox[1]) / height
    return [
        native_bbox[0] + rect[0] * scale_x,
        native_bbox[3] - rect[3] * scale_y,
        native_bbox[0] + rect[2] * scale_x,
        native_bbox[3] - rect[1] * scale_y,
    ]


def background_color(data):
    for path in data["paths"]:
        if path.get("possible_background") and path.get("fill"):
            fill = path["fill"]
            values = fill["values"]
            if fill.get("rgb"):
                return np.array(fill["rgb"]) * 255
            if fill["operator"] == "g":
                return np.array([values[0]] * 3) * 255
            if fill["operator"] == "k" and len(values) == 4:
                return (1 - np.minimum(1, np.array(values[:3]) + values[3])) * 255
    return np.array([16, 24, 33], dtype=float)


def unpremultiply_rgba(rgba: np.ndarray) -> np.ndarray:
    """Convert PyMuPDF's premultiplied RGBA samples to straight-alpha RGBA."""
    result = rgba.copy()
    alpha = result[:, :, 3].astype(np.uint16)
    rgb = result[:, :, :3].astype(np.uint16)
    visible = alpha > 0
    rgb[visible] = np.minimum(
        255,
        (rgb[visible] * 255 + alpha[visible, None] // 2) // alpha[visible, None],
    )
    rgb[~visible] = 0
    result[:, :, :3] = rgb.astype(np.uint8)
    return result


def render_pdf_alpha(page, rect, scale, width, height):
    pixmap = page.get_pixmap(
        matrix=fitz.Matrix(scale, scale),
        clip=fitz.Rect(rect),
        colorspace=fitz.csRGB,
        alpha=True,
    )
    rgba = np.frombuffer(pixmap.samples, dtype=np.uint8).reshape(
        pixmap.height, pixmap.width, 4
    ).copy()
    if (pixmap.width, pixmap.height) != (width, height):
        # Resize premultiplied channels independently so edge colors remain valid.
        rgba = np.dstack(
            [
                np.asarray(
                    Image.fromarray(rgba[:, :, channel]).resize(
                        (width, height), Image.Resampling.LANCZOS
                    )
                )
                for channel in range(4)
            ]
        )
    return Image.fromarray(unpremultiply_rgba(rgba), "RGBA")


def render_native_mask(page, data, paths, native_rect, rect, scale, width, height):
    mask = alpha_mask(data, paths, native_rect, width, height)
    if not np.any(mask):
        raise ValueError("Empty alpha mask")
    pixmap = page.get_pixmap(
        matrix=fitz.Matrix(scale, scale), clip=fitz.Rect(rect), alpha=False
    )
    rgb = np.frombuffer(pixmap.samples, dtype=np.uint8).reshape(
        pixmap.height, pixmap.width, 3
    ).copy()
    if (pixmap.width, pixmap.height) != (width, height):
        rgb = np.array(
            Image.fromarray(rgb).resize((width, height), Image.Resampling.LANCZOS)
        )
    alpha = mask.astype(np.float32) / 255
    background = background_color(data)
    # Recover the unassociated foreground at antialiased edges.
    foreground = (
        rgb.astype(np.float32) - (1 - alpha[:, :, None]) * background[None, None, :]
    ) / np.maximum(alpha[:, :, None], 1 / 255)
    foreground = np.clip(foreground, 0, 255).astype(np.uint8)
    foreground[mask == 0] = 0
    return Image.fromarray(np.dstack([foreground, mask]), "RGBA")


def render_source(number, items, output_root, manifest_prefix=None):
    data = pickle.loads((WORK / f"native{number:02d}.pkl").read_bytes())
    by_order = {group["reading_order"]: group for group in data["crop_groups"]}
    width, height = data["page_size"]
    records = []
    source_pdf = fitz.open(WORK / f"source{number:02d}.pdf")
    transparent_pdf = None
    counters = {key: 0 for key in CATEGORIES}

    try:
        for item in items:
            category = item["category"]
            if category not in CATEGORIES:
                raise ValueError(f"Unknown category for EPS{number:02d}: {category!r}")
            mode = item.get("mode", "native_outline")
            if mode not in ("native_outline", "rendered_alpha"):
                raise ValueError(f"Unknown mode for EPS{number:02d}: {mode!r}")

            counters[category] += 1
            chosen = [by_order[index] for index in item.get("ids", [])]
            if "rect" in item:
                rect = [
                    item["rect"][0] * width,
                    item["rect"][1] * height,
                    item["rect"][2] * width,
                    item["rect"][3] * height,
                ]
            elif chosen:
                boxes = [group["page_rect"] for group in chosen]
                rect = [
                    min(box[0] for box in boxes),
                    min(box[1] for box in boxes),
                    max(box[2] for box in boxes),
                    max(box[3] for box in boxes),
                ]
            else:
                raise ValueError(f"No rect for EPS{number:02d}: {item}")

            padding = max(width, height) * item.get("padding", 0.0025)
            rect = [
                max(0, rect[0] - padding),
                max(0, rect[1] - padding),
                min(width, rect[2] + padding),
                min(height, rect[3] + padding),
            ]
            desired = item.get("long_edge", TARGET[category])
            scale = desired / max(rect[2] - rect[0], rect[3] - rect[1])
            # Integral clip bounds ensure the source renderer and mask share one grid.
            x0, y0 = math.floor(rect[0] * scale), math.floor(rect[1] * scale)
            x1, y1 = math.ceil(rect[2] * scale), math.ceil(rect[3] * scale)
            rect = [x0 / scale, y0 / scale, x1 / scale, y1 / scale]
            pixel_width, pixel_height = x1 - x0, y1 - y0
            native_rect = page_to_native(rect, data)
            selected = dict(
                item,
                native_rect=native_rect,
                group_ids=[group["id"] for group in chosen],
            )

            if mode == "rendered_alpha":
                if transparent_pdf is None:
                    transparent_path = WORK / f"transparent{number:02d}.pdf"
                    if not transparent_path.is_file():
                        raise FileNotFoundError(
                            f"Rendered-alpha mode needs {transparent_path}"
                        )
                    transparent_pdf = fitz.open(transparent_path)
                output = render_pdf_alpha(
                    transparent_pdf[0],
                    rect,
                    scale,
                    pixel_width,
                    pixel_height,
                )
                source_groups = selected["group_ids"]
                shape_count = 0
                method = "transparent_pdf_alpha_render"
            else:
                # These sheets use opacity-masked Illustrator meshes for glows. The
                # mesh alpha is absent from the geometry cache, so its bounding box
                # is not a valid silhouette.
                if number in (7, 24):
                    selected["exclude_mesh"] = True
                if item.get("group_suffixes"):
                    selected["group_ids"] = [
                        group["id"]
                        for group in data["groups"]
                        if group["id"].split("_g")[-1] in item["group_suffixes"]
                    ]
                if number == 24:
                    selected["exclude_groups"] = [
                        group["id"]
                        for group in data["groups"]
                        if group["id"].endswith("_g0003")
                    ]
                paths = paths_for_item(data, selected)
                if not paths:
                    raise ValueError(f"No shapes for EPS{number:02d}: {item}")
                output = render_native_mask(
                    source_pdf[0],
                    data,
                    paths,
                    native_rect,
                    rect,
                    scale,
                    pixel_width,
                    pixel_height,
                )
                source_groups = selected["group_ids"]
                shape_count = len(paths)
                method = "native_outline_mask_original_render"

            content = output.getbbox()
            if content is None:
                raise ValueError(f"Blank result for EPS{number:02d}: {item}")
            # Keep a small transparent margin for UI sprites and edge filtering.
            content = (
                max(0, content[0] - 4),
                max(0, content[1] - 4),
                min(output.width, content[2] + 4),
                min(output.height, content[3] + 4),
            )
            output = output.crop(content)
            filename = (
                f"EPS{number:02d}_{LABELS[category]}_{counters[category]:02d}.png"
            )
            relative_path = Path(CATEGORIES[category]) / filename
            output_path = output_root / relative_path
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output.save(output_path, optimize=True)
            recorded_path = (
                relative_path
                if manifest_prefix is None
                else Path(manifest_prefix) / relative_path
            )
            records.append(
                {
                    "path": recorded_path.as_posix(),
                    "source_number": number,
                    "source_name": f"源文件 ({number}).eps",
                    "width": output.width,
                    "height": output.height,
                    "category": CATEGORIES[category],
                    "source_rect_normalized": [
                        round(rect[0] / width, 6),
                        round(rect[1] / height, 6),
                        round(rect[2] / width, 6),
                        round(rect[3] / height, 6),
                    ],
                    "source_groups": source_groups,
                    "shape_count": shape_count,
                    "sha256": hashlib.sha256(output_path.read_bytes()).hexdigest(),
                    "method": method,
                    "note": item.get("note", ""),
                }
            )
    finally:
        source_pdf.close()
        if transparent_pdf is not None:
            transparent_pdf.close()
    return records


def generated_files(root):
    resolved_ui = UI.resolve()
    resolved_root = root.resolve()
    if root == UI:
        if resolved_root != resolved_ui:
            raise RuntimeError(f"Refusing unsafe UI cleanup root: {resolved_root}")
    elif resolved_root != (WORK / "partial_assets").resolve():
        raise RuntimeError(f"Refusing unsafe partial cleanup root: {resolved_root}")

    for category in CATEGORIES.values():
        directory = root / category
        if not directory.exists():
            continue
        resolved_directory = directory.resolve()
        if root == UI and resolved_directory.parent != resolved_ui:
            raise RuntimeError(f"Refusing cleanup outside UI: {resolved_directory}")
        if root != UI and resolved_directory.parent != resolved_root:
            raise RuntimeError(f"Refusing cleanup outside partial assets: {resolved_directory}")
        for candidate in directory.glob("EPS*.png"):
            if (
                candidate.is_file()
                and not candidate.is_symlink()
                and GENERATED_NAME.fullmatch(candidate.name)
            ):
                yield candidate


def commit_assets(stage, destination, records, manifest_prefix=None):
    expected = set()
    prefix_parts = len(Path(manifest_prefix).parts) if manifest_prefix else 0
    for record in records:
        recorded = Path(record["path"])
        relative = Path(*recorded.parts[prefix_parts:])
        expected.add(relative.as_posix())
        source = stage / relative
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        source.replace(target)

    removed = []
    for candidate in generated_files(destination):
        relative = candidate.relative_to(destination).as_posix()
        if relative not in expected:
            candidate.unlink()
            removed.append(relative)
    return removed


def write_manifest(path, records):
    payload = {
        "format_version": 1,
        "source_directory": r"C:\Users\StarryXiaoKui\Downloads\EPS格式",
        "asset_count": len(records),
        "assets": records,
    }
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf8"
    )
    temporary.replace(path)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--sources",
        nargs="+",
        type=int,
        help="Export only these sources into UI/Tools/.work/partial_assets",
    )
    args = parser.parse_args()
    plan = json.loads((UI / "Tools/asset_plan.json").read_text(encoding="utf8"))
    partial = args.sources is not None
    numbers = (
        list(dict.fromkeys(args.sources))
        if partial
        else [int(number) for number in plan]
    )
    missing = [number for number in numbers if str(number) not in plan]
    if missing:
        parser.error(f"sources missing from asset_plan.json: {missing}")

    WORK.mkdir(parents=True, exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix="export_stage_", dir=WORK))
    partial_prefix = Path("Tools/.work/partial_assets") if partial else None
    results = []
    try:
        with ProcessPoolExecutor(max_workers=4) as pool:
            futures = {
                pool.submit(
                    render_source,
                    number,
                    plan[str(number)],
                    stage,
                    partial_prefix,
                ): number
                for number in numbers
            }
            for future in as_completed(futures):
                assets = future.result()
                results.extend(assets)
                print(
                    f"Exported EPS{futures[future]:02d}: {len(assets)}", flush=True
                )
        results.sort(key=lambda item: (item["source_number"], item["path"]))

        if partial:
            destination = WORK / "partial_assets"
            manifest = WORK / "partial_manifest.json"
        else:
            destination = UI
            manifest = UI / "asset_manifest.json"
        removed = commit_assets(stage, destination, results, partial_prefix)
        write_manifest(manifest, results)
        print(
            f"Committed {len(results)} assets; removed {len(removed)} stale generated files",
            flush=True,
        )
    finally:
        shutil.rmtree(stage, ignore_errors=True)


if __name__ == "__main__":
    main()
