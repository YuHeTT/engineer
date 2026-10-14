"""Create source review contact sheets on a dark checkerboard."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

WORKSPACE = Path(__file__).resolve().parents[2]
TEMP = WORKSPACE / "UI" / "Tools" / ".work"
font = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 22)

for transparent in (False, True):
    prefix = "transparent" if transparent else "source"
    for batch in range(4):
        canvas = Image.new("RGB", (1600, 1320), "#162630")
        draw = ImageDraw.Draw(canvas)
        for i in range(8):
            number = batch*8+i+1
            path = TEMP / f"{prefix}{number:02d}.png"
            if not path.exists():
                continue
            img = Image.open(path).convert("RGBA")
            img.thumbnail((780, 285), Image.Resampling.LANCZOS)
            x, y = (i%2)*800+10, (i//2)*330
            draw.text((x,y+3),f"EPS {number:02d}",fill="white",font=font)
            canvas.paste(img,(x,y+33),img)
        canvas.save(TEMP / f"{prefix}_overview_{batch+1}.jpg", quality=90)
