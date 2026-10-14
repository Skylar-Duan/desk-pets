using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeskPets
{
    /// <summary>
    /// 分层置顶小窗口：整窗只有精灵图，透明像素由系统自动点击穿透，
    /// 不进任务栏、不抢焦点。渲染走 UpdateLayeredWindow。
    /// </summary>
    public class PetWindow : Form
    {
        Bitmap lastBitmap;
        int lastX = int.MinValue, lastY = int.MinValue;
        byte lastOpacity;
        bool clickThrough;

        public bool ClickThrough
        {
            get { return clickThrough; }
            set
            {
                clickThrough = value;
                if (IsHandleCreated) NativeMethods.SetClickThrough(Handle, value);
            }
        }

        public PetWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED
                            | NativeMethods.WS_EX_TOOLWINDOW
                            | NativeMethods.WS_EX_TOPMOST
                            | NativeMethods.WS_EX_NOACTIVATE;
                if (clickThrough) cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        /// <summary>把当前帧画到屏幕 (x,y)。位图与位置都没变时跳过。</summary>
        public void Render(Bitmap bmp, int x, int y)
        {
            Render(bmp, x, y, 255);
        }

        public void Render(Bitmap bmp, int x, int y, byte opacity)
        {
            if (bmp == null || IsDisposed || !IsHandleCreated) return;
            if (ReferenceEquals(bmp, lastBitmap) && x == lastX && y == lastY && lastOpacity == opacity) return;
            lastBitmap = bmp;
            lastX = x;
            lastY = y;
            lastOpacity = opacity;

            IntPtr screenDC = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memDC = NativeMethods.CreateCompatibleDC(screenDC);
            IntPtr hBmp = IntPtr.Zero;
            IntPtr oldBmp = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                oldBmp = NativeMethods.SelectObject(memDC, hBmp);
                NativeMethods.POINT dst = new NativeMethods.POINT(x, y);
                NativeMethods.SIZE size = new NativeMethods.SIZE(bmp.Width, bmp.Height);
                NativeMethods.POINT src = new NativeMethods.POINT(0, 0);
                NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION();
                blend.BlendOp = NativeMethods.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = opacity;
                blend.AlphaFormat = NativeMethods.AC_SRC_ALPHA;
                NativeMethods.UpdateLayeredWindow(Handle, screenDC, ref dst, ref size,
                    memDC, ref src, 0, ref blend, NativeMethods.ULW_ALPHA);
            }
            finally
            {
                if (oldBmp != IntPtr.Zero) NativeMethods.SelectObject(memDC, oldBmp);
                if (hBmp != IntPtr.Zero) NativeMethods.DeleteObject(hBmp);
                NativeMethods.DeleteDC(memDC);
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDC);
            }
        }

        /// <summary>强制下一次 Render 必绘（显示/隐藏切换后用）。</summary>
        public void InvalidateCache()
        {
            lastBitmap = null;
            lastX = int.MinValue;
            lastY = int.MinValue;
        }
    }
}
