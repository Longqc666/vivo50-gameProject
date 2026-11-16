using System;
using System.Collections.Generic;
using UnityEngine;
using D225.Interact;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>
    /// 测试世界：一个玩家（带背包）、一份任务进度、一个可复用的交互上下文。
    /// 所有临时对象在 Dispose 时销毁，避免用例互相污染。
    /// </summary>
    public sealed class TestWorld : IDisposable
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public TestWorld()
        {
            Actor = CreateObject("TestPlayer");
            Inventory = Actor.AddComponent<Inventory>();
            Inventory.ConfigureStartingItems(new ItemData[0]);
        }

        public QuestLog Quest { get; } = new QuestLog();

        public GameObject Actor { get; }

        public Inventory Inventory { get; }

        public InteractionContext Context => new InteractionContext(Actor, Inventory, Quest);

        public GameObject CreateObject(string objectName)
        {
            GameObject go = new GameObject(objectName);
            created.Add(go);
            return go;
        }

        public ItemData Item(
            string id,
            string displayName,
            ItemCategory category = ItemCategory.PuzzleItem,
            bool stackable = false,
            PuzzleFlag pickupFlag = PuzzleFlag.None,
            PuzzleFlag inspectFlag = PuzzleFlag.None)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.Configure(id, displayName, displayName + "：测试用说明", category, stackable, pickupFlag, inspectFlag);
            created.Add(item);
            return item;
        }

        public ItemData Clue(string id, string displayName, PuzzleFlag inspectFlag = PuzzleFlag.None)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.Configure(id, displayName, displayName + "：测试用线索文本", ItemCategory.Clue, false, PuzzleFlag.None, inspectFlag);
            created.Add(item);
            return item;
        }

        public T Attach<T>(string objectName = "Target") where T : Component
        {
            return CreateObject(objectName).AddComponent<T>();
        }

        /// <summary>创建一个带 QuestLogProvider 的场景根，测 Escape/出口类组件用。</summary>
        public QuestLogProvider CreateProvider()
        {
            QuestLogProvider provider = CreateObject("QuestRoot").AddComponent<QuestLogProvider>();
            provider.Configure(new PuzzleFlag[0]);
            return provider;
        }

        public GameObject AttachHider(out FakeHider hider, string objectName = "Hider")
        {
            GameObject go = CreateObject(objectName);
            hider = go.AddComponent<FakeHider>();
            return go;
        }

        public static void Destroy(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        public void Dispose()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                Destroy(created[i]);
            }

            created.Clear();
        }
    }
}
