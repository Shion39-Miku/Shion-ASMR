"""Optional test-only dependency: pip install soundfile numpy. Never downloads media."""
import sys
from pathlib import Path
import numpy as np
import soundfile as sf

out = Path(sys.argv[1] if len(sys.argv) > 1 else 'test-fixtures')
out.mkdir(parents=True, exist_ok=True)
t = np.arange(48000 * 4) / 48000
stereo = np.column_stack([np.sin(t * np.pi * 440), np.sin(t * np.pi * 880)]) * 0.025
sf.write(out / 'generated.flac', stereo, 48000, subtype='PCM_16')
sf.write(out / 'generated.ogg', stereo, 48000, subtype='VORBIS')
print(out.resolve())
