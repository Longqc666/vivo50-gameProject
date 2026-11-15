using System;
using UnityEngine;
using UnityEngine.Events;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>
    /// 楼外安全区/出口触发区（T11 后半）：出口未开启时进入只给提示、不通关；
    /// 出口联动装置已启动后才置「撤离完成」并触发结算表现。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("D225/谜题/出口触发区 (EvacuationZone)")]
    public sealed class EvacuationZone : MonoBehaviour
    {
        [Tooltip("任务进度来源；留空时自动查找场景里的 QuestLogProvider。")]
        [SerializeField]
        private QuestLogProvider provider;

        [Tooltip("只有带该标签的角色会触发出口；玩家 Prefab 必须打 Player 标签。")]
        [SerializeField]
        private string playerTag = "Player";

        [TextArea(1, 3)]
        [SerializeField]
        private string lockedMessage = "出口还锁着：先去门厅启动出口联动装置。";

        [TextArea(1, 3)]
        [SerializeField]
        private string evacuatedMessage = "225 全员撤出宿舍楼。";

        [Tooltip("结算表现：结局界面、灯光、音乐，由成员5/6 接入。")]
        [SerializeField]
        private UnityEvent onEvacuated;

        private bool evacuated;

        /// <summary>是否已完成撤离结算。</summary>
        public bool HasEvacuated => evacuated;

        /// <summary>给 UI/字幕的反馈文本（成功或失败都会给）。</summary>
        public event Action<string> Feedback;

        /// <summary>撤离完成事件。</summary>
        public event Action Evacuated;

        private void Reset()
        {
            Collider2D zone = GetComponent<Collider2D>();
            if (zone != null)
            {
                zone.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(playerTag) && !IsPlayer(other))
            {
                return;
            }

            TryEvacuate();
        }

        /// <summary>
        /// 尝试结算：出口未开启返回 false 并给出原因；已结算过直接返回 true。
        /// 触发器与测试都走这里，避免两条判定路径。
        /// </summary>
        public bool TryEvacuate()
        {
            if (evacuated)
            {
                return true;
            }

            IQuestLog log = ResolveLog();
            if (!log.HasFlag(PuzzleFlag.ExitUnlocked))
            {
                Feedback?.Invoke(lockedMessage);
                return false;
            }

            evacuated = true;
            log.SetFlag(PuzzleFlag.EvacuationComplete);
            onEvacuated?.Invoke();
            Evacuated?.Invoke();
            Feedback?.Invoke(evacuatedMessage);
            return true;
        }

        /// <summary>编辑器工具与自测使用：配置来源与文案。</summary>
        public void Configure(QuestLogProvider questProvider, string tag = "Player", string lockedText = null, string successText = null)
        {
            provider = questProvider;
            playerTag = tag;
            if (!string.IsNullOrEmpty(lockedText))
            {
                lockedMessage = lockedText;
            }

            if (!string.IsNullOrEmpty(successText))
            {
                evacuatedMessage = successText;
            }

            evacuated = false;
        }

        private IQuestLog ResolveLog()
        {
            return provider != null ? provider.Log : QuestLogLocator.Resolve();
        }

        private bool IsPlayer(Collider2D other)
        {
            if (other.CompareTag(playerTag))
            {
                return true;
            }

            Rigidbody2D body = other.attachedRigidbody;
            return body != null && body.CompareTag(playerTag);
        }
    }
}
