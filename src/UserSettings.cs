using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace DeskPets
{
    public sealed class UserSettings
    {
        public string[] SelectedPets = new string[] { "ragdoll", "golden", "nuomi" };
        public bool Visible = true;
        public bool ClickThrough = true;
        public string Background = "none";
        public int BackgroundHeight = 48;
        public int BackgroundTransparency = 75;

        public static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskPets", "settings.json"); }
        }

        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    UserSettings settings = new JavaScriptSerializer().Deserialize<UserSettings>(File.ReadAllText(FilePath));
                    if (settings != null)
                    {
                        if (settings.SelectedPets == null) settings.SelectedPets = new string[0];
                        settings.BackgroundHeight = Math.Max(32, Math.Min(96, settings.BackgroundHeight));
                        settings.BackgroundTransparency = Math.Max(0, Math.Min(100, settings.BackgroundTransparency));
                        return settings;
                    }
                }
            }
            catch (Exception) { }
            return new UserSettings();
        }

        public bool IsSelected(string id) { return Array.IndexOf(SelectedPets, id) >= 0; }

        public void Select(string id, bool enabled)
        {
            List<string> ids = new List<string>(SelectedPets);
            ids.RemoveAll(delegate(string item) { return item == id; });
            if (enabled) ids.Add(id);
            SelectedPets = ids.ToArray();
        }

        public void Save()
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            string json = new JavaScriptSerializer().Serialize(this);
            File.WriteAllText(temporary, json, new System.Text.UTF8Encoding(false));
            new JavaScriptSerializer().Deserialize<UserSettings>(File.ReadAllText(temporary));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
