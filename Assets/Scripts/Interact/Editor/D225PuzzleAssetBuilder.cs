using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using D225.Quest;

namespace D225.Interact.Editor
{
    /// <summary>
    /// 一次性生成成员4 模块的物品配置与可交互物 Prefab（可重复执行，资产 GUID 保持稳定）。
    /// 手动：菜单 D225/生成交互与谜题资源；命令行：
    /// Unity.exe -batchmode -projectPath . -executeMethod D225.Interact.Editor.D225PuzzleAssetBuilder.BuildAll -quit
    /// </summary>
    public static class D225PuzzleAssetBuilder
    {
        private const string PlaceholderFolder = "Assets/Art/Placeholder";
        private const string ItemFolder = "Assets/Data/Items";
        private const string PrefabFolder = "Assets/Prefabs/Items";
        private const string WhiteSpritePath = PlaceholderFolder + "/D225_WhiteSquare.png";

        private static Sprite whiteSprite;

        [MenuItem("D225/生成交互与谜题资源")]
        public static void BuildAllFromMenu()
        {
            BuildAll();
            EditorUtility.DisplayDialog(
                "D225 交互与谜题资源",
                "已生成物品配置与 Prefab：\n" + ItemFolder + "\n" + PrefabFolder,
                "好");
        }

        /// <summary>批处理入口：不弹窗、不依赖当前打开的场景。</summary>
        public static void BuildAll()
        {
            EnsureFolder(PlaceholderFolder);
            EnsureFolder(ItemFolder);
            EnsureFolder(PrefabFolder);

            whiteSprite = CreateOrLoadWhiteSprite();

            ItemData flashlight = CreateOrLoadItem(
                ItemFolder + "/D225_Item_Flashlight.asset",
                "item.flashlight",
                "手电筒",
                "抽屉里的旧手电筒，按下开关还能亮。\n找到配电室，就能把走廊的灯修好。",
                ItemCategory.Tool,
                PuzzleFlag.FlashlightAcquired,
                PuzzleFlag.None);

            ItemData fuse = CreateOrLoadItem(
                ItemFolder + "/D225_Item_Fuse.asset",
                "item.fuse",
                "保险丝",
                "设备间工具柜里的备用保险丝，铜丝还没烧断。\n把它装回走廊的配电箱。",
                ItemCategory.PuzzleItem,
                PuzzleFlag.FuseAcquired,
                PuzzleFlag.None);

            ItemData stairKey = CreateOrLoadItem(
                ItemFolder + "/D225_Item_StairKey.asset",
                "item.stair_key",
                "门禁钥匙",
                "一楼值班台抽屉里的门禁钥匙，挂着宿舍楼的旧牌子。\n二楼楼梯门需要它。",
                ItemCategory.Key,
                PuzzleFlag.StairKeyAcquired,
                PuzzleFlag.None);

            ItemData dormNote = CreateOrLoadItem(
                ItemFolder + "/D225_Clue_DormNote.asset",
                "clue.dorm_note",
                "宿舍便笺",
                "「停电前听见配电室那边有响动……手电在抽屉里。」\n配电室在二楼走廊东头。",
                ItemCategory.Clue,
                PuzzleFlag.None,
                PuzzleFlag.DutyNoteRead);

            ItemData bulletin = CreateOrLoadItem(
                ItemFolder + "/D225_Clue_Bulletin.asset",
                "clue.bulletin",
                "走廊公告栏",
                "公告：楼梯门禁已改为备用钥匙管理，钥匙放在一楼值班台抽屉。",
                ItemCategory.Clue,
                PuzzleFlag.None,
                PuzzleFlag.BulletinRead);

            ItemData shiftLog = CreateOrLoadItem(
                ItemFolder + "/D225_Clue_ShiftLog.asset",
                "clue.shift_log",
                "值班记录",
                "值班记录：22:40 楼内跳闸，配电箱保险丝熔断，备件放在设备间工具柜。",
                ItemCategory.Clue,
                PuzzleFlag.None,
                PuzzleFlag.None);

            CreatePickupPrefab("D225_Item_Flashlight", flashlight, new Color(1f, 0.9f, 0.55f), new Vector2(0.45f, 0.45f));
            CreatePickupPrefab("D225_Item_Fuse", fuse, new Color(0.75f, 0.75f, 0.72f), new Vector2(0.35f, 0.35f));
            CreatePickupPrefab("D225_Item_StairKey", stairKey, new Color(0.9f, 0.72f, 0.28f), new Vector2(0.35f, 0.2f));

            CreateCluePrefab("D225_Clue_DormNote", dormNote, new Color(0.92f, 0.9f, 0.82f), new Vector2(0.45f, 0.32f));
            CreateCluePrefab("D225_Clue_Bulletin", bulletin, new Color(0.82f, 0.82f, 0.76f), new Vector2(1.3f, 0.9f));
            CreateCluePrefab("D225_Clue_ShiftLog", shiftLog, new Color(0.9f, 0.88f, 0.8f), new Vector2(0.5f, 0.4f));

            CreateFuseBoxPrefab(fuse);
            CreateStairDoorPrefab(stairKey);
            CreateExitSwitchPrefab();
            CreateHidingSpotPrefab();
            CreateEvacuationZonePrefab();
            CreateQuestControllerPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[D225] 交互与谜题资源生成完成：物品 " + ItemFolder + "，Prefab " + PrefabFolder);
        }

