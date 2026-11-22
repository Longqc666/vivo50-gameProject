using System.Collections.Generic;
using NUnit.Framework;
using D225.Core;

namespace D225.Core.Tests
{
    /// <summary>
    /// 流程状态机用例（对应 T10「暂停与场景」、异常清单「暂停仍被抓」「重复触发」）。
    /// 全部是纯 C#，不依赖场景。
    /// </summary>
    public sealed class GameStateMachineTests
    {
        private static GameStateMachine MachineAt(GameStateId state)
        {
            var machine = new GameStateMachine(state);
            return machine;
        }

        [Test]
        public void Init_CanEnterMainMenu()
        {
            var machine = MachineAt(GameStateId.Init);
            Assert.IsTrue(machine.TryEnter(GameStateId.MainMenu, out string reason), reason);
            Assert.AreEqual(GameStateId.MainMenu, machine.Current);
        }

        [Test]
        public void Init_CanEnterExploreForDirectScenePlay()
        {
            var machine = MachineAt(GameStateId.Init);
            Assert.IsTrue(machine.TryEnter(GameStateId.Explore, out _));
            Assert.IsTrue(machine.IsGameplayActive);
        }

        [Test]
        public void Explore_CanPauseAndResume()
        {
            var machine = MachineAt(GameStateId.Explore);

            Assert.IsTrue(machine.TryEnter(GameStateId.Paused, out _));
            Assert.IsFalse(machine.IsGameplayActive);

            Assert.IsTrue(machine.TryEnter(GameStateId.Explore, out _));
            Assert.IsTrue(machine.IsGameplayActive);
        }

        [Test]
        public void Paused_CannotFail_SoPlayerCannotBeCaughtWhilePaused()
        {
            var machine = MachineAt(GameStateId.Paused);

            Assert.IsFalse(machine.CanEnter(GameStateId.Failed));
            Assert.IsFalse(machine.TryEnter(GameStateId.Failed, out string reason));
            Assert.IsNotEmpty(reason);
            Assert.AreEqual(GameStateId.Paused, machine.Current);
        }

        [Test]
        public void Failed_CannotPause()
        {
            var machine = MachineAt(GameStateId.Failed);
            Assert.IsFalse(machine.TryEnter(GameStateId.Paused, out _));
            Assert.AreEqual(GameStateId.Failed, machine.Current);
        }

        [Test]
        public void Failed_CanRetryToExplore()
        {
            var machine = MachineAt(GameStateId.Failed);
            Assert.IsTrue(machine.TryEnter(GameStateId.Explore, out _));
        }

        [Test]
        public void Paused_CanReturnToMainMenu()
        {
            var machine = MachineAt(GameStateId.Paused);
            Assert.IsTrue(machine.TryEnter(GameStateId.MainMenu, out _));
        }

        [Test]
        public void Ending_CannotGoBackToExplore()
        {
            var machine = MachineAt(GameStateId.Ending);
            Assert.IsFalse(machine.TryEnter(GameStateId.Explore, out _));
            Assert.IsTrue(machine.TryEnter(GameStateId.MainMenu, out _));
        }

        [Test]
        public void DuplicateTransition_IsRejectedWithoutChangingState()
        {
            var machine = MachineAt(GameStateId.Paused);
            Assert.IsFalse(machine.TryEnter(GameStateId.Paused, out string reason));
            StringAssert.Contains("已经是当前状态", reason);
            Assert.AreEqual(GameStateId.Paused, machine.Current);
        }

        [Test]
        public void IllegalTransition_RaisesRejectedEvent()
        {
            var machine = MachineAt(GameStateId.MainMenu);
            var rejected = new List<GameStateId>();
            machine.Rejected += (target, reason) => rejected.Add(target);

            machine.TryEnter(GameStateId.Ending, out _);

            CollectionAssert.AreEqual(new[] { GameStateId.Ending }, rejected);
        }

        [Test]
        public void StateChanges_AreReportedInOrder()
        {
            var machine = MachineAt(GameStateId.Init);
            var changes = new List<string>();
            machine.Changed += (from, to) => changes.Add(from + ">" + to);

            machine.TryEnter(GameStateId.Explore, out _);
            machine.TryEnter(GameStateId.Paused, out _);
            machine.TryEnter(GameStateId.Explore, out _);
            machine.TryEnter(GameStateId.Failed, out _);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Init>Explore",
                    "Explore>Paused",
                    "Paused>Explore",
                    "Explore>Failed"
                },
                changes);
        }

        [Test]
        public void RejectedTransition_DoesNotRaiseChanged()
        {
            var machine = MachineAt(GameStateId.Paused);
            int changed = 0;
            machine.Changed += (_, __) => changed++;

            machine.TryEnter(GameStateId.Failed, out _);

            Assert.AreEqual(0, changed);
        }
    }
}
