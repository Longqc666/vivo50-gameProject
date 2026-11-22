using System.Collections;
using System.Collections.Generic;
using D225.Quest;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace D225.Core
{
    /// <summary>
    /// 构建后冒烟测试（T09 / T10 / T11 / T12 的回归用）：只在命令行带 <c>-d225-smoke</c> 时启动，
    /// 正常游玩、编辑器里都不会创建它。
    ///
    /// 用法：
    /// <code>
    /// D225.exe -d225-smoke -screen-width 960 -screen-height 540 -screen-fullscreen 0
    /// </code>
    ///
    /// 它会依次验证：主菜单 → 开始游戏进入 A 区 → 暂停/恢复 → 走到门口切 B 区 →
    /// B 区未开楼梯门时被拦 → 开门后切 C 区 → 撤离结算，最后写 <c>SMOKE PASS/FAIL</c> 并以退出码表明结果。
    /// 它是「能跑起来」的证据，不替代人工试玩与 PlayMode 用例。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowSmokeDriver : MonoBehaviour
    {
        public const string Switch = "-d225-smoke";

        private const float RegionTimeout = 25f;
        private const float WalkSpeed = 8f;

        private readonly List<string> failures = new List<string>();

        /// <summary>命令行里是否要求跑冒烟。</summary>
        public static bool Requested
        {
            get
            {
                string[] args = System.Environment.GetCommandLineArgs();
                foreach (string arg in args)
                {
                    if (string.Equals(arg, Switch, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Start()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Log("冒烟开始");

            yield return WaitUntil(() => GameManager.Instance != null, 15f, "GameManager 出现");
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.6f);

            Check(manager.State == GameStateId.MainMenu, "启动后应在主菜单，实际 " + manager.State);

            // ---- 开始游戏 → A 区（走真实按钮路径，而不是绕过界面直接 BeginGame）
            Check(FlowStartRequest.TryInvoke(), "主菜单「开始」按钮应已绑定流程（FlowStartRequest 未注册）");
            yield return WaitRegion("A", "进入 A 区");
            Check(manager.State == GameStateId.Explore, "A 区应为 Explore，实际 " + manager.State);
            Check(manager.CurrentCheckpoint != null, "进入 A 区应写入检查点");

            // ---- 暂停 / 恢复（T10）
            Check(manager.RequestPause(true), "探索中应能暂停");
            Check(Mathf.Approximately(Time.timeScale, 0f), "暂停时 timeScale 应为 0");
            Check(!manager.NotifyPlayerCaught("冒烟-暂停"), "暂停时不应结算抓捕");
            Check(manager.State == GameStateId.Paused, "暂停状态应保持");
            Check(manager.RequestPause(false), "暂停后应能恢复");
            Check(Mathf.Approximately(Time.timeScale, 1f), "恢复后 timeScale 应为 1");

            // ---- A 区走到门口 → B 区
            yield return WalkToX(RegionCatalog.DormDoorX + 0.4f, "走向宿舍门", allowStuck: true);
            yield return WaitRegion("B", "进入 B 区");
            Check(manager.State == GameStateId.Explore, "B 区应为 Explore");

            // ---- B 区未开楼梯门：应被拦（T11 前置）
            yield return WalkToX(RegionCatalog.StairDoorX + 0.4f, "走向楼梯门（未开门）", allowStuck: true);
            yield return new WaitForSecondsRealtime(1.2f);
            Check(manager.CurrentRegion != null && manager.CurrentRegion.Id == "B",
                  "未开楼梯门不应离开 B 区，实际在 " + (manager.CurrentRegion?.Id ?? "null"));

            // ---- 抓捕 → 失败 → 从检查点重试（T09）
            manager.Quest.SetFlag(PuzzleFlag.FuseAcquired); // 造一条「永久进度」，重试后必须还在
            Check(manager.NotifyPlayerCaught("冒烟-抓捕"), "探索中应能结算抓捕");
            yield return WaitUntil(() => manager.State == GameStateId.Failed, 5f, "进入失败态");
            Check(manager.State == GameStateId.Failed, "应进入 Failed，实际 " + manager.State);
            Check(Mathf.Approximately(Time.timeScale, 0f), "失败时 timeScale 应为 0");
            Check(!manager.RequestPause(true), "失败态不应还能暂停");

            Check(manager.RetryFromCheckpoint(), "失败后应能重试");
            yield return WaitUntil(
                () => manager.State == GameStateId.Explore &&
                      manager.CurrentRegion != null &&
                      manager.CurrentRegion.Id == "B",
                RegionTimeout,
                "重试后回到 B 区探索");
            yield return new WaitForSecondsRealtime(0.8f);
            Check(manager.State == GameStateId.Explore, "重试后应回到 Explore，实际 " + manager.State);
            Check(manager.CurrentCheckpoint != null &&
                  string.Equals(manager.CurrentCheckpoint.SceneId, "B", System.StringComparison.Ordinal),
                  "重试后检查点应仍在 B 区");
            Check(manager.Quest.HasFlag(PuzzleFlag.FuseAcquired), "重试后永久进度（FuseAcquired）应保留");

            // ---- 开门后应切 C 区
            manager.Quest.SetFlag(PuzzleFlag.StairDoorUnlocked);
            yield return WalkToX(RegionCatalog.StairDoorX + 0.6f, "开门后走向楼梯门", allowStuck: true);
            yield return WaitRegion("C", "进入 C 区");

            // ---- 结算（T11）
            manager.Quest.SetFlag(PuzzleFlag.EvacuationComplete);
            yield return WaitUntil(() => manager.State == GameStateId.Ending, 10f,
                                  "进入结算状态");
            Check(manager.State == GameStateId.Ending, "写入 EvacuationComplete 后应为 Ending");

            // ---- 返回主菜单
            Check(manager.ReturnToMainMenu(), "结算后应能返回主菜单");
            yield return WaitRegion(RegionCatalog.BootSceneName, "回到主菜单场景");
            yield return new WaitForSecondsRealtime(0.8f);
            Check(manager.State == GameStateId.MainMenu, "回到主菜单后状态应为 MainMenu");

            // ---- 结果
            if (failures.Count == 0)
            {
                Debug.Log("SMOKE PASS：主菜单→A→暂停→B→门禁拦截→C→结算→主菜单 全链路通过");
                Application.Quit(0);
            }
            else
            {
                Debug.LogError("SMOKE FAIL（" + failures.Count + " 项）：\n · " + string.Join("\n · ", failures));
                Application.Quit(1);
            }
        }

        // ------------------------------------------------------------------ 步骤工具

        private IEnumerator WaitUntil(System.Func<bool> predicate, float timeout, string what)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (predicate())
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Check(false, "等待超时：" + what);
        }

        private IEnumerator WaitRegion(string sceneOrRegion, string what)
        {
            yield return WaitUntil(
                () =>
                {
                    string active = SceneManager.GetActiveScene().name;
                    RegionInfo region = RegionCatalog.GetBySceneName(active);
                    if (region != null && string.Equals(region.Id, sceneOrRegion, System.StringComparison.Ordinal))
                    {
                        return true;
                    }

                    return string.Equals(active, sceneOrRegion, System.StringComparison.Ordinal);
                },
                RegionTimeout,
                what);

            // 等切区协程收尾（淡入、恢复）
            yield return new WaitForSecondsRealtime(0.8f);
        }

        /// <summary>
        /// 把角色逐步挪到目标 X。关卡里有墙，角色可能到不了精确坐标（例如楼梯门在 x≈28.1、右墙内表面 28.8），
        /// 因此 <paramref name="allowStuck"/> 为真时「被墙挡住」只记一条说明，不算失败——
        /// 真正的断言是「区域有没有切过去」。
        /// </summary>
        private IEnumerator WalkToX(float targetX, string what, bool allowStuck = false)
        {
            float elapsed = 0f;
            while (elapsed < RegionTimeout)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null)
                {
                    yield return null;
                    continue;
                }

                float x = player.transform.position.x;
                if (x >= targetX)
                {
                    yield break;
                }

                float step = WalkSpeed * Time.unscaledDeltaTime;
                x = Mathf.Min(targetX, x + step);
                SetPosition(player, new Vector2(x, player.transform.position.y));

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (allowStuck)
            {
                Log("（" + what + "：角色被墙体挡住，未到精确坐标，属正常）");
            }
            else
            {
                Check(false, "走位超时：" + what);
            }
        }

        private static void SetPosition(GameObject player, Vector2 position)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.position = position;
                body.simulated = true;
            }

            player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);
        }

        private void Check(bool condition, string message)
        {
            if (condition)
            {
                Log("✓ " + message);
                return;
            }

            failures.Add(message);
            Debug.LogError("[D225.冒烟] ✗ " + message);
        }

        private static void Log(string message)
        {
            Debug.Log("[D225.冒烟] " + message);
        }
    }
}
