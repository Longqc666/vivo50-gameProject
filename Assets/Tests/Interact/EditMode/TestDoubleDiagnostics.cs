using NUnit.Framework;
using UnityEngine;

namespace D225.Interact.Tests
{
    /// <summary>
    /// 诊断用例：确认测试替身 <see cref="FakeHider"/> 能被正常挂载与按接口查找。
    /// 这条用例失败通常意味着测试程序集里的 MonoBehaviour 没有被 Unity 正确注册。
    /// </summary>
    public sealed class TestDoubleDiagnostics
    {
        [Test]
        public void FakeHider_IsFoundByInterfaceLookup()
        {
            GameObject go = new GameObject("Hider");
            try
            {
                FakeHider hider = go.AddComponent<FakeHider>();
                Assert.IsNotNull(hider, "AddComponent<FakeHider>() 返回 null");
                Assert.IsNotNull(go.GetComponent<FakeHider>(), "GetComponent<FakeHider> 为空");
                Assert.IsNotNull(go.GetComponent<IHidableActor>(), "GetComponent<IHidableActor> 为空");
                Assert.IsNotNull(go.GetComponentInParent<IHidableActor>(), "GetComponentInParent<IHidableActor> 为空");

                hider.IsCatchLocked = true;
                Assert.IsTrue(hider.IsCatchLocked);
            }
            finally
            {
                TestWorld.Destroy(go);
            }
        }

        [Test]
        public void HidingSpot_ResolvesHiderOnActorRoot()
        {
            TestWorld world = new TestWorld();
            try
            {
                GameObject actor = world.AttachHider(out FakeHider hider, "PlayerRoot");
                HidingSpot spot = world.Attach<HidingSpot>("储物柜");
                spot.SetDisplayName("储物柜");
                spot.Configure(null, false);

                InteractionResult result = spot.Interact(new InteractionContext(actor, world.Inventory, world.Quest));

                Assert.IsTrue(result.Succeeded, "躲藏点没有找到 IHidableActor：" + result.Message);
                Assert.IsTrue(hider.IsHidden);
            }
            finally
            {
                world.Dispose();
            }
        }
    }
}
