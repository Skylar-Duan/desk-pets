"""复用现有资产中枢和DashScope提交工具生成洛洛候选，密钥不落项目、不输出。"""
import importlib.util
import json
from pathlib import Path
import sys
import time
import urllib.request
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
spec = json.loads((ROOT / "assets-src/lolo/base-prompt.json").read_text(encoding="utf8"))
source = Path("S:/AI/Claude Code/project code/11-Preferred/company design/web design/图片资产库/_tools/genimg.py")
module_spec = importlib.util.spec_from_file_location("shared_genimg", source)
generator = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(generator)
generator.bypass_proxy()
key = generator.get_dashscope_key()
generator.SUBMIT = "https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation"
folder = ROOT / "assets-src/lolo/qwen"
folder.mkdir(parents=True, exist_ok=True)
task_file = folder / "generation.json"
image_path = folder / "base.png"
if image_path.exists() and task_file.exists():
    print("Existing Qwen candidate preserved: " + str(image_path))
    sys.exit(0)
payload = {"model": spec["qwenModel"],
    "input": {"messages": [{"role": "user", "content": [{"text": spec["prompt"]}]}]},
    "parameters": {"size": spec["size"], "n": 1, "prompt_extend": False, "watermark": False}}
request = urllib.request.Request(generator.SUBMIT, data=json.dumps(payload).encode("utf8"),
    headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"}, method="POST")
print("Generating Qwen candidate via supported synchronous endpoint...", flush=True)
with urllib.request.urlopen(request, timeout=300) as response:
    result = json.load(response)
url = result["output"]["choices"][0]["message"]["content"][0]["image"]
generator._download(url, image_path.with_suffix(".png.tmp"))
image_path.with_suffix(".png.tmp").replace(image_path)
metadata = {"requestId": result.get("request_id"), "model": spec["qwenModel"], "prompt": spec["prompt"], "size": spec["size"]}
with Image.open(image_path) as image:
    alpha = image.convert("RGBA").getchannel("A")
    metadata.update({"width": image.width, "height": image.height, "actualAlpha": alpha.getextrema()[0] == 0, "usage": result.get("usage")})
task_file.with_suffix(".json.tmp").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf8")
task_file.with_suffix(".json.tmp").replace(task_file)
print(json.dumps({"ok": True, "path": str(image_path), "actualAlpha": metadata["actualAlpha"]}, ensure_ascii=False), flush=True)
