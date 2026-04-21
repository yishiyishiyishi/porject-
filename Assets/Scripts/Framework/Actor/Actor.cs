using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Framework
{
    /// <summary>
    /// Actor 基类。自研框架的核心调度器。
    ///
    /// 职责：
    ///   1. 持有 ActorState / ActionGate 两块中心数据。
    ///   2. 收集挂在同对象上的所有 IActorModule，按 Order 排序。
    ///   3. 统一驱动模块的 Tick / FixedTick（以 LocalTimeScale 缩放后的 dt）。
    ///   4. 广播 Actor 级事件到 EventBus，方便 meta/叙事层接入。
    ///
    /// 不负责：动画、具体玩法、输入 —— 那些交给模块。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Actor : MonoBehaviour
    {
        public ActorState State { get; } = new ActorState();
        public ActionGate Gate { get; } = new ActionGate();

        [Tooltip("翻转 localScale.x 来表现朝向。关闭则由动画或其他系统自行处理。")]
        public bool flipSpriteByScale = true;

        [Tooltip("Actor ID，用于事件广播中识别身份。留空则使用 GameObject 名。")]
        public string actorId;

        private readonly List<IActorModule> _modules = new List<IActorModule>();

        public string Id => string.IsNullOrEmpty(actorId) ? name : actorId;

        /// <summary>
        /// 暂停时跳过 Tick/FixedTick 分发，但保留物理。
        /// 用于对话、过场动画等需要"冻结角色自主行为"的场合。
        /// 静止：把 Rigidbody2D.linearVelocity 由调用方清零。
        /// </summary>
        public bool IsPaused { get; set; }

        protected virtual void Awake()
        {
            State.Rb = GetComponent<Rigidbody2D>();

            var raw = GetComponents<IActorModule>();
            _modules.Clear();
            _modules.AddRange(raw);
            _modules.Sort((a, b) => a.Order.CompareTo(b.Order));

            for (int i = 0; i < _modules.Count; i++)
                _modules[i].OnAttach(this);

            EventBus.Publish(new ActorSpawned(Id));
        }

        protected virtual void OnDestroy()
        {
            EventBus.Publish(new ActorDespawned(Id));
        }

        protected virtual void Update()
        {
            if (IsPaused) return;
            float dt = Time.deltaTime * State.LocalTimeScale;
            Gate.Tick(dt);
            for (int i = 0; i < _modules.Count; i++)
                _modules[i].Tick(dt);
        }

        protected virtual void FixedUpdate()
        {
            if (IsPaused) return;
            float dt = Time.fixedDeltaTime * State.LocalTimeScale;
            for (int i = 0; i < _modules.Count; i++)
                _modules[i].FixedTick(dt);
        }

        public void SetDirection(int dir)
        {
            if (dir == 0 || dir == State.Direction) return;
            State.Direction = dir > 0 ? 1 : -1;
            if (flipSpriteByScale)
            {
                var s = transform.localScale;
                s.x = Mathf.Abs(s.x) * State.Direction;
                transform.localScale = s;
            }
        }

        public T GetModule<T>() where T : class, IActorModule
        {
            for (int i = 0; i < _modules.Count; i++)
                if (_modules[i] is T t) return t;
            return null;
        }
    }

    // === Actor 级事件（EventBus payload）===
    public readonly struct ActorSpawned { public readonly string Id; public ActorSpawned(string id) { Id = id; } }
    public readonly struct ActorDespawned { public readonly string Id; public ActorDespawned(string id) { Id = id; } }
}
