using UnityEngine;

namespace D225.Quest
{
    /// <summary>
    /// 把 <see cref="QuestLog"/> 挂到场景里的轻量提供者：初始化标记、给交互与 UI 提供 <see cref="IQuestLog"/>。
    /// 正式流程中应挂在成员6 的 GameManager 上，或由 GameManager 直接实现 <see cref="IQuestLog"/>；
    /// 只有一个场景物件时交互物会通过 <see cref="QuestLogLocator"/> 自动找到。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/任务进度提供者 (QuestLogProvider)")]
    public sealed class QuestLogProvider : MonoBehaviour
    {
        [Tooltip("进入场景时就视为已完成的标记；留空表示从零开始。")]
        [SerializeField]
        private PuzzleFlag[] startingFlags = new PuzzleFlag[0];

        private QuestLog log;

        /// <summary>对外只暴露接口，避免 UI 直接依赖实现类。</summary>
        public IQuestLog Log => EnsureLog();

        /// <summary>调试用：读取已写入标记数量。</summary>
        public int RaisedCount => EnsureLog().Count;

        private QuestLog EnsureLog()
        {
            if (log == null)
            {
                log = new QuestLog();
                ApplyStartingFlags();
            }

            return log;
        }

        private void Awake()
        {
            EnsureLog();
        }

        private void OnDestroy()
        {
            log = null;
        }

        private void ApplyStartingFlags()
        {
            if (startingFlags == null)
            {
                return;
            }

            for (int i = 0; i < startingFlags.Length; i++)
            {
                log.SetFlag(startingFlags[i]);
            }
        }

        /// <summary>编辑器工具与自测使用：配置初始标记。</summary>
        public void Configure(PuzzleFlag[] flags)
        {
            startingFlags = flags ?? new PuzzleFlag[0];
            log = null;
        }
    }
}
