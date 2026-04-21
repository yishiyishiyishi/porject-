using UnityEngine;
using Game.Core;
using Game.Core.Narrative;
using Game.Framework.Save;

namespace Game.Framework
{
    /// <summary>
    /// 全局叙事状态的运行时入口。把 NarrativeState SO 指针存在这里，
    /// 任何代码都可通过 NarrativeRuntime.State 拿到；避免到处 FindObject。
    ///
    /// 启动期订阅 MetaLoaded：SaveManager 从磁盘读到 Meta 后派发此事件，
    /// 我们在这里把跨周目数据（glitchDiscovered / endings）回灌到 NarrativeState。
    /// 槽位数据则在 SaveManager.LoadSlot 时直接写入 State，无需走事件。
    ///
    /// 用法：场景里放一个 GameObject 挂此组件，把 NarrativeState SO 拖进 stateAsset。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class NarrativeRuntime : MonoBehaviour
    {
        public static NarrativeState State { get; private set; }

        [SerializeField] private NarrativeState stateAsset;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Tooltip("进入 Play 时是否把 State 里的本周目字段（loopIndex、milestones）重置。"
               + "避免编辑器下 SO 残留上次 Play 的数据。")]
        [SerializeField] private bool resetSlotFieldsOnAwake = true;

        private void Awake()
        {
            if (State != null && State != stateAsset) { Destroy(gameObject); return; }
            State = stateAsset;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            if (resetSlotFieldsOnAwake && State != null)
            {
                State.loopIndex = 0;
                State.reachedMilestones.Clear();
            }

            EventBus.Subscribe<MetaLoaded>(OnMetaLoaded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<MetaLoaded>(OnMetaLoaded);
            if (State == stateAsset) State = null;
        }

        private void OnMetaLoaded(MetaLoaded evt)
        {
            if (State == null || evt.Data == null) return;
            State.totalBoots = evt.Data.totalBoots;
            State.glitchDiscovered = evt.Data.glitchDiscovered;
            // endings / persistentFlags 目前 NarrativeState 里没字段存；
            // 需要时由订阅 MetaLoaded 的业务层自行取 evt.Data.endingsReached 使用
        }
    }
}
