using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

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

        public TrayContext()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            List<PetDef> defs = PetDef.LoadAll(Path.Combine(exeDir, "pets"));
            if (defs.Count == 0)
            {
                MessageBox.Show("没找到宠物素材：请确认 exe 旁边有 pets 文件夹。",
                    "桌面小伙伴", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                throw new InvalidOperationException("no pets");
            }

            manager = new PetManager();
            manager.Start(defs);

            SelfHealAutostart();
            miToggle = new MenuItem("隐藏小伙伴", OnToggle);
            miAutostart = new MenuItem("开机自动出现", OnAutostart);
            miAutostart.Checked = IsAutostartEnabled();
            MenuItem miExit = new MenuItem("退出", OnExit);

            tray = new NotifyIcon();
            tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            List<string> names = new List<string>();
            foreach (PetDef d in defs) names.Add(d.Name);
            string title = string.Join("和", names.ToArray()) + " 在你桌面上";
            tray.Text = title.Length > 63 ? title.Substring(0, 63) : title;
            tray.ContextMenu = new ContextMenu(new MenuItem[] { miToggle, miAutostart, miExit });
            tray.DoubleClick += OnToggle;
            tray.Visible = true;
        }

        void OnToggle(object sender, EventArgs e)
        {
            manager.ToggleVisible();
            miToggle.Text = manager.PetsVisible ? "隐藏小伙伴" : "召唤小伙伴";
        }

        void OnAutostart(object sender, EventArgs e)
        {
            bool enable = !miAutostart.Checked;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (enable)
                        key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
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
                    string expect = "\"" + Application.ExecutablePath + "\"";
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
            manager.Dispose();
            ExitThread();
        }
    }
}
