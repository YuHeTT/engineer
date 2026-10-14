"""Convert EPS sources to local PDF and review PNGs without editing the EPS."""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import json
from pathlib import Path
import re
import subprocess

GHOSTSCRIPT = Path(r"C:\Users\StarryXiaoKui\AppData\Local\Programs\MiKTeX\miktex\bin\x64\mgs.exe")
WORKSPACE = Path(__file__).resolve().parents[2]
SOURCE = Path(r"C:\Users\StarryXiaoKui\Downloads\EPS格式")
TEMP = WORKSPACE / "UI" / "Tools" / ".work"


def convert(path: Path) -> dict:
    number = int(re.search(r"\((\d+)\)", path.name).group(1))
    prefix = TEMP / f"source{number:02d}"
    raw = path.read_bytes().decode("latin1")
    bbox = [float(n) for n in re.search(r"%%HiResBoundingBox:\s*([^\r\n]+)", raw).group(1).split()]
    dpi = 72 * min(1, 1600 / max(bbox[2] - bbox[0], bbox[3] - bbox[1]))
    common = [str(GHOSTSCRIPT), "-dSAFER", "-dBATCH", "-dNOPAUSE", "-dEPSCrop", "-dQUIET"]
    for device, output, options in (
        ("pdfwrite", prefix.with_suffix(".pdf"), []),
        ("pngalpha", prefix.with_suffix(".png"), [f"-r{dpi:.6f}", "-dTextAlphaBits=4", "-dGraphicsAlphaBits=4"]),
    ):
        if output.exists() and output.stat().st_size > 0:
            continue
        result = subprocess.run(common + [f"-sDEVICE={device}", f"-sOutputFile={output}"] + options + [str(path)], capture_output=True, timeout=180)
        if result.returncode:
            raise RuntimeError(f"{path.name}: {result.stderr.decode('utf8', errors='replace')} {result.stdout.decode('utf8', errors='replace')}")
    return {"number": number, "name": path.name, "bbox": bbox, "pdf": str(prefix.with_suffix(".pdf")), "preview": str(prefix.with_suffix(".png"))}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=SOURCE)
    args = parser.parse_args()
    TEMP.mkdir(parents=True, exist_ok=True)
    files = list(args.source.glob("*.eps"))
    results = []
    with ThreadPoolExecutor(max_workers=4) as pool:
        futures = {pool.submit(convert, path): path for path in files}
        for future in as_completed(futures):
            record = future.result()
            results.append(record)
            print(f"Converted {record['number']:02d}", flush=True)
    results.sort(key=lambda item: item["number"])
    (TEMP / "sources.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf8")
    print(f"Completed {len(results)} EPS sources", flush=True)


if __name__ == "__main__":
    main()
