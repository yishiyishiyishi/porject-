using UnityEngine;
using Game.Framework;

namespace Game.Player.Modules
{
    /// <summary>
    /// 水平移动。加速/刹车分离 + 空中折损。
    /// 被 Gate.Move 锁时完全让出控制，用于冲刺等强制位移期间。
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

            float inp = _player.Input.Horizontal;
            Vector2 v = State.Velocity;

            float a = (inp * v.x > 0f) ? acceleration : brake;
            if (!State.IsGrounded) a *= airAccelerationFactor;

            v.x = v.x.MoveTo(inp * moveSpeed, a * dt);
            if (inp != 0f) Actor.SetDirection((int)Mathf.Sign(inp));
            State.Velocity = v;
        }
    }
}
