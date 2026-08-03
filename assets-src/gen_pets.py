# -*- coding: utf-8 -*-
"""
desk-pets 占位素材生成器
像素图以字符画方式定义（1 字符 = 1 像素），程序拼装为各状态横条 spritesheet。
输出: ../pets/<id>/<state>.png + pet.json ；预览: ../_work/preview.png
运行: python gen_pets.py
"""
import json
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
PETS_DIR = os.path.join(ROOT, "pets")
WORK_DIR = os.path.join(ROOT, "_work")

# ---------------------------------------------------------------- palettes
CAT_PAL = {
    "o": (69, 57, 47, 255),      # outline 暖深棕
    "b": (242, 233, 220, 255),   # 奶白主体
    "s": (215, 200, 180, 255),   # 阴影(远侧腿)
    "d": (126, 106, 92, 255),    # 重点色(耳/尾)
    "w": (252, 249, 243, 255),   # 白(下巴/肚/腿爪)
    "t": (232, 168, 178, 255),   # 粉(内耳)
    "E": (91, 143, 217, 255),    # 蓝眼
    "W": (255, 255, 255, 255),   # 眼高光
    "n": (226, 141, 153, 255),   # 鼻
    "h": (240, 110, 130, 255),   # 爱心
    "z": (140, 165, 205, 255),   # Zzz
}
DOG_PAL = {
    "o": (85, 56, 28, 255),      # outline 深棕
    "b": (226, 168, 92, 255),    # 金色主体
    "s": (192, 135, 66, 255),    # 金色阴影(远侧腿)
    "d": (183, 122, 52, 255),    # 深金(耳/尾羽)
    "w": (244, 220, 168, 255),   # 浅奶油(口吻/胸/爪)
    "t": (235, 130, 120, 255),   # 舌头粉
    "E": (56, 40, 28, 255),      # 深棕眼
    "W": (255, 255, 255, 255),   # 眼高光
    "n": (42, 32, 24, 255),      # 黑鼻
    "h": (240, 110, 130, 255),   # 爱心
    "z": (140, 165, 205, 255),   # Zzz
}

Z_GLYPH = ["zzz", ".z.", "zzz"]  # 3x3 小 Z

# ---------------------------------------------------------------- helpers
def blank(w, h):
    return ["." * w for _ in range(h)]

def splice(base, patch, x, y):
    """把 patch(字符画行列表)覆盖到 base 的 (x,y)，'.' 不覆盖。"""
    out = [list(r) for r in base]
    for dy, row in enumerate(patch):
        for dx, ch in enumerate(row):
            if ch != ".":
                yy, xx = y + dy, x + dx
                if 0 <= yy < len(out) and 0 <= xx < len(out[0]):
                    out[yy][xx] = ch
    return ["".join(r) for r in out]

def shift(m, dx=0, dy=0):
    w, h = len(m[0]), len(m)
    return splice(blank(w, h), m, dx, dy)

def close_eyes(m):
    """眨眼: 每列眼睛像素只保留最下一格改为 outline，其余变主体色。"""
    grid = [list(r) for r in m]
    h, w = len(grid), len(grid[0])
    for x in range(w):
        ys = [y for y in range(h) if grid[y][x] in ("E", "W")]
        if not ys:
            continue
        for y in ys:
            grid[y][x] = "b"
        grid[max(ys)][x] = "o"
    return ["".join(r) for r in grid]

def legs_patch(width, shaft, xs, lifts, fill, paw):
    """
    程序化腿块。xs=(近后,远后,远前,近前) 各腿左缘 x；lifts 同序抬脚像素。
    近腿: o+fill*2+o 宽4,末行前为 paw 色,底行 outline；远腿: 阴影色 2 宽,无 outline,短 1 行。
    patch 高 = shaft+1。
    """
    h = shaft + 1
    rows = [["."] * width for _ in range(h)]
    bn, fb, ff, fn = xs
    lbn, lfb, lff, lfn = lifts
    for x, lift in ((fb, lfb), (ff, lff)):  # 远腿先画(被近腿遮挡)
        top, bot = 0, shaft - 1 - lift
        for r in range(top, bot + 1):
            for dx in (0, 1):
                rows[r][x + dx] = "s"
    for x, lift in ((bn, lbn), (fn, lfn)):  # 近腿
        bot = shaft - lift            # 底行 outline 所在行
        for r in range(0, bot):
            rows[r][x] = "o"
            rows[r][x + 1] = fill if r < bot - 1 else paw
            rows[r][x + 2] = fill if r < bot - 1 else paw
            rows[r][x + 3] = "o"
        for dx in range(4):
            rows[bot][x + dx] = "o"
    return ["".join(r) for r in rows]

