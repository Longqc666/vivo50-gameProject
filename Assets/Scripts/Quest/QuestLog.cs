using System;
using System.Collections.Generic;

namespace D225.Quest
{
    /// <summary>
    /// <see cref="IQuestLog"/> 的纯 C# 实现：不依赖场景对象，便于单元测试与调试。
    /// 正式流程中由成员6 的 QuestManager 持有（或替换为它的实现），谜题侧不直接持有本类。
    /// </summary>
    public sealed class QuestLog : IQuestLog
    {
        private readonly HashSet<PuzzleFlag> raised = new HashSet<PuzzleFlag>();

        public event Action<PuzzleFlag> FlagRaised;

        public IReadOnlyCollection<PuzzleFlag> RaisedFlags => raised;

        public int Count => raised.Count;

        public bool HasFlag(PuzzleFlag flag)
        {
            if (flag == PuzzleFlag.None)
            {
                return true;
            }

            return raised.Contains(flag);
        }

        public bool SetFlag(PuzzleFlag flag)
        {
            if (flag == PuzzleFlag.None || !raised.Add(flag))
            {
                return false;
            }

            FlagRaised?.Invoke(flag);
            return true;
        }

        /// <summary>清空全部标记（重开本局或测试之间复用实例时使用，不触发事件）。</summary>
        public void Clear()
        {
            raised.Clear();
        }
    }
}
