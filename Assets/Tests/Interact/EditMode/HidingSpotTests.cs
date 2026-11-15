using NUnit.Framework;
using UnityEngine;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>4-5 躲藏点：进出、占用、抓捕锁定、首次躲藏标记（T06）。</summary>
    public sealed class HidingSpotTests
    {
        private TestWorld world;
        private HidingSpot spot;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            spot = world.Attach<HidingSpot>("储物柜");
            spot.SetDisplayName("储物柜");
            spot.Configure(null, false, PuzzleFlag.FirstHiding);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void EnterAndExit_TogglesOccupancyAndCallsActor()
        {
            GameObject actor = world.AttachHider(out FakeHider hider);
            InteractionContext context = new InteractionContext(actor, world.Inventory, world.Quest);

            Assert.AreEqual("[E] 躲进 储物柜", spot.GetPrompt(context));

            InteractionResult enter = spot.Interact(context);
            Assert.IsTrue(enter.Succeeded, enter.Message);
            Assert.IsTrue(hider.IsHidden);
            Assert.AreEqual(1, hider.EnterCount);
            Assert.IsTrue(spot.IsOccupied);
            Assert.IsTrue(world.Quest.HasFlag(PuzzleFlag.FirstHiding));
            Assert.AreEqual("[E] 离开 储物柜", spot.GetPrompt(context));

            InteractionResult exit = spot.Interact(context);
            Assert.IsTrue(exit.Succeeded, exit.Message);
            Assert.IsFalse(hider.IsHidden);
            Assert.AreEqual(1, hider.ExitCount);
            Assert.IsFalse(spot.IsOccupied);
        }

        [Test]
        public void OccupiedSpot_RejectsSecondActor()
        {
            GameObject first = world.AttachHider(out FakeHider firstHider, "First");
            GameObject second = world.AttachHider(out FakeHider secondHider, "Second");
            spot.Interact(new InteractionContext(first, world.Inventory, world.Quest));

            InteractionContext secondContext = new InteractionContext(second, world.Inventory, world.Quest);

            Assert.AreEqual(InteractionState.Unavailable, spot.EvaluateState(secondContext));
            StringAssert.Contains("已经有人", spot.GetPrompt(secondContext));
            Assert.IsFalse(spot.Interact(secondContext).Succeeded);
            Assert.IsFalse(secondHider.IsHidden);
            Assert.IsTrue(firstHider.IsHidden);
        }

        [Test]
        public void CatchLocked_ActorCannotHideToAvoidCapture()
        {
            GameObject actor = world.AttachHider(out FakeHider hider);
            hider.IsCatchLocked = true;
            InteractionContext context = new InteractionContext(actor, world.Inventory, world.Quest);

            Assert.AreEqual(InteractionState.Unavailable, spot.EvaluateState(context));
            StringAssert.Contains("抓住", spot.GetPrompt(context));

            InteractionResult result = spot.Interact(context);

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(hider.IsHidden);
            Assert.AreEqual(0, hider.EnterCount);
        }

        [Test]
        public void LockedSpot_RejectsEvenWithoutOccupant()
        {
            spot.Configure(null, true);
            GameObject actor = world.AttachHider(out FakeHider hider);
            InteractionContext context = new InteractionContext(actor, world.Inventory, world.Quest);

            StringAssert.Contains("卡住", spot.GetPrompt(context));
            Assert.IsFalse(spot.Interact(context).Succeeded);
            Assert.IsFalse(hider.IsHidden);
        }

        [Test]
        public void ForceRelease_ClearsOccupantForCheckpointReset()
        {
            GameObject actor = world.AttachHider(out FakeHider hider);
            spot.Interact(new InteractionContext(actor, world.Inventory, world.Quest));

            spot.ForceRelease();

            Assert.IsFalse(spot.IsOccupied);
            Assert.IsFalse(hider.IsHidden);
            Assert.AreEqual(1, hider.ExitCount);
        }

        [Test]
        public void HidingSpot_WithoutHidableActor_IsRejectedWithReason()
        {
            GameObject plainActor = world.CreateObject("PlainPlayer");
            InteractionContext context = new InteractionContext(plainActor, world.Inventory, world.Quest);

            Assert.AreEqual(InteractionState.Unavailable, spot.EvaluateState(context));
            Assert.IsFalse(spot.Interact(context).Succeeded);
        }
    }
}