def render(m, pal):
    h, w = len(m), len(m[0])
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(m):
        for x, ch in enumerate(row):
            if ch != ".":
                if ch not in pal:
                    raise ValueError("unknown pixel char %r at (%d,%d)" % (ch, x, y))
                px[x, y] = pal[ch]
    return img

def validate(maps, w, h, name):
    for i, m in enumerate(maps):
        assert len(m) == h, "%s frame %d: %d rows != %d" % (name, i, len(m), h)
        for j, row in enumerate(m):
            assert len(row) == w, "%s frame %d row %d: len %d != %d" % (name, i, j, len(row), w)

# ================================================================ 布偶猫 小布
# 画布 32x24。三分头身:头右侧朝向观者,身体侧面,尾在最左。地面=第23行。
CW, CH = 32, 24

CAT_BASE = [  # 头+身体(无尾无腿) 行0-17
    "................................",
    "..................oo.....oo.....",
    ".................oddo...oddo....",
    ".................odtdo.odtdo....",
    ".................oddddoddddo....",
    "................obbbbbbbbbbbo...",
    "...............obbbbbbbbbbbbbo..",
    "...............obbEEbbbbbEEbbo..",
    "...............obbEEbbbbbEEbbo..",
    "...............obbbbbwwwbbbbbo..",
    "...............obbbbwnnwwbbbbo..",
    "................obbbwwowwbbbo...",
    "......ooooooooooobbbbwwwbbbbo...",
    ".....obbbbbbbbbbbbbbbbbbbbbo....",
    "....obbbbbbbbbbbbbbbbbbbbbo.....",
    "....obbbbbbbbbbbbbbbbbbbo.......",
    "....obbbwwwwwwwwwwwbbbbo........",
    ".....obbwwwwwwwwwwwbbbo.........",
]

CAT_TAIL_A = [  # 拼到 (0,4)
    "..oo..",
    ".oddo.",
    ".oddo.",
    ".oddo.",
    ".odddo",
    ".odddo",
    "..oddo",
    "..oddo",
    "..oddo",
    "...odo",
    "...odo",
    "....oo",
]
CAT_TAIL_B = [  # 尾尖摆动
    "......",
    ".oo...",
    "oddo..",
    "oddo..",
    ".oddo.",
    ".odddo",
    "..oddo",
    "..oddo",
    "..oddo",
    "...odo",
    "...odo",
    "....oo",
]

CAT_LEG_XS = (4, 10, 13, 17)   # 站立基准: 近后/远后/远前/近前 左缘
CAT_SHAFT = 5                   # 腿区拼到 y=18, 占 18-23

def cat_legs(xs, lifts):
    return legs_patch(CW, CAT_SHAFT, xs, lifts, "w", "w")

def cat_frame(tail, legs, blink=False, dy=0):
    m = splice(blank(CW, CH), CAT_BASE, 0, 0)
    m = splice(m, tail, 0, 4)
    if dy:
        m = shift(m, 0, dy)
    m = splice(m, legs, 0, 18 + dy)
    if blink:
        m = close_eyes(m)
    return m

