using UnityEngine;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>
    /// 二楼楼梯门禁（T08）：先恢复供电，再持门禁钥匙才能开启；两种缺失给不同原因。
    /// 钥匙是主线进度物品，开门不消耗。
    /// </summary>
    [AddComponentMenu("D225/谜题/楼梯门禁 (StairDoorPuzzle)")]
    public sealed class StairDoorPuzzle : PuzzleGate
    {
        [Header("需求")]
        [Tooltip("门禁钥匙的 ItemId，必须与拾取物一致。")]
        [SerializeField]
        private string keyItemId = "item.stair_key";

        [SerializeField]
        private string keyDisplayName = "门禁钥匙";

        [Tooltip("是否要求配电箱已修复（门禁上电）。")]
        [SerializeField]
        private bool requirePower = true;

        protected override bool TryGetBlocker(InteractionContext context, out string reason)
        {
            if (context.Quest == null)
            {
                reason = "门禁读取不到任务状态，请检查场景里的任务进度来源。";
                return true;
            }

            if (requirePower && !context.Quest.HasFlag(PuzzleFlag.PowerRestored))
            {
                reason = "门禁没有电：先去配电箱恢复供电。";
                return true;
            }

            if (context.Inventory == null || !context.Inventory.Has(keyItemId))
            {
                reason = "门禁无响应：需要「" + keyDisplayName + "」。";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        protected override InteractionResult Apply(InteractionContext context)
        {
            return InteractionResult.Ok("钥匙插进锁孔，指示灯变绿，楼梯门滑开了。");
        }

        /// <summary>编辑器工具与自测使用：配置需求与结果。</summary>
        public void Configure(string keyId, string keyName, PuzzleFlag resultFlag, bool needsPower = true, string successText = null)
        {
            keyItemId = keyId;
            keyDisplayName = keyName;
            requirePower = needsPower;
            ConfigureResult(resultFlag, successText);
        }
    }
}
