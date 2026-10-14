"""Print PDF drawing operations while developing the vector extraction."""
import argparse
from pathlib import Path
from pypdf import PdfReader

parser = argparse.ArgumentParser()
parser.add_argument("file", type=Path)
parser.add_argument("--limit", type=int, default=120)
args = parser.parse_args()
reader = PdfReader(args.file)
page = reader.pages[0]
print("page", list(page.mediabox))
print("resources", {str(k): str(v)[:300] for k, v in page["/Resources"].items()})
ops = page.get_contents().operations
print("operation count", len(ops))
for i, (operands, operator) in enumerate(ops[:args.limit]):
    print(i, operands, operator)
