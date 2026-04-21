using UnityEngine;

namespace Game.Framework.AI
{
    /// <summary>
    /// 敌人移动模块。统一使用 Rigidbody2D.linearVelocity.x，
    /// 带加速/刹车分离与 EnvironmentProbe 的墙/悬崖联动：
    /// 如果前方不可通行，自动把 input 视作 0 并立即停住。
    ///
    /// Order = 0。在 Brain(+100) 之前、在 Senses(-80) 和 Probe(-90) 之后。
    /// </summary>
    public class EnemyLocomotion : ActorModule
    {
        [Header("Speed")]
        public float walkSpeed = 2f;
        public float chaseSpeed = 4f;

        [Header("Acceleration")]
        public float acceleration = 30f;
        public float brake = 60f;

        [Header("Behavior")]
        [Tooltip("遇到墙或悬崖时自动停下（不转身；是否转身由状态机决定）。")]
        public bool respectEnvironment = true;

        private EnvironmentProbe _probe;
        private float _inputDir; // -1/0/1
        private float _targetSpeed;

        public override int Order => 0;

        public override void OnAttach(Actor actor)
        {
            base.OnAttach(actor);
            _probe = GetComponent<EnvironmentProbe>();
        }

        /// <summary>由 Brain 调用下达指令，指令保持到下一条 Request / Stop() 为止。
        /// dir 为 -1/0/1，speed 通常用 walkSpeed / chaseSpeed。</summary>
        public void Request(float dir, float speed)
        {
            _inputDir = Mathf.Sign(dir);
            if (Mathf.Abs(dir) < 0.01f) _inputDir = 0f;
            _targetSpeed = speed;
        }

        public void Stop() => Request(0f, 0f);

        public override void FixedTick(float dt)
        {
            // 指令在 Request/Stop 之间保持不变；不再按 Tick 频率自动清零，否则
            // 帧率抖动时（多次 FixedTick 打一次 Tick）敌人会抖脚。
            float effectiveDir = _inputDir;
            if (respectEnvironment && _probe != null && effectiveDir != 0f)
            {
                int facing = State.Direction == 0 ? 1 : State.Direction;
                // 请求的方向和当前朝向一致才检查前方探针，反方向无需检查
                if (Mathf.Approximately(Mathf.Sign(effectiveDir), facing) && !_probe.CanAdvance)
                    effectiveDir = 0f;
            }

            // 更新 Actor 面朝
            if (_inputDir != 0f) Actor.SetDirection((int)Mathf.Sign(_inputDir));

            Vector2 v = State.Velocity;
            float target = effectiveDir * _targetSpeed;
            float rate = (effectiveDir * v.x > 0f) ? acceleration : brake;
            v.x = MoveTo(v.x, target, rate * dt);
            State.Velocity = v;
        }

        private static float MoveTo(float from, float to, float step)
        {
            if (Mathf.Abs(from - to) <= step) return to;
            return to > from ? from + step : from - step;
        }
    }
}
