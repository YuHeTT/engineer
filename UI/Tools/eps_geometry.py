"""Decode EPS Illustrator groups and obtain conservative UI crop rectangles."""
import argparse, base64, json, math, re, zlib
from pathlib import Path
N=r"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?"
NUM=re.compile(N)
PATH=re.compile(rf"^\s*((?:{N}\s+)+)([mlLcCvVyY])\s*$")

def decode_native_data(raw):
    match=re.search(rb"%AI9_DataStream\s+(.*?)%AI9_PrivateDataEnd",raw,re.S)
    if not match: raise ValueError("No Illustrator AI9 private stream")
    data=re.sub(rb"(?m)^%",b"",match[1].replace(b"\r",b"\n"))
    data=re.sub(rb"\s+",b"",data)
    if data.endswith(b"~>"): data=data[:-2]
    return zlib.decompress(base64.a85decode(data)).decode("latin1").replace("\r\n","\n").replace("\r","\n")

def _bbox(text):
    match=re.search(rf"^%%HiResBoundingBox:\s+({N})\s+({N})\s+({N})\s+({N})",text,re.M)
    if not match: match=re.search(rf"^%%BoundingBox:\s+({N})\s+({N})\s+({N})\s+({N})",text,re.M)
    if not match: raise ValueError("No numeric bounding box")
    return list(map(float,match.groups()))

def _union(boxes):
    boxes=[b for b in boxes if b is not None]
    return None if not boxes else [min(b[0] for b in boxes),min(b[1] for b in boxes),max(b[2] for b in boxes),max(b[3] for b in boxes)]

def _intersect(a,b):
    if a is None or b is None:return None
    c=[max(a[0],b[0]),max(a[1],b[1]),min(a[2],b[2]),min(a[3],b[3])]
    return c if c[2]>=c[0] and c[3]>=c[1] else None

def _area(b):
    return 0 if b is None else max(0,b[2]-b[0])*max(0,b[3]-b[1])

def native_to_page(box, native_bbox, width=None, height=None):
    """[xmin,ymin,xmax,ymax] native -> [left,top,right,bottom] points/pixels."""
    if box is None:return None
    sx=1 if width is None else width/(native_bbox[2]-native_bbox[0])
    sy=1 if height is None else height/(native_bbox[3]-native_bbox[1])
    return [(box[0]-native_bbox[0])*sx,(native_bbox[3]-box[3])*sy,(box[2]-native_bbox[0])*sx,(native_bbox[3]-box[1])*sy]

def _curve_box(points):
    values=[points[0],points[-1]]
    for axis in (0,1):
        p0,p1,p2,p3=[p[axis] for p in points]
        a=-p0+3*p1-3*p2+p3; b=2*(p0-2*p1+p2); c=p1-p0
        if abs(a)<1e-12: roots=[] if abs(b)<1e-12 else [-c/b]
        else:
            disc=b*b-4*a*c
            roots=[] if disc<0 else [(-b+math.sqrt(disc))/(2*a),(-b-math.sqrt(disc))/(2*a)]
        for t in roots:
            if 0<t<1:
                u=1-t
                values.append(tuple(u*u*u*points[0][k]+3*u*u*t*points[1][k]+3*u*t*t*points[2][k]+t*t*t*points[3][k] for k in (0,1)))
    return [min(p[0] for p in values),min(p[1] for p in values),max(p[0] for p in values),max(p[1] for p in values)]

