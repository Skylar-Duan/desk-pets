"""发布已经提交推送的Windows交付包；复用Git保存的GitHub凭据，不打印或落盘密钥。"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess
import urllib.error
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
REPO = "Skylar-Duan/desk-pets"
TAG = "v1.1.0"
parser = argparse.ArgumentParser()
parser.add_argument("--replace-same-version", action="store_true", help="按用户授权更新同一版本附件，不增加版本号")
args = parser.parse_args()
env = dict(os.environ, GIT_TERMINAL_PROMPT="0", GCM_INTERACTIVE="never")

def git(*args):
    return subprocess.check_output(["git", "-C", str(ROOT), *args], env=env, text=True).strip()

git("fetch", "origin", "-q")
head = git("rev-parse", "HEAD")
subprocess.check_call(["git", "-C", str(ROOT), "merge-base", "--is-ancestor", head, "origin/main"])
report = json.loads((ROOT / "_work/package-result.json").read_text(encoding="utf-8-sig"))
package = Path(report["zip"])
assert report["version"] == "1.1.0"
with zipfile.ZipFile(package) as archive:
    assert archive.testzip() is None
credentials = subprocess.run(["git", "credential", "fill"], input="protocol=https\nhost=github.com\npath=Skylar-Duan/desk-pets.git\n\n",
                             capture_output=True, text=True, env=env, check=True)
values = dict(line.split("=", 1) for line in credentials.stdout.splitlines() if "=" in line)
token = values.get("password")
if not token:
    raise RuntimeError("现有GitHub凭据不能用于Release发布")

def api(url, method="GET", data=None, binary=False):
    headers = {"Authorization": "Bearer " + token, "User-Agent": "desk-pets-release", "Accept": "application/vnd.github+json",
               "X-GitHub-Api-Version": "2022-11-28"}
    if data is not None:
        headers["Content-Type"] = "application/zip" if binary else "application/json"
        if not binary:
            data = json.dumps(data, ensure_ascii=False).encode("utf8")
    request = urllib.request.Request(url, data=data, headers=headers, method=method)
    with urllib.request.urlopen(request, timeout=60) as response:
        raw = response.read()
        return json.loads(raw) if raw else None

base = "https://api.github.com/repos/" + REPO
try:
    release = api(base + "/releases/tags/" + TAG)
except urllib.error.HTTPError as error:
    if error.code != 404:
        raise RuntimeError("GitHub Release读取失败，HTTP " + str(error.code)) from None
    release = api(base + "/releases", "POST", {"tag_name": TAG, "target_commitish": head, "name": "桌面小伙伴 1.1.0",
        "draft": True, "body": "小布、糯米和现代金毛洛洛可独立选择、调节大小，像素阿金也保留。新增可调高度及透明度的桌面背景，修复字体裁切、拖动、各动作体型与贴地。\n\n下载DeskPets.zip，解压后运行DeskPets.exe；支持Windows 10/11，使用系统自带.NET Framework 4.8。"})
asset = next((a for a in release["assets"] if a["name"] == "DeskPets.zip"), None)
digest = "sha256:" + hashlib.sha256(package.read_bytes()).hexdigest()
if asset is not None:
    if asset.get("digest") != digest:
        if not args.replace_same_version:
            raise RuntimeError("同名Release附件内容不同；同版本更新需要显式--replace-same-version")
        temporary_name = "DeskPets." + digest[7:19] + ".zip"
        replacement = next((a for a in release["assets"] if a["name"] == temporary_name), None)
        if replacement is None:
            replacement = api(release["upload_url"].split("{")[0] + "?name=" + temporary_name, "POST", package.read_bytes(), True)
        assert replacement["size"] == package.stat().st_size and replacement.get("digest") == digest
        api(asset["url"], "DELETE")
        asset = api(replacement["url"], "PATCH", {"name": "DeskPets.zip"})
else:
    asset = api(release["upload_url"].split("{")[0] + "?name=DeskPets.zip", "POST", package.read_bytes(), True)
assert asset["size"] == package.stat().st_size
if release["draft"]:
    release = api(base + "/releases/" + str(release["id"]), "PATCH", {"draft": False})
if args.replace_same_version:
    release = api(base + "/releases/" + str(release["id"]), "PATCH", {"body":
        "小布、糯米和现代金毛洛洛可独立选择、调节大小，像素阿金也保留。新增可调高度及透明度的桌面背景，修复字体裁切、拖动、各动作体型与贴地。三只原有动物的新100%统一采用上一轮150%的体型；洛洛按同一基准校准，具有八种状态、30帧动作。\n\n本轮继续使用1.1.0。本包源代码提交：" + head + "。\n\n下载DeskPets.zip，解压后运行DeskPets.exe；支持Windows 10/11，使用系统自带.NET Framework 4.8。"})
else:
    release = api(base + "/releases/" + str(release["id"]))
asset = next(a for a in release["assets"] if a["name"] == "DeskPets.zip")
result = {"ok": True, "release": release["html_url"], "download": asset["browser_download_url"],
          "bytes": asset["size"], "sha256": digest, "sourceCommit": head}
out = ROOT / "_work/release-result.json"
out.with_suffix(".json.tmp").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf8")
out.with_suffix(".json.tmp").replace(out)
print(json.dumps(result, ensure_ascii=False))
