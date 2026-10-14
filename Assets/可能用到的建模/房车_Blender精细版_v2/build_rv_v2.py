"""Reference-led, independently editable RV model; run inside Blender 5.2."""
import bpy, math, json, sys, time
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

OUT=Path(__file__).resolve().parent
(OUT/'renders').mkdir(exist_ok=True)
START=time.time()
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection': bpy.data.collections.remove(c)
S=bpy.context.scene
S.name='AGRI_RV_Deployable'
S.unit_settings.system='METRIC'; S.unit_settings.scale_length=1
S.frame_start=1; S.frame_end=180; S.render.fps=30
S.render.engine='BLENDER_EEVEE'
S.render.resolution_x=1800; S.render.resolution_y=1200; S.render.resolution_percentage=100
S.render.film_transparent=True
S.render.image_settings.file_format='PNG'; S.render.image_settings.color_mode='RGBA'
S.view_settings.view_transform='AgX'
S.world.use_nodes=True; S.world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.26,.31,1)
S.world.node_tree.nodes['Background'].inputs[1].default_value=.4
COL={}
for n in ['00_Controls','01_Chassis','02_Wheels','03_Cab','04_Habitat_Walls','05_Roof_Static','06_Roof_Hatches','07_Robot_Garage','08_Crew_Entry','09_Interior','10_Exterior_Hardware','11_Drone','12_Tracked_Robot','90_Cameras_Lights','99_Reference_Images']:
    c=bpy.data.collections.new(n); S.collection.children.link(c); COL[n]=c

def empty(n,loc=(0,0,0),col='00_Controls',parent=None):
    o=bpy.data.objects.new(n,None); COL[col].objects.link(o); o.location=loc; o.parent=parent
    o.empty_display_type='PLAIN_AXES'; o.empty_display_size=.15
    return o
ROOT=empty('RV_ROOT__meters__front_X__passenger_minusY')

def mat(n,color,metal=0,rough=.45,emission=0,alpha=1):
    m=bpy.data.materials.new(n); m.use_nodes=True; m.diffuse_color=(*color,alpha)
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,alpha)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    if emission: p.inputs['Emission Color'].default_value=(*color,1); p.inputs['Emission Strength'].default_value=emission
    return m
WHITE=mat('Paint • porcelain white',(.75,.78,.78),.25,.32)
BLUE=mat('Paint • marine blue',(.018,.095,.23),.4,.29)
DARK=mat('Powder coat • graphite',(.013,.019,.024),.3,.50)
BLACK=mat('Polymer • charcoal',(.009,.012,.015),0,.53)
RUBBER=mat('Tires • natural rubber',(.006,.007,.009),0,.88)
STEEL=mat('Stainless • brushed',(.43,.49,.52),.82,.28)
ALU=mat('Aluminium • machined',(.58,.63,.65),.78,.3)
GLASS=mat('Tinted laminated glass',(.028,.074,.091),.3,.13)
SOLAR=mat('Photovoltaic • blue cells',(.008,.035,.095),.25,.42)
FLOOR=mat('Anti slip rubber floor',(.055,.067,.077),.05,.76)
SEAT=mat('Seat fabric',(.03,.038,.045),0,.9)
LED=mat('Work lights',(.79,.9,1),.15,.2,4)
WARM=mat('Cabin LED • warm white',(1,.78,.42),0,.3,4)
AMBER=mat('Amber markers',(1,.25,.01),.05,.25,2)
RED=mat('Rear lamps',(.6,.008,.006),.1,.22,1.5)
GREEN=mat('Navigation green',(.02,.8,.18),0,.22,3)
SCREEN=mat('LCD navy',(.006,.022,.04),.1,.2,.3)
CYAN=mat('LCD cyan',(.03,.5,.7),0,.35,1)
GOLD=mat('Safety yellow',(.93,.52,.035),.2,.4)
for m,scale,strength in [(RUBBER,75,.08),(WHITE,180,.012),(STEEL,110,.025),(FLOOR,65,.12),(SEAT,150,.09)]:
    nd=m.node_tree.nodes; lk=m.node_tree.links
    tex=nd.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=scale; tex.inputs['Detail'].default_value=2
    bump=nd.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=strength; bump.inputs['Distance'].default_value=.025
    lk.new(tex.outputs['Fac'],bump.inputs['Height']); lk.new(bump.outputs['Normal'],nd.get('Principled BSDF').inputs['Normal'])

def mesh(n,verts,faces,ma,col,parent=None,bevel=0):
    d=bpy.data.meshes.new(n); d.from_pydata(verts,[],faces); d.update()
    o=bpy.data.objects.new(n,d); COL[col].objects.link(o); o.parent=parent or ROOT
    if ma: d.materials.append(ma)
    if bevel:
        b=o.modifiers.new('Manufactured edge radii','BEVEL'); b.width=bevel; b.segments=3
        o.modifiers.new('Face normals','WEIGHTED_NORMAL')
    return o
CUBE_F=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
UNIT=[(-.5,-.5,-.5),(.5,-.5,-.5),(.5,.5,-.5),(-.5,.5,-.5),(-.5,-.5,.5),(.5,-.5,.5),(.5,.5,.5),(-.5,.5,.5)]
def box(n,loc,dims,ma,col,bevel=.01,parent=None,rot=None):
    o=mesh(n,[(v[0]*dims[0],v[1]*dims[1],v[2]*dims[2]) for v in UNIT],CUBE_F,ma,col,parent,bevel)
    o.location=loc
    if rot: o.rotation_euler=rot
    return o
def cyl(n,loc,r,depth,ma,col,axis='Z',verts=32,parent=None,r2=None):
    vs=[]
    for z,rr in [(-depth/2,r),(depth/2,r if r2 is None else r2)]:
        for i in range(verts):
            a=2*math.pi*i/verts; vs.append((rr*math.cos(a),rr*math.sin(a),z))
    fs=[tuple(reversed(range(verts))),tuple(range(verts,verts*2))]
    fs += [(i,(i+1)%verts,(i+1)%verts+verts,i+verts) for i in range(verts)]
    o=mesh(n,vs,fs,ma,col,parent); o.location=loc
    if axis=='X': o.rotation_euler[1]=math.pi/2
    if axis=='Y': o.rotation_euler[0]=math.pi/2
    for p in o.data.polygons:
        if len(p.vertices)==4: p.use_smooth=True
    return o
def bar(n,a,b,r,ma,col,parent=None):
    a,b=Vector(a),Vector(b); d=b-a
    o=cyl(n,(a+b)/2,r,d.length,ma,col,verts=16,parent=parent)
    o.rotation_euler=d.to_track_quat('Z','Y').to_euler(); return o
