using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeskPets
{
    static class PetPalette
    {
        public static readonly Color Ink = Color.FromArgb(39, 53, 48);
        public static readonly Color Muted = Color.FromArgb(112, 122, 112);
        public static readonly Color Green = Color.FromArgb(69, 105, 82);
        public static readonly Color Line = Color.FromArgb(224, 227, 215);
        public static readonly Color Paper = Color.FromArgb(246, 246, 237);
        public static readonly Color Soft = Color.FromArgb(233, 238, 225);
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static void Fill(Graphics g, RectangleF r, float radius, Color color)
        { using (GraphicsPath p = Round(r, radius)) using (Brush b = new SolidBrush(color)) g.FillPath(b, p); }
        public static void Border(Graphics g, RectangleF r, float radius, Color color, float width)
        { using (GraphicsPath p = Round(r, radius)) using (Pen pen = new Pen(color, width)) g.DrawPath(pen, p); }
        public static void Text(Graphics g, string text, Font font, Color color, Rectangle r, ContentAlignment align)
        {
            TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (align == ContentAlignment.MiddleRight) flags |= TextFormatFlags.Right | TextFormatFlags.VerticalCenter;
            else if (align == ContentAlignment.MiddleCenter) flags |= TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            else flags |= TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, text, font, r, color, flags);
        }
    }

    sealed class PetSurface : Panel
    {
        public float ScaleFactor = 1;
        public PetSurface() { DoubleBuffered = true; BackColor = PetPalette.Paper; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PetPalette.Fill(e.Graphics, new RectangleF(0, 0, Width - 1, Height - 1), 18 * ScaleFactor, Color.FromArgb(255, 255, 249));
            PetPalette.Border(e.Graphics, new RectangleF(.5F, .5F, Width - 2, Height - 2), 18 * ScaleFactor, PetPalette.Line, 1);
        }
    }

    sealed class ModernButton : Control
    {
        public bool Primary;
        public bool Quiet;
        public float ScaleFactor = 1;
        bool hover, down;
        public ModernButton()
        { DoubleBuffered = true; SetStyle(ControlStyles.Selectable, true); TabStop = true; Cursor = Cursors.Hand; BackColor = PetPalette.Paper; AccessibleRole = AccessibleRole.PushButton; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? (down ? Color.FromArgb(43, 77, 56) : hover ? Color.FromArgb(56, 90, 67) : PetPalette.Green) : (down ? PetPalette.Line : hover ? PetPalette.Soft : Quiet ? BackColor : Color.FromArgb(255, 255, 249));
            PetPalette.Fill(e.Graphics, new RectangleF(0, 0, Width - 1, Height - 1), 10 * ScaleFactor, fill);
            if (!Primary && !Quiet) PetPalette.Border(e.Graphics, new RectangleF(.5F, .5F, Width - 2, Height - 2), 10 * ScaleFactor, PetPalette.Line, 1);
            PetPalette.Text(e.Graphics, Text, Font, Primary ? Color.White : PetPalette.Ink, ClientRectangle, ContentAlignment.MiddleCenter);
            if (Focused) PetPalette.Border(e.Graphics, new RectangleF(3, 3, Width - 7, Height - 7), 8 * ScaleFactor, Primary ? Color.FromArgb(190, 211, 184) : PetPalette.Green, 1);
        }
    }

    sealed class ModernSegments : Control
    {
        public string[] Items = new string[0];
        public float ScaleFactor = 1;
        int selectedIndex;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex { get { return selectedIndex; } set { int v = Math.Max(0, Math.Min(Items.Length - 1, value)); if (v == selectedIndex) return; selectedIndex = v; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); } }
        public ModernSegments() { DoubleBuffered = true; TabStop = true; Cursor = Cursors.Hand; BackColor = Color.FromArgb(255,255,249); AccessibleRole = AccessibleRole.PageTabList; }
        protected override void OnMouseDown(MouseEventArgs e)
        { if (e.Button == MouseButtons.Left && Items.Length > 0) { Focus(); SelectedIndex = Math.Min(Items.Length - 1, e.X * Items.Length / Math.Max(1, Width)); } base.OnMouseDown(e); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        { if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) { SelectedIndex += e.KeyCode == Keys.Left ? -1 : 1; e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            PetPalette.Fill(e.Graphics, new RectangleF(0, 0, Width - 1, Height - 1), 10 * ScaleFactor, PetPalette.Soft);
            if (Items.Length == 0) return;
            for (int i = 0; i < Items.Length; i++)
            {
                Rectangle r = new Rectangle(i * Width / Items.Length + 3, 3, Width / Items.Length - 6, Height - 6);
                if (i == selectedIndex) PetPalette.Fill(e.Graphics, r, 8 * ScaleFactor, Color.FromArgb(255, 255, 249));
                PetPalette.Text(e.Graphics, Items[i], Font, i == selectedIndex ? PetPalette.Green : PetPalette.Muted, r, ContentAlignment.MiddleCenter);
                if (i == selectedIndex && Focused) PetPalette.Border(e.Graphics, r, 8 * ScaleFactor, PetPalette.Green, 1);
            }
        }
    }

    sealed class ModernSlider : Control
    {
        public int Minimum = 0, Maximum = 100;
        public string Suffix = "%";
        public string LeftHint = "", RightHint = "";
        public float ScaleFactor = 1;
        int value;
        bool dragging, hover;
        public event EventHandler ValueChanged;
        public event EventHandler DragCompleted;
        public bool IsDragging { get { return dragging; } }
        public int Value { get { return value; } set { int v = Math.Max(Minimum, Math.Min(Maximum, value)); if (v == this.value) return; this.value = v; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); } }
        public ModernSlider() { DoubleBuffered = true; TabStop = true; Cursor = Cursors.Hand; BackColor = Color.FromArgb(255,255,249); AccessibleRole = AccessibleRole.Slider; }
        int P(float v) { return (int)Math.Round(v * ScaleFactor); }
        void SetFromMouse(int x) { int pad = P(10); Value = Minimum + (int)Math.Round((Maximum - Minimum) * Math.Max(0, Math.Min(1, (x - pad) / (double)Math.Max(1, Width - pad * 2)))); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Focus(); if (e.Y >= P(27) && e.Y <= P(57)) { Capture = true; dragging = true; SetFromMouse(e.X); } } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromMouse(e.X); base.OnMouseMove(e); }
        void FinishDrag()
        {
            if (!dragging) return;
            dragging = false; Capture = false; Invalidate();
            if (DragCompleted != null) DragCompleted(this, EventArgs.Empty);
        }
        protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left) FinishDrag(); base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) FinishDrag(); base.OnMouseCaptureChanged(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys keyData) { Keys k = keyData & Keys.KeyCode; return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || k == Keys.Home || k == Keys.End || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = e.Shift ? 10 : 1;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value -= step;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value += step;
            else if (e.KeyCode == Keys.Home) Value = Minimum;
            else if (e.KeyCode == Keys.End) Value = Maximum;
            else { base.OnKeyDown(e); return; }
            e.Handled = true;
        }
        protected override void OnMouseWheel(MouseEventArgs e) { if (Focused) Value += Math.Sign(e.Delta); base.OnMouseWheel(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Color ink = Enabled ? PetPalette.Ink : PetPalette.Muted;
            PetPalette.Text(g, Text, Font, ink, new Rectangle(0, 0, Width - P(72), P(25)), ContentAlignment.MiddleLeft);
            Rectangle badge = new Rectangle(Width - P(68), 0, P(68), P(25));
            PetPalette.Fill(g, badge, P(7), Enabled ? PetPalette.Soft : Color.FromArgb(241,242,234));
            PetPalette.Text(g, Value + Suffix, Font, Enabled ? PetPalette.Green : PetPalette.Muted, badge, ContentAlignment.MiddleCenter);
            int pad = P(10), y = P(41), x = pad + (int)Math.Round((Width - pad * 2) * (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum));
            using (Pen pen = new Pen(PetPalette.Line, P(5))) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, pad, y, Width - pad, y); }
            if (Enabled) using (Pen pen = new Pen(PetPalette.Green, P(5))) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, pad, y, x, y); }
            if (Focused || dragging || hover) using (Brush b = new SolidBrush(Color.FromArgb(35, PetPalette.Green))) g.FillEllipse(b, x - P(14), y - P(14), P(28), P(28));
            using (Brush b = new SolidBrush(Enabled ? PetPalette.Green : PetPalette.Muted)) g.FillEllipse(b, x - P(8), y - P(8), P(16), P(16));
            using (Brush b = new SolidBrush(Color.White)) g.FillEllipse(b, x - P(3), y - P(3), P(6), P(6));
            using (Font small = new Font(Font.FontFamily, Font.Size * .83F, Font.Style, Font.Unit))
            {
                PetPalette.Text(g, LeftHint, small, PetPalette.Muted, new Rectangle(0, P(56), Width / 2, P(18)), ContentAlignment.MiddleLeft);
                PetPalette.Text(g, RightHint, small, PetPalette.Muted, new Rectangle(Width / 2, P(56), Width / 2, P(18)), ContentAlignment.MiddleRight);
            }
        }
    }

    sealed class ModernToggle : Control
    {
        public float ScaleFactor = 1;
        bool isChecked;
        public event EventHandler CheckedChanged;
        public bool Checked { get { return isChecked; } set { if (isChecked == value) return; isChecked = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
        public ModernToggle() { DoubleBuffered = true; TabStop = true; Cursor = Cursors.Hand; BackColor = PetPalette.Paper; AccessibleRole = AccessibleRole.CheckButton; }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int w = (int)(36 * ScaleFactor), h = (int)(21 * ScaleFactor), y = (Height - h) / 2;
            PetPalette.Fill(e.Graphics, new RectangleF(0,y,w,h), h / 2F, Checked ? PetPalette.Green : Color.FromArgb(183,190,178));
            using (Brush b = new SolidBrush(Color.White)) e.Graphics.FillEllipse(b, Checked ? w-h+3 : 3, y+3, h-6,h-6);
            PetPalette.Text(e.Graphics, Text, Font, PetPalette.Ink, new Rectangle(w+(int)(10*ScaleFactor),0,Width-w-(int)(10*ScaleFactor),Height), ContentAlignment.MiddleLeft);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(w+5,2,Width-w-6,Height-4));
        }
    }
}
