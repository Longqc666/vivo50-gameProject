using NUnit.Framework;
using D225.Core;
using D225.Quest;

namespace D225.Core.Tests
{
    /// <summary>
    /// 区域出口规则用例（分工表 6-3；策划案 5.6、T10）：
    /// A 区走到宿舍门就能去 B；B 区必须先开楼梯门才能去 C；C 区没有出口（靠安全区结算）。
    /// </summary>
    public sealed class RegionCatalogTests
    {
        private QuestLog log;

        [SetUp]
        public void SetUp()
        {
            log = new QuestLog();
        }

        [Test]
        public void Catalog_HasThreeRegionsInOrder()
        {
            Assert.AreEqual(3, RegionCatalog.All.Length);
            Assert.AreEqual("A", RegionCatalog.All[0].Id);
            Assert.AreEqual("B", RegionCatalog.All[1].Id);
            Assert.AreEqual("C", RegionCatalog.All[2].Id);
            Assert.AreEqual("Scene_A_Dorm", RegionCatalog.Get("A").SceneName);
        }

        [Test]
        public void Get_IsCaseInsensitive_AndMissingReturnsNull()
        {
            Assert.IsNotNull(RegionCatalog.Get("a"));
            Assert.IsNull(RegionCatalog.Get("Z"));
            Assert.IsFalse(RegionCatalog.TryGet("Z", out RegionInfo region));
            Assert.IsNull(region);
        }

        [Test]
        public void GetBySceneName_FindsRegion()
        {
            Assert.AreEqual("B", RegionCatalog.GetBySceneName("Scene_B_Corridor").Id);
            Assert.IsNull(RegionCatalog.GetBySceneName("Scene_Boot"));
        }

        [Test]
        public void RegionA_LeavesAtDormDoor()
        {
            Assert.IsFalse(RegionCatalog.TryResolveExit("A", RegionCatalog.DormDoorX - 0.1f, log, out _, out _));

            Assert.IsTrue(RegionCatalog.TryResolveExit("A", RegionCatalog.DormDoorX, log,
                                                      out RegionInfo next, out string reason));
            Assert.AreEqual("B", next.Id);
            Assert.IsEmpty(reason);
        }

        [Test]
        public void RegionB_BlockedWithoutStairDoorFlag()
        {
            bool allowed = RegionCatalog.TryResolveExit("B", RegionCatalog.StairDoorX + 0.5f, log,
                                                        out RegionInfo next, out string reason);

            Assert.IsFalse(allowed);
            Assert.IsNull(next);
            StringAssert.Contains("楼梯门还锁着", reason);
        }

        [Test]
        public void RegionB_AllowedOnceStairDoorUnlocked()
        {
            log.SetFlag(PuzzleFlag.StairDoorUnlocked);

            bool allowed = RegionCatalog.TryResolveExit("B", RegionCatalog.StairDoorX, log,
                                                        out RegionInfo next, out _);

            Assert.IsTrue(allowed);
            Assert.AreEqual("C", next.Id);
        }

        [Test]
        public void RegionB_WaitsUntilPlayerReachesDoor()
        {
            log.SetFlag(PuzzleFlag.StairDoorUnlocked);
            Assert.IsFalse(RegionCatalog.TryResolveExit("B", RegionCatalog.StairDoorX - 1f, log,
                                                       out _, out string reason));
            Assert.IsEmpty(reason); // 还没走到，不算「条件不满足」
        }

        [Test]
        public void RegionC_HasNoExit()
        {
            log.SetFlag(PuzzleFlag.ExitUnlocked);
            Assert.IsFalse(RegionCatalog.TryResolveExit("C", 999f, log, out RegionInfo next, out _));
            Assert.IsNull(next);
        }

        [Test]
        public void UnknownRegion_ReportsReason()
        {
            Assert.IsFalse(RegionCatalog.TryResolveExit("X", 100f, log, out _, out string reason));
            StringAssert.Contains("未知区域", reason);
        }

        [Test]
        public void EntrySpawnIds_MatchLevelCheckpointAnchors()
        {
            // 与成员3 关卡数据里的 CP_* 空锚点保持一致
            Assert.AreEqual("CP_A_Spawn", RegionCatalog.Get("A").EntrySpawnId);
            Assert.AreEqual("CP_B_Entry", RegionCatalog.Get("B").EntrySpawnId);
            Assert.AreEqual("CP_C_Entry", RegionCatalog.Get("C").EntrySpawnId);
        }
    }
}
