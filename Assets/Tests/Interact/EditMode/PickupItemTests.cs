using NUnit.Framework;
using UnityEngine;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>4-2/4-3 拾取交互：四态、提示文本、只拾取一次、任务标记（T03）。</summary>
    public sealed class PickupItemTests
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

        private PickupItem CreatePickup(ItemData item)
        {
            PickupItem pickup = world.Attach<PickupItem>("手电筒");
            pickup.Configure(item);
            return pickup;
        }

        [Test]
        public void Interact_FirstTime_PicksUpOnceAndWritesFlag()
        {
            ItemData flashlight = world.Item(
                "item.flashlight", "手电筒", ItemCategory.Tool, false, PuzzleFlag.FlashlightAcquired);
            PickupItem pickup = CreatePickup(flashlight);

            Assert.AreEqual(InteractionState.Pickupable, pickup.EvaluateState(world.Context));
            Assert.AreEqual("[E] 拾取 手电筒", pickup.GetPrompt(world.Context));

            InteractionResult result = pickup.Interact(world.Context);

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.IsTrue(world.Inventory.Has(flashlight));
            Assert.IsTrue(world.Quest.HasFlag(PuzzleFlag.FlashlightAcquired));
            Assert.IsFalse(pickup.IsActive, "拾取后必须退出交互检测");
            Assert.AreEqual(1, world.Inventory.Count("item.flashlight"));
        }

        [Test]
        public void Interact_SecondTime_IsRejectedAndDoesNotDuplicate()
        {
            ItemData flashlight = world.Item(
                "item.flashlight", "手电筒", ItemCategory.Tool, false, PuzzleFlag.FlashlightAcquired);
            PickupItem pickup = CreatePickup(flashlight);
            pickup.Interact(world.Context);

            InteractionResult second = pickup.Interact(world.Context);

            Assert.IsFalse(second.Succeeded);
            Assert.IsTrue(second.HasMessage, "重复按 E 也要给玩家反馈");
            Assert.AreEqual(1, world.Inventory.Count("item.flashlight"));
            Assert.AreEqual(InteractionState.Unavailable, pickup.EvaluateState(world.Context));
            StringAssert.DoesNotContain("[E]", pickup.GetPrompt(world.Context));
        }

        [Test]
        public void Interact_WithoutInventory_IsRejected()
        {
            ItemData flashlight = world.Item("item.flashlight", "手电筒");
            GameObject bareActor = world.CreateObject("NoInventory");
            PickupItem pickup = bareActor.AddComponent<PickupItem>();
            pickup.Configure(flashlight);

            InteractionContext context = new InteractionContext(bareActor, null, world.Quest);
            InteractionResult result = pickup.Interact(context);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("背包", result.Message);
        }

        [Test]
        public void EvaluateState_IsSideEffectFree()
        {
            ItemData flashlight = world.Item("item.flashlight", "手电筒");
            PickupItem pickup = CreatePickup(flashlight);

            pickup.EvaluateState(world.Context);
            pickup.GetPrompt(world.Context);

            Assert.IsFalse(pickup.HasBeenPicked);
            Assert.IsFalse(world.Inventory.Has(flashlight));
        }
    }
}
