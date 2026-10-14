"""将已生成、已选定的像素裁切配准为桌宠皮肤，不绘制或重画动物。"""
from pathlib import Path
from PIL import Image, ImageDraw
import hashlib
import json
from extract_strip_frames import connected_components, component_group_image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = ROOT / 'pets' / 'nuomi'
QA = ROOT / '_work' / 'nuomi-desktop-qa'
OUT.mkdir(parents=True, exist_ok=True)
QA.mkdir(parents=True, exist_ok=True)
SIZE = (96, 128)
BASELINE = 125


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_bytes((json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf8'))
    assert json.loads(temporary.read_text(encoding='utf8')) == value
    temporary.replace(path)


def poses(name):
    with Image.open(HERE / name) as file:
        source = file.convert('RGBA')
    components = connected_components(source)
    main = sorted(components, key=lambda item: item['area'], reverse=True)[:4]
    assert len(main) == 4 and all(item['area'] > 100000 for item in main)
    main.sort(key=lambda item: item['center_x'])
    for item in main:
        x0, y0, x1, y1 = item['bbox']
        assert 0 < x0 < x1 < source.width and 0 < y0 < y1 < source.height
    return [component_group_image(source, [item], padding=0) for item in main], main


sleep, sleep_components = poses('sleep-strip.png')
air, air_components = poses('airborne-strip.png')
# 所有新增姿态共用一个比例；仅去除透明噪声及主轮廓之外的碎片。
scale = min(92 / max(p.width for p in sleep + air), 116 / max(p.height for p in sleep + air))


def register(frames):
    output = []
    for pose in frames:
        fitted = pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS)
        cell = Image.new('RGBA', SIZE)
        cell.alpha_composite(fitted, ((SIZE[0] - fitted.width) // 2, BASELINE - fitted.height))
        output.append(cell)
    return output


with Image.open(HERE.parent / 'original' / 'spritesheet.png') as source:
    assert source.size == (1536, 2288)
    atlas = source.convert('RGBA')


def row(number, count):
    frames = []
    for index in range(count):
        cell = atlas.crop((index * 192, number * 208, (index + 1) * 192, (number + 1) * 208))
        resized = cell.resize((96, 104), Image.Resampling.LANCZOS)
        frame = Image.new('RGBA', SIZE)
        frame.alpha_composite(resized, (0, 24))
        frames.append(frame)
    return frames


idle, right, left, wave = row(0, 6), row(1, 8), row(2, 8), row(3, 4)
registered_sleep, registered_air = register(sleep), register(air)
states = {
    'idle': (idle, 5, True),
    'walk': (right, 4, True),
    'run': (right, 8, True),
    'sit': (idle, 4, True),
    'sleep': (registered_sleep, 2, True),
    'drag': (registered_air[:2], 3, True),
    'fall': (registered_air[2:], 4, True),
    'happy': (wave, 4, False),
}


def strip(name, frames):
    image = Image.new('RGBA', (SIZE[0] * len(frames), SIZE[1]))
    for index, frame in enumerate(frames):
        # 清除全透明像素残留RGB，保存确定性裁切结果。
        cleaned = Image.new('RGBA', SIZE)
        cleaned.putdata([rgba if rgba[3] else (0, 0, 0, 0) for rgba in frame.getdata()])
        image.alpha_composite(cleaned, (index * SIZE[0], 0))
    path = OUT / (name + '.png')
    temporary = path.with_suffix('.png.tmp')
    image.save(temporary, format='PNG')
    with Image.open(temporary) as check:
        assert check.size == (SIZE[0] * len(frames), SIZE[1]) and check.mode == 'RGBA'
    temporary.replace(path)


manifest = {'name': '糯米', 'frameSize': list(SIZE), 'scale': 1, 'baseSizePercent': 180,
            'pixelArt': False, 'playful': False, 'facing': 'right', 'states': {}}
for name, (frames, fps, loop) in states.items():
    strip(name, frames)
    entry = {'file': name + '.png', 'frames': len(frames), 'fps': fps, 'loop': loop}
    # 原图行之间的体型不同；桌面加载时按头部尺度校准，再应用用户的整体大小。
    entry['poseScale'] = {'idle': .70, 'sit': .70, 'sleep': .85,
                          'drag': .78, 'fall': .78, 'happy': .70}.get(name, 1.0)
    if name in ('walk', 'run'):
        strip(name + '-left', left)
        entry['leftFile'] = name + '-left.png'
    manifest['states'][name] = entry
write_json(OUT / 'pet.json', manifest)

sheet = Image.new('RGB', (8 * SIZE[0], len(states) * (SIZE[1] + 24)), '#f4f0ea')
draw = ImageDraw.Draw(sheet)
for y, (name, (frames, _, _)) in enumerate(states.items()):
    top = y * (SIZE[1] + 24)
    draw.text((4, top + 4), name, fill='#383330')
    for x, frame in enumerate(frames):
        sheet.paste(frame, (x * SIZE[0], top + 24), frame)
sheet.save(QA / 'contact-sheet.png')

summary = {'ok': True, 'frameSize': list(SIZE), 'newPoseSharedScale': scale,
           'newSleepPoses': 4, 'newDragPoses': 2, 'newFallPoses': 2,
           'allRuntimeStates': list(states), 'walking': 'approved directional gait at reduced fps and movement speed',
           'happy': 'approved attached-paw greeting response',
           'anatomyReview': 'four limbs in airborne poses; curled sleeping limbs naturally occluded',
           'componentExtractor': 'bundled work-pets 0.1.6 helper, preserved beside this script',
           'runtimeFiles': [{'file': path.name, 'bytes': path.stat().st_size,
                             'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
                            for path in sorted(OUT.glob('*')) if path.is_file()]}
write_json(QA / 'skin-validation.json', summary)
print(json.dumps({key: value for key, value in summary.items() if key != 'runtimeFiles'}, ensure_ascii=False, indent=2))
