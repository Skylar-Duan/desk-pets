using System;
using System.IO;
using System.Web.Script.Serialization;
using DeskPets;

static class SettingsChecks
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static int Main(string[] args)
    {
        string path = Path.Combine(args[0], "settings-migration-check.json");
        try
        {
            Directory.CreateDirectory(args[0]);
            var old = new UserSettings();
            old.SettingsVersion = 5;
            old.SelectedPets = new string[] { "nuomi", "ragdoll", "lolo", "golden" };
            old.PetSizes["nuomi"] = 100; old.PetSizes["lolo"] = 150;
            old.PetSizes["golden"] = 75; old.PetSizes["ragdoll"] = 125;
            old.Background = "garden"; old.BackgroundHeight = 160; old.BackgroundTransparency = 73;
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(path + ".tmp", serializer.Serialize(old));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
            UserSettings migrated = UserSettings.LoadFrom(path);
            Require(migrated.SizeFor("lolo") == 100, "洛洛旧150%应迁移为新100%，不重复放大");
            Require(migrated.SizeFor("nuomi") == 100 && migrated.SizeFor("ragdoll") == 125 && migrated.SizeFor("golden") == 75, "其它宠物大小不应改变");
            Require(string.Join(",", migrated.SelectedPets) == string.Join(",", old.SelectedPets), "宠物选择不应改变");
            Require(migrated.BackgroundHeight == 160 && migrated.BackgroundTransparency == 73 && migrated.Background == "garden", "背景设置不应改变");
            File.WriteAllText(path + ".tmp", serializer.Serialize(migrated)); File.Replace(path + ".tmp", path, null);
            Require(UserSettings.LoadFrom(path).SizeFor("lolo") == 100, "重新启动不能重复迁移大小");
            var linked = ScaleAdjustment.Capture(migrated);
            linked.Apply(migrated, 50, false);
            Require(migrated.SizeFor("ragdoll") == 84 && migrated.SizeFor("golden") == 50 && migrated.BackgroundHeight == 160, "联动应保持比例且整体止于最小成员边界");
            linked.Apply(migrated, 100, false);
            Require(migrated.SizeFor("ragdoll") == 125 && migrated.SizeFor("golden") == 75, "联动来回拖动应从同一快照恢复");
            Require(ScaleAdjustment.Capture(migrated).MaximumPercent(true) == 100, "背景160时整体联动应停止继续放大");
            migrated.PetSizes["nuomi"] = 300;
            Require(migrated.SizeFor("nuomi") == 300, "新范围应支持300%，不能沿用旧200%夹值");
            File.WriteAllText(path + ".tmp", serializer.Serialize(migrated)); File.Replace(path + ".tmp", path, null);
            Require(UserSettings.LoadFrom(path).SizeFor("nuomi") == 300, "300%重启后应保留");
            migrated.RestoreDefaults();
            Require(migrated.SelectedPets.Length == 3 && migrated.IsSelected("ragdoll") && migrated.IsSelected("lolo") && migrated.IsSelected("nuomi") && !migrated.IsSelected("golden"), "恢复默认应选择三位默认伙伴");
            Require(migrated.PetSizes.Count == 0 && migrated.SizeFor("nuomi") == 100 && migrated.SizeFor("lolo") == 100, "恢复默认应清除所有自定义大小");
            Require(migrated.Visible && !migrated.ClickThrough && migrated.Background == "none" && migrated.BackgroundHeight == 96 && migrated.BackgroundTransparency == 75, "恢复默认应还原背景与互动");
            linked = ScaleAdjustment.Capture(migrated);
            Require(linked.MaximumPercent(false) == 300, "默认伙伴应可一起放大到300%");
            linked.Apply(migrated, 300, false);
            Require(migrated.SizeFor("nuomi") == 300 && migrated.SizeFor("lolo") == 300 && migrated.BackgroundHeight == 96, "伙伴一起300%不应改变背景");
            Console.WriteLine("PASS: migration, 300% persistence, complete restore defaults, linked ratios and background bounds.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); }
    }
}
