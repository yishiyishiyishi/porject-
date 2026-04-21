using UnityEngine;

namespace Game.Framework.Visual
{
    /// <summary>
    /// 按 Y 坐标动态写 sortingOrder，实现"脚下物挡住远处物"的俯视角深度排序。
    ///
    /// 公式：sortingOrder = baseOrder - round(transform.position.y * precision)
    /// 即：Y 越小（屏幕下方）→ sortingOrder 越大 → 越靠前渲染。
    ///
    /// 场景 Transparency Sort Mode 为 Perspective 时，Y 轴排序主要靠 Z；
    /// 但 Y 差距不明显时 URP 2D Renderer 的顺序是不稳的，这个组件把 Y 差
    /// 直接反映到 sortingOrder，彻底解决"我在桌子后面却显示在桌子前"的问题。
    ///
    /// 横板模式下多数对象 Y 轴范围很小，本组件可关闭；
    /// TopDown 模式下对场景所有 XY 平面物件（角色、桌子、树）挂上即可。
    ///
    /// 多 SpriteRenderer（比如角色本体 + 影子 + 特效）的情况：挂 Unity 内置的
    /// SortingGroup 组件在根对象上，本组件写其 sortingOrder 会更精确；此处为减
    /// 少依赖不强引 SortingGroup 类型，若存在则走 Reflection-free 路径由用户自行
    /// 在 SortingGroup 上用同一脚本挂一份即可（SortingGroup 本身也是 Renderer 子类）。
    /// 当前实现只操作直接的 SpriteRenderer。
    ///
    /// 挂载：哪个对象要参与深度排序，就挂在哪个对象上。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class YSortModule : MonoBehaviour
    {
        [Tooltip("要写 sortingOrder 的目标 SpriteRenderer。不填则 GetComponentInChildren<SpriteRenderer>。")]
        public SpriteRenderer targetRenderer;

        [Tooltip("基准 sortingOrder，Y=0 时取该值。")]
        public int baseOrder = 0;

        [Tooltip("单位：sortingOrder 每单位 Y 的变化量。越大分辨率越高。")]
        public int precision = 100;

        [Tooltip("静态物体：只在 Awake 写一次，不每帧刷。")]
        public bool staticObject = false;

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<SpriteRenderer>();
            Apply();
            if (staticObject) enabled = false;
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (targetRenderer == null) return;
            targetRenderer.sortingOrder = baseOrder - Mathf.RoundToInt(transform.position.y * precision);
        }
    }
}