def poly_extrude(n,profile,axis,lo,hi,ma,col,bevel=.01,parent=None):
    def pt(u,v,w):
        return (w,u,v) if axis=='X' else (u,w,v) if axis=='Y' else (u,v,w)
    vs=[pt(u,v,w) for w in (lo,hi) for u,v in profile]; N=len(profile)
    fs=[tuple(reversed(range(N))),tuple(range(N,2*N))]+[(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
    return mesh(n,vs,fs,ma,col,parent,bevel)
def torus(n,loc,major,minor,ma,col,axis='Z',parent=None):
    vs=[]; fs=[]; N=48; M=10
    for i in range(N):
        a=2*math.pi*i/N
        for j in range(M):
            b=2*math.pi*j/M; vs.append(((major+minor*math.cos(b))*math.cos(a),(major+minor*math.cos(b))*math.sin(a),minor*math.sin(b)))
    for i in range(N):
        for j in range(M): fs.append((i*M+j,((i+1)%N)*M+j,((i+1)%N)*M+(j+1)%M,i*M+(j+1)%M))
    o=mesh(n,vs,fs,ma,col,parent); o.location=loc
    if axis=='Y': o.rotation_euler[0]=math.pi/2
    if axis=='X': o.rotation_euler[1]=math.pi/2
    for p in o.data.polygons:p.use_smooth=True
    return o
def multi_boxes(n,specs,ma,col,parent=None):
    vs=[]; fs=[]
    for loc,dims,rot in specs:
        R=Matrix.Identity(3) if rot is None else rot
        off=len(vs); vs += [Vector(loc)+R@Vector((v[0]*dims[0],v[1]*dims[1],v[2]*dims[2])) for v in UNIT]
        fs += [tuple(off+i for i in f) for f in CUBE_F]
    return mesh(n,vs,fs,ma,col,parent,.002)
def bolt(n,loc,axis='Y',col='10_Exterior_Hardware',parent=None,r=.018):
    return cyl(n,loc,r,.012,STEEL,col,axis,6,parent)
def label(n,text,loc,size,col,ma=WHITE,rot=None,parent=None):
    d=bpy.data.curves.new(n,'FONT');d.body=text;d.size=size;d.extrude=.0004;d.align_x='CENTER';d.align_y='CENTER'
    o=bpy.data.objects.new(n,d);COL[col].objects.link(o);o.location=loc;o.parent=parent or ROOT
    if rot:o.rotation_euler=rot
    d.materials.append(ma);return o
def panel(n,x0,x1,z0,z1,y,ma=WHITE,col='10_Exterior_Hardware'):
    o=box(n,((x0+x1)/2,y,(z0+z1)/2),(x1-x0,.036,z1-z0),ma,col,.015)
    sg=-1 if y<0 else 1
    for x in (x0+.07,x1-.07):
        for z in (z0+.065,z1-.065):bolt(n+' screw',(x,y+sg*.025,z),col=col,r=.012)
    return o

# Chassis with real suspension, axles, drive shaft, fuel and utility boxes.
box('Driver-side ladder frame rail',(0,.71,.97),(8.75,.17,.30),DARK,'01_Chassis',.025)
box('Passenger-side ladder frame rail',(1.06,-.71,.97),(6.57,.17,.30),DARK,'01_Chassis',.025)
box('Garage upper load-bearing rail',(-3.32,-.71,1.88),(2.08,.17,.15),DARK,'01_Chassis',.018)
box('Rear offset chassis crossmember',(-4.0,.47,.96),(.13,.64,.17),DARK,'01_Chassis')
for x in [-2.2,-1.05,.65,2.05,3.85]:box('Crossmember',(x,0,.96),(.13,1.55,.17),DARK,'01_Chassis')
AXLES=[-1.50,-.10,3.49]
for x in AXLES:
    bar('Axle tube',(x,-1.13,.60),(x,1.13,.60),.105,DARK,'01_Chassis')
    cyl('Differential',(x,0,.60),.19,.38,DARK,'01_Chassis','Y')
    for y in [-.68,.68]:
        for k in range(4):box('Leaf spring',(x,y,.84+k*.026),(1.02-k*.12,.085,.023),DARK,'01_Chassis',.007)
        bar('Shock piston',(x-.28,y,.58),(x-.13,y,1.16),.024,STEEL,'01_Chassis')
        bar('Shock sleeve',(x-.28,y,.58),(x-.20,y,.90),.048,DARK,'01_Chassis')
bar('Driveshaft',(-1.50,0,.62),(3.4,0,.77),.052,STEEL,'01_Chassis')
for y in [-.83,.83]:
    box('Underfloor battery enclosure',(.64,y,.98),(.78,.55,.55),DARK,'01_Chassis',.055)
    for x in (.31,.65,.97):bar('Underfloor strap',(x,y-.27,.70),(x,y-.27,1.23),.015,STEEL,'01_Chassis')
box('Rear towing crossbar',(-4.60,0,.56),(.20,2.35,.17),DARK,'01_Chassis',.03)
for y in [-.6,.6]:torus('Recovery eye',(-4.74,y,.57),.075,.018,STEEL,'01_Chassis','X')

# Rounded truck tires, distinct rim/hub, multi-row directional tread.
for ai,x in enumerate(AXLES):
    for side in [-1,1]:
        y=side*1.14; w=empty('WHEEL_%d_%s'%(ai,'R' if side<0 else 'L'),(x,y,.61),'02_Wheels',ROOT)
        profile=[(-.215,.34),(-.218,.43),(-.20,.53),(-.15,.583),(-.10,.595),(.10,.595),(.15,.583),(.20,.53),(.218,.43),(.215,.34)]
        vs=[];N=64
        for yy,r in profile:
            vs += [(r*math.sin(i*2*math.pi/N),yy,r*math.cos(i*2*math.pi/N)) for i in range(N)]
        fs=[]
        for j in range(len(profile)-1):
            fs += [(j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i) for i in range(N)]
        o=mesh('TIRE_%d_%d'%(ai,side),vs,fs,RUBBER,'02_Wheels',w)
        for p in o.data.polygons:p.use_smooth=True
        specs=[]
        for i in range(44):
            for row in [-1,0,1]:
                a=2*math.pi*(i+(row%2)*.45)/44
                R=Matrix.Rotation(a,3,'Y')@Matrix.Rotation(row*.32,3,'Z')
                specs.append(((.593*math.sin(a),row*.135,.593*math.cos(a)),(.069,.123,.040),R))
        multi_boxes('Directional tread',specs,RUBBER,'02_Wheels',w)
        outer=side*.218
        cyl('Wheel recessed steel disk',(0,outer,0),.327,.035,DARK,'02_Wheels','Y',48,w)
        torus('Rim machined lip',(0,outer+side*.025,0),.312,.020,STEEL,'02_Wheels','Y',w)
        torus('Tire molded sidewall',(0,outer+side*.005,0),.48,.008,RUBBER,'02_Wheels','Y',w)
        cyl('Hub cap',(0,outer+side*.035,0),.15,.055,DARK,'02_Wheels','Y',32,w)
        for i in range(10):
            a=i*2*math.pi/10
            bolt('Lug nut',(.215*math.sin(a),outer+side*.054,.215*math.cos(a)),'Y','02_Wheels',w,.025)
        for i in range(8):
            a=i*2*math.pi/8
            cyl('Rim ventilation recess',(.266*math.sin(a),outer+side*.023,.266*math.cos(a)),.032,.006,BLACK,'02_Wheels','Y',12,w)
        # Fender wraps the shoulder without touching the tire.
        vv=[];ff=[];K=32
        for yy in [side*1.025,side*1.395]:
            for r in [.673,.745]:
                vv += [(x+r*math.cos(math.pi*i/K),yy,.61+r*math.sin(math.pi*i/K)) for i in range(K+1)]
        for i in range(K):
            ff += [(i,i+1,K+1+i+1,K+1+i),(2*(K+1)+i,3*(K+1)+i,3*(K+1)+i+1,2*(K+1)+i+1),(i,2*(K+1)+i,2*(K+1)+i+1,i+1),(K+1+i,K+1+i+1,3*(K+1)+i+1,3*(K+1)+i)]
        mesh('Contoured wheel fender',vv,ff,DARK,'02_Wheels',bevel=.008)
        box('Rear flexible mudflap',(x-.68,side*1.15,.48),(.045,.45,.43),BLACK,'02_Wheels',.008)

# Habitation body; garage is below a sealed rear raised utility shelf.
box('Cabin main floor',(.01,0,1.33),(4.53,2.38,.14),DARK,'04_Habitat_Walls',.02)
box('Cabin aft driver-side floor',(-3.40,.63,1.33),(2.24,1.10,.14),DARK,'04_Habitat_Walls',.02)
box('Walkable main rubber floor',(.01,0,1.41),(4.53,2.26,.025),FLOOR,'09_Interior')
box('Walkable aft rubber floor',(-3.40,.63,1.41),(2.24,1.10,.025),FLOOR,'09_Interior')
box('Rear wall',(-4.54,0,2.32),(.10,2.46,2.44),WHITE,'04_Habitat_Walls')
box('Driver side wall',(-1.16,1.22,2.46),(6.83,.085,2.18),WHITE,'04_Habitat_Walls')
def wall(n,x0,x1,z0,z1):return box(n,((x0+x1)/2,-1.22,(z0+z1)/2),(x1-x0,.085,z1-z0),WHITE,'04_Habitat_Walls')
wall('Aft corner pillar',-4.52,-4.34,.61,3.55)
wall('Wall over rear garage',-4.34,-2.31,1.82,3.55)
wall('Workzone wall',-2.31,1.05,1.36,3.55)
wall('Front entry pillar',2.03,2.25,1.36,3.55)
wall('Crew door header',1.05,2.03,3.18,3.55)
box('Front bulkhead left',(2.24,.91,2.48),(.10,.62,2.15),WHITE,'04_Habitat_Walls')
box('Front bulkhead right',(2.24,-.91,2.48),(.10,.62,2.15),WHITE,'04_Habitat_Walls')
box('Cab walk-through header',(2.24,0,3.21),(.10,1.24,.54),WHITE,'04_Habitat_Walls')
for y in [-1.27,1.27]:
    box('Corner extrusion top',(-1.16,y,3.48),(6.90,.075,.095),ALU,'10_Exterior_Hardware')
    for x in [-4.56,2.27]:box('Corner extrusion upright',(x,y,2.45),(.07,.075,2.13),ALU,'10_Exterior_Hardware')
    box('Lower extruded rub rail',(-.10,y,1.42),(4.6,.07,.10),DARK,'10_Exterior_Hardware')
    for x in [-4.35,-2.25,-.25,1.87]:
        box('Body fastener plate',(x,y,3.40),(.13,.035,.13),DARK,'10_Exterior_Hardware',.014)
        bolt('Body fastener',(x,y+(.027 if y>0 else -.027),3.40),r=.021)
    for x0,x1 in [(-4.28,-2.38),(-2.26,-.46),(-.38,1.01)]:
        panel('Upper service shutter',x0,x1,2.27,3.36,y,WHITE)
        for xx in [x0+.1,x1-.1]:
            box('Black shutter hinge',(xx,y+(.04 if y>0 else -.04),3.20),(.058,.025,.16),DARK,'10_Exterior_Hardware',.009)
            box('Recessed compression latch',(xx,y+(.04 if y>0 else -.04),2.4),(.055,.028,.085),BLACK,'10_Exterior_Hardware',.009)
    for x0,x1 in [(-2.20,-1.25),(-1.19,-.19),(-.13,.97)]:panel('Lower service locker',x0,x1,1.52,1.96,y)
    # Stripe passes behind the opening only on the sealed driver side.
    segments=[(-4.46,1.02),(2.04,2.24)] if y<0 else [(-4.46,2.24)]
    for a,b in segments:box('Blue body stripe',((a+b)/2,y+(.023 if y>0 else -.023),2.125),(b-a,.013,.11),BLUE,'10_Exterior_Hardware',0)
    for x in [-4.4,-2.1,.6,2.16]:box('Side amber clearance lamp',(x,y+(.055 if y>0 else -.055),1.44),(.13,.028,.047),AMBER,'10_Exterior_Hardware')
    for x in [-4.48,2.13]:
        for z in [1.94,2.57,3.18]:bolt('Corner rivet',(x,y+(.043 if y>0 else -.043),z),r=.013)
panel('Rear hatch panel',-1,1,1.6,3.24,1.32) if False else None
box('Rear service door gasket',(-4.603,0,2.37),(.025,2.1,1.87),DARK,'10_Exterior_Hardware',.04)
box('Rear service door',(-4.623,0,2.37),(.035,2.04,1.81),WHITE,'10_Exterior_Hardware',.035)
for y in [-.96,.96]:
    for z in [1.83,2.85]:box('Rear door hinge',(-4.65,y,z),(.035,.12,.14),DARK,'10_Exterior_Hardware')
    box('Rear lamp surround',(-4.65,y,1.05),(.06,.34,.16),BLACK,'10_Exterior_Hardware')
    box('Rear stop lamp',(-4.69,y-.07,1.06),(.018,.14,.10),RED,'10_Exterior_Hardware')
    box('Rear indicator',(-4.69,y+.075,1.06),(.018,.1,.10),AMBER,'10_Exterior_Hardware')

# Cab: hollow coachwork, steep windshield and high truck face, independent glazing.
box('Cab floor',(3.42,0,1.34),(2.23,2.31,.15),DARK,'03_Cab')
box('Cab roof',(3.36,0,3.27),(2.11,2.40,.16),WHITE,'03_Cab',.09)
box('Cab rear wall left',(2.33,.95,2.3),(.10,.45,1.92),WHITE,'03_Cab')
box('Cab rear wall right',(2.33,-.95,2.3),(.10,.45,1.92),WHITE,'03_Cab')
for sy in [-1,1]:
    y=sy*1.19
    outer=[(2.38,1.36),(4.51,1.36),(4.56,2.01),(4.23,3.19),(2.38,3.19)]
    inner=[(2.60,2.21),(4.31,2.21),(4.08,3.04),(2.60,3.04)]
    poly_extrude('Cab lower door',[(2.40,1.38),(4.48,1.38),(4.51,2.14),(2.4,2.14)],'Y',y-.045,y+.045,WHITE,'03_Cab',.026)
    box('Cab rear pillar',(2.47,y,2.68),(.22,.09,1.02),WHITE,'03_Cab',.035)
    poly_extrude('Cab front A pillar',[(4.29,2.10),(4.52,2.06),(4.23,3.2),(4.06,3.2)],'Y',y-.045,y+.045,WHITE,'03_Cab',.02)
    box('Cab window header',(3.30,y,3.14),(1.6,.10,.12),WHITE,'03_Cab',.02)
    poly_extrude('Cab rubber window gasket',inner,'Y',y-.055,y+.055,BLACK,'03_Cab',.024)
    glassprofile=[(2.66,2.26),(4.22,2.26),(4.015,2.985),(2.66,2.985)]
    poly_extrude('Cab side glass',glassprofile,'Y',y+sy*.059,y+sy*.069,GLASS,'03_Cab',.01)
    box('Cab window divider',(2.93,y+sy*.073,2.62),(.027,.018,.70),DARK,'03_Cab')
    # Fine door seams and ergonomic handle.
    for x in [2.49,4.22]:bar('Cab panel seam',(x,y+sy*.057,1.48),(x,y+sy*.057,2.13),.006,DARK,'03_Cab')
    box('Door handle pocket',(2.77,y+sy*.059,2.02),(.23,.022,.092),BLACK,'03_Cab',.025)
    box('Door pull',(2.77,y+sy*.08,2.03),(.16,.035,.033),DARK,'03_Cab',.01)
    box('Cab blue stripe',(2.99,y+sy*.058,1.82),(1.15,.017,.09),BLUE,'03_Cab',.005)
    for zz,xx in [(1.15,2.50),(.87,2.49)]:box('Cab foothold',(xx,sy*1.22,zz),(.41,.30,.055),ALU,'03_Cab')
    bar('Mirror upper arm',(4.04,sy*1.22,2.85),(4.04,sy*1.48,2.84),.026,DARK,'03_Cab')
    bar('Mirror lower arm',(4.18,sy*1.22,2.20),(4.14,sy*1.50,2.30),.026,DARK,'03_Cab')
    box('Mirror large shell',(4.1,sy*1.53,2.63),(.18,.14,.48),DARK,'03_Cab',.05)
    box('Mirror lower wide angle',(4.13,sy*1.53,2.29),(.18,.14,.16),DARK,'03_Cab',.04)
    box('Mirror glass',(4.00,sy*1.53,2.63),(.014,.107,.41),STEEL,'03_Cab',.027)
    box('Cab side corner indicator',(4.40,sy*1.22,1.59),(.10,.018,.08),AMBER,'03_Cab')
# Front surface is a sequence of styled trapezoid pieces.
frontprof=[(-1.20,1.35),(1.20,1.35),(1.20,2.09),(-1.20,2.09)]
poly_extrude('Cab lower front fascia',frontprof,'X',4.45,4.55,WHITE,'03_Cab',.07)
poly_extrude('Radiator black trapezoid',[(-.72,1.42),(.72,1.42),(.98,2.00),(-.98,2.00)],'X',4.55,4.585,DARK,'03_Cab',.02)
for i,z in enumerate([1.50,1.63,1.76,1.89]):
    width=1.40+i*.135
    box('Grille horizontal louvre',(4.602,0,z),(.055,width,.045),BLACK,'03_Cab',.017)
    box('Grille bright upper edge',(4.632,0,z+.017),(.008,width-.05,.008),STEEL,'03_Cab',.003)
for y in [-.55,-.3,0,.3,.55]:box('Radiator vertical mesh',(4.587,y,1.72),(.01,.009,.49),BLACK,'03_Cab')
# Windshield: layered perimeter plus inset glazing on a common sloped plane.
wind=[(4.54,-1.13,2.10),(4.54,1.13,2.10),(4.245,1.13,3.14),(4.245,-1.13,3.14)]
mesh('Windshield weatherstrip',wind,[(0,1,2,3)],BLACK,'03_Cab')
wind2=[(4.532,-1.047,2.18),(4.532,1.047,2.18),(4.273,1.047,3.055),(4.273,-1.047,3.055)]
mesh('Wide laminated windshield',wind2,[(0,1,2,3)],GLASS,'03_Cab')
for yy in [-.65,.55]:
    bar('Wiper arm',(4.57,yy,2.14),(4.505,yy-.18,2.37),.010,BLACK,'03_Cab')
    bar('Wiper blade',(4.505,yy-.38,2.39),(4.505,yy+.18,2.39),.013,BLACK,'03_Cab')
box('Sun visor',(4.23,0,3.24),(.26,2.39,.14),DARK,'03_Cab',.043)
for yy in [-.98,.98]:box('Visor clearance LED',(4.379,yy,3.235),(.025,.14,.043),LED,'03_Cab')
box('Front bumper',(4.57,0,1.04),(.32,2.50,.29),DARK,'03_Cab',.05)
for sy in [-1,1]:
    box('Headlamp black surround',(4.645,sy*.96,1.31),(.15,.45,.23),BLACK,'03_Cab',.05)
    for yy in [sy*.86,sy*1.06]:
        cyl('Projector headlamp',(4.733,yy,1.32),.070,.025,ALU,'03_Cab','X')
        cyl('Projector glass',(4.75,yy,1.32),.052,.015,LED,'03_Cab','X')
    box('LED running light',(4.733,sy*.96,1.40),(.018,.34,.025),LED,'03_Cab')
    torus('Front tow ring',(4.76,sy*.57,.97),.068,.017,STEEL,'03_Cab','X')
poly_extrude('Front protection skid',[(-.56,.83),(.56,.83),(.69,1.04),(-.69,1.04)],'X',4.68,4.82,ALU,'03_Cab',.025)
for yy in [-.38,0,.38]:box('Skid cutout insert',(4.831,yy,.94),(.008,.20,.07),DARK,'03_Cab')
box('Roof rack base',(3.18,0,3.385),(1.47,2.16,.045),DARK,'03_Cab')
for y in [-1.05,1.05]:
    bar('Roof rack top rail',(2.48,y,3.57),(3.87,y,3.57),.025,DARK,'03_Cab')
    for x in [2.49,3.20,3.86]:bar('Roof rack post',(x,y,3.39),(x,y,3.57),.022,DARK,'03_Cab')
for x in [2.49,3.86]:bar('Roof rack end',(x,-1.05,3.57),(x,1.05,3.57),.025,DARK,'03_Cab')

# Roof structure has a genuine recessed, separated drone bay.
HX0,HX1,HY=-1.85,.35,.97
for a,b in [(-4.57,HX0),(HX1,2.28)]:box('Roof panel',((a+b)/2,0,3.55),(b-a,2.48,.11),WHITE,'05_Roof_Static',.023)
for y in [-1.12,1.12]:box('Roof bay side bridge',(-.75,y,3.55),(2.20,.28,.11),WHITE,'05_Roof_Static')
box('Drone bay sealed bottom',(-.75,0,3.19),(2.17,1.92,.065),DARK,'06_Roof_Hatches')
box('White cabin ceiling below drone well',(-.75,0,3.149),(2.12,1.89,.018),WHITE,'06_Roof_Hatches')
for y in [-.975,.975]:box('Drone bay sidewall',(-.75,y,3.385),(2.2,.045,.34),DARK,'06_Roof_Hatches')
for x in [HX0,HX1]:box('Drone bay endwall',(x,0,3.385),(.045,1.94,.34),DARK,'06_Roof_Hatches')
for y in [-1.0,1.0]:box('Hatch rubber seal',(-.75,y,3.618),(2.27,.024,.018),BLACK,'06_Roof_Hatches',.006)
ROOF=[]
for name,x,sg in [('REAR',HX0,-1),('FRONT',HX1,1)]:
    p=empty('CTRL_ROOF_'+name,(x,0,3.632),'06_Roof_Hatches',ROOT);ROOF.append((p,sg))
    p['axis']='Local Y';p['closed_degrees']=0.;p['open_degrees']=sg*103.
    # Front leaf extends toward -X, rear leaf extends toward +X.
    cx=-sg*.545
    box('HATCH_'+name+'_panel',(cx,0,0),(1.08,1.97,.063),WHITE,'06_Roof_Hatches',.027,p)
    box('HATCH_'+name+'_inner_reinforcement',(cx,0,-.047),(.91,1.77,.035),ALU,'06_Roof_Hatches',.016,p)
    for y in [-.72,.72]:box('Hatch inner rib',(cx,y,-.07),(.87,.035,.035),DARK,'06_Roof_Hatches',.008,p)
    for yy in [-.84,-.28,.28,.84]:
        cyl('Hatch hinge barrel',(0,yy,0),.041,.13,DARK,'06_Roof_Hatches','Y',24,p)
        bolt('Hatch hinge bolt',(0,yy-.074,0),'Y','06_Roof_Hatches',p,.025)
    for xx in [cx-.4,cx+.4]:
        for yy in [-.82,.82]:bolt('Hatch screw',(xx,yy,.039),'Z','06_Roof_Hatches',p,.012)
    box('Hatch lifting handle',(cx,0,.045),(.12,.30,.026),DARK,'06_Roof_Hatches',.009,p)
PLATFORM=empty('CTRL_DRONE_LIFT',(-.75,0,3.237),'06_Roof_Hatches',ROOT)
box('Drone elevator platform',(0,0,0),(1.90,1.69,.032),BLACK,'06_Roof_Hatches',.018,PLATFORM)
for x in [-.83,.83]:
    for y in [-.70,.70]:cyl('Landing tie-down eye',(x,y,.027),.025,.012,GOLD,'06_Roof_Hatches','Z',16,PLATFORM)
for y in [-.66,.66]:box('Landing guide',(0,y,.025),(1.40,.027,.014),GOLD,'06_Roof_Hatches',.004,PLATFORM)
for x0,x1 in [(-4.26,-2.12),(.67,2.04)]:
    cx=(x0+x1)/2; length=x1-x0
    box('Solar aluminium frame',(cx,0,3.643),(length,1.87,.062),ALU,'05_Roof_Static',.012)
    box('Solar module',(cx,0,3.681),(length-.06,1.80,.018),SOLAR,'05_Roof_Static',.006)
    specs=[]
    for k in range(1,13):specs.append(((x0+.03+(length-.06)*k/13,0,3.694),(.005,1.78,.004),None))
    for j in range(1,9):specs.append(((cx,-.9+1.8*j/9,3.694),(length-.06,.004,.004),None))
    multi_boxes('Solar cell busbars',specs,ALU,'05_Roof_Static')
    # Roof reference includes a small white radome on each fixed section.
    xx=cx; yy=.55
    cyl('Radome base',(xx,yy,3.74),.17,.11,DARK,'05_Roof_Static')
    cyl('White navigation dome',(xx,yy,3.84),.17,.18,WHITE,'05_Roof_Static',r2=.14)
for y in [-1.22,1.22]:
    bar('Roof equipment rail',(-4.45,y,3.74),(2.18,y,3.74),.020,DARK,'05_Roof_Static')
    for x in [-4.44,-2.3,.61,2.17]:bar('Rail standoff',(x,y,3.57),(x,y,3.74),.022,STEEL,'05_Roof_Static')
bar('Weather mast',(2.04,.93,3.67),(2.04,.93,4.45),.035,DARK,'05_Roof_Static')
cyl('Mast upper antenna',(2.04,.93,4.54),.055,.24,WHITE,'05_Roof_Static')
for dx,dy in [(-.16,0),(.16,0),(0,.16),(0,-.16)]:
    bar('Anemometer arm',(2.04,.93,4.22),(2.04+dx,.93+dy,4.22),.009,STEEL,'05_Roof_Static')
    cyl('Anemometer cup',(2.04+dx,.93+dy,4.24),.029,.043,BLACK,'05_Roof_Static')
for y in [-.93,.93]:bar('Radio whip',(-4.36,y,3.6),(-4.36,y,4.12),.006,DARK,'05_Roof_Static')

# Side garage: floor at hinge level; robot points toward passenger-side exit.
GX=-3.32; GY=-1.275; GZ=.69; GH=1.10
box('Garage floor',(GX,-.65,GZ),(2.0,1.23,.08),ALU,'07_Robot_Garage')
box('Garage sealed back',(GX,-.025,1.245),(2.04,.08,1.19),DARK,'07_Robot_Garage')
box('Garage ceiling',(GX,-.65,1.82),(2.04,1.25,.075),DARK,'07_Robot_Garage')
for x in [-4.35,-2.29]:box('Garage partition',(x,-.65,1.245),(.07,1.25,1.20),DARK,'07_Robot_Garage')
for x in [-4.37,-2.27]:box('Garage jamb',(x,GY,1.245),(.065,.09,1.23),ALU,'07_Robot_Garage')
box('Garage upper jamb',(GX,GY,1.848),(2.16,.09,.07),ALU,'07_Robot_Garage')
box('Garage LED strip',(GX,-.98,1.758),(1.74,.035,.025),WARM,'07_Robot_Garage')
for x in [-4.15,-2.49]:
    for z in [.89,1.25,1.58]:box('Dock wall storage',(x,-.22,z),(.29,.25,.24),BLACK,'07_Robot_Garage',.025)
    bar('Dock guide',(x,-1.10,.75),(x,-.2,.75),.02,GOLD,'07_Robot_Garage')
RAMP=empty('CTRL_ROBOT_RAMP',(GX,-1.35,GZ),'07_Robot_Garage',ROOT)
RAMP['axis']='Local X';RAMP['closed_degrees']=0.;RAMP['open_degrees']=125.
box('Garage threshold bridge',(GX,-1.281,GZ+.022),(1.85,.032,.028),ALU,'07_Robot_Garage',.006)
box('ROBOT_RAMP_outer_door',(0,-.028,GH/2),(1.96,.067,GH),WHITE,'07_Robot_Garage',.02,RAMP)
box('ROBOT_RAMP_inner_deck',(0,.014,GH/2),(1.88,.035,GH-.10),ALU,'07_Robot_Garage',.01,RAMP)
for x in [-.95,.95]:box('Ramp rim',(x,.046,GH/2),(.041,.049,GH-.06),DARK,'07_Robot_Garage',.009,RAMP)
spec=[]
for i in range(18):
    for j in range(9):
        spec.append(((-.84+i*.099,.038,.12+j*.105),(.055,.012,.009),Matrix.Rotation((1 if (i+j)%2 else -1)*.65,3,'Y')))
multi_boxes('Ramp diamond tread',spec,STEEL,'07_Robot_Garage',RAMP)
for x in [-.8,.8]:
    box('Ramp latch pocket',(x,-.067,.98),(.075,.016,.10),BLACK,'07_Robot_Garage',.007,RAMP)
    box('Ramp release lever',(x,-.08,.98),(.025,.014,.067),STEEL,'07_Robot_Garage',.004,RAMP)
for x in [-.78,0,.78]:cyl('Ramp hinge knuckle',(x,0,0),.045,.27,STEEL,'07_Robot_Garage','X',24,RAMP)
for x in [-.88,.88]:bolt('Ramp base screw',(x,-.074,.07),'Y','07_Robot_Garage',RAMP,.014)

# Crew door, independent swing pivot and retracting access ladder.
ENTRY=empty('CTRL_CREW_DOOR',(2.01,-1.278,1.41),'08_Crew_Entry',ROOT)
box('Crew door lower',(-.465,0,.48),(.92,.075,.96),WHITE,'08_Crew_Entry',.04,ENTRY)
box('Crew door upper header',(-.465,0,1.735),(.92,.075,.10),WHITE,'08_Crew_Entry',.025,ENTRY)
for xx in [-.867,-.063]:box('Crew door window post',(xx,0,1.35),(.115,.075,.69),WHITE,'08_Crew_Entry',.02,ENTRY)
box('Crew door window rubber',(-.465,0,1.34),(.69,.081,.68),BLACK,'08_Crew_Entry',.055,ENTRY)
box('Crew door tinted glass',(-.465,-.044,1.34),(.57,.010,.57),GLASS,'08_Crew_Entry',.044,ENTRY)
box('Crew door handle',(-.80,-.070,.68),(.07,.048,.13),BLACK,'08_Crew_Entry',.015,ENTRY)
for zz in [.22,1.47]:cyl('Crew door hinge',(0,0,zz),.032,.16,DARK,'08_Crew_Entry','Z',20,ENTRY)
STEPS=empty('CTRL_CREW_STEPS',(1.54,-1.25,1.35),'08_Crew_Entry',ROOT)
for i in range(4):
    y=-.17-i*.19; z=-.18-i*.25
    box('Perforated boarding tread',(0,y,z),(.83,.24,.042),ALU,'08_Crew_Entry',.015,STEPS)
    for xx in [-.28,-.14,0,.14,.28]:box('Tread grip slot',(xx,y,z+.023),(.075,.14,.006),DARK,'08_Crew_Entry',.015,STEPS)
for x in [-.44,.44]:bar('Boarding ladder stringer',(x,-.10,-.05),(x,-.84,-1.0),.025,STEEL,'08_Crew_Entry',STEPS)
for x in [1.03,2.09]:bar('Crew entry grab handle',(x,-1.35,1.67),(x,-1.35,2.3),.018,GOLD,'08_Crew_Entry')

# Interior furniture and operator equipment, with clear center aisle.
box('Operator worktop',(-.72,.86,2.11),(2.82,.60,.06),STEEL,'09_Interior',.025)
for x in [-1.84,.40]:
    box('Blue operator drawer pedestal',(x,.88,1.76),(.49,.54,.66),BLUE,'09_Interior',.018)
    for z in [1.54,1.74,1.94]:
        box('Operator drawer front',(x,.593,z),(.43,.018,.17),BLUE,'09_Interior',.007)
        box('Drawer metal pull',(x,.57,z+.025),(.18,.028,.02),STEEL,'09_Interior')
for i,x in enumerate([-1.63,-.72,.19]):
    box('Monitor back',(x,1.055,2.57),(.82,.07,.49),BLACK,'09_Interior',.025)
    box('Monitor display',(x,1.010,2.57),(.755,.012,.421),SCREEN,'09_Interior',.008)
    bar('Monitor arm',(x,1.12,2.15),(x,1.12,2.53),.022,DARK,'09_Interior')
    # Screen UI is geometry, independent from external textures.
    for k in range(4):box('Telemetry readout',(x-.28,1.000,2.69-k*.055),(.11+.02*(k%2),.005,.013),CYAN,'09_Interior',0)
    for k in range(5):box('Data chart bar',(x-.06+k*.057,1.000,2.44+.018*k),(.026,.005,.05+.036*k),GREEN,'09_Interior',0)
    label('Monitor header',['DRONE','FIELD MAP','ROVER'][i],(x,.993,2.733),.029,'09_Interior',CYAN,(math.pi/2,0,0))
box('Operator keyboard',(-.71,.70,2.155),(.51,.19,.028),BLACK,'09_Interior',.012)
spec=[]
for i in range(14):
    for j in range(4):spec.append(((-.946+i*.035,.63+j*.04,2.175),(.025,.027,.01),None))
multi_boxes('Keyboard keycaps',spec,DARK,'09_Interior')
box('Mouse',(-.29,.70,2.17),(.09,.14,.048),BLACK,'09_Interior',.032)
for x in [-1.84,.43]:
    box('Hand controller base',(x,.70,2.17),(.16,.17,.035),DARK,'09_Interior')
    cyl('Hand controller stick',(x,.70,2.22),.023,.08,BLACK,'09_Interior')
    cyl('Emergency stop',(x-.052,.65,2.201),.014,.016,RED,'09_Interior')
def chair(n,x,y,z,col,facing='Y'):
    p=empty(n,(x,y,z),col,ROOT)
    if facing=='X':p.rotation_euler.z=-math.pi/2
    cyl('Chair swivel post',(0,0,.23),.041,.38,STEEL,col,parent=p)
    for i in range(5):
        a=i*2*math.pi/5
        bar('Chair base spoke',(0,0,.08),(.25*math.cos(a),.25*math.sin(a),.065),.018,DARK,col,p)
        cyl('Chair caster',(.25*math.cos(a),.25*math.sin(a),.047),.047,.07,BLACK,col,'Y',16,p)
    box('Seat cushion',(0,0,.46),(.46,.43,.12),SEAT,col,.055,p)
    box('Seat back',(0,-.20,.77),(.45,.075,.58),SEAT,col,.050,p,(-.10,0,0))
    for xx in [-.255,.255]:
        bar('Chair arm support',(xx,-.15,.49),(xx,-.15,.68),.014,DARK,col,p)
        box('Padded armrest',(xx,-.015,.70),(.055,.28,.05),BLACK,col,.02,p)
    for xx in [-.16,.16]:bar('Seat back stitch',(xx,-.242,.58),(xx,-.242,.99),.002,ALU,col,p)
    return p
for x in [-1.48,.03]:chair('Operator chair',x,.43,1.43,'09_Interior')
# Opposite sample bench; cuttable true basin built from rims and a recessed tub.
box('Sample bench cabinetry',(-.66,-.865,1.75),(2.68,.58,.64),WHITE,'09_Interior',.025)
for x in [-1.67,-1.00,-.33,.34]:
    box('Sample cabinet door',(x,-.558,1.77),(.62,.035,.55),WHITE,'09_Interior',.008)
    box('Cabinet latch',(x+.24,-.535,1.93),(.045,.025,.075),BLACK,'09_Interior')
box('Sample bench main steel top',(-.34,-.865,2.11),(2.05,.61,.043),STEEL,'09_Interior',.009)
for y in [-1.134,-.597]:box('Sink side rim',(-1.61,y,2.11),(.60,.075,.043),STEEL,'09_Interior')
for x in [-1.895,-1.335]:box('Sink end rim',(x,-.865,2.11),(.07,.46,.043),STEEL,'09_Interior')
box('Sink bowl bottom',(-1.61,-.865,1.99),(.47,.38,.025),STEEL,'09_Interior',.055)
for x in [-1.845,-1.375]:box('Sink bowl sides',(x,-.865,2.055),(.018,.39,.13),STEEL,'09_Interior',.008)
for y in [-1.05,-.68]:box('Sink bowl sides',(-1.61,y,2.055),(.48,.018,.13),STEEL,'09_Interior',.008)
cyl('Sink drain',(-1.61,-.865,2.005),.032,.005,DARK,'09_Interior')
bar('Faucet stem',(-1.77,-1.09,2.11),(-1.77,-1.09,2.34),.016,STEEL,'09_Interior')
bar('Faucet spout',(-1.77,-1.09,2.34),(-1.77,-.9,2.34),.016,STEEL,'09_Interior')
bar('Faucet outlet',(-1.77,-.9,2.34),(-1.77,-.9,2.30),.016,STEEL,'09_Interior')
box('Sample preparation inset',(-.69,-.84,2.139),(.55,.36,.013),ALU,'09_Interior',.028)
box('Instrument charging cabinet',(.82,-.89,1.9),(.31,.55,.94),DARK,'09_Interior')
for i in range(8):box('Instrument cabinet vent',(.82,-.595,1.69+i*.032),(.22,.012,.01),BLACK,'09_Interior')
# Rear raised utility zone above isolated robot garage, not a hidden floor intersection.
box('Raised garage top inside',(-3.32,-.66,1.88),(2.02,1.18,.08),FLOOR,'09_Interior')
for z in [2.12,2.48,2.84]:
    box('Rugged field equipment case',(-4.14,.78,z),(.57,.57,.31),BLACK,'09_Interior',.035)
    for x in [-4.32,-3.96]:box('Case yellow latch',(x,.472,z),(.04,.033,.06),GOLD,'09_Interior')
    box('Case handle',(-4.14,.458,z+.03),(.18,.03,.025),DARK,'09_Interior')
box('Rear tool cabinet',(-3.27,.91,2.29),(.90,.45,1.65),WHITE,'09_Interior',.025)
for z in [1.66,1.90,2.14,2.38]:
    box('Tool drawer front',(-3.27,.665,z),(.79,.026,.19),ALU,'09_Interior')
    box('Tool drawer handle',(-3.27,.639,z+.02),(.49,.035,.025),DARK,'09_Interior')
box('Tool cabinet upper locker',(-3.27,.659,2.76),(.79,.034,.47),WHITE,'09_Interior')
box('Forward electrical cabinet',(1.33,.91,2.35),(.92,.47,1.84),WHITE,'09_Interior',.025)
for x in [1.10,1.56]:
    box('Electrical locker door',(x,.657,2.36),(.435,.028,1.72),WHITE,'09_Interior')
    box('Electrical latch',(x+.15,.631,2.25),(.045,.02,.08),BLACK,'09_Interior')
for y in [-1.04,1.04]:
    box('Interior window frame',(-.8,y,2.70),(2.20,.05,.67),BLACK,'09_Interior',.05)
    box('Interior dark window',(-.8,y+(.028 if y<0 else -.028),2.70),(2.08,.01,.55),GLASS,'09_Interior',.035)
    box('Upper storage fascia',(-.65,y,3.19),(3.50,.31,.27),WHITE,'09_Interior',.018)
    box('Continuous warm task light',(-.65,y*.95,3.022),(3.42,.04,.021),WARM,'09_Interior')
    for x in [-1.9,-.8,.30]:box('Upper locker latch',(x,y*.85,3.14),(.04,.024,.055),BLACK,'09_Interior')
for y in [-.48,.48]:box('Ceiling light strip',(-.6,y,3.455),(4.8,.06,.021),WARM,'05_Roof_Static')
for x in [-3.1,1.4]:
    box('Vent grille', (x,0,3.47),(.54,.47,.027),WHITE,'05_Roof_Static')
    for i in range(9):box('Vent louvre',(x-.22+i*.055,0,3.451),(.018,.37,.016),DARK,'05_Roof_Static')
# Driver's cabin includes seating, steering column, dashboard and displays.
for y in [-.58,.58]:chair('Driving seat',2.92,y,1.43,'03_Cab','X')
box('Dashboard',(4.09,0,2.08),(.49,2.10,.27),DARK,'03_Cab',.08)
box('Instrument binnacle',(3.88,.57,2.25),(.24,.55,.20),BLACK,'03_Cab',.04)
torus('Steering rim',(3.75,.57,2.27),.18,.016,BLACK,'03_Cab','X')
bar('Steering spoke',(3.75,.39,2.27),(3.75,.75,2.27),.015,DARK,'03_Cab')
bar('Steering column',(3.75,.57,2.27),(4.02,.57,2.06),.035,DARK,'03_Cab')

# Compact four-rotor UAV; all parts move with its elevator platform.
D=empty('DRONE_ROOT',(0,0,.075),'11_Drone',PLATFORM)
box('Drone lower avionics',(0,0,.10),(.35,.22,.10),DARK,'11_Drone',.04,D)
box('Drone white shell',(0,0,.15),(.41,.25,.13),WHITE,'11_Drone',.055,D)
box('Drone blue dorsal strip',(0,0,.218),(.28,.065,.012),BLUE,'11_Drone',.012,D)
cyl('Drone GNSS cap',(0,0,.24),.055,.025,WHITE,'11_Drone',parent=D)
for sx in [-1,1]:
    for sy in [-1,1]:
        x=sx*.41;y=sy*.42
        bar('Carbon UAV boom',(sx*.13,sy*.09,.12),(x,y,.15),.021,DARK,'11_Drone',D)
        cyl('Blue motor mount',(x,y,.16),.037,.052,BLUE,'11_Drone',parent=D)
        cyl('Brushless motor',(x,y,.193),.033,.035,DARK,'11_Drone',parent=D)
        # Two aerodynamically tapered propeller blades, not disks.
        prof=[(-.205,-.015),(-.20,.016),(-.04,.028),(.04,.025),(.205,.010),(.205,-.009),(.04,-.025),(-.04,-.023)]
        o=poly_extrude('Rotor blade',prof,'Z',.214,.222,BLACK,'11_Drone',.003,D);o.location.x=x;o.location.y=y;o.rotation_euler.z=(sx*sy)*.38
        cyl('Rotor screw',(x,y,.229),.015,.014,STEEL,'11_Drone',parent=D)
        bar('Landing leg',(x,y,.14),(x,y,-.055),.012,DARK,'11_Drone',D)
        box('Landing foot',(x,y,-.055),(.054,.042,.018),BLACK,'11_Drone',.007,D)
        box('UAV nav light',(x+sx*.024,y,.17),(.024,.024,.016),GREEN if sx<0 else RED,'11_Drone',.004,D)
box('Camera gimbal',( .19,0,.045),(.07,.10,.075),DARK,'11_Drone',.012,D)
cyl('Camera front glass',(.233,0,.045),.021,.016,GLASS,'11_Drone','X',24,D)

# Tracked rover, nose toward -Y and centered in the independent rear garage.
R=empty('ROVER_ROOT',(GX,-.65,.755),'12_Tracked_Robot',ROOT)
box('Rover chassis',(0,0,.20),(.52,.84,.19),DARK,'12_Tracked_Robot',.055,R)
box('Rover beveled hull',(0,-.01,.35),(.59,.80,.24),WHITE,'12_Tracked_Robot',.085,R)
box('Rover blue top',(0,.015,.476),(.32,.56,.020),BLUE,'12_Tracked_Robot',.025,R)
for sx in [-1,1]:
    # Capsule track band assembled from continuous outside and inside loops.
    pts=[]
    for i in range(17):
        a=math.pi*i/16;pts.append((-.29-.135*math.sin(a),.15+.135*math.cos(a)))
    for i in range(17):
        a=math.pi*i/16;pts.append((.29+.135*math.sin(a),.15-.135*math.cos(a)))
    vv=[]
    for x in [sx*.30-.078,sx*.30+.078]:vv += [(x,y,z) for y,z in pts]
    N=len(pts);ff=[(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
    mesh('Continuous rubber track',vv,ff,RUBBER,'12_Tracked_Robot',R)
    for yy in [-.29,-.145,0,.145,.29]:
        rad=.10 if abs(yy)>.2 else .076
        cyl('Rover road wheel',(sx*.385,yy,.15),rad,.018,DARK,'12_Tracked_Robot','X',24,R)
        cyl('Rover wheel hub',(sx*.40,yy,.15),rad*.45,.009,STEEL,'12_Tracked_Robot','X',16,R)
    spec=[]
    for i in range(14):
        yy=-.30+i*.047
        for zz in [.02,.281]:spec.append(((sx*.30,yy,zz),(.18,.026,.025),None))
    for yy in [-.416,.416]:
        for zz in [.08,.15,.22]:spec.append(((sx*.30,yy,zz),(.18,.025,.045),None))
    multi_boxes('Rover tread blocks',spec,RUBBER,'12_Tracked_Robot',R)
box('Rover stereo fascia',(0,-.425,.35),(.43,.035,.135),BLACK,'12_Tracked_Robot',.035,R)
for x in [-.13,.13]:
    cyl('Stereo camera rim',(x,-.452,.35),.041,.016,STEEL,'12_Tracked_Robot','Y',24,R)
    cyl('Stereo optical glass',(x,-.464,.35),.030,.010,GLASS,'12_Tracked_Robot','Y',24,R)
cyl('Lidar black band',(0,.04,.53),.092,.095,GLASS,'12_Tracked_Robot',parent=R)
cyl('Lidar white cap',(0,.04,.585),.095,.023,WHITE,'12_Tracked_Robot',parent=R)
box('Drill mounting rail',(.36,.22,.41),(.07,.10,.42),STEEL,'12_Tracked_Robot',.012,R)
bar('Soil probe shaft',(.36,.22,.12),(.36,.22,.68),.019,STEEL,'12_Tracked_Robot',R)
cyl('Soil probe drive',(.36,.22,.67),.038,.1,DARK,'12_Tracked_Robot',parent=R)
cyl('Probe cone',(.36,.22,.095),.020,.07,STEEL,'12_Tracked_Robot',parent=R,r2=.004)

# Baked mechanical support struts, reversible transform animation.
def linear_keys(o):
    ac=o.animation_data.action
    if hasattr(ac,'fcurves'):curves=list(ac.fcurves)
    else:
        curves=[]
        for layer in ac.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:curves += list(bag.fcurves)
    for fc in curves:
        for k in fc.keyframe_points:k.interpolation='LINEAR'
def animate_rot(o,axis,keys,name):
    for f,v in keys:o.rotation_euler[axis]=v;o.keyframe_insert('rotation_euler',frame=f)
    o.animation_data.action.name=name
    linear_keys(o)
roofkeys=[(1,0),(15,0),(55,1),(115,1),(160,0),(180,0)]
for p,sg in ROOF:animate_rot(p,1,[(f,sg*math.radians(103)*v) for f,v in roofkeys],'Roof_'+p.name+'_OpenClose')
animate_rot(RAMP,0,[(1,0),(15,0),(55,math.radians(125)),(115,math.radians(125)),(160,0),(180,0)],'RobotGarage_OpenClose')
animate_rot(ENTRY,2,[(1,0),(15,0),(55,math.radians(100)),(115,math.radians(100)),(160,0),(180,0)],'CrewDoor_OpenClose')
animate_rot(STEPS,0,[(1,math.radians(90)),(15,math.radians(90)),(55,0),(115,0),(160,math.radians(90)),(180,math.radians(90))],'CrewSteps_DeployRetract')
for f,dz in [(1,0),(55,0),(75,.32),(100,.32),(115,0),(180,0)]:
    PLATFORM.location.z=3.237+dz;PLATFORM.keyframe_insert('location',frame=f)
PLATFORM.animation_data.action.name='DronePlatform_RaiseLower'
linear_keys(PLATFORM)
STRUTS=[]
for p,sg in ROOF:
    for yy in [-.92,.92]:
        base=Vector((p.location.x-sg*.36,yy,3.30)); tiplocal=Vector((-sg*.70,yy,-.06))
        sleeve=cyl('Roof gas strut sleeve',(0,0,0),.022,1,DARK,'06_Roof_Hatches')
        rod=cyl('Roof gas strut chrome rod',(0,0,0),.011,1,STEEL,'06_Roof_Hatches')
        STRUTS.append((p,tiplocal,base,sleeve,rod))
for xx in [-.93,.93]:
    base=Vector((GX+xx,-.38,1.69)); tiplocal=Vector((xx,.055,.92))
    sleeve=cyl('Ramp support sleeve',(0,0,0),.022,1,DARK,'07_Robot_Garage')
    rod=cyl('Ramp support rod',(0,0,0),.012,1,STEEL,'07_Robot_Garage')
    STRUTS.append((RAMP,tiplocal,base,sleeve,rod))
for f in range(1,181):
    S.frame_set(f);bpy.context.view_layer.update()
    for pivot,tip,base,sleeve,rod in STRUTS:
        end=pivot.matrix_world@tip; d=end-base;length=d.length; unit=d.normalized(); split=base+unit*(length*.58)
        for o,a,b in [(sleeve,base,split),(rod,split,end)]:
            dd=b-a;o.location=(a+b)/2;o.rotation_euler=dd.to_track_quat('Z','Y').to_euler();o.scale.z=dd.length
            for prop in ['location','rotation_euler','scale']:o.keyframe_insert(prop,frame=f)
for obj in list(S.objects):
    if obj.animation_data and obj.animation_data.action:
        ac=obj.animation_data.action
        # New layered action API, preserving compatibility with older fcurves.
        curves=[]
        if hasattr(ac,'fcurves'):curves=list(ac.fcurves)
        else:
            for layer in ac.layers:
                for strip in layer.strips:
                    for bag in strip.channelbags:curves += list(bag.fcurves)
        for fc in curves:
            for k in fc.keyframe_points:k.interpolation='LINEAR'
for f,n in [(1,'CLOSED'),(35,'OPENING'),(55,'HATCHES_OPEN'),(85,'DEPLOYED'),(115,'PLATFORM_STOWED'),(160,'CLOSED_AGAIN')]:S.timeline_markers.new(n,frame=f)

# Reference images packed in the .blend as image datablocks; not exported geometry.
REF=OUT.parent/'房车建模参考_v2_一致性校对'
for filename in ['01_基础车体_关闭状态_四视角.png','02_外观_展开状态_三视角.png','03_内部布局_俯视.png','04_侧面剖视_舱室对应.png','05_机构细节_无人机舱与机器人坡道.png','06_内部视角_望向车尾.png','07_内部视角_望向驾驶室.png']:
    try:
        im=bpy.data.images.load(str(REF/filename),check_existing=True);im.pack();im.use_fake_user=True
    except Exception as e:print('REFERENCE_LOAD_ERROR',str(e),flush=True)
notes=bpy.data.texts.new('READ_ME__CONTROLS')
notes.write('Front is +X; passenger/robot side is -Y. Units are metres.\nTimeline: 1 closed, 55 hatches open, 85 platform raised, 160 reclosed.\nRoof pivots: CTRL_ROOF_FRONT / CTRL_ROOF_REAR, local Y. Ramp: CTRL_ROBOT_RAMP local X.\nTo operate one part independently: duplicate file, clear only that pivot animation, rotate on its listed local axis.\nHide 04_Habitat_Walls, 05_Roof_Static, 06_Roof_Hatches, 10_Exterior_Hardware and 11_Drone for an interior cutaway.\nReference images are packed. Procedural micro-textures are Blender-only; GLB carries base PBR values.\n')

# Studio lighting and reusable inspection cameras.
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
def light(n,loc,power,size,target=(0,0,1.8)):
    d=bpy.data.lights.new(n,'AREA');d.energy=power;d.shape='DISK';d.size=size
    d.specular_factor=.4
    o=bpy.data.objects.new(n,d);COL['90_Cameras_Lights'].objects.link(o);o.location=loc;aim(o,target)
    return o
light('Key softbox',(2,-5,8),2300,7)
light('Rear rim',(-5,2,6),1800,5)
light('Front fill',(6,4,5),1800,5)
light('Passenger fill',(-3,-4,4),1100,5)
for x in [-3.4,-1.5,.4,1.75]:light('Interior downlight',(x,0,3.08),40,1.0,(x,0,1.42))
light('Garage light',(GX,-.60,1.73),20,.9,(GX,-.7,.80))
def camera(n,loc,target,ortho=None,lens=38):
    d=bpy.data.cameras.new(n);o=bpy.data.objects.new(n,d);COL['90_Cameras_Lights'].objects.link(o);o.location=loc;aim(o,target)
    if ortho:d.type='ORTHO';d.ortho_scale=ortho
    d.lens=lens;d.clip_start=.03;d.clip_end=100
    return o
CAMS={
 'hero':camera('CAM_Exterior',(11,-13,7.2),(-.25,0,2),12.5),
 'side':camera('CAM_Right_Side',(0,-16,2.05),(0,0,2.05),10.6),
 'roof':camera('CAM_Roof_Bay',(-.7,-6,9),(-.75,0,3.50),5.5),
 'garage':camera('CAM_Robot_Garage',(-5.7,-6.7,3.0),(-3.32,-.85,1.08),4.2),
 'cutaway':camera('CAM_Interior_Cutaway',(1,-7,9),(-.20,0,2),11.7),
 'interior':camera('CAM_Cabin_Looking_Rear',(1.48,-.05,2.68),(-3.55,0,2.32),None,20),
 'cab':camera('CAM_Interior_Looking_Front',(-2.55,-.05,2.65),(3.2,0,2.30),None,20),
}
S.camera=CAMS['hero'];S.frame_set(1)
# Friendly startup view, no interfering light/camera selection.
bpy.ops.object.select_all(action='DESELECT');ROOT.select_set(True);bpy.context.view_layer.objects.active=ROOT
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=12
            area.spaces.active.region_3d.view_location=(0,0,2)
            area.spaces.active.region_3d.view_rotation=CAMS['hero'].rotation_euler.to_quaternion()
            area.spaces.active.clip_end=150
            area.spaces.active.shading.type='MATERIAL'
S['reference_package']='房车建模参考_v2_一致性校对'
S['build_method']='Local Blender procedural mesh construction; no Lux3D cloud generation task'
S['nominal_envelope_m']='Approximately 9.5 L x 3.2 W including mirrors x 4.66 H including mast; nominal habitat body width 2.5'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'智能农田房车_精细可开合_v2.blend'),compress=True)
print('MODEL_SAVED',len(S.objects),'objects',flush=True)

def render(name,cam,frame=1,cutaway=False):
    S.camera=CAMS[cam];S.frame_set(frame)
    hidden=[]
    if cutaway:
        for cn in ['04_Habitat_Walls','05_Roof_Static','06_Roof_Hatches','10_Exterior_Hardware','11_Drone']:
            for ob in COL[cn].objects:
                hidden.append((ob,ob.hide_render));ob.hide_render=True
        for ob in COL['09_Interior'].objects:
            if ob.name.startswith(('Interior window','Interior dark window','Upper storage fascia','Upper locker latch','Continuous warm task light')) and ob.location.y<0:
                hidden.append((ob,ob.hide_render));ob.hide_render=True
    S.render.filepath=str(OUT/'renders'/name);bpy.ops.render.render(write_still=True)
    for ob,old in hidden:ob.hide_render=old
render('01_closed.png','hero',1)
render('02_open.png','hero',85)
render('03_roof_open.png','roof',85)
render('04_garage_open.png','garage',85)
render('05_interior_cutaway.png','cutaway',85,True)
render('06_cabin_to_rear.png','interior',85)
render('07_cabin_to_front.png','cab',85)
S.camera=CAMS['hero'];S.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'智能农田房车_精细可开合_v2.blend'),compress=True)
# All geometry + independent transform nodes, one combined deployment cycle.
bpy.ops.object.select_all(action='DESELECT')
for cn,c in COL.items():
    if cn.startswith('90') or cn.startswith('99'):continue
    for o in c.objects:o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'智能农田房车_精细可开合_v2.glb'),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='SCENE',export_anim_scene_split_object=False,export_frame_range=True,export_force_sampling=True,export_apply=True,export_extras=True)
summary={'blender':bpy.app.version_string,'objects':len(S.objects),'mesh_objects':sum(o.type=='MESH' for o in S.objects),'animated_objects':sum(bool(o.animation_data) for o in S.objects),'elapsed_seconds':round(time.time()-START,2),'reference':str(REF),'local_only':True}
(OUT/'build_summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print('RV_V2_COMPLETE',json.dumps(summary),flush=True)
