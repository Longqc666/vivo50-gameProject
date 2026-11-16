using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>4-2 拾取与背包：去重、消耗、线索页（T03 的数据层）。</summary>
    public sealed class InventoryTests
    {
        private TestWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void TryAdd_MainItem_RejectsDuplicateAndRaisesEventOnce()
        {
            ItemData fuse = world.Item("item.fuse", "保险丝");
            int addedEvents = 0;
            world.Inventory.ItemAdded += (item, count) => addedEvents++;

            Assert.IsTrue(world.Inventory.TryAdd(fuse, out string firstReason), "首次拾取应成功：" + firstReason);
            Assert.IsTrue(world.Inventory.Has("item.fuse"));
            Assert.AreEqual(1, world.Inventory.Count("item.fuse"));
            Assert.AreEqual(1, addedEvents);

            Assert.IsFalse(world.Inventory.TryAdd(fuse, out string duplicateReason));
            StringAssert.Contains("已经有", duplicateReason);
            Assert.AreEqual(1, world.Inventory.Count("item.fuse"), "重复拾取不能叠加");
            Assert.AreEqual(1, addedEvents, "重复拾取不能再次通知背包变化");
        }

        [Test]
        public void TryAdd_StackableItem_AccumulatesCountWithoutDuplicateEntries()
        {
            ItemData battery = world.Item("item.battery", "电池", ItemCategory.PuzzleItem, stackable: true);

            Assert.IsTrue(world.Inventory.TryAdd(battery, out _));
            Assert.IsTrue(world.Inventory.TryAdd(battery, out _));

            Assert.AreEqual(2, world.Inventory.Count("item.battery"));
            Assert.AreEqual(1, world.Inventory.Items.Count, "可堆叠物品在背包里只占一格");
        }

        [Test]
        public void TryConsume_RemovesItemAndRaisesRemovedEvent()
        {
            ItemData fuse = world.Item("item.fuse", "保险丝");
            world.Inventory.TryAdd(fuse, out _);

            int remaining = -1;
            world.Inventory.ItemRemoved += (item, count) => remaining = count;

            Assert.IsTrue(world.Inventory.TryConsume("item.fuse"));
            Assert.IsFalse(world.Inventory.Has(fuse));
            Assert.AreEqual(0, remaining);
            Assert.AreEqual(0, world.Inventory.Items.Count);
        }

        [Test]
        public void TryConsume_WhenNotEnough_KeepsInventoryUntouched()
        {
            ItemData fuse = world.Item("item.fuse", "保险丝");

            Assert.IsFalse(world.Inventory.TryConsume("item.fuse"));
            world.Inventory.TryAdd(fuse, out _);
            Assert.IsFalse(world.Inventory.TryConsume("item.fuse", 2));
            Assert.IsTrue(world.Inventory.Has(fuse));
        }

        [Test]
        public void AddClue_RecordsOnceAndRaisesClueEventOnce()
        {
            ItemData note = world.Clue("clue.note", "宿舍便笺", PuzzleFlag.DutyNoteRead);
            int clueEvents = 0;
            world.Inventory.ClueAdded += _ => clueEvents++;

            Assert.IsTrue(world.Inventory.AddClue(note, out _));
            Assert.IsTrue(world.Inventory.HasClue("clue.note"));
            Assert.AreEqual(1, world.Inventory.Clues.Count);

            Assert.IsFalse(world.Inventory.AddClue(note, out string duplicateReason));
            StringAssert.Contains("已经记下", duplicateReason);
            Assert.AreEqual(1, world.Inventory.Clues.Count);
            Assert.AreEqual(1, clueEvents);
        }

        [Test]
        public void TryAdd_NullItem_LogsErrorAndReturnsReason()
        {
            LogAssert.Expect(LogType.Error, new Regex("空的 ItemData"));

            Assert.IsFalse(world.Inventory.TryAdd(null, out string reason));
            Assert.AreEqual("物品数据缺失", reason);
        }
    }
}
