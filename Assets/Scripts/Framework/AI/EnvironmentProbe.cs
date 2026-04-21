using UnityEngine;

namespace Game.Framework.AI
{
    /// <summary>
    /// 2D 动作游戏地面敌人专用的环境探测。每 FixedTick 做两条射线：
    ///   - 前方墙壁射线：脚踝高度水平探测，判定能不能继续前进；
    ///   - 前方悬崖射线：前方一小段 + 向下，判定再走半步会不会掉下去。
    /// 面朝方向 由 Actor.State.Direction 决定。
    ///
    /// Order = -90：比 GroundSensor(-100) 晚一点，但早于所有逻辑模块。
    /// </summary>
    public class EnvironmentProbe : ActorModule
    {
        [Header("Wall Probe (水平撞墙)")]
        [Tooltip("从 Actor 原点沿朝向发射的水平射线距离。")]
        public float wallProbeDistance = 0.4f;
        [Tooltip("射线起点相对于 Actor 原点的偏移。Y 一般是脚踝高度略上一点。")]
        public Vector2 wallProbeOrigin = new Vector2(0f, 0.2f);
        public LayerMask wallLayer = ~0;

        [Header("Ledge Probe (悬崖)")]
        [Tooltip("从 Actor 脚前发射的向下射线距离。")]
        public float ledgeProbeDistance = 1.0f;
        [Tooltip("射线起点相对于 Actor 原点的偏移。X 为面朝方向上的领先距离。")]
        public Vector2 ledgeProbeOriginOffset = new Vector2(0.5f, 0f);
        public LayerMask groundLayer = ~0;

        [Header("Debug")]
        public bool drawGizmos = true;

        public bool HasWallAhead { get; private set; }
        public bool HasGroundAhead { get; private set; }

        /// <summary>综合判定：能不能继续朝当前朝向走下去。</summary>
        public bool CanAdvance => !HasWallAhead && HasGroundAhead;

        public override int Order => -90;

        // 复用缓冲区，避免 Raycast 的 GC。4 个足够过滤自己 + 前方障碍。
        private readonly RaycastHit2D[] _hitBuf = new RaycastHit2D[4];

        public override void FixedTick(float dt)
        {
            int dir = State.Direction == 0 ? 1 : State.Direction;

            // Wall
            Vector2 wallOrigin = (Vector2)transform.position
                               + new Vector2(wallProbeOrigin.x * dir, wallProbeOrigin.y);
            HasWallAhead = FirstHitExcludingSelf(wallOrigin, Vector2.right * dir, wallProbeDistance, wallLayer);

            // Ledge
            Vector2 ledgeOrigin = (Vector2)transform.position
                                + new Vector2(ledgeProbeOriginOffset.x * dir, ledgeProbeOriginOffset.y);
            HasGroundAhead = FirstHitExcludingSelf(ledgeOrigin, Vector2.down, ledgeProbeDistance, groundLayer);
        }

        /// <summary>排除敌人自己身上的 Collider（queriesStartInColliders=true 时常见的自击问题）。</summary>
        private bool FirstHitExcludingSelf(Vector2 origin, Vector2 dir, float dist, LayerMask mask)
        {
            int n = Physics2D.RaycastNonAlloc(origin, dir, _hitBuf, dist, mask);
            for (int i = 0; i < n; i++)
            {
                var c = _hitBuf[i].collider;
                if (c == null) continue;
                if (State != null && State.Rb != null && _hitBuf[i].rigidbody == State.Rb) continue;
                return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            int dir = 1;
            if (Application.isPlaying && State != null) dir = State.Direction == 0 ? 1 : State.Direction;

            Vector3 wallOrigin = transform.position
                               + new Vector3(wallProbeOrigin.x * dir, wallProbeOrigin.y, 0f);
            Gizmos.color = HasWallAhead ? Color.red : Color.yellow;
            Gizmos.DrawLine(wallOrigin, wallOrigin + new Vector3(wallProbeDistance * dir, 0f, 0f));

            Vector3 ledgeOrigin = transform.position
                                + new Vector3(ledgeProbeOriginOffset.x * dir, ledgeProbeOriginOffset.y, 0f);
            Gizmos.color = HasGroundAhead ? Color.green : Color.red;
            Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + new Vector3(0f, -ledgeProbeDistance, 0f));
        }
    }
}
