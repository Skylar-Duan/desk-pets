using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DeskPets
{
    /// <summary>
    /// 管全体宠物：统一 30Hz 心跳、鼠标交互（摸/拖/抛）、活跃度感知、显隐。
    /// </summary>
    public class PetManager : IDisposable
    {
        class PetItem
        {
            public PetBrain Brain;
            public PetWindow Window;
            public bool Dragging;
            public bool Moved;
            public Point DownPos;
            public double DownTime;
            public int GrabDx, GrabDy;
            public List<double[]> Samples = new List<double[]>(); // t,x,y
        }

        readonly List<PetItem> items = new List<PetItem>();
        Timer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        double lastT;
        public bool PetsVisible { get; private set; }

        public void Start(List<PetDef> defs)
        {
            PetsVisible = true;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            for (int i = 0; i < defs.Count; i++)
            {
                PetDef def = defs[i];
                // 狗（或第二只起）主动跑向同伴
                PetBrain brain = new PetBrain(def, def.Id != "ragdoll" && i > 0 || def.Id == "golden");
                double frac = defs.Count == 1 ? 0.5 : 0.25 + 0.5 * i / (defs.Count - 1);
                brain.X = wa.Left + (wa.Width - def.SpriteW) * frac;
                brain.Y = wa.Bottom - def.SpriteH;

                PetWindow win = new PetWindow();
                PetItem item = new PetItem();
                item.Brain = brain;
                item.Window = win;
                WireMouse(item);
                win.Show();
                items.Add(item);
            }
            lastT = clock.Elapsed.TotalSeconds;
            timer = new Timer();
            timer.Interval = 33;
            timer.Tick += OnTick;
            timer.Start();
        }

        void WireMouse(PetItem item)
        {
            item.Window.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                Point cur = Cursor.Position;
                item.Dragging = true;
                item.Moved = false;
                item.DownPos = cur;
                item.DownTime = clock.Elapsed.TotalSeconds;
                item.GrabDx = cur.X - (int)item.Brain.X;
                item.GrabDy = cur.Y - (int)item.Brain.Y;
                item.Samples.Clear();
                item.Brain.BeginDrag();
            };
            item.Window.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (!item.Dragging) return;
                Point cur = Cursor.Position;
                if (Math.Abs(cur.X - item.DownPos.X) + Math.Abs(cur.Y - item.DownPos.Y) > 6)
                    item.Moved = true;
                item.Brain.DragTo(cur.X - item.GrabDx, cur.Y - item.GrabDy);
                double t = clock.Elapsed.TotalSeconds;
                item.Samples.Add(new double[] { t, cur.X, cur.Y });
                if (item.Samples.Count > 12) item.Samples.RemoveAt(0);
            };
            item.Window.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || !item.Dragging) return;
                item.Dragging = false;
                double now = clock.Elapsed.TotalSeconds;
                bool isClick = !item.Moved && (now - item.DownTime) < 0.4;
                double vxOut = 0, vyOut = 0;
                if (!isClick && item.Samples.Count >= 2)
                {
                    double[] last = item.Samples[item.Samples.Count - 1];
                    double[] first = null;
                    for (int i = item.Samples.Count - 2; i >= 0; i--)
                    {
                        first = item.Samples[i];
                        if (last[0] - first[0] > 0.1) break;
                    }
                    double span = last[0] - first[0];
                    if (span > 0.01)
                    {
                        vxOut = (last[1] - first[1]) / span;
                        vyOut = (last[2] - first[2]) / span;
                    }
                }
                item.Brain.EndDrag(vxOut, vyOut, isClick);
            };
        }

        void OnTick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(now - lastT, 0.1);   // 掉帧保护
            lastT = now;
            if (!PetsVisible) return;

            double idleSec = GetIdleSeconds();
            for (int i = 0; i < items.Count; i++)
            {
                EnvInfo env = new EnvInfo();
                env.Dt = dt;
                env.IdleSeconds = idleSec;
                env.Buddy = items.Count > 1 ? items[(i + 1) % items.Count].Brain : null;
                items[i].Brain.Tick(env);
            }
            for (int i = 0; i < items.Count; i++)
            {
                PetItem it = items[i];
                it.Window.Render(it.Brain.CurrentFrame(), (int)it.Brain.X, (int)it.Brain.Y);
            }
        }

        public static double GetIdleSeconds()
        {
            NativeMethods.LASTINPUTINFO lii = new NativeMethods.LASTINPUTINFO();
            lii.CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO));
            if (!NativeMethods.GetLastInputInfo(ref lii)) return 0;
            uint tick = (uint)Environment.TickCount;
            return (tick - lii.DwTime) / 1000.0;
        }

        public void ToggleVisible()
        {
            PetsVisible = !PetsVisible;
            foreach (PetItem it in items)
            {
                it.Window.Visible = PetsVisible;
                if (PetsVisible) it.Window.InvalidateCache();
            }
        }

        public void Dispose()
        {
            if (timer != null) { timer.Stop(); timer.Dispose(); }
            foreach (PetItem it in items) it.Window.Dispose();
            items.Clear();
        }
    }
}
