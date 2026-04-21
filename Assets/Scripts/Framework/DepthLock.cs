using UnityEngine;

namespace Framework
{
    /// <summary>
    /// 强制把物体锁在指定 Z 平面。
    /// 2.5D（透视相机）下防止角色因误操作或物理抖动脱离 Gameplay 平面 (Z=0)。
    /// 默认每帧 LateUpdate 吸附；Editor 模式下也生效，便于摆场景时及时回正。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DepthLock : MonoBehaviour
    {
        [Tooltip("目标 Z 值。Gameplay 平面用 0；前景/背景片可设置为其它值。")]
        public float targetZ = 0f;

        [Tooltip("允许的误差范围（小于该差值不纠正，避免无意义写入）。")]
        public float tolerance = 0.0001f;

        [Tooltip("是否使用局部坐标（作为子物体用）。默认世界坐标。")]
        public bool useLocal = false;

        private void LateUpdate()
        {
            if (useLocal)
            {
                var p = transform.localPosition;
                if (Mathf.Abs(p.z - targetZ) > tolerance)
                {
                    p.z = targetZ;
                    transform.localPosition = p;
                }
            }
            else
            {
                var p = transform.position;
                if (Mathf.Abs(p.z - targetZ) > tolerance)
                {
                    p.z = targetZ;
                    transform.position = p;
                }
            }
        }
    }
}
