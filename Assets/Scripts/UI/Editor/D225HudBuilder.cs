using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using D225.Interact;
using D225.Quest;
using D225.UI;

namespace D225.UI.EditorTools
{
    /// <summary>
    /// 一键生成局内 HUD：画布、任务栏、交互提示、反馈条、耐力条，并把 D225Hud 的引用全部接好。
    /// 菜单：D225 → 生成局内 HUD。可以重复运行：
    /// 已存在的对象会被校正参数（锚点/轴心/尺寸/颜色/字体），缺的才新建，不会删你手动加的东西。
    /// </summary>
    public static class D225HudBuilder
    {
        private const string FontPath = "Assets/Fonts/NotoSansCJKsc-Regular SDF.asset";

        private static readonly Color32 PanelColor = new Color32(0x0A, 0x0F, 0x14, 175);
        private static readonly Color32 CaptionColor = new Color32(0x98, 0xA6, 0xB5, 255);
        private static readonly Color32 BodyColor = new Color32(0xE8, 0xED, 0xF2, 255);
        private static readonly Color32 PromptColor = new Color32(0xFF, 0xE9, 0xA8, 255);
        private static readonly Color32 FeedbackColor = new Color32(0xC6, 0xE6, 0xCF, 255);
        private static readonly Color32 StaminaBgColor = new Color32(0x10, 0x18, 0x20, 200);
        private static readonly Color32 StaminaFillColor = new Color32(0x6F, 0xA8, 0xDC, 255);

        [MenuItem("D225/生成局内 HUD")]
        public static void BuildHud()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
                Debug.LogWarning("[D225] 没找到中文字体 " + FontPath + "，先用默认字体，中文可能显示为方块。");
            }

            Canvas canvas = EnsureCanvas();
            GameObject hudRoot = EnsureChild(canvas.transform, "HUD", null);

            // HUD 铺满画布
            RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
            Stretch(hudRect);

            // ---------- 任务栏 ----------
            GameObject panel = EnsureChild(hudRoot.transform, "TaskPanel", typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Corner(panelRect, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(420f, 100f));
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = false;

            GameObject caption = EnsureText(hudRoot.transform, panel.transform, "Caption", font);
            Corner(caption.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(388f, 24f));
            ApplyText(caption, "当前任务", font, 20f, CaptionColor, TextAlignmentOptions.Left);

            GameObject objective = EnsureText(hudRoot.transform, panel.transform, "ObjectiveText", font);
            Corner(objective.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(16f, -40f), new Vector2(388f, 48f));
            ApplyText(objective, "（任务文本占位）", font, 24f, BodyColor, TextAlignmentOptions.TopLeft);

            // ---------- 交互提示 ----------
            GameObject promptGroup = EnsureChild(hudRoot.transform, "PromptGroup", typeof(CanvasGroup));
            Corner(promptGroup.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(800f, 44f));
            promptGroup.GetComponent<CanvasGroup>().alpha = 1f;

            GameObject promptText = EnsureText(hudRoot.transform, promptGroup.transform, "PromptText", font);
            Stretch(promptText.GetComponent<RectTransform>());
            ApplyText(promptText, "[E] 拾取 手电筒", font, 28f, PromptColor, TextAlignmentOptions.Center);

            // ---------- 反馈条 ----------
            GameObject feedbackGroup = EnsureChild(hudRoot.transform, "FeedbackGroup", typeof(CanvasGroup));
            Corner(feedbackGroup.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 152f), new Vector2(900f, 40f));
            feedbackGroup.GetComponent<CanvasGroup>().alpha = 1f;

            GameObject feedbackText = EnsureText(hudRoot.transform, feedbackGroup.transform, "FeedbackText", font);
            Stretch(feedbackText.GetComponent<RectTransform>());
            ApplyText(feedbackText, "获得：手电筒", font, 22f, FeedbackColor, TextAlignmentOptions.Center);

            // ---------- 耐力条 ----------
            GameObject staminaGroup = EnsureChild(hudRoot.transform, "StaminaGroup", typeof(CanvasGroup));
            Corner(staminaGroup.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(260f, 18f));
            staminaGroup.GetComponent<CanvasGroup>().alpha = 1f;