CAT_SIT = [
    "................................",
    "..................oo.....oo.....",
    ".................oddo...oddo....",
    ".................odtdo.odtdo....",
    ".................oddddoddddo....",
    "................obbbbbbbbbbbo...",
    "...............obbbbbbbbbbbbbo..",
    "...............obbEEbbbbbEEbbo..",
    "...............obbEEbbbbbEEbbo..",
    "...............obbbbbwwwbbbbbo..",
    "...............obbbbwnnwwbbbbo..",
    "................obbbwwowwbbbo...",
    "................obbbbwwwbbbo....",
    "...............obbbbbbbbbbbo....",
    "..............obbbbbbbbbbbo.....",
    ".............obbbbwwwwbbbbo.....",
    ".............obbwwwwwwwbbbo.....",
    "............obbwwwwwwwwwbbo.....",
    "............obbwwwwwwwwwbbo.....",
    "...........obbwwwwwwwwwwbbo.....",
    "...........obbwwwwwwwwwwbbo.....",
    "..ooo......obbwwwwwwwwwwbbo.....",
    ".odddooooooobwwwoowwwoowwbo.....",
    "..ooooooooooooooooooooooooo.....",
]
CAT_SIT_FLICK = [  # 尾尖上翘变体 拼到 (1,19)
    "..oo..",
    ".oddo.",
    "odddoo",
    ".ooo..",
]

CAT_SLEEP = [
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "..................oo....oo......",
    ".................oddo..oddo.....",
    ".................odddooddddo....",
    "..ooo...........obbbbbbbbbbbo...",
    ".odddo.........obbbbbbbbbbbbbo..",
    ".odddoooooooooobbbbbbbbbbbbbbo..",
    ".oddobbbbbbbbbbbbbboobbbboobbo..",
    "..oobbbbbbbbbbbbbbbbbwwwbbbbo...",
    "...obbbbbbbbbbbbbbbbwnnwwbbbo...",
    "...obbbbbbbbbbbbbbbbbwwwbbbo....",
    "....obbbbbbbbbbbbbbbbbbbbbo.....",
    ".....ooooooooooooooooooooo......",
    "................................",
    "................................",
    "................................",
    "................................",
]

def build_cat():
    stand = CAT_LEG_XS
    idle = [
        cat_frame(CAT_TAIL_A, cat_legs(stand, (0, 0, 0, 0))),
        cat_frame(CAT_TAIL_B, cat_legs(stand, (0, 0, 0, 0))),
        cat_frame(CAT_TAIL_A, cat_legs(stand, (0, 0, 0, 0)), blink=True),
        cat_frame(CAT_TAIL_A, cat_legs(stand, (0, 0, 0, 0))),
    ]
    walk = [
        cat_frame(CAT_TAIL_A, cat_legs((5, 9, 14, 16), (1, 0, 1, 0))),
        cat_frame(CAT_TAIL_B, cat_legs(stand, (0, 0, 0, 0))),
        cat_frame(CAT_TAIL_A, cat_legs((3, 11, 12, 18), (0, 1, 0, 1))),
        cat_frame(CAT_TAIL_B, cat_legs(stand, (0, 0, 0, 0))),
    ]
    run = [
        cat_frame(CAT_TAIL_B, cat_legs((2, 8, 15, 19), (0, 0, 0, 0)), dy=-1),
        cat_frame(CAT_TAIL_A, cat_legs((6, 10, 13, 15), (1, 1, 1, 1)), dy=1),
        cat_frame(CAT_TAIL_B, cat_legs((2, 8, 15, 19), (0, 0, 0, 0))),
        cat_frame(CAT_TAIL_A, cat_legs((6, 10, 13, 15), (1, 1, 1, 1))),
    ]
    sit_a = CAT_SIT
    sit_b = splice(CAT_SIT, CAT_SIT_FLICK, 1, 19)
    sleep_a = splice(CAT_SLEEP, Z_GLYPH, 26, 4)
    sleep_b = splice(CAT_SLEEP, Z_GLYPH, 24, 2)
    drag = [
        cat_frame(CAT_TAIL_A, cat_legs((6, 10, 13, 16), (0, 1, 0, 1))),
        cat_frame(CAT_TAIL_B, cat_legs((5, 11, 12, 17), (1, 0, 1, 0))),
    ]
    fall = [
        cat_frame(CAT_TAIL_B, cat_legs((2, 9, 14, 19), (1, 1, 1, 1))),
        cat_frame(CAT_TAIL_A, cat_legs((3, 9, 14, 18), (2, 2, 2, 2))),
    ]
    heart = [
        ".hh.hh.",
        "hhhhhhh",
        ".hhhhh.",
        "..hhh..",
        "...h...",
    ]
    happy = [
        splice(cat_frame(CAT_TAIL_B, cat_legs((6, 10, 13, 15), (1, 1, 1, 1)), dy=-1), heart, 12, 0),
        splice(cat_frame(CAT_TAIL_A, cat_legs(stand, (0, 0, 0, 0))), heart, 12, 1),
    ]
    states = {
        "idle": (idle, 3, True),
        "walk": (walk, 8, True),
        "run": (run, 10, True),
        "sit": ([sit_a, sit_b], 2, True),
        "sleep": ([sleep_a, sleep_b], 2, True),
        "drag": (drag, 8, True),
        "fall": (fall, 10, True),
        "happy": (happy, 5, True),
    }
    return "ragdoll", "小布", (CW, CH), CAT_PAL, states

