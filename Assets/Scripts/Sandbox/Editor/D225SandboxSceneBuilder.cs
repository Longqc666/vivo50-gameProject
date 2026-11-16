using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using D225.Interact;
using D225.Quest;

namespace D225.Sandbox.Editor
{
    /// <summary>
    /// 用两张场景图生成成员4 的联调演示场景（A 区 225 宿舍 + B 区二楼走廊）。
    /// 该场景是手工验收与录屏用的临时场景，正式关卡由成员3 在 Scenes 下制作。
    /// 菜单：D225/生成交互联调场景；命令行：
    /// Unity.exe -batchmode -projectPath . -executeMethod D225.Sandbox.Editor.D225SandboxSceneBuilder.BuildAll -quit
    /// </summary>
    public static class D225SandboxSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/D225_Sandbox_Puzzle.unity";
        private const string DormBgPath = "Assets/Art/Level/D225_BG_Dorm.png";
        private const string CorridorBgPath = "Assets/Art/Level/D225_BG_Corridor.png";
        private const string WhiteSpritePath = "Assets/Art/Placeholder/D225_WhiteSquare.png";
        private const string PrefabFolder = "Assets/Prefabs/Items";

        // 世界尺寸：背景 1920x1080 @100PPU，缩放 0.5 后每个区域 9.6 x 5.4，正好等于 16:9、正交 2.7 的视野
        private const float AreaWidth = 9.6f;
        private const float AreaHeight = 5.4f;
        private const float DormCenterX = 4.8f;
        private const float CorridorCenterX = 14.4f;

        // 按渲染预览校准：两张图的地面线在 y = -1.84 对齐
        private const float FloorY = -1.84f;
        private const float PlayerHeight = 1.6f;
        private const float ChestOffset = 0.85f;

        [MenuItem("D225/生成交互联调场景")]
        public static void BuildFromMenu()
        {
            BuildAll();
            EditorUtility.DisplayDialog(
                "D225 联调场景",
                "已生成：\n" + ScenePath + "\n\n打开场景后按 Play，A/D 移动、E 交互。",
                "好");
        }

        public static void BuildAll()
        {
            EnsureBackgroundImport(DormBgPath);
            EnsureBackgroundImport(CorridorBgPath);

            Sprite dormSprite = LoadSprite(DormBgPath);
            Sprite corridorSprite = LoadSprite(CorridorBgPath);
            Sprite whiteSprite = LoadSprite(WhiteSpritePath);
            if (dormSprite == null || corridorSprite == null || whiteSprite == null)
            {
                throw new FileNotFoundException("缺少背景或占位 Sprite，请先确认 Art/Level 与 Art/Placeholder 下的图片已导入。");
            }

            EnsureFolder("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            CreateBackground("BG_Dorm", dormSprite, new Vector3(DormCenterX, 0f, 0f), new Vector2(0.5f, 0.5f), -100);
            CreateBackground("BG_Corridor", corridorSprite, new Vector3(CorridorCenterX, 0f, 0f), new Vector2(0.5f, 0.5f), -99);
            CreateSeamPillar(whiteSprite);
            CreateBox("Ground", new Vector2(AreaWidth, FloorY - 0.5f), new Vector2(AreaWidth * 2f + 0.4f, 1f));
            CreateBox("Wall_Left", new Vector2(-0.2f, 0f), new Vector2(0.4f, AreaHeight));
            CreateBox("Wall_Right", new Vector2(AreaWidth * 2f + 0.2f, 0f), new Vector2(0.4f, AreaHeight));

            GameObject player = CreatePlayer(whiteSprite, out Inventory inventory, out SandboxInteractor interactor);
            GameObject questRoot = CreateQuestRoot(out PuzzleObjectiveTracker tracker);

            PlaceProps();
            CreateHud(interactor, inventory, tracker);

            var follow = camera.gameObject.AddComponent<SandboxCameraFollow>();
            follow.Configure(player.transform, DormCenterX, CorridorCenterX, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            RegisterScene();
            Debug.Log("[D225] 已生成联调场景 " + ScenePath + "（任务根：" + questRoot.name + "）");
        }

        /// <summary>一次生成场景并输出预览图（批处理入口）。</summary>
        public static void BuildAllWithPreviews()
        {
            BuildAll();
            CapturePreviews();
        }

        /// <summary>生成后把场景渲染成 PNG，便于快速检查背景比例与道具位置（输出在仓库上一级目录）。</summary>
        public static void CapturePreviews()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null)
            {
                throw new MissingComponentException("场景中没有 Main Camera");
            }

            camera.aspect = 16f / 9f;

            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string[] names = { "dorm", "seam", "corridor" };
            float[] positions = { DormCenterX, AreaWidth, CorridorCenterX };

            for (int i = 0; i < names.Length; i++)
            {
                camera.transform.position = new Vector3(positions[i], 0f, -10f);
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                Texture2D texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(root, "_preview_" + names[i] + ".png"), texture.EncodeToPNG());

                Object.DestroyImmediate(texture);
                camera.targetTexture = null;
                RenderTexture.active = null;
            }

