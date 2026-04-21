using UnityEngine;

namespace Game.Framework
{
    /// <summary>
    /// Actor 的运行时中心化状态。所有模块读写同一份数据，避免各模块各自查询刚体或做重复物理检测。
    /// 纯 POCO，无 MonoBehaviour 生命周期。
    /// </summary>
    public class ActorState
    {
        public Rigidbody2D Rb;

        public Vector2 Velocity
        {
            get => Rb.linearVelocity;
            set => Rb.linearVelocity = value;
        }

        /// <summary>-1 面左，1 面右。</summary>
        public int Direction = 1;

        /// <summary>由 GroundSensor 每 FixedUpdate 刷新。</summary>
        public bool IsGrounded;

        /// <summary>由 WallSensor（后续扩展）刷新。</summary>
        public bool IsTouchingWall;

        /// <summary>
        /// 个体时间缩放。Actor 以此乘 Time.deltaTime 后分发给模块，
        /// 实现单体时停/慢放，不影响全局 Time.timeScale。
        /// 也用于 meta 效果（例如"系统篡改玩家时间流速"）。
        /// </summary>
        public float LocalTimeScale = 1f;
    }
}
