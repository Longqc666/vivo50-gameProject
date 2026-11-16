using UnityEngine;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>物品分类：决定背包分页与提示语气。</summary>
    public enum ItemCategory
    {
        /// <summary>工具：手电等可切换道具。</summary>
        Tool,

        /// <summary>谜题物品：保险丝、门禁零件等。</summary>
        PuzzleItem,

        /// <summary>钥匙：主线通行物，使用后不消耗。</summary>
        Key,

        /// <summary>线索：纸条、公告栏内容，进背包的「线索页」。</summary>
        Clue
    }

    /// <summary>
    /// 物品数据配置（策划案 12：配置与逻辑分离）。ItemId 是背包去重与谜题判定的唯一键，
    /// 同一物品的所有引用必须填同一个 ItemId。
    /// </summary>
    [CreateAssetMenu(fileName = "D225_Item_New", menuName = "D225/物品数据 (ItemData)")]
    public sealed class ItemData : ScriptableObject
    {
        [Tooltip("唯一键，例如 item.flashlight；背包去重与谜题条件都用它。")]
        [SerializeField]
        private string itemId = "item.new";

        [Tooltip("显示名，例如「手电筒」。")]
        [SerializeField]
        private string displayName = "新物品";

        [Tooltip("拾取/调查后给玩家看的说明。")]
        [TextArea(2, 5)]
        [SerializeField]
        private string description = string.Empty;

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private ItemCategory category = ItemCategory.PuzzleItem;

        [Tooltip("可堆叠物品（原型期只有消耗品需要）。主线道具保持关闭，避免重复拾取。")]
        [SerializeField]
        private bool stackable;

        [Tooltip("拾取成功时写入的任务标记。")]
        [SerializeField]
        private PuzzleFlag pickupFlag = PuzzleFlag.None;

        [Tooltip("调查成功时写入的任务标记（线索类使用）。")]
        [SerializeField]
        private PuzzleFlag inspectFlag = PuzzleFlag.None;

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId.Trim();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public string Description => description ?? string.Empty;

        public Sprite Icon => icon;

        public ItemCategory Category => category;

        public bool Stackable => stackable;

        public PuzzleFlag PickupFlag => pickupFlag;

        public PuzzleFlag InspectFlag => inspectFlag;

        /// <summary>编辑器工具与自测使用：一次性写入配置。</summary>
        public void Configure(
            string id,
            string title,
            string desc,
            ItemCategory itemCategory,
            bool canStack = false,
            PuzzleFlag onPickup = PuzzleFlag.None,
            PuzzleFlag onInspect = PuzzleFlag.None,
            Sprite sprite = null)
        {
            itemId = id;
            displayName = title;
            description = desc;
            category = itemCategory;
            stackable = canStack;
            pickupFlag = onPickup;
            inspectFlag = onInspect;
            icon = sprite;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = "item.untitled";
            }
        }
    }
}
