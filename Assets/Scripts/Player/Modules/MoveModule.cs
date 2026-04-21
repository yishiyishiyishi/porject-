using UnityEngine;
using Game.Framework;
using Game.Framework.View;

namespace Game.Player.Modules
{
    /// <summary>
    /// 玩家移动。
    ///   Side 模式：横版 —— 仅水平输入驱动 X，加/刹车分离 + 空中折损。
    ///   TopDown 模式：俯视 —— 水平 + 垂直输入同时驱动 XY，无空中折损。
    /// Gate.Move 锁定时完全让出（冲刺/硬直等强制位移期间）。
    /// </summary>
    public class MoveModule : ActorModule
    {
        public float moveSpeed = 12f;
        public float acceleration = 50f;
        public float brake = 100f;
        [Range(0f, 1f)] public float airAccelerationFactor = 0.2f;

        public override int Order => 10;

        private PlayerActor _player;

        public override void OnAttach(Actor actor)
        {
            base.OnAttach(actor);
            _player = actor as PlayerActor;
            if (_player == null) Debug.LogError($"[MoveModule] {name} 必须挂在 PlayerActor 上", this);
        }

        public override void FixedTick(float dt)
        {
            if (_player == null) return;
            if (Gate.IsBlocked(ActionTag.Move)) return;

            if (ViewModeController.Current == ViewMode.TopDown)
                TopDownTick(dt);
            else
                SideTick(dt);
        }

        private void SideTick(float dt)
        {
            float inp = _player.Input.Horizontal;
            Vector2 v = State.Velocity;

            float a = (inp * v.x > 0f) ? acceleration : brake;
            if (!State.IsGrounded) a *= airAccelerationFactor;

            v.x = v.x.MoveTo(inp * moveSpeed, a * dt);
            if (inp != 0f) Actor.SetDirection((int)Mathf.Sign(inp));
            State.Velocity = v;
        }

        private void TopDownTick(float dt)
        {
            float ix = _player.Input.Horizontal;
            float iy = _player.Input.Vertical;

            // 单位化 + 斜向不超速
            Vector2 target = new Vector2(ix, iy);
            if (target.sqrMagnitude > 1f) target.Normalize();
            target *= moveSpeed;

            Vector2 v = State.Velocity;
            float ax = (target.x * v.x > 0f) ? acceleration : brake;
            float ay = (target.y * v.y > 0f) ? acceleration : brake;

            v.x = v.x.MoveTo(target.x, ax * dt);
            v.y = v.y.MoveTo(target.y, ay * dt);

            if (Mathf.Abs(ix) > 0.01f) Actor.SetDirection((int)Mathf.Sign(ix));
            State.Velocity = v;
        }
    }
}
