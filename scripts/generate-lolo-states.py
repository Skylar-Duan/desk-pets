"""千问同一角色参考图生成动作帧条，逐条存档，可增量续跑。"""
import argparse
import base64
from concurrent.futures import ThreadPoolExecutor, as_completed
import hashlib
import json
from pathlib import Path
import sys
import urllib.error
import urllib.request
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, "S:/AI/Claude Code/claude-home/tools/py")
from dashscope_keys import get_dashscope_key, bypass_proxy
parser = argparse.ArgumentParser()
parser.add_argument("states", nargs="*")
parser.add_argument("--revision", default="rows")
args = parser.parse_args()
bypass_proxy()
key = get_dashscope_key()
prompts = json.loads((ROOT / "assets-src/lolo/animation-prompts.json").read_text(encoding="utf8"))
reference = ROOT / "assets-src/lolo/qwen/base.png"
encoded = "data:image/png;base64," + base64.b64encode(reference.read_bytes()).decode()
folder = ROOT / "assets-src/lolo/qwen" / args.revision
folder.mkdir(parents=True, exist_ok=True)
prefix = ("Input image is ONLY an identity reference for Lolo, one adult Golden Retriever, NOT one of the animation frames. Preserve the exact face, ears, fur colors, muzzle, proportions and soft modern character rendering. "
          "Maintain identical camera scale across all four frames AND across animation states: dog head at the same size; seated reference full dog height about 400 pixels in a cell. "
          "Do not enlarge sleeping or shrink moving poses to fill their cells. All paws, ears and tail inside each cell with 20 pixel safety padding. "
          "Genuinely transparent alpha background. All pixels outside the dog's physical silhouette MUST be fully transparent, including underneath and between paws. Absolutely no floor, no cast shadow, no grey blob under paws, no semi-transparent floor strip, no colored background, no painted checkerboard, no text, no labels, no grid lines, no watermarks. "
          "The four dogs remain separated by transparent margins, never overlap neighboring cells. Stable soft diffuse lighting. Animation instruction: ")

def run(state):
    destination = folder / (state + ".png")
    meta_path = folder / (state + ".json")
    if destination.exists() and meta_path.exists():
        return state + ": existing preserved"
    cell_width = 768 if state in ("walk", "run") else 512
    canvas_width = cell_width * 4
    layout = "Create EXACTLY FOUR equally sized cells in a single horizontal row, one complete pose in each cell. Each cell is " + str(cell_width) + "x512 pixels; total canvas " + str(canvas_width) + "x512. "
    prompt = layout + prefix + prompts[state] + " Every one of the FOUR cells must depict the requested state. Do not include the seated input image as the first cell. Never touch or crop any tail, nose, ear or paw at a cell boundary."
    payload = {"model":"qwen-image-2.1-pro", "input":{"messages":[{"role":"user","content":[{"image":encoded},{"text":prompt}]}]},
               "parameters":{"size":str(canvas_width)+"*512","n":1,"prompt_extend":False,"watermark":False}}
    request = urllib.request.Request("https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation",
        data=json.dumps(payload).encode(),headers={"Authorization":"Bearer "+key,"Content-Type":"application/json"},method="POST")
    try:
        with urllib.request.urlopen(request,timeout=300) as response:
            result=json.load(response)
    except urllib.error.HTTPError as error:
        body=json.loads(error.read()); raise RuntimeError(state+": "+body.get("code",str(error.code))+" "+body.get("message","")) from None
    url=result["output"]["choices"][0]["message"]["content"][0]["image"]
    with urllib.request.urlopen(urllib.request.Request(url,headers={"User-Agent":"Mozilla/5.0"}),timeout=60) as response:
        pixels=response.read()
    destination.with_suffix(".png.tmp").write_bytes(pixels)
    with Image.open(destination.with_suffix(".png.tmp")) as image:
        assert image.size==(canvas_width,512),state+": unexpected canvas size"
        alpha=image.convert("RGBA").getchannel("A");hist=alpha.histogram()
        assert sum(hist[:16])>image.width*image.height/4,state+": no useful alpha"
    destination.with_suffix(".png.tmp").replace(destination)
    metadata={"state":state,"model":"qwen-image-2.1-pro","prompt":prompt,"referenceSHA256":hashlib.sha256(reference.read_bytes()).hexdigest(),
              "requestId":result.get("request_id"),"usage":result.get("usage"),"frames":4,"canvas":[canvas_width,512],"sha256":hashlib.sha256(pixels).hexdigest()}
    meta_path.with_suffix(".json.tmp").write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding="utf8")
    meta_path.with_suffix(".json.tmp").replace(meta_path)
    return state+": generated"

failed=[]
with ThreadPoolExecutor(max_workers=2) as pool:
    futures={pool.submit(run,state):state for state in (args.states or list(prompts))}
    for future in as_completed(futures):
        try: print(future.result(),flush=True)
        except Exception as error: failed.append(futures[future]);print(str(error),flush=True)
if failed: raise SystemExit("Failed states: "+", ".join(failed))
