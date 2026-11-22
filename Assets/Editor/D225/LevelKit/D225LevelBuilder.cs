// D225LevelBuilder.cs —— 三段正式关卡场景生成器（成员3 · 彭靖毅）
//
// 用法（Unity 菜单）：
//   D225/关卡/1. 检查关卡资源          → 先看缺什么 Prefab / 图 / 运行时脚本
//   D225/关卡/2. 生成关卡场景          → 生成并保存 A/B/C 三段 + 224/226/222 三间邻室
//   D225/关卡/3. 写入 Build Settings   → 把 6 个场景追加进 Build Settings（A 为启动场景）
//   D225/关卡/9. 清理辅助色块          → 构建前删掉 Gizmo_* 预览色块
//
// 命令行（CI / 免开界面）：
//   Unity.exe -batchmode -quit -projectPath <工程> -executeMethod D225.Level.EditorTools.D225LevelBuilder.BuildAll -logFile -
//
// 设计约束（见 docs/关卡设计说明.md）：
//   · 只摆位、只建碰撞/触发器/空锚点，不写交互逻辑、不改 Prefabs/*。
//   · 坐标唯一来源是 D225LevelLayoutData.g.cs（由 d225-work/level_spec.py 生成，勿手改）。
//   · 生成是「重建」：同名场景文件会被覆盖，先在版本控制里留好。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace D225.Level.EditorTools
{
    public static class D225LevelBuilder
    {
        // ---------------------------------------------------------------- 常量

        const string ScenesDir = "Assets/Scenes";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string WhiteSquarePath = "Assets/Art/Placeholder/D225_WhiteSquare.png";
        const string VisionLayerName = "VisionBlocker";

        // 与 D225LevelLayoutData 保持一致（此处只做兜底，数值以数据文件为准）
        static readonly Color CameraClear = new Color(0.02f, 0.025f, 0.03f, 1f);
        static readonly Color DecorTint = new Color(0.16f, 0.17f, 0.20f, 1f);
        static readonly Color VisionTint = new Color(0.30f, 0.78f, 0.86f, 0.22f);
        static readonly Color CheckpointTint = new Color(0.25f, 0.85f, 0.45f, 0.85f);
        static readonly Color WaypointTint = new Color(0.95f, 0.42f, 0.55f, 0.85f);
        static readonly Color SpawnTint = new Color(0.35f, 0.72f, 0.95f, 0.9f);
        // 门禁状态灯的世界高度：走廊图门框顶 +0.81、宿舍图门框顶 +1.05，取 1.35 让灯落在两图门框上方的空白墙面。
        const float LampWorldY = 1.12f;

        // ---------------------------------------------------------------- 菜单

        [MenuItem("D225/关卡/1. 检查关卡资源", priority = 10)]
        public static void CheckAssetsMenu()
        {
            var report = CheckAssets();
            Debug.Log(report);
            EditorUtility.DisplayDialog("关卡资源检查", report, "好");
        }

        [MenuItem("D225/关卡/2. 生成关卡场景（A/B/C + 三间邻室）", priority = 11)]
        public static void BuildAllScenesMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[D225] 已取消生成（当前场景未保存）。");
                return;
            }
            var report = BuildAllScenes();
            Debug.Log(report);
            EditorUtility.DisplayDialog("关卡生成完成", report, "好");
        }

        [MenuItem("D225/关卡/3. 写入 Build Settings（6 个场景）", priority = 12)]
        public static void WriteBuildSettingsMenu()
        {
            var report = WriteBuildSettings();
            Debug.Log(report);
            EditorUtility.DisplayDialog("Build Settings", report, "好");
        }

        [MenuItem("D225/关卡/9. 清理辅助色块（Gizmo_*）", priority = 20)]
        public static void RemoveGizmosMenu()
        {
            int n = RemoveGizmos();
            EditorUtility.DisplayDialog("清理辅助色块", $"已删除 {n} 个 Gizmo_* 预览色块。", "好");
        }

        /// <summary>批处理入口：检查 → 建层 → 生成三场景 → 写 Build Settings。</summary>
        public static void BuildAll()
        {
            Debug.Log("[D225] ===== 关卡批量生成开始 =====");
            Debug.Log(CheckAssets());
            EnsureVisionLayer();
            Debug.Log(BuildAllScenes());
            Debug.Log(WriteBuildSettings());
            Debug.Log("[D225] ===== 关卡批量生成结束 =====");
        }

        // ---------------------------------------------------------------- 资源检查

        static string CheckAssets()
        {
            var missing = new List<string>();
            var seen = new HashSet<string>();

            foreach (var e in D225LevelLayoutData.Entries)
            {
                if (string.IsNullOrEmpty(e.AssetPath)) continue;
                if (!seen.Add(e.AssetPath)) continue;
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(e.AssetPath) == null)
                    missing.Add($"  · {e.AssetPath}（{e.Scene} / {e.Name}）");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                missing.Add($"  · {PlayerPrefabPath}（玩家，成员1 提供）");
            if (FindTypeBySimpleName("CameraFollow") == null)
                missing.Add("  · 脚本 CameraFollow（成员1 提供，用于相机跟随；缺失时相机将固定居中）");
            if (FindTypeBySimpleName("SceneDoor") == null)
                missing.Add("  · 脚本 SceneDoor（Assets/Scripts/Level/SceneDoor.cs，场景门交互；缺失时门只是空触发区）");
            if (FindTypeBySimpleName("SceneSpawnPoint") == null)
                missing.Add("  · 脚本 SceneSpawnPoint（Assets/Scripts/Level/D225SceneRouter.cs，跨场景落点；缺失时玩家不会换位置）");
            if (LayerMask.NameToLayer(VisionLayerName) < 0)
                missing.Add($"  · Layer「{VisionLayerName}」（生成时会自动创建）");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("【D225 关卡资源检查】");
            if (missing.Count == 0) sb.AppendLine("全部就绪，可以生成场景。");
            else
            {
                sb.AppendLine($"缺失 {missing.Count} 项：");
                foreach (var m in missing) sb.AppendLine(m);
            }
            if (AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath) == null)
                sb.AppendLine("提示：占位白方块缺失，装饰色块会退化为运行时临时 sprite（不会随场景保存），请尽快补图。");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 场景生成

        /// <summary>生成全部三段场景并保存，返回报告。</summary>
        public static string BuildAllScenes()
        {
            EnsureVisionLayer();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("【D225 关卡生成】");
            if (!AssetDatabase.IsValidFolder(ScenesDir))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            foreach (var spec in D225LevelLayoutData.Scenes)
            {
                var entries = D225LevelLayoutData.Entries
                    .Where(e => e.Scene == spec.Key)
                    .ToArray();
                var stats = BuildOneScene(spec, entries);
                sb.AppendLine($"· {spec.Title}：{spec.ScenePath}");
                foreach (var kv in stats) sb.AppendLine($"    {kv.Key,-12} {kv.Value}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("完成。请按 docs/关卡设计说明.md §10 逐条手动验收。");
            return sb.ToString();
        }

        static Dictionary<string, int> BuildOneScene(D225LevelLayoutData.SceneSpec spec,
                                                     D225LevelLayoutData.Entry[] entries)
        {
            var counts = new Dictionary<string, int>();
            void Bump(string k) { counts[k] = counts.TryGetValue(k, out var v) ? v + 1 : 1; }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject(Path.GetFileNameWithoutExtension(spec.ScenePath));
            root.transform.position = Vector3.zero;

            var white = LoadWhiteSquare();
            GameObject playerGo = null;

            // ---- 逐条创建（按组归档，Scene 面板整洁）
            foreach (var e in entries)
            {
                var group = GetGroup(root.transform, e.Group);
                GameObject go = null;

                switch (e.ObjectKind)
                {
                    case D225LevelLayoutData.Kind.Background:
                        go = CreateBackground(e);
                        break;
                    case D225LevelLayoutData.Kind.Collider:
                        go = CreateCollider(e);
                        break;
                    case D225LevelLayoutData.Kind.Decor:
                        go = CreateDecor(e, white);
                        break;
                    case D225LevelLayoutData.Kind.Vision:
                        go = CreateVision(e, white);
                        break;
                    case D225LevelLayoutData.Kind.Trigger:
                        go = CreateTrigger(e);
                        break;
                    case D225LevelLayoutData.Kind.Prefab:
                        go = CreatePrefabInstance(e);
                        break;
                    case D225LevelLayoutData.Kind.Checkpoint:
                        go = CreateMarker(e, CheckpointTint, "Gizmo_CP");
                        Bump("检查点");
                        break;
                    case D225LevelLayoutData.Kind.Waypoint:
                        go = CreateMarker(e, WaypointTint, "Gizmo_WP");
                        Bump("巡逻路点");
                        break;
                    case D225LevelLayoutData.Kind.Hint:
                        go = CreateHint(e);
                        break;
                    case D225LevelLayoutData.Kind.Door:
                        go = CreateDoor(e, white);
                        break;
                    case D225LevelLayoutData.Kind.Spawn:
                        go = CreateSpawn(e);
                        break;
                }

                if (go == null) continue;
                go.transform.SetParent(group, false);
                // 父节点已在原点，local ≈ world；这里用 world 覆盖一次防止误差
                go.transform.position = new Vector3(e.X, e.Y, 0f);
                Bump(e.ObjectKind.ToString());
            }

            // ---- 玩家（成员1 的 Prefab，只实例化不改）
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerGo.name = "Player";
                playerGo.transform.SetParent(GetGroup(root.transform, "Player"), false);
                playerGo.transform.position =
                    new Vector3(spec.EntryX, D225LevelLayoutData.PlayerStandY, 0f);
                Bump("玩家");
            }
            else
            {
                Debug.LogWarning($"[D225] 缺少 {PlayerPrefabPath}，{spec.Key} 区没有玩家。");
            }

            // ---- 相机（成员1 的 CameraFollow 若存在则挂上并设边界）
            var camGo = CreateCamera(spec, playerGo != null ? playerGo.transform : null);
            Bump("相机");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, spec.ScenePath);

            counts["场景宽度"] = (int)spec.Width;
            return counts;
        }

        // ---------------------------------------------------------------- 各类物体

        static Transform GetGroup(Transform root, string group)
        {
            if (string.IsNullOrEmpty(group)) group = "Misc";
            var existing = root.Find(group);
            if (existing != null) return existing;
            var go = new GameObject(group);
            go.transform.SetParent(root, false);
            go.transform.position = Vector3.zero;
            return go.transform;
        }

        static GameObject CreateBackground(D225LevelLayoutData.Entry e)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.AssetPath);
            if (sprite == null)
            {
                Debug.LogError($"[D225] 背景图缺失：{e.AssetPath}（{e.Scene} / {e.Name}）");
                return null;
            }
            var go = new GameObject(e.Name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = e.Sorting;
            go.transform.localScale = new Vector3(e.ScaleX, e.ScaleY, 1f);
            return go;
        }

        static GameObject CreateCollider(D225LevelLayoutData.Entry e)
        {
            var go = new GameObject(e.Name);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(e.W, e.H);
            bc.offset = Vector2.zero;
            return go;
        }

        static GameObject CreateDecor(D225LevelLayoutData.Entry e, Sprite white)
        {
            var go = new GameObject(e.Name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = TintFor(e.Name);
            sr.sortingOrder = e.Sorting;
            FitToSize(go, white, e.W, e.H);
            return go;
        }

        static GameObject CreateVision(D225LevelLayoutData.Entry e, Sprite white)
        {
            var go = new GameObject(e.Name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = VisionTint;
            sr.sortingOrder = e.Sorting;

            var bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(e.W, e.H);

            var layer = LayerMask.NameToLayer(VisionLayerName);
            if (layer >= 0) go.layer = layer;
            FitToSize(go, white, e.W, e.H);
            return go;
        }

        static GameObject CreateTrigger(D225LevelLayoutData.Entry e)
        {
            var go = new GameObject(e.Name);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(e.W, e.H);
            // 场景切换组件由成员6 挂到本物体上（按 GameObject 名字映射目标场景）
            return go;
        }

        static GameObject CreatePrefabInstance(D225LevelLayoutData.Entry e)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.AssetPath);
            if (prefab == null)
            {
                Debug.LogError($"[D225] Prefab 缺失：{e.AssetPath}（{e.Scene} / {e.Name}）");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = e.Name;
            go.transform.localScale = new Vector3(e.ScaleX, e.ScaleY, 1f);
            return go;
        }

        /// <summary>给辅助色块挂上运行时隐藏组件（编辑器里照常可见）。</summary>
        static void HideAtRuntime(GameObject target)
        {
            var t = FindTypeBySimpleName("D225EditorGizmo");
            if (t != null && target.GetComponent(t) == null) target.AddComponent(t);
        }

        static GameObject CreateMarker(D225LevelLayoutData.Entry e, Color tint, string prefix)
        {
            // 空锚点（给成员6 存档系统收集）+ 仅编辑器可见的预览色块
            var go = new GameObject(e.Name);
            var vis = new GameObject($"{prefix}_{e.Name}");
            vis.transform.SetParent(go.transform, false);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sprite = LoadWhiteSquare();
            sr.color = tint;
            sr.sortingOrder = 88;
            vis.transform.localScale = new Vector3(0.14f, 0.14f, 1f);
            vis.transform.localPosition = Vector3.zero;
            HideAtRuntime(vis);
            return go;
        }

        static GameObject CreateHint(D225LevelLayoutData.Entry e)
        {
            var go = new GameObject(e.Name);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(e.W <= 0 ? 1.6f : e.W, e.H <= 0 ? 2.4f : e.H);
            return go;
        }

        /// <summary>
        /// 场景门：墙上的可交互区域，挂运行时脚本 SceneDoor（成员3 的临时实现）。
        /// 正式版若成员4 出自己的门交互物，把这个方法换掉即可，坐标与物体名不用动。
        /// </summary>
        static GameObject CreateDoor(D225LevelLayoutData.Entry e, Sprite white)
        {
            // 门在背景图里已经画好了，不叠任何色块：只留一个不可见的触发区 + SceneDoor 组件。
            // 玩家走近看到 [E] 提示即可；编辑器里选中门对象能看到 Gizmos 线框（SceneDoor.OnDrawGizmosSelected）。
            var go = new GameObject(e.Name);

            var bc = go.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(e.W, e.H);

            var type = FindTypeBySimpleName("SceneDoor");
            if (type == null)
            {
                Debug.LogError($"[D225] 找不到 SceneDoor 脚本（应为 Assets/Scripts/Level/SceneDoor.cs），" +
                               $"{e.Scene} / {e.Name} 只生成了空触发区。");
                return go;
            }

            var comp = go.AddComponent(type);
            var so = new SerializedObject(comp);
            SetStr(so, "doorPlate", e.DoorPlate);
            SetStr(so, "sceneName", e.DoorScene);
            SetStr(so, "spawnId", e.DoorSpawn);
            SetStr(so, "doorId", e.Name);
            var lp = so.FindProperty("locked");
            if (lp != null && lp.propertyType == SerializedPropertyType.Boolean) lp.boolValue = e.DoorLocked;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!string.IsNullOrEmpty(e.DoorFlag))
            {
                var f = type.GetField("requiredFlag",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null && f.FieldType.IsEnum)
                {
                    try { f.SetValue(comp, Enum.Parse(f.FieldType, e.DoorFlag)); }
                    catch { Debug.LogWarning($"[D225] {e.Name} 的 requiredFlag「{e.DoorFlag}」不是合法枚举值。"); }
                }
            }

            // 门上不盖色块；在门框上方的空白墙面点一盏小指示灯（门禁状态灯），
            // 位置固定在 y=1.35：走廊图门框顶 +0.81、宿舍图门框顶 +1.05，两处都在灯下方，不会压到门。
            var lamp = new GameObject("Lamp");
            lamp.transform.SetParent(go.transform, false);
            lamp.transform.localPosition = new Vector3(0f, LampWorldY - e.Y, 0f);
            var lsr = lamp.AddComponent<SpriteRenderer>();
            lsr.sprite = white;
            lsr.color = e.DoorLocked
                ? new Color(0.58f, 0.24f, 0.22f, 0.85f)
                : new Color(0.30f, 0.56f, 0.46f, 0.85f);
            lsr.sortingOrder = e.Sorting;
            lamp.transform.localScale = new Vector3(0.36f, 0.14f, 1f);
            return go;
        }

        /// <summary>出生点：只挂 SceneSpawnPoint（空物体 + 编辑器预览色块），无碰撞。</summary>
        static GameObject CreateSpawn(D225LevelLayoutData.Entry e)
        {
            var go = new GameObject(e.Name);

            var type = FindTypeBySimpleName("SceneSpawnPoint");
            if (type == null)
            {
                Debug.LogError($"[D225] 找不到 SceneSpawnPoint 脚本，{e.Name} 不会把玩家挪过来。");
            }
            else
            {
                var comp = go.AddComponent(type);
                var so = new SerializedObject(comp);
                SetStr(so, "spawnId", string.IsNullOrEmpty(e.ExtraValue) ? e.Name : e.ExtraValue);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var vis = new GameObject("Gizmo_Spawn_" + e.Name);
            vis.transform.SetParent(go.transform, false);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sprite = LoadWhiteSquare();
            sr.color = SpawnTint;
            sr.sortingOrder = 88;
            vis.transform.localScale = new Vector3(0.1f, 0.5f, 1f);
            HideAtRuntime(vis);
            return go;
        }

        static void SetStr(SerializedObject so, string prop, string value)
        {
            var p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.String) p.stringValue = value ?? "";
        }

        static GameObject CreateCamera(D225LevelLayoutData.SceneSpec spec, Transform player)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = D225LevelLayoutData.CameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CameraClear;
            cam.nearClipPlane = -50f;
            cam.farClipPlane = 100f;
            go.AddComponent<AudioListener>();

            float halfView = D225LevelLayoutData.CameraSize * 16f / 9f * 0.5f; // 16:9 下的半视宽
            float minX = halfView;
            float maxX = Mathf.Max(halfView, spec.Width - halfView);
            float startX = Mathf.Clamp(spec.EntryX, minX, maxX);
            go.transform.position = new Vector3(startX, 0f, -10f);

            // 成员1 的相机跟随脚本：存在就用，按字段名尝试写入边界
            var type = FindTypeBySimpleName("CameraFollow");
            if (type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                var comp = go.AddComponent(type) as MonoBehaviour;
                var so = new SerializedObject(comp);
                TrySetFloat(so, "smoothTime", 0.2f);
                TrySetFloat(so, "leftBoundary", 0f);
                TrySetFloat(so, "rightBoundary", spec.Width);
                TrySetFloat(so, "fixedY", 0f);
                var targetProp = so.FindProperty("target") ?? so.FindProperty("followTarget");
                if (targetProp != null &&
                    targetProp.propertyType == SerializedPropertyType.ObjectReference && player != null)
                    targetProp.objectReferenceValue = player;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[D225] 未找到 CameraFollow，相机固定在本区中点；" +
                                 "玩家离开首屏后将看不到角色。请成员1 提供脚本。");
                go.transform.position = new Vector3(spec.Width * 0.5f, 0f, -10f);
            }
            return go;
        }

        // ---------------------------------------------------------------- 工具

        static Sprite LoadWhiteSquare()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            if (sprite != null) return sprite;

            // 退化：1×1 白图（运行时 sprite 不会随场景保存，仅用于预览）
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            Debug.LogWarning("[D225] 缺少占位白方块，使用临时 1×1 sprite（不会保存进场景）。");
            return s;
        }

        static void FitToSize(GameObject go, Sprite sprite, float w, float h)
        {
            if (sprite == null) return;
            var size = sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return;
            go.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
        }

        /// <summary>按名字生成稳定的深色灰阶，让相邻装饰块能区分开。</summary>
        static Color TintFor(string name)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (var c in name) { hash ^= c; hash *= 16777619u; }
                float v = 0.10f + (hash % 1000) / 1000f * 0.16f; // 0.10 ~ 0.26
                return new Color(v, v * 1.04f, v * 1.14f, 1f);
            }
        }

        static void TrySetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.Float)
                p.floatValue = value;
        }

        static Type FindTypeBySimpleName(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                catch { continue; }

                foreach (var t in types)
                    if (t != null && t.Name == simpleName && !t.IsAbstract)
                        return t;
            }
            return null;
        }

        // ---------------------------------------------------------------- Layer

        static void EnsureVisionLayer()
        {
            if (LayerMask.NameToLayer(VisionLayerName) >= 0) return;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[D225] 读不到 TagManager.asset，请手动创建 Layer「VisionBlocker」。");
                return;
            }
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            if (layers == null) return;

            for (int i = 8; i < layers.arraySize; i++) // 0~7 是内置层
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = VisionLayerName;
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[D225] 已创建 Layer「{VisionLayerName}」(index {i})。请告知成员2 在视线 Raycast 中包含该层。");
                    return;
                }
            }
            Debug.LogError("[D225] 没有空闲 Layer 槽位，请手动腾一个给「VisionBlocker」。");
        }

        // ---------------------------------------------------------------- Build Settings

        static string WriteBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var added = new List<string>();
            foreach (var spec in D225LevelLayoutData.Scenes)
            {
                if (list.Any(s => string.Equals(s.path, spec.ScenePath, StringComparison.OrdinalIgnoreCase)))
                    continue;
                list.Add(new EditorBuildSettingsScene(spec.ScenePath, true));
                added.Add(spec.ScenePath);
            }
            EditorBuildSettings.scenes = list.ToArray();

            var sb = new System.Text.StringBuilder("【Build Settings】\n");
            if (added.Count == 0) sb.AppendLine("三段场景都已在列表中，未改动。");
            else
            {
                sb.AppendLine($"新增 {added.Count} 个场景：");
                foreach (var a in added) sb.AppendLine("  · " + a);
            }
            sb.AppendLine("当前顺序：");
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                sb.AppendLine($"  {i}. {EditorBuildSettings.scenes[i].path}" +
                              (EditorBuildSettings.scenes[i].enabled ? "" : "（未勾选）"));
            sb.AppendLine(EditorBuildSettings.scenes.Length > 0 &&
                          EditorBuildSettings.scenes[0].path == D225LevelLayoutData.Scenes[0].ScenePath
                ? "启动场景 = 区域A，正确。"
                : "警告：启动场景不是区域A，请在 Build Settings 里把 Scene_A_Dorm 拖到第 0 位。");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 清理

        static int RemoveGizmos()
        {
            int n = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid()) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var hits = root.GetComponentsInChildren<Transform>(true)
                        .Where(t => t.name.StartsWith("Gizmo_"))
                        .Select(t => t.gameObject)
                        .ToArray();
                    foreach (var go in hits) { UnityEngine.Object.DestroyImmediate(go); n++; }
                }
            }
            if (n > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[D225] 已清理 {n} 个辅助色块。");
            return n;
        }
    }
}
