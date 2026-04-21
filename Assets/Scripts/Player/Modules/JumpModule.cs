using UnityEngine;
using Game.Framework;
using Game.Framework.View;

namespace Game.Player.Modules
{
    /// <summary>
    /// 跳跃 + 分段重力（上升 / 浮空 / 下落 三套常数）+ Coyote + Jump Buffer + 可变跳高。
    /// 驱动 Y 轴速度，因此 Rigidbody2D.gravityScale 应设为 0。
    /// </summary>
    public class JumpModule : ActorModule
    {
        [Header("Jump")]
        public float jumpSpeed = 20f;
        public float jumpCutSpeed = 6f;

        [Header("Gravity 分段")]
        public float gravityUp = 60f;
        public float gravityFloat = 40f;
        public float gravityFall = 70f;
        public float floatThreshold = 2f;
        public float maxFallingSpeed = 20f;

        [Header("Feel")]
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.2f;

        public override int Order => 20;

        private PlayerActor _player;
        private float _coyote;

        public override void OnAttach(Actor actor)
        {
            base.OnAttach(actor);
            _player = actor as PlayerActor;
            if (_player == null) Debug.LogError($"[JumpModule] {name} 必须挂在 PlayerActor 上", this);
        }

        public override void Tick(float dt)
        {
            _coyote = State.IsGrounded ? coyoteTime : Mathf.Max(0f, _coyote - dt);
        }

        public override void FixedTick(float dt)
        {
            if (_player == null) return;
            // 俯视模式：跳跃 + 自定义重力全部让位；MoveModule 接管 Y 轴
            if (ViewModeController.Current == ViewMode.TopDown) return;

            Vector2 v = State.Velocity;

            // 起跳
            bool buffered = Time.time - _player.Input.JumpPressedAt <= jumpBufferTime;
            if (buffered && _coyote > 0f && !Gate.IsBlocked(ActionTag.Jump))
            {
                v.y = jumpSpeed;
                _coyote = 0f;
                _player.Input.ConsumeJump();
                Gate.Block(ActionTag.Jump, 0.1f); // 防止单帧双触发
            }
            else if (!_player.Input.JumpHeld && v.y > jumpCutSpeed)
            {
                v.y = jumpCutSpeed;
            }

            // 分段重力
            float g;
            if (v.y > floatThreshold) g = gravityUp;
            else if (v.y > -floatThreshold) g = gravityFloat;
            else g = gravityFall;

            v.y -= g * dt;
            if (v.y < -maxFallingSpeed) v.y = -maxFallingSpeed;

            State.Velocity = v;
        }
    }
}
