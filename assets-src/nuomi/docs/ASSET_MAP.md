# 资产地图

| 路径 | 内容与用途 |
| --- | --- |
| deliverables/nuomi | 最终PNG、九动作GIF/MP4、跳跃衔接、16方向循环、等待动作修复预览、联系表、方向表、带时间截图、宠物身份信息 |
| work/nuomi/decoded | 已解码生成图，含选用版和保留的修订/错误版本 |
| raw-generation | 由本任务及其原有子任务生成目录复制的24张原始图片 |
| work/nuomi/references | 角色基准、槽位指南、用户六肢反馈及参考拼图 |
| work/nuomi/prompts | 基础角色、每个动作、方向及修复提示词 |
| work/nuomi/frames | 标准动作提帧及清单 |
| work/nuomi/final | 标准图、扩展原始图、最终清理图与结构校验 |
| work/nuomi/qa | 自动检测、独立视觉审查、盲评、逐帧证据、最终预览 |
| work/nuomi/library-archives | 原有12个ZIP分卷和源文件、归档清单；这是历史归档快照，后续文件仍以本包为准 |
| work/nuomi/library-*.json | Library目标、上传记录与页面回读证据 |
| tooling/work-pets-0.1.6 | 当时使用的 create-pet 技能、Python脚本和插件共享参考文件快照 |
| work/nuomi/*.py | 预览与原分卷归档辅助脚本；跳跃辅助脚本另在 qa/rows/jumping |
| conversation | 公开消息、原制作工具调用参数、导出覆盖范围 |
| docs | 本次补充的工程说明和复现指南 |
| MANIFEST.json | 文件大小及SHA-256；不含自身与外部ZIP，以避免循环校验 |

所有既有 work/nuomi 文件及 outputs/nuomi 文件均复制，缓存目录除外。原来的分卷也保留，因此存在有意的重复资产。未打包Python运行时、FFmpeg依赖二进制及缓存；可依据复现说明重新安装。

历史 JSON/日志保留原 Windows 绝对路径，体现当时来源。迁移后将旧工作目录前缀映射到本包根目录；旧 outputs/nuomi 映射到 deliverables/nuomi。不要把历史路径当作新电脑上存在的文件。
