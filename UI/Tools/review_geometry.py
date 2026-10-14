"""Show numbered Illustrator crop candidates over the original source sheets."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
WORK=Path(__file__).resolve().parent/'.work'
def main():
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',15)
    for n in range(1,33):
        d=json.loads((WORK/f'geometry{n:02d}.json').read_text(encoding='utf8'))
        im=Image.open(WORK/f'source{n:02d}.png').convert('RGB');draw=ImageDraw.Draw(im)
        sx=im.width/d['page_size'][0];sy=im.height/d['page_size'][1]
        for c in d['crop_groups']:
            r=c['page_rect'];b=[r[0]*sx,r[1]*sy,r[2]*sx,r[3]*sy]
            draw.rectangle(b,outline='#ffbe39',width=2)
            label=str(c['reading_order'])
            draw.rectangle((b[0],b[1],b[0]+24,b[1]+18),fill='#e6443d')
            draw.text((b[0]+2,b[1]),label,font=font,fill='white')
        im.save(WORK/f'candidates{n:02d}.jpg',quality=92)
if __name__=='__main__':main()
