"""发布已经提交推送的Windows交付包；复用Git保存的GitHub凭据，不打印或落盘密钥。"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import subprocess
import urllib.error
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
REPO = "Skylar-Duan/desk-pets"
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
version = report.get("version", "")
if not isinstance(version, str) or not re.fullmatch(r"\d+\.\d+\.\d+", version):
    raise RuntimeError("package-result.json中的版本号格式无效，应为X.Y.Z")
TAG = "v" + version
if not package.is_file():
    raise RuntimeError("打包结果文件不存在：" + str(package))
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
if git("rev-parse", "origin/main") != head:
    raise RuntimeError("当前HEAD尚未推送为origin/main；请先推送后再发布")
try:
    release = api(base + "/releases/tags/" + TAG)
except urllib.error.HTTPError as error:
    if error.code != 404:
        raise RuntimeError("GitHub Release读取失败，HTTP " + str(error.code)) from None
    if version == "1.2.0":
        body = "宠物大小范围扩展至50%–300%；恢复默认只将所有宠物大小设为100%、背景高度设为60px，保留宠物选择、总显隐、背景开关与样式、透明度和互动设置。新用户背景高度默认60px，已有高度不会自动迁移，点击恢复默认才会重置。新增手动检查更新；陪伴离线运行，手动检查更新时联网，不会自动检查或下载安装。\n\n下载DeskPets.zip，解压后运行DeskPets.exe；支持Windows 10/11，使用系统自带.NET Framework 4.8。"
    else:
        body = "发布桌面小伙伴 " + version + "。\n\n下载DeskPets.zip，解压后运行DeskPets.exe；支持Windows 10/11，使用系统自带.NET Framework 4.8。"
    release = api(base + "/releases", "POST", {"tag_name": TAG, "target_commitish": head, "name": "桌面小伙伴 " + version,
        "draft": True, "body": body})
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
if asset.get("digest") != digest:
    raise RuntimeError("Release附件摘要核验失败")
if release["draft"]:
    release = api(base + "/releases/" + str(release["id"]), "PATCH", {"draft": False})
if args.replace_same_version:
    release = api(base + "/releases/" + str(release["id"]), "PATCH", {"body":
        "宠物大小范围扩展至50%–300%；恢复默认只将所有宠物大小设为100%、背景高度设为60px，保留宠物选择、总显隐、背景开关与样式、透明度和互动设置。新用户背景高度默认60px，已有高度不会自动迁移，点击恢复默认才会重置。新增手动检查更新；陪伴离线运行，手动检查更新时联网，不会自动检查或下载安装。\n\n本包源代码提交：" + head + "。\n\n下载DeskPets.zip，解压后运行DeskPets.exe；支持Windows 10/11，使用系统自带.NET Framework 4.8。"})
else:
    release = api(base + "/releases/" + str(release["id"]))
asset = next(a for a in release["assets"] if a["name"] == "DeskPets.zip")
result = {"ok": True, "release": release["html_url"], "download": asset["browser_download_url"],
          "bytes": asset["size"], "sha256": digest, "sourceCommit": head}
out = ROOT / "_work/release-result.json"
out.with_suffix(".json.tmp").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf8")
out.with_suffix(".json.tmp").replace(out)
print(json.dumps(result, ensure_ascii=False))
