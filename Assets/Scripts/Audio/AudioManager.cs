using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace D225.Audio
{
    /// <summary>
    /// 全局音频管理器。职责只有四件事：
    /// 1) 一次性音效（拾取、开关门、按钮）——同一音效短时间内重复请求只播一次；
    /// 2) 环境声——换区域时淡入淡出，不硬切；
    /// 3) 追逐心跳——进入追逐淡入、解除淡出，绝不叠加两条；
    /// 4) 主音量——主菜单和暂停页共用同一个值。
    /// 它不认识谜题、不认识任务、不认识 UI，只接收「播这个键」的请求。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/音频/音频管理器 (AudioManager)")]
    public sealed class AudioManager : MonoBehaviour
    {
        /// <summary>场景里唯一的实例。别处用 AudioManager.Play(...) 静态方法即可，不用自己判空。</summary>
        public static AudioManager Instance { get; private set; }

        [Header("音效表")]
        [SerializeField] private SfxLibrary library;

        [Header("播放设置")]
        [Tooltip("同时能播几条一次性音效的声道数，4~8 足够")]
        [SerializeField] private int sfxVoices = 6;

        [Tooltip("同一音效在这个时间窗口内重复请求会被忽略（秒）")]
        [SerializeField] private float duplicateWindow = 0.06f;

        [Header("长音轨（拖场景里的两个 AudioSource 进来）")]
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource chaseSource;

        private readonly List<AudioSource> pool = new List<AudioSource>();
        private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
        private int nextVoice;
        private float master = 1f;
        private Coroutine ambienceRoutine;
        private Coroutine chaseRoutine;

        private const string VolumeKey = "d225.volume";

        private void Awake()
        {
            // 单例：如果场景里已经有别的 AudioManager，就把自己删掉，避免两份同时响
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // 运行时创建声道池：不需要在编辑器里摆一堆 AudioSource，也不会因为漏拖而报错
            int count = Mathf.Max(1, sfxVoices);
            for (int i = 0; i < count; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f; // 2D 游戏，不做 3D 衰减
                pool.Add(source);
            }

            master = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            AudioListener.volume = master;
        }

        /// <summary>静态快捷入口：找不到管理器时静默忽略，不会让整个游戏因为少个音效崩掉。</summary>
        public static void Play(string key, float volumeScale = 1f)
        {
            if (Instance != null)
            {
                Instance.PlayKey(key, volumeScale);
            }
        }

        /// <summary>直接播一个 AudioClip（音频来源不是音效表时用）。</summary>
        public static void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (Instance != null)
            {
                Instance.PlaySfx(clip, volume);
            }
        }

        private void PlayKey(string key, float volumeScale)
        {
            if (library == null || !library.TryGet(key, out SfxLibrary.Entry entry))
            {
                return;
            }

            PlaySfx(entry.clip, entry.volume * volumeScale);
        }

        /// <summary>
        /// 播一次性音效。同一个 clip 在 duplicateWindow 内再次请求会被丢弃，
        /// 这样「连按两次 E」或「一帧内两个脚本同时请求」都不会变成两声。
        /// </summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || pool.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(clip, out float last) && now - last < duplicateWindow)
            {
                return;
            }

            lastPlayed[clip] = now;

            AudioSource source = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Count;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume) * master;
            source.Play();
        }

        /// <summary>切换环境声（换区域时调用）。传 null 表示淡出到静音。</summary>
        public void SetAmbience(AudioClip clip, float fadeSeconds = 1.5f)
        {
            if (ambienceSource == null)
            {
                return;
            }

            if (clip != null)
            {
                ambienceSource.clip = clip;
                ambienceSource.loop = true;
            }

            if (ambienceRoutine != null)
            {
                StopCoroutine(ambienceRoutine);
            }

            ambienceRoutine = StartCoroutine(FadeTo(ambienceSource, clip != null ? 1f : 0f, fadeSeconds));
        }

        /// <summary>按键切环境声，键名对应音效表里的 amb.*</summary>
        public static void SetAmbienceKey(string key, float fadeSeconds = 1.5f)
        {
            if (Instance == null || Instance.library == null)
            {
                return;
            }

            Instance.library.TryGet(key, out SfxLibrary.Entry entry);
            Instance.SetAmbience(entry.clip, fadeSeconds);
        }

        /// <summary>
        /// 追逐音：进入追逐淡入，解除淡出。用 bool 而不是「播一次」，
        /// 是因为策划案要求「追逐解除逐渐淡出，避免重复叠加」。
        /// </summary>
        public void SetChase(bool active, float fadeSeconds = 0.7f)
        {
            if (chaseSource == null)
            {
                return;
            }

            if (chaseRoutine != null)
            {
                StopCoroutine(chaseRoutine);
            }

            chaseRoutine = StartCoroutine(FadeTo(chaseSource, active ? 1f : 0f, fadeSeconds));
        }

        /// <summary>主音量。主菜单设置页和暂停设置页都调这一个方法，值就自然同步了。</summary>
        public void SetMasterVolume(float value)
        {
            master = Mathf.Clamp01(value);
            AudioListener.volume = master;
            PlayerPrefs.SetFloat(VolumeKey, master);
            PlayerPrefs.Save();
        }

        /// <summary>读当前音量，给设置页的滑条初始化用。</summary>
        public float GetMasterVolume()
        {
            return master;
        }

        private IEnumerator FadeTo(AudioSource source, float target, float seconds)
        {
            if (target > 0f && !source.isPlaying)
            {
                source.Play();
            }

            float from = source.volume;
            float duration = Mathf.Max(0.01f, seconds);
            float t = 0f;

            while (t < duration)
            {
                // unscaledDeltaTime：暂停（timeScale = 0）时淡出仍然要走完，否则音乐会卡在半音量
                t += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, target, t / duration) * master;
                yield return null;
            }

            source.volume = target * master;

            if (target <= 0f)
            {
                source.Stop();
            }
        }
    }
}
