using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeskPets
{
    public enum PetState { Idle, Walk, Run, Sit, Sleep, Drag, Fall, Happy }

    /// <summary>每帧环境信息。</summary>
    public class EnvInfo
    {
        public double Dt;            // 秒
        public double IdleSeconds;   // 用户键鼠空闲秒数（只测活跃度，不涉内容）
        public PetBrain Buddy;       // 同屏另一只（可为 null）
    }

    /// <summary>
    /// 行为状态机：游走/奔跑/坐卧/睡觉/拖拽/抛落/开心。
    /// 用户活跃 → 宠物精神；用户离开 → 宠物打盹。
    /// </summary>
    public class PetBrain
    {
        public PetDef Def;
        public double X, Y;          // 窗口左上角（物理像素）
        public int Facing = 1;       // 1 右 -1 左
        public PetState State = PetState.Idle;
        public bool Playful;         // true = 会主动跑向同伴（狗）

        double stateTime;
        double stateDur;
        double animTime;
        double vx, vy;
        bool chasing;
        readonly Random rng;

        const double WalkSpeed = 42;
        const double RunSpeed = 150;
        const double Gravity = 2500;

        public PetBrain(PetDef def, bool playful)
        {
            Def = def;
            Playful = playful;
            rng = new Random(Guid.NewGuid().GetHashCode());
            Enter(PetState.Idle);
        }

        public double CenterX { get { return X + Def.SpriteW / 2.0; } }

        Rectangle WorkArea()
        {
            Point c = new Point((int)CenterX, (int)(Y + Def.SpriteH / 2.0));
            return Screen.FromPoint(c).WorkingArea;
        }

        double GroundTop() { return WorkArea().Bottom - Def.SpriteH; }

        void Enter(PetState s)
        {
            State = s;
            stateTime = 0;
            animTime = 0;
            switch (s)
            {
                case PetState.Idle: stateDur = 2 + rng.NextDouble() * 3; break;
                case PetState.Walk: stateDur = 3 + rng.NextDouble() * 4; break;
                case PetState.Run: stateDur = 1.5 + rng.NextDouble() * 1.5; break;
                case PetState.Sit: stateDur = 4 + rng.NextDouble() * 5; break;
                case PetState.Sleep: stateDur = 15 + rng.NextDouble() * 25; break;
                case PetState.Happy: stateDur = 1.4; break;
                default: stateDur = double.MaxValue; break;
            }
            if (s == PetState.Walk) { vx = WalkSpeed * PickDir(); Facing = vx >= 0 ? 1 : -1; }
            else if (s == PetState.Run && !chasing) { vx = RunSpeed * PickDir(); Facing = vx >= 0 ? 1 : -1; }
            else if (s != PetState.Fall) { vx = 0; }
        }

        int PickDir()
        {
            Rectangle wa = WorkArea();
            // 靠边时往回走，避免贴边打转
            if (X < wa.Left + 60) return 1;
            if (X + Def.SpriteW > wa.Right - 60) return -1;
            return rng.Next(2) == 0 ? 1 : -1;
        }

        public void Tick(EnvInfo env)
        {
            double dt = env.Dt;
            stateTime += dt;
            animTime += dt;

            // 悬空自检：非拖拽/下落状态却离地 → 掉下来
            if (State != PetState.Drag && State != PetState.Fall && Y < GroundTop() - 2)
            {
                vy = 0;
                Enter(PetState.Fall);
            }

            switch (State)
            {
                case PetState.Walk:
                case PetState.Run:
                    MoveHorizontal(dt, env);
                    break;
                case PetState.Fall:
                    StepFall(dt);
                    break;
                case PetState.Sleep:
                    // 用户回来后不久醒来
                    if (env.IdleSeconds < 3 && stateTime > 8 && rng.NextDouble() < dt * 0.8)
                        Enter(PetState.Sit);
                    break;
                case PetState.Drag:
                    return; // 位置由拖拽驱动
            }

            if (stateTime >= stateDur && State != PetState.Fall)
                PickNext(env);
        }

        void MoveHorizontal(double dt, EnvInfo env)
        {
            Rectangle wa = WorkArea();
            if (chasing && env.Buddy != null)
            {
                double target = env.Buddy.CenterX;
                double dir = target > CenterX ? 1 : -1;
                vx = RunSpeed * dir;
                Facing = dir >= 0 ? 1 : -1;
                if (Math.Abs(target - CenterX) < 130 || stateTime > 6)
                {
                    chasing = false;
                    Enter(PetState.Happy);
                    // 同伴被追上：清醒时一半概率跟着开心一下
                    if (Math.Abs(target - CenterX) < 160 &&
                        (env.Buddy.State == PetState.Idle || env.Buddy.State == PetState.Walk ||
                         env.Buddy.State == PetState.Sit) && rng.NextDouble() < 0.5)
                        env.Buddy.Enter(PetState.Happy);
                    return;
                }
            }
            X += vx * dt;
            if (X < wa.Left) { X = wa.Left; vx = Math.Abs(vx); Facing = 1; }
            else if (X + Def.SpriteW > wa.Right) { X = wa.Right - Def.SpriteW; vx = -Math.Abs(vx); Facing = -1; }
            Y = GroundTop();
        }

        void StepFall(double dt)
        {
            Rectangle wa = WorkArea();
            vy += Gravity * dt;
            X += vx * dt;
            Y += vy * dt;
            if (X < wa.Left) { X = wa.Left; vx = -vx * 0.5; }
            else if (X + Def.SpriteW > wa.Right) { X = wa.Right - Def.SpriteW; vx = -vx * 0.5; }
            if (vx != 0) Facing = vx >= 0 ? 1 : -1;
            double ground = GroundTop();
            if (Y >= ground)
            {
                Y = ground;
                if (Math.Abs(vy) > 450)
                {
                    vy = -vy * 0.35;   // 小弹跳
                    vx *= 0.6;
                }
                else
                {
                    vy = 0;
                    vx = 0;
                    Enter(PetState.Idle);
                }
            }
        }

        void PickNext(EnvInfo env)
        {
            double idle = env.IdleSeconds;
            // 追同伴：精神时段、同伴离得远，小概率发起
            if (Playful && env.Buddy != null && idle < 120 &&
                Math.Abs(env.Buddy.CenterX - CenterX) > 350 && rng.NextDouble() < 0.22)
            {
                chasing = true;
                Enter(PetState.Run);
                stateDur = 6;
                return;
            }

            double r = rng.NextDouble();
            if (idle < 120)          // 她在干活：宠物也精神
            {
                if (r < 0.25) Enter(PetState.Idle);
                else if (r < 0.60) Enter(PetState.Walk);
                else if (r < 0.72) Enter(PetState.Run);
                else if (r < 0.92) Enter(PetState.Sit);
                else Enter(PetState.Sleep);
            }
            else if (idle < 420)     // 小歇
            {
                if (r < 0.25) Enter(PetState.Idle);
                else if (r < 0.40) Enter(PetState.Walk);
                else if (r < 0.75) Enter(PetState.Sit);
                else Enter(PetState.Sleep);
            }
            else                     // 人不在：睡大觉
            {
                if (r < 0.80) { Enter(PetState.Sleep); stateDur = 120 + rng.NextDouble() * 480; }
                else if (r < 0.92) Enter(PetState.Sit);
                else Enter(PetState.Idle);
            }
        }

        // ------------------------------------------------ 拖拽接口
        public void BeginDrag()
        {
            chasing = false;
            Enter(PetState.Drag);
        }

        public void DragTo(double x, double y)
        {
            X = x;
            Y = y;
        }

        public void EndDrag(double throwVx, double throwVy, bool isClick)
        {
            if (isClick)
            {
                Enter(PetState.Happy);  // 摸摸头；悬空会自动掉下来
                return;
            }
            vx = Math.Max(-900, Math.Min(900, throwVx));
            vy = Math.Max(-1500, Math.Min(1500, throwVy));
            Enter(PetState.Fall);
        }

        // ------------------------------------------------ 渲染
        string MapState()
        {
            switch (State)
            {
                case PetState.Walk: return "walk";
                case PetState.Run: return "run";
                case PetState.Sit: return "sit";
                case PetState.Sleep: return "sleep";
                case PetState.Drag: return "drag";
                case PetState.Fall: return "fall";
                case PetState.Happy: return "happy";
                default: return "idle";
            }
        }

        public Bitmap CurrentFrame()
        {
            StateAnim anim = Def.States[MapState()];
            int n = anim.Right.Length;
            int idx;
            if (anim.Loop) idx = (int)(animTime * anim.Fps) % n;
            else idx = Math.Min((int)(animTime * anim.Fps), n - 1);
            return Facing >= 0 ? anim.Right[idx] : anim.Left[idx];
        }
    }
}