            bool hadBg = staminaGroup.transform.Find("StaminaBG") != null;
            GameObject staminaBg = EnsureChild(staminaGroup.transform, "StaminaBG", typeof(Image));
            Stretch(staminaBg.GetComponent<RectTransform>());
            Image bgImage = staminaBg.GetComponent<Image>();
            if (!hadBg)
            {
                bgImage.color = StaminaBgColor;   // 你自己换过颜色就保留，不覆盖
            }
            bgImage.raycastTarget = false;
            // 纯色块：不指定源图像，Image 会画满一个矩形（没有圆角，两端不会露出底槽）
            bgImage.sprite = null;
            bgImage.type = Image.Type.Simple;

            bool hadFill = staminaGroup.transform.Find("StaminaFill") != null;
            GameObject staminaFill = EnsureChild(staminaGroup.transform, "StaminaFill", typeof(Image));
            Stretch(staminaFill.GetComponent<RectTransform>());
            Image fillImage = staminaFill.GetComponent<Image>();
            if (!hadFill)
            {
                fillImage.color = StaminaFillColor;   // 同上
            }
            fillImage.raycastTarget = false;
            // 同样是纯色块，宽度由 D225Hud 每帧改「右锚点」来控制（anchorMax.x = 耐力比例）。
            // 不再用「已填充」+ 圆角贴图：圆角被横向拉伸后，两端会露出一大块底槽，看着像没填满。
            fillImage.sprite = null;
            fillImage.type = Image.Type.Simple;

            // ---------- 任务进度 ----------
            GameObject questGo = EnsureQuestTracker();

            // ---------- 挂脚本、接引用 ----------
            D225Hud hud = hudRoot.GetComponent<D225Hud>();
            if (hud == null)
            {
                hud = hudRoot.AddComponent<D225Hud>();
            }

            PlayerInteractor interactor = Object.FindObjectOfType<PlayerInteractor>();
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            Inventory inventory = Object.FindObjectOfType<Inventory>();

            SerializedObject so = new SerializedObject(hud);
            SetRef(so, "interactor", interactor);
            SetRef(so, "player", player);
            SetRef(so, "tracker", questGo.GetComponent<PuzzleObjectiveTracker>());
            SetRef(so, "inventory", inventory);
            SetRef(so, "objectiveText", objective.GetComponent<TMP_Text>());
            SetRef(so, "promptText", promptText.GetComponent<TMP_Text>());
            SetRef(so, "promptGroup", promptGroup.GetComponent<CanvasGroup>());
            SetRef(so, "feedbackText", feedbackText.GetComponent<TMP_Text>());
            SetRef(so, "feedbackGroup", feedbackGroup.GetComponent<CanvasGroup>());
            SetRef(so, "staminaFill", fillImage);
            SetRef(so, "staminaGroup", staminaGroup.GetComponent<CanvasGroup>());
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = hudRoot;
            EditorGUIUtility.PingObject(hudRoot);

            string missing = "";
            if (interactor == null) missing += " PlayerInteractor";
            if (player == null) missing += " PlayerController";
            if (inventory == null) missing += " Inventory";
            if (missing.Length > 0)
            {
                Debug.LogWarning("[D225] HUD 已生成，但场景里没找到这些组件，需要手动拖：" + missing);
            }

