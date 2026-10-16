#!/usr/bin/env python3
"""Validate the final manifest and copy its PNG files to Assets/UI as sprites."""
from __future__ import annotations
import argparse,hashlib,json,os,re,struct,sys,tempfile,uuid
from pathlib import Path,PurePosixPath
CATEGORIES={"点击按钮","文本框与数据面板","图标","圆形仪表","边框与装饰"}
OWNER="engineer_ui_sync_v1"
TEMPLATE="""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {max_size}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

def sha(data):return hashlib.sha256(data).hexdigest()
def inside(path,base):
    if not path.resolve().is_relative_to(base.resolve()):raise ValueError(f"路径越出允许目录：{path}")
def png_size(data,path):
    if len(data)<33 or data[:16]!=b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR":raise ValueError(f"PNG 文件头无效：{path}")
    width,height=struct.unpack(">II",data[16:24])
    if not width or not height or max(width,height)>16384:raise ValueError(f"PNG 尺寸不支持：{path} ({width}x{height})")
    return width,height
def field(text,name,value):
    text,count=re.subn(rf"^([ ]+{re.escape(name)}:)[^\n]*$",lambda m:f"{m[1]} {value}",text,flags=re.MULTILINE)
    if not count:raise ValueError(f"现有 .meta 缺少 {name}，停止以保护原文件")
    return text
def meta_bytes(existing,max_size):
    if existing is None:return TEMPLATE.format(guid=uuid.uuid4().hex,sprite_id=uuid.uuid4().hex,max_size=max_size).encode()
    text=existing.decode("utf-8-sig").replace("\r\n","\n")
    if not re.search(r"^guid: [0-9a-fA-F]{32}$",text,re.MULTILINE) or "\nTextureImporter:\n" not in text:raise ValueError("现有 .meta 不是有效的 TextureImporter")
    for key,value in {"enableMipMap":0,"sRGBTexture":1,"filterMode":1,"wrapU":1,"wrapV":1,"wrapW":1,"nPOTScale":0,"spriteMode":1,"spriteMeshType":0,"alphaUsage":1,"alphaIsTransparency":1,"textureType":8,"maxTextureSize":max_size,"textureCompression":0,"crunchedCompression":0}.items():text=field(text,key,value)
    text=re.sub(r"^(    textureFormat:)[^\n]*$",r"\1 -1",text,flags=re.MULTILINE)
    return text.encode()
def atomic(path,data):
    path.parent.mkdir(parents=True,exist_ok=True);temporary=None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent,prefix=".ui-sync-",delete=False) as stream:temporary=Path(stream.name);stream.write(data)
        os.replace(temporary,path)
    finally:
        if temporary and temporary.exists():temporary.unlink()

def sync(workspace,dry_run=False):
    source_root=workspace/'UI';target_root=workspace/'Assets/UI';manifest=json.loads((source_root/'asset_manifest.json').read_text(encoding='utf-8-sig'))
    ledger_file=target_root/'.ui_sync_manifest.json';ledger={"generated_by":OWNER,"assets":{}}
    if ledger_file.exists():
        ledger=json.loads(ledger_file.read_text(encoding='utf-8-sig'))
        if ledger.get('generated_by')!=OWNER:raise ValueError("目标同步记录不属于此工具")
    plans=[];conflicts=[];seen=set();counts={}
    for item in manifest.get('assets',[]):
        rel=PurePosixPath(item['path'].replace('\\','/'))
        if rel.is_absolute() or len(rel.parts)!=2 or rel.suffix.lower()!='.png' or rel.parts[0] not in CATEGORIES or any(x in ('..','.') or ':' in x for x in rel.parts):raise ValueError(f"清单路径无效：{item['path']}")
        key=rel.as_posix();folded=key.casefold()
        if folded in seen:raise ValueError(f"清单重复路径：{key}")
        seen.add(folded);source=source_root/Path(*rel.parts);target=target_root/Path(*rel.parts);meta=Path(str(target)+'.meta')
        inside(source,source_root);inside(target,target_root)
        data=source.read_bytes();checksum=sha(data);width,height=png_size(data,source)
        if item.get('sha256','').lower()!=checksum or item.get('width')!=width or item.get('height')!=height:raise ValueError(f"PNG 与清单不一致：{key}")
        prior=target.read_bytes() if target.exists() else None;owned=ledger['assets'].get(key)
        if prior is not None and prior!=data and (not owned or owned.get('sha256')!=sha(prior)):conflicts.append(key);continue
        max_size=max(32,1<<(max(width,height)-1).bit_length());old_meta=meta.read_bytes() if meta.exists() else None
        plans.append((target,data,meta,meta_bytes(old_meta,max_size)));counts[rel.parts[0]]=counts.get(rel.parts[0],0)+1;ledger['assets'][key]={"sha256":checksum}
    if conflicts:raise ValueError("Assets/UI 中有非本工具修改的冲突文件："+", ".join(conflicts[:10]))
    if not dry_run:
        for target,data,meta,meta_data in plans:
            if not meta.exists() or meta.read_bytes()!=meta_data:atomic(meta,meta_data)
            if not target.exists() or target.read_bytes()!=data:atomic(target,data)
        atomic(ledger_file,(json.dumps(ledger,ensure_ascii=False,indent=2,sort_keys=True)+'\n').encode())
    result={"dry_run":dry_run,"destination":str(target_root),"asset_count":len(plans),"categories":counts,"written":not dry_run}
    print(json.dumps(result,ensure_ascii=False,indent=2));return result
def main():
    p=argparse.ArgumentParser();p.add_argument('--workspace',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--dry-run',action='store_true');a=p.parse_args()
    try:sync(a.workspace.resolve(),a.dry_run)
    except (OSError,ValueError,KeyError,TypeError) as error:print(json.dumps({"error":str(error)},ensure_ascii=False),file=sys.stderr);return 1
    return 0
if __name__=='__main__':raise SystemExit(main())
