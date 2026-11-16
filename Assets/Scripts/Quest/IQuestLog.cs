using System;
using System.Collections.Generic;

namespace D225.Quest
{
    /// <summary>
    /// 任务进度查询与写入的最小契约（成员4 谜题模块与成员6 流程模块之间的对接点）。
    /// 交互物只做「读标记 / 写标记」，不直接改 UI；UI 与 HUD 只订阅 <see cref="FlagRaised"/>。
    /// </summary>
    public interface IQuestLog
    {
        /// <summary>某个标记首次被写入时触发；重复写入不再触发。</summary>
        event Action<PuzzleFlag> FlagRaised;

        /// <summary>当前已写入的标记（只读）。</summary>
        IReadOnlyCollection<PuzzleFlag> RaisedFlags { get; }

        /// <summary>查询标记是否已写入；<see cref="PuzzleFlag.None"/> 一律视为已满足。</summary>
        bool HasFlag(PuzzleFlag flag);

        /// <summary>写入标记；首次写入返回 true 并触发 <see cref="FlagRaised"/>。</summary>
        bool SetFlag(PuzzleFlag flag);
    }
}