def inspect_eps(path, include_commands=False):
    path=Path(path); raw=path.read_bytes(); native=decode_native_data(raw)
    nb=_bbox(native); eb=_bbox(raw.decode("latin1").replace("\r","\n"))
    width,height=eb[2]-eb[0],eb[3]-eb[1]
    body=native.split("%%EndSetup",1)[1].split("showpage",1)[0]
    groups=[]; paths=[]; byid={}; warnings=[]
    stem=re.sub(r"\W+","_",path.stem).strip("_") or "eps"
    def group(kind,parent,line):
        gid=f"{stem}_g{len(groups):04d}"
        g={"id":gid,"kind":kind,"parent_id":parent,"children":[],"path_ids":[],"text_hints":[],"line_start":line}
        groups.append(g);byid[gid]=g
        if parent is not None:byid[parent]["children"].append(gid)
        return gid
    root=group("document",None,0);stack=[root]; active_clip=None
    box=None; point=None; start=None; commands=[]; clipping=False
    stroke_width=1.0; fill=None; saved=[]; mesh=None; path_start=0; opacity=1.0; fillrule=0
    def record(paint,line,mesh_box=None):
        nonlocal box,point,start,commands,clipping,active_clip
        b=mesh_box if mesh_box is not None else box
        if b is not None:
            is_painted=paint not in {"n","N"} and not clipping
            is_stroked=paint in {"s","S","b","B"}
            vb=[b[0]-stroke_width/2,b[1]-stroke_width/2,b[2]+stroke_width/2,b[3]+stroke_width/2] if is_stroked else b
            if active_clip is not None:vb=_intersect(vb,active_clip)
            if not is_painted:vb=None
            pid=f"{stem}_p{len(paths):05d}"
            p={"id":pid,"parent_id":stack[-1],"bbox":b,"visible_bbox":vb,"paint":paint,"painted":is_painted,
               "is_clip":clipping,"is_mesh":mesh_box is not None,"fill":fill,"stroke_width":stroke_width if is_stroked else 0,
               "clip_bbox":active_clip,"line_start":path_start,"line_end":line,"opacity":opacity,"fillrule":fillrule}
            if include_commands:p["commands"]=commands[:]
            paths.append(p);byid[stack[-1]]["path_ids"].append(pid)
            if clipping:
                active_clip=b if active_clip is None else _intersect(active_clip,b)
                byid[stack[-1]]["clip_bbox"]=active_clip
        box=None;point=None;start=None;commands=[];clipping=False
    for lineno,rawline in enumerate(body.splitlines(),1):
        line=rawline.strip()
        if not line:continue
        if line.startswith("%AI5_BeginLayer"):
            stack.append(group("layer",stack[-1],lineno));continue
        if line.startswith("%AI5_EndLayer") or line=="LB":
            if byid[stack[-1]]["kind"]=="layer":byid[stack.pop()]["line_end"]=lineno
            continue
        if line.startswith("%"):
            if mesh is not None and line.endswith("/N X#"):
                nums=list(map(float,NUM.findall(line[2:])))
                offset=len(nums)-8
                if offset>=0:mesh += [nums[offset:offset+2],nums[offset+2:offset+4],nums[offset+5:offset+7]]
            elif mesh is not None and (line.endswith("/S X#") or line.endswith("/A X#")):
                nums=list(map(float,NUM.findall(line[2:])))
                mesh += [nums[i:i+2] for i in range(0,min(6,len(nums)),2)]
            continue
        if line in {"u","*u","q"}:
            gid=group({"u":"group","*u":"compound","q":"clip_scope"}[line],stack[-1],lineno)
            if line=="q":saved.append((gid,active_clip,stroke_width,fill,opacity,fillrule))
            stack.append(gid);continue
        if line in {"U","*U","Q"}:
            expect={"U":"group","*U":"compound","Q":"clip_scope"}[line]
            if len(stack)>1 and byid[stack[-1]]["kind"]==expect:
                gid=stack.pop();byid[gid]["line_end"]=lineno
                if line=="Q" and saved:
                    _,active_clip,stroke_width,fill,opacity,fillrule=saved.pop()
            else:warnings.append(f"Unexpected {line} at body line {lineno}")
            continue
        if line.endswith(" Ln"):
            match=re.match(r"\((.*)\)\s+Ln$",line)
            if match:byid[stack[-1]]["name"]=match[1]
            continue
        if line.endswith(" Lb"):byid[stack[-1]]["layer_flags"]=line[:-3];continue
        if line=="/Mesh X!":mesh=[];path_start=lineno;continue
        if line=="/End X!" and mesh is not None:
            mb=_union([[p[0],p[1],p[0],p[1]] for p in mesh if len(p)==2])
            record("f",lineno,mb);mesh=None;continue
        match=PATH.match(line)
        if match:
            nums=list(map(float,NUM.findall(match[1])));op=match[2].lower()
            pts=list(zip(nums[::2],nums[1::2]));segment=None
            if op=="m" and len(pts)==1:
                if box is None:path_start=lineno
                point=start=pts[0];segment=[*point,*point]
            elif op=="l" and len(pts)==1:
                segment=_union([[*p,*p] for p in (point,pts[0]) if p is not None]);point=pts[0]
            elif op=="c" and len(pts)==3 and point is not None:segment=_curve_box([point,*pts]);point=pts[-1]
            elif op=="v" and len(pts)==2 and point is not None:segment=_curve_box([point,point,*pts]);point=pts[-1]
            elif op=="y" and len(pts)==2 and point is not None:segment=_curve_box([point,pts[0],pts[1],pts[1]]);point=pts[-1]
            box=_union([box,segment])
            if include_commands:commands.append({"op":match[2],"values":nums})
            continue
        if line in {"h","H"}:
            point=start
            if include_commands:commands.append({"op":"h","values":[]})
            continue
        if line in {"W","W*"}:clipping=True;continue
        if line in {"f","F","s","S","b","B","n","N"}:record(line,lineno);continue
        parts=line.split();last=parts[-1]
        if last=="Xy":
            vals=NUM.findall(line)
            if len(vals)>=2:opacity=max(0,min(1,float(vals[1])))
        if last=="XR":
            vals=NUM.findall(line)
            if vals:fillrule=int(float(vals[0]))
        if last in {"g","k","Xa","x"}:
            try:nums=list(map(float,parts[:-1]))
            except ValueError:nums=[]
            if nums:fill={"operator":last,"values":nums,"rgb":nums[-3:] if last=="Xa" and len(nums)>=7 else None}
        sw=re.search(rf"({N})\s+w(?:\s|$)",line)
        if sw:stroke_width=float(sw[1])
        if last in {"Tj","Tx","TJ"}:byid[stack[-1]]["text_hints"]+=re.findall(r"\(((?:\\.|[^\\)])*)\)",line)
    pb={p["id"]:p for p in paths}
    def finish(gid):
        g=byid[gid]
        for child in g["children"]:finish(child)
        nodes=[pb[x] for x in g["path_ids"]]+[byid[x] for x in g["children"]]
        g["bbox"]=_union([x["bbox"] for x in nodes]);g["visible_bbox"]=_union([x["visible_bbox"] for x in nodes])
        g["child_count"]=len(g["children"]);g["direct_path_count"]=len(g["path_ids"])
        g["path_count"]=len(g["path_ids"])+sum(byid[x]["path_count"] for x in g["children"])
        g["text_hints"]=list(dict.fromkeys(g["text_hints"]+[t for x in g["children"] for t in byid[x]["text_hints"]]))
        g["page_rect"]=native_to_page(g["visible_bbox"],nb,width,height)
    finish(root)
    if len(stack)>1:warnings.append(f"Unclosed group count {len(stack)-1}")
    for p in paths:
        p["page_rect"]=native_to_page(p["visible_bbox"],nb,width,height)
        p["possible_background"]=p["painted"] and not p["is_mesh"] and p["paint"] in {"f","F"} and p["bbox"] is not None and all(abs(a-b)<max(0.1,(nb[2]-nb[0] if i%2==0 else nb[3]-nb[1])*0.025) for i,(a,b) in enumerate(zip(p["bbox"],nb)))
    result={"source":str(path.resolve()),"native_bbox":nb,"eps_bbox":eb,"page_size":[width,height],"root_id":root,"groups":groups,"paths":paths,"warnings":warnings,"transform":{"x_offset":-nb[0],"y_top":nb[3],"scale_x":width/(nb[2]-nb[0]),"scale_y":height/(nb[3]-nb[1])}}
    result["crop_groups"]=select_crop_groups(result)
    return result

