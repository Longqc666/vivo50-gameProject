namespace D225.Interact
{
    /// <summary>
    /// 可躲藏角色（成员1 的玩家控制器实现）。躲藏点只负责判定与占用，移动锁定由角色自己完成；
    /// 这样躲藏点不必知道玩家用哪种移动组件。
    /// </summary>
    public interface IHidableActor
    {
        /// <summary>当前是否躲在某个躲藏点里。</summary>
        bool IsHidden { get; }

        /// <summary>
        /// 是否处于「抓捕已成立/贴身」的锁定态。由成员2 的抓捕判定写入，
        /// 用于阻止玩家在被抓瞬间躲进柜子获得免疫（策划案 5.4）。
        /// </summary>
        bool IsCatchLocked { get; }

        /// <summary>进入躲藏：锁定移动、切到躲藏表现、把位置移到 <paramref name="spot"/> 的隐藏点。</summary>
        void EnterHiding(HidingSpot spot);

        /// <summary>离开躲藏：恢复移动与正常表现。</summary>
        void ExitHiding();
    }
}
