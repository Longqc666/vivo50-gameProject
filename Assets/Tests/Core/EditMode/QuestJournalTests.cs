using System.Collections.Generic;
using NUnit.Framework;
using D225.Core;
using D225.Quest;

namespace D225.Core.Tests
{
    /// <summary>
    /// 任务状态用例（分工表 6-2；接口来自成员4 的 IQuestLog/PuzzleFlag）。
    /// 重点：标记只写一次、永久进度不回滚、恢复只补写不清空。
    /// </summary>
    public sealed class QuestJournalTests
    {
        private QuestLog log;
        private QuestJournal journal;

        [SetUp]
        public void SetUp()
        {
            log = new QuestLog();
            journal = new QuestJournal(log);
        }

        [TearDown]
        public void TearDown()
        {
            journal.Dispose();
        }

        [Test]
        public void SetFlag_WritesThroughToSource()
        {
            Assert.IsTrue(journal.SetFlag(PuzzleFlag.FlashlightAcquired));
            Assert.IsTrue(log.HasFlag(PuzzleFlag.FlashlightAcquired));
        }

        [Test]
        public void SetFlag_Twice_RaisesEventOnce()
        {
            int raised = 0;
            journal.FlagRaised += _ => raised++;

            journal.SetFlag(PuzzleFlag.FuseAcquired);
            journal.SetFlag(PuzzleFlag.FuseAcquired);

            Assert.AreEqual(1, raised);
            Assert.AreEqual(1, journal.Count);
        }

        [Test]
        public void None_IsAlwaysSatisfied()
        {
            Assert.IsTrue(journal.HasFlag(PuzzleFlag.None));
            Assert.IsFalse(journal.SetFlag(PuzzleFlag.None));
        }

        [Test]
        public void Snapshot_IsSortedAndIndependentOfLaterWrites()
        {
            journal.SetFlag(PuzzleFlag.StairDoorUnlocked);
            journal.SetFlag(PuzzleFlag.FlashlightAcquired);

            PuzzleFlag[] snapshot = journal.Snapshot();
            CollectionAssert.AreEqual(
                new[] { PuzzleFlag.FlashlightAcquired, PuzzleFlag.StairDoorUnlocked },
                snapshot);

            journal.SetFlag(PuzzleFlag.ExitUnlocked);
            Assert.AreEqual(2, snapshot.Length);
        }

        [Test]
        public void Restore_WritesPermanentProgressIntoNewJournal()
        {
            journal.SetFlag(PuzzleFlag.PowerRestored);
            PuzzleFlag[] saved = journal.Snapshot();

            // 模拟换区后新的任务日志（每个场景一个 QuestLogProvider）
            var freshLog = new QuestLog();
            var freshJournal = new QuestJournal(freshLog);

            int added = freshJournal.Restore(saved);

            Assert.AreEqual(1, added);
            Assert.IsTrue(freshJournal.HasFlag(PuzzleFlag.PowerRestored));
            freshJournal.Dispose();
        }

        [Test]
        public void Restore_DoesNotClearExistingFlags()
        {
            journal.SetFlag(PuzzleFlag.StairKeyAcquired);

            int added = journal.Restore(new[] { PuzzleFlag.FlashlightAcquired });

            Assert.AreEqual(1, added);
            Assert.IsTrue(journal.HasFlag(PuzzleFlag.StairKeyAcquired));
            Assert.IsTrue(journal.HasFlag(PuzzleFlag.FlashlightAcquired));
        }

        [Test]
        public void Restore_RepeatedFlags_AddsNothing()
        {
            journal.SetFlag(PuzzleFlag.ExitUnlocked);
            Assert.AreEqual(0, journal.Restore(new[] { PuzzleFlag.ExitUnlocked }));
        }

        [Test]
        public void Clear_EmptiesProgressForNewSession()
        {
            journal.SetFlag(PuzzleFlag.FuseAcquired);
            journal.SetFlag(PuzzleFlag.PowerRestored);

            int cleared = journal.Clear();

            Assert.AreEqual(2, cleared);
            Assert.AreEqual(0, journal.Count);
            Assert.IsFalse(journal.HasFlag(PuzzleFlag.FuseAcquired));
        }

        [Test]
        public void EvacuationFlag_DrivesEndingCondition()
        {
            Assert.IsFalse(journal.IsEvacuated);
            journal.SetFlag(PuzzleFlag.EvacuationComplete);
            Assert.IsTrue(journal.IsEvacuated);
        }

        [Test]
        public void Dispose_StopsReceivingSourceEvents()
        {
            int raised = 0;
            journal.FlagRaised += _ => raised++;
            journal.Dispose();

            log.SetFlag(PuzzleFlag.BulletinRead);

            Assert.AreEqual(0, raised);
        }
    }
}
