"""Render Illustrator outlines as alpha masks for isolating complete widgets."""
from collections import defaultdict
import sys
from pathlib import Path
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent/'.python'))
import pymupdf as fitz

def box_area(b):
    return max(0,b[2]-b[0])*max(0,b[3]-b[1]) if b else 0

def overlap(a,b):
    return box_area([max(a[0],b[0]),max(a[1],b[1]),min(a[2],b[2]),min(a[3],b[3])])

def paths_for_item(data,item):
    groups={g['id']:g for g in data['groups']}
    native=data['native_bbox'];canvas=box_area(native)
    chosen=set(item.get('group_ids',[]));excluded=set(item.get('exclude_groups',[]))
    rect=item['native_rect']
    def ancestors(gid):
        while gid:
            yield gid;gid=groups[gid]['parent_id']
    result=[]
    for p in data['paths']:
        b=p['visible_bbox'];chain=set(ancestors(p['parent_id']))
        if b is None or not p['painted'] or excluded & chain:continue
        if p['possible_background'] or box_area(b)>canvas*.6:continue
        if chosen:
            if not chosen & chain:continue
        else:
            frac=overlap(b,rect)/max(1e-12,box_area(b))
            if frac<.92:continue
            bw,bh=b[2]-b[0],b[3]-b[1]
            if (bh> (native[3]-native[1])*.85 and bw< (native[2]-native[0])*.02):continue
        if overlap(b,rect) or box_area(b)==0:result.append(p)
    return result

def alpha_mask(data,paths,rect,width,height,opaque=False):
    """Native rectangle is in y-up coordinates; output is an antialiased mask."""
    groups={g['id']:g for g in data['groups']}
    sx=width/(rect[2]-rect[0]);sy=height/(rect[3]-rect[1])
    def point(x,y):return fitz.Point((x-rect[0])*sx,(rect[3]-y)*sy)
    doc=fitz.open();page=doc.new_page(width=width,height=height)
    batches=[];compound={}
    for p in paths:
        gid=p['parent_id'];cid=None
        while gid:
            if groups[gid]['kind']=='compound':cid=gid;break
            gid=groups[gid]['parent_id']
        if cid:
            if cid not in compound:
                batch=[];compound[cid]=batch;batches.append(batch)
            compound[cid].append(p)
        else:batches.append([p])
    for batch in batches:
        shape=page.new_shape();has=False;current=None;start=None
        for p in batch:
            if p['is_mesh']:
                b=p['visible_bbox'];shape.draw_rect(fitz.Rect(point(b[0],b[3]),point(b[2],b[1])));has=True;continue
            for c in p.get('commands',[]):
                vals=c['values'];op=c['op'].lower();pts=[point(vals[i],vals[i+1]) for i in range(0,len(vals),2)]
                if op=='m':current=start=pts[0]
                elif op=='l' and current is not None:
                    shape.draw_line(current,pts[0]);current=pts[0];has=True
                elif op=='c' and current is not None:
                    shape.draw_bezier(current,*pts);current=pts[-1];has=True
                elif op=='v' and current is not None:
                    shape.draw_bezier(current,current,*pts);current=pts[-1];has=True
                elif op=='y' and current is not None:
                    shape.draw_bezier(current,pts[0],pts[1],pts[1]);current=pts[-1];has=True
                elif op=='h' and current is not None and start is not None:
                    shape.draw_line(current,start);current=start;has=True
        if not has:continue
        p=batch[-1];paint=p['paint'].lower()
        fill=(1,1,1) if paint in ('f','b') or p['is_mesh'] else None
        color=(1,1,1) if paint in ('s','b') else None
        opacity=1 if opaque else max(b.get('opacity',1) for b in batch)
        shape.finish(fill=fill,color=color,width=max(.05,p['stroke_width']*(sx+sy)/2),
                     even_odd=(len(batch)>1 or p.get('fillrule',0)==1),
                     closePath=paint in ('f','b'),fill_opacity=opacity,stroke_opacity=opacity)
        shape.commit()
    pix=page.get_pixmap(alpha=True)
    result=np.frombuffer(pix.samples,dtype=np.uint8).reshape(pix.height,pix.width,4)[:,:,3].copy()
    doc.close();return result
