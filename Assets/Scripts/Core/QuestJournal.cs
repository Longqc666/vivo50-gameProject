using System;
using System.Collections.Generic;
using D225.Quest;

namespace D225.Core
{
    /// <summary>
    /// 任务进度门面：把 <see cref="IQuestLog"/> 包装成流程模块唯一的进度来源。
    /// 不新增第二套判定——谜题侧照旧读写 <see cref="IQuestLog"/>，本类只负责转发、快照与恢复。
    /// </summary>
    /// <remarks>
    /// 与成员4 的接口约定（docs/交互接口说明.md 第 7 节）：
    /// 标记是「永久进度」，检查点重试不回滚，所以 <see cref="Restore"/> 只补写、不清空。
    /// </remarks>
    public sealed class QuestJournal : IQuestLog, IDisposable
    {
        private readonly IQuestLog source;
        private bool disposed;

        public QuestJournal(IQuestLog source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.source.FlagRaised += Relay;
        }

        /// <summary>用场景里的来源（QuestLogProvider，找不到时为临时日志）构造。</summary>
        public static QuestJournal ForCurrentScene()
        {
            return new QuestJournal(QuestLogLocator.Resolve());
        }

        /// <summary>某个标记首次写入时触发（与来源同步）。</summary>
        public event Action<PuzzleFlag> FlagRaised;

        public IReadOnlyCollection<PuzzleFlag> RaisedFlags => source.RaisedFlags;

        public bool HasFlag(PuzzleFlag flag) => source.HasFlag(flag);

        public bool SetFlag(PuzzleFlag flag) => source.SetFlag(flag);

        /// <summary>已写入标记数量（日志与自测用）。</summary>
        public int Count => source.RaisedFlags.Count;

        /// <summary>当前进度的快照，按枚举值排序，便于比对。</summary>
        public PuzzleFlag[] Snapshot()
        {
            var list = new List<PuzzleFlag>(source.RaisedFlags);
            list.Sort();
            return list.ToArray();
        }

        /// <summary>把快照写回当前来源；返回本次新写入的数量。已有标记不会重复触发事件。</summary>
        public int Restore(IEnumerable<PuzzleFlag> flags)
        {
            if (flags == null)
            {
                return 0;
            }

            int added = 0;
            foreach (PuzzleFlag flag in flags)
            {
                if (flag == PuzzleFlag.None)
                {
                    continue;
                }

                if (source.SetFlag(flag))
                {
                    added++;
                }
            }

            return added;
        }

        /// <summary>主线是否走到「已撤离」（结算条件，供 GameManager 订阅）。</summary>
        public bool IsEvacuated => HasFlag(PuzzleFlag.EvacuationComplete);

        /// <summary>
        /// 清空全部标记（新开一局用）。只对具体实现 <see cref="QuestLog"/> 生效；
        /// 来源是别的实现时返回 0，避免误清别人的进度。
        /// </summary>
        public int Clear()
        {
            if (!(source is QuestLog concrete))
            {
                return 0;
            }

            int count = concrete.Count;
            concrete.Clear();
            return count;
        }

        private void Relay(PuzzleFlag flag)
        {
            if (disposed)
            {
                return;
            }

            FlagRaised?.Invoke(flag);
        }

        /// <summary>退订来源事件。GameManager 销毁时调用，避免跨局残留。</summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            source.FlagRaised -= Relay;
            FlagRaised = null;
        }
    }
}
