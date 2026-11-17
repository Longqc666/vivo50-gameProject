using System;
using System.Collections.Generic;
using UnityEngine;

namespace D225.Interact
{
    /// <summary>
    /// 背包与线索页（策划案 4-2）：按 <see cref="ItemData.ItemId"/> 去重计数，主线道具不可重复获取，
    /// 线索单独进线索页。只负责数据与事件，不判定谜题、不碰 UI。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/背包 (Inventory)")]
    public sealed class Inventory : MonoBehaviour
    {
        [Tooltip("进入场景时背包里已有的物品（通常留空）。")]
        [SerializeField]
        private ItemData[] startingItems = new ItemData[0];

        private readonly List<ItemData> items = new List<ItemData>();
        private readonly List<ItemData> clues = new List<ItemData>();
        private readonly Dictionary<string, int> countById = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool initialized;

        /// <summary>物品入包（参数：物品、当前数量）。</summary>
        public event Action<ItemData, int> ItemAdded;

        /// <summary>物品被消耗或移除（参数：物品、剩余数量）。</summary>
        public event Action<ItemData, int> ItemRemoved;

        /// <summary>发现新线索（同一线索只触发一次）。</summary>
        public event Action<ItemData> ClueAdded;

        /// <summary>背包内的物品（只读，按首次入包顺序）。</summary>
        public IReadOnlyList<ItemData> Items
        {
            get
            {
                EnsureInitialized();
                return items;
            }
        }

        /// <summary>线索页内容（只读）。</summary>
        public IReadOnlyList<ItemData> Clues
        {
            get
            {
                EnsureInitialized();
                return clues;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>懒惰初始化：编辑器测试里不会走 Awake，首次访问时补齐初始物品。</summary>
        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            if (startingItems == null)
            {
                return;
            }

            for (int i = 0; i < startingItems.Length; i++)
            {
                if (startingItems[i] != null)
                {
                    TryAdd(startingItems[i], out _);
                }
            }
        }

        /// <summary>某物品的持有数量。</summary>
        public int Count(string itemId)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            return countById.TryGetValue(itemId, out int count) ? count : 0;
        }

        /// <summary>是否持有某物品（按 ItemId）。</summary>
        public bool Has(string itemId)
        {
            return Count(itemId) > 0;
        }

        /// <summary>是否持有某物品（按数据引用）。</summary>
        public bool Has(ItemData item)
        {
            return item != null && Has(item.ItemId);
        }

        /// <summary>
        /// 尝试把物品放进背包。主线道具重复获取会被拒绝并给出原因（T03 只拾取一次）。
        /// </summary>
        public bool TryAdd(ItemData item, out string reason)
        {
            EnsureInitialized();

            if (item == null)
            {
                reason = "物品数据缺失";
                Debug.LogError("[D225] Inventory.TryAdd 收到空的 ItemData，请检查 Prefab 引用。");
                return false;
            }

            string id = item.ItemId;
            countById.TryGetValue(id, out int current);

            if (!item.Stackable && current > 0)
            {
                reason = "背包中已经有「" + item.DisplayName + "」了";
                return false;
            }

            countById[id] = current + 1;
            if (current == 0)
            {
                items.Add(item);
            }

            if (item.Category == ItemCategory.Clue)
            {
                AddClueInternal(item);
            }

            reason = string.Empty;
            ItemAdded?.Invoke(item, countById[id]);
            return true;
        }

        /// <summary>消耗/移除指定数量；数量不足返回 false，不产生任何改变。</summary>
        public bool TryConsume(string itemId, int amount = 1)
        {
            EnsureInitialized();

            if (amount <= 0 || string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            if (!countById.TryGetValue(itemId, out int current) || current < amount)
            {
                return false;
            }

            ItemData item = items.Find(entry => entry.ItemId == itemId);
            int remaining = current - amount;

            if (remaining == 0)
            {
                countById.Remove(itemId);
                items.RemoveAll(entry => entry.ItemId == itemId);
            }
            else
            {
                countById[itemId] = remaining;
            }

            if (item != null)
            {
                ItemRemoved?.Invoke(item, remaining);
            }

            return true;
        }

        /// <summary>把线索记入线索页；已记录过返回 false（不重复提示）。</summary>
        public bool AddClue(ItemData clue, out string reason)
        {
            EnsureInitialized();

            if (clue == null)
            {
                reason = "线索数据缺失";
                Debug.LogError("[D225] Inventory.AddClue 收到空的 ItemData，请检查 Prefab 引用。");
                return false;
            }

            if (AddClueInternal(clue))
            {
                reason = string.Empty;
                return true;
            }

            reason = "这条线索已经记下了";
            return false;
        }

        /// <summary>线索是否已记录（按 ItemId）。</summary>
        public bool HasClue(string clueId)
        {
            EnsureInitialized();
            return clues.Exists(clue => clue != null && clue.ItemId == clueId);
        }

        /// <summary>清空背包（检查点重试/重开本局时由流程侧调用）。</summary>
        public void Clear()
        {
            items.Clear();
            clues.Clear();
            countById.Clear();
            initialized = true;
        }

        /// <summary>编辑器工具与自测使用：配置初始物品。</summary>
        public void ConfigureStartingItems(ItemData[] initialItems)
        {
            startingItems = initialItems ?? new ItemData[0];
            Clear();
            initialized = false;
        }

        private bool AddClueInternal(ItemData clue)
        {
            for (int i = 0; i < clues.Count; i++)
            {
                if (clues[i] != null && clues[i].ItemId == clue.ItemId)
                {
                    return false;
                }
            }

            clues.Add(clue);
            ClueAdded?.Invoke(clue);
            return true;
        }
    }
}
