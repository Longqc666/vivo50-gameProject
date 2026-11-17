using System.Collections.Generic;
using TMPro;
using UnityEngine;
using D225.Audio;

namespace D225.UI
{
    /// <summary>
    /// 字幕/对话框：一句一句往下走，打字机效果 + 空格跳过 + 自动停留。
    /// 只吃 DialogueSequence，不判定剧情——什么时候该说话由任务事件或触发器决定。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/界面/字幕 (SubtitleView)")]
    public sealed class SubtitleView : MonoBehaviour
    {
        [Header("显示对象")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;

        [Header("节奏")]
        [Tooltip("每秒显示多少字，25~35 比较像人说话")]
        [SerializeField] private float charsPerSecond = 30f;

        [SerializeField] private KeyCode advanceKey = KeyCode.Space;

        private readonly Queue<DialogueLine> queue = new Queue<DialogueLine>();
        private DialogueLine current;
        private bool hasLine;
        private bool typing;
        private float visibleChars;
        private float holdLeft;

        /// <summary>正在播（含排队）时为 true，别的地方可以据此决定要不要暂停玩家输入。</summary>
        public bool IsPlaying => hasLine || queue.Count > 0;

        private void Awake()
        {
            Hide();
        }

        /// <summary>播放一整段对话（会接在当前队列后面）。</summary>
        public void Play(DialogueSequence sequence)
        {
            if (sequence == null || sequence.Count == 0)
            {
                return;
            }

            for (int i = 0; i < sequence.Count; i++)
            {
                queue.Enqueue(sequence.Lines[i]);
            }

            if (!hasLine)
            {
                NextLine();
            }
        }

        /// <summary>播单句（临时提示用）。</summary>
        public void PlayLine(string speaker, string text, float holdSeconds = 2.5f)
        {
            queue.Enqueue(new DialogueLine
            {
                speaker = speaker,
                text = text,
                holdSeconds = holdSeconds
            });

            if (!hasLine)
            {
                NextLine();
            }
        }

        /// <summary>清空并隐藏（切场景、被抓、返回主菜单时调用）。</summary>
        public void Clear()
        {
            queue.Clear();
            hasLine = false;
            typing = false;
            holdLeft = 0f;
            Hide();
        }

        private void Update()
        {
            if (!hasLine)
            {
                return;
            }

            // unscaledDeltaTime：万一对话时游戏被暂停（timeScale = 0），字幕还得继续走
            float dt = Time.unscaledDeltaTime;
            bool advance = Input.GetKeyDown(advanceKey);

            if (typing)
            {
                visibleChars += charsPerSecond * dt;

                if (advance)
                {
                    visibleChars = current.text.Length; // 按空格直接显示全句
                }

                if (bodyText != null)
                {
                    bodyText.maxVisibleCharacters = Mathf.FloorToInt(visibleChars);
                }

                if (visibleChars >= current.text.Length)
                {
                    typing = false;
                    holdLeft = Mathf.Max(0.5f, current.holdSeconds);
                }

                return;
            }

            holdLeft -= dt;

            if (advance || holdLeft <= 0f)
            {
                NextLine();
            }
        }

        private void NextLine()
        {
            if (queue.Count == 0)
            {
                hasLine = false;
                Hide();
                return;
            }

            current = queue.Dequeue();
            hasLine = true;
            typing = true;
            visibleChars = 0f;
            holdLeft = 0f;

            if (speakerText != null)
            {
                speakerText.text = string.IsNullOrEmpty(current.speaker) ? string.Empty : current.speaker;
            }

            if (bodyText != null)
            {
                // 先整句写进去再靠 maxVisibleCharacters 控制显示到第几个字，
                // 这样排版不会因为逐字追加而抖动换行
                bodyText.text = current.text ?? string.Empty;
                bodyText.maxVisibleCharacters = 0;
            }

            if (group != null)
            {
                group.alpha = 1f;
            }

            if (current.voice != null)
            {
                AudioManager.PlayClip(current.voice, 1f);
            }
        }

        private void Hide()
        {
            if (group != null)
            {
                group.alpha = 0f;
            }

            if (bodyText != null)
            {
                bodyText.text = string.Empty;
            }
        }
    }
}