# ================================================================ 金毛 阿金
# 画布 36x26。垂耳、奶油口吻、上翘羽状尾。地面=第25行。
DW, DH = 36, 26

DOG_BASE = [  # 行0-18
    "....................................",
    "....................................",
    "...................ooooooooo........",
    "..................obbbbbbbbbo.......",
    ".................obbbbbbbbbbbo......",
    "................oddbbbbbbbbbddo.....",
    "...............oddobbbbbbbboddo.....",
    "...............oddobEEbbbEEoddo.....",
    "...............oddobEEbbbEEoddo.....",
    "...............oddobbwwwwwoddo......",
    "................ooobwwnnwwooo.......",
    "..................obwwnnwwbo........",
    "..................obwwwowwbo........",
    "......oooooooooooobbwwwwwbbo........",
    ".....obbbbbbbbbbbbbbbbbbbbbo........",
    "....obbbbbbbbbbbbbbbbbbbbbo.........",
    "....obbbbbbbbbbbbbbbbbbbo...........",
    "....obbbwwwwwwwwwwwbbbbo............",
    ".....obbwwwwwwwwwwwbbbo.............",
]

DOG_TAIL_A = [  # 拼到 (0,5)
    "....oo.",
    "...oddo",
    "..oddo.",
    "..oddo.",
    ".odddo.",
    ".oddo..",
    ".oddo..",
    "..oddo.",
    "..odddo",
    "...oddo",
    "....oo.",
]
DOG_TAIL_B = [  # 摇尾
    "oo.....",
    "oddo...",
    ".oddo..",
    ".oddo..",
    ".odddo.",
    "..oddo.",
    "..oddo.",
    "..oddo.",
    "..oddo.",
    "...oo..",
    ".......",
]

DOG_LEG_XS = (5, 11, 14, 18)
DOG_SHAFT = 6                   # 拼到 y=19, 占 19-25

def dog_legs(xs, lifts):
    return legs_patch(DW, DOG_SHAFT, xs, lifts, "b", "w")

def dog_frame(tail, legs, blink=False, tongue=False, dy=0):
    m = splice(blank(DW, DH), DOG_BASE, 0, 0)
    m = splice(m, tail, 0, 5)
    if tongue:
        m = splice(m, ["tt", "tt", ".t"], 21, 13)
    if dy:
        m = shift(m, 0, dy)
    m = splice(m, legs, 0, 19 + dy)
    if blink:
        m = close_eyes(m)
    return m

DOG_SIT = [
    "....................................",
    "....................................",
    "...................ooooooooo........",
    "..................obbbbbbbbbo.......",
    ".................obbbbbbbbbbbo......",
    "................oddbbbbbbbbbddo.....",
    "...............oddobbbbbbbboddo.....",
    "...............oddobEEbbbEEoddo.....",
    "...............oddobEEbbbEEoddo.....",
    "...............oddobbwwwwwoddo......",
    "................ooobwwnnwwooo.......",
    "..................obwwnnwwbo........",
    "..................obwwwowwbo........",
    ".................obbbwwwwwbbo.......",
    "................obbbbbbbbbbbo.......",
    "...............obbbbbbbbbbbbo.......",
    "..............obbbbbbbbbbbbbo.......",
    "..............obbwwwwwwwwbbbo.......",
    "..............obwwwwwwwwwwbbo.......",
    "..............obwwwwwwwwwwbbo.......",
    "..............obwwwwwwwwwwbbo.......",
    "...ooo........obwwwwwwwwwwbbo.......",
    "..odddooooooooobwwwoowwwoowbo.......",
    "...ooooooooooooooooooooooooo........",
    "....................................",
    "....................................",
]
DOG_SIT_FLICK = [  # 拼到 (2,19)
    "..oo..",
    ".oddo.",
    "odddoo",
    ".ooo..",
]

