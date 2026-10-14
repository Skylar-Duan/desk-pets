using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Threading;

namespace DeskPets
{
    /// <summary>托盘常驻：显隐宠物、开机自启开关、退出。</summary>
    public class TrayContext : ApplicationContext
    {
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "DeskPets";

        readonly NotifyIcon tray;
        readonly PetManager manager;
        readonly MenuItem miToggle;
        readonly MenuItem miAutostart;
        readonly List<PetDef> definitions;
        readonly System.Windows.Forms.Timer signalTimer;
        readonly EventWaitHandle gallerySignal;
        readonly EventWaitHandle exitSignal;
        readonly UserSettings settings;
        readonly List<SceneDef> scenes;
        readonly BackdropManager backdrop;
        PetControlForm gallery;
        bool settingsDirty;
        DateTime changedAt;
        bool saveWarningShown;
        int displayRefreshTicks;

        public TrayContext(EventWaitHandle signal, EventWaitHandle closeSignal, bool showGallery)
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            List<PetDef> defs = PetDef.LoadAll(Path.Combine(exeDir, "pets"));
            definitions = defs;
            gallerySignal = signal;
            exitSignal = closeSignal;
            settings = UserSettings.Load();
            scenes = SceneDef.LoadAll(Path.Combine(exeDir, "backgrounds"));
            if (defs.Count == 0)
            {
                MessageBox.Show("没找到宠物素材：请确认 exe 旁边有 pets 文件夹。",
                    "桌面小伙伴", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                throw new InvalidOperationException("no pets");
            }

            backdrop = new BackdropManager();
            backdrop.Apply(settings, scenes);
            manager = new PetManager();
            manager.Start(defs, settings);

            SelfHealAutostart();
            miToggle = new MenuItem(settings.Visible ? "隐藏全部" : "召唤已选宠物", OnToggle);
            miAutostart = new MenuItem("开机自动出现", OnAutostart);
            miAutostart.Checked = IsAutostartEnabled();
            MenuItem miExit = new MenuItem("退出", OnExit);

            tray = new NotifyIcon();
            tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            List<string> names = new List<string>();
            foreach (PetDef d in defs) names.Add(d.Name);
            string title = string.Join("和", names.ToArray()) + " 在你桌面上";
            tray.Text = title.Length > 63 ? title.Substring(0, 63) : title;
            tray.ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("选择宠物与背景", delegate { ShowGallery(); }),
                miToggle, miAutostart, new MenuItem("-"), miExit });
            tray.DoubleClick += delegate { ShowGallery(); };
            tray.Visible = true;

            signalTimer = new System.Windows.Forms.Timer();
            signalTimer.Interval = 200;
            signalTimer.Tick += delegate
            {
                if (exitSignal.WaitOne(0)) { OnExit(null, EventArgs.Empty); return; }
                if (gallerySignal.WaitOne(0)) ShowGallery();
                if (settingsDirty && (DateTime.UtcNow - changedAt).TotalMilliseconds > 500) SaveSettings();
                if (++displayRefreshTicks >= 10)
                {
                    displayRefreshTicks = 0;
                    backdrop.Apply(settings, scenes);
                    manager.RaisePets();
                }
            };
            signalTimer.Start();
            if (showGallery) ShowGallery();
        }

        void ShowGallery()
        {
            if (gallery == null || gallery.IsDisposed)
                gallery = new PetControlForm(definitions, scenes, settings, SettingsChanged,
                    delegate { OnToggle(null, EventArgs.Empty); }, delegate { OnExit(null, EventArgs.Empty); });
            gallery.RefreshState();
            gallery.Show();
            if (gallery.WindowState == FormWindowState.Minimized)
                gallery.WindowState = FormWindowState.Normal;
            gallery.Activate();
        }

        void OnToggle(object sender, EventArgs e)
        {
            manager.ToggleVisible();
            settings.Visible = manager.PetsVisible;
            SettingsChanged();
        }

        void SettingsChanged()
        {
            foreach (PetDef def in definitions) manager.Select(def.Id, settings.IsSelected(def.Id));
            manager.SetClickThrough(settings.ClickThrough);
            if (manager.PetsVisible != settings.Visible) manager.ToggleVisible();
            backdrop.Apply(settings, scenes);
            manager.RaisePets();
            miToggle.Text = manager.PetsVisible ? "隐藏全部" : "召唤已选宠物";
            if (gallery != null && !gallery.IsDisposed) gallery.RefreshState();
            settingsDirty = true;
            changedAt = DateTime.UtcNow;
        }

        void SaveSettings()
        {
            settingsDirty = false;
            try { settings.Save(); }
            catch (Exception)
            {
                if (!saveWarningShown)
                {
                    saveWarningShown = true;
                    MessageBox.Show("本次选择已生效，但暂时无法记住设置。", "桌面小伙伴",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        void OnAutostart(object sender, EventArgs e)
        {
            bool enable = !miAutostart.Checked;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (enable)
                        key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\" --background");
                    else if (key.GetValue(RunValueName) != null)
                        key.DeleteValue(RunValueName);
                }
                miAutostart.Checked = enable;
            }
            catch (Exception)
            {
                MessageBox.Show("没能修改开机自启设置。", "桌面小伙伴",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>文件夹被移动/改名后，把注册表里的自启路径自动修正为当前位置。</summary>
        static void SelfHealAutostart()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    object val = key.GetValue(RunValueName);
                    string expect = "\"" + Application.ExecutablePath + "\" --background";
                    if (val is string && (string)val != expect)
                        key.SetValue(RunValueName, expect);
                }
            }
            catch (Exception) { }
        }

        static bool IsAutostartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key != null && key.GetValue(RunValueName) != null;
            }
        }

        void OnExit(object sender, EventArgs e)
        {
            tray.Visible = false;
            if (settingsDirty) SaveSettings();
            signalTimer.Stop();
            signalTimer.Dispose();
            if (gallery != null && !gallery.IsDisposed) gallery.Close();
            manager.Dispose();
            backdrop.Dispose();
            tray.Dispose();
            ExitThread();
        }
    }
}
