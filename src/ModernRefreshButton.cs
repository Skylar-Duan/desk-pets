using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeskPets
{
    sealed class ModernRefreshButton : Control
    {
        public float ScaleFactor = 1;
        readonly Timer spinner = new Timer();
        bool busy, hover;
        float angle;
        public bool Busy
        {
            get { return busy; }
            set { busy = value; Enabled = !value; spinner.Enabled = value; if (!value) angle = 0; Invalidate(); }
        }
        public ModernRefreshButton()
        {
            DoubleBuffered = true; TabStop = true; Cursor = Cursors.Hand;
            BackColor = PetPalette.Paper; AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = "检查版本更新";
            spinner.Interval = 40;
            spinner.Tick += delegate { angle = (angle + 12) % 360; Invalidate(); };
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF surface = new RectangleF(1, 1, Width - 3, Height - 3);
            if (hover || Focused || busy) PetPalette.Fill(g, surface, 10 * ScaleFactor, PetPalette.Soft);
            float radius = 8 * ScaleFactor, cx = Width / 2F, cy = Height / 2F;
            using (Pen pen = new Pen(PetPalette.Green, 1.9F * ScaleFactor))
            using (Brush brush = new SolidBrush(PetPalette.Green))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawArc(pen, cx - radius, cy - radius, radius * 2, radius * 2, 55 + angle, 290);
                double end = (345 + angle) * Math.PI / 180;
                PointF tip = new PointF(cx + radius * (float)Math.Cos(end), cy + radius * (float)Math.Sin(end));
                float dx = -(float)Math.Sin(end), dy = (float)Math.Cos(end), length = 5 * ScaleFactor, half = 3 * ScaleFactor;
                g.FillPolygon(brush, new PointF[] { tip,
                    new PointF(tip.X - dx * length - dy * half, tip.Y - dy * length + dx * half),
                    new PointF(tip.X - dx * length + dy * half, tip.Y - dy * length - dx * half) });
            }
            if (Focused) PetPalette.Border(g, surface, 10 * ScaleFactor, PetPalette.Green, 1);
        }
        protected override void Dispose(bool disposing)
        { if (disposing) spinner.Dispose(); base.Dispose(disposing); }
    }
}
