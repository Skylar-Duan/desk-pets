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
            Require(defs.Count == 3, "应加载小布、阿金、糯米共三只");
            PetDef nuomi = defs.Find(delegate(PetDef def) { return def.Id == "nuomi"; });
            Require(nuomi != null && !nuomi.PixelArt && !nuomi.Playful, "糯米应使用现代画风与猫的行为");
            foreach (PetDef def in defs)
            {
                Require(def.SpriteH <= 192 && def.SpriteW <= 192, "宠物窗口过大");
                foreach (string state in PetDef.RequiredStates)
                {
                    Require(def.States.ContainsKey(state), "缺状态 " + state);
                    StateAnim animation = def.States[state];
                    Require(animation.Right.Length == animation.Left.Length && animation.Right.Length > 0, "方向帧不完整");
                    foreach (Bitmap image in animation.Right)
                        Require(image.Width == def.SpriteW && image.Height == def.SpriteH, "单格尺寸不一致");
                }
            }
            Require(nuomi.States["sleep"].Right.Length == 4, "未装入新睡眠动作");
            Require(nuomi.States["drag"].Right.Length == 2 && nuomi.States["fall"].Right.Length == 2, "未装入空中动作");
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
                            Require(y >= 48, "背景元素侵入高处：" + scene.Id);
                            visible++;
                        }
                    Require(visible > 0 && visible < image.Width * image.Height / 3, "背景应大部分留空：" + scene.Id);
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
            Console.WriteLine("PASS: 3 pets, all runtime states, directional frames, 3 sparse low backgrounds, native click-through/no-activation flags.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
