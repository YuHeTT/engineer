import bpy
from pathlib import Path
OUT=Path(__file__).resolve().parent
S=bpy.context.scene
S.camera=bpy.data.objects['CAM_Exterior'];S.frame_set(1)
S.render.resolution_x=960;S.render.resolution_y=640;S.render.resolution_percentage=100
S.render.film_transparent=False
S.world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.064,.08,1)
S.render.image_settings.media_type='VIDEO';S.render.image_settings.file_format='FFMPEG'
S.render.ffmpeg.format='MPEG4';S.render.ffmpeg.codec='H264'
S.render.ffmpeg.constant_rate_factor='MEDIUM';S.render.ffmpeg.ffmpeg_preset='GOOD'
S.render.fps=15;S.frame_step=2;S.frame_start=1;S.frame_end=180
S.render.filepath=str(OUT/'房车开合演示.mp4')
bpy.ops.render.render(animation=True)
print('VIDEO_RENDER_OK',flush=True)