DOG_SLEEP = [
    "....................................",
    "....................................",
    "....................................",
    "....................................",
    "....................................",
    "....................................",
    "....................................",
    "....................................",
    "....................................",
    "...................ooooooooo........",
    "..................obbbbbbbbbo.......",
    ".................obbbbbbbbbbbo......",
    "..oo............oddbbbbbbbbbddo.....",
    ".oddo..........oddobbbbbbbboddo.....",
    ".oddoooooooooooddobooboobooddo......",
    ".oddbbbbbbbbbbbbbbbbwwwwwoddo.......",
    "..obbbbbbbbbbbbbbbbwwnnwwooo........",
    "..obbbbbbbbbbbbbbbbwwwwwwbo.........",
    "...obbbbbbbbbbbbbbbbwwwwbbo.........",
    "....obbbbbbbbbbbbbbbbbbbbo..........",
    ".....oooooooooooooooooooo...........",
    "....................................",
    "....................................",
    "....................................",
    "....................................",
    "....................................",
]

def build_dog():
    stand = DOG_LEG_XS
    idle = [
        dog_frame(DOG_TAIL_A, dog_legs(stand, (0, 0, 0, 0))),
        dog_frame(DOG_TAIL_B, dog_legs(stand, (0, 0, 0, 0))),
        dog_frame(DOG_TAIL_A, dog_legs(stand, (0, 0, 0, 0)), blink=True),
        dog_frame(DOG_TAIL_B, dog_legs(stand, (0, 0, 0, 0))),
    ]
    walk = [
        dog_frame(DOG_TAIL_A, dog_legs((6, 10, 15, 17), (1, 0, 1, 0))),
        dog_frame(DOG_TAIL_B, dog_legs(stand, (0, 0, 0, 0))),
        dog_frame(DOG_TAIL_A, dog_legs((4, 12, 13, 19), (0, 1, 0, 1))),
        dog_frame(DOG_TAIL_B, dog_legs(stand, (0, 0, 0, 0))),
    ]
    run = [
        dog_frame(DOG_TAIL_B, dog_legs((2, 9, 16, 21), (0, 0, 0, 0)), tongue=True, dy=-1),
        dog_frame(DOG_TAIL_A, dog_legs((7, 11, 14, 16), (1, 1, 1, 1)), tongue=True, dy=1),
        dog_frame(DOG_TAIL_B, dog_legs((2, 9, 16, 21), (0, 0, 0, 0)), tongue=True),
        dog_frame(DOG_TAIL_A, dog_legs((7, 11, 14, 16), (1, 1, 1, 1)), tongue=True),
    ]
    sit_b = splice(DOG_SIT, DOG_SIT_FLICK, 2, 19)
    sleep_a = splice(DOG_SLEEP, Z_GLYPH, 30, 5)
    sleep_b = splice(DOG_SLEEP, Z_GLYPH, 28, 3)
    drag = [
        dog_frame(DOG_TAIL_A, dog_legs((7, 11, 14, 17), (0, 1, 0, 1))),
        dog_frame(DOG_TAIL_B, dog_legs((6, 12, 13, 18), (1, 0, 1, 0))),
    ]
    fall = [
        dog_frame(DOG_TAIL_B, dog_legs((3, 10, 15, 20), (1, 1, 1, 1))),
        dog_frame(DOG_TAIL_A, dog_legs((4, 10, 15, 19), (2, 2, 2, 2))),
    ]
    heart = [
        ".hh.hh.",
        "hhhhhhh",
        ".hhhhh.",
        "..hhh..",
        "...h...",
    ]
    happy = [
        splice(dog_frame(DOG_TAIL_B, dog_legs((7, 11, 14, 16), (1, 1, 1, 1)), tongue=True, dy=-2), heart, 14, 0),
        splice(dog_frame(DOG_TAIL_A, dog_legs(stand, (0, 0, 0, 0)), tongue=True), heart, 14, 1),
    ]
    states = {
        "idle": (idle, 3, True),
        "walk": (walk, 8, True),
        "run": (run, 10, True),
        "sit": ([DOG_SIT, sit_b], 2, True),
        "sleep": ([sleep_a, sleep_b], 2, True),
        "drag": (drag, 8, True),
        "fall": (fall, 10, True),
        "happy": (happy, 5, True),
    }
    return "golden", "阿金", (DW, DH), DOG_PAL, states

