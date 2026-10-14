import bpy,json,struct
from pathlib import Path
OUT=Path(__file__).resolve().parent
S=bpy.context.scene
for o in S.objects:
    if not o.animation_data or not o.animation_data.action:continue
    a=o.animation_data.action;slot=o.animation_data.action_slot
    if o.name.startswith(('CTRL_ROOF','CTRL_DRONE_LIFT','Roof gas strut')):group='Roof_OpenClose'
    elif o.name.startswith(('CTRL_ROBOT_RAMP','Ramp support')):group='RobotGarage_OpenClose'
    else:group='CrewEntry_OpenClose'
    track=o.animation_data.nla_tracks.new();track.name=group
    strip=track.strips.new(a.name,1,a);strip.action_slot=slot
    o.animation_data.action=None
S.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for c in bpy.data.collections:
    if c.name.startswith(('90_','99_')):continue
    for o in c.objects:o.select_set(True)
path=OUT/'房车_独立开合动作.glb'
bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_frame_range=True,export_force_sampling=True,export_apply=True,export_extras=True)
data=path.read_bytes();length=struct.unpack_from('<I',data,12)[0];d=json.loads(data[20:20+length])
info=[]
for a in d.get('animations',[]):
    names=sorted(set(d['nodes'][c['target']['node']].get('name','') for c in a['channels']))
    info.append({'name':a.get('name'),'nodes':names})
expected={'Roof_OpenClose','RobotGarage_OpenClose','CrewEntry_OpenClose'}
assert {a['name'] for a in info}==expected,info
for a in info:
    if a['name']=='Roof_OpenClose':assert 'CTRL_ROOF_FRONT' in a['nodes'] and 'CTRL_ROBOT_RAMP' not in a['nodes']
    if a['name']=='RobotGarage_OpenClose':assert 'CTRL_ROBOT_RAMP' in a['nodes'] and 'CTRL_ROOF_FRONT' not in a['nodes']
(OUT/'independent_animation_report.json').write_text(json.dumps(info,indent=2),encoding='utf-8')
print('INDEPENDENT_CLIPS_OK',info,flush=True)
