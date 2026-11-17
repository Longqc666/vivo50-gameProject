using UnityEngine;

namespace D225.Quest
{
    /// <summary>
    /// 场景内 <see cref="IQuestLog"/> 的解析入口。优先用 <see cref="QuestLogProvider"/>；
    /// 场景中没有提供者时退回一个进程内临时日志并只警告一次，保证原型场景与自测可以直接跑，
    /// 正式流程仍需由成员6 的 GameManager 提供来源。
    /// </summary>
    public static class QuestLogLocator
    {
        private static readonly QuestLog FallbackLog = new QuestLog();
        private static QuestLogProvider cachedProvider;
        private static bool warnedMissingProvider;

        /// <summary>原型期兜底日志；测试可清空它避免用例互相污染。</summary>
        public static QuestLog Fallback => FallbackLog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewPlaySession()
        {
            cachedProvider = null;
            warnedMissingProvider = false;
            FallbackLog.Clear();
        }

        /// <summary>解析当前场景的任务进度来源；永不为 null。</summary>
        public static IQuestLog Resolve()
        {
            if (cachedProvider != null)
            {
                return cachedProvider.Log;
            }

            cachedProvider = Object.FindObjectOfType<QuestLogProvider>(true);
            if (cachedProvider != null)
            {
                return cachedProvider.Log;
            }

            if (!warnedMissingProvider)
            {
                warnedMissingProvider = true;
                Debug.LogWarning(
                    "[D225] 场景中没有 QuestLogProvider，交互将使用临时任务日志。" +
                    "正式流程请由成员6 的 GameManager 提供 IQuestLog。");
            }

            return FallbackLog;
        }

        /// <summary>测试与场景切换后调用，丢弃缓存。</summary>
        public static void InvalidateCache()
        {
            cachedProvider = null;
        }
    }
}