# ================================================================ 输出
def emit(pet):
    pid, name, (w, h), pal, states = pet
    outdir = os.path.join(PETS_DIR, pid)
    os.makedirs(outdir, exist_ok=True)
    manifest = {
        "name": name,
        "frameSize": [w, h],
        "scale": 3,
        "facing": "right",
        "states": {},
    }
    for st, (frames, fps, loop) in states.items():
        validate(frames, w, h, "%s.%s" % (pid, st))
        strip = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
        for i, m in enumerate(frames):
            strip.paste(render(m, pal), (i * w, 0))
        strip.save(os.path.join(outdir, st + ".png"))
        manifest["states"][st] = {"file": st + ".png", "frames": len(frames), "fps": fps, "loop": loop}
    with open(os.path.join(outdir, "pet.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    return pid, states

def preview(pets, scale=5):
    rows = []
    total_w, total_h = 0, 0
    for pid, states in pets:
        for st, (frames, _, _) in states.items():
            fw = (len(frames[0][0]) + 1) * len(frames)
            fh = len(frames[0])
            rows.append((pid, st, frames))
            total_w = max(total_w, fw)
            total_h += fh + 1
    img = Image.new("RGBA", ((total_w + 2) * scale, (total_h + 2) * scale), (255, 255, 255, 255))
    dr = ImageDraw.Draw(img)
    cs = scale * 2
    for y in range(0, img.height, cs):
        for x in range(0, img.width, cs):
            if (x // cs + y // cs) % 2 == 0:
                dr.rectangle([x, y, x + cs - 1, y + cs - 1], fill=(233, 233, 238, 255))
    y = scale
    for pid, st, frames in rows:
        pal = CAT_PAL if pid == "ragdoll" else DOG_PAL
        x = scale
        for m in frames:
            fr = render(m, pal)
            fr = fr.resize((fr.width * scale, fr.height * scale), Image.NEAREST)
            img.alpha_composite(fr, (x, y))
            x += fr.width + scale
        y += (len(frames[0]) + 1) * scale
    os.makedirs(WORK_DIR, exist_ok=True)
    img.save(os.path.join(WORK_DIR, "preview.png"))

def make_icon():
    """用猫头做 app 图标"""
    m = cat_frame(CAT_TAIL_A, cat_legs(CAT_LEG_XS, (0, 0, 0, 0)))
    head = render(m, CAT_PAL).crop((14, 0, 32, 14))
    sq = Image.new("RGBA", (18, 18), (0, 0, 0, 0))
    sq.alpha_composite(head, (0, 2))
    sizes = [sq.resize((s, s), Image.NEAREST) for s in (16, 32, 48, 64)]
    srcdir = os.path.join(ROOT, "src")
    os.makedirs(srcdir, exist_ok=True)
    sizes[0].save(os.path.join(srcdir, "app.ico"),
                  sizes=[(16, 16), (32, 32), (48, 48), (64, 64)],
                  append_images=sizes[1:])

if __name__ == "__main__":
    cat = build_cat()
    dog = build_dog()
    pets = [emit(cat), emit(dog)]
    preview(pets)
    make_icon()
    print("OK: pets/ + _work/preview.png + src/app.ico")
