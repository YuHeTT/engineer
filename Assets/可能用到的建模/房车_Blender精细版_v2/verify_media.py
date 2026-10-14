import bpy,json
from pathlib import Path
OUT=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
S=bpy.context.scene
seq=S.sequence_editor_create()
strip=seq.strips.new_movie('Final motion check',str(OUT/'房车开合演示.mp4'),channel=1,frame_start=1)
info={'width':strip.elements[0].orig_width,'height':strip.elements[0].orig_height,'frames':strip.frame_final_duration,'fps':strip.fps}
assert info['frames']>=80 and info['width']==960 and info['height']==640
S.render.resolution_x=960;S.render.resolution_y=640;S.render.resolution_percentage=100
S.render.use_sequencer=True;S.render.image_settings.file_format='PNG';S.render.fps=15
for f in [1,45,89]:
    S.frame_set(f);S.render.filepath=str(OUT/'renders'/('video_check_%02d.png'%f));bpy.ops.render.render(write_still=True)
info['duration_seconds']=info['frames']/info['fps']
(OUT/'video_report.json').write_text(json.dumps(info,indent=2),encoding='utf-8')
print('VIDEO_DECODE_OK',info,flush=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps=30
bpy.ops.import_scene.gltf(filepath=str(OUT/'房车_独立开合动作.glb'))
S=bpy.context.scene
controls=['CTRL_ROOF_FRONT','CTRL_ROOF_REAR','CTRL_ROBOT_RAMP']
details={}
for mode in ['Roof_OpenClose','RobotGarage_OpenClose']:
    for o in S.objects:
        if not o.animation_data:continue
        o.animation_data.action=None
        for t in o.animation_data.nla_tracks:t.mute=(t.name!=mode)
    poses=[]
    for f in [1,85,180]:
        S.frame_set(f);bpy.context.view_layer.update()
        poses.append({n:tuple(v for row in bpy.data.objects[n].matrix_world for v in row) for n in controls})
    changed={n:any(abs(a-b)>1e-4 for a,b in zip(poses[0][n],poses[1][n])) for n in controls}
    expected={'CTRL_ROOF_FRONT':mode=='Roof_OpenClose','CTRL_ROOF_REAR':mode=='Roof_OpenClose','CTRL_ROBOT_RAMP':mode=='RobotGarage_OpenClose'}
    assert changed==expected,(mode,changed,[(o.name,[t.name for t in o.animation_data.nla_tracks]) for o in S.objects if o.animation_data])
    assert all(all(abs(a-b)<1e-4 for a,b in zip(poses[0][n],poses[2][n])) for n in controls)
    details[mode]={'changed_controls':changed,'reclosed':True}
(OUT/'independent_roundtrip_report.json').write_text(json.dumps(details,indent=2),encoding='utf-8')
print('INDEPENDENT_PLAYBACK_OK',details,flush=True)
