"""Build source/category contact sheets and a searchable offline HTML catalogue."""
import argparse,json,html,math
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2];UI=ROOT/'UI';OUT=UI/'预览'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
def sheet(assets,title,path):
    cols=4;cw,ch=360,250;rows=math.ceil(len(assets)/cols)
    im=Image.new('RGB',(cols*cw,rows*ch+54),'#26313c');d=ImageDraw.Draw(im)
    d.text((16,12),title,font=FONT,fill='white')
    for index,a in enumerate(assets):
        x=(index%cols)*cw;y=(index//cols)*ch+54
        d.rounded_rectangle((x+5,y+5,x+cw-5,y+ch-5),radius=8,fill='#151f29')
        # A checkerboard confirms the alpha channel on light and dark cells.
        for bx in range(x+12,x+cw-12,16):
            for by in range(y+12,y+ch-56,16):
                d.rectangle((bx,by,min(bx+15,x+cw-13),min(by+15,y+ch-57)),fill=('#d7dce1' if ((bx-x)//16+(by-y)//16)%2 else '#8c959e'))
        asset=Image.open(UI/a['path']).convert('RGBA');asset.thumbnail((cw-34,ch-84),Image.Resampling.LANCZOS)
        im.paste(asset,(x+(cw-asset.width)//2,y+14+(ch-86-asset.height)//2),asset)
        d.text((x+12,y+ch-50),Path(a['path']).stem,font=FONT,fill='#f6f7f8')
        d.text((x+12,y+ch-26),f"{a['category']}  {a['width']} × {a['height']}",font=FONT,fill='#bac8d5')
    im.save(path,quality=90)

def main():
    p=argparse.ArgumentParser();p.add_argument('--partial',action='store_true');args=p.parse_args()
    source=UI/'Tools/.work/partial_manifest.json' if args.partial else UI/'asset_manifest.json'
    manifest=json.loads(source.read_text(encoding='utf8'));assets=manifest['assets'];OUT.mkdir(exist_ok=True)
    for n in sorted(set(a['source_number'] for a in assets)):
        group=[a for a in assets if a['source_number']==n]
        for offset in range(0,len(group),24):sheet(group[offset:offset+24],f'EPS{n:02d} 独立控件 ({offset+1}–{min(offset+24,len(group))})',OUT/f'EPS{n:02d}_{offset//24+1}.jpg')
    if args.partial:return
    for category in sorted(set(a['category'] for a in assets)):
        group=[a for a in assets if a['category']==category]
        for offset in range(0,len(group),24):sheet(group[offset:offset+24],f'{category} ({offset+1}–{min(offset+24,len(group))})',OUT/f'{category}_{offset//24+1:02d}.jpg')
    cards='\n'.join(f'<article data-category="{html.escape(a["category"])}" data-name="{html.escape(a["path"])}"><a href="../{html.escape(a["path"])}"><div class="img"><img loading="lazy" src="../{html.escape(a["path"])}"></div></a><b>{html.escape(Path(a["path"]).stem)}</b><small>{a["category"]} · {a["width"]}×{a["height"]}</small><code>{html.escape(a["path"])}</code></article>' for a in assets)
    options=''.join(f'<option>{html.escape(c)}</option>' for c in sorted(set(a['category'] for a in assets)))
    document='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Unity UI 素材目录</title><style>
*{box-sizing:border-box}body{margin:0;background:#111923;color:#e8f0f9;font:15px system-ui,"Microsoft Yahei",sans-serif}header{position:sticky;top:0;background:#152434ee;padding:20px 28px;z-index:1;backdrop-filter:blur(16px);border-bottom:1px solid #38495a}h1{font-size:24px;margin:0 0 10px}p{color:#afbecf}select,input{font:inherit;padding:8px 12px;border:1px solid #52708c;border-radius:6px;background:#203448;color:white;margin-right:10px}main{padding:24px;display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:18px}article{background:#1a2838;padding:12px;border-radius:9px;overflow:hidden;border:1px solid #33475e}.img{height:190px;display:flex;align-items:center;justify-content:center;background:repeating-conic-gradient(#d5dbe1 0% 25%,#939eaa 0% 50%) 50% /20px 20px;border-radius:4px}img{max-width:94%;max-height:94%;object-fit:contain}b,small,code{display:block;margin-top:8px}small{color:#b8ccdf}code{font-size:11px;color:#85c6fc;overflow-wrap:anywhere}[hidden]{display:none}
</style><header><h1>Unity UI 素材目录</h1><p>COUNT 张独立透明 PNG · 点击图片查看原图 · Unity 内素材位于 Assets/UI</p><select id="category"><option value="">全部类型</option>OPTIONS</select><input id="search" placeholder="搜索 EPS 编号或名称"><span id="count"></span></header><main>CARDS</main><script>
const cat=document.getElementById('category'),search=document.getElementById('search'),cards=[...document.querySelectorAll('article')];function filter(){let n=0;for(const a of cards){a.hidden=!!((cat.value&&a.dataset.category!==cat.value)||!a.dataset.name.toLowerCase().includes(search.value.toLowerCase()));if(!a.hidden)n++;}document.getElementById('count').textContent=n+' 张';}cat.onchange=filter;search.oninput=filter;filter();</script></html>'''
    (OUT/'素材目录.html').write_text(document.replace('COUNT',str(len(assets))).replace('OPTIONS',options).replace('CARDS',cards),encoding='utf8')
    print(f'Catalogue: {len(assets)} assets')
if __name__=='__main__':main()
