using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeskPets
{
    public sealed class PetControlForm : Form
    {
        readonly UserSettings settings;
        readonly Action changed;
        readonly Action toggle;
        readonly Button visibilityButton;
        readonly Dictionary<string, Label> statusLabels = new Dictionary<string, Label>();
        readonly Timer animationTimer;
        readonly List<AnimalPreview> previews = new List<AnimalPreview>();
        readonly float uiScale;
        readonly Color muted = Color.FromArgb(113, 106, 96);

        int Px(int value) { return (int)Math.Round(value * uiScale); }
        Font UiFont(float pixels, FontStyle style) { return new Font("Microsoft YaHei UI", pixels * uiScale, style, GraphicsUnit.Pixel); }

        public PetControlForm(List<PetDef> defs, List<SceneDef> scenes, UserSettings state,
            Action onChanged, Action onToggle, Action onExit)
        {
            AutoScaleMode = AutoScaleMode.None;
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero)) uiScale = Math.Max(1, graphics.DpiX / 96F);
            settings = state; changed = onChanged; toggle = onToggle;
            Text = "桌面小伙伴 · 选择宠物";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = UiFont(13, FontStyle.Regular);
            ForeColor = Color.FromArgb(52, 48, 43);
            BackColor = Color.FromArgb(248, 245, 240);
            StartPosition = FormStartPosition.CenterScreen;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(Px(defs.Count > 3 ? 1040 : 860), area.Width - Px(30)), Math.Min(Px(700), area.Height - Px(70)));
            MinimumSize = new Size(Math.Min(Px(740), area.Width), Math.Min(Px(560), area.Height));

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(Px(20), Px(16), Px(20), Px(12));
            layout.ColumnCount = 1; layout.RowCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            FlowLayoutPanel header = Vertical();
            header.Controls.Add(Label("我的小伙伴", UiFont(25, FontStyle.Bold)));
            header.Controls.Add(Label("勾选想留在桌面上的动物，选择会自动记住。", Font));
            layout.Controls.Add(header, 0, 0);

            FlowLayoutPanel cards = new FlowLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.AutoScroll = true;
            cards.WrapContents = true;
            cards.Margin = new Padding(0, Px(14), 0, Px(10));
            foreach (PetDef def in defs) cards.Controls.Add(Card(def));
            layout.Controls.Add(cards, 0, 1);

            GroupBox options = new GroupBox();
            options.Text = "桌面装饰与工作模式";
            options.AutoSize = true; options.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            options.Dock = DockStyle.Fill;
            options.Padding = new Padding(Px(12), Px(18), Px(12), Px(10));
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.AutoSize = true; grid.Dock = DockStyle.Fill; grid.ColumnCount = 4;
            for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.Controls.Add(Label("背景", Font), 0, 0);
            ComboBox themes = new ComboBox();
            themes.DropDownStyle = ComboBoxStyle.DropDownList;
            themes.Width = Px(165);
            themes.Items.Add(new SceneDef { Id = "none", Name = "无背景" });
            foreach (SceneDef scene in scenes) themes.Items.Add(scene);
            themes.SelectedIndex = 0;
            for (int i = 0; i < themes.Items.Count; i++)
                if (((SceneDef)themes.Items[i]).Id == settings.Background) themes.SelectedIndex = i;
            themes.SelectedIndexChanged += delegate { settings.Background = ((SceneDef)themes.SelectedItem).Id; changed(); };
            grid.Controls.Add(themes, 1, 0);
            grid.Controls.Add(Label("背景高度", Font), 2, 0);
            NumericUpDown height = new NumericUpDown();
            height.Minimum = 32; height.Maximum = 160; height.Increment = 8;
            height.Value = settings.BackgroundHeight; height.Width = Px(80);
            height.ValueChanged += delegate { settings.BackgroundHeight = (int)height.Value; changed(); };
            grid.Controls.Add(height, 3, 0);

            Label transparencyLabel = Label("透明度 " + settings.BackgroundTransparency + "%", Font);
            grid.Controls.Add(transparencyLabel, 0, 1);
            TrackBar transparency = new TrackBar();
            transparency.Minimum = 0; transparency.Maximum = 100; transparency.TickFrequency = 25;
            transparency.Value = settings.BackgroundTransparency; transparency.Width = Px(215);
            transparency.ValueChanged += delegate
            {
                settings.BackgroundTransparency = transparency.Value;
                transparencyLabel.Text = "透明度 " + transparency.Value + "%";
                changed();
            };
            grid.Controls.Add(transparency, 1, 1);
            Label limit = Label("越高越淡；100%完全透明\n仅装饰底部，不覆盖任务栏。", Font);
            limit.ForeColor = muted;
            grid.Controls.Add(limit, 2, 1); grid.SetColumnSpan(limit, 2);

            CheckBox passthrough = new CheckBox();
            passthrough.AutoSize = true;
            passthrough.Text = "暂时禁用摸摸和拖动（宠物也完全穿透鼠标）";
            passthrough.Checked = settings.ClickThrough;
            passthrough.CheckedChanged += delegate { settings.ClickThrough = passthrough.Checked; changed(); };
            grid.Controls.Add(passthrough, 0, 2); grid.SetColumnSpan(passthrough, 4);
            Label backgroundTip = Label("宠物默认可拖，透明区域穿透；背景随屏幕放大，最多占底部20%，始终穿透。", Font);
            backgroundTip.ForeColor = muted;
            grid.Controls.Add(backgroundTip, 0, 3); grid.SetColumnSpan(backgroundTip, 4);
            foreach (Control control in grid.Controls) control.Margin = new Padding(Px(6));
            options.Controls.Add(grid);
            layout.Controls.Add(options, 0, 2);

            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.AutoSize = true; footer.Dock = DockStyle.Fill;
            footer.Margin = new Padding(0, Px(12), 0, 0);
            visibilityButton = Button("隐藏全部", delegate { toggle(); RefreshState(); });
            footer.Controls.Add(visibilityButton);
            footer.Controls.Add(Button("收起控制窗口", delegate { Close(); }));
            footer.Controls.Add(Button("退出软件", delegate { onExit(); }));
            Label hint = Label("收起窗口后，桌宠继续陪伴。", Font);
            hint.Margin = new Padding(Px(12), Px(9), 0, 0); hint.ForeColor = muted;
            footer.Controls.Add(hint);
            layout.Controls.Add(footer, 0, 3);
            RefreshState();

            animationTimer = new Timer(); animationTimer.Interval = 100;
            animationTimer.Tick += delegate
            {
                if (WindowState == FormWindowState.Minimized) return;
                foreach (AnimalPreview preview in previews) preview.Invalidate();
            };
            Shown += delegate { animationTimer.Start(); };
        }

        FlowLayoutPanel Vertical()
        {
            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.AutoSize = true; flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flow.FlowDirection = FlowDirection.TopDown; flow.WrapContents = false;
            flow.Dock = DockStyle.Fill; flow.Margin = Padding.Empty;
            return flow;
        }

        Label Label(string text, Font font)
        {
            Label label = new Label();
            label.AutoSize = true; label.Text = text; label.Font = font;
            label.Margin = new Padding(0, 0, 0, Px(6));
            return label;
        }

        Button Button(string text, EventHandler clicked)
        {
            Button button = new Button();
            button.AutoSize = true; button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(Px(112), Px(36));
            button.Text = text; button.Padding = new Padding(Px(10), Px(6), Px(10), Px(6));
            button.Margin = new Padding(0, 0, Px(10), 0);
            button.BackColor = Color.White; button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(216, 208, 196);
            button.Click += clicked;
            return button;
        }

        Control Card(PetDef def)
        {
            FlowLayoutPanel card = Vertical();
            card.Dock = DockStyle.None;
            card.Padding = new Padding(Px(12));
            card.Margin = new Padding(0, 0, Px(12), Px(12));
            card.BackColor = Color.White;
            AnimalPreview preview = new AnimalPreview(def.States["idle"], def.PixelArt);
            preview.Size = new Size(Px(224), Px(140));
            preview.AccessibleName = def.Name + "动画预览";
            card.Controls.Add(preview); previews.Add(preview);
            card.Controls.Add(Label(def.Name, UiFont(19, FontStyle.Bold)));
            string type = def.Breed + " · " + (def.PixelArt ? "像素风" : def.Id == "nuomi" ? "现代玩具风" : "现代风");
            Label category = Label(type, Font); category.ForeColor = muted;
            card.Controls.Add(category);
            CheckBox selected = new CheckBox();
            selected.AutoSize = true; selected.Text = "在桌面显示";
            selected.Name = "pet-" + def.Id;
            selected.AccessibleName = "显示" + def.Name;
            selected.Checked = settings.IsSelected(def.Id);
            selected.Margin = new Padding(0, Px(4), 0, Px(6));
            selected.CheckedChanged += delegate { settings.Select(def.Id, selected.Checked); changed(); RefreshState(); };
            card.Controls.Add(selected);
            FlowLayoutPanel sizeRow = new FlowLayoutPanel();
            sizeRow.AutoSize = true;
            sizeRow.WrapContents = false;
            sizeRow.Margin = new Padding(0, Px(3), 0, Px(7));
            Label sizeTitle = Label("桌面大小", Font);
            sizeTitle.Margin = new Padding(0, Px(4), Px(6), 0);
            sizeRow.Controls.Add(sizeTitle);
            NumericUpDown size = new NumericUpDown();
            size.Minimum = 50; size.Maximum = 200; size.Increment = 25;
            size.Value = settings.SizeFor(def.Id); size.Width = Px(78);
            size.AccessibleName = def.Name + "大小";
            size.ValueChanged += delegate { settings.PetSizes[def.Id] = (int)size.Value; changed(); };
            sizeRow.Controls.Add(size);
            Label percent = Label("%", Font);
            percent.Margin = new Padding(Px(4), Px(4), 0, 0);
            sizeRow.Controls.Add(percent);
            card.Controls.Add(sizeRow);
            Label status = Label("", Font); status.ForeColor = Color.FromArgb(66, 123, 89);
            card.Controls.Add(status); statusLabels[def.Id] = status;
            return card;
        }

        public void RefreshState()
        {
            visibilityButton.Text = settings.Visible ? "隐藏全部" : "召唤已选宠物";
            foreach (var entry in statusLabels)
                entry.Value.Text = !settings.IsSelected(entry.Key) ? "未展示" : settings.Visible ? "桌面陪伴中" : "已选择 · 暂时隐藏";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && animationTimer != null) animationTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    sealed class AnimalPreview : Control
    {
        readonly StateAnim animation;
        readonly bool pixel;
        readonly Stopwatch clock = Stopwatch.StartNew();
        public AnimalPreview(StateAnim state, bool pixelArt)
        {
            animation = state; pixel = pixelArt;
            TabStop = false;
            DoubleBuffered = true; BackColor = Color.FromArgb(251, 249, 246);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Bitmap[] frames = animation.BaseRight ?? animation.Right;
            Bitmap frame = frames[(int)(clock.Elapsed.TotalSeconds * animation.Fps) % frames.Length];
            double scale = Math.Min((Width - 16.0) / frame.Width, (Height - 12.0) / frame.Height);
            if (pixel) scale = Math.Max(1, Math.Floor(scale));
            int w = (int)(frame.Width * scale), h = (int)(frame.Height * scale);
            e.Graphics.InterpolationMode = pixel ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(frame, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h));
        }
    }
}
