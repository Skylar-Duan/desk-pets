using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DeskPets
{
    public sealed class SceneDef
    {
        public string Id;
        public string Name;
        public object[] Elements;
        public override string ToString() { return Name; }

        public static List<SceneDef> LoadAll(string root)
        {
            List<SceneDef> scenes = new List<SceneDef>();
            if (!Directory.Exists(root)) return scenes;
            foreach (string dir in Directory.GetDirectories(root))
            {
                try
                {
                    var json = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(
                        File.ReadAllText(Path.Combine(dir, "scene.json")));
                    scenes.Add(new SceneDef { Id = Path.GetFileName(dir), Name = (string)json["name"], Elements = (object[])json["elements"] });
                }
                catch (Exception) { }
            }
            scenes.Sort(delegate(SceneDef a, SceneDef b) { return string.CompareOrdinal(a.Id, b.Id); });
            return scenes;
        }

        public Bitmap Render(int width, int height)
        {
            Bitmap image = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float scale = height / 96F;
                for (float offset = 0; offset < width; offset += 384 * scale)
                {
                    foreach (object entry in Elements)
                    {
                        var shape = (Dictionary<string, object>)entry;
                        string kind = (string)shape["type"];
                        Color color = ColorTranslator.FromHtml((string)shape["color"]);
                        float x = offset + Convert.ToSingle(shape["x"]) * scale;
                        float y = Convert.ToSingle(shape["y"]) * scale;
                        float w = Convert.ToSingle(shape["w"]) * scale;
                        float h = Convert.ToSingle(shape["h"]) * scale;
                        using (Brush brush = new SolidBrush(color))
                        {
                            if (kind == "ellipse") graphics.FillEllipse(brush, x, y, w, h);
                            else if (kind == "rect") graphics.FillRectangle(brush, x, y, w, h);
                            else if (kind == "blade")
                                graphics.FillPolygon(brush, new PointF[] { new PointF(x, y + h), new PointF(x + w * .55F, y), new PointF(x + w, y + h) });
                        }
                    }
                }
            }
            return image;
        }
    }

    public sealed class BackdropManager : IDisposable
    {
        readonly List<PetWindow> windows = new List<PetWindow>();
        readonly List<Bitmap> images = new List<Bitmap>();
        readonly List<Rectangle> areas = new List<Rectangle>();
        string geometry = "";

        public void Apply(UserSettings settings, List<SceneDef> scenes)
        {
            SceneDef scene = scenes.Find(delegate(SceneDef item) { return item.Id == settings.Background; });
            if (scene == null || !settings.Visible || settings.BackgroundTransparency >= 100)
            {
                foreach (PetWindow window in windows) window.Hide();
                return;
            }
            string next = scene.Id + ":" + settings.BackgroundHeight;
            foreach (Screen screen in Screen.AllScreens) next += ":" + screen.WorkingArea;
            if (next != geometry)
            {
                DisposeBuffers();
                geometry = next;
                foreach (Screen screen in Screen.AllScreens)
                {
                    Rectangle area = screen.WorkingArea;
                    PetWindow window = new PetWindow();
                    window.Text = "桌宠背景 · " + scene.Name;
                    window.ClickThrough = true;
                    window.Location = new Point(area.Left + 20, area.Top + 20);
                    float screenScale;
                    using (Graphics graphics = Graphics.FromHwnd(window.Handle)) screenScale = graphics.DpiY / 96F;
                    int height = Math.Max(1, Math.Min((int)Math.Round(settings.BackgroundHeight * screenScale), area.Height / 5));
                    windows.Add(window);
                    images.Add(scene.Render(area.Width, height));
                    areas.Add(new Rectangle(area.Left, area.Bottom - height, area.Width, height));
                }
            }
            byte opacity = (byte)Math.Round(255 * (100 - settings.BackgroundTransparency) / 100.0);
            for (int i = 0; i < windows.Count; i++)
            {
                if (!windows[i].Visible) windows[i].Show();
                windows[i].Render(images[i], areas[i].Left, areas[i].Top, opacity);
            }
        }

        void DisposeBuffers()
        {
            foreach (PetWindow window in windows) window.Dispose();
            foreach (Bitmap image in images) image.Dispose();
            windows.Clear(); images.Clear(); areas.Clear();
        }

        public void Dispose() { DisposeBuffers(); }
    }
}
