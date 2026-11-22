// D225FlowSceneBuilder.cs —— 流程与集成（成员6 何奕佳）的资源/场景装配器
//
// 用法（Unity 菜单 D225/流程/*）：
//   1. 生成流程界面预制体       → 从成员5 的 D225_UI_Test 场景抽出 Canvas，生成 Assets/Prefabs/UI/D225_FlowMenu.prefab
//   2. 生成主菜单场景           → Assets/Scenes/Scene_Boot.unity，内含 GameManager
//   3. 写入 Build Settings      → Scene_Boot 放第 0 位，其后 A → B → C
//   9. 构建 Windows 版          → Build/Windows/D225.exe
//
// 命令行（CI / 免开界面）：
//   Unity.exe -batchmode -quit -projectPath <工程> -executeMethod D225.Flow.EditorTools.D225FlowSceneBuilder.BuildAll -logFile -
//
// 设计约束：
//   · 只读取成员5 的 UI 场景与成员3 的关卡场景，不保存、不修改它们。
//   · 产出的预制体/场景是本模块自己的文件，可重复生成（覆盖）。
//   · 不引用成员1/2/3/5 的脚本类型以外的实现细节；UiRouter 通过字段名（SerializedObject）写入。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using D225.Core;
using D225.Flow;
using D225.UI;

namespace D225.Flow.EditorTools
{
    public static class D225FlowSceneBuilder
    {
        const string UiSourceScene = "Assets/Scenes/D225_UI_Test.unity";
        const string PrefabsDir = "Assets/Prefabs/UI";
        const string FlowMenuPrefabPath = PrefabsDir + "/D225_FlowMenu.prefab";
        const string BootScenePath = "Assets/Scenes/Scene_Boot.unity";
        const string BuildDir = "Build/Windows";
        const string TmpFontPath = "Assets/Fonts/NotoSansCJKsc-Regular SDF.asset";

        // ------------------------------------------------------------------ 菜单

        [MenuItem("D225/流程/1. 生成流程界面预制体", priority = 30)]
        public static void BuildFlowMenuMenu()
        {
            string report = BuildFlowMenu();
            Debug.Log(report);
            EditorUtility.DisplayDialog("流程界面预制体", report, "好");
        }

        [MenuItem("D225/流程/2. 生成主菜单场景 Scene_Boot", priority = 31)]
        public static void BuildBootSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string report = BuildBootScene();
            Debug.Log(report);
            EditorUtility.DisplayDialog("主菜单场景", report, "好");
        }

        [MenuItem("D225/流程/3. 写入 Build Settings（Boot → A → B → C）", priority = 32)]
        public static void WriteBuildSettingsMenu()
        {
            string report = WriteBuildSettings();
            Debug.Log(report);
            EditorUtility.DisplayDialog("Build Settings", report, "好");
        }

        [MenuItem("D225/流程/9. 构建 Windows 版", priority = 39)]
        public static void BuildWindowsMenu()
        {
            string report = BuildWindows();
            Debug.Log(report);
            EditorUtility.DisplayDialog("Windows 构建", report, "好");
        }

        /// <summary>批处理入口：预制体 → 主菜单场景 → Build Settings。</summary>
        public static void BuildAll()
        {
            Debug.Log("[D225.流程] ===== 流程资源生成开始 =====");
            Debug.Log(BuildFlowMenu());
            Debug.Log(BuildBootScene());
            Debug.Log(WriteBuildSettings());
            Debug.Log("[D225.流程] ===== 流程资源生成结束 =====");
        }

        // ------------------------------------------------------------------ 1. 界面预制体

        /// <summary>从成员5 已验收的 UI 测试场景抽出 Canvas，补转场黑幕与结算页，生成流程界面预制体。</summary>
        public static string BuildFlowMenu()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("【生成 D225_FlowMenu.prefab】");

            if (!File.Exists(UiSourceScene))
            {
                log.AppendLine("找不到 " + UiSourceScene + "，无法生成。");
                return log.ToString();
            }

            Scene source = EditorSceneManager.OpenScene(UiSourceScene, OpenSceneMode.Single);

            UiRouter router = UnityEngine.Object.FindObjectOfType<UiRouter>(true);
            if (router == null)
            {
                log.AppendLine("UI 场景里没有 UiRouter（成员5 的界面路由），无法生成。");
                return log.ToString();
            }

