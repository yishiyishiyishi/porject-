using UnityEngine;

namespace Game.Framework.Sensors
{
    /// <summary>
    /// 接地检测模块。Order=-100，保证在所有逻辑模块之前刷新 State.IsGrounded，
    /// 后续模块直接读状态，不再各自做 OverlapCircle。
    /// </summary>
    public class GroundSensor : ActorModule
    {
        public Transform probe;
        public float radius = 0.15f;
        public LayerMask groundLayer = ~0;

        public override int Order => -100;

        public override void FixedTick(float dt)
        {
            if (probe == null)
            {
                State.IsGrounded = true; // 兜底：未配置时视为在地，方便调试
                return;
            }
            State.IsGrounded = Physics2D.OverlapCircle(probe.position, radius, groundLayer) != null;
        }

        private void OnDrawGizmosSelected()
        {
            if (probe == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(probe.position, radius);
        }
    }
}
