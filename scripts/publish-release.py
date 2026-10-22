"""发布已经提交推送的Windows交付包；复用Git保存的GitHub凭据，不打印或落盘密钥。"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import struct
import subprocess
import urllib.error
import urllib.parse
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
for diff_args in (("diff", "--quiet"), ("diff", "--cached", "--quiet")):
    if subprocess.run(["git", "-C", str(ROOT), *diff_args], env=env).returncode != 0:
        raise RuntimeError("工作区或暂存区还有未提交改动；请完成构建验证、提交并推送后再发布")
report = json.loads((ROOT / "_work/package-result.json").read_text(encoding="utf-8-sig"))
version = report.get("version", "")
if not isinstance(version, str) or not re.fullmatch(r"\d+\.\d+\.\d+", version):
    raise RuntimeError("package-result.json中的版本号格式无效，应为X.Y.Z")
TAG = "v" + version
installer_name = "DeskPets-" + version + "-setup.exe"
packages = [(installer_name, Path(report["installer"]), "application/vnd.microsoft.portable-executable"),
            ("DeskPets.zip", Path(report["zip"]), "application/zip")]
for name, package, content_type in packages:
    if not package.is_file():
        raise RuntimeError("打包结果文件不存在：" + str(package))
    if name.endswith(".zip"):
        with zipfile.ZipFile(package) as archive:
            if archive.testzip() is not None:
                raise RuntimeError("便携包完整性检查失败")
            if not any(entry.filename.replace("\\", "/").endswith("/DeskPets.exe") for entry in archive.infolist()):
                raise RuntimeError("便携包缺少DeskPets.exe")
    else:
        with package.open("rb") as stream:
            header = stream.read(64)
            if len(header) != 64 or header[:2] != b"MZ":
                raise RuntimeError("安装包不是有效的Windows程序")
            stream.seek(struct.unpack_from("<I", header, 60)[0])
            if stream.read(4) != b"PE\0\0":
                raise RuntimeError("安装包PE签名无效")
credentials = subprocess.run(["git", "credential", "fill"], input="protocol=https\nhost=github.com\npath=Skylar-Duan/desk-pets.git\n\n",
                             capture_output=True, text=True, env=env, check=True)
values = dict(line.split("=", 1) for line in credentials.stdout.splitlines() if "=" in line)
token = values.get("password")
if not token:
    raise RuntimeError("现有GitHub凭据不能用于Release发布")

def api(url, method="GET", data=None, content_type=None):
    headers = {"Authorization": "Bearer " + token, "User-Agent": "desk-pets-release", "Accept": "application/vnd.github+json",
               "X-GitHub-Api-Version": "2022-11-28"}
    if data is not None:
        headers["Content-Type"] = content_type or "application/json"
        if content_type is None:
            data = json.dumps(data, ensure_ascii=False).encode("utf8")
    request = urllib.request.Request(url, data=data, headers=headers, method=method)
    with urllib.request.urlopen(request, timeout=60) as response:
        raw = response.read()
        return json.loads(raw) if raw else None

base = "https://api.github.com/repos/" + REPO
if git("rev-parse", "origin/main") != head:
    raise RuntimeError("当前HEAD尚未推送为origin/main；请先推送后再发布")
body = ("桌面小伙伴 " + version + " 提供Windows安装向导，自动创建快捷方式，可在系统设置中卸载；"
        "缺少运行环境时，安装期间联网补齐。\n\n"
        "下载 **" + installer_name + "**，双击后按向导安装；支持Windows 10/11。"
        "已有.NET Framework 4.8或更新版本时，安装无需联网。升级保留原有宠物选择和设置。\n\n"
        "陪伴离线运行；手动检查更新时联网，不会自动检查或下载安装。"
        "DeskPets.zip作为便携版保留，供旧版本更新入口和需要免安装的用户使用。\n\n"
        "本包源代码提交：" + head + "。")
try:
    release = api(base + "/releases/tags/" + TAG)
except urllib.error.HTTPError as error:
    if error.code != 404:
        raise RuntimeError("GitHub Release读取失败，HTTP " + str(error.code)) from None
    release = api(base + "/releases", "POST", {"tag_name": TAG, "target_commitish": head, "name": "桌面小伙伴 " + version,
        "draft": True, "body": body})
prepared = []
for name, package, content_type in packages:
    digest = "sha256:" + hashlib.sha256(package.read_bytes()).hexdigest()
    asset = next((a for a in release["assets"] if a["name"] == name), None)
    if asset is not None and asset.get("digest") != digest and not args.replace_same_version:
        raise RuntimeError("同名Release附件内容不同；同版本更新需要显式--replace-same-version：" + name)
    prepared.append({"name": name, "path": package, "content_type": content_type,
                     "digest": digest, "old": asset})

# 先上传并核验所有替换附件，全部成功后再切换现有下载链接。
for item in prepared:
    old = item["old"]
    if old is not None and old.get("digest") == item["digest"]:
        item["uploaded"] = old
        continue
    temporary_name = item["name"] + "." + item["digest"][7:19] if old is not None else item["name"]
    uploaded = next((a for a in release["assets"] if a["name"] == temporary_name), None)
    if uploaded is None:
        url = release["upload_url"].split("{")[0] + "?name=" + urllib.parse.quote(temporary_name)
        uploaded = api(url, "POST", item["path"].read_bytes(), item["content_type"])
    if uploaded.get("size") != item["path"].stat().st_size or uploaded.get("digest") != item["digest"]:
        raise RuntimeError("Release附件摘要或大小核验失败：" + item["name"])
    item["uploaded"] = uploaded

for item in prepared:
    if item["uploaded"]["name"] != item["name"]:
        api(item["old"]["url"], "DELETE")
        item["uploaded"] = api(item["uploaded"]["url"], "PATCH", {"name": item["name"]})

release = api(base + "/releases/" + str(release["id"]), "PATCH",
              {"draft": False, "name": "桌面小伙伴 " + version, "body": body})
published = []
for item in prepared:
    asset = next(a for a in release["assets"] if a["name"] == item["name"])
    if asset.get("digest") != item["digest"] or asset.get("size") != item["path"].stat().st_size:
        raise RuntimeError("发布后附件核验失败：" + item["name"])
    published.append({"name": item["name"], "download": asset["browser_download_url"],
                      "bytes": asset["size"], "sha256": item["digest"]})
result = {"ok": True, "release": release["html_url"], "download": published[0]["download"],
          "bytes": published[0]["bytes"], "sha256": published[0]["sha256"],
          "assets": published, "sourceCommit": head}
out = ROOT / "_work/release-result.json"
out.with_suffix(".json.tmp").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf8")
out.with_suffix(".json.tmp").replace(out)
print(json.dumps(result, ensure_ascii=False))
