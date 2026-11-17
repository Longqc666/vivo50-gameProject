using System;
using UnityEngine;
using UnityEngine.Events;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>
    /// 谜题机关公共基类：把「条件未满足的原因」「成功后写任务标记」「成功后表现」三件事固定下来。
    /// 子类只回答两个问题：现在还缺什么（<see cref="TryGetBlocker"/>）、成功后做什么（<see cref="Apply"/>）。
    /// </summary>
    public abstract class PuzzleGate : InteractableBase
    {
        [Header("谜题结果")]
        [Tooltip("完成时写入的任务标记。")]
        [SerializeField]
        protected PuzzleFlag flagToRaise = PuzzleFlag.None;

        [Tooltip("完成时的字幕/反馈。")]
        [TextArea(1, 3)]
        [SerializeField]
        protected string solvedMessage = "操作成功。";

        [Tooltip("完成后再按 E 的提示，避免玩家误以为没生效。")]
        [TextArea(1, 3)]
        [SerializeField]
        protected string completedMessage = "这里已经处理好了。";

        [Tooltip("完成表现：灯光、开门动画、音效，由成员3/5 接入。")]
        [SerializeField]
        protected UnityEvent onSolved;

        private bool solved;

        /// <summary>是否已完成；完成后不再接受操作。</summary>
        public bool IsSolved => solved;

        /// <summary>完成后写入的标记。</summary>
        public PuzzleFlag FlagToRaise => flagToRaise;

        /// <summary>完成事件（参数：写入的标记），供流程/音效侧订阅。</summary>
        public event Action<PuzzleFlag> Solved;

        protected override InteractionState EvaluateStateCore(InteractionContext context)
        {
            if (solved || TryGetBlocker(context, out _))
            {
                return InteractionState.Unavailable;
            }

            return InteractionState.Usable;
        }

        public override string BlockedReason(InteractionContext context)
        {
            if (!IsActive)
            {
                return base.BlockedReason(context);
            }

            if (solved)
            {
                return completedMessage;
            }

            return TryGetBlocker(context, out string reason) ? reason : base.BlockedReason(context);
        }

        protected override InteractionResult Execute(InteractionContext context)
        {
            if (solved)
            {
                return InteractionResult.Rejected(completedMessage);
            }

            if (TryGetBlocker(context, out string reason))
            {
                return InteractionResult.Rejected(reason);
            }

            InteractionResult applied = Apply(context);
            if (!applied.Succeeded)
            {
                return applied;
            }

            solved = true;

            if (flagToRaise != PuzzleFlag.None)
            {
                if (context.Quest != null)
                {
                    context.Quest.SetFlag(flagToRaise);
                }
                else
                {
                    Debug.LogWarning("[D225] 谜题「" + DisplayName + "」完成时没有任务进度来源，标记未写入。", this);
                }
            }

            onSolved?.Invoke();
            Solved?.Invoke(flagToRaise);

            string message = string.IsNullOrWhiteSpace(applied.Message) ? solvedMessage : applied.Message;
            return InteractionResult.Ok(message);
        }

        /// <summary>返回 true 表示当前不可用，<paramref name="reason"/> 是给玩家的具体原因。</summary>
        protected abstract bool TryGetBlocker(InteractionContext context, out string reason);

        /// <summary>执行谜题消耗/状态变更；返回成功后才写标记与播表现。</summary>
        protected abstract InteractionResult Apply(InteractionContext context);

        /// <summary>编辑器工具与自测使用：设置完成标记与文案。</summary>
        public void ConfigureResult(PuzzleFlag flag, string successText = null, string doneText = null)
        {
            flagToRaise = flag;
            if (!string.IsNullOrEmpty(successText))
            {
                solvedMessage = successText;
            }

            if (!string.IsNullOrEmpty(doneText))
            {
                completedMessage = doneText;
            }
        }

        /// <summary>测试/调试用：把机关恢复为未完成（正式流程中只有重开本局才需要）。</summary>
        public void ResetSolvedState()
        {
            solved = false;
        }
    }
}
