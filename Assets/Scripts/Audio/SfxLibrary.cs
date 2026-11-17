using System;
using System.Collections.Generic;
using UnityEngine;

namespace D225.Audio
{
    /// <summary>
    /// 音效索引表：把「字符串键」映射到「音频文件」。
    /// 好处是代码里只写 AudioManager.Play("ui.pickup")，换音频时只改这张表，不用改代码、不用重新拖引用。
    /// 这也是分工表要求的「音频引用清单」本身——把这张表截图就是证据。
    /// </summary>
    [CreateAssetMenu(fileName = "D225_SfxLibrary", menuName = "D225/音效表 (SfxLibrary)")]
    public sealed class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("代码里用的键，全组统一小写点分，例如 ui.pickup")]
            public string key;

            public AudioClip clip;

            [Range(0f, 1f)]
            public float volume;
        }

        [SerializeField]
        private Entry[] entries = new Entry[0];

        private Dictionary<string, Entry> map;

        /// <summary>按键查一条音效；找不到返回 false（不会抛异常，方便占位期直接跑）。</summary>
        public bool TryGet(string key, out Entry entry)
        {
            if (map == null)
            {
                BuildMap();
            }

            entry = default;
            return !string.IsNullOrEmpty(key) && map.TryGetValue(key, out entry) && entry.clip != null;
        }

        /// <summary>编辑器/自测使用：一次性写入整张表。</summary>
        public void Configure(Entry[] items)
        {
            entries = items ?? new Entry[0];
            map = null;
        }

        private void BuildMap()
        {
            map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                map[entry.key] = entry;
            }
        }

        private void OnValidate()
        {
            map = null;
        }
    }
}
