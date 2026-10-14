"""Remove authored page backgrounds from PDF copies, preserving vector UI art."""
from __future__ import annotations

from concurrent.futures import ThreadPoolExecutor, as_completed
import json
from pathlib import Path
import sys

WORKSPACE = Path(__file__).resolve().parents[2]
TEMP = WORKSPACE / "UI" / "Tools" / ".work"
sys.path.insert(0, str(WORKSPACE / "UI" / "Tools" / ".python"))
import pymupdf
from pypdf import PdfReader, PdfWriter


def matrix_product(a, b):
    return (
        a[0]*b[0]+a[2]*b[1], a[1]*b[0]+a[3]*b[1],
        a[0]*b[2]+a[2]*b[3], a[1]*b[2]+a[3]*b[3],
        a[0]*b[4]+a[2]*b[5]+a[4], a[1]*b[4]+a[3]*b[5]+a[5],
    )


def transform(matrix, x, y):
    return (matrix[0]*x + matrix[2]*y + matrix[4], matrix[1]*x + matrix[3]*y + matrix[5])


def bbox(points):
    return (min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)) if points else None


def clean(source: Path, number: int) -> dict:
    reader = PdfReader(source)
    page = reader.pages[0]
    w, h = float(page.mediabox.width), float(page.mediabox.height)
    content = page.get_contents()
    state = {"matrix": (1, 0, 0, 1, 0, 0), "clip": (0, 0, w, h)}
    stack = []
    points = []
    edited = []
    removed = []
    clip_pending = False

    def is_background(box):
        if box is None:
            return False
        x0, y0, x1, y1 = box
        return x0 <= w*.045 and y0 <= h*.045 and x1 >= w*.955 and y1 >= h*.955 and (x1-x0)*(y1-y0) >= w*h*.85

    for index, (operands, op) in enumerate(content.operations):
        if op == b"q":
            stack.append(state.copy())
        elif op == b"Q":
            state = stack.pop() if stack else state
        elif op == b"cm":
            state["matrix"] = matrix_product(state["matrix"], tuple(float(v) for v in operands))
        elif op in (b"m", b"l"):
            points.append(transform(state["matrix"], *map(float, operands)))
        elif op == b"c":
            nums = list(map(float, operands))
            points.extend(transform(state["matrix"], *nums[i:i+2]) for i in (0, 2, 4))
        elif op in (b"v", b"y"):
            nums = list(map(float, operands))
            points.extend(transform(state["matrix"], *nums[i:i+2]) for i in (0, 2))
        elif op == b"re":
            x, y, width, height = map(float, operands)
            points.extend(transform(state["matrix"], *p) for p in ((x,y),(x+width,y),(x+width,y+height),(x,y+height)))
        elif op in (b"W", b"W*"):
            clip_pending = True
        elif op in (b"f", b"f*", b"F", b"B", b"B*", b"b", b"b*", b"S", b"s", b"n"):
            path_box = bbox(points)
            if op != b"n" and is_background(path_box):
                removed.append({"operation": index, "type": op.decode(), "bbox": path_box})
                op, operands = b"n", []
            if clip_pending and path_box:
                prior = state["clip"]
                state["clip"] = (max(prior[0],path_box[0]),max(prior[1],path_box[1]),min(prior[2],path_box[2]),min(prior[3],path_box[3]))
            points = []
            clip_pending = False
        elif op == b"sh" and is_background(state["clip"]):
            removed.append({"operation": index, "type": "page shading", "bbox": state["clip"]})
            continue
        elif op == b"Do":
            obj = page["/Resources"].get("/XObject", {}).get(operands[0])
            if obj and obj.get_object().get("/Subtype") == "/Image":
                image_box = bbox([transform(state["matrix"], *p) for p in ((0,0),(1,0),(1,1),(0,1))])
                if is_background(image_box):
                    removed.append({"operation": index, "type": "page image", "bbox": image_box})
                    continue
        edited.append((operands, op))
    content.operations = edited
    page.replace_contents(content)
    writer = PdfWriter()
    writer.add_page(page)
    output = TEMP / f"transparent{number:02d}.pdf"
    with output.open("wb") as stream:
        writer.write(stream)
    doc = pymupdf.open(output)
    preview = TEMP / f"transparent{number:02d}.png"
    pix = doc[0].get_pixmap(matrix=pymupdf.Matrix(min(1,1600/w),min(1,1600/w)), alpha=True)
    pix.save(preview)
    doc.close()
    return {"source": number, "removed": removed, "width": w, "height": h, "pdf": str(output), "preview": str(preview)}


def main():
    sources = json.loads((TEMP / "sources.json").read_text(encoding="utf8"))
    results = []
    with ThreadPoolExecutor(max_workers=4) as pool:
        futures = {pool.submit(clean, Path(item["pdf"]), item["number"]): item for item in sources}
        for future in as_completed(futures):
            result = future.result()
            print(f"Transparent {result['source']:02d}: {len(result['removed'])} page backgrounds removed", flush=True)
            results.append(result)
    results.sort(key=lambda item:item["source"])
    (TEMP / "background_removal.json").write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding="utf8")


if __name__ == "__main__":
    main()
