# 糯米第三位角色素材

糯米是现代玩具风的蓝眼布偶猫，现已接入Windows桌宠，可与小布、阿金独立勾选。这里保存可继续制作的原始资产；可运行皮肤见 `../../pets/nuomi/`。它是第三位角色，不是第三种动物。

## 直接查看

- `original/spritesheet.png`：最终透明图，1536×2288，8列×11行，单格192×208，共73个有效帧。
- `original/base.png`：统一角色外观的参考图。
- `preview/all-states.gif`：九种基础动作预览。
- `preview/look-loop.gif`：16个视线方向。
- `preview/idle-jump-idle.gif`：静止、起跳、落地衔接。
- `docs/ENGINEERING_LOG.md`：制作策略及六肢、裁切、方向错误的修复记录。
- `provenance/`：原始提示词、制作任务表、请求及现有 ChatGPT pet ID。
- `qa/`：制作时的验收报告，保留其中的轻微方向警告。
- `source.json`：来源、格式和文件哈希。历史 JSON 中的绝对路径是原制作位置，不是本项目执行入口。

## 完整工程归档

完整源图、错误版本、逐帧图、脚本快照、公开过程记录保存在项目内：

`../../_work/imports/nuomi-20261014/nuomi-codex-handoff.zip`

该包223,346,805字节，约213 MiB；不进入Git。解压后先读 `CODEX_START_HERE.md`，可运行其中的 `tooling/verify_package.py`。初次核对511个清单文件全部通过；清单自身不在自校验列表中。

## 接入边界

现有桌宠使用 `idle walk run sit sleep drag fall happy`；糯米包含九种工作状态及16个视线方向。`running` 表示思考工作，左右跑是 `running-right/left`，不能只按相似名字替换动作。

当前复用眨眼、左右步态、坐姿和挥爪；慢走使用低速步态，点击用挥爪回应。睡觉、悬提、下落已由内置imagegen补画，完整源图、提示词和裁切脚本见 `desktop-source/`。下落使用静止姿态，由桌面程序提供位移，避免图内跳跃和程序抛物线叠加。

原始73帧图集原样保留。桌面皮肤使用96×128单格、1倍显示，支持独立左向步态和现代画风预览。`../../_work/reports/2026-10-14-pet-fusion-review.md` 是接入前的初评；当前状态以本说明和项目README为准。
