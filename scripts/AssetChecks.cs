using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using DeskPets;

static class AssetChecks
{
    static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string root = args[0];
            List<PetDef> defs = PetDef.LoadAll(Path.Combine(root, "pets"));
            int manifests = 0;
            foreach (string folder in Directory.GetDirectories(Path.Combine(root, "pets")))
                if (File.Exists(Path.Combine(folder, "pet.json"))) manifests++;
            Require(defs.Count >= 4 && defs.Count == manifests, "应完整加载小布、阿金、糯米、洛洛及新增宠物");
            Require(defs.Exists(delegate(PetDef def) { return def.Id == "ragdoll"; }) &&
                defs.Exists(delegate(PetDef def) { return def.Id == "golden"; }), "原始像素动物未保留");
            PetDef nuomi = defs.Find(delegate(PetDef def) { return def.Id == "nuomi"; });
            Require(nuomi != null && !nuomi.PixelArt && !nuomi.Playful, "糯米应使用现代画风与猫的行为");
            PetDef lolo = defs.Find(delegate(PetDef def) { return def.Id == "lolo"; });
            Require(lolo != null && !lolo.PixelArt && lolo.Playful && lolo.Breed == "金色寻回猎犬", "洛洛应使用现代金毛的素材与行为");
            foreach (PetDef def in defs)
            {
                Require(def.SpriteH <= 288 && def.SpriteW <= 288, "基础宠物窗口过大");
                foreach (string state in PetDef.RequiredStates)
                {
                    Require(def.States.ContainsKey(state), "缺状态 " + state);
                    StateAnim animation = def.States[state];
                    Require(animation.Right.Length == animation.Left.Length && animation.Right.Length > 0, "方向帧不完整");
                    foreach (Bitmap image in animation.Right)
                        Require(image.Width == def.SpriteW && image.Height == def.SpriteH, "单格尺寸不一致");
                }
                foreach (int size in new int[] { 50, 100, 150, 200, 100 })
                {
                    def.SetSize(size);
                    foreach (StateAnim animation in def.States.Values)
                        foreach (Bitmap[] frames in new Bitmap[][] { animation.Right, animation.Left })
                            foreach (Bitmap frame in frames)
                            {
                                Require(frame.Size == new Size(def.SpriteW, def.SpriteH), "缩放后的动作尺寸不一致");
                                if (def.PixelArt) continue;
                                int bottom = frame.Height - 1;
                                for (; bottom >= 0; bottom--)
                                {
                                    bool visible = false;
                                    for (int x = 0; x < frame.Width; x++)
                                        if (frame.GetPixel(x, bottom).A >= 16) { visible = true; break; }
                                    if (visible) break;
                                }
                                Require(bottom + 1 + def.GroundPadding(frame) == def.SpriteH,
                                    "某动作或某大小的脚底无法贴地");
                            }
                }
            }
            Require(nuomi.States["sleep"].Right.Length == 4, "未装入新睡眠动作");
            Require(nuomi.States["drag"].Right.Length == 2 && nuomi.States["fall"].Right.Length == 2, "未装入空中动作");
            foreach (PetDef subject in new PetDef[] { nuomi, lolo })
            foreach (int size in new int[] { 50, 100, 200 })
            {
                subject.SetSize(size);
                PetBrain brain = new PetBrain(subject, subject.Playful);
                System.Drawing.Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                brain.X = area.Left + area.Width / 2;
                brain.Y = area.Bottom - subject.SpriteH;
                EnvInfo environment = new EnvInfo { Dt = 1.0 / 30, IdleSeconds = 0 };
                brain.BeginDrag();
                brain.DragTo(brain.X, area.Top + 80);
                Require(brain.State == PetState.Drag, "拖动状态丢失");
                brain.EndDrag(300, 0, false);
                Require(brain.State == PetState.Fall, "松手后未进入下落");
                for (int tick = 0; tick < 600 && brain.State == PetState.Fall; tick++) brain.Tick(environment);
                Require(brain.State != PetState.Fall && brain.State != PetState.Drag, "抛落后未回到地面");
                Bitmap frame = brain.CurrentFrame();
                Require(Math.Abs(brain.Y + subject.SpriteH - area.Bottom) < 1, "落地后脚底锚点错误");
                Require(brain.Y + subject.GroundPadding(frame) <= area.Bottom, "落地绘制位置错误");
            }
            nuomi.SetSize(100);
            lolo.SetSize(100);
            if (args.Length > 2)
                using (PetDef previous = PetDef.Load(args[2]))
                {
                    previous.SetSize(120);
                    Require(previous.SpriteW == nuomi.SpriteW && previous.SpriteH == nuomi.SpriteH, "新100%不等于原120%");
                    foreach (string state in PetDef.RequiredStates)
                        for (int direction = 0; direction < 2; direction++)
                        {
                            Bitmap[] before = direction == 0 ? previous.States[state].Right : previous.States[state].Left;
                            Bitmap[] after = direction == 0 ? nuomi.States[state].Right : nuomi.States[state].Left;
                            for (int i = 0; i < before.Length; i++)
                                for (int y = 0; y < before[i].Height; y++)
                                    for (int x = 0; x < before[i].Width; x++)
                                        Require(before[i].GetPixel(x, y) == after[i].GetPixel(x, y), "新旧基准像素不一致：" + state);
                        }
                }
            if (args.Length > 1)
            {
                Directory.CreateDirectory(args[1]);
                using (Bitmap comparison = new Bitmap(224 * defs.Count, 1280))
                using (Graphics graphics = Graphics.FromImage(comparison))
                using (Font font = new Font("Microsoft YaHei UI", 12, GraphicsUnit.Pixel))
                {
                    graphics.Clear(Color.FromArgb(244, 240, 234));
                    string[] compareStates = { "idle", "walk", "sleep", "drag" };
                    for (int row = 0; row < compareStates.Length; row++)
                    {
                        graphics.DrawLine(Pens.DarkSeaGreen, 0, row * 320 + 300, comparison.Width, row * 320 + 300);
                        for (int column = 0; column < defs.Count; column++)
                        {
                            PetDef def = defs[column];
                            Bitmap frame = def.States[compareStates[row]].Right[0];
                            graphics.DrawString(def.Name + " 100% · " + compareStates[row], font, Brushes.Black, column * 224 + 4, row * 320 + 4);
                            graphics.DrawImageUnscaled(frame, column * 224 + (224 - frame.Width) / 2,
                                row * 320 + 301 - frame.Height + def.GroundPadding(frame));
                        }
                    }
                    comparison.Save(Path.Combine(args[1], "pets-100-percent.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
                using (Bitmap sheet = new Bitmap(8 * nuomi.SpriteW, 16 * (nuomi.SpriteH + 24)))
                using (Graphics graphics = Graphics.FromImage(sheet))
                using (Font font = new Font("Segoe UI", 11, GraphicsUnit.Pixel))
                {
                    graphics.Clear(Color.FromArgb(244, 240, 234));
                    int row = 0;
                    foreach (string state in PetDef.RequiredStates)
                        foreach (Bitmap[] frames in new Bitmap[][] { nuomi.States[state].Right, nuomi.States[state].Left })
                        {
                            int top = row++ * (nuomi.SpriteH + 24);
                            graphics.DrawString(state + (row % 2 == 1 ? " right" : " left"), font, Brushes.Black, 4, top + 4);
                            graphics.DrawLine(Pens.DarkSeaGreen, 0, top + 23 + nuomi.SpriteH, sheet.Width, top + 23 + nuomi.SpriteH);
                            for (int i = 0; i < frames.Length; i++)
                                graphics.DrawImageUnscaled(frames[i], i * nuomi.SpriteW, top + 24 + nuomi.GroundPadding(frames[i]));
                        }
                    sheet.Save(Path.Combine(args[1], "runtime-all-states.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            var scenes = SceneDef.LoadAll(Path.Combine(root, "backgrounds"));
            Require(scenes.Count == 3, "背景素材数量不符");
            foreach (SceneDef scene in scenes)
            {
                using (Bitmap image = scene.Render(768, 96))
                {
                    int visible = 0;
                    for (int y = 0; y < image.Height; y++)
                        for (int x = 0; x < image.Width; x++)
                        {
                            if (image.GetPixel(x, y).A == 0) continue;
                            Require(y >= 4, "背景缺少顶部留白：" + scene.Id);
                            visible++;
                        }
                    Require(visible > image.Width * image.Height / 4 && visible < image.Width * image.Height * 3 / 4,
                        "背景密度应可见且保留留白：" + scene.Id);
                }
            }
            using (PetWindow window = new PetWindow())
            {
                window.ClickThrough = true;
                long style = NativeMethods.GetExtendedStyle(window.Handle);
                Require((style & NativeMethods.WS_EX_TRANSPARENT) != 0, "点击穿透未应用到Windows窗口");
                Require((style & NativeMethods.WS_EX_NOACTIVATE) != 0, "窗口可能抢焦点");
                Require((style & NativeMethods.WS_EX_LAYERED) != 0, "窗口未使用透明图层");
                window.ClickThrough = false;
                Require((NativeMethods.GetExtendedStyle(window.Handle) & NativeMethods.WS_EX_TRANSPARENT) == 0,
                    "互动模式不能关闭点击穿透");
            }
            Require(!new UserSettings().ClickThrough, "新用户默认应可以拖动宠物");
            Console.WriteLine("PASS: 4 pets at 50/100/150/200%, all state/direction ground anchors, denser backgrounds, native interaction flags.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
