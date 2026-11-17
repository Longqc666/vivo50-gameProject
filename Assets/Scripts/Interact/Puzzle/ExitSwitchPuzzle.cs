using UnityEngine;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>
    /// 门厅出口联动装置（T11 前半）：未恢复供电或未打开楼梯门时不可用；
    /// 有效操作后把出口从锁定切换为开启（写入 <see cref="PuzzleFlag.ExitUnlocked"/>）。
    /// </summary>
    [AddComponentMenu("D225/谜题/出口联动装置 (ExitSwitchPuzzle)")]
    public sealed class ExitSwitchPuzzle : PuzzleGate
    {
        [Header("需求")]
        [Tooltip("是否要求局部供电已恢复。")]
        [SerializeField]
        private bool requirePower = true;

        [Tooltip("是否要求二楼楼梯门已开启（证明玩家已到过门厅）。")]
        [SerializeField]
        private bool requireStairDoor = true;

        protected override bool TryGetBlocker(InteractionContext context, out string reason)
        {
            if (context.Quest == null)
            {
                reason = "出口装置读取不到任务状态，请检查场景里的任务进度来源。";
                return true;
            }

            if (requirePower && !context.Quest.HasFlag(PuzzleFlag.PowerRestored))
            {
                reason = "出口联动装置没有电：先恢复配电。";
                return true;
            }

            if (requireStairDoor && !context.Quest.HasFlag(PuzzleFlag.StairDoorUnlocked))
            {
                reason = "联动装置还没接通：先打开二楼楼梯门。";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        protected override InteractionResult Apply(InteractionContext context)
        {
            return InteractionResult.Ok("你扳下出口联动装置，门厅的应急灯闪了两下，出口锁扣弹开了。");
        }

        /// <summary>编辑器工具与自测使用：配置需求与结果。</summary>
        public void Configure(PuzzleFlag resultFlag, bool needsPower = true, bool needsStairDoor = true, string successText = null)
        {
            requirePower = needsPower;
            requireStairDoor = needsStairDoor;
            ConfigureResult(resultFlag, successText);
        }
    }
}
