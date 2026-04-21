using System.Collections;
using UnityEngine;
using Game.Framework.AI;

namespace Game.Framework.Combat
{
    /// <summary>
    /// 受击闪白。订阅同对象（或父对象）上的 Health.OnDamaged，命中瞬间把
    /// SpriteRenderer 的颜色替换成 flashColor，duration 秒后回原色。
    ///
    /// 挂载：和 Health 同一 GameObject，或其父对象；visualTarget 指向 Sprite 根。
    /// 支持多 SpriteRenderer（GetComponentsInChildren），统一上色统一恢复。
    /// </summary>
    public class HurtFlash : MonoBehaviour
    {
        [Tooltip("要闪白的 Sprite 根。不填则自身 GetComponentsInChildren<SpriteRenderer>()。")]
        public Transform visualTarget;

        public Color flashColor = Color.white;
        [Range(0.01f, 0.5f)] public float duration = 0.08f;

        private Health _health;
        private SpriteRenderer[] _renderers;
        private Color[] _original;
        private Coroutine _co;

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (_health == null) _health = GetComponentInParent<Health>();

            var root = visualTarget != null ? visualTarget : transform;
            _renderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            _original = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _original[i] = _renderers[i].color;
        }

        private void OnEnable()
        {
            if (_health != null) _health.OnDamaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnDamaged -= OnDamaged;
            if (_co != null) { StopCoroutine(_co); _co = null; }
            Restore();
        }

        private void OnDamaged(DamageInfo _)
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(FlashOnce());
        }

        private IEnumerator FlashOnce()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = flashColor;

            // 用 unscaledDeltaTime 保证顿帧期间也能看到闪白
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            Restore();
            _co = null;
        }

        private void Restore()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = _original[i];
        }
    }
}
