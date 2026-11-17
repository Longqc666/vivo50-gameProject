using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using D225.Quest;

namespace D225.UI
{
    /// <summary>策划案 10 的三段灯光状态。</summary>
    public enum LightingState
    {
        /// <summary>停电：区域A/B 常态，冷蓝灰、偏暗。</summary>
        Blackout = 0,

        /// <summary>配电修复后：走廊与门禁上电，稍微亮一点。</summary>
        PowerRestored = 1,

        /// <summary>门厅警报：更硬的光、轻微抖动，但绝不闪瞎眼。</summary>
        HallAlarm = 2
    }

    /// <summary>一个灯光状态对应的参数。</summary>
    [Serializable]
    public struct LightingPreset
    {
        public Color color;

        [Range(0f, 1f)]
        public float intensity;

        [Tooltip("亮度抖动幅度，只有门厅警报给一点点，其他保持 0")]
        [Range(0f, 0.3f)]
        public float flicker;
    }

    /// <summary>
    /// 灯光导演：把「全局光长什么样」收敛到一个脚本里，别的脚本只说「现在该是哪个状态」。
    /// 它会自动跟着 PuzzleFlag.PowerRestored 走，所以成员4 的配电箱一修好，灯就自己亮。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/界面/灯光导演 (LightDirector)")]
    public sealed class LightDirector : MonoBehaviour
    {
        [Header("全局光（场景里的 Global Light 2D）")]
        [SerializeField] private Light2D globalLight;

        [Header("三段预设：顺序必须是 停电 / 供电 / 警报")]
        [SerializeField] private LightingPreset[] presets = new LightingPreset[3];

        [Header("过渡")]
        [SerializeField] private float transitionSeconds = 1.2f;

        [Tooltip("低于这个亮度就看不见路了，暗场也不能黑到影响读信息（策划案 10）")]
        [SerializeField] private float minimumIntensity = 0.15f;

        [Header("自动跟随任务")]
        [Tooltip("勾上后：配电箱修复（PowerRestored）会自动切到「供电」状态")]
        [SerializeField] private bool followQuestFlags = true;

        private IQuestLog log;
        private float baseIntensity;
        private float seed;

        public LightingState State { get; private set; } = LightingState.Blackout;

        /// <summary>状态变化时通知（音效、HUD 提示可以订阅）。</summary>
        public event Action<LightingState> StateChanged;

        private void OnEnable()
        {
            seed = UnityEngine.Random.value * 10f;

            if (followQuestFlags)
            {
                log = QuestLogLocator.Resolve();
                if (log != null)
                {
                    log.FlagRaised += OnFlagRaised;
                    // 场景重载后标记还在，灯要直接落到正确状态，不能先亮一下再暗回去
                    if (log.HasFlag(PuzzleFlag.PowerRestored))
                    {
                        State = LightingState.PowerRestored;
                    }
                }
            }

            ApplyNow();
        }

        private void OnDisable()
        {
            if (log != null)
            {
                log.FlagRaised -= OnFlagRaised;
                log = null;
            }
        }

        private void OnFlagRaised(PuzzleFlag flag)
        {
            if (flag == PuzzleFlag.PowerRestored)
            {
                SetState(LightingState.PowerRestored);
            }
        }

        /// <summary>切状态。instant = true 用于切场景/检查点重试，不要每次都演一遍渐变。</summary>
        public void SetState(LightingState next, bool instant = false)
        {
            if (next == State && !instant)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(next);

            if (instant || transitionSeconds <= 0f)
            {
                ApplyNow();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(Blend(next));
        }

        private void ApplyNow()
        {
            if (globalLight == null)
            {
                Debug.LogWarning("[D225] LightDirector 没有拖 Global Light 2D，灯光不会生效。", this);
                return;
            }

            LightingPreset preset = PresetOf(State);
            globalLight.color = preset.color;
            globalLight.intensity = Mathf.Max(minimumIntensity, preset.intensity);
            baseIntensity = globalLight.intensity;
        }

        private IEnumerator Blend(LightingState state)
        {
            if (globalLight == null)
            {
                yield break;
            }

            LightingPreset preset = PresetOf(state);
            Color fromColor = globalLight.color;
            float fromIntensity = globalLight.intensity;
            float target = Mathf.Max(minimumIntensity, preset.intensity);
            float duration = Mathf.Max(0.01f, transitionSeconds);
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / duration;
                globalLight.color = Color.Lerp(fromColor, preset.color, k);
                globalLight.intensity = Mathf.Lerp(fromIntensity, target, k);
                yield return null;
            }

            globalLight.color = preset.color;
            globalLight.intensity = target;
            baseIntensity = target;
        }

        private LightingPreset PresetOf(LightingState state)
        {
            if (presets == null || presets.Length == 0)
            {
                return new LightingPreset
                {
                    color = new Color(0.106f, 0.141f, 0.188f),
                    intensity = 0.25f,
                    flicker = 0f
                };
            }

            int index = Mathf.Clamp((int)state, 0, presets.Length - 1);
            return presets[index];
        }

        private void Update()
        {
            LightingPreset preset = PresetOf(State);
            if (preset.flicker <= 0f || globalLight == null)
            {
                return;
            }

            // PerlinNoise 比随机数好：连续、不跳变，看起来像电流不稳而不是坏掉的灯
            float noise = Mathf.PerlinNoise(seed, Time.unscaledTime * 7f) - 0.5f;
            globalLight.intensity = Mathf.Max(minimumIntensity, baseIntensity + noise * preset.flicker);
        }
    }
}
