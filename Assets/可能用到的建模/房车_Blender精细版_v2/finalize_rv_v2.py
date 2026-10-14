import bpy,json
from pathlib import Path
OUT=Path(__file__).resolve().parent
S=bpy.context.scene
S.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for c in bpy.data.collections:
    if c.name.startswith(('90_','99_')):continue
    for o in c.objects:o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'智能农田房车_精细可开合_v2.glb'),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='SCENE',export_anim_scene_split_object=False,export_frame_range=True,export_force_sampling=True,export_apply=True,export_extras=True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'智能农田房车_精细可开合_v2.fbx'),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,add_leaf_bones=False)
S.camera=bpy.data.objects['CAM_Exterior']
for frame,name in [(35,'08_opening_mid.png'),(138,'09_closing_mid.png'),(180,'10_reclosed.png')]:
    S.frame_set(frame);S.render.filepath=str(OUT/'renders'/name);bpy.ops.render.render(write_still=True)
S.frame_set(1)
S.render.resolution_x=960;S.render.resolution_y=640
S.render.resolution_percentage=100
S.render.film_transparent=False
S.world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.064,.08,1)
S.render.image_settings.media_type='VIDEO'
S.render.image_settings.file_format='FFMPEG'
S.render.ffmpeg.format='MPEG4';S.render.ffmpeg.codec='H264'
S.render.ffmpeg.constant_rate_factor='MEDIUM';S.render.ffmpeg.ffmpeg_preset='GOOD'
S.render.fps=15;S.frame_step=2
S.render.filepath=str(OUT/'房车开合演示.mp4')
bpy.ops.render.render(animation=True)
print('FINAL_EXPORTS_AND_VIDEO_OK',flush=True)
