using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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
        public Bitmap[] BaseRight;
        public Bitmap[] BaseLeft;
        public Bitmap[] SourceRight;
        public Bitmap[] SourceLeft;
    }

    /// <summary>
    /// 一只宠物的完整定义，由 pets/&lt;id&gt;/pet.json 驱动。
    /// 素材可整目录替换，代码只认 manifest；缺失状态按回退链补齐。
    /// </summary>
    public class PetDef : IDisposable
    {
        public string Id;
        public string Name;
        public string Breed;
        public int FrameW;
        public int FrameH;
        public int Scale;
        public int BaseSizePercent = 100;
        public bool PixelArt = true;
        public bool Playful;
        public Dictionary<string, StateAnim> States = new Dictionary<string, StateAnim>();
        readonly Dictionary<Bitmap, int> groundPadding = new Dictionary<Bitmap, int>();
        public int DisplayPercent = 100;

        public int SpriteW { get { return Math.Max(1, (int)Math.Round(Math.Round(FrameW * Scale * BaseSizePercent / 100.0) * DisplayPercent / 100.0)); } }
        public int SpriteH { get { return Math.Max(1, (int)Math.Round(Math.Round(FrameH * Scale * BaseSizePercent / 100.0) * DisplayPercent / 100.0)); } }

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
            def.Breed = root.ContainsKey("breed") ? (string)root["breed"] : def.Id == "golden" ? "金毛" : "布偶猫";
            object[] fs = (object[])root["frameSize"];
            def.FrameW = Convert.ToInt32(fs[0]);
            def.FrameH = Convert.ToInt32(fs[1]);
            def.Scale = root.ContainsKey("scale") ? Convert.ToInt32(root["scale"]) : 3;
            def.BaseSizePercent = root.ContainsKey("baseSizePercent") ? Convert.ToInt32(root["baseSizePercent"]) : 100;
            def.PixelArt = !root.ContainsKey("pixelArt") || Convert.ToBoolean(root["pixelArt"]);
            def.Playful = root.ContainsKey("playful") ? Convert.ToBoolean(root["playful"]) : def.Id == "golden";
            if (def.Scale < 1) def.Scale = 1;
            if (def.Scale > 8) def.Scale = 8;
            if (def.FrameW < 1 || def.FrameH < 1 || def.FrameW * def.Scale > 1024 || def.FrameH * def.Scale > 1024)
                throw new InvalidDataException("宠物源素材大小超出限制");
            if (def.BaseSizePercent < 25 || def.BaseSizePercent > 200 || def.SpriteW > 288 || def.SpriteH > 288)
                throw new InvalidDataException("宠物默认体型超出桌面限制");

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
                double poseScale = st.ContainsKey("poseScale") ? Convert.ToDouble(st["poseScale"]) : 1.0;
                if (double.IsNaN(poseScale) || double.IsInfinity(poseScale) || poseScale < 0.25 || poseScale > 1)
                    throw new InvalidDataException("动作体型比例超出范围：" + kv.Key);
                string pngPath = Path.Combine(dir, file);
                if (!File.Exists(pngPath) || frames < 1) continue;
                def.States[kv.Key] = LoadStrip(pngPath, frames, def.FrameW, def.FrameH, def.Scale, fps, loop, poseScale, def.PixelArt);
                if (st.ContainsKey("leftFile"))
                {
                    StateAnim left = LoadStrip(Path.Combine(dir, (string)st["leftFile"]), frames,
                        def.FrameW, def.FrameH, def.Scale, fps, loop, poseScale, def.PixelArt);
                    foreach (Bitmap previous in def.States[kv.Key].Left) previous.Dispose();
                    foreach (Bitmap extra in left.Left) extra.Dispose();
                    def.States[kv.Key].Left = left.Right;
                }
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

            def.SetSize(100);
            return def;
        }

        public void SetSize(int percent)
        {
            percent = Math.Max(50, Math.Min(200, percent));
            if (percent == DisplayPercent && groundPadding.Count > 0) return;
            DisplayPercent = percent;
            groundPadding.Clear();
            HashSet<StateAnim> animations = new HashSet<StateAnim>(States.Values);
            foreach (StateAnim animation in animations)
            {
                if (animation.SourceRight == null)
                {
                    animation.SourceRight = animation.Right;
                    animation.SourceLeft = animation.Left;
                    animation.BaseRight = ResizeFrames(animation.SourceRight);
                    animation.BaseLeft = ResizeFrames(animation.SourceLeft);
                }
                else if (!ReferenceEquals(animation.Right, animation.BaseRight) && !ReferenceEquals(animation.Right, animation.SourceRight))
                {
                    foreach (Bitmap bitmap in animation.Right) bitmap.Dispose();
                    foreach (Bitmap bitmap in animation.Left) bitmap.Dispose();
                }
                animation.Right = percent == 100 ? animation.BaseRight : ResizeFrames(animation.SourceRight);
                animation.Left = percent == 100 ? animation.BaseLeft : ResizeFrames(animation.SourceLeft);
                foreach (Bitmap bitmap in animation.Right) RegisterGround(bitmap);
                foreach (Bitmap bitmap in animation.Left) RegisterGround(bitmap);
            }
        }

        Bitmap[] ResizeFrames(Bitmap[] source)
        {
            Bitmap[] images = new Bitmap[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Bitmap image = new Bitmap(SpriteW, SpriteH, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.InterpolationMode = PixelArt ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawImage(source[i], new Rectangle(0, 0, SpriteW, SpriteH));
                }
                images[i] = image;
            }
            return images;
        }

        void RegisterGround(Bitmap image)
        {
            int padding = 0;
            if (!PixelArt)
            {
                int bottom = image.Height - 1;
                BitmapData pixels = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] row = new byte[image.Width * 4];
                    for (; bottom >= 0; bottom--)
                    {
                        Marshal.Copy(IntPtr.Add(pixels.Scan0, bottom * pixels.Stride), row, 0, row.Length);
                        bool visible = false;
                        for (int x = 0; x < image.Width; x++)
                            if (row[x * 4 + 3] >= 16) { visible = true; break; }
                        if (visible) break;
                    }
                }
                finally { image.UnlockBits(pixels); }
                padding = Math.Max(0, image.Height - 1 - bottom);
            }
            groundPadding[image] = padding;
        }

        public int GroundPadding(Bitmap image)
        {
            int padding;
            return groundPadding.TryGetValue(image, out padding) ? padding : 0;
        }

        public void Dispose()
        {
            HashSet<Bitmap> images = new HashSet<Bitmap>();
            foreach (StateAnim animation in States.Values)
                foreach (Bitmap[] frames in new Bitmap[][] { animation.Right, animation.Left, animation.BaseRight, animation.BaseLeft, animation.SourceRight, animation.SourceLeft })
                    if (frames != null) foreach (Bitmap image in frames) images.Add(image);
            foreach (Bitmap image in images) image.Dispose();
        }

        static StateAnim LoadStrip(string path, int frames, int fw, int fh, int scale, double fps, bool loop, double poseScale, bool pixelArt)
        {
            StateAnim anim = new StateAnim();
            anim.Fps = fps;
            anim.Loop = loop;
            anim.Right = new Bitmap[frames];
            anim.Left = new Bitmap[frames];
            using (Bitmap strip = new Bitmap(path))
            {
                if (strip.Width != frames * fw || strip.Height != fh)
                    throw new InvalidDataException("动画尺寸与说明不符：" + path);
                for (int i = 0; i < frames; i++)
                {
                    Rectangle crop = new Rectangle(i * fw, 0, fw, fh);
                    Bitmap right = ScaleFrame(strip, crop, scale, poseScale, pixelArt);
                    Bitmap left = (Bitmap)right.Clone();
                    left.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    anim.Right[i] = right;
                    anim.Left[i] = left;
                }
            }
            return anim;
        }

        static Bitmap ScaleFrame(Bitmap src, Rectangle crop, int scale, double poseScale, bool pixelArt)
        {
            Bitmap dst = new Bitmap(crop.Width * scale, crop.Height * scale, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = pixelArt ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                int width = Math.Max(1, (int)Math.Round(dst.Width * poseScale));
                int height = Math.Max(1, (int)Math.Round(dst.Height * poseScale));
                g.DrawImage(src, new Rectangle((dst.Width - width) / 2, dst.Height - height, width, height), crop, GraphicsUnit.Pixel);
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
