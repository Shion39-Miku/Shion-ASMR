"""Generate our own synthetic codec fixtures; never download media.
Optional test dependency: pip install imageio-ffmpeg (or pass --ffmpeg).
Not a player runtime dependency and not distributed in the portable app.
"""
import argparse
from pathlib import Path
import subprocess

args=argparse.ArgumentParser()
args.add_argument('--ffmpeg')
options=args.parse_args()
exe=options.ffmpeg
if not exe:
    import imageio_ffmpeg
    exe=imageio_ffmpeg.get_ffmpeg_exe()
root=Path(__file__).resolve().parent.parent/'test-fixtures'
root.mkdir(exist_ok=True)
subprocess.run([exe,'-y','-f','lavfi','-i','testsrc2=size=640x360:rate=24','-f','lavfi','-i','sine=frequency=330:sample_rate=48000','-t','25','-c:v','libx264','-preset','fast','-pix_fmt','yuv420p','-c:a','aac','-ac','2','-movflags','+faststart',str(root/'generated.mp4')],check=True)
subprocess.run([exe,'-y','-i',str(root/'generated.mp4'),'-t','5','-c:v','wmv2','-c:a','wmav2',str(root/'generated.wmv')],check=True)
