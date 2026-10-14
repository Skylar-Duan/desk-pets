# 糯米第三位角色素材

糯米是现代玩具风的蓝眼布偶猫，作为小布、阿金之外的第三位角色保留。这里保存可继续制作的原始资产；目前尚未接入桌面运行名单。它是第三位角色，不是第三种动物。

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

可先复用眨眼、左右跑、挥爪；独立慢走、睡觉、悬提、下落和摸头开心动作仍需补齐或明确临时替代。跳跃不能直接代替下落，否则图内位移会与桌面物理运动叠加。

先保留原分辨率和两套画风。当前引擎默认3倍像素放大不适合这套素材；接入前需处理显示大小、平滑缩放和方向播放。详细建议见 `../../_work/reports/2026-10-14-pet-fusion-review.md`。
