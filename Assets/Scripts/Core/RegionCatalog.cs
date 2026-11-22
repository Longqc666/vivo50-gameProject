using System;
using D225.Quest;

namespace D225.Core
{
    /// <summary>一段区域的静态定义：场景名、入口出生点、出口条件。</summary>
    public sealed class RegionInfo
    {
        public RegionInfo(string id, string title, string sceneName, string entrySpawnId)
        {
            Id = id;
            Title = title;
            SceneName = sceneName;
            EntrySpawnId = entrySpawnId;
        }

        /// <summary>区域 ID（A / B / C）。</summary>
        public string Id { get; }

        /// <summary>中文标题，日志与结算用。</summary>
        public string Title { get; }

        /// <summary>Unity 场景名（Build Settings 里的文件名，不带路径）。</summary>
        public string SceneName { get; }

        /// <summary>从上一区进入本区时的出生点 ID。</summary>
        public string EntrySpawnId { get; }

        /// <summary>向右走到这个 X 就离开本区；<see cref="float.PositiveInfinity"/> 表示本区没有出口（如 C 区靠安全区结算）。</summary>
        public float ExitX { get; internal set; } = float.PositiveInfinity;

        /// <summary>离开本区还需要的任务标记；<see cref="PuzzleFlag.None"/> 表示无条件。</summary>
        public PuzzleFlag ExitFlag { get; internal set; } = PuzzleFlag.None;

        /// <summary>条件不满足时给玩家/日志的原因。</summary>
        public string ExitBlockedHint { get; internal set; } = string.Empty;

        /// <summary>出口通往的区域 ID；空表示没有下一区。</summary>
        public string NextRegionId { get; internal set; } = string.Empty;

        public bool HasExit => !float.IsInfinity(ExitX) && !string.IsNullOrEmpty(NextRegionId);

        public override string ToString() => $"{Id} {Title} ({SceneName})";
    }

    /// <summary>
    /// 三段区域的目录与出口规则。坐标来自成员3 的关卡数据
    /// （<c>Assets/Editor/D225/LevelKit/D225LevelLayoutData.g.cs</c>），这里只引用、不修改。
    /// 纯 C#，出口判定可以直接在 EditMode 用例里断言。
    /// </summary>
    public static class RegionCatalog
    {
        /// <summary>流程启动场景名（本模块新建，见 docs/成员6-交付清单.md）。</summary>
        public const string BootSceneName = "Scene_Boot";

        /// <summary>A 区宿舍的走廊门触发区：关卡数据里 Door_ToCorridor 在 x=9、宽 1.2，玩家走到 8.4 即进入。</summary>
        public const float DormDoorX = 8.4f;

        /// <summary>B 区楼梯门：门在 x≈28.1，右墙内表面 28.8，玩家走到 28.3 视为出门。</summary>
        public const float StairDoorX = 28.3f;

        public static readonly RegionInfo[] All =
        {
            new RegionInfo("A", "区域A 225宿舍", "Scene_A_Dorm", "CP_A_Spawn")
            {
                ExitX = DormDoorX,
                ExitFlag = PuzzleFlag.None,
                NextRegionId = "B"
            },
            new RegionInfo("B", "区域B 二楼走廊与设备间", "Scene_B_Corridor", "CP_B_Entry")
            {
                ExitX = StairDoorX,
                ExitFlag = PuzzleFlag.StairDoorUnlocked,
                ExitBlockedHint = "楼梯门还锁着：先用门禁钥匙把楼梯门打开。",
                NextRegionId = "C"
            },
            new RegionInfo("C", "区域C 楼梯间与一楼门厅", "Scene_C_Hall", "CP_C_Entry")
        };

        /// <summary>第一段区域（主菜单「开始游戏」进入这里）。</summary>
        public const string FirstRegionId = "A";

        public static RegionInfo Get(string id)
        {
            foreach (RegionInfo region in All)
            {
                if (string.Equals(region.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return region;
                }
            }

            return null;
        }

        public static bool TryGet(string id, out RegionInfo region)
        {
            region = Get(id);
            return region != null;
        }

        /// <summary>按场景名找区域（场景加载后用它判断「现在在哪一区」）。</summary>
        public static RegionInfo GetBySceneName(string sceneName)
        {
            foreach (RegionInfo region in All)
            {
                if (string.Equals(region.SceneName, sceneName, StringComparison.Ordinal))
                {
                    return region;
                }
            }

            return null;
        }

        /// <summary>
        /// 出口判定（纯函数）：
        /// 玩家位置到达出口线、且所需标记已写入时才允许切区。
        /// 返回 false 时 <paramref name="reason"/> 要么是空串（还没走到），要么是给玩家的原因（条件没满足）。
        /// </summary>
        public static bool TryResolveExit(string regionId, float playerX, IQuestLog quest,
                                          out RegionInfo next, out string reason)
        {
            next = null;
            reason = string.Empty;

            RegionInfo region = Get(regionId);
            if (region == null)
            {
                reason = $"未知区域：{regionId}";
                return false;
            }

            if (!region.HasExit)
            {
                return false;
            }

            if (playerX < region.ExitX)
            {
                return false;
            }

            if (region.ExitFlag != PuzzleFlag.None && (quest == null || !quest.HasFlag(region.ExitFlag)))
            {
                reason = region.ExitBlockedHint;
                return false;
            }

            next = Get(region.NextRegionId);
            if (next == null)
            {
                reason = $"区域 {regionId} 的下一区 {region.NextRegionId} 没有登记";
                return false;
            }

            return true;
        }
    }
}
