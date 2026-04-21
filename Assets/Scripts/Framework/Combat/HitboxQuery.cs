using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Framework.AI;

namespace Game.Framework.Combat
{
    /// <summary>
    /// Hitbox 判定 —— 攻击方在"active 帧"调用，帧内做一次 OverlapBox 查询，
    /// 过滤同阵营 / 重复命中 / 无 Hurtbox 的目标，对通过的目标 TakeDamage 并抛 HitLanded 事件。
    ///
    /// 不保存任何状态在这里；攻击方自己维护"本次挥击已命中过谁"的 set 以支持 1 次挥击多段判定。
    /// </summary>
    public static class HitboxQuery
    {
        // 再入安全的 buffer 小池：TakeDamage 回调里可能又发起 HitboxQuery（连锁爆炸等），
        // 深度用 _depth 跟踪，从池里取对应层级的 buffer。超过池深度时退回 new Collider2D[]（罕见、GC 可接受）
        private const int BufferCapacity = 32;
        private const int PoolDepth = 4;
        private static readonly Collider2D[][] _bufPool = InitPool();
        private static int _depth;

        private static Collider2D[][] InitPool()
        {
            var arr = new Collider2D[PoolDepth][];
            for (int i = 0; i < PoolDepth; i++) arr[i] = new Collider2D[BufferCapacity];
            return arr;
        }

        private static Collider2D[] RentBuffer()
        {
            if (_depth < PoolDepth) return _bufPool[_depth];
            return new Collider2D[BufferCapacity]; // 再入太深时兜底分配，不 crash
        }

        /// <summary>
        /// 在世界坐标 center、角度 angle 的 box 范围内查询 Hurtbox，对所有敌对目标造成伤害。
        /// alreadyHit 若非空，会把命中的 Hurtbox 加进去，用于攻击方跨帧去重。
        /// </summary>
        /// <returns>本次实际造成有效伤害的目标数（0 表示空挥）。</returns>
        public static int OverlapBox(
            Vector2 center, Vector2 size, float angleDeg,
            LayerMask mask,
            Faction attackerFaction,
            DamageInfo template,
            GameObject attacker,
            HashSet<Hurtbox> alreadyHit = null)
        {
            var buf = RentBuffer();
            _depth++;
            int count;
            try
            {
                count = Physics2D.OverlapBoxNonAlloc(center, size, angleDeg, buf, mask);
            }
            catch
            {
                _depth--;
                throw;
            }

            int hits = 0;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var col = buf[i];
                    if (col == null) continue;

                    // 必须挂 Hurtbox 才算有效目标（普通地形 / 装饰不被误伤）
                    var hb = col.GetComponent<Hurtbox>();
                    if (hb == null) continue;

                    // 阵营过滤
                    if (!attackerFaction.IsHostile(hb.faction)) continue;

                    // 跨帧去重
                    if (alreadyHit != null && !alreadyHit.Add(hb)) continue;

                    var dmg = hb.Damageable;
                    if (dmg == null || !dmg.IsAlive) continue;

                    var info = template;
                    info.hitPoint = center;
                    info.source = attacker;
                    dmg.TakeDamage(info);

                    EventBus.Publish(new HitLanded(attacker, hb.gameObject, center, info.amount));
                    hits++;
                }
            }
            finally
            {
                _depth--;
            }
            return hits;
        }
    }

    /// <summary>EventBus payload。每次 Hitbox 命中 Hurtbox 都广播，供顿帧 / 震屏 / 特效 / 音效统一订阅。</summary>
    public readonly struct HitLanded
    {
        public readonly GameObject Attacker;
        public readonly GameObject Target;
        public readonly Vector2 Point;
        public readonly float Amount;
        public HitLanded(GameObject a, GameObject t, Vector2 p, float amt)
        { Attacker = a; Target = t; Point = p; Amount = amt; }
    }
}
