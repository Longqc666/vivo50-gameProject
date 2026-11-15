namespace D225.Quest
{
    /// <summary>
    /// 主线谜题进度标记。由成员4 的交互/谜题模块写入，成员5 的 HUD 与成员6 的流程状态读取。
    /// 约定：一旦写入即视为「永久进度」，检查点重试时不回滚（见策划案 5.6）。
    /// </summary>
    /// <remarks>
    /// 本枚举是 <c>QuestFlag</c> 契约在谜题侧的落地版本。成员6 的 QuestManager 正式接入时，
    /// 应实现 <see cref="IQuestLog"/> 并把这里的取值映射到其 <c>QuestFlag</c>，不要在两处各写一份判定。
    /// </remarks>
    public enum PuzzleFlag
    {
        /// <summary>无占位：任何判定都应视为「不要求该标记」。</summary>
        None = 0,

        /// <summary>拿到手电筒（区域A，教学）。</summary>
        FlashlightAcquired = 1,

        /// <summary>读过 225 宿舍桌面便笺（配电室位置线索）。</summary>
        DutyNoteRead = 2,

        /// <summary>拿到保险丝（区域B 设备间）。</summary>
        FuseAcquired = 3,

        /// <summary>配电箱已修复，局部供电恢复（走廊灯、门禁上电）。</summary>
        PowerRestored = 4,

        /// <summary>拿到门禁钥匙。</summary>
        StairKeyAcquired = 5,

        /// <summary>二楼楼梯门已开启。</summary>
        StairDoorUnlocked = 6,

        /// <summary>门厅出口联动装置已启动，出口从锁定切换为开启。</summary>
        ExitUnlocked = 7,

        /// <summary>玩家进入楼外安全区，六人撤离结算。</summary>
        EvacuationComplete = 8,

        /// <summary>读过走廊公告栏（门禁密码线索）。</summary>
        BulletinRead = 9,

        /// <summary>第一次躲进躲藏点（教学提示用）。</summary>
        FirstHiding = 10
    }
}
