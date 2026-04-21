using UnityEngine;

namespace Game.Framework.AI
{
    /// <summary>
    /// 敌人大脑。基于 StateMachine&lt;EnemyState&gt; 驱动 Patrol / Chase / Attack / Stunned / Dead。
    /// 依赖同对象上的：EnemyLocomotion, EnemySenses, EnvironmentProbe, Health（后两个可选）。
    /// Order = 100：在所有感知/移动模块之后决策，保证读到当帧最新状态。
    /// </summary>
    [RequireComponent(typeof(EnemyLocomotion))]
    [RequireComponent(typeof(EnemySenses))]
    public class EnemyBrain : ActorModule
    {
        public enum EnemyState { Patrol, Chase, Attack, Stunned, Dead }

        [Header("Patrol")]
        [Tooltip("起点两侧巡逻的半径。")]
        public float patrolRange = 3f;
        [Tooltip("到达巡逻点后停顿时间（秒）。")]
        public float patrolPauseSeconds = 1.0f;

        [Header("Attack")]
        public float attackWindup = 0.3f;
        public float attackActive = 0.15f;
        public float attackRecovery = 0.6f;
        public float attackDamage = 1f;
        public float attackHitboxRadius = 0.9f;
        [Tooltip("攻击判定中心相对于 Actor 的偏移（X 为面朝方向）。")]
        public Vector2 attackHitboxOffset = new Vector2(0.8f, 0.3f);
        public LayerMask attackableLayer = ~0;
        public Vector2 attackKnockback = new Vector2(4f, 2f);

        [Header("Stun")]
        public float defaultStunSeconds = 0.4f;

        [Header("Debug")]
        [SerializeField, Tooltip("只读：当前 FSM 状态。")]
        private EnemyState _debugState;

        public StateMachine<EnemyState> FSM { get; private set; }
        public EnemyState CurrentState => FSM != null ? FSM.Current : EnemyState.Patrol;

        private EnemyLocomotion _loco;
        private EnemySenses _senses;
        private EnvironmentProbe _probe;
        private Health _health;

        private Vector2 _startPos;
        private float _patrolTargetX;
        private float _patrolWaitTimer;

        public override int Order => 100;

        public override void OnAttach(Actor actor)
        {
            base.OnAttach(actor);
            _loco   = GetComponent<EnemyLocomotion>();
            _senses = GetComponent<EnemySenses>();
            _probe  = GetComponent<EnvironmentProbe>();
            _health = GetComponent<Health>();

            _startPos = transform.position;
            PickNewPatrolTarget();

            BuildFSM();

            if (_health != null)
            {
                _health.OnDamaged += OnDamaged;
                _health.OnDied    += OnDied;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDamaged -= OnDamaged;
                _health.OnDied    -= OnDied;
            }
        }

        // ---------- FSM 定义 ----------

        private void BuildFSM()
        {
            FSM = new StateMachine<EnemyState>(EnemyState.Patrol);

            FSM.Configure(EnemyState.Patrol)
                .OnTick(dt => TickPatrol(dt));

            FSM.Configure(EnemyState.Chase)
                .OnEnter(() => { /* 可接播警觉音效 / 动画 */ })
                .OnTick(_ => TickChase());

            FSM.Configure(EnemyState.Attack)
                .OnEnter(() =>
                {
                    _loco.Stop();
                    _attackFired = false; // 每次进入攻击都要重置，否则硬直打断后再攻击 active 帧会被跳过
                    // 攻击开始朝向玩家
                    if (_senses.Target != null)
                        Actor.SetDirection((int)Mathf.Sign(_senses.Target.position.x - transform.position.x));
                })
                .OnTick(dt => TickAttack(dt));

            FSM.Configure(EnemyState.Stunned)
                .OnEnter(() => _loco.Stop())
                .OnTick(_ => { if (FSM.TimeInState >= _stunSeconds) FSM.ChangeState(EnemyState.Patrol); });

            FSM.Configure(EnemyState.Dead)
                .OnEnter(() =>
                {
                    _loco.Stop();
                    State.Rb.simulated = false;
                    // 这里可以触发掉落、淡出、销毁等
                });

            FSM.Start();
        }

        // ---------- 每帧驱动 ----------

        public override void Tick(float dt)
        {
            _debugState = FSM.Current;

            if (FSM.Current == EnemyState.Dead) return;

            // 全局切换条件（高优先级：死亡/硬直由事件驱动，这里不重复；其余按感知决定）
            if (FSM.Current != EnemyState.Stunned && FSM.Current != EnemyState.Attack)
            {
                if (_senses.InAttackRange)      FSM.ChangeState(EnemyState.Attack);
                else if (_senses.InDetection)   FSM.ChangeState(EnemyState.Chase);
                else if (FSM.Current != EnemyState.Patrol) FSM.ChangeState(EnemyState.Patrol);
            }

            FSM.Tick(dt);
        }

