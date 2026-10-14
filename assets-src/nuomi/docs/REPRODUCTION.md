# 校验与复现

## 最低环境

建议 Python 3.12。安装 `python -m pip install -r tooling/requirements.txt`。制作时 Pillow=12.3.0，视频预览使用 imageio-ffmpeg=0.6.0。FFmpeg二进制不在此包；首次安装是否能获取对应二进制取决于平台和网络。

## 文件完整性（不需额外依赖）

在此包根目录执行：

```powershell
python tooling/verify_package.py
```

校验器逐文件比对大小和SHA-256。已有 final/validation-extended.json 与 qa 报告是原制作验收记录。文件完整性校验通过并不自动证明未来修改后的视觉质量。

## 对当前最终图重新做结构检查

```powershell
python tooling/work-pets-0.1.6/skills/create-pet/scripts/validate_atlas.py deliverables/nuomi/spritesheet.png --require-v2 --chroma-key '#FF00FF' --json-out work/revalidation.json
```

## 从最终图重建动态预览

```powershell
python work/nuomi/preview_support.py --atlas deliverables/nuomi/spritesheet.png --atlas-validation work/revalidation.json --scripts-dir tooling/work-pets-0.1.6/skills/create-pet/scripts --output-dir work/rebuilt-previews --deps-dir .
```

该命令使用最终atlas逐格裁切，不重新生成图像。Windows使用Segoe UI；其他系统字体回退可能导致注释截图外观差异。MP4编码版本差异可能导致文件哈希不同。

## 从源图重新制作或改图

阅读 work/nuomi/imagegen-jobs.json、pet_request.json、prompts 与 qa 的配准记录；原始工具调用命令保存在 conversation/engineering-tool-calls.json。脚本各自支持 `--help`。旧绝对路径需映射到本包位置；旧 outputs/nuomi 对应 deliverables/nuomi。不要盲目执行整份历史工具调用，它包含历史上传与创建操作。

生成式模型无法保证重跑提示词后像素级一致。可精确保留与校验的是本包中已生成的源图、帧和最终图。重新创作仍需 ImageGen 能力；上传或选择需要已连接的 Pets 工具和账号权限。本包不携带凭据或有效上传会话。

原12份Library分卷是分批ZIP，每个可单独打开；不是需要拼接才能解压的多卷压缩格式。新的总ZIP解压后可直接使用，无须先解原分卷。
