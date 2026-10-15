"""将已验收姿态配准成桌面帧条；保留原图与失败候选，不重画角色。"""
from pathlib import Path
import hashlib
import json
import sys
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / 'assets-src/nuomi/desktop-source'))
from extract_strip_frames import connected_components, component_group_image

OUT = ROOT / 'pets/lolo'
QA = ROOT / '_work/qa/pose-scale/lolo'
OUT.mkdir(parents=True, exist_ok=True)
QA.mkdir(parents=True, exist_ok=True)
SIZE = (384, 384)
BASELINE = 376
# 每个动作都从原始大图直接缩一次；头部尺度一致，腿部伸展保留自然变化。
SCALE = .54
STATES = {
    'idle': ('qwen/rows/idle.png', [0,1,2,3], 3, True, 1),
    'walk': ('builtin/walk-grid.png', [0,1,2,3], 5, True, 1),
    'run': ('builtin/run-grid.png', [0,1,2,3], 8, True, 1),
    'sit': ('qwen/rows/sit.png', [0,1,2,3], 2, True, 1),
    'sleep': ('qwen/rows/sleep.png', [1,2,3], 1.5, True, 1),
    'drag': ('qwen/rows/drag.png', [0,1,2,3], 3, True, 1),
    # 原行悬空候选头部约154px，静置178px；按头宽校准整组，不按伸腿后的总高。
    # 只保留最后一格完整姿态，位移由桌面物理提供。
    'fall': ('qwen/rows/fall.png', [3], 4, True, 1.16),
    'happy': ('qwen/rows/happy.png', [0,1,2,3], 4, False, 1),
}

manifest = {'name':'洛洛','breed':'金色寻回猎犬','frameSize':list(SIZE),'scale':1,'baseSizePercent':75,
            'pixelArt':False,'playful':True,'states':{}}
audit = []
preview = Image.new('RGB', (4*SIZE[0], len(STATES)*(SIZE[1]+24)), '#f4f0ea')
draw = ImageDraw.Draw(preview)

for row,(state,(source_name,indices,fps,loop,adjustment)) in enumerate(STATES.items()):
    with Image.open(HERE/source_name) as image:
        source=image.convert('RGBA')
    frames=[]
    for index in indices:
        if 'grid' in source_name:
            cell=source.crop(((index%2)*source.width//2,(index//2)*source.height//2,
                              (index%2+1)*source.width//2,(index//2+1)*source.height//2))
        else:
            cell=source.crop((index*source.width//4,0,(index+1)*source.width//4,source.height))
        components=connected_components(cell)
        main=max(components,key=lambda item:item['area'])
        box=main['bbox']
        assert main['area'] > 10000,(state,index,'empty dog')
        assert 0<box[0]<box[2]<cell.width and 0<box[1]<box[3]<cell.height,(state,index,'clipped silhouette',box)
        pose=component_group_image(cell,[main],padding=0)
        factor=SCALE*adjustment
        display=pose.resize((round(pose.width*factor),round(pose.height*factor)),Image.Resampling.LANCZOS)
        assert display.width <= SIZE[0]-4 and display.height <= BASELINE-4,(state,index,'body exceeds viewport',display.size)
        frame=Image.new('RGBA',SIZE)
        frame.alpha_composite(display,((SIZE[0]-display.width)//2,BASELINE-display.height))
        frames.append(frame)
        audit.append({'state':state,'frame':index,'source':source_name,'sourceBounds':list(box),'scale':factor,'bodyPixels':list(display.size)})
    strip=Image.new('RGBA',(SIZE[0]*len(frames),SIZE[1]))
    for index,frame in enumerate(frames):
        strip.alpha_composite(frame,(index*SIZE[0],0))
        preview.paste(frame,(index*SIZE[0],row*(SIZE[1]+24)+24),frame)
    destination=OUT/(state+'.png')
    strip.save(destination.with_suffix('.png.tmp'),format='PNG')
    destination.with_suffix('.png.tmp').replace(destination)
    manifest['states'][state]={'file':state+'.png','frames':len(frames),'fps':fps,'loop':loop}
    draw.text((4,row*(SIZE[1]+24)+4),state,fill='#383330')

def save_json(path,value):
    temporary=path.with_suffix(path.suffix+'.tmp')
    temporary.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    assert json.loads(temporary.read_text(encoding='utf8'))==value
    temporary.replace(path)

save_json(OUT/'pet.json',manifest)
preview.save(QA/'contact-sheet.png')
save_json(QA/'skin-validation.json',{'ok':True,'name':'洛洛','frameSize':list(SIZE),'sharedScale':SCALE,'frames':audit,
    'selectedSource':'Qwen identity and calm poses; built-in imagegen repaired directional gait sheets',
    'excluded':'Qwen sleep/fall reference-only first cells; Qwen gait candidates with clipping, floor strips or reference poses',
    'runtimeFiles':[{'name':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(OUT.iterdir()) if p.is_file()]})
print('Built Lolo: 8 states, '+str(sum(len(spec[1]) for spec in STATES.values()))+' frames, intact silhouettes.')
