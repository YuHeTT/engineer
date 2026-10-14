"""Build ten independent, editable maize/soil assets with Blender 5.2.

Usage: blender --background --factory-startup --python-exit-code 1 --python build_maize_assets.py
All dimensions are metres.  Geometry is authored here; no reference image is
embedded as a texture.  The nine plants share one construction vocabulary.
"""

import math
import random
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(r"C:\Users\H-CHILD\Documents\Codex\2026-09-27\woy\outputs\玉米与土壤_Blender模型_v1")
ROOT.mkdir(parents=True, exist_ok=True)


def material(name, color, roughness=0.78):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    model = bpy.data.collections.new("MODEL_可编辑主体")
    scene.collection.children.link(model)
    return model


def put_in(obj, collection):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def mesh_obj(name, verts, faces, collection, mat, material_indices=None):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.data.materials.append(mat)
    if material_indices:
        for polygon, idx in zip(mesh.polygons, material_indices):
            polygon.material_index = idx
    return obj


def cylinder_between(name, a, b, r1, r2, mat, collection, sides=10):
    a, b = Vector(a), Vector(b)
    direction = b - a
    if direction.length < 0.0001:
        return None
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r1, radius2=r2,
                                    depth=direction.length, location=(a + b) / 2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(mat)
    put_in(obj, collection)
    return obj


