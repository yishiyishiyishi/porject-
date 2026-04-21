using UnityEngine;

namespace Game.Framework.AI
{
    /// <summary>
    /// 轻量 NPC 游荡模块。Idle / Wander / Talk 三态。
    /// Talk 态由 DialogueTrigger 等外部调用 SetTalking(true/false) 切换；
    /// 不依赖 EnemySenses / Probe，也不做攻击。需要的话可和 DepthLock 一起挂。
    ///
    /// Order = 50。
    /// </summary>
    [RequireComponent(typeof(EnemyLocomotion))]
    public class NpcWanderer : ActorModule
    {
        public enum NpcState { Idle, Wander, Talk }

        [Header("Wander")]
        public float wanderRange = 2f;
        public float idleMinSeconds = 1f;
        public float idleMaxSeconds = 3f;
        public float wanderMinSeconds = 1.5f;
        public float wanderMaxSeconds = 4f;
        public float walkSpeed = 1.5f;

        [Header("Debug")]
        [SerializeField] private NpcState _debug;

        private StateMachine<NpcState> _fsm;
        private EnemyLocomotion _loco;
        private EnvironmentProbe _probe;

        private Vector2 _origin;
        private float _targetDir;
        private float _stateDuration;

        public override int Order => 50;

        public override void OnAttach(Actor actor)
        {
            base.OnAttach(actor);
            _loco = GetComponent<EnemyLocomotion>();
            _probe = GetComponent<EnvironmentProbe>();
            _origin = transform.position;

            _fsm = new StateMachine<NpcState>(NpcState.Idle);
            _fsm.Configure(NpcState.Idle)
                .OnEnter(() => { _stateDuration = Random.Range(idleMinSeconds, idleMaxSeconds); _loco.Stop(); });
            _fsm.Configure(NpcState.Wander)
                .OnEnter(() =>
                {
                    _stateDuration = Random.Range(wanderMinSeconds, wanderMaxSeconds);
                    // 朝向原点+随机偏移的方向
                    float drift = Random.Range(-wanderRange, wanderRange);
                    float dx = (_origin.x + drift) - transform.position.x;
                    _targetDir = Mathf.Abs(dx) < 0.05f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(dx);
                });
            _fsm.Configure(NpcState.Talk)
                .OnEnter(() => _loco.Stop());
            _fsm.Start();
        }

        public override void Tick(float dt)
        {
            _debug = _fsm.Current;
            if (_fsm.Current == NpcState.Talk) return;

            if (_fsm.TimeInState >= _stateDuration)
            {
                _fsm.ChangeState(_fsm.Current == NpcState.Idle ? NpcState.Wander : NpcState.Idle);
                return;
            }

            if (_fsm.Current == NpcState.Wander)
            {
                // 走出 origin ± wanderRange 就回头
                float fromOrigin = transform.position.x - _origin.x;
                if (Mathf.Abs(fromOrigin) > wanderRange && Mathf.Sign(fromOrigin) == _targetDir)
                    _targetDir = -_targetDir;

                // 前方不通也回头
                if (_probe != null)
                {
                    int facing = State.Direction == 0 ? 1 : State.Direction;
                    if (Mathf.Approximately(_targetDir, facing) && !_probe.CanAdvance)
                        _targetDir = -_targetDir;
                }

                _loco.Request(_targetDir, walkSpeed);
            }
        }

        /// <summary>由 DialogueTrigger / 交互系统调用。</summary>
        public void SetTalking(bool talking)
        {
            if (talking) _fsm.ChangeState(NpcState.Talk);
            else _fsm.ChangeState(NpcState.Idle);
        }
    }
}
