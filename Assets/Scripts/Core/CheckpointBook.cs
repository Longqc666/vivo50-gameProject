using System;
using System.Collections.Generic;
using D225.Interact;
using D225.Quest;

namespace D225.Core
{
    /// <summary>
    /// 一份检查点（策划案 5.6）：SceneID、出生点、关键物品、QuestFlag。
    /// 只保存「重试后要恢复的东西」，不保存鬼怪位置等场景内临时状态——那些靠重载场景重置。
    /// </summary>
    public sealed class CheckpointData
    {
        public CheckpointData(string sceneId, string spawnId)
        {
            SceneId = string.IsNullOrEmpty(sceneId) ? string.Empty : sceneId;
            SpawnId = string.IsNullOrEmpty(spawnId) ? string.Empty : spawnId;
            Items = Array.Empty<ItemData>();
            Clues = Array.Empty<ItemData>();
            Flags = Array.Empty<PuzzleFlag>();
        }

        /// <summary>区域 ID（A / B / C），对应 <see cref="RegionCatalog"/>。</summary>
        public string SceneId { get; }

        /// <summary>出生点 ID（关卡里 CP_* 空锚点的名字）。</summary>
        public string SpawnId { get; }

        /// <summary>背包里的道具（内存引用，MVP 不落盘；正式磁盘存档属于争取项）。</summary>
        public ItemData[] Items { get; private set; }

        /// <summary>已记录的线索。</summary>
        public ItemData[] Clues { get; private set; }

        /// <summary>写入时的任务标记快照。</summary>
        public PuzzleFlag[] Flags { get; private set; }

        /// <summary>第几次保存（自测与日志用，从 1 开始）。</summary>
        public int Revision { get; internal set; }

        public bool HasFlag(PuzzleFlag flag) => Array.IndexOf(Flags, flag) >= 0;

        /// <summary>从背包与任务日志抓取一份快照。</summary>
        public static CheckpointData Capture(string sceneId, string spawnId, Inventory inventory, IQuestLog quest)
        {
            var data = new CheckpointData(sceneId, spawnId);

            var items = new List<ItemData>();
            var clues = new List<ItemData>();
            if (inventory != null)
            {
                foreach (ItemData item in inventory.Items)
                {
                    if (item != null) items.Add(item);
                }

                foreach (ItemData clue in inventory.Clues)
                {
                    if (clue != null) clues.Add(clue);
                }
            }

            var flags = new List<PuzzleFlag>();
            if (quest != null)
            {
                foreach (PuzzleFlag flag in quest.RaisedFlags)
                {
                    if (flag != PuzzleFlag.None) flags.Add(flag);
                }
            }

            flags.Sort();
            data.Items = items.ToArray();
            data.Clues = clues.ToArray();
            data.Flags = flags.ToArray();
            return data;
        }

        /// <summary>把道具与线索写回（重试/换区后的新角色）背包；返回恢复的道具数量。</summary>
        public int RestoreInventory(Inventory inventory)
        {
            if (inventory == null)
            {
                return 0;
            }

            inventory.Clear();

            int restored = 0;
            foreach (ItemData item in Items)
            {
                if (item != null && inventory.TryAdd(item, out _))
                {
                    restored++;
                }
            }

            foreach (ItemData clue in Clues)
            {
                if (clue != null)
                {
                    inventory.AddClue(clue, out _);
                }
            }

            return restored;
        }

        /// <summary>把任务标记写回任务日志；返回新写入数量。</summary>
        public int RestoreQuest(IQuestLog quest)
        {
            if (quest == null)
            {
                return 0;
            }

            int added = 0;
            foreach (PuzzleFlag flag in Flags)
            {
                if (quest.SetFlag(flag)) added++;
            }

            return added;
        }

        public override string ToString()
        {
            return $"#{Revision} {SceneId}/{SpawnId} items={Items.Length} clues={Clues.Length} flags={Flags.Length}";
        }
    }

    /// <summary>
    /// 检查点账本（纯 C#，可 EditMode 测试）：保存「最近一次通过的区域+出生点」。
    /// 只前进不回退——回头走过旧检查点不会把进度改回去。
    /// </summary>
    public sealed class CheckpointBook
    {
        private readonly Dictionary<string, int> progress = new Dictionary<string, int>(StringComparer.Ordinal);
        private int revision;

        /// <summary>最近一份检查点；没有时为 null。</summary>
        public CheckpointData Current { get; private set; }

        /// <summary>保存成功时触发（HUD/音效可订阅）。</summary>
        public event Action<CheckpointData> Saved;

        /// <summary>保存次数。</summary>
        public int Count => revision;

        /// <summary>清空（返回主菜单、新开一局）。</summary>
        public void Clear()
        {
            Current = null;
            revision = 0;
            progress.Clear();
        }

        /// <summary>登记某个区域里最远到达的检查点序号；返回是否推进了进度。</summary>
        public bool Advance(string sceneId, string spawnId, int order)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return false;
            }

            if (progress.TryGetValue(sceneId, out int previous) && order <= previous)
            {
                return false;
            }

            progress[sceneId] = order;
            return true;
        }

        /// <summary>写入一份检查点。</summary>
        public CheckpointData Save(string sceneId, string spawnId, Inventory inventory, IQuestLog quest, int order)
        {
            Advance(sceneId, spawnId, order);

            CheckpointData data = CheckpointData.Capture(sceneId, spawnId, inventory, quest);
            data.Revision = ++revision;
            Current = data;
            Saved?.Invoke(data);
            return data;
        }

        /// <summary>区域进度（自测与日志用）。</summary>
        public int ProgressOf(string sceneId)
        {
            return progress.TryGetValue(sceneId, out int value) ? value : -1;
        }
    }
}
