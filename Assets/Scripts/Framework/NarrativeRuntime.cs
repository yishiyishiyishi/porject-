using UnityEngine;
using Game.Core.Narrative;

namespace Game.Framework
{
    /// <summary>
    /// 全局叙事状态的运行时入口。把 NarrativeState SO 指针存在这里，
    /// 任何代码都可通过 NarrativeRuntime.State 拿到；避免到处 FindObject。
    ///
    /// 用法：场景里放一个 GameObject 挂此组件，把 NarrativeState SO 拖进 stateAsset。
    /// 切场景保留（DontDestroyOnLoad）。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NarrativeRuntime : MonoBehaviour
    {
        public static NarrativeState State { get; private set; }

        [SerializeField] private NarrativeState stateAsset;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private void Awake()
        {
            if (State != null && State != stateAsset) { Destroy(gameObject); return; }
            State = stateAsset;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            // 只在自己就是当前持有者时清空，避免 Domain Reload 后误清
            if (State == stateAsset) State = null;
        }
    }
}
