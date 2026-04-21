using UnityEngine;

namespace Game.Framework.Visual
{
    /// <summary>
    /// 挤压 / 拉伸 视觉反馈。游戏感（Game Feel）的标准配件。
    ///
    /// 工作方式：
    ///   - 维护一个"目标 scale"和"当前 scale"，每帧向目标插值
    ///   - API：TriggerJump() / TriggerLand(impactNormalized) / TriggerTurn()
    ///   - 不改 transform 根 scale（会影响物理碰撞），只改视觉子节点的 localScale
    ///
    /// 挂载：挂在 Actor 根对象上；visualTarget 指向"只含视觉"的子物体
    ///       （建议 Hierarchy 布局：Actor(根) → Visual(SpriteRenderer/Animator)）。
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class SquashStretch : MonoBehaviour
    {
        [Tooltip("要被挤压/拉伸的视觉子物体。不填则用自己 transform（会影响碰撞，慎用）。")]
        public Transform visualTarget;

        [Header("Response")]
        [Tooltip("scale 向目标值回弹的速度（越大越脆）。")]
        public float returnSpeed = 12f;

        [Header("Presets")]
        public Vector2 jumpScale = new Vector2(0.85f, 1.2f);  // 起跳：X 扁、Y 拉
        public Vector2 landScale = new Vector2(1.25f, 0.75f); // 落地：X 扁、Y 压
        public Vector2 turnScale = new Vector2(1.15f, 0.9f);  // 急停：X 扁一点

        [Tooltip("触发后效果的持续时间上限，超过就强行回弹。")]
        public float maxHoldSeconds = 0.12f;

        private Vector3 _baseScale = Vector3.one;
        private Vector3 _targetScale = Vector3.one;
        private float _holdTimer;

        private void Awake()
        {
            if (visualTarget == null) visualTarget = transform;
            _baseScale = visualTarget.localScale;
            _targetScale = _baseScale;
        }

        private void LateUpdate()
        {
            if (_holdTimer > 0f)
            {
                _holdTimer -= Time.deltaTime;
                if (_holdTimer <= 0f) _targetScale = _baseScale;
            }
            visualTarget.localScale = Vector3.Lerp(
                visualTarget.localScale, _targetScale, 1f - Mathf.Exp(-returnSpeed * Time.deltaTime));
        }

        /// <summary>起跳：瞬间拉高。</summary>
        public void TriggerJump() => Push(jumpScale);

        /// <summary>落地：按冲击强度 0..1 压扁。</summary>
        public void TriggerLand(float impact01 = 1f)
        {
            impact01 = Mathf.Clamp01(impact01);
            Vector2 k = Vector2.Lerp(Vector2.one, landScale, impact01);
            Push(k);
        }

        /// <summary>急停 / 急转。</summary>
        public void TriggerTurn() => Push(turnScale);

        /// <summary>自定义一次缩放事件。</summary>
        public void Push(Vector2 xyScale)
        {
            // 保留 Direction 翻转（localScale.x 正负由 Actor.SetDirection 翻）
            float sign = Mathf.Sign(visualTarget.localScale.x == 0f ? 1f : visualTarget.localScale.x);
            _targetScale = new Vector3(_baseScale.x * xyScale.x * sign, _baseScale.y * xyScale.y, _baseScale.z);
            _holdTimer = maxHoldSeconds;
        }
    }
}