            Object.DestroyImmediate(target);
            Debug.Log("[D225] 预览图已输出到 " + root);
        }

        private static Camera CreateCamera()
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(DormCenterX, 0f, -10f);

            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = AreaHeight * 0.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.03f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateBackground(string name, Sprite sprite, Vector3 position, Vector2 scale, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
        }

        private static void CreateSeamPillar(Sprite whiteSprite)
        {
            // 两块背景拼接处用一根深色墙柱遮住接缝，正式关卡由成员3 用门洞/黑场衔接替代
            GameObject go = new GameObject("SeamPillar");
            go.transform.position = new Vector3(AreaWidth, 0f, 0f);
            go.transform.localScale = new Vector3(0.35f, AreaHeight, 1f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = new Color(0.015f, 0.018f, 0.022f);
            renderer.sortingOrder = -50;
        }

        private static void CreateBox(string name, Vector2 center, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.position = center;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static GameObject CreatePlayer(Sprite whiteSprite, out Inventory inventory, out SandboxInteractor interactor)
        {
            GameObject go = new GameObject("Player");
            go.tag = "Player";
            go.transform.position = new Vector3(2.6f, FloorY + PlayerHeight * 0.5f, 0f);
            go.transform.localScale = new Vector3(0.5f, PlayerHeight, 1f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = new Color(0.72f, 0.79f, 0.9f, 0.95f);
            renderer.sortingOrder = 10;

            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            go.AddComponent<SandboxPlayer>();
            interactor = go.AddComponent<SandboxInteractor>();
            inventory = go.AddComponent<Inventory>();
            inventory.ConfigureStartingItems(new ItemData[0]);
            return go;
        }

        private static GameObject CreateQuestRoot(out PuzzleObjectiveTracker tracker)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/D225_QuestController.prefab");
            if (prefab == null)
            {
                throw new FileNotFoundException("缺少 D225_QuestController.prefab，请先执行 D225/生成交互与谜题资源");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new MissingReferenceException("实例化 D225_QuestController 失败");
            }

            instance.name = "QuestController";
            instance.transform.position = Vector3.zero;
            tracker = instance.GetComponent<PuzzleObjectiveTracker>();
            return instance;
        }

        private static void PlaceProps()
        {
            // A 区 225 宿舍：桌面便笺、手电、右侧衣柜躲藏点
            PlaceProp("D225_Clue_DormNote", new Vector3(1.75f, -0.55f, 0f));
            PlaceProp("D225_Item_Flashlight", new Vector3(6.35f, -0.55f, 0f));
            GameObject locker = PlaceProp("D225_Hiding_Locker", new Vector3(6.95f, -0.74f, 0f), new Vector2(0.75f, 2.2f));
            Tint(locker, new Color(0.22f, 0.25f, 0.3f));

            // B 区 二楼走廊：钥匙、保险丝放在窗台；配电箱在窗与门之间；公告栏在布告板；楼梯门在左侧第一扇门
            PlaceProp("D225_Item_StairKey", new Vector3(12.1f, -1.1f, 0f));
            PlaceProp("D225_Item_Fuse", new Vector3(12.9f, -1.1f, 0f));
            PlaceProp("D225_Puzzle_FuseBox", new Vector3(13.7f, -1.0f, 0f), new Vector2(0.6f, 0.7f));
            PlaceProp("D225_Clue_Bulletin", new Vector3(16.6f, -0.15f, 0f), new Vector2(0.85f, 0.6f));
            PlaceProp("D225_Puzzle_StairDoor", new Vector3(10.6f, -0.38f, 0f), new Vector2(1.4f, 3.0f));
        }

        private static void Tint(GameObject instance, Color color)
        {
            SpriteRenderer renderer = instance != null ? instance.GetComponent<SpriteRenderer>() : null;
            if (renderer != null)
            {
                renderer.color = color;
            }
        }

        private static GameObject PlaceProp(string prefabName, Vector3 position, Vector2? scale = null)
        {
            string path = PrefabFolder + "/" + prefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new FileNotFoundException("缺少 Prefab：" + path);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new MissingReferenceException("实例化失败：" + path);
            }

            instance.transform.position = position;
            if (scale.HasValue)
            {
                instance.transform.localScale = new Vector3(scale.Value.x, scale.Value.y, 1f);
            }

            return instance;
        }

        private static void CreateHud(SandboxInteractor interactor, Inventory inventory, PuzzleObjectiveTracker tracker)
        {
            GameObject go = new GameObject("HUD");
            SandboxHud hud = go.AddComponent<SandboxHud>();
            hud.Configure(interactor, inventory, tracker);
        }

        private static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(entry => entry.path == ScenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureBackgroundImport(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("找不到背景图：" + path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = false;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || folder == "Assets" || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
