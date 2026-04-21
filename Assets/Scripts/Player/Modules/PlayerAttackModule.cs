using System.Collections.Generic;
using UnityEngine;
using Game.Framework;
using Game.Framework.AI;
using Game.Framework.Combat;
using Game.Framework.Time;

namespace Game.Player.Modules
{
    /// <summary>
    /// 玩家攻击模块。三段结构：windup → active → recovery。
    ///   - windup 末尾前按攻击键可以"接招"：recovery 末尾自动连到下一段（最多 combo 配置数量）
    ///   - active 帧按固定节奏做 HitboxQuery.OverlapBox，同次挥击不重复命中
    ///   - 命中时通过 HitStop.Pulse 自动做顿帧 + 触发相机轻震
    /// 尊重 ActionGate.Attack 锁（受击硬直 / 冲刺期间不响应）。
    /// </summary>
    public class PlayerAttackModule : ActorModule
    {
        [System.Serializable]
        public class ComboStep
        {
            public float windup   = 0.08f;
            public float active   = 0.12f;
            public float recovery = 0.25f;
            public float damage   = 2f;
            [Tooltip("Hitbox 相对 Actor 的偏移（X 随朝向镜像）。")]
            public Vector2 offset = new Vector2(1.0f, 0.2f);
            public Vector2 size   = new Vector2(1.6f, 1.2f);
            public Vector2 knockback = new Vector2(6f, 3f);
            [Tooltip("命中顿帧时长（秒，unscaled）。0 表示该段不顿帧。")]
            public float hitStop = 0.05f;
            [Tooltip("命中时相机轻震强度。0 表示不震。")]
            public float cameraShake = 0.3f;
        }

        [Header("Combo")]
        public List<ComboStep> steps = new List<ComboStep>
        {
            new ComboStep(),
            new ComboStep { damage = 2.5f, knockback = new Vector2(7f, 3f), hitStop = 0.06f },
            new ComboStep { damage = 4f,   knockback = new Vector2(9f, 5f), hitStop = 0.1f, cameraShake = 0.6f, windup = 0.14f, active = 0.18f, recovery = 0.4f },
        };

        [Header("Buffer")]
        [Tooltip("玩家按下攻击键后在多少秒内被视为 combo 预输入。")]
        public float attackBufferTime = 0.25f;

        [Header("Hit Detection")]
        public LayerMask hurtboxLayer = ~0;
        public Faction faction = Faction.Player;

        public override int Order => 40;

        private PlayerActor _player;
        private int _currentStep = -1;     // -1 = idle
        private float _phaseStart;         // TimeInState 起点（用缩放 dt 累计）
        private float _t;                  // 当前段已进行时间（缩放 dt）
        private bool _comboQueued;
        private readonly HashSet<Hurtbox> _hitThisSwing = new HashSet<Hurtbox>();

        private enum Phase { Idle, Windup, Active, Recovery }
        private Phase _phase = Phase.Idle;

        public override void OnAttach(Actor actor)
        {
            base.OnAttach(actor);
            _player = actor as PlayerActor;
            if (_player == null) Debug.LogError($"[PlayerAttackModule] 必须挂在 PlayerActor 上。", this);
        }

        public override void Tick(float dt)
        {
            if (_player == null) return;

            bool buffered = UnityEngine.Time.time - _player.Input.AttackPressedAt <= attackBufferTime;

            if (_phase == Phase.Idle)
            {
                if (buffered && !Gate.IsBlocked(ActionTag.Attack))
                {
                    _player.Input.ConsumeAttack();
                    StartStep(0);
                }
                return;
            }

            _t += dt;
            var step = steps[_currentStep];

            // 在 active / recovery 期间按攻击 = 排队下一段
            if (buffered && (_phase == Phase.Active || _phase == Phase.Recovery))
            {
                _player.Input.ConsumeAttack();
                _comboQueued = true;
            }

            switch (_phase)
            {
                case Phase.Windup:
                    if (_t >= step.windup)
                    {
                        _phase = Phase.Active;
                        _t = 0f;
                        _hitThisSwing.Clear();
                        PerformHit(step); // 第一帧 active 就判，手感更脆
                    }
                    break;

                case Phase.Active:
                    // active 段每帧重判一次，支持粗短挥击也能命中快速移过的目标
                    PerformHit(step);
                    if (_t >= step.active)
                    {
                        _phase = Phase.Recovery;
                        _t = 0f;
                    }
                    break;

                case Phase.Recovery:
                    if (_t >= step.recovery)
                    {
                        if (_comboQueued && _currentStep + 1 < steps.Count)
                        {
                            StartStep(_currentStep + 1);
                        }
                        else
                        {
                            EndCombo();
                        }
                    }
                    break;
            }
        }

        private void StartStep(int idx)
        {
            _currentStep = idx;
            _phase = Phase.Windup;
            _t = 0f;
            _comboQueued = false;
            _hitThisSwing.Clear();
            // 出招中锁移动 / 冲刺；recovery 末尾由 EndCombo/连招自然释放
            Gate.Block(ActionTag.Move, steps[idx].windup + steps[idx].active);
        }

        private void EndCombo()
        {
            _phase = Phase.Idle;
            _currentStep = -1;
            _comboQueued = false;
            _hitThisSwing.Clear();
        }

        private void PerformHit(ComboStep step)
        {
            int facing = State.Direction == 0 ? 1 : State.Direction;
            Vector2 center = (Vector2)transform.position + new Vector2(step.offset.x * facing, step.offset.y);

            var template = new DamageInfo
            {
                amount = step.damage,
                knockback = new Vector2(step.knockback.x * facing, step.knockback.y),
                stunDuration = 0.2f,
            };

            int hits = HitboxQuery.OverlapBox(
                center, step.size, 0f, hurtboxLayer,
                faction, template, gameObject, _hitThisSwing);

            if (hits > 0)
            {
                if (step.hitStop > 0f) HitStop.Pulse(step.hitStop);
                if (step.cameraShake > 0f)
                {
                    var cm = Framework.Cameras.CameraManager.Instance;
                    if (cm != null) cm.ShakeQuick(step.cameraShake);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (steps == null) return;
            int facing = 1;
            if (Application.isPlaying && State != null) facing = State.Direction == 0 ? 1 : State.Direction;
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                Vector3 c = transform.position + new Vector3(s.offset.x * facing, s.offset.y, 0f);
                Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.25f + 0.15f * i);
                Gizmos.DrawWireCube(c, new Vector3(s.size.x, s.size.y, 0.1f));
            }
        }
    }
}
