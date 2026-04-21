using UnityEngine;
using Game.Framework.AI;

namespace Game.Framework.Combat
{
    /// <summary>
    /// 受击盒。挂在能挨打的对象身上（玩家/敌人/可破坏物），含：
    ///   - faction：阵营，给 HitboxQuery 做友军过滤
    ///   - damageable：实际接受伤害的对象引用（通常是同层级的 Health 组件）
    ///
    /// 该组件本身不参与碰撞 —— 它只是"标签 + 入口"。真正的命中判定由攻击方
    /// 的 HitboxQuery.OverlapBox(...) 发起（帧动画事件驱动），这是为了让
    /// hitbox 仅在"active 帧"存在，符合传统动作游戏的感觉。
    ///
    /// 挂载要求：同对象上有一个 Collider2D（可以是 trigger，用于被 OverlapBox 搜中）。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hurtbox : MonoBehaviour
    {
        [Tooltip("阵营，决定谁能打它。")]
        public Faction faction = Faction.Enemy;

        [Tooltip("伤害接收者。留空则在 Awake 时 GetComponentInParent<IDamageable>()。")]
        [SerializeField] private MonoBehaviour damageableRef;

        private IDamageable _damageable;

        public IDamageable Damageable => _damageable;

        private void Awake()
        {
            _damageable = damageableRef as IDamageable;
            if (_damageable == null)
                _damageable = GetComponentInParent<IDamageable>();
            if (_damageable == null)
                Debug.LogWarning($"[Hurtbox] {name} 找不到 IDamageable，挨打不会掉血。", this);
        }
    }
}
