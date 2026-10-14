"""Read-only validation of the delivered GLBs in Blender."""

from pathlib import Path
import bpy
from mathutils import Vector

root = Path(r"C:\Users\H-CHILD\Documents\Codex\2026-09-27\woy\outputs\玉米与土壤_Blender模型_v1")
files = sorted(root.rglob("*.glb"))
assert len(files) == 10, f"Expected ten GLBs, got {len(files)}"

for path in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    assert meshes, f"No mesh in {path}"
    materials = {m.name for o in meshes for m in o.data.materials if m}
    assert materials, f"No materials in {path}"
    points = [o.matrix_world @ Vector(corner) for o in meshes for corner in o.bound_box]
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    bounds = tuple(round(hi[i]-lo[i], 3) for i in range(3))
    assert all(d > 0.01 for d in bounds), f"Invalid bounds: {path} {bounds}"
    name = path.stem
    ears = sum("雌穗_" in o.name and "苞叶" in o.name for o in meshes)
    tassels = sum("顶部雄穗主轴" in o.name for o in meshes)
    if "成熟" in name:
        expected_ears = 2 if "_好" in name else 1
        assert ears == expected_ears, (name, ears)
        assert tassels == 1, (name, tassels)
    elif "玉米" in name:
        assert ears == 0 and tassels == 0, (name, ears, tassels)
    print("PASS", str(path.relative_to(root)), "meshes", len(meshes),
          "materials", len(materials), "size_m", bounds)

print("VALIDATION_COMPLETE", len(files))
