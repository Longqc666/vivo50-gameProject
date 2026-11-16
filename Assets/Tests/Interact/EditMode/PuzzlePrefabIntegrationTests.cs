using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>
    /// 生成物集成校验：确认 <c>D225/生成交互与谜题资源</c> 产出的 Prefab 引用完整、
    /// 组件参数正确，并且实例化后可以完成一次真实交互，避免「脚本通过但 Prefab 没接好」。
    /// </summary>
    public sealed class PuzzlePrefabIntegrationTests
    {
        private const string PrefabRoot = "Assets/Prefabs/Items/";

        private GameObject player;
        private Inventory inventory;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("TestPlayer");
            inventory = player.AddComponent<Inventory>();
            inventory.ConfigureStartingItems(new ItemData[0]);
        }

        [TearDown]
        public void TearDown()
        {
            TestWorld.Destroy(player);
        }

        private static GameObject LoadPrefab(string fileName)
        {
            string path = PrefabRoot + fileName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "缺少 Prefab：" + path + "（先执行 D225/生成交互与谜题资源）");
            return prefab;
        }

        private static T LoadComponent<T>(string fileName) where T : Component
        {
            GameObject prefab = LoadPrefab(fileName);
            T component = prefab.GetComponent<T>();
            Assert.IsNotNull(component, fileName + " 缺少组件 " + typeof(T).Name);
            return component;
        }

        [Test]
        public void ItemPrefabs_ReferenceExpectedItemData()
        {
            Assert.AreEqual("item.flashlight", LoadComponent<PickupItem>("D225_Item_Flashlight").Item.ItemId);
            Assert.AreEqual("item.fuse", LoadComponent<PickupItem>("D225_Item_Fuse").Item.ItemId);
            Assert.AreEqual("item.stair_key", LoadComponent<PickupItem>("D225_Item_StairKey").Item.ItemId);

            Assert.AreEqual("clue.dorm_note", LoadComponent<InspectableClue>("D225_Clue_DormNote").Clue.ItemId);
            Assert.AreEqual("clue.bulletin", LoadComponent<InspectableClue>("D225_Clue_Bulletin").Clue.ItemId);
            Assert.AreEqual("clue.shift_log", LoadComponent<InspectableClue>("D225_Clue_ShiftLog").Clue.ItemId);
        }

        [Test]
        public void PuzzlePrefabs_HaveExpectedFlagsAndNames()
        {
            FuseBoxPuzzle fuseBox = LoadComponent<FuseBoxPuzzle>("D225_Puzzle_FuseBox");
            Assert.AreEqual(PuzzleFlag.PowerRestored, fuseBox.FlagToRaise);
            Assert.AreEqual("配电箱", fuseBox.DisplayName);

            StairDoorPuzzle door = LoadComponent<StairDoorPuzzle>("D225_Puzzle_StairDoor");
            Assert.AreEqual(PuzzleFlag.StairDoorUnlocked, door.FlagToRaise);
            Assert.AreEqual("楼梯门禁", door.DisplayName);

            ExitSwitchPuzzle exit = LoadComponent<ExitSwitchPuzzle>("D225_Puzzle_ExitSwitch");
            Assert.AreEqual(PuzzleFlag.ExitUnlocked, exit.FlagToRaise);
            Assert.AreEqual("出口联动装置", exit.DisplayName);
        }

        [Test]
        public void HidingLocker_IsReadyToUse()
        {
            HidingSpot locker = LoadComponent<HidingSpot>("D225_Hiding_Locker");

            Assert.AreEqual("储物柜", locker.DisplayName);
            Assert.IsFalse(locker.IsOccupied);
            Assert.IsNotNull(locker.HidePoint);
        }

        [Test]
        public void EvacuationZone_ColliderIsTrigger()
        {
            GameObject prefab = LoadPrefab("D225_Trigger_Evacuation");
            BoxCollider2D zone = prefab.GetComponent<BoxCollider2D>();

            Assert.IsNotNull(zone, "出口触发区缺少 BoxCollider2D");
            Assert.IsTrue(zone.isTrigger, "出口触发区必须是 Trigger");
            Assert.IsNotNull(prefab.GetComponent<EvacuationZone>());
        }

        [Test]
        public void QuestController_ProvidesObjectiveText()
        {
            GameObject instance = Object.Instantiate(LoadPrefab("D225_QuestController"));
            try
            {
                QuestLogProvider provider = instance.GetComponent<QuestLogProvider>();
                PuzzleObjectiveTracker tracker = instance.GetComponent<PuzzleObjectiveTracker>();

                Assert.IsNotNull(provider);
                Assert.IsNotNull(tracker);

                tracker.Refresh();
                Assert.IsFalse(string.IsNullOrEmpty(tracker.CurrentObjective), "初始任务目标不能为空");
                StringAssert.Contains("手电", tracker.CurrentObjective);
            }
            finally
            {
                TestWorld.Destroy(instance);
            }
        }

        [Test]
        public void FuseBoxPrefab_CanRestorePowerAfterAddingRealFuseAsset()
        {
            ItemData fuse = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/D225_Item_Fuse.asset");
            Assert.IsNotNull(fuse, "缺少保险丝 ItemData 资产");

            GameObject instance = Object.Instantiate(LoadPrefab("D225_Puzzle_FuseBox"));
            try
            {
                FuseBoxPuzzle fuseBox = instance.GetComponent<FuseBoxPuzzle>();
                QuestLog quest = new QuestLog();
                InteractionContext context = new InteractionContext(player, inventory, quest);

                Assert.IsFalse(fuseBox.Interact(context).Succeeded, "空手不能修复配电箱");

                inventory.TryAdd(fuse, out _);
                Assert.IsTrue(fuseBox.Interact(context).Succeeded);
                Assert.IsTrue(quest.HasFlag(PuzzleFlag.PowerRestored));
                Assert.IsFalse(inventory.Has(fuse));
            }
            finally
            {
                TestWorld.Destroy(instance);
            }
        }

        [Test]
        public void FlashlightPrefab_CanBePickedOnlyOnce()
        {
            GameObject instance = Object.Instantiate(LoadPrefab("D225_Item_Flashlight"));
            try
            {
                PickupItem pickup = instance.GetComponent<PickupItem>();
                QuestLog quest = new QuestLog();
                InteractionContext context = new InteractionContext(player, inventory, quest);

                Assert.IsTrue(pickup.Interact(context).Succeeded);
                Assert.IsTrue(inventory.Has("item.flashlight"));
                Assert.IsTrue(quest.HasFlag(PuzzleFlag.FlashlightAcquired));
                Assert.IsFalse(pickup.Interact(context).Succeeded);
                Assert.AreEqual(1, inventory.Count("item.flashlight"));
            }
            finally
            {
                TestWorld.Destroy(instance);
            }
        }
    }
}
