using System;
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
    }
}
