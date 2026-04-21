using UnityEngine;

namespace Game.Framework.View
{
    /// <summary>
    /// 进入触发器 → 切 ViewMode；可选离开时反切。
    /// 挂在带 Collider2D (Is Trigger) 的物体上。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ViewModeTrigger : MonoBehaviour
    {
        public ViewMode onEnterMode = ViewMode.TopDown;
        public bool revertOnExit = true;
        public ViewMode onExitMode = ViewMode.Side;

        [Tooltip("只响应带此 Tag 的物体；留空则响应所有。")]
        public string requiredTag = "Player";

        [Tooltip("只触发一次。")]
        public bool oneShot = false;

        private bool _fired;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!Matches(other)) return;
            if (oneShot && _fired) return;
            _fired = true;
            ViewModeController.Instance?.SetMode(onEnterMode);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!revertOnExit) return;
            if (!Matches(other)) return;
            ViewModeController.Instance?.SetMode(onExitMode);
        }

        private bool Matches(Collider2D other)
        {
            if (string.IsNullOrEmpty(requiredTag)) return true;
            return other.CompareTag(requiredTag);
        }
    }
}
