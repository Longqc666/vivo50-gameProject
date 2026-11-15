using System;
using UnityEngine;

namespace D225.Quest
{
    /// <summary>一条任务目标：标记完成后目标推进到下一条。</summary>
    [Serializable]
    public struct ObjectiveStep
    {
        [Tooltip("该标记写入后，本条目标视为完成。")]
        public PuzzleFlag completedFlag;

        [Tooltip("未完成时显示在任务栏的目标文本。")]
        [TextArea(1, 3)]
        public string objective;
    }

    /// <summary>
    /// 根据 <see cref="IQuestLog"/> 的标记推算当前任务目标文本，供成员5 的 HUD 直接绑定。
    /// 只读不判定：谜题是否可交互由交互物自己算，本类只把「下一步做什么」翻译成一句话。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/任务目标跟踪 (PuzzleObjectiveTracker)")]
    public sealed class PuzzleObjectiveTracker : MonoBehaviour
    {
        [Tooltip("任务进度来源；留空时自动查找场景中的 QuestLogProvider。")]
        [SerializeField]
        private QuestLogProvider provider;

        [Tooltip("按顺序排列的目标；第一条未完成的会作为当前目标。")]
        [SerializeField]
        private ObjectiveStep[] steps = new ObjectiveStep[0];

        private IQuestLog subscribed;

        /// <summary>当前目标文本变化时触发（HUD/字幕订阅）。</summary>
        public event Action<string> ObjectiveChanged;

        /// <summary>当前目标文本；全部完成时为空字符串。</summary>
        public string CurrentObjective { get; private set; } = string.Empty;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Start()
        {
            if (provider == null)
            {
                provider = FindObjectOfType<QuestLogProvider>(true);
            }

            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private IQuestLog ResolveLog()
        {
            if (provider != null)
            {
                return provider.Log;
            }

            return QuestLogLocator.Resolve();
        }

        private void Subscribe()
        {
            IQuestLog log = ResolveLog();
            if (log == null || ReferenceEquals(log, subscribed))
            {
                return;
            }

            Unsubscribe();
            subscribed = log;
            subscribed.FlagRaised += HandleFlagRaised;
        }

        private void Unsubscribe()
        {
            if (subscribed == null)
            {
                return;
            }

            subscribed.FlagRaised -= HandleFlagRaised;
            subscribed = null;
        }

        private void HandleFlagRaised(PuzzleFlag flag)
        {
            Refresh();
        }

        /// <summary>重新计算目标文本，变化时通知订阅者。</summary>
        public void Refresh()
        {
            string next = Evaluate(ResolveLog());
            if (string.Equals(next, CurrentObjective, StringComparison.Ordinal))
            {
                return;
            }

            CurrentObjective = next;
            ObjectiveChanged?.Invoke(next);
        }

        /// <summary>纯函数版：给定进度返回当前目标，便于单元测试。</summary>
        public string Evaluate(IQuestLog log)
        {
            if (steps == null || log == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < steps.Length; i++)
            {
                ObjectiveStep step = steps[i];
                if (!log.HasFlag(step.completedFlag))
                {
                    return step.objective ?? string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>编辑器工具与自测使用：配置目标链。</summary>
        public void Configure(QuestLogProvider questProvider, ObjectiveStep[] objectiveSteps)
        {
            provider = questProvider;
            steps = objectiveSteps ?? new ObjectiveStep[0];
            subscribed = null;
            CurrentObjective = string.Empty;
        }
    }
}
