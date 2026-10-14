import bpy,json,math,struct
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps=30
bpy.ops.import_scene.gltf(filepath=str(OUT/'智能农田房车_精细可开合_v2.glb'))
S=bpy.context.scene
names=['CTRL_ROOF_FRONT','CTRL_ROOF_REAR','CTRL_ROBOT_RAMP']
report={'glb_imported_objects':len(S.objects),'control_nodes':{},'poses':[]}
for name in names:
    o=bpy.data.objects.get(name);assert o is not None,name
    report['control_nodes'][name]={'animation':bool(o.animation_data),'tracks':len(o.animation_data.nla_tracks) if o.animation_data else 0}
for frame in [1,55,85,160,180]:
    S.frame_set(frame);bpy.context.view_layer.update()
    transforms={n:[round(v,5) for row in bpy.data.objects[n].matrix_world for v in row] for n in names}
    report['poses'].append({'frame':frame,'transforms':transforms})
for n in names:
    assert report['poses'][0]['transforms'][n]!=report['poses'][1]['transforms'][n],n+' static after import'
    assert all(abs(a-b)<1e-4 for a,b in zip(report['poses'][0]['transforms'][n],report['poses'][-1]['transforms'][n])),n+' does not reclose'
report['glb_cycle_verified']=True
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'智能农田房车_精细可开合_v2.fbx'))
report['fbx_imported_objects']=len(bpy.context.scene.objects)
report['fbx_controls_present']=all(bpy.data.objects.get(n) is not None for n in names)
report['fbx_animation_present']=all(bool(bpy.data.objects[n].animation_data) for n in names)
assert report['fbx_controls_present'] and report['fbx_animation_present']
(OUT/'roundtrip_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('EXPORT_ROUNDTRIP_OK',report['glb_imported_objects'],report['fbx_imported_objects'],flush=True)
