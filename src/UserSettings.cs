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
        public bool ClickThrough = false;
        public int SettingsVersion = 3;
        public Dictionary<string, int> PetSizes = new Dictionary<string, int>();
        public string Background = "none";
        public int BackgroundHeight = 96;
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
                    string json = File.ReadAllText(FilePath);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    UserSettings settings = serializer.Deserialize<UserSettings>(json);
                    var stored = (Dictionary<string, object>)serializer.DeserializeObject(json);
                    if (settings != null)
                    {
                        if (settings.SelectedPets == null) settings.SelectedPets = new string[0];
                        if (!stored.ContainsKey("SettingsVersion")) settings.ClickThrough = false;
                        if (settings.PetSizes == null) settings.PetSizes = new Dictionary<string, int>();
                        int priorVersion = stored.ContainsKey("SettingsVersion") ? Convert.ToInt32(stored["SettingsVersion"]) : 1;
                        int nuomiSize;
                        if (priorVersion < 3 && settings.PetSizes.TryGetValue("nuomi", out nuomiSize) && nuomiSize == 120)
                            settings.PetSizes["nuomi"] = 100;
                        settings.SettingsVersion = 3;
                        settings.BackgroundHeight = Math.Max(32, Math.Min(160, settings.BackgroundHeight));
                        settings.BackgroundTransparency = Math.Max(0, Math.Min(100, settings.BackgroundTransparency));
                        return settings;
                    }
                }
            }
            catch (Exception) { }
            return new UserSettings();
        }

        public bool IsSelected(string id) { return Array.IndexOf(SelectedPets, id) >= 0; }

        public int SizeFor(string id)
        {
            int value;
            return PetSizes.TryGetValue(id, out value) ? Math.Max(50, Math.Min(200, value)) : 100;
        }

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
