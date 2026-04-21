using System;
using System.Collections.Generic;

namespace Game.Framework.AI
{
    /// <summary>
    /// 极简泛型有限状态机。TState 通常为 enum。
    /// 用法：
    ///   var fsm = new StateMachine&lt;EnemyState&gt;(EnemyState.Patrol);
    ///   fsm.Configure(EnemyState.Patrol)
    ///      .OnEnter(() =&gt; ...)
    ///      .OnTick(dt =&gt; ...)
    ///      .OnExit(() =&gt; ...);
    ///   每帧：fsm.Tick(dt);
    ///   切换：fsm.ChangeState(EnemyState.Chase);
    /// </summary>
    public class StateMachine<TState>
    {
        private class Handler
        {
            public Action OnEnter;
            public Action<float> OnTick;
            public Action<float> OnFixedTick;
            public Action OnExit;
        }

        public class StateBuilder
        {
            internal Handler H;
            public StateBuilder OnEnter(Action a)              { H.OnEnter      = a; return this; }
            public StateBuilder OnTick(Action<float> a)        { H.OnTick       = a; return this; }
            public StateBuilder OnFixedTick(Action<float> a)   { H.OnFixedTick  = a; return this; }
            public StateBuilder OnExit(Action a)               { H.OnExit       = a; return this; }
        }

        private readonly Dictionary<TState, Handler> _handlers = new();
        public TState Current { get; private set; }
        public TState Previous { get; private set; }
        public float TimeInState { get; private set; }

        public event Action<TState, TState> OnTransition; // (from, to)

        public StateMachine(TState initial)
        {
            Current = initial;
            Previous = initial;
        }

        public StateBuilder Configure(TState state)
        {
            if (!_handlers.TryGetValue(state, out var h))
                _handlers[state] = h = new Handler();
            return new StateBuilder { H = h };
        }

        public void Start()
        {
            TimeInState = 0f;
            if (_handlers.TryGetValue(Current, out var h)) h.OnEnter?.Invoke();
        }

        public void ChangeState(TState next)
        {
            if (EqualityComparer<TState>.Default.Equals(Current, next)) return;
            if (_handlers.TryGetValue(Current, out var old)) old.OnExit?.Invoke();
            Previous = Current;
            Current = next;
            TimeInState = 0f;
            if (_handlers.TryGetValue(Current, out var nw)) nw.OnEnter?.Invoke();
            OnTransition?.Invoke(Previous, Current);
        }

        public void Tick(float dt)
        {
            TimeInState += dt;
            if (_handlers.TryGetValue(Current, out var h)) h.OnTick?.Invoke(dt);
        }

        public void FixedTick(float dt)
        {
            if (_handlers.TryGetValue(Current, out var h)) h.OnFixedTick?.Invoke(dt);
        }
    }
}