        private static void CreatePickupPrefab(string prefabName, ItemData item, Color color, Vector2 size)
        {
            GameObject root = CreateProp(prefabName, color, size);
            try
            {
                PickupItem pickup = root.AddComponent<PickupItem>();
                pickup.Configure(item);
                SavePrefab(root, PrefabFolder + "/" + prefabName + ".prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateCluePrefab(string prefabName, ItemData clue, Color color, Vector2 size)
        {
            GameObject root = CreateProp(prefabName, color, size);
            try
            {
                InspectableClue inspectable = root.AddComponent<InspectableClue>();
                inspectable.Configure(clue);
                SavePrefab(root, PrefabFolder + "/" + prefabName + ".prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateFuseBoxPrefab(ItemData fuse)
        {
            GameObject root = CreateProp("D225_Puzzle_FuseBox", new Color(0.32f, 0.38f, 0.42f), new Vector2(0.8f, 1f));
            try
            {
                FuseBoxPuzzle box = root.AddComponent<FuseBoxPuzzle>();
                box.SetDisplayName("配电箱");
                box.Configure(fuse.ItemId, fuse.DisplayName, PuzzleFlag.PowerRestored);
                SavePrefab(root, PrefabFolder + "/D225_Puzzle_FuseBox.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateStairDoorPrefab(ItemData key)
        {
            GameObject root = CreateProp("D225_Puzzle_StairDoor", new Color(0.45f, 0.32f, 0.22f), new Vector2(1.1f, 2.4f));
            try
            {
                StairDoorPuzzle door = root.AddComponent<StairDoorPuzzle>();
                door.SetDisplayName("楼梯门禁");
                door.Configure(key.ItemId, key.DisplayName, PuzzleFlag.StairDoorUnlocked);
                SavePrefab(root, PrefabFolder + "/D225_Puzzle_StairDoor.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateExitSwitchPrefab()
        {
            GameObject root = CreateProp("D225_Puzzle_ExitSwitch", new Color(0.72f, 0.22f, 0.2f), new Vector2(0.55f, 0.7f));
            try
            {
                ExitSwitchPuzzle exit = root.AddComponent<ExitSwitchPuzzle>();
                exit.SetDisplayName("出口联动装置");
                exit.Configure(PuzzleFlag.ExitUnlocked);
                SavePrefab(root, PrefabFolder + "/D225_Puzzle_ExitSwitch.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateHidingSpotPrefab()
        {
            GameObject root = CreateProp("D225_Hiding_Locker", new Color(0.26f, 0.42f, 0.56f), new Vector2(1f, 2f));
            try
            {
                GameObject hidePoint = new GameObject("HidePoint");
                hidePoint.transform.SetParent(root.transform, false);
                hidePoint.transform.localPosition = new Vector3(0f, -0.1f, 0f);

                HidingSpot spot = root.AddComponent<HidingSpot>();
                spot.SetDisplayName("储物柜");
                spot.Configure(hidePoint.transform, false, PuzzleFlag.FirstHiding);
                SavePrefab(root, PrefabFolder + "/D225_Hiding_Locker.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateEvacuationZonePrefab()
        {
            GameObject root = CreateProp("D225_Trigger_Evacuation", new Color(0.3f, 1f, 0.5f, 0.22f), new Vector2(3f, 4f));
            try
            {
                BoxCollider2D zoneCollider = root.AddComponent<BoxCollider2D>();
                zoneCollider.isTrigger = true;
                zoneCollider.size = Vector2.one;

                EvacuationZone zone = root.AddComponent<EvacuationZone>();
                zone.Configure(null, "Player");
                SavePrefab(root, PrefabFolder + "/D225_Trigger_Evacuation.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateQuestControllerPrefab()
        {
            GameObject root = new GameObject("D225_QuestController");
            try
            {
                QuestLogProvider provider = root.AddComponent<QuestLogProvider>();
                provider.Configure(new PuzzleFlag[0]);

                PuzzleObjectiveTracker tracker = root.AddComponent<PuzzleObjectiveTracker>();
                tracker.Configure(
                    provider,
                    new[]
                    {
                        new ObjectiveStep { completedFlag = PuzzleFlag.FlashlightAcquired, objective = "调查 225 宿舍桌面，找到手电筒" },
                        new ObjectiveStep { completedFlag = PuzzleFlag.DutyNoteRead, objective = "读一下桌面便笺，确认配电室的位置" },
                        new ObjectiveStep { completedFlag = PuzzleFlag.FuseAcquired, objective = "去二楼设备间找备用保险丝" },
                        new ObjectiveStep { completedFlag = PuzzleFlag.PowerRestored, objective = "把保险丝装回走廊的配电箱，恢复局部供电" },
                        new ObjectiveStep { completedFlag = PuzzleFlag.StairKeyAcquired, objective = "在一楼值班台找到门禁钥匙" },
                        new ObjectiveStep { completedFlag = PuzzleFlag.StairDoorUnlocked, objective = "用钥匙打开二楼楼梯门" },
                        new ObjectiveStep { completedFlag = PuzzleFlag.ExitUnlocked, objective = "到一楼门厅启动出口联动装置" }
                    });
                SavePrefab(root, PrefabFolder + "/D225_QuestController.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ItemData CreateOrLoadItem(
            string assetPath,
            string itemId,
            string displayName,
            string description,
            ItemCategory category,
            PuzzleFlag pickupFlag,
            PuzzleFlag inspectFlag)
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, assetPath);
            }

            item.Configure(itemId, displayName, description, category, false, pickupFlag, inspectFlag, whiteSprite);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static GameObject CreateProp(string name, Color color, Vector2 size)
        {
            GameObject root = new GameObject(name);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = color;
            root.transform.localScale = new Vector3(size.x, size.y, 1f);
            return root;
        }

        private static void SavePrefab(GameObject root, string prefabPath)
        {
            EnsureFolder(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[D225] 生成 Prefab " + prefabPath);
        }

        private static Sprite CreateOrLoadWhiteSprite()
        {
            if (!File.Exists(WhiteSpritePath))
            {
                const int size = 32;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color32[] pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(WhiteSpritePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(WhiteSpritePath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException("占位白色 Sprite 导入失败：" + WhiteSpritePath);
            }

            return sprite;
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