def select_crop_groups(data):
    """Respect widgets; descend groups covering most of a sheet."""
    byid={g["id"]:g for g in data["groups"]};canvas=_area(data["native_bbox"]);out=[]
    def visit(gid):
        g=byid[gid];children=[byid[x] for x in g["children"] if byid[x]["visible_bbox"] is not None]
        if g["kind"] in {"document","layer"}:
            for c in children:visit(c["id"])
            return
        if g["kind"]=="group" and children:
            if len(children)==1 and not g["direct_path_count"]:visit(children[0]["id"]);return
            if _area(g["visible_bbox"])>canvas*.55 and len(children)>=3 and sum(_area(c["visible_bbox"])<canvas*.35 for c in children)>=len(children)*.65:
                for c in children:visit(c["id"])
                return
        rect=_intersect(g["visible_bbox"],data["native_bbox"])
        if rect is not None and _area(rect)>0:out.append({"id":gid,"kind":g["kind"],"bbox":rect,"page_rect":native_to_page(rect,data["native_bbox"],*data["page_size"]),"source_group_id":gid,"path_count":g["path_count"],"child_count":g["child_count"],"text_hints":g["text_hints"]})
    visit(data["root_id"]);out.sort(key=lambda x:(round(x["page_rect"][1],2),round(x["page_rect"][0],2)))
    for i,g in enumerate(out,1):g["reading_order"]=i
    return out

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument("eps",type=Path);parser.add_argument("--include-commands",action="store_true")
    args=parser.parse_args();data=inspect_eps(args.eps,args.include_commands);print(json.dumps(data,ensure_ascii=False,indent=2))
if __name__=="__main__":main()
