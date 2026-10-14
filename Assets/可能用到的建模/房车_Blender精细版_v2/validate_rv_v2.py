import bpy, math, json, struct
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

OUT=Path(__file__).resolve().parent
S=bpy.context.scene
checks=[]; failures=[]
def check(name,ok,detail=None):
    checks.append({'check':name,'passed':bool(ok),'detail':detail})
    if not ok:failures.append(name)
def bbox(o):
    vs=[o.matrix_world@Vector(v) for v in o.bound_box]
    return [min(v[i] for v in vs) for i in range(3)],[max(v[i] for v in vs) for i in range(3)]
def allprefix(prefixes,col=None):
    obs=bpy.data.collections[col].objects if col else S.objects
    return [o for o in obs if o.type=='MESH' and any(o.name.startswith(p) for p in prefixes)]
check('Six tire bodies',len(allprefix(['TIRE_']))==6)
check('Two solar modules',len(allprefix(['Solar module']))==2)
check('Seven packed reference images',sum(bool(i.packed_file) for i in bpy.data.images)==7)
for name in ['CTRL_ROOF_FRONT','CTRL_ROOF_REAR','CTRL_ROBOT_RAMP','CTRL_DRONE_LIFT','CTRL_CREW_DOOR','CTRL_CREW_STEPS']:
    check(name+' has animation',bpy.data.objects.get(name) is not None and bool(bpy.data.objects[name].animation_data))
states=[]
for f,front,rear,ramp,lift in [(1,0,0,0,3.237),(55,103,-103,125,3.237),(85,103,-103,125,3.557),(160,0,0,0,3.237),(180,0,0,0,3.237)]:
    S.frame_set(f);bpy.context.view_layer.update()
    got=[math.degrees(bpy.data.objects['CTRL_ROOF_FRONT'].rotation_euler.y),math.degrees(bpy.data.objects['CTRL_ROOF_REAR'].rotation_euler.y),math.degrees(bpy.data.objects['CTRL_ROBOT_RAMP'].rotation_euler.x),bpy.data.objects['CTRL_DRONE_LIFT'].location.z]
    check('Pose at frame '+str(f),all(abs(a-b)<.002 for a,b in zip(got,[front,rear,ramp,lift])),got);states.append({'frame':f,'values':got})
S.frame_set(85);bpy.context.view_layer.update()
lo,hi=bbox(bpy.data.objects['ROBOT_RAMP_outer_door'])
check('Ramp near-ground but above ground',lo[2]>=-.001 and lo[2]<.06,lo[2])
# Full-resolution evaluated mesh surface collision checks; hinges intentionally excluded.
roof_moving=allprefix(['HATCH_FRONT_panel','HATCH_REAR_panel','HATCH_FRONT_inner','HATCH_REAR_inner'])
roof_fixed=allprefix(['Roof panel','Roof bay side bridge','Drone bay sidewall','Drone bay endwall','Solar module','Solar aluminium','Roof equipment rail'])
drone=list(o for o in bpy.data.collections['11_Drone'].objects if o.type=='MESH')
ramp_moving=allprefix(['ROBOT_RAMP_outer_door','ROBOT_RAMP_inner_deck','Ramp rim'])
garage_fixed=allprefix(['Garage floor','Garage sealed back','Garage ceiling','Garage partition','Garage jamb','Garage upper jamb','Cabin main floor','Cabin aft','TIRE_','Contoured wheel fender','Ladder frame','Passenger-side ladder','Driver-side ladder'])
rover=list(o for o in bpy.data.collections['12_Tracked_Robot'].objects if o.type=='MESH')
supports=allprefix(['Roof gas strut'])
platform=allprefix(['Drone elevator platform'])
collisions=[]
for f in range(1,181):
    S.frame_set(f);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();cache={}
    def geo(o):
        if o.name in cache:return cache[o.name]
        e=o.evaluated_get(dg);me=e.to_mesh();vs=[e.matrix_world@v.co for v in me.vertices]
        lo=[min(v[i] for v in vs) for i in range(3)];hi=[max(v[i] for v in vs) for i in range(3)]
        tree=BVHTree.FromPolygons(vs,[tuple(p.vertices) for p in me.polygons],epsilon=0)
        e.to_mesh_clear();cache[o.name]=(lo,hi,tree);return cache[o.name]
    def collide(a,b):
        la,ha,ta=geo(a);lb,hb,tb=geo(b)
        if any(ha[i]<lb[i]+.0005 or hb[i]<la[i]+.0005 for i in range(3)):return False
        return bool(ta.overlap(tb))
    for group,A,B in [('roof_to_fixed',roof_moving,roof_fixed),('roof_to_drone',roof_moving,drone),('ramp_to_fixed',ramp_moving,garage_fixed),('ramp_to_rover',ramp_moving,rover),('platform_to_supports',platform,supports)]:
        for a in A:
            for b in B:
                if collide(a,b):collisions.append({'frame':f,'group':group,'moving':a.name,'other':b.name})
    if f in [1,55,85,160]:print('COLLISION_SAMPLE',f,len(collisions),flush=True)
check('Sampled core motion has no surface intersections',not collisions,collisions)

# Inspect actual GLB container, geometry and animation records.
g=OUT/'智能农田房车_精细可开合_v2.glb'
data=g.read_bytes();magic,version,length=struct.unpack_from('<4sII',data,0)
check('GLB binary header',magic==b'glTF' and version==2 and length==len(data))
jl,jtype=struct.unpack_from('<II',data,12);doc=json.loads(data[20:20+jl])
names={n.get('name','') for n in doc.get('nodes',[])}
check('GLB independent hatch nodes',all(n in names for n in ['CTRL_ROOF_FRONT','CTRL_ROOF_REAR','CTRL_ROBOT_RAMP']))
anim=doc.get('animations',[])
targets={doc['nodes'][c['target']['node']].get('name') for a in anim for c in a['channels']}
check('GLB hatch animation channels',all(n in targets for n in ['CTRL_ROOF_FRONT','CTRL_ROOF_REAR','CTRL_ROBOT_RAMP']))
check('GLB materials embedded',len(doc.get('materials',[]))>=15 and all('uri' not in b for b in doc.get('buffers',[])))
report={'checks':checks,'failures':failures,'collision_count':len(collisions),'states':states,'glb':{'bytes':len(data),'nodes':len(names),'animations':len(anim),'animated_nodes':len(targets)},'scope':'Sampled mesh surface intersections for hatch panels/ramp against selected fixed geometry, drone, rover and lift support rods; not manufacturing certification.'}
(OUT/'validation_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('VALIDATION_RESULT',json.dumps({'failures':failures,'collision_count':len(collisions),'glb':report['glb']}),flush=True)
if failures:print('COLLISIONS',json.dumps(collisions[:30]),flush=True)
