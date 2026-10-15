"""Losslessly encode modern preview strips; original Windows skins remain PNG."""
from pathlib import Path
import json
import hashlib
from PIL import Image

root = Path(__file__).resolve().parents[1] / 'web' / 'assets'
converted = 0
for manifest in root.glob('*/pet.json'):
    data = json.loads(manifest.read_text(encoding='utf-8-sig'))
    if data.get('pixelArt', True):
        continue
    shared = {}
    for state in data['states'].values():
        for key in ('file', 'leftFile'):
            if key not in state:
                continue
            source = manifest.parent / state[key]
            target = source.with_suffix('.webp')
            if source.suffix != '.webp':
                temporary = target.with_suffix('.webp.tmp')
                with Image.open(source) as image:
                    image.save(temporary, format='WEBP', lossless=True, quality=100, method=6, exact=True)
                    with Image.open(temporary) as check:
                        assert check.size == image.size
                        assert check.convert('RGBA').tobytes() == image.convert('RGBA').tobytes()
                temporary.replace(target)
                source.unlink()
                converted += 1
            digest = hashlib.sha256(target.read_bytes()).hexdigest()
            if digest in shared and shared[digest] != target.name:
                state[key] = shared[digest]
                target.unlink()
            else:
                shared[digest] = target.name
                state[key] = target.name
    temporary = manifest.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(data, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    json.loads(temporary.read_text(encoding='utf-8'))
    temporary.replace(manifest)
print(f'Packed {converted} modern strips as verified lossless WebP.')
