"""将已生成、已选定的像素裁切配准为桌宠皮肤，不绘制或重画动物。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import hashlib
import json
from extract_strip_frames import connected_components, component_group_image, remove_chroma_background
from despill_chroma_edges import decontaminate_image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = ROOT / 'pets' / 'nuomi'
QA = ROOT / '_work/qa/pose-scale/nuomi'
OUT.mkdir(parents=True, exist_ok=True)
QA.mkdir(parents=True, exist_ok=True)
SIZE = (384, 512)
BASELINE = 500
audit = []
edge_audit = []


def neutralize_fringe(pose):
    # 二次过滤只作用于轮廓附近的粉紫残色，不侵蚀alpha，也不动内部粉鼻和肉垫。
    # 紫色溢色的R/B都高于G，天然棕毛和蓝眼不符合此条件。
    boundary = pose.getchannel('A').filter(ImageFilter.MinFilter(9))
    output = pose.copy()
    pixels = output.load()
    for y in range(pose.height):
        for x in range(pose.width):
            r, g, b, a = pixels[x, y]
            if a and boundary.getpixel((x, y)) < 250 and min(r, b) - g > 6:
                pixels[x, y] = (r, (r + b) // 2, b, a)
    return output


def clean_edges(pose, label):
    # 色键只去掉背景还不够：原图抗锯齿像素混入紫色，需要从邻近毛色恢复RGB。
    # 透明度逐像素保持，保护细毛和天然白边；pad使裁切四边也能识别到透明外侧。
    padding = 6
    padded = Image.new('RGBA', (pose.width + padding * 2, pose.height + padding * 2))
    padded.alpha_composite(pose, (padding, padding))
    cleaned, result = decontaminate_image(padded, chroma_key=(255, 0, 255), edge_radius=5,
                                         minimum_saturation=.03, spill_tolerance=.70)
    cleaned = cleaned.crop((padding, padding, padding + pose.width, padding + pose.height))
    assert cleaned.getchannel('A').tobytes() == pose.getchannel('A').tobytes()
    edge_audit.append({'group': label, 'changedPixels': result['changed_pixels'], 'alphaPreserved': True})
    return neutralize_fringe(cleaned)


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_bytes((json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf8'))
    assert json.loads(temporary.read_text(encoding='utf8')) == value
    temporary.replace(path)


def poses(name, count=4, archived=False):
    if archived:
        # 从只读导入档案逐字节提取并保留的生成大图，不依赖盘外路径或整个213MiB归档。
        with Image.open(HERE / 'highres' / name) as file:
            source = remove_chroma_background(file, (255, 0, 255), 96)
    else:
        with Image.open(HERE / name) as file:
            source = file.convert('RGBA')
    components = connected_components(source)
    main = sorted(components, key=lambda item: item['area'], reverse=True)[:count]
    assert len(main) == count and all(item['area'] > 50000 for item in main)
    main.sort(key=lambda item: item['center_x'])
    for item in main:
        x0, y0, x1, y1 = item['bbox']
        assert 0 < x0 < x1 < source.width and 0 < y0 < y1 < source.height
    return [clean_edges(component_group_image(source, [item], padding=0), name) for item in main], main


sleep, sleep_components = poses('sleep-strip.png')
air, air_components = poses('airborne-strip.png')
idle_source, _ = poses('idle.png', 6, archived=True)
wave_source, _ = poses('waving.png', 4, archived=True)
# 以头颅宽度/眼距和躯干配准整行动作，绝不逐帧拉伸或按总身高归一化。
# idle头宽262px、happy约374px、air约414px，统一到约180px源帧头宽。
# 96x128旧画布变为四倍像素；45%显示仍为173x230，保持原静置体感。
POSE_SCALE = {'idle': .70, 'happy': .485, 'sleep': .50, 'air': .425}


def register(frames, state):
    output = []
    scale = POSE_SCALE[state]
    for pose in frames:
        fitted = neutralize_fringe(pose.resize((round(pose.width * scale), round(pose.height * scale)), Image.Resampling.LANCZOS))
        cell = Image.new('RGBA', SIZE)
        cell.alpha_composite(fitted, ((SIZE[0] - fitted.width) // 2, BASELINE - fitted.height))
        output.append(cell)
        audit.append({'group': state, 'frame': len(output)-1, 'sourceBody': list(pose.size),
                      'sharedScale': scale, 'outputBody': list(fitted.size)})
    return output


with Image.open(HERE.parent / 'original' / 'spritesheet.png') as source:
    assert source.size == (1536, 2288)
    atlas = source.convert('RGBA')


def row(number, count):
    frames = []
    for index in range(count):
        cell = atlas.crop((index * 192, number * 208, (index + 1) * 192, (number + 1) * 208))
        # 左右步态的已批准修复版仅有192px；保留此源的最高可用质量。
        resized = neutralize_fringe(clean_edges(cell, 'approved-gait').resize((384, 416), Image.Resampling.LANCZOS))
        frame = Image.new('RGBA', SIZE)
        frame.alpha_composite(resized, (0, 96))
        frames.append(frame)
    return frames


idle, wave = register(idle_source, 'idle'), register(wave_source, 'happy')
right, left = row(1, 8), row(2, 8)
registered_sleep, registered_air = register(sleep, 'sleep'), register(air, 'air')
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


manifest = {'name': '糯米', 'frameSize': list(SIZE), 'scale': 1, 'baseSizePercent': 45,
            'pixelArt': False, 'playful': False, 'facing': 'right', 'states': {}}
for name, (frames, fps, loop) in states.items():
    strip(name, frames)
    entry = {'file': name + '.png', 'frames': len(frames), 'fps': fps, 'loop': loop}
    # 姿态尺度已烘焙进高清帧，运行时只负责用户选择的整体大小。
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

summary = {'ok': True, 'frameSize': list(SIZE), 'sharedPoseScales': POSE_SCALE,
           'frames': audit, 'sourceProvenance': 'assets-src/nuomi/desktop-source/highres/source-provenance.json',
           'edgeCleanup': edge_audit,
           'sourceResolution': 'idle and happy re-extracted from original 2172px generated rows; sleep/air original large strips; approved gait retains its 192px native quality',
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