        public override void FixedTick(float dt)
        {
            FSM.FixedTick(dt);
        }

        // ---------- 状态逻辑 ----------

        private void TickPatrol(float dt)
        {
            if (_patrolWaitTimer > 0f)
            {
                _patrolWaitTimer -= dt; // 用 Actor 的缩放 dt，而不是 Time.deltaTime，保持 LocalTimeScale 生效
                _loco.Stop();
                return;
            }

            float dx = _patrolTargetX - transform.position.x;
            if (Mathf.Abs(dx) < 0.1f)
            {
                _patrolWaitTimer = patrolPauseSeconds;
                PickNewPatrolTarget();
                _loco.Stop();
                return;
            }

            float dir = Mathf.Sign(dx);
            // 如果前方不通，提前放弃此目标
            if (_probe != null)
            {
                int facing = State.Direction == 0 ? 1 : State.Direction;
                if (Mathf.Approximately(dir, facing) && !_probe.CanAdvance)
                {
                    _patrolTargetX = transform.position.x; // 原地折返
                    _patrolWaitTimer = patrolPauseSeconds;
                    _loco.Stop();
                    return;
                }
            }
            _loco.Request(dir, _loco.walkSpeed);
        }

        private void TickChase()
        {
            if (_senses.Target == null) { _loco.Stop(); return; }
            float dx = _senses.Target.position.x - transform.position.x;
            float dir = Mathf.Abs(dx) < 0.05f ? 0f : Mathf.Sign(dx);
            _loco.Request(dir, _loco.chaseSpeed);
        }

        private void TickAttack(float dt)
        {
            float t = FSM.TimeInState;

            if (t < attackWindup)
            {
                // 蓄力（无判定，可播动画）
            }
            else if (t < attackWindup + attackActive)
            {
                // 判定窗口：开一次（由 _attackFired 标记）
                if (!_attackFired)
                {
                    PerformAttackHit();
                    _attackFired = true;
                }
            }
            else if (t < attackWindup + attackActive + attackRecovery)
            {
                // 收招
            }
            else
            {
                // 攻击结束：根据当前感知决定下一步
                if (_senses.InAttackRange) FSM.ReenterState(); // 连招：强制重入 Attack，否则 ChangeState(Attack) 会因 equal 检查被忽略
                else if (_senses.InDetection) FSM.ChangeState(EnemyState.Chase);
                else FSM.ChangeState(EnemyState.Patrol);
            }
        }

        private bool _attackFired;

        private void PerformAttackHit()
        {
            int facing = State.Direction == 0 ? 1 : State.Direction;
            Vector2 center = (Vector2)transform.position
                           + new Vector2(attackHitboxOffset.x * facing, attackHitboxOffset.y);

            var hits = Physics2D.OverlapCircleAll(center, attackHitboxRadius, attackableLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                var dmg = hits[i].GetComponent<IDamageable>() ?? hits[i].GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.IsAlive && hits[i].gameObject != gameObject)
                {
                    dmg.TakeDamage(new DamageInfo
                    {
                        amount = attackDamage,
                        hitPoint = center,
                        knockback = new Vector2(attackKnockback.x * facing, attackKnockback.y),
                        source = gameObject,
                        stunDuration = 0.2f,
                    });
                }
            }
        }

        // ---------- Health 事件 ----------

        private float _stunSeconds;

        private void OnDamaged(DamageInfo info)
        {
            if (FSM.Current == EnemyState.Dead) return;
            _stunSeconds = info.stunDuration > 0f ? info.stunDuration : defaultStunSeconds;
            FSM.ChangeState(EnemyState.Stunned);
        }

        private void OnDied()
        {
            FSM.ChangeState(EnemyState.Dead);
        }

        // ---------- Utility ----------

        private void PickNewPatrolTarget()
        {
            _patrolTargetX = _startPos.x + Random.Range(-patrolRange, patrolRange);
        }

        private void OnDrawGizmosSelected()
        {
            // 巡逻范围
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Vector3 origin = Application.isPlaying ? (Vector3)_startPos : transform.position;
            Gizmos.DrawLine(origin + Vector3.left * patrolRange, origin + Vector3.right * patrolRange);

            // 攻击判定框
            int facing = 1;
            if (Application.isPlaying && State != null) facing = State.Direction == 0 ? 1 : State.Direction;
            Vector3 atk = transform.position + new Vector3(attackHitboxOffset.x * facing, attackHitboxOffset.y, 0f);
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.7f);
            Gizmos.DrawWireSphere(atk, attackHitboxRadius);
        }
    }
}
