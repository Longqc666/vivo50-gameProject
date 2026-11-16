using NUnit.Framework;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>4-4 任务目标：按标记顺序推算当前目标，供 HUD 绑定。</summary>
    public sealed class PuzzleObjectiveTrackerTests
    {
        private TestWorld world;
        private PuzzleObjectiveTracker tracker;

        [SetUp]
        public void SetUp()
        {
            world = new TestWorld();
            tracker = world.Attach<PuzzleObjectiveTracker>("Objectives");
            tracker.Configure(
                null,
                new[]
                {
                    new ObjectiveStep { completedFlag = PuzzleFlag.FlashlightAcquired, objective = "找到手电筒" },
                    new ObjectiveStep { completedFlag = PuzzleFlag.PowerRestored, objective = "恢复供电" },
                    new ObjectiveStep { completedFlag = PuzzleFlag.ExitUnlocked, objective = "启动出口联动装置" }
                });
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void Evaluate_ReturnsFirstUnfinishedObjective()
        {
            Assert.AreEqual("找到手电筒", tracker.Evaluate(world.Quest));

            world.Quest.SetFlag(PuzzleFlag.FlashlightAcquired);
            Assert.AreEqual("恢复供电", tracker.Evaluate(world.Quest));

            world.Quest.SetFlag(PuzzleFlag.PowerRestored);
            Assert.AreEqual("启动出口联动装置", tracker.Evaluate(world.Quest));
        }

        [Test]
        public void Evaluate_AllDone_ReturnsEmpty()
        {
            world.Quest.SetFlag(PuzzleFlag.FlashlightAcquired);
            world.Quest.SetFlag(PuzzleFlag.PowerRestored);
            world.Quest.SetFlag(PuzzleFlag.ExitUnlocked);

            Assert.AreEqual(string.Empty, tracker.Evaluate(world.Quest));
        }

        [Test]
        public void Evaluate_NullLog_ReturnsEmptyInsteadOfThrowing()
        {
            Assert.DoesNotThrow(() => Assert.AreEqual(string.Empty, tracker.Evaluate(null)));
        }
    }
}
