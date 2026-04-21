namespace Game.Framework.View
{
    /// <summary>
    /// 视角模式。Side = 横版平视（XY 平面，重力向下）；
    /// TopDown = 俯视（XY 平面，无重力，W/S 直接驱动 Y 速度）。
    ///
    /// 物理层仍然是 Rigidbody2D，所谓"俯视"只是：
    ///   1. 相机切到一个上方俯视的 CinemachineCamera
    ///   2. 玩家 JumpModule 禁用（不再对 Y 做重力/跳跃）
    ///   3. MoveModule 同时吃 Horizontal + Vertical 输入，驱动 XY 速度
    /// 这样 2D 骨骼/Sprite 不会因视角转正而被压成一条线，同时 2D 碰撞体全套继续可用。
    /// </summary>
    public enum ViewMode
    {
        Side,
        TopDown,
    }
}
