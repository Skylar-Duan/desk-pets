# desk-pets · 桌面小伙伴

给女朋友做的桌面电子宠物：一只布偶猫「小布」+ 一只金毛「阿金」，像素风，常驻桌面。
灵感对标 OpenAI Codex 桌宠的审美与克制感，但完全独立、零依赖、全离线。

## 功能

- 两只宠物在屏幕底部游走、奔跑、坐卧、打盹；金毛会时不时跑去找猫
- **活跃度感知**：她在打字干活，宠物就精神；人离开几分钟，宠物睡大觉（只读系统"距上次键鼠输入的秒数"，不记录任何按键内容）
- 鼠标点宠物 = 摸摸头（开心冒爱心）；按住可拖走、甩出去有抛物线和落地小弹跳
- 宠物身体可点击，身体之外的透明像素自动点击穿透，不挡任何操作
- 托盘图标：隐藏/召唤、开机自启开关（默认关）、退出
- 不进任务栏、不抢焦点、单实例

## 交付/安装

把 `DeskPets.exe` + `pets/` 文件夹放进任意目录（如她的工作文件夹），双击 exe 即可。
Windows 11 零依赖（用系统自带 .NET Framework 4.8）。首次运行 SmartScreen 提示时：更多信息 → 仍要运行。

## 构建

```
build.bat
```

用 Windows 自带的 csc.exe 编译（C# 5 语法约束），任何 Win10/11 机器都能构建，无需安装任何工具链。

## 素材规范（换装/数字化真猫狗都按这个来）

`pets/<id>/` 一个文件夹 = 一只宠物：

- `pet.json`：`name` / `frameSize [w,h]` / `scale` / `states{ 状态: {file, frames, fps, loop} }`
- 每个状态一条横向帧条 PNG（透明底，帧等宽）
- 状态集：`idle walk run sit sleep drag fall happy`；缺的状态自动按回退链补
  （walk→idle，run→walk，sit→idle，sleep→sit，fall/drag 互补，happy→idle）
- **换素材 = 换文件夹，代码零改动**。新增宠物 = 新增文件夹（自动加载，全部同屏）

占位素材由 `assets-src/gen_pets.py` 程序化生成（Python + Pillow，像素图用字符画定义）。
以后把真猫真狗数字化：照片 → 逐状态画帧条（AI 生成 + 手修，或委托像素画师）→ 落同规范文件夹即可。

## 结构

```
src/           C# 源码（NativeMethods / PetDef / PetWindow / PetBrain / PetManager / TrayContext / Program）
assets-src/    素材生成器
pets/          宠物素材（可整体替换）
build.bat      一键构建
```
