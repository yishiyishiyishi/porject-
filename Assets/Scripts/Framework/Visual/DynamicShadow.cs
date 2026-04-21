using UnityEngine;

namespace Game.Framework.Visual
{
    /// <summary>
    /// 脚下动态影子。给 2D / 俯视角的"纸片人"加体积感的最便宜手段。
    ///
    /// 工作方式：
    ///   - 在 Actor 子层级里找（或自动建）一个 SpriteRenderer 作为影子
    ///   - 根据角色"离地高度"（由射线探测到脚下地面得到）动态缩放 + 调透明度
    ///   - 影子位置始终锚在"地面点"处，因此跳跃时角色飞起来而影子留在地上
    ///
    /// 横板模式下跳跃 → 影子缩小变淡；落地 → 影子恢复。
    /// TopDown 模式下如果用视觉偏移（角色 childSprite.localPosition.y += h）来模拟跳，
    /// 这个组件一样成立 —— 地面点始终在 Actor.position，角色视觉浮起。
    ///
    /// 挂载：直接挂在 Actor 所在 GameObject 上。不继承 ActorModule 的原因是它不需要 Actor
    /// 调度，自己 LateUpdate 即可；也方便独立于 Actor 使用（挂在装饰物上也行）。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class DynamicShadow : MonoBehaviour
    {
        [Header("Shadow Sprite")]
        [Tooltip("影子贴图。建议：一张模糊的黑色椭圆 PNG，Pivot 居中。")]
        public Sprite shadowSprite;

        [Tooltip("如果为空，Awake 时会建一个子物体挂 SpriteRenderer 放 shadowSprite。")]
        public SpriteRenderer shadowRenderer;

        [Header("Sizing")]
        [Tooltip("贴地时的基准世界尺寸（Y 方向）。X 按此比例算。")]
        public Vector2 baseSize = new Vector2(1.0f, 0.3f);

        [Tooltip("离地越远缩得越小；1 表示离地 1m 时缩到该比例。")]
        [Range(0.1f, 1f)] public float minScaleAtFarHeight = 0.4f;
        [Tooltip("达到该离地高度后影子尺寸不再继续缩小。")]
        public float maxHeight = 3f;

        [Header("Opacity")]
        [Range(0f, 1f)] public float baseAlpha = 0.5f;
        [Tooltip("离地时的最低透明度。")]
        [Range(0f, 1f)] public float minAlphaAtFarHeight = 0.15f;

        [Header("Ground Raycast")]
        public LayerMask groundLayer = ~0;
        [Tooltip("从 Actor 原点向下最长射线距离。")]
        public float probeDistance = 10f;
        [Tooltip("影子相对地面点的抬升，防止被地面剪裁。正值为向相机方向提一点。")]
        public float groundZOffset = -0.01f;

        [Header("Sorting")]
        public string sortingLayer = "Default";
        public int sortingOrder = -100;

        private Transform _shadowTr;

        private void Awake()
        {
            if (shadowRenderer == null)
            {
                var go = new GameObject("~Shadow");
                go.transform.SetParent(transform, worldPositionStays: false);
                shadowRenderer = go.AddComponent<SpriteRenderer>();
            }
            shadowRenderer.sprite = shadowSprite;
            shadowRenderer.sortingLayerName = sortingLayer;
            shadowRenderer.sortingOrder = sortingOrder;
            var c = shadowRenderer.color; c.a = baseAlpha; shadowRenderer.color = c;
            _shadowTr = shadowRenderer.transform;
        }

        private void LateUpdate()
        {
            if (shadowRenderer == null || _shadowTr == null) return;

            // 从角色原点向下发射线找地面
            var hit = Physics2D.Raycast(transform.position, Vector2.down, probeDistance, groundLayer);
            float groundY;
            bool hasGround;
            if (hit.collider != null)
            {
                groundY = hit.point.y;
                hasGround = true;
            }
            else
            {
                groundY = transform.position.y; // 找不到地面时影子直接贴脚下，避免飘到屏幕外
                hasGround = false;
            }

            float height = Mathf.Max(0f, transform.position.y - groundY);
            float k = Mathf.Clamp01(height / Mathf.Max(0.01f, maxHeight));

            // 位置（世界空间，但跟随角色 X；Y 固定到地面；Z 贴地）
            _shadowTr.position = new Vector3(transform.position.x, groundY, transform.position.z + groundZOffset);

            // 尺寸：离地越远越小
            float scaleK = Mathf.Lerp(1f, minScaleAtFarHeight, k);
            _shadowTr.localScale = new Vector3(baseSize.x * scaleK, baseSize.y * scaleK, 1f);

            // 透明度
            float a = hasGround ? Mathf.Lerp(baseAlpha, minAlphaAtFarHeight, k) : 0f;
            var col = shadowRenderer.color;
            col.a = a;
            shadowRenderer.color = col;
        }
    }
}
