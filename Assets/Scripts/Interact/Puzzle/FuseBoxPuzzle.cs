using UnityEngine;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>
    /// 配电箱谜题（T07）：空手操作提示缺少保险丝；带着保险丝操作则安装、消耗保险丝并恢复供电。
    /// 对应区域B「恢复供电」步骤（策划案 5.5）。
    /// </summary>
    [AddComponentMenu("D225/谜题/配电箱 (FuseBoxPuzzle)")]
    public sealed class FuseBoxPuzzle : PuzzleGate
    {
        [Header("需求")]
        [Tooltip("保险丝的 ItemId，必须与拾取物一致。")]
        [SerializeField]
        private string fuseItemId = "item.fuse";

        [SerializeField]
        private string fuseDisplayName = "保险丝";

        [Tooltip("安装后是否从背包扣除保险丝。")]
        [SerializeField]
        private bool consumeFuse = true;

        protected override bool TryGetBlocker(InteractionContext context, out string reason)
        {
            if (context.Inventory == null)
            {
                reason = "配电箱：背包数据未就绪";
                return true;
            }

            if (!context.Inventory.Has(fuseItemId))
            {
                reason = "配电箱缺少「" + fuseDisplayName + "」：先去设备间把它找出来。";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        protected override InteractionResult Apply(InteractionContext context)
        {
            if (consumeFuse && !context.Inventory.TryConsume(fuseItemId))
            {
                return InteractionResult.Rejected("安装失败：手里没有可用的" + fuseDisplayName);
            }

            return InteractionResult.Ok("你把" + fuseDisplayName + "装进配电箱，走廊的灯重新亮了起来。");
        }

        /// <summary>编辑器工具与自测使用：配置需求与结果。</summary>
        public void Configure(string fuseId, string fuseName, PuzzleFlag resultFlag, bool consume = true, string successText = null)
        {
            fuseItemId = fuseId;
            fuseDisplayName = fuseName;
            consumeFuse = consume;
            ConfigureResult(resultFlag, successText);
        }
    }
}