def ellipsoid(name, center, radius, mat, collection, segments=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.scale = radius
    obj.data.materials.append(mat)
    put_in(obj, collection)
    return obj


def setup_mats(condition):
    if condition == "好":
        leaf = (0.10, 0.38, 0.095)
        leaf_alt = (0.15, 0.47, 0.11)
        stem = (0.30, 0.49, 0.16)
    elif condition == "差":
        leaf = (0.46, 0.48, 0.17)
        leaf_alt = (0.57, 0.52, 0.21)
        stem = (0.54, 0.53, 0.23)
    else:
        leaf = (0.16, 0.40, 0.115)
        leaf_alt = (0.23, 0.47, 0.14)
        stem = (0.39, 0.50, 0.19)
    return {
        "leaf": material("叶片_主色", leaf),
        "leaf_alt": material("叶片_受光面", leaf_alt),
        "stem": material("茎秆_青绿", stem),
        "vein": material("叶脉_浅绿", (0.41, 0.59, 0.22)),
        "dry": material("叶尖_干黄", (0.53, 0.36, 0.13)),
        "husk": material("苞叶_淡绿", (0.32, 0.53, 0.19)),
        "silk": material("花丝_棕金", (0.57, 0.37, 0.16)),
        "tassel": material("雄穗_浅金", (0.64, 0.53, 0.24)),
    }


def add_leaf(collection, mats, index, count, height, stage, condition, rng):
    # A gently twisted, cambered blade.  Five width stations define a real
    # strip surface so leaves remain editable geometry in Blender and GLB.
    start_z = height * (0.13 + 0.74 * index / max(1, count - 1))
    angle = (index * 137.5 + (18 if stage == "幼苗" else 0)) * math.pi / 180
    orient = Vector((math.cos(angle), math.sin(angle), 0))
    across = Vector((-math.sin(angle), math.cos(angle), 0))
    base_len = {"幼苗": 0.29, "成长": 0.68, "成熟": 0.82}[stage]
    base_width = {"幼苗": 0.052, "成长": 0.125, "成熟": 0.145}[stage]
    vigor = {"好": 1.17, "正常": 1.0, "差": 0.75}[condition]
    length = base_len * vigor * (0.72 + 0.30 * math.sin(math.pi * (index + 1) / (count + 1)))
    width = base_width * vigor * (0.72 + 0.28 * math.sin(math.pi * (index + 1) / (count + 1)))
    if stage == "幼苗":
        length *= 0.75 + 0.35 * index / max(1, count - 1)
    rise = length * (0.63 if stage == "幼苗" else 0.52)
    droop = length * (0.56 if condition != "差" else 0.65)
    vertices = []
    segment_count = 12
    for i in range(segment_count + 1):
        t = i / segment_count
        radial = 0.02 + length * t
        z = start_z + rise * t - droop * t * t
        side_bend = 0.04 * math.sin(math.pi * t) * (1 if index % 2 else -1)
        center = orient * radial + across * side_bend + Vector((0, 0, z))
        half_w = width * math.sin(math.pi * t) ** 0.72
        for w in (-1.0, -0.5, 0.0, 0.5, 1.0):
            # Center rib sits slightly proud of the blade.
            p = center + across * half_w * w + Vector((0, 0, 0.010 * (1 - abs(w)) * math.sin(math.pi*t)))
            vertices.append(tuple(p))
    faces = []
    for i in range(segment_count):
        for j in range(4):
            a = i * 5 + j
            faces.append((a, a+1, a+6, a+5))
    mat = mats["leaf_alt"] if index % 4 == 0 else mats["leaf"]
    leaf = mesh_obj(f"叶片_{index+1:02d}", vertices, faces, collection, mat)
    for poly in leaf.data.polygons:
        poly.use_smooth = True
    solid = leaf.modifiers.new("叶片厚度", "SOLIDIFY")
    solid.thickness = 0.003 if stage == "幼苗" else 0.004
    # Brown stress shows as a separate editable tip, not a texture-only change.
    if condition == "差" and (index % 2 == 0 or index < 2):
        tip = vertices[-3]
        ellipsoid(f"叶尖干枯_{index+1:02d}", tip, (0.022, 0.011, 0.004), mats["dry"], collection)


def add_ear(collection, mats, name, height, stem_height, size, angle, rng):
    outward = Vector((math.cos(angle), math.sin(angle), 0))
    base = Vector((0, 0, height))
    axis = (outward * 0.62 + Vector((0, 0, 0.78))).normalized()
    center = base + outward * (0.12 * size) + axis * (0.14 * size)
    cylinder_between(name + "_穗柄", base, center, 0.035*size, 0.022*size,
                     mats["stem"], collection)
    ear = ellipsoid(name + "_苞叶包裹果穗", center, (0.078*size, 0.078*size, 0.25*size),
                    mats["husk"], collection, 16, 12)
    ear.rotation_euler = axis.to_track_quat("Z", "Y").to_euler()
    # Husk seam and a handful of loose silk strands.
    end = center + axis * (0.22*size)
    for j in range(5):
        theta = 2 * math.pi * j / 5
        wiggle = Vector((math.cos(theta), math.sin(theta), 0))
        p1 = end + wiggle * 0.025 * size + Vector((0,0,0.035*size))
        p2 = p1 + wiggle * 0.025 * size - Vector((0,0,0.07*size))
        cylinder_between(name+f"_花丝_{j+1}", end, p1, 0.003*size, 0.002*size,
                         mats["silk"], collection, 5)
        cylinder_between(name+f"_花丝尾_{j+1}", p1, p2, 0.002*size, 0.001*size,
                         mats["silk"], collection, 5)


def add_tassel(collection, mats, height, condition):
    base = Vector((0, 0, height * 0.90))
    tip = Vector((0, 0, height * 1.06))
    cylinder_between("顶部雄穗主轴", base, tip, 0.010, 0.002,
                     mats["tassel"], collection, 7)
    branches = {"好": 12, "正常": 9, "差": 6}[condition]
    spread = {"好": 0.18, "正常": 0.15, "差": 0.10}[condition]
    for k in range(branches):
        t = k / branches
        angle = k * 2.39996
        a = base + Vector((0, 0, height * 0.09 * t))
        r = spread * (1 - 0.35*t)
        b = a + Vector((r*math.cos(angle), r*math.sin(angle), height*0.09*(1-t)))
        cylinder_between(f"雄穗分枝_{k+1:02d}", a, b, 0.004, 0.001,
                         mats["tassel"], collection, 5)
        for j in range(2):
            p = a.lerp(b, 0.60 + 0.18*j)
            ellipsoid(f"雄穗花药_{k+1:02d}_{j+1}", p, (0.006,0.006,0.015),
                      mats["tassel"], collection, 6, 4)


def make_plant(stage, condition):
    rng = random.Random(f"maize-v1-{stage}-{condition}")
    model = reset_scene()
    mats = setup_mats(condition)
    normal_height = {"幼苗": 0.36, "成长": 1.30, "成熟": 1.95}[stage]
    factor = {"好": 1.16, "正常": 1.0, "差": 0.77}[condition]
    height = normal_height * factor
    leaves = {
        "幼苗": {"好": 6, "正常": 5, "差": 4},
        "成长": {"好": 12, "正常": 10, "差": 7},
        "成熟": {"好": 13, "正常": 11, "差": 8},
    }[stage][condition]
    stem_r = {"幼苗": 0.013, "成长": 0.032, "成熟": 0.040}[stage]
    stem_r *= {"好": 1.15, "正常": 1.0, "差": 0.72}[condition]
    segment_count = max(4, leaves + 1)
    top_of_stem = height * (0.90 if stage == "成熟" else 0.94)
    for i in range(segment_count):
        z1 = top_of_stem * i / segment_count
        z2 = top_of_stem * (i + 1) / segment_count
        taper1 = 1.0 - 0.42 * i / segment_count
        taper2 = 1.0 - 0.42 * (i + 1) / segment_count
        cylinder_between(f"茎节_{i+1:02d}", (0,0,z1), (0,0,z2),
                         stem_r*taper1, stem_r*taper2, mats["stem"], model, 12)
        if i > 0:
            ellipsoid(f"节环_{i:02d}", (0,0,z1),
                      (stem_r*taper1*1.05, stem_r*taper1*1.05, 0.008),
                      mats["stem"], model, 12, 4)
    for i in range(leaves):
        add_leaf(model, mats, i, leaves, height, stage, condition, rng)
    if stage == "成熟":
        add_tassel(model, mats, height, condition)
        if condition == "好":
            add_ear(model, mats, "雌穗_上", height*0.59, height, 1.10, 0.35, rng)
            add_ear(model, mats, "雌穗_下", height*0.46, height, 0.88, 2.45, rng)
        elif condition == "差":
            add_ear(model, mats, "雌穗_瘦小", height*0.51, height, 0.60, 0.35, rng)
        else:
            add_ear(model, mats, "雌穗_正常", height*0.55, height, 1.0, 0.35, rng)
    model["资产类别"] = "玉米单株"
    model["生长阶段"] = stage
    model["长势"] = condition
    model["株高米"] = round(height, 3)
    model["叶片数量"] = leaves
    return model, height


def soil_height(x, y):
    # Four rounded raised rows run along Y; lower furrows remain slightly damp.
    ridge = max(0, math.cos(2 * math.pi * (x + 1.5))) ** 2.3
    ripple = 0.017 * math.sin(23*x + 11*y) + 0.009 * math.sin(47*y - 17*x)
    return 0.27 + 0.18*ridge + ripple


def make_soil():
    rng = random.Random(81203)
    model = reset_scene()
    dry = material("土壤_深棕壤土", (0.19, 0.092, 0.049))
    top_mat = material("垄背_松散褐土", (0.27, 0.137, 0.075))
    wet = material("沟底_湿土", (0.115, 0.066, 0.040), 0.62)
    clod_mats = [
        material("土块_深", (0.17, 0.085, 0.047)),
        material("土块_中", (0.24, 0.118, 0.062)),
        material("土块_浅", (0.31, 0.17, 0.094)),
    ]
    nx, ny = 80, 60
    verts = []
    for j in range(ny+1):
        y = -1.5 + 3*j/ny
        for i in range(nx+1):
            x = -2 + 4*i/nx
            verts.append((x, y, soil_height(x,y)))
    top_count = len(verts)
    verts += [(x,y,0.0) for x,y,_ in verts]
    faces, indices = [], []
    for j in range(ny):
        for i in range(nx):
            a = j*(nx+1)+i
            faces.append((a,a+1,a+nx+2,a+nx+1))
            x = -2+4*(i+0.5)/nx
            ridge = max(0, math.cos(2*math.pi*(x+1.5)))**2.3
            indices.append(1 if ridge>0.25 else 2)
    # The long side and short side form a clean cut cross-section.
    for i in range(nx):
        for a,b in ((i,i+1),(ny*(nx+1)+i,ny*(nx+1)+i+1)):
            faces.append((a,b,b+top_count,a+top_count))
            indices.append(0)
    for j in range(ny):
        for a,b in ((j*(nx+1),(j+1)*(nx+1)),
                    (j*(nx+1)+nx,(j+1)*(nx+1)+nx)):
            faces.append((a,b,b+top_count,a+top_count))
            indices.append(0)
    faces.append(tuple(range(top_count, top_count+nx+1)) +
                 tuple(range(top_count+ny*(nx+1),top_count+(ny+1)*(nx+1))))
    indices.append(0)
    terrain = mesh_obj("垄沟土壤_4米乘3米", verts, faces, model, dry)
    terrain.data.materials.append(top_mat)
    terrain.data.materials.append(wet)
    for poly, idx in zip(terrain.data.polygons, indices):
        poly.material_index = idx
    for k in range(260):
        x, y = rng.uniform(-1.93,1.93), rng.uniform(-1.43,1.43)
        z = soil_height(x,y)
        r = rng.uniform(0.012,0.055)
        # More clods on raised rows; wet furrows stay smoother.
        ridge = max(0, math.cos(2*math.pi*(x+1.5)))**2.3
        if rng.random() > 0.25+0.70*ridge:
            continue
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r,
                                              location=(x,y,z+r*0.25))
        obj = bpy.context.object
        obj.name = f"土块_{k:03d}"
        obj.scale = (rng.uniform(1.0,1.6),rng.uniform(0.7,1.25),rng.uniform(0.45,0.9))
        obj.data.materials.append(rng.choice(clod_mats))
        put_in(obj, model)
    model["资产类别"] = "田地土壤"
    model["尺寸米"] = "4 x 3"
    model["垄数"] = 4
    return model, 4.0


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def set_preview(model, stage, height):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new("预览环境光")
    scene.world.color = (0.11, 0.11, 0.11)
    preview = bpy.data.collections.new("PREVIEW_ONLY_灯光相机")
    scene.collection.children.link(preview)
    bpy.ops.object.camera_add()
    camera = put_in(bpy.context.object, preview)
    camera.name = "预览相机"
    if stage == "土地":
        camera.location = (5.7,-6.5,5.5)
        look_at(camera, (0,0,0.22))
        camera.data.ortho_scale = 6.6
    else:
        normal_height = {"幼苗": 0.36, "成长": 1.30, "成熟": 1.95}[stage]
        camera.location = (2.4,-4.8,max(1.0,normal_height*0.9))
        look_at(camera, (0,0,normal_height*0.52))
        # One framing per stage keeps good/normal/poor visually comparable.
        camera.data.ortho_scale = {"幼苗": 0.98, "成长": 2.30,
                                   "成熟": 3.08}[stage]
    camera.data.type = "ORTHO"
    scene.camera = camera
    for name, loc, power, size in (
        ("主光", (3,-4,6), 550, 5),
        ("补光", (-4,2,4), 350, 4),
    ):
        bpy.ops.object.light_add(type="AREA", location=loc)
        obj = put_in(bpy.context.object, preview)
        obj.name = name
        obj.data.energy = power
        obj.data.shape = "DISK"
        obj.data.size = size
        look_at(obj, (0,0,height*0.5 if stage != "土地" else 0))
    scene.view_settings.view_transform = "Standard"
    return preview


def export_asset(folder, filename, model, stage, height):
    folder.mkdir(parents=True, exist_ok=True)
    preview = set_preview(model, stage, height)
    scene = bpy.context.scene
    scene.render.filepath = str(folder / "预览.png")
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / (filename + ".blend")))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in model.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = next(iter(model.objects))
    bpy.ops.export_scene.gltf(filepath=str(folder / (filename + ".glb")),
                              export_format="GLB", use_selection=True,
                              export_apply=True)
    print("DELIVERED", folder, "objects", len(model.objects))


def main():
    soil, span = make_soil()
    export_asset(ROOT / "土地", "玉米田土壤_四垄", soil, "土地", span)
    for stage in ("幼苗", "成长", "成熟"):
        for condition in ("正常", "好", "差"):
            model, height = make_plant(stage, condition)
            export_asset(ROOT / stage / condition, f"玉米_{stage}_{condition}",
                         model, stage, height)
    print("ALL_DONE", ROOT)


if __name__ == "__main__":
    main()
