using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace DeskPets
{
    public sealed class PetControlForm : Form
    {
        readonly UserSettings settings;
        readonly Action changed, toggle;
        readonly List<PetDef> definitions;
        readonly List<SceneDef> sceneDefinitions = new List<SceneDef>();
        readonly List<PetCard> cards = new List<PetCard>();
        readonly Timer animationTimer;
        readonly float uiScale;
        readonly PetSurface adjustment = new PetSurface();
        readonly ModernSegments modes = new ModernSegments(), scenes = new ModernSegments();
        readonly ModernSlider size = new ModernSlider(), height = new ModernSlider(), transparency = new ModernSlider();
        readonly ModernToggle interaction = new ModernToggle();
        readonly ModernButton visibilityButton, collapseButton, exitButton, resetButton, updateLink;
        readonly ModernRefreshButton refreshButton = new ModernRefreshButton();
        readonly ToolTip tooltips = new ToolTip();
        readonly Label title, subtitle, galleryTitle, galleryHint, panelTitle, modeHint, backgroundTitle, updateStatus;
        CancellationTokenSource updateCancellation;
        string updateUrl;
        ScaleAdjustment scaleSnapshot;
        string focusedId;
        bool refreshing;
        public bool IsAdjustingSizes { get { return size.IsDragging; } }
        public event EventHandler SizeAdjustmentFinished;

        int P(float v) { return (int)Math.Round(v * uiScale); }
        Font F(float pixels, FontStyle style) { return new Font("Microsoft YaHei UI", pixels * uiScale, style, GraphicsUnit.Pixel); }

        public PetControlForm(List<PetDef> defs, List<SceneDef> backgrounds, UserSettings state,
            Action onChanged, Action onToggle, Action onExit)
        {
            AutoScaleMode = AutoScaleMode.None;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            float dpi;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) dpi = Math.Max(1, g.DpiX / 96F);
            // Fit the complete surface on compact screens, including high-DPI laptops.
            int chromeHeight = Math.Max(70, SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height * 2 + 12);
            uiScale = Math.Max(.8F, Math.Min(dpi, Math.Min((area.Width - 36) / 940F, (area.Height - chromeHeight) / 660F)));
            settings = state; changed = onChanged; toggle = onToggle; definitions = defs;
            focusedId = defs.Count > 0 ? defs[0].Id : "";
            foreach (PetDef def in defs) if (settings.IsSelected(def.Id)) { focusedId = def.Id; break; }
            Text = "桌面小伙伴"; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = F(13, FontStyle.Regular); ForeColor = PetPalette.Ink; BackColor = PetPalette.Paper;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(P(940), P(660));
            MinimumSize = new Size(P(880) + SystemInformation.FrameBorderSize.Width * 2,
                P(660) + SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height * 2);
            DoubleBuffered = true;
            title = MakeLabel("桌面小伙伴", 27, FontStyle.Bold, PetPalette.Ink, this);
            subtitle = MakeLabel("给桌面一点柔软的陪伴。", 13, FontStyle.Regular, PetPalette.Muted, this);
            galleryTitle = MakeLabel("我的伙伴", 14, FontStyle.Bold, PetPalette.Ink, this);
            galleryHint = MakeLabel("点卡片调整 · 右上角开关显示", 12, FontStyle.Regular, PetPalette.Muted, this);
            adjustment.ScaleFactor = uiScale; Controls.Add(adjustment);
            panelTitle = MakeLabel("陪伴设置", 16, FontStyle.Bold, PetPalette.Ink, adjustment);
            modeHint = MakeLabel("", 12, FontStyle.Regular, PetPalette.Muted, adjustment);
            backgroundTitle = MakeLabel("桌面背景", 14, FontStyle.Bold, PetPalette.Ink, adjustment);
            updateStatus = MakeLabel("", 12, FontStyle.Regular, PetPalette.Muted, this);
            updateStatus.TextAlign = ContentAlignment.MiddleRight;
            refreshButton.ScaleFactor = uiScale; Controls.Add(refreshButton);
            refreshButton.Click += async delegate { await CheckUpdatesAsync(); };
            string version = new UpdateChecker().CurrentVersion;
            tooltips.SetToolTip(refreshButton, "检查版本更新 · 当前 v" + version);
            updateLink = MakeButton("", false, false, delegate { OpenUpdatePage(); }); updateLink.Visible = false;
            modes.Items = new string[] { "单独调整", "伙伴一起", "整个桌面" }; modes.AccessibleName = "调整范围";
            Setup(modes, adjustment);
            modes.SelectedIndexChanged += delegate { CaptureScale(); RefreshControls(); };
            size.Minimum = 50; size.Maximum = 300; size.AccessibleName = "宠物大小"; Setup(size, adjustment);
            size.ValueChanged += delegate
            {
                if (refreshing) return;
                if (modes.SelectedIndex == 0) settings.PetSizes[focusedId] = size.Value;
                else { if (scaleSnapshot == null) CaptureScale(); scaleSnapshot.Apply(settings, size.Value, modes.SelectedIndex == 2); }
                changed(); RefreshState();
            };
            size.DragCompleted += delegate { if (SizeAdjustmentFinished != null) SizeAdjustmentFinished(this, EventArgs.Empty); };
            sceneDefinitions.Add(new SceneDef { Id = "none", Name = "无背景" }); sceneDefinitions.AddRange(backgrounds);
            List<string> sceneNames = new List<string>();
            foreach (SceneDef scene in sceneDefinitions) sceneNames.Add(scene.Id == "none" ? "关闭" : scene.Name.Replace("背景", "").Replace("场景", ""));
            scenes.Items = sceneNames.ToArray(); scenes.AccessibleName = "背景样式"; Setup(scenes, adjustment);
            scenes.SelectedIndexChanged += delegate { if (refreshing) return; settings.Background = sceneDefinitions[scenes.SelectedIndex].Id; changed(); RefreshControls(); };
            height.Text = "背景高度"; height.Minimum = 32; height.Maximum = 160; height.Suffix = " px";
            height.LeftHint = "32 px"; height.RightHint = "160 px"; height.AccessibleName = "背景高度"; Setup(height, adjustment);
            height.ValueChanged += delegate { if (refreshing) return; settings.BackgroundHeight = height.Value; changed(); if (modes.SelectedIndex > 0) CaptureScale(); RefreshControls(); };
            transparency.Text = "背景透明度"; transparency.Minimum = 0; transparency.Maximum = 100;
            transparency.LeftHint = "更清晰"; transparency.RightHint = "更通透"; transparency.AccessibleName = "背景透明度"; Setup(transparency, adjustment);
            transparency.ValueChanged += delegate { if (refreshing) return; settings.BackgroundTransparency = transparency.Value; changed(); };
            interaction.Text = "允许摸摸与拖动"; interaction.Checked = !settings.ClickThrough; interaction.AccessibleName = "允许摸摸与拖动"; Setup(interaction, this);
            interaction.CheckedChanged += delegate { if (refreshing) return; settings.ClickThrough = !interaction.Checked; changed(); };
            visibilityButton = MakeButton("", false, false, delegate { toggle(); RefreshState(); });
            collapseButton = MakeButton("收起窗口", true, false, delegate { Close(); });
            exitButton = MakeButton("退出", false, true, delegate { onExit(); });
            resetButton = MakeButton("恢复默认", false, true, delegate { RestoreDefaults(); });
            resetButton.AccessibleName = "恢复默认";
            tooltips.SetToolTip(resetButton, "只恢复宠物100%与背景60px，保留显示选择和其它设置");
            foreach (PetDef def in defs)
            {
                PetCard card = new PetCard(def, uiScale); card.Font = Font;
                card.AccessibleName = def.Name + "，回车调整大小，空格切换桌面显示";
                card.Activated += delegate(object sender, EventArgs e) { focusedId = ((PetCard)sender).Definition.Id; modes.SelectedIndex = 0; RefreshControls(); };
                card.SelectionChanged += delegate(object sender, EventArgs e)
                {
                    PetCard selected = (PetCard)sender;
                    settings.Select(selected.Definition.Id, !settings.IsSelected(selected.Definition.Id)); changed(); CaptureScale(); RefreshControls();
                };
                Controls.Add(card); cards.Add(card);
            }
            CaptureScale(); RefreshControls(); Resize += delegate { LayoutControls(); }; LayoutControls();
            animationTimer = new Timer(); animationTimer.Interval = 100;
            animationTimer.Tick += delegate { if (WindowState != FormWindowState.Minimized) foreach (PetCard card in cards) card.InvalidatePreview(); };
            Shown += delegate { animationTimer.Start(); };
        }

        void Setup(Control c, Control parent)
        {
            c.Font = Font; parent.Controls.Add(c);
            ModernSlider slider = c as ModernSlider; if (slider != null) slider.ScaleFactor = uiScale;
            ModernSegments segment = c as ModernSegments; if (segment != null) segment.ScaleFactor = uiScale;
            ModernToggle switchControl = c as ModernToggle; if (switchControl != null) switchControl.ScaleFactor = uiScale;
        }
        Label MakeLabel(string text, float pixels, FontStyle style, Color color, Control parent)
        {
            Label label = new Label(); label.Text = text; label.Font = F(pixels, style); label.ForeColor = color;
            label.BackColor = Color.Transparent; label.AutoSize = false; label.TextAlign = ContentAlignment.MiddleLeft;
            label.UseCompatibleTextRendering = false; label.AutoEllipsis = true; parent.Controls.Add(label); return label;
        }
        ModernButton MakeButton(string text, bool primary, bool quiet, EventHandler click)
        {
            ModernButton button = new ModernButton(); button.Text = text; button.Font = F(13, primary ? FontStyle.Bold : FontStyle.Regular);
            button.ScaleFactor = uiScale; button.Primary = primary; button.Quiet = quiet; button.Click += click; Controls.Add(button); return button;
        }
        void RestoreDefaults()
        {
            settings.RestoreDefaults();
            CaptureScale(); RefreshControls();
            changed();
            if (SizeAdjustmentFinished != null) SizeAdjustmentFinished(this, EventArgs.Empty);
        }
        async Task CheckUpdatesAsync()
        {
            if (updateCancellation != null) return;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            updateCancellation = cancellation;
            refreshButton.Busy = true; updateLink.Visible = false;
            updateStatus.Text = "正在检查更新…";
            try
            {
                UpdateResult result = await new UpdateChecker().CheckAsync(cancellation.Token);
                if (IsDisposed || cancellation.IsCancellationRequested || result.Cancelled) return;
                tooltips.SetToolTip(refreshButton, result.Message);
                updateStatus.Text = result.Message;
                if (result.Success && result.HasUpdate)
                {
                    updateUrl = result.ReleaseUrl;
                    updateLink.Text = "更新至 v" + result.LatestVersion + "  ↗";
                    updateLink.AccessibleName = "打开新版下载页面";
                    updateStatus.Text = ""; updateLink.Visible = true;
                }
            }
            catch (Exception)
            {
                if (!IsDisposed) updateStatus.Text = "暂时无法检查，请重试";
            }
            finally
            {
                if (!IsDisposed) refreshButton.Busy = false;
                updateCancellation = null;
                cancellation.Dispose();
            }
        }
        void OpenUpdatePage()
        {
            if (string.IsNullOrEmpty(updateUrl)) return;
            try { Process.Start(new ProcessStartInfo(updateUrl) { UseShellExecute = true }); }
            catch (Exception) { updateStatus.Text = "暂时无法打开下载页面"; updateLink.Visible = false; }
        }
        void CaptureScale()
        {
            scaleSnapshot = ScaleAdjustment.Capture(settings); refreshing = true;
            size.Minimum = modes.SelectedIndex == 0 ? 50 : scaleSnapshot.MinimumPercent(modes.SelectedIndex == 2);
            size.Maximum = modes.SelectedIndex == 0 ? 300 : scaleSnapshot.MaximumPercent(modes.SelectedIndex == 2);
            size.Value = modes.SelectedIndex == 0 ? settings.SizeFor(focusedId) : 100; refreshing = false;
        }
        void RefreshControls()
        {
            refreshing = true;
            PetDef active = definitions.Find(delegate(PetDef def) { return def.Id == focusedId; });
            bool single = modes.SelectedIndex == 0, everything = modes.SelectedIndex == 2;
            if (single) { size.Minimum = 50; size.Maximum = 300; size.Value = settings.SizeFor(focusedId); }
            size.Text = single ? (active == null ? "宠物大小" : active.Name + "的大小") : everything ? "伙伴与背景一起缩放" : "已选伙伴一起缩放";
            size.LeftHint = single ? size.Minimum + "%" : "100% = 进入时大小";
            size.RightHint = single ? size.Maximum + "%" : size.Minimum + "–" + size.Maximum + "%";
            size.Enabled = single || settings.SelectedPets.Length > 0;
            modeHint.Text = single ? "只改变这位伙伴，其他伙伴保持原样。" : everything ? "同步 " + settings.SelectedPets.Length + " 位伙伴与背景，保持比例。" : "同步 " + settings.SelectedPets.Length + " 位已选伙伴，保持大小比例。";
            if (!single && size.Maximum == 100)
                modeHint.Text = everything && settings.BackgroundHeight >= 160 && scaleSnapshot.MaximumPercent(false) > 100
                    ? "背景已到高度上限，可先调低背景。" : "已有伙伴达到上限，可先单独调整。";
            for (int i = 0; i < sceneDefinitions.Count; i++) if (sceneDefinitions[i].Id == settings.Background) scenes.SelectedIndex = i;
            height.Value = settings.BackgroundHeight; transparency.Value = settings.BackgroundTransparency;
            height.Enabled = settings.Background != "none"; transparency.Enabled = settings.Background != "none";
            refreshing = false; RefreshState();
        }
        public void RefreshState()
        {
            if (visibilityButton == null) return;
            visibilityButton.Text = settings.Visible ? "隐藏伙伴" : "召唤伙伴";
            galleryTitle.Text = "我的伙伴  /  " + settings.SelectedPets.Length + " 位已选";
            foreach (PetCard card in cards)
            {
                card.Selected = settings.IsSelected(card.Definition.Id); card.Editing = card.Definition.Id == focusedId && modes.SelectedIndex == 0;
                card.PetVisible = settings.Visible; card.SizePercent = settings.SizeFor(card.Definition.Id); card.Invalidate();
            }
            if (modes.SelectedIndex == 2) { refreshing = true; height.Value = settings.BackgroundHeight; refreshing = false; }
        }
        void LayoutControls()
        {
            if (title == null) return;
            int margin = P(24), gap = P(20), rightWidth = P(316), leftWidth = ClientSize.Width - P(48) - gap - rightWidth;
            int panelY = P(102), bottom = ClientSize.Height - P(78), contentHeight = bottom - panelY;
            title.SetBounds(margin, P(17), P(350), P(38)); subtitle.SetBounds(margin, P(56), P(420), P(25));
            refreshButton.SetBounds(ClientSize.Width - margin - P(36), P(29), P(36), P(36));
            updateStatus.SetBounds(refreshButton.Left - P(250), P(30), P(240), P(34));
            updateLink.SetBounds(refreshButton.Left - P(220), P(29), P(210), P(36));
            galleryTitle.SetBounds(margin, P(83), P(230), P(24));
            galleryHint.SetBounds(margin + P(230), P(83), Math.Max(P(120), leftWidth - P(230)), P(24)); galleryHint.TextAlign = ContentAlignment.MiddleRight;
            int cardsY = P(117), cardGap = P(12), cardWidth = (leftWidth - cardGap) / 2, cardHeight = (bottom - cardsY - cardGap) / 2;
            for (int i = 0; i < cards.Count; i++) cards[i].SetBounds(margin + (i % 2) * (cardWidth + cardGap), cardsY + (i / 2) * (cardHeight + cardGap), cardWidth, cardHeight);
            adjustment.SetBounds(margin + leftWidth + gap, panelY, rightWidth, contentHeight);
            int inner = P(18), w = rightWidth - inner * 2;
            panelTitle.SetBounds(inner, P(13), w, P(26)); modes.SetBounds(inner, P(48), w, P(38)); modeHint.SetBounds(inner, P(94), w, P(29));
            size.SetBounds(inner, P(132), w, P(77)); backgroundTitle.SetBounds(inner, P(226), w, P(26)); scenes.SetBounds(inner, P(261), w, P(34));
            height.SetBounds(inner, P(310), w, P(77)); transparency.SetBounds(inner, P(398), w, P(77));
            int footerY = ClientSize.Height - P(58);
            interaction.SetBounds(margin, footerY, P(225), P(36)); exitButton.SetBounds(ClientSize.Width - margin - P(60), footerY, P(60), P(36));
            collapseButton.SetBounds(exitButton.Left - P(124), footerY, P(112), P(36)); visibilityButton.SetBounds(collapseButton.Left - P(122), footerY, P(112), P(36));
            resetButton.SetBounds(visibilityButton.Left - P(124), footerY, P(112), P(36)); Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); using (Pen p = new Pen(PetPalette.Line)) e.Graphics.DrawLine(p, P(24), ClientSize.Height - P(76), ClientSize.Width - P(24), ClientSize.Height - P(76));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (updateCancellation != null) updateCancellation.Cancel();
                if (animationTimer != null) animationTimer.Dispose();
                tooltips.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    sealed class PetCard : Control
    {
        public readonly PetDef Definition;
        public bool Selected, Editing, PetVisible;
        public int SizePercent = 100;
        public event EventHandler Activated, SelectionChanged;
        readonly float scale;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Rectangle previewCrop;
        bool hover;
        int P(float v) { return (int)Math.Round(v * scale); }
        public PetCard(PetDef def, float uiScale)
        {
            Definition = def; scale = uiScale; DoubleBuffered = true; BackColor = PetPalette.Paper; TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.CheckButton;
            StateAnim idle = def.States["idle"];
            Bitmap[] frames = idle.SourceRight ?? idle.BaseRight ?? idle.Right;
            Rectangle union = Rectangle.Empty;
            foreach (Bitmap frame in frames)
            {
                Rectangle occupied = VisibleBounds(frame);
                if (!occupied.IsEmpty) union = union.IsEmpty ? occupied : Rectangle.Union(union, occupied);
            }
            previewCrop = union.IsEmpty ? new Rectangle(0, 0, frames[0].Width, frames[0].Height) : union;
        }
        Rectangle PreviewBounds { get { return new Rectangle(P(12), P(40), Width - P(24), Height - P(104)); } }
        public void InvalidatePreview() { Invalidate(PreviewBounds); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                if (new Rectangle(Width - P(62), 0, P(62), P(44)).Contains(e.Location)) { if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); }
                else if (Activated != null) Activated(this, EventArgs.Empty);
            }
            base.OnMouseDown(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { if (Activated != null) Activated(this, EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF outer = new RectangleF(1, 1, Width - 3, Height - 3);
            Color fill = Editing ? Color.FromArgb(237, 242, 226) : hover ? Color.FromArgb(251, 252, 244) : Color.FromArgb(255, 255, 249);
            PetPalette.Fill(g, outer, P(15), fill); PetPalette.Border(g, outer, P(15), Editing ? PetPalette.Green : PetPalette.Line, Editing ? P(1.5F) : 1);
            using (Font small = new Font(Font.FontFamily, Font.Size * .85F, FontStyle.Regular, Font.Unit))
            {
                PetPalette.Text(g, Definition.PixelArt ? "像素伙伴" : "现代伙伴", small, PetPalette.Muted, new Rectangle(P(15), P(10), Width - P(78), P(24)), ContentAlignment.MiddleLeft);
                string state = !Selected ? "未显示" : PetVisible ? "陪伴中" : "已隐藏";
                PetPalette.Text(g, state, small, Selected ? PetPalette.Green : PetPalette.Muted, new Rectangle(P(16), Height - P(34), Width - P(95), P(22)), ContentAlignment.MiddleLeft);
                PetPalette.Text(g, SizePercent + "%", small, PetPalette.Muted, new Rectangle(Width - P(76), Height - P(34), P(60), P(22)), ContentAlignment.MiddleRight);
            }
            int tw = P(32), th = P(19), tx = Width - P(47), ty = P(15);
            PetPalette.Fill(g, new RectangleF(tx, ty, tw, th), th / 2F, Selected ? PetPalette.Green : Color.FromArgb(198,205,189));
            using (Brush b = new SolidBrush(Color.White)) g.FillEllipse(b, Selected ? tx + tw - th + P(3) : tx + P(3), ty + P(3), th - P(6), th - P(6));
            DrawAnimal(g, PreviewBounds);
            using (Font name = new Font(Font.FontFamily, P(19), FontStyle.Bold, GraphicsUnit.Pixel)) PetPalette.Text(g, Definition.Name, name, PetPalette.Ink, new Rectangle(P(16), Height - P(67), P(75), P(30)), ContentAlignment.MiddleLeft);
            using (Font breed = new Font(Font.FontFamily, P(12), FontStyle.Regular, GraphicsUnit.Pixel)) PetPalette.Text(g, Definition.Breed, breed, PetPalette.Muted, new Rectangle(P(86), Height - P(64), Width - P(102), P(28)), ContentAlignment.MiddleRight);
            if (Focused) PetPalette.Border(g, new RectangleF(P(5),P(5),Width-P(11),Height-P(11)), P(12), PetPalette.Green, 1);
        }
        void DrawAnimal(Graphics g, Rectangle bounds)
        {
            StateAnim animation = Definition.States["idle"]; Bitmap[] frames = animation.SourceRight ?? animation.BaseRight ?? animation.Right;
            if (frames == null || frames.Length == 0) return;
            Bitmap frame = frames[(int)(clock.Elapsed.TotalSeconds * animation.Fps) % frames.Length];
            double ratio = Math.Min((bounds.Width - P(12)) / (double)previewCrop.Width, (bounds.Height - P(8)) / (double)previewCrop.Height);
            if (Definition.PixelArt && ratio * Definition.Scale >= 1)
                ratio = Math.Floor(ratio * Definition.Scale) / Definition.Scale;
            int w = (int)(previewCrop.Width * ratio), h = (int)(previewCrop.Height * ratio);
            g.InterpolationMode = Definition.PixelArt ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(frame, new Rectangle(bounds.X + (bounds.Width - w) / 2, bounds.Bottom - h, w, h), previewCrop, GraphicsUnit.Pixel);
        }
        static Rectangle VisibleBounds(Bitmap frame)
        {
            Rectangle all = new Rectangle(0, 0, frame.Width, frame.Height);
            BitmapData data = frame.LockBits(all, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[frame.Width * 4]; int minX = frame.Width, minY = frame.Height, maxX = -1, maxY = -1;
                for (int y = 0; y < frame.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < frame.Width; x++) if (row[x * 4 + 3] >= 16)
                    { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                }
                return maxX < minX ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            }
            finally { frame.UnlockBits(data); }
        }
    }
}
