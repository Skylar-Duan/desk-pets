# desk-pets · 桌面小伙伴

独立Windows桌面宠物软件：像素布偶猫「小布」、像素金毛「阿金」、现代玩具风布偶猫「糯米」。在控制窗口勾选想展示的动物，常驻桌面。
灵感对标 OpenAI Codex 桌宠的审美与克制感，但完全独立、零依赖、全离线。

## 功能

- **选择宠物**：小布、阿金、糯米各自勾选，选择与装饰设置自动记住；关控制窗口后继续运行
- 宠物在屏幕底部游走、奔跑、坐卧、打盹；金毛会时不时跑去找同伴
- **活跃度感知**：她在打字干活，宠物就精神；人离开几分钟，宠物睡大觉（只读系统"距上次键鼠输入的秒数"，不记录任何按键内容）
- **工作优先**：默认整只宠物鼠标穿透，不挡桌面点击；关闭穿透后可摸摸、拖动、抛落
- **三款低矮背景**：苔绿小径、暖砂石滩、云朵小窝；默认关闭，高度32–96像素、透明度0–100%可调；背景始终穿透鼠标，位置在任务栏上方
- 托盘图标：双击打开选择窗口；右键隐藏/召唤已选宠物、开机自启开关（默认关）、退出
- 不进任务栏、不抢焦点、单实例

## 交付/安装

当前版本 **1.1.0**。打包：`build.bat` 后运行 `powershell -File scripts/package.ps1`，得到 `dist/桌面小伙伴-1.1.0.zip`；包含 exe、三只宠物、三款背景和使用说明。

本机安装：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-local.ps1`。它正常退出旧实例、复制核对文件、创建桌面与开始菜单的「桌面小伙伴」快捷方式并打开软件；安装到 `%LOCALAPPDATA%/Programs/DeskPets/`。无须管理员权限，使用系统自带 .NET Framework 4.8。

也可以解压后直接运行。设置在 `%LOCALAPPDATA%/DeskPets/settings.json`，覆盖更新不改用户选择。关闭控制窗口只是收起；退出软件使用窗口或托盘的「退出」。

## 构建

```
build.bat
```

用 Windows 自带的 csc.exe 编译（C# 5 语法约束），任何 Win10/11 机器都能构建，无需安装任何工具链。

## 素材规范（换装/数字化真猫狗都按这个来）

`pets/<id>/` 一个文件夹 = 一只宠物：

- `pet.json`：`name` / `frameSize [w,h]` / `scale` / `states{ 状态: {file, frames, fps, loop} }`
- 可选 `pixelArt` / `playful`；状态可带 `leftFile` 保留独立左向步态。实际显示窗口宽高不超过192像素
- 每个状态一条横向帧条 PNG（透明底，帧等宽）
- 状态集：`idle walk run sit sleep drag fall happy`；缺的状态自动按回退链补
  （walk→idle，run→walk，sit→idle，sleep→sit，fall/drag 互补，happy→idle）
- **换素材 = 换文件夹**。新增文件夹进入控制窗口，勾选才加入桌面；缺动作的回退仅为容错，不代表美术已齐全

像素素材由 `assets-src/gen_pets.py` 生成。糯米由已验收v2素材加新睡眠、提起、下落姿态组成；生成来源和可复现裁切入口见 `assets-src/nuomi/desktop-source/`，运行皮肤在 `pets/nuomi/`。
以后把真猫真狗数字化：照片 → 逐状态画帧条（AI 生成 + 手修，或委托像素画师）→ 落同规范文件夹即可。

## 网页预览（web/）

`web/` 是自包含静态站：两只宠物用同一套素材在网页里真实跑动（可点可拖可甩），
附全状态图鉴、皮肤规范和下载按钮。由 10-Platform 挂载到 cdpandas.com 工具站子路径。
本地预览：任意静态服务器指向 `web/` 即可（如 `python -m http.server --directory web`）。
注意：`web/assets/` 是 `pets/` 的拷贝，改素材后需重新同步。
目前网页仍展示原有两只像素宠物；本轮发布范围为Windows本机软件，糯米与背景未同步到网站。

## 结构

```
src/           C# 源码（NativeMethods / PetDef / PetWindow / PetBrain / PetManager / TrayContext / Program）
assets-src/    素材生成器
pets/          宠物素材（可整体替换）
backgrounds/   三款低矮背景素材
scripts/       打包、本机安装、素材与Windows窗口属性检查
web/           网站预览页（静态自包含，交 10 部署）
dist/          交付包（gitignore，build 后生成）
build.bat      一键构建
```

## 待办 / 路线图

- **糯米已接入桌面（2026-10-14）**：完整制作档案仍保留，新增睡眠、提起与下落动作，现可独立勾选。原素材见 [`assets-src/nuomi/README.md`](assets-src/nuomi/README.md)，完整历史工程包在 `_work/imports/nuomi-20261014/`。当日初评为历史方案，当前行为以本说明和软件为准。

- **后续手感设置**：宠物缩放/活泼度、多显示器行为完善、可选音效
- **v1.2 皮肤管理器（本地）**：导入 zip 自动校验+预览+启用；兼容 Codex pet-pack（8×9及8×11图集转换）
- **v2 网站皮肤工坊（与 10 协同）**：在线画廊（复用 web/ 引擎做预览）、网页端上传皮肤（校验+实时预览+打包）、上传自定义动作（states 扩展即插即用）、账号挂 10 门户
- **v2.5 AI 孵化**：照片→像素 spritesheet 生成管线（先给小布阿金数字化用，验证后开放成网站功能）
- **v3 行为拓展**：气泡提醒（久坐/喝水/整点/纪念日）、宠物依偎互动、窗口顶栏行走
- **挂账**：真猫真狗数字化素材（等照片）；她的使用反馈回收

