using System;
using System.Collections.Generic;
using UnityEngine;

namespace D225.UI
{
    /// <summary>一句对白。不做成单独的资源文件，是为了让一段对话能一次配完。</summary>
    [Serializable]
    public struct DialogueLine
    {
        [Tooltip("说话人，没有就留空（旁白/字幕）")]
        public string speaker;

        [TextArea(2, 5)]
        public string text;

        [Tooltip("整句显示完后停留多少秒；打字过程中按空格可跳过")]
        public float holdSeconds;

        [Tooltip("可选：这句的配音，没有就留空")]
        public AudioClip voice;
    }

    /// <summary>
    /// 一段对话的配置资产。策划案里五位室友的所有台词都放进这里，
    /// 改文案只改资产，不碰代码也不碰场景。
    /// </summary>
    [CreateAssetMenu(fileName = "D225_Dialogue_New", menuName = "D225/对话段落 (DialogueSequence)")]
    public sealed class DialogueSequence : ScriptableObject
    {
        [SerializeField]
        private DialogueLine[] lines = new DialogueLine[0];

        public IReadOnlyList<DialogueLine> Lines => lines;

        public int Count => lines == null ? 0 : lines.Length;

        /// <summary>编辑器/自测使用。</summary>
        public void Configure(DialogueLine[] items)
        {
            lines = items ?? new DialogueLine[0];
        }
    }
}
