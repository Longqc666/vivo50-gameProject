using UnityEngine;
using D225.Interact;
using D225.Quest;

namespace D225.Sandbox
{
    /// <summary>
    /// 联调演示用的极简 HUD：任务目标、背包/线索、交互提示与反馈。
    /// 只服务本场景的手工验收，正式 HUD 属于成员5，接入后会替换本脚本。
    /// </summary>
    [AddComponentMenu("D225/联调/演示 HUD (SandboxHud)")]
    public sealed class SandboxHud : MonoBehaviour
    {
        [SerializeField]
        private SandboxInteractor interactor;

        [SerializeField]
        private Inventory inventory;

        [SerializeField]
        private PuzzleObjectiveTracker tracker;

        private Font font;
        private Texture2D panelTexture;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle hintStyle;
        private GUIStyle captionStyle;
        private GUIStyle bodyStyle;
        private GUIStyle promptStyle;
        private GUIStyle feedbackStyle;
        private bool stylesReady;

        public void Configure(SandboxInteractor sandboxInteractor, Inventory playerInventory, PuzzleObjectiveTracker objectiveTracker)
        {
            interactor = sandboxInteractor;
            inventory = playerInventory;
            tracker = objectiveTracker;
        }

        private void EnsureStyles()
        {
            if (stylesReady)
            {
                return;
            }

            stylesReady = true;
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Arial Unicode MS", "Arial" },
                18);

            panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelTexture.SetPixel(0, 0, new Color(0.02f, 0.03f, 0.04f, 0.68f));
            panelTexture.Apply();

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = panelTexture },
                padding = new RectOffset(12, 12, 10, 10)
            };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.88f, 0.9f, 0.94f) }
            };

            hintStyle = new GUIStyle(titleStyle)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.62f, 0.66f, 0.72f) }
            };

            captionStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 12,
                normal = { textColor = new Color(0.55f, 0.62f, 0.7f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = new Color(0.95f, 0.94f, 0.9f) }
            };

            promptStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.92f, 0.66f) }
            };

            feedbackStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.94f, 0.82f) }
            };
        }

        private void OnGUI()
        {
            EnsureStyles();

            float width = Screen.width;
            float height = Screen.height;

            GUI.Label(new Rect(0f, 8f, width, 28f), "225：熄灯后 · 交互与谜题联调场景", titleStyle);
            GUI.Label(
                new Rect(0f, 34f, width, 20f),
                "A / D 移动　Shift 奔跑　E 交互　F 仅用于正式版手电（本场景未接）",
                hintStyle);

            DrawObjective();
            DrawInventory();

            if (interactor != null && !string.IsNullOrEmpty(interactor.Feedback))
            {
                feedbackStyle.normal.textColor = interactor.FeedbackSucceeded
                    ? new Color(0.78f, 0.94f, 0.82f)
                    : new Color(1f, 0.72f, 0.62f);
                GUI.Label(new Rect(0f, height - 158f, width, 58f), interactor.Feedback, feedbackStyle);
            }

            if (interactor != null && !string.IsNullOrEmpty(interactor.Prompt))
            {
                GUI.Label(new Rect(0f, height - 104f, width, 36f), interactor.Prompt, promptStyle);
            }

            GUI.Label(
                new Rect(16f, height - 34f, width - 32f, 22f),
                "联调场景：背景为成员4 提供的场景图，角色与镜头为临时占位，正式关卡由成员3 制作",
                hintStyle);
        }

        private void DrawObjective()
        {
            string objective = tracker != null && !string.IsNullOrEmpty(tracker.CurrentObjective)
                ? tracker.CurrentObjective
                : "主线目标已完成";

            GUI.Box(new Rect(16f, 66f, Mathf.Min(430f, Screen.width * 0.42f), 66f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(30f, 72f, 400f, 18f), "当前任务", captionStyle);
            GUI.Label(new Rect(30f, 92f, 400f, 34f), objective, bodyStyle);
        }

        private void DrawInventory()
        {
            if (inventory == null)
            {
                return;
            }

            string items = "背包：空";
            if (inventory.Items.Count > 0)
            {
                items = "背包：";
                for (int i = 0; i < inventory.Items.Count; i++)
                {
                    ItemData item = inventory.Items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    if (i > 0)
                    {
                        items += "　";
                    }

                    items += item.DisplayName + "×" + inventory.Count(item.ItemId);
                }
            }

            string clues = "线索：" + inventory.Clues.Count + " 条";
            float panelWidth = Mathf.Min(360f, Screen.width * 0.34f);
            float x = Screen.width - panelWidth - 16f;

            GUI.Box(new Rect(x, 66f, panelWidth, 76f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 14f, 72f, panelWidth - 28f, 18f), "道具与线索", captionStyle);
            GUI.Label(new Rect(x + 14f, 92f, panelWidth - 28f, 22f), items, bodyStyle);
            GUI.Label(new Rect(x + 14f, 116f, panelWidth - 28f, 20f), clues, captionStyle);
        }
    }
}
