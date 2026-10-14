using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using DeskPets;

static class ExportWebAssets
{
    static void Strip(string path, Bitmap[] frames, PetDef def)
    {
        using (Bitmap strip = new Bitmap(def.SpriteW * frames.Length, def.SpriteH))
        using (Graphics graphics = Graphics.FromImage(strip))
        {
            for (int i = 0; i < frames.Length; i++)
                graphics.DrawImageUnscaled(frames[i], i * def.SpriteW, def.GroundPadding(frames[i]));
            strip.Save(path + ".tmp", ImageFormat.Png);
        }
        if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
        else File.Move(path + ".tmp", path);
    }

    static int Main(string[] args)
    {
        try
        {
            List<PetDef> defs = PetDef.LoadAll(args[0]);
            foreach (PetDef def in defs)
                using (def)
                {
                    string folder = Path.Combine(args[1], def.Id);
                    Directory.CreateDirectory(folder);
                    var states = new Dictionary<string, object>();
                    foreach (string name in PetDef.RequiredStates)
                    {
                        StateAnim animation = def.States[name];
                        Strip(Path.Combine(folder, name + ".png"), animation.Right, def);
                        Strip(Path.Combine(folder, name + "-left.png"), animation.Left, def);
                        states[name] = new { file = name + ".png", leftFile = name + "-left.png",
                            frames = animation.Right.Length, fps = animation.Fps, loop = animation.Loop };
                    }
                    string path = Path.Combine(folder, "pet.json");
                    var manifest = new { name = def.Name, frameSize = new[] { def.SpriteW, def.SpriteH },
                        scale = 1, pixelArt = def.PixelArt, playful = def.Playful, calibrated = true, states = states };
                    File.WriteAllText(path + ".tmp", new JavaScriptSerializer().Serialize(manifest), new UTF8Encoding(false));
                    if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                    else File.Move(path + ".tmp", path);
                }
            Console.WriteLine("Exported " + defs.Count + " calibrated desktop pets for web.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
