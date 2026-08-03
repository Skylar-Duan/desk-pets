using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Script.Serialization;

namespace DeskPets
{
    /// <summary>单个状态的动画：左右朝向各一组已放大的帧位图。</summary>
    public class StateAnim
    {
        public Bitmap[] Right;
        public Bitmap[] Left;
        public double Fps;
        public bool Loop;
    }

    /// <summary>
    /// 一只宠物的完整定义，由 pets/&lt;id&gt;/pet.json 驱动。
    /// 素材可整目录替换，代码只认 manifest；缺失状态按回退链补齐。
    /// </summary>
    public class PetDef
    {
        public string Id;
        public string Name;
        public int FrameW;
        public int FrameH;
        public int Scale;
        public Dictionary<string, StateAnim> States = new Dictionary<string, StateAnim>();

        public int SpriteW { get { return FrameW * Scale; } }
        public int SpriteH { get { return FrameH * Scale; } }

        static readonly string[,] FallbackChain = new string[,]
        {
            { "walk",  "idle" },
            { "run",   "walk" },
            { "sit",   "idle" },
            { "sleep", "sit"  },
            { "fall",  "drag" },
            { "drag",  "fall" },
            { "happy", "idle" },
        };

        public static readonly string[] RequiredStates =
            { "idle", "walk", "run", "sit", "sleep", "drag", "fall", "happy" };

        public static PetDef Load(string dir)
        {
            string manifestPath = Path.Combine(dir, "pet.json");
            string json = File.ReadAllText(manifestPath);
            JavaScriptSerializer js = new JavaScriptSerializer();
            Dictionary<string, object> root = (Dictionary<string, object>)js.DeserializeObject(json);

            PetDef def = new PetDef();
            def.Id = Path.GetFileName(dir);
            def.Name = root.ContainsKey("name") ? (string)root["name"] : def.Id;
            object[] fs = (object[])root["frameSize"];
            def.FrameW = Convert.ToInt32(fs[0]);
            def.FrameH = Convert.ToInt32(fs[1]);
            def.Scale = root.ContainsKey("scale") ? Convert.ToInt32(root["scale"]) : 3;
            if (def.Scale < 1) def.Scale = 1;
            if (def.Scale > 8) def.Scale = 8;

            Dictionary<string, object> states = (Dictionary<string, object>)root["states"];
            foreach (KeyValuePair<string, object> kv in states)
            {
                Dictionary<string, object> st = (Dictionary<string, object>)kv.Value;
                string file = (string)st["file"];
                int frames = Convert.ToInt32(st["frames"]);
                double fps = st.ContainsKey("fps") ? Convert.ToDouble(st["fps"]) : 6.0;
                if (double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0) fps = 6.0;
                if (fps > 60) fps = 60;
                bool loop = st.ContainsKey("loop") ? Convert.ToBoolean(st["loop"]) : true;
                string pngPath = Path.Combine(dir, file);
                if (!File.Exists(pngPath) || frames < 1) continue;
                def.States[kv.Key] = LoadStrip(pngPath, frames, def.FrameW, def.FrameH, def.Scale, fps, loop);
            }

            if (!def.States.ContainsKey("idle"))
                throw new InvalidDataException("宠物 " + def.Id + " 缺少必需状态 idle");

            // 回退链补齐（引用同一组位图，不复制）
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < FallbackChain.GetLength(0); i++)
                {
                    string want = FallbackChain[i, 0], from = FallbackChain[i, 1];
                    if (!def.States.ContainsKey(want) && def.States.ContainsKey(from))
                    {
                        def.States[want] = def.States[from];
                        changed = true;
                    }
                }
            }
            foreach (string s in RequiredStates)
                if (!def.States.ContainsKey(s))
                    def.States[s] = def.States["idle"];

            return def;
        }

        static StateAnim LoadStrip(string path, int frames, int fw, int fh, int scale, double fps, bool loop)
        {
            StateAnim anim = new StateAnim();
            anim.Fps = fps;
            anim.Loop = loop;
            anim.Right = new Bitmap[frames];
            anim.Left = new Bitmap[frames];
            using (Bitmap strip = new Bitmap(path))
            {
                for (int i = 0; i < frames; i++)
                {
                    Rectangle crop = new Rectangle(i * fw, 0, fw, fh);
                    Bitmap right = ScaleFrame(strip, crop, scale);
                    Bitmap left = (Bitmap)right.Clone();
                    left.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    anim.Right[i] = right;
                    anim.Left[i] = left;
                }
            }
            return anim;
        }

        static Bitmap ScaleFrame(Bitmap src, Rectangle crop, int scale)
        {
            Bitmap dst = new Bitmap(crop.Width * scale, crop.Height * scale, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.DrawImage(src, new Rectangle(0, 0, dst.Width, dst.Height), crop, GraphicsUnit.Pixel);
            }
            return dst;
        }

        /// <summary>扫描 pets 根目录，加载所有含 pet.json 的子目录。</summary>
        public static List<PetDef> LoadAll(string petsRoot)
        {
            List<PetDef> defs = new List<PetDef>();
            if (!Directory.Exists(petsRoot)) return defs;
            foreach (string dir in Directory.GetDirectories(petsRoot))
            {
                if (!File.Exists(Path.Combine(dir, "pet.json"))) continue;
                try { defs.Add(Load(dir)); }
                catch (Exception) { /* 单个宠物损坏不拖垮全体 */ }
            }
            return defs;
        }
    }
}
