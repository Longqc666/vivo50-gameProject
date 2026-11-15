using NUnit.Framework;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>4-3 可调查线索：进线索页、可反复查看、任务标记（T03 线索部分）。</summary>
    public sealed class InspectableClueTests
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
        public void Inspect_RecordsClueAndWritesFlag()
        {
            ItemData note = world.Clue("clue.dorm_note", "宿舍便笺", PuzzleFlag.DutyNoteRead);
            InspectableClue clue = world.Attach<InspectableClue>("便笺");
            clue.Configure(note);

            Assert.AreEqual(InteractionState.Inspectable, clue.EvaluateState(world.Context));
            Assert.AreEqual("[E] 查看 宿舍便笺", clue.GetPrompt(world.Context));

            InteractionResult result = clue.Interact(world.Context);

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.IsTrue(world.Inventory.HasClue("clue.dorm_note"));
            Assert.IsTrue(world.Quest.HasFlag(PuzzleFlag.DutyNoteRead));
            StringAssert.Contains("测试用线索文本", result.Message);
        }

        [Test]
        public void Inspect_SecondTime_IsAllowedButDoesNotDuplicateClue()
        {
            ItemData note = world.Clue("clue.dorm_note", "宿舍便笺", PuzzleFlag.DutyNoteRead);
            InspectableClue clue = world.Attach<InspectableClue>("便笺");
            clue.Configure(note);

            clue.Interact(world.Context);
            InteractionResult second = clue.Interact(world.Context);

            Assert.IsTrue(second.Succeeded, "线索应该可以反复查看");
            Assert.AreEqual(1, world.Inventory.Clues.Count);
            StringAssert.Contains("已经记", second.Message);
        }

        [Test]
        public void Inspect_WithoutFlag_DoesNotWriteAnyQuestFlag()
        {
            ItemData log = world.Clue("clue.shift_log", "值班记录");
            InspectableClue clue = world.Attach<InspectableClue>("值班记录");
            clue.Configure(log);

            clue.Interact(world.Context);

            Assert.AreEqual(0, world.Quest.Count);
            Assert.IsTrue(world.Inventory.HasClue("clue.shift_log"));
        }
    }
}
