import bpy,json,hashlib
from pathlib import Path
OUT=Path(__file__).resolve().parent
S=bpy.context.scene
S['nominal_envelope_m']='Approximately 9.5 L x 3.2 W including mirrors x 4.66 H including mast; nominal habitat body width 2.5'
txt=bpy.data.texts.get('使用说明') or bpy.data.texts.new('使用说明')
txt.clear();txt.write((OUT/'使用说明.md').read_text(encoding='utf-8'));txt.use_fake_user=True
S.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'智能农田房车_精细可开合_v2.blend'),compress=True)
files=['智能农田房车_精细可开合_v2.blend','智能农田房车_精细可开合_v2.glb','智能农田房车_精细可开合_v2.fbx','房车_独立开合动作.glb','房车开合演示.mp4']
manifest={'method':'local Blender 5.2.1','cloud_generation_tasks':[],'references_packed':sum(bool(i.packed_file) for i in bpy.data.images),'files':[]}
for name in files:
    p=OUT/name;manifest['files'].append({'name':name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(OUT/'delivery_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('DELIVERY_FILES_VERIFIED',len(files),manifest['references_packed'],flush=True)
