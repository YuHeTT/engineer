"""Export complete EPS widgets using native outlines and the original rendered colors."""
from __future__ import annotations
import argparse,json,pickle,hashlib,sys,math
from pathlib import Path
from concurrent.futures import ProcessPoolExecutor,as_completed
import numpy as np
from PIL import Image
from native_mask import alpha_mask,paths_for_item,box_area
sys.path.insert(0,str(Path(__file__).resolve().parent/'.python'))
import pymupdf as fitz
ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'UI/Tools/.work'
CATEGORIES={'panel':'文本框与数据面板','button':'点击按钮','icon':'图标','gauge':'圆形仪表','decoration':'边框与装饰'}
LABELS={'panel':'Panel','button':'Button','icon':'Icon','gauge':'Gauge','decoration':'Decoration'}
TARGET={'panel':1024,'button':768,'icon':384,'gauge':512,'decoration':1024}

def page_to_native(r,data):
    nb=data['native_bbox'];w,h=data['page_size'];sx=(nb[2]-nb[0])/w;sy=(nb[3]-nb[1])/h
    return [nb[0]+r[0]*sx,nb[3]-r[3]*sy,nb[0]+r[2]*sx,nb[3]-r[1]*sy]

def background_color(data):
    for p in data['paths']:
        if p.get('possible_background') and p.get('fill'):
            f=p['fill'];v=f['values']
            if f.get('rgb'):return np.array(f['rgb'])*255
            if f['operator']=='g':return np.array([v[0]]*3)*255
            if f['operator']=='k' and len(v)==4:return (1-np.minimum(1,np.array(v[:3])+v[3]))*255
    return np.array([16,24,33],dtype=float)

def render_source(n,items):
    data=pickle.loads((WORK/f'native{n:02d}.pkl').read_bytes())
    byorder={g['reading_order']:g for g in data['crop_groups']}
    w,h=data['page_size'];nb=data['native_bbox'];records=[]
    original=fitz.open(WORK/f'source{n:02d}.pdf');bg=background_color(data)
    counters={key:0 for key in CATEGORIES}
    for item in items:
        category=item['category'];counters[category]+=1
        chosen=[byorder[i] for i in item.get('ids',[])]
        if 'rect' in item:r=[item['rect'][0]*w,item['rect'][1]*h,item['rect'][2]*w,item['rect'][3]*h]
        elif chosen:
            boxes=[c['page_rect'] for c in chosen];r=[min(b[0] for b in boxes),min(b[1] for b in boxes),max(b[2] for b in boxes),max(b[3] for b in boxes)]
        else:raise ValueError(f'No rect for EPS {n}')
        padding=max(w,h)*.0025
        r=[max(0,r[0]-padding),max(0,r[1]-padding),min(w,r[2]+padding),min(h,r[3]+padding)]
        desired=item.get('long_edge',TARGET[category]);scale=desired/max(r[2]-r[0],r[3]-r[1])
        # Integral clip bounds ensure the source renderer and mask share one grid.
        x0,y0=math.floor(r[0]*scale),math.floor(r[1]*scale)
        x1,y1=math.ceil(r[2]*scale),math.ceil(r[3]*scale)
        r=[x0/scale,y0/scale,x1/scale,y1/scale]
        width,height=x1-x0,y1-y0
        native=page_to_native(r,data)
        selected=dict(item,native_rect=native,group_ids=[c['id'] for c in chosen])
        if item.get('group_suffixes'):
            selected['group_ids']=[g['id'] for g in data['groups'] if g['id'].split('_g')[-1] in item['group_suffixes']]
        if n==24:selected['exclude_groups']=[g['id'] for g in data['groups'] if g['id'].endswith('_g0003')]
        paths=paths_for_item(data,selected)
        if not paths:raise ValueError(f'No shapes for EPS{n} {item}')
        mask=alpha_mask(data,paths,native,width,height)
        if not np.any(mask):raise ValueError(f'Empty alpha mask for EPS{n} {item}')
        pix=original[0].get_pixmap(matrix=fitz.Matrix(scale,scale),clip=fitz.Rect(r),alpha=False)
        rgb=np.frombuffer(pix.samples,dtype=np.uint8).reshape(pix.height,pix.width,3).copy()
        if (pix.width,pix.height)!=(width,height):
            rgb=np.array(Image.fromarray(rgb).resize((width,height),Image.Resampling.LANCZOS))
        a=mask.astype(np.float32)/255
        # Recover the unassociated foreground at antialiased edges.
        foreground=(rgb.astype(np.float32)-(1-a[:,:,None])*bg[None,None,:])/np.maximum(a[:,:,None],1/255)
        foreground=np.clip(foreground,0,255).astype(np.uint8)
        foreground[mask==0]=0
        output=Image.fromarray(np.dstack([foreground,mask]),'RGBA')
        content=output.getbbox()
        if content is None:raise ValueError('Blank result')
        # Keep a small transparent margin for UI sprites and edge filtering.
        content=(max(0,content[0]-4),max(0,content[1]-4),min(width,content[2]+4),min(height,content[3]+4))
        output=output.crop(content)
        filename=f'EPS{n:02d}_{LABELS[category]}_{counters[category]:02d}.png'
        path=ROOT/'UI'/CATEGORIES[category]/filename;path.parent.mkdir(parents=True,exist_ok=True);output.save(path,optimize=True)
        record={'path':f'{CATEGORIES[category]}/{filename}','source_number':n,'source_name':f'源文件 ({n}).eps',
                'width':output.width,'height':output.height,'category':CATEGORIES[category],
                'source_rect_normalized':[round(r[0]/w,6),round(r[1]/h,6),round(r[2]/w,6),round(r[3]/h,6)],
                'source_groups':selected['group_ids'],'shape_count':len(paths),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                'method':'native_outline_mask_original_render','note':item.get('note','')}
        records.append(record)
    original.close();return records

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--sources',nargs='*',type=int);args=parser.parse_args()
    plan=json.loads((ROOT/'UI/Tools/asset_plan.json').read_text(encoding='utf8'))
    numbers=args.sources or [int(n) for n in plan]
    results=[]
    with ProcessPoolExecutor(max_workers=4) as pool:
        futures={pool.submit(render_source,n,plan[str(n)]):n for n in numbers}
        for future in as_completed(futures):
            assets=future.result();results.extend(assets);print(f'Exported EPS{futures[future]:02d}: {len(assets)}',flush=True)
    results.sort(key=lambda x:(x['source_number'],x['path']))
    if args.sources:
        manifest=WORK/'partial_manifest.json'
    else:manifest=ROOT/'UI/asset_manifest.json'
    manifest.write_text(json.dumps({'format_version':1,'source_directory':r'C:\Users\StarryXiaoKui\Downloads\EPS格式','asset_count':len(results),'assets':results},ensure_ascii=False,indent=2),encoding='utf8')
if __name__=='__main__':main()
