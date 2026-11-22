using NUnit.Framework;
using UnityEngine;
using D225.Core;
using D225.Interact;
using D225.Quest;

namespace D225.Core.Tests
{
    /// <summary>
    /// 检查点用例（分工表 6-4；策划案 5.6）：保存 SceneID/出生点/关键物品/QuestFlag，
    /// 重试时保留永久进度、重置临时状态。T09。
    /// </summary>
    public sealed class CheckpointTests
    {
        private GameObject inventoryObject;
        private Inventory inventory;
        private QuestLog log;
        private ItemData fuse;
        private ItemData note;

        [SetUp]
        public void SetUp()
        {
            inventoryObject = new GameObject("TestInventory");
            inventory = inventoryObject.AddComponent<Inventory>();

            fuse = ScriptableObject.CreateInstance<ItemData>();
            fuse.Configure("item.fuse", "保险丝", "测试用保险丝", ItemCategory.PuzzleItem);

            note = ScriptableObject.CreateInstance<ItemData>();
            note.Configure("item.note", "宿舍便笺", "测试用线索", ItemCategory.Clue);

            log = new QuestLog();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(inventoryObject);
            Object.DestroyImmediate(fuse);
            Object.DestroyImmediate(note);
        }

        [Test]
        public void Capture_SavesSceneSpawnItemsAndFlags()
        {
            inventory.TryAdd(fuse, out _);
            inventory.AddClue(note, out _);
            log.SetFlag(PuzzleFlag.FuseAcquired);

            CheckpointData data = CheckpointData.Capture("B", "CP_B_Mid", inventory, log);

            Assert.AreEqual("B", data.SceneId);
            Assert.AreEqual("CP_B_Mid", data.SpawnId);
            Assert.AreEqual(1, data.Items.Length);
            Assert.AreEqual("item.fuse", data.Items[0].ItemId);
            Assert.AreEqual(1, data.Clues.Length);
            CollectionAssert.AreEqual(new[] { PuzzleFlag.FuseAcquired }, data.Flags);
        }

        [Test]
        public void Capture_WithNullSources_DoesNotThrow()
        {
            CheckpointData data = CheckpointData.Capture("A", "CP_A_Spawn", null, null);

            Assert.AreEqual(0, data.Items.Length);
            Assert.AreEqual(0, data.Clues.Length);
            Assert.AreEqual(0, data.Flags.Length);
        }

        [Test]
        public void RestoreInventory_PutsItemsBackAfterSceneReload()
        {
            inventory.TryAdd(fuse, out _);
            log.SetFlag(PuzzleFlag.FuseAcquired);
            CheckpointData data = CheckpointData.Capture("B", "CP_B_Mid", inventory, log);

            // 重载场景：新的角色背包是空的
            inventory.Clear();
            Assert.IsFalse(inventory.Has("item.fuse"));

            int restored = data.RestoreInventory(inventory);

            Assert.AreEqual(1, restored);
            Assert.IsTrue(inventory.Has("item.fuse"));
        }

        [Test]
        public void RestoreInventory_IsIdempotent()
        {
            inventory.TryAdd(fuse, out _);
            CheckpointData data = CheckpointData.Capture("B", "CP_B_Mid", inventory, log);

            data.RestoreInventory(inventory);
            int restoredAgain = data.RestoreInventory(inventory);

            Assert.AreEqual(1, restoredAgain); // Clear 后重新放入，数量始终是 1
            Assert.AreEqual(1, inventory.Count("item.fuse"));
        }

        [Test]
        public void RestoreQuest_KeepsPermanentProgress()
        {
            log.SetFlag(PuzzleFlag.PowerRestored);
            log.SetFlag(PuzzleFlag.StairKeyAcquired);
            CheckpointData data = CheckpointData.Capture("B", "CP_B_Stair", inventory, log);

            var freshLog = new QuestLog();
            int added = data.RestoreQuest(freshLog);

            Assert.AreEqual(2, added);
            Assert.IsTrue(freshLog.HasFlag(PuzzleFlag.PowerRestored));
            Assert.IsTrue(freshLog.HasFlag(PuzzleFlag.StairKeyAcquired));
        }

        [Test]
        public void Book_Save_AdvancesRevisionAndFiresEvent()
        {
            var book = new CheckpointBook();
            CheckpointData saved = null;
            book.Saved += data => saved = data;

            CheckpointData first = book.Save("A", "CP_A_Spawn", inventory, log, 0);
            CheckpointData second = book.Save("A", "CP_A_Door", inventory, log, 1);

            Assert.AreEqual(1, first.Revision);
            Assert.AreEqual(2, second.Revision);
            Assert.AreSame(second, book.Current);
            Assert.AreSame(second, saved);
        }

        [Test]
        public void Book_ProgressNeverGoesBackwards()
        {
            var book = new CheckpointBook();

            Assert.IsTrue(book.Advance("B", "CP_B_Mid", 2));
            Assert.IsFalse(book.Advance("B", "CP_B_Entry", 0));
            Assert.AreEqual(2, book.ProgressOf("B"));
        }

        [Test]
        public void Book_Clear_ResetsEverything()
        {
            var book = new CheckpointBook();
            book.Save("B", "CP_B_Mid", inventory, log, 2);

            book.Clear();

            Assert.IsNull(book.Current);
            Assert.AreEqual(0, book.Count);
            Assert.AreEqual(-1, book.ProgressOf("B"));
        }

        [Test]
        public void Data_ToString_ContainsSceneAndSpawn()
        {
            CheckpointData data = CheckpointData.Capture("C", "CP_C_Hall", inventory, log);
            StringAssert.Contains("C/CP_C_Hall", data.ToString());
        }
    }
}
