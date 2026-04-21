namespace Framework
{
    /// <summary>
    /// 2.5D 场景的 Z 轴深度协议常量。所有美术/策划/程序放置物体时统一参考这里。
    ///
    ///   - Gameplay 平面（战斗/角色/碰撞）：Z = 0
    ///   - 前景遮挡（镜头前树叶/栏杆）：Z 为负（离相机近）
    ///   - 背景分层（近景/中景/远景/天空）：Z 为正
    ///
    /// 相机通常位于 Z = -10。FOV 30 时，Z=10 的物体约缩小至 50%，Z=50 约 17%。
    /// </summary>
    public static class ZDepth
    {
        // Gameplay
        public const float Gameplay = 0f;

        // Foreground（相机在 Z=-10 时，前景应大于 -10 且小于 0）
        public const float ForegroundNear = -2f;
        public const float ForegroundFar  = -5f;

        // Background
        public const float BackgroundNear = 10f;   // 近景建筑
        public const float BackgroundMid  = 30f;   // 中景
        public const float BackgroundFar  = 80f;   // 远山
        public const float Sky            = 200f;  // 天空盒面片
    }
}