            GameObject uiRoot = router.transform.root.gameObject;
            Canvas canvas = uiRoot.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                canvas = router.GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                log.AppendLine("UiRouter 不在 Canvas 下，无法生成转场黑幕。");
                return log.ToString();
            }

            log.AppendLine("界面根节点：" + uiRoot.name + "；Canvas：" + canvas.name);

            EventSystem eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>(true);

            // ---- 组装预制体根
            var prefabRoot = new GameObject("D225_FlowMenu");
            uiRoot.transform.SetParent(prefabRoot.transform, true);

            if (eventSystem != null && !eventSystem.transform.IsChildOf(prefabRoot.transform))
            {
                eventSystem.transform.SetParent(prefabRoot.transform, true);
                log.AppendLine("EventSystem 已并入预制体。");
            }

            // ---- HUD 依赖场景里的玩家/背包引用，菜单预制体里没有数据源，移除避免空引用
            D225Hud hud = uiRoot.GetComponentInChildren<D225Hud>(true);
            if (hud != null)
            {
                UnityEngine.Object.DestroyImmediate(hud);
                log.AppendLine("已移除 HUD 组件（局内 HUD 由成员5 在各关卡场景接入）。");
            }

            // ---- 转场黑幕
            CanvasGroup fade = EnsureFadeOverlay(canvas.transform);
            log.AppendLine("转场黑幕：" + (fade != null ? "已生成" : "失败"));

            // ---- 结算页
            GameObject endingOverlay = BuildEndingPage(canvas.transform, router);
            log.AppendLine("结算页：" + (endingOverlay != null ? "已生成" : "失败"));

            // ---- 写入 UiRouter 的私有序列化字段（不修改其脚本）
            var routerSo = new SerializedObject(router);
            SetObject(routerSo, "fadeGroup", fade);
            SetBool(routerSo, "manageTimeScale", false);   // 交给 GameManager 统一管 timeScale
            SetBool(routerSo, "startInMainMenu", true);
            SetObject(routerSo, "endingOverlay", endingOverlay);
            routerSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 适配器
            var adapter = prefabRoot.AddComponent<UiRouterFlowAdapter>();
            var adapterSo = new SerializedObject(adapter);
            SetObject(adapterSo, "fadeGroup", fade);
            adapterSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 保存预制体
            EnsureFolder(PrefabsDir);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, FlowMenuPrefabPath);
            log.AppendLine(saved != null
                ? "已保存 " + FlowMenuPrefabPath
                : "保存失败：" + FlowMenuPrefabPath);

            // ---- 丢弃对成员5 场景的所有内存改动（不保存）
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            log.AppendLine("已丢弃对 " + UiSourceScene + " 的临时改动（未保存原场景）。");
            return log.ToString();
        }

        static CanvasGroup EnsureFadeOverlay(Transform canvas)
        {
            Transform existing = canvas.Find("Fade");
            if (existing != null)
            {
                CanvasGroup old = existing.GetComponent<CanvasGroup>();
                if (old != null)
                {
                    return old;
                }
            }

            var go = new GameObject("Fade", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(canvas, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            go.transform.SetAsLastSibling();
            return group;
        }

        static GameObject BuildEndingPage(Transform canvas, UiRouter router)
        {
            GameObject existing = canvas.Find("EndingOverlay")?.gameObject;
            if (existing != null)
            {
                return existing;
            }

            var page = new GameObject("EndingOverlay", typeof(RectTransform));
            page.transform.SetParent(canvas, false);

            var pageRect = page.GetComponent<RectTransform>();
            pageRect.anchorMin = Vector2.zero;
            pageRect.anchorMax = Vector2.one;
            pageRect.offsetMin = Vector2.zero;
            pageRect.offsetMax = Vector2.zero;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(page.transform, false);
            Stretch(background.GetComponent<RectTransform>());
            var bgImage = background.GetComponent<Image>();
            bgImage.color = new Color(0.03f, 0.035f, 0.05f, 0.96f);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);

            CreateLabel(page.transform, "Title", "撤离成功", font, 72f, new Vector2(0f, 60f));
            CreateLabel(page.transform, "Body", "225 全员撤出宿舍楼。", font, 40f, new Vector2(0f, -30f));

            var buttonGo = new GameObject("BackToMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(page.transform, false);
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(320f, 88f);
            buttonRect.anchoredPosition = new Vector2(0f, -150f);

            var buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.18f, 0.2f, 0.26f, 0.95f);

            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, router.ConfirmBackToMenu);

            CreateLabel(buttonGo.transform, "Label", "返回主菜单", font, 34f, Vector2.zero, stretchToParent: true);

            page.SetActive(false);
            return page;
        }

        static void CreateLabel(Transform parent, string name, string text, TMP_FontAsset font,
                                float size, Vector2 position, bool stretchToParent = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            if (stretchToParent)
            {
                Stretch(rect);
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(900f, 120f);
                rect.anchoredPosition = position;
            }

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }

            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.92f, 0.93f, 0.95f, 1f);
            label.raycastTarget = false;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ 2. 主菜单场景

        /// <summary>生成 Scene_Boot：只有相机与流程根节点，菜单 UI 由预制体在运行时实例化。</summary>
        public static string BuildBootScene()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("【生成 " + BootScenePath + "】");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlowMenuPrefabPath);
            if (prefab == null)
            {
                log.AppendLine("缺少 " + FlowMenuPrefabPath + "：请先执行「1. 生成流程界面预制体」。");
                return log.ToString();
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 1f);
            camera.nearClipPlane = -50f;
            camera.farClipPlane = 100f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var flowRoot = new GameObject(GameBootstrap.RootName);
            var manager = flowRoot.AddComponent<GameManager>();
            var so = new SerializedObject(manager);
            SetObject(so, "flowMenuPrefab", prefab);
            SetBool(so, "startInMainMenu", true);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootScenePath);

            log.AppendLine("已保存 " + BootScenePath + "（含 GameManager，运行时 DontDestroyOnLoad）。");
            log.AppendLine("菜单界面由 " + FlowMenuPrefabPath + " 在运行时实例化。");
            return log.ToString();
        }

        // ------------------------------------------------------------------ 3. Build Settings

        /// <summary>Scene_Boot 放到第 0 位，其后按 A → B → C 补齐；其它场景保持原样。</summary>
        public static string WriteBuildSettings()
        {
            var log = new System.Text.StringBuilder("【Build Settings】\n");
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // 先移除旧的 boot 条目，再插到最前，避免重复
            list.RemoveAll(s => string.Equals(s.path, BootScenePath, StringComparison.OrdinalIgnoreCase));

            if (!File.Exists(BootScenePath))
            {
                log.AppendLine("缺少 " + BootScenePath + "：请先执行「2. 生成主菜单场景」。");
            }

            list.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));

            foreach (RegionInfo region in RegionCatalog.All)
            {
                string path = "Assets/Scenes/" + region.SceneName + ".unity";
                if (!File.Exists(path))
                {
                    log.AppendLine("缺少关卡场景：" + path);
                    continue;
                }

                if (list.Any(s => string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                list.Add(new EditorBuildSettingsScene(path, true));
                log.AppendLine("新增：" + path);
            }

            EditorBuildSettings.scenes = list.ToArray();

            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                EditorBuildSettingsScene entry = EditorBuildSettings.scenes[i];
                log.AppendLine("  " + i + ". " + entry.path + (entry.enabled ? "" : "（未勾选）"));
            }

            log.AppendLine(EditorBuildSettings.scenes.Length > 0 &&
                           string.Equals(EditorBuildSettings.scenes[0].path, BootScenePath,
                                         StringComparison.OrdinalIgnoreCase)
                ? "启动场景 = Scene_Boot，正确。"
                : "警告：启动场景不是 Scene_Boot。");
            return log.ToString();
        }

        // ------------------------------------------------------------------ 9. Windows 构建

        /// <summary>构建 Windows 64 位包（T12）；输出目录已被 .gitignore 忽略。</summary>
        public static string BuildWindows()
        {
            var log = new System.Text.StringBuilder("【Windows 构建】\n");

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                log.AppendLine("Build Settings 里没有勾选的场景。");
                return log.ToString();
            }

            Directory.CreateDirectory(BuildDir);
            string exePath = Path.Combine(BuildDir, "D225.exe");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            log.AppendLine("结果：" + report.summary.result);
            log.AppendLine("产物：" + exePath);
            log.AppendLine("大小：" + (report.summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB");
            log.AppendLine("用时：" + report.summary.totalTime);
            return log.ToString();
        }

        // ------------------------------------------------------------------ 工具

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void SetObject(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[D225.流程] 字段不存在，跳过：" + field);
                return;
            }

            property.objectReferenceValue = value;
        }

        static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[D225.流程] 字段不存在，跳过：" + field);
                return;
            }

            property.boolValue = value;
        }
    }
}
