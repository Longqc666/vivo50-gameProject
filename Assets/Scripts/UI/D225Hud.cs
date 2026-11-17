using TMPro;
using UnityEngine;
using UnityEngine.UI;
using D225.Audio;
using D225.Interact;
using D225.Quest;

namespace D225.UI
{
    /// <summary>
    /// 局内 HUD：任务栏、[E] 交互提示、反馈条、耐力条。
    /// 只负责「把别人算好的结果放到屏幕上」：任务文本来自 PuzzleObjectiveTracker，
    /// 提示文本来自 PlayerInteractor（内部调用 InteractionPrompt），耐力来自 PlayerController。
    /// 本脚本不做任何谜题判定，也不决定能不能交互——那是成员4 的交互物自己算的。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/界面/局内 HUD (D225Hud)")]
    public sealed class D225Hud : MonoBehaviour
    {
        [Header("数据源：全部拖拽赋值，禁止在运行时用 Find 查找")]
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerController player;
        [SerializeField] private PuzzleObjectiveTracker tracker;
        [SerializeField] private Inventory inventory;

        [Header("任务栏")]
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private string objectiveDoneText = "主线目标已完成";

        [Header("交互提示")]
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private CanvasGroup promptGroup;

        [Header("反馈条（拾取、拒绝原因、线索提示）")]
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private CanvasGroup feedbackGroup;
        [SerializeField] private float feedbackSeconds = 2.5f;

        [Header("耐力条：用 Image 的 Filled 模式，不用 Slider")]
        [SerializeField] private Image staminaFill;
        [SerializeField] private CanvasGroup staminaGroup;

        // 上一次已经显示过的反馈版本号，用来保证「同一条反馈只播一次」
        private int seenFeedbackVersion;
        // 反馈条剩余显示时间（秒，倒计时）
        private float feedbackLeft;

        private void OnEnable()
        {
            // 任务栏：订阅「目标变了」事件。事件是别人（PuzzleObjectiveTracker）发出来的，
            // 我们只在事件里改文本，绝不在 Update 里每帧重新算。
            if (tracker != null)
            {
                tracker.ObjectiveChanged += SetObjective;
                SetObjective(tracker.CurrentObjective); // 场景刚加载时事件可能已经发过了，这里补一次
            }

            // 背包：拿到新物品/新线索时弹一条反馈
            if (inventory != null)
            {
                inventory.ItemAdded += OnItemAdded;
                inventory.ClueAdded += OnClueAdded;
            }
        }

        private void OnDisable()
        {
            // 取消订阅一定要和订阅成对出现，否则对象销毁后事件还会调用到已经死掉的脚本，报空引用
            if (tracker != null)
            {
                tracker.ObjectiveChanged -= SetObjective;
            }

            if (inventory != null)
            {
                inventory.ItemAdded -= OnItemAdded;
                inventory.ClueAdded -= OnClueAdded;
            }
        }

        private void Update()
        {
            UpdatePrompt();
            UpdateFeedback();
            UpdateStamina();
        }

        /// <summary>提示只在有目标时显示。PlayerInteractor 在暂停/躲藏时会把 CurrentPrompt 清空。</summary>
        private void UpdatePrompt()
        {
            string prompt = interactor != null ? interactor.CurrentPrompt : string.Empty;
            bool show = !string.IsNullOrEmpty(prompt);

            if (promptText != null)
            {
                promptText.text = show ? prompt : string.Empty;
            }

            if (promptGroup != null)
            {
                promptGroup.alpha = show ? 1f : 0f;
            }
        }

        /// <summary>
        /// PlayerInteractor 没有「反馈事件」，只暴露了一个自增的 FeedbackVersion。
        /// 所以这里用「版本号变了就显示」的轮询方式，代价只有一次整数比较，很便宜。
        /// </summary>
        private void UpdateFeedback()
        {
            if (interactor != null && interactor.FeedbackVersion != seenFeedbackVersion)
            {
                seenFeedbackVersion = interactor.FeedbackVersion;
                ShowFeedback(interactor.LastFeedback);
            }

            if (feedbackLeft <= 0f)
            {
                // 空闲时必须把整组压到透明：场景里存的占位文字否则会一直挂在屏幕上
                if (feedbackGroup != null && feedbackGroup.alpha > 0f)
                {
                    feedbackGroup.alpha = 0f;
                }

                return;
            }

            // 用 unscaledDeltaTime：暂停时 Time.timeScale = 0，用 deltaTime 会永远不减，反馈条就卡住了
            feedbackLeft -= Time.unscaledDeltaTime;

            if (feedbackGroup != null)
            {
                feedbackGroup.alpha = Mathf.Clamp01(feedbackLeft);
            }
        }

        private void UpdateStamina()
        {
            if (player == null)
            {
                return;
            }

            if (staminaFill != null)
            {
                float ratio = Mathf.Clamp01(player.StaminaRatio);

                if (staminaFill.type == Image.Type.Filled)
                {
                    // 有「源图像」+ 已填充：靠填充量缩
                    staminaFill.fillAmount = ratio;
                }
                else
                {
                    // 纯色块（无源图像）：靠改右锚点缩宽度，不依赖任何贴图
                    RectTransform fillRect = staminaFill.rectTransform;
                    Vector2 max = fillRect.anchorMax;
                    if (!Mathf.Approximately(max.x, ratio))
                    {
                        fillRect.anchorMax = new Vector2(ratio, max.y);
                    }
                }
            }

            // 满耐力时淡一点但仍然可见，否则玩家会以为耐力条坏了
            if (staminaGroup != null)
            {
                float target = player.StaminaRatio >= 0.999f ? 0.5f : 1f;
                staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, target, Time.unscaledDeltaTime * 2f);
            }
        }

        /// <summary>给别处调用：改任务栏文本（一般不用手动调，订阅事件就够了）。</summary>
        public void SetObjective(string text)
        {
            if (objectiveText == null)
            {
                return;
            }

            objectiveText.text = string.IsNullOrEmpty(text) ? objectiveDoneText : text;
        }

        /// <summary>给别处调用：弹一条反馈（剧情提示、教学提示都可以用它）。</summary>
        public void ShowFeedback(string text)
        {
            if (string.IsNullOrEmpty(text) || feedbackText == null)
            {
                return;
            }

            feedbackText.text = text;
            feedbackLeft = feedbackSeconds;

            if (feedbackGroup != null)
            {
                feedbackGroup.alpha = 1f;
            }
        }

        private void OnItemAdded(ItemData item, int count)
        {
            if (item == null)
            {
                return;
            }

            // 线索在 ClueAdded 里已经提示过了，这里跳过，避免同一秒弹两条互相覆盖
            if (item.Category == ItemCategory.Clue)
            {
                return;
            }

            ShowFeedback("获得：" + item.DisplayName);
            AudioManager.Play("ui.pickup");
        }

        private void OnClueAdded(ItemData clue)
        {
            if (clue == null)
            {
                return;
            }

            ShowFeedback("记下线索：" + clue.DisplayName);
            AudioManager.Play("ui.clue");
        }
    }
}
