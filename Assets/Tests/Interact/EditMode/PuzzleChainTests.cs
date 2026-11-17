using System.Collections.Generic;
using NUnit.Framework;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>
    /// 4-4 主线谜题链：手电→保险丝→配电箱→门禁→出口→撤离。
    /// 覆盖验收用例 T07（保险丝任务）、T08（门禁依赖）、T11（结局条件）。
    /// </summary>
    public sealed class PuzzleChainTests
    {
        private TestWorld world;
        private ItemData fuse;
        private ItemData key;
        private FuseBoxPuzzle fuseBox;
        private StairDoorPuzzle stairDoor;
        private ExitSwitchPuzzle exitSwitch;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            fuse = world.Item("item.fuse", "保险丝", ItemCategory.PuzzleItem, false, PuzzleFlag.FuseAcquired);
            key = world.Item("item.stair_key", "门禁钥匙", ItemCategory.Key, false, PuzzleFlag.StairKeyAcquired);

            fuseBox = world.Attach<FuseBoxPuzzle>("配电箱");
            fuseBox.SetDisplayName("配电箱");
            fuseBox.Configure(fuse.ItemId, fuse.DisplayName, PuzzleFlag.PowerRestored);

            stairDoor = world.Attach<StairDoorPuzzle>("楼梯门禁");
            stairDoor.SetDisplayName("楼梯门禁");
            stairDoor.Configure(key.ItemId, key.DisplayName, PuzzleFlag.StairDoorUnlocked);

            exitSwitch = world.Attach<ExitSwitchPuzzle>("出口联动装置");
            exitSwitch.SetDisplayName("出口联动装置");
            exitSwitch.Configure(PuzzleFlag.ExitUnlocked);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void T07_FuseBox_WithoutFuse_ReportsMissingMaterial()
        {
            Assert.AreEqual(InteractionState.Unavailable, fuseBox.EvaluateState(world.Context));
            StringAssert.Contains("缺少", fuseBox.GetPrompt(world.Context));
            StringAssert.Contains("保险丝", fuseBox.GetPrompt(world.Context));

            InteractionResult result = fuseBox.Interact(world.Context);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("保险丝", result.Message);
            Assert.IsFalse(world.Quest.HasFlag(PuzzleFlag.PowerRestored));
            Assert.IsFalse(fuseBox.IsSolved);
        }

        [Test]
        public void T07_FuseBox_WithFuse_RestoresPowerAndConsumesFuse()
        {
            world.Inventory.TryAdd(fuse, out _);

            Assert.AreEqual(InteractionState.Usable, fuseBox.EvaluateState(world.Context));
            Assert.AreEqual("[E] 使用 配电箱", fuseBox.GetPrompt(world.Context));

            InteractionResult result = fuseBox.Interact(world.Context);

            Assert.IsTrue(result.Succeeded, result.Message);
            StringAssert.Contains("灯", result.Message);
            Assert.IsTrue(world.Quest.HasFlag(PuzzleFlag.PowerRestored));
            Assert.IsFalse(world.Inventory.Has(fuse), "保险丝安装后应从背包扣除");
            Assert.IsTrue(fuseBox.IsSolved);
            StringAssert.DoesNotContain("[E]", fuseBox.GetPrompt(world.Context));
        }

        [Test]
        public void T07_FuseBox_AfterSolved_RejectsRepeatWithoutDuplicating()
        {
            world.Inventory.TryAdd(fuse, out _);
            fuseBox.Interact(world.Context);

            InteractionResult repeat = fuseBox.Interact(world.Context);

            Assert.IsFalse(repeat.Succeeded);
            StringAssert.Contains("处理", repeat.Message);
            Assert.AreEqual(1, world.Quest.Count, "已完成机关不应再写第二个标记");
        }

        [Test]
        public void T08_StairDoor_WithoutPower_ReportsNoPower()
        {
            StringAssert.Contains("没有电", stairDoor.GetPrompt(world.Context));

            Assert.IsFalse(stairDoor.Interact(world.Context).Succeeded);
            Assert.IsFalse(world.Quest.HasFlag(PuzzleFlag.StairDoorUnlocked));
        }

        [Test]
        public void T08_StairDoor_WithPowerButNoKey_ReportsMissingKey()
        {
            world.Quest.SetFlag(PuzzleFlag.PowerRestored);

            string prompt = stairDoor.GetPrompt(world.Context);
            StringAssert.Contains("门禁钥匙", prompt);

            Assert.IsFalse(stairDoor.Interact(world.Context).Succeeded);
            Assert.IsFalse(world.Quest.HasFlag(PuzzleFlag.StairDoorUnlocked));
        }

        [Test]
        public void T08_StairDoor_WithPowerAndKey_UnlocksAndKeepsKey()
        {
            world.Quest.SetFlag(PuzzleFlag.PowerRestored);
            world.Inventory.TryAdd(key, out _);

            Assert.AreEqual(InteractionState.Usable, stairDoor.EvaluateState(world.Context));
            InteractionResult result = stairDoor.Interact(world.Context);

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.IsTrue(world.Quest.HasFlag(PuzzleFlag.StairDoorUnlocked));
            Assert.IsTrue(world.Inventory.Has(key), "钥匙是主线进度物品，开门不消耗");
        }

        [Test]
        public void T11_ExitSwitch_BeforeStairDoor_DoesNotUnlockExit()
        {
            world.Quest.SetFlag(PuzzleFlag.PowerRestored);

            StringAssert.Contains("楼梯门", exitSwitch.GetPrompt(world.Context));
            Assert.IsFalse(exitSwitch.Interact(world.Context).Succeeded);
            Assert.IsFalse(world.Quest.HasFlag(PuzzleFlag.ExitUnlocked));
        }

        [Test]
        public void T11_ExitSwitch_AfterStairDoor_UnlocksExit()
        {
            world.Quest.SetFlag(PuzzleFlag.PowerRestored);
            world.Quest.SetFlag(PuzzleFlag.StairDoorUnlocked);

            InteractionResult result = exitSwitch.Interact(world.Context);

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.IsTrue(world.Quest.HasFlag(PuzzleFlag.ExitUnlocked));
        }

        [Test]
        public void T11_EvacuationZone_BeforeExitUnlocked_DoesNotComplete()
        {
            QuestLogProvider provider = world.CreateProvider();
            EvacuationZone zone = CreateEvacuationZone(provider, out List<string> feedback);

            Assert.IsFalse(zone.TryEvacuate());
            Assert.IsFalse(zone.HasEvacuated);
            Assert.IsFalse(provider.Log.HasFlag(PuzzleFlag.EvacuationComplete));
            Assert.AreEqual(1, feedback.Count);
            StringAssert.Contains("还锁着", feedback[0]);
        }

        [Test]
        public void T11_EvacuationZone_AfterExitUnlocked_CompletesOnlyOnce()
        {
            QuestLogProvider provider = world.CreateProvider();
            provider.Log.SetFlag(PuzzleFlag.ExitUnlocked);
            EvacuationZone zone = CreateEvacuationZone(provider, out List<string> feedback);

            Assert.IsTrue(zone.TryEvacuate());
            Assert.IsTrue(zone.HasEvacuated);
            Assert.IsTrue(provider.Log.HasFlag(PuzzleFlag.EvacuationComplete));
            Assert.AreEqual(1, feedback.Count);
            Assert.IsTrue(zone.TryEvacuate(), "重复进入应保持已完成，不报错");
            Assert.AreEqual(1, feedback.Count, "完成后不应重复播反馈");
        }

        [Test]
        public void FullChain_CompletesInDesignOrder()
        {
            QuestLogProvider provider = world.CreateProvider();
            InteractionContext context = new InteractionContext(world.Actor, world.Inventory, provider.Log);

            // 1. 空手操作配电箱：拒绝
            Assert.IsFalse(fuseBox.Interact(context).Succeeded);

            // 2. 找保险丝 → 配电箱：供电恢复
            world.Inventory.TryAdd(fuse, out _);
            Assert.IsTrue(fuseBox.Interact(context).Succeeded);
            Assert.IsTrue(provider.Log.HasFlag(PuzzleFlag.PowerRestored));

            // 3. 门禁无钥匙：拒绝
            Assert.IsFalse(stairDoor.Interact(context).Succeeded);

            // 4. 找钥匙 → 门禁：楼梯门开
            world.Inventory.TryAdd(key, out _);
            Assert.IsTrue(stairDoor.Interact(context).Succeeded);
            Assert.IsTrue(provider.Log.HasFlag(PuzzleFlag.StairDoorUnlocked));

            // 5. 出口装置：出口开启
            Assert.IsTrue(exitSwitch.Interact(context).Succeeded);
            Assert.IsTrue(provider.Log.HasFlag(PuzzleFlag.ExitUnlocked));

            // 6. 进入安全区：撤离完成
            EvacuationZone zone = CreateEvacuationZone(provider, out _);
            Assert.IsTrue(zone.TryEvacuate());
            Assert.IsTrue(provider.Log.HasFlag(PuzzleFlag.EvacuationComplete));
        }

        private EvacuationZone CreateEvacuationZone(QuestLogProvider provider, out List<string> feedback)
        {
            UnityEngine.GameObject zoneObject = world.CreateObject("出口触发区");
            zoneObject.AddComponent<UnityEngine.BoxCollider2D>();
            EvacuationZone zone = zoneObject.AddComponent<EvacuationZone>();
            zone.Configure(provider, "Player");
            List<string> messages = new List<string>();
            zone.Feedback += messages.Add;
            feedback = messages;
            return zone;
        }
    }
}