            Debug.Log("[D225] HUD 生成完成。按 Ctrl+S 保存场景，然后点播放验收。");
        }

        // ---------- 构建工具 ----------

        private static Canvas EnsureCanvas()
        {
            Canvas existing = Object.FindObjectOfType<Canvas>();
            GameObject go;

            // 优先找我们自己命名的画布，避免复用队友的临时 HUD 画布
            GameObject named = GameObject.Find("D225_Canvas");
            if (named != null)
            {
                go = named;
            }
            else
            {
                go = new GameObject("D225_Canvas", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create D225_Canvas");
                if (existing != null && existing.gameObject.name == "Canvas")
                {
                    Debug.Log("[D225] 场景里已有队友的 Canvas，这里另建 D225_Canvas 叠在上面。");
                }
            }

            Canvas canvas = go.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = go.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = go.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            if (go.GetComponent<GraphicRaycaster>() == null)
            {
                go.AddComponent<GraphicRaycaster>();
            }

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;

            // 场景里没有 EventSystem 就补一个，否则以后按钮点不动
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            return canvas;
        }

        private static GameObject EnsureChild(Transform parent, string name, System.Type componentType)
        {
            Transform found = parent.Find(name);
            GameObject go;

            if (found != null)
            {
                go = found.gameObject;
            }
            else
            {
                // 兼容手滑拼错的名字
                if (name == "ObjectiveText")
                {
                    Transform typo = parent.Find("ObjectiveTest");
                    if (typo != null)
                    {
                        typo.gameObject.name = "ObjectiveText";
                        return typo.gameObject;
                    }
                }

                go = componentType == null
                    ? new GameObject(name, typeof(RectTransform))
                    : new GameObject(name, typeof(RectTransform), componentType);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                go.transform.SetParent(parent, false);
            }

            if (componentType != null && go.GetComponent(componentType) == null)
            {
                go.AddComponent(componentType);
            }

            return go;
        }

        /// <summary>取一个 TMP 文本对象：parent 是层级父级，panel 是面板父级（可为空）。</summary>
        private static GameObject EnsureText(Transform hudRoot, Transform panel, string name, TMP_FontAsset font)
        {
            GameObject go;
            Transform found = panel.Find(name);

            if (found != null)
            {
                go = found.gameObject;
            }
            else if (name == "ObjectiveText" && panel.Find("ObjectiveTest") != null)
            {
                go = panel.Find("ObjectiveTest").gameObject;
                go.name = "ObjectiveText";
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                go.transform.SetParent(panel, false);
            }

            if (go.GetComponent<TextMeshProUGUI>() == null)
            {
                go.AddComponent<TextMeshProUGUI>();
            }

            return go;
        }

        private static void ApplyText(GameObject go, string content, TMP_FontAsset font, float size, Color32 color, TextAlignmentOptions align)
        {
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
        }


        private static GameObject EnsureQuestTracker()
        {
            GameObject go = GameObject.Find("D225_Quest");
            if (go == null)
            {
                go = new GameObject("D225_Quest", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create D225_Quest");
            }

            go.transform.localPosition = Vector3.zero;

            PuzzleObjectiveTracker tracker = go.GetComponent<PuzzleObjectiveTracker>();
            if (tracker == null)
            {
                tracker = go.AddComponent<PuzzleObjectiveTracker>();
            }

            SerializedObject so = new SerializedObject(tracker);
            SerializedProperty steps = so.FindProperty("steps");

            ObjectiveStep[] wanted =
            {
                Step(PuzzleFlag.FlashlightAcquired, "找到手电筒，照向走廊"),
                Step(PuzzleFlag.DutyNoteRead, "读一下桌上的便笺"),
                Step(PuzzleFlag.FuseAcquired, "去设备间找到保险丝"),
                Step(PuzzleFlag.PowerRestored, "把保险丝装进配电箱"),
                Step(PuzzleFlag.StairKeyAcquired, "找到楼梯门钥匙"),
                Step(PuzzleFlag.StairDoorUnlocked, "用钥匙打开楼梯门"),
                Step(PuzzleFlag.ExitUnlocked, "启动门厅出口联动装置")
            };

            steps.arraySize = wanted.Length;
            for (int i = 0; i < wanted.Length; i++)
            {
                SerializedProperty element = steps.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("completedFlag").enumValueIndex = (int)wanted[i].completedFlag;
                element.FindPropertyRelative("objective").stringValue = wanted[i].objective;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static ObjectiveStep Step(PuzzleFlag flag, string objective)
        {
            ObjectiveStep step = new ObjectiveStep();
            step.completedFlag = flag;
            step.objective = objective;
            return step;
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[D225] D225Hud 上没有字段 " + field);
                return;
            }

            property.objectReferenceValue = value;
        }

        /// <summary>锚点贴角：锚点和轴心一起改，位置才等于「离这个角多少像素」。</summary>
        private static void Corner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
