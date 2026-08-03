using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DeskPets
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "DeskPets_SingleInstance", out createdNew))
            {
                if (!createdNew) return;   // 已经在跑了
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // 消息循环内任何异常：记日志、一句人话、干净退出——绝不反复弹技术报错框
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnThreadException;
                try
                {
                    Application.Run(new TrayContext());
                }
                catch (InvalidOperationException)
                {
                    // 没有宠物素材，提示框已弹出
                }
            }
        }

        static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                string log = Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath), "error.txt");
                File.AppendAllText(log,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + e.Exception + "\r\n\r\n");
            }
            catch (Exception) { }
            MessageBox.Show("小伙伴遇到点问题，先回窝休息啦。重新双击 DeskPets.exe 就能叫回来。",
                "桌面小伙伴", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Exit();
        }
    }
}
