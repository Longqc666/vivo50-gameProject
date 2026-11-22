using System.Collections;
using System.Collections.Generic;
using D225.Interact;
using D225.Quest;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace D225.Core
{
    /// <summary>
    /// 流程总控（成员6）：状态机、任务状态、场景衔接、检查点、失败重试与结算。
    ///
    /// 设计约束（便于其他人接入，也便于自测）：
    /// · 唯一负责写 <c>Time.timeScale</c> 的组件；暂停/失败/结算/切场景时置 0，探索时置 1。
    ///   成员1 的移动与交互都按 <c>Time.deltaTime</c>、<c>Time.timeScale</c> 工作，因此不改他们的脚本也能冻结。
    /// · 不直接引用成员1/2/5 的具体类型：只通过 <see cref="IGameFlowView"/>、<see cref="IQuestLog"/>
    ///   和 Unity 消息（<c>SendMessage("ResetAfterFailure")</c>）对接。
    /// · 区域出口按坐标判定（<see cref="RegionCatalog"/>），因此不需要往成员3 的场景里加组件。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/流程/流程总控 (GameManager)")]
    public sealed class GameManager : MonoBehaviour
    {
        public const float DefaultFadeSeconds = 0.45f;

        private static GameManager instance;

        [Header("表现层（拖入 Assets/Prefabs/UI/D225_FlowMenu.prefab；留空则只有逻辑没有界面）")]
        [SerializeField] private GameObject flowMenuPrefab;

        [Header("启动")]
        [Tooltip("在 Scene_Boot 里先显示主菜单；关闭则直接进 A 区。")]
        [SerializeField] private bool startInMainMenu = true;

        [Header("调试")]
        [SerializeField] private bool verboseLog = true;

        private readonly GameStateMachine machine = new GameStateMachine(GameStateId.Init);
        private readonly CheckpointBook checkpoints = new CheckpointBook();
        private readonly List<CheckpointPoint> sceneCheckpoints = new List<CheckpointPoint>();

        private QuestJournal quest;
        private IGameFlowView view;
        private GameObject viewInstance;
        private Transform player;
        private Inventory playerInventory;
        private RegionInfo currentRegion;
        private Coroutine transition;
        private bool busy;
        private string lastBlockedExitReason;

        /// <summary>全局单例；没有流程的关卡场景里为 null。</summary>
        public static GameManager Instance => instance;

        public GameStateMachine Machine => machine;

        public GameStateId State => machine.Current;

        public QuestJournal Quest => quest;

        public CheckpointBook Checkpoints => checkpoints;

        public RegionInfo CurrentRegion => currentRegion;

        public CheckpointData CurrentCheckpoint => checkpoints.Current;

        /// <summary>是否正在切场景/重试（此时输入与抓捕都不应生效）。</summary>
        public bool IsBusy => busy || transition != null;

        public event System.Action<GameStateId, GameStateId> StateChanged;

        public event System.Action<CheckpointData> CheckpointSaved;

        // ================================================================== 生命周期

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[D225.流程] 已存在 GameManager，销毁重复实例。", this);
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            machine.Changed += HandleStateChanged;
            machine.Rejected += (target, reason) => Log("状态迁移被拒绝：" + reason, true);

            SceneManager.sceneLoaded += HandleSceneLoaded;

            // 构建后冒烟（T09/T10/T11/T12）：只有命令行带 -d225-smoke 时才挂上去
            if (FlowSmokeDriver.Requested && GetComponent<FlowSmokeDriver>() == null)
            {
                gameObject.AddComponent<FlowSmokeDriver>();
            }
        }

        private void Start()
        {
            RebindQuest();

            if (string.Equals(SceneManager.GetActiveScene().name, RegionCatalog.BootSceneName,
                              System.StringComparison.Ordinal))
            {
                if (startInMainMenu)
                {
                    EnterMainMenu();
                }
                else
                {
                    BeginGame();
                }
            }
            else
            {
                AdoptCurrentScene();
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            instance = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            ReleaseQuest();

            DestroyView();
            Time.timeScale = 1f;
            GameState.Publish(GameStateId.Init);
        }

        private void Update()
        {
            if (machine.Current != GameStateId.Explore || IsBusy)
            {
                return;
            }

            TrackCheckpoints();
            CheckRegionExit();
        }

        // ================================================================== 对外流程 API

        /// <summary>主菜单「开始游戏」：从第一区入口开始新一局。</summary>
        public bool BeginGame()
        {
            if (machine.Current == GameStateId.Explore || IsBusy)
            {
                Log("BeginGame 被忽略：已经在游戏中或正在加载。", true);
                return false;
            }

            ResetSession();

            RegionInfo first = RegionCatalog.Get(RegionCatalog.FirstRegionId);
            if (first == null)
            {
                Log("区域表里没有第一区，无法开始游戏。", true);
                return false;
            }

            StartTransition(first, first.EntrySpawnId, replaceCheckpoint: true);
            return true;
        }

        /// <summary>
        /// 暂停开关。只允许探索↔暂停这一对；失败/结算/加载中一律忽略
        /// （对应异常清单里的「暂停仍被抓」「切场景期间重复触发」）。
        /// </summary>
        public bool RequestPause(bool paused)
        {
            if (paused)
            {
                if (machine.Current != GameStateId.Explore || IsBusy)
                {
                    return false;
                }

                return machine.TryEnter(GameStateId.Paused, out _);
            }

            if (machine.Current != GameStateId.Paused)
            {
                return false;
            }

            return machine.TryEnter(GameStateId.Explore, out _);
        }

        /// <summary>
        /// 成员2 在抓捕判定成立时调用。<paramref name="source"/> 只用于日志。
        /// 非探索态（暂停、加载中、已经失败）一律不结算，避免「暂停仍被抓」。
        /// </summary>
        public bool NotifyPlayerCaught(string source = null)
        {
            if (machine.Current != GameStateId.Explore)
            {
                Log("抓捕结算被忽略：当前状态 " + machine.Current + "（来源 " + (source ?? "未知") + "）。", true);
                return false;
            }

            if (IsBusy)
            {
                Log("抓捕结算被忽略：场景切换/重试进行中（来源 " + (source ?? "未知") + "）。", true);
                return false;
            }

            SaveCheckpointNow(null);

            if (!machine.TryEnter(GameStateId.Failed, out string reason))
            {
                Log("进入失败态失败：" + reason, true);
                return false;
            }

            return true;
        }

        /// <summary>失败页「从检查点重试」。</summary>
        public bool RetryFromCheckpoint()
        {
            if (machine.Current != GameStateId.Failed)
            {
                Log("重试被忽略：当前状态 " + machine.Current + "。", true);
                return false;
            }

            if (IsBusy)
            {
                Log("重试被忽略：已经在加载中。", true);
                return false;
            }

            CheckpointData checkpoint = checkpoints.Current;
            RegionInfo target = null;
            string spawnId = null;

            if (checkpoint != null)
            {
                target = RegionCatalog.Get(checkpoint.SceneId);
                spawnId = checkpoint.SpawnId;
            }

            if (target == null)
            {
                Log("没有可用的检查点，重试退化为「从本区入口重新开始」。", true);
                target = currentRegion ?? RegionCatalog.Get(RegionCatalog.FirstRegionId);
                spawnId = target.EntrySpawnId;
                StartTransition(target, spawnId, replaceCheckpoint: true);
                return target != null;
            }

            StartTransition(target, spawnId, replaceCheckpoint: false, isRetry: true);
            return true;
        }

        /// <summary>暂停/失败页「返回主菜单」。</summary>
        public bool ReturnToMainMenu()
        {
            if (IsBusy)
            {
                return false;
            }

            if (machine.Current == GameStateId.MainMenu)
            {
                return false;
            }

            if (machine.Current != GameStateId.Init &&
                !machine.TryEnter(GameStateId.MainMenu, out string reason))
            {
                Log("返回主菜单被拒绝：" + reason, true);
                return false;
            }

            ResetSession();
            StartCoroutine(LoadBootRoutine());
            return true;
        }

        /// <summary>撤离成功（EvacuationZone 写 EvacuationComplete 时自动触发，也可手动调）。</summary>
        public bool NotifyEvacuated()
        {
            if (machine.Current != GameStateId.Explore)
            {
                Log("结算被忽略：当前状态 " + machine.Current + "。", true);
                return false;
            }

            return machine.TryEnter(GameStateId.Ending, out _);
        }

        /// <summary>存档当前检查点；<paramref name="bestSpawnId"/> 为空时取「已通过的最靠右检查点」。</summary>
        public bool SaveCheckpointNow(string bestSpawnId)
        {
            RegionInfo region = currentRegion;
            if (region == null)
            {
                return false;
            }

            string spawnId = bestSpawnId;
            int order = 0;

            if (string.IsNullOrEmpty(spawnId))
            {
                CheckpointPoint best = FindBestCheckpoint(out order);
                spawnId = best != null ? best.SpawnId : region.EntrySpawnId;
            }
            else
            {
                CheckpointPoint named = FindCheckpoint(spawnId);
                if (named != null)
                {
                    order = named.Order;
                }
            }

            CheckpointData data = checkpoints.Save(region.Id, spawnId, ResolveInventory(), quest, order);
            CheckpointSaved?.Invoke(data);
            Log("检查点已保存：" + data);
            return true;
        }

        // ================================================================== 场景

        private void AdoptCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            RegionInfo region = RegionCatalog.GetBySceneName(scene.name);

            if (region == null)
            {
                Log("场景「" + scene.name + "」不在区域表里：流程不接管。", true);
                GameState.Publish(GameStateId.Init);
                return;
            }

            currentRegion = region;
            EnsureView();
            CacheSceneActors();

            if (machine.Current != GameStateId.Explore)
            {
                machine.ForceSet(GameStateId.Init);
                machine.TryEnter(GameStateId.Explore, out _);
            }

            SaveCheckpointNow(region.EntrySpawnId);
        }

        private void StartTransition(RegionInfo region, string spawnId, bool replaceCheckpoint,
                                     bool isRetry = false)
        {
            if (region == null)
            {
                Log("切场景失败：目标区域为空。", true);
                return;
            }

            if (transition != null)
            {
                Log("切场景被忽略：上一次切换还没结束。", true);
                return;
            }

            transition = StartCoroutine(TransitionRoutine(region, spawnId, replaceCheckpoint, isRetry));
        }

        private IEnumerator TransitionRoutine(RegionInfo region, string spawnId, bool replaceCheckpoint,
                                             bool isRetry)
        {
            busy = true;
            Time.timeScale = 0f; // 淡出与加载期间冻结：成员1 的脚本按 timeScale 自动停手

            if (view != null)
            {
                yield return view.FadeOut(DefaultFadeSeconds);
                view.SetFadeInstant(1f);
            }

            // 同区重试也重载场景：鬼怪位置、消耗品等临时状态靠重载重置（策划案 5.6）
            AsyncOperation load = SceneManager.LoadSceneAsync(region.SceneName, LoadSceneMode.Single);
            if (load == null)
            {
                Log("场景「" + region.SceneName + "」加载失败：Build Settings 里可能没有它。", true);
                busy = false;
                transition = null;
                Time.timeScale = 1f;
                yield break;
            }

            while (!load.isDone)
            {
                yield return null;
            }

            // 等一帧，确保新场景物体的 Awake/OnEnable 都跑完
            yield return null;

            currentRegion = region;
            CacheSceneActors();
            PlacePlayer(spawnId);
            RestoreFromCheckpoint(restoreInventory: true);

            if (replaceCheckpoint)
            {
                SaveCheckpointNow(spawnId);
            }

            ResetSceneActors(isRetry);

            if (machine.Current != GameStateId.Explore)
            {
                machine.ForceSet(GameStateId.Init);
                machine.TryEnter(GameStateId.Explore, out _);
            }

            Time.timeScale = 1f;
            busy = false;
            transition = null;

            if (view != null)
            {
                yield return view.FadeIn(DefaultFadeSeconds);
            }

            Log("进入 " + region.Id + " 区，出生点 " + spawnId);
        }

        private IEnumerator LoadBootRoutine()
        {
            busy = true;
            Time.timeScale = 0f;

            if (view != null)
            {
                yield return view.FadeOut(DefaultFadeSeconds);
            }

            DestroyView();

            if (!string.Equals(SceneManager.GetActiveScene().name, RegionCatalog.BootSceneName,
                               System.StringComparison.Ordinal))
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(RegionCatalog.BootSceneName, LoadSceneMode.Single);
                if (load != null)
                {
                    while (!load.isDone)
                    {
                        yield return null;
                    }
                }
                else
                {
                    Log("主菜单场景 Scene_Boot 不在 Build Settings 里。", true);
                }
            }

            yield return null;

            currentRegion = null;
            player = null;
            playerInventory = null;
            sceneCheckpoints.Clear();
            busy = false;
            Time.timeScale = 1f;

            machine.ForceSet(GameStateId.Init);
            machine.TryEnter(GameStateId.MainMenu, out _);
            GameState.Publish(GameStateId.MainMenu);

            EnsureView();
            if (view != null)
            {
                view.ShowMainMenu();
                yield return view.FadeIn(DefaultFadeSeconds);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (instance != this)
            {
                return;
            }

            RebindQuest();

            if (string.Equals(scene.name, RegionCatalog.BootSceneName, System.StringComparison.Ordinal))
            {
                currentRegion = null;
                player = null;
                playerInventory = null;
                sceneCheckpoints.Clear();

                if (!busy)
                {
                    machine.ForceSet(GameStateId.Init);
                    machine.TryEnter(GameStateId.MainMenu, out _);
                    GameState.Publish(GameStateId.MainMenu);
                    EnsureView();
                    view?.ShowMainMenu();
                    Time.timeScale = 1f;
                }

                return;
            }

            RegionInfo region = RegionCatalog.GetBySceneName(scene.name);
            if (region == null)
            {
                return;
            }

            currentRegion = region;
            CacheSceneActors();

            if (!busy)
            {
                // 编辑器里手动加载了某段区域：直接进入探索，并恢复永久进度
                RestoreFromCheckpoint(restoreInventory: false);
                machine.ForceSet(GameStateId.Init);
                machine.TryEnter(GameStateId.Explore, out _);
                Time.timeScale = 1f;
                EnsureView();
                view?.ShowGameplay();
            }
        }

        // ================================================================== 内部：状态/表现

        private void EnterMainMenu()
        {
            EnsureView();
            machine.ForceSet(GameStateId.Init);
            machine.TryEnter(GameStateId.MainMenu, out _);
            GameState.Publish(GameStateId.MainMenu);
            Time.timeScale = 1f;

            if (view != null)
            {
                view.ShowMainMenu();
                view.SetFadeInstant(0f);
            }
        }

        private void HandleStateChanged(GameStateId previous, GameStateId next)
        {
            GameState.Publish(next);
            StateChanged?.Invoke(previous, next);

            switch (next)
            {
                case GameStateId.Paused:
                case GameStateId.Failed:
                case GameStateId.Ending:
                    Time.timeScale = 0f;
                    break;
                default:
                    Time.timeScale = 1f;
                    break;
            }

            if (view != null)
            {
                switch (next)
                {
                    case GameStateId.MainMenu:
                        view.ShowMainMenu();
                        break;
                    case GameStateId.Explore:
                        if (previous == GameStateId.Paused)
                        {
                            view.ShowPause(false);
                        }
                        else
                        {
                            view.ShowGameplay();
                        }
                        break;
                    case GameStateId.Paused:
                        view.ShowPause(true);
                        break;
                    case GameStateId.Failed:
                        view.ShowFail();
                        break;
                    case GameStateId.Ending:
                        view.ShowEnding();
                        break;
                }
            }

            Log("状态：" + previous + " → " + next);
        }

        private void HandleFlagRaised(PuzzleFlag flag)
        {
            if (flag != PuzzleFlag.EvacuationComplete)
            {
                return;
            }

            if (machine.Current == GameStateId.Explore && !IsBusy)
            {
                machine.TryEnter(GameStateId.Ending, out _);
            }
        }

        private void RebindQuest()
        {
            ReleaseQuest();
            QuestLogLocator.InvalidateCache();
            quest = QuestJournal.ForCurrentScene();
            quest.FlagRaised += HandleFlagRaised;
        }

        private void ReleaseQuest()
        {
            if (quest == null)
            {
                return;
            }

            quest.FlagRaised -= HandleFlagRaised;
            quest.Dispose();
            quest = null;
        }

        private void EnsureView()
        {
            if (view != null || flowMenuPrefab == null)
            {
                return;
            }

            viewInstance = Instantiate(flowMenuPrefab);
            viewInstance.name = "[D225 FlowUI]";
            view = viewInstance.GetComponentInChildren<IGameFlowView>(true);

            if (view == null)
            {
                Log("D225_FlowMenu.prefab 里没有 IGameFlowView 实现，界面不可用。", true);
            }
        }

        private void DestroyView()
        {
            if (viewInstance != null)
            {
                Destroy(viewInstance);
            }

            viewInstance = null;
            view = null;
        }

        // ================================================================== 内部：场景对象

        private void CacheSceneActors()
        {
            sceneCheckpoints.Clear();

            GameObject playerObject = GameObject.FindWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
            playerInventory = playerObject != null
                ? playerObject.GetComponentInChildren<Inventory>(true)
                : null;

            if (player == null)
            {
                Log("场景里没有 Player 标签的对象：出生点与检查点恢复会跳过。", true);
            }

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                CollectCheckpoints(root.transform);
            }

            sceneCheckpoints.Sort((a, b) => a.X.CompareTo(b.X));
            for (int i = 0; i < sceneCheckpoints.Count; i++)
            {
                sceneCheckpoints[i].Order = i;
            }
        }

        private void CollectCheckpoints(Transform node)
        {
            if (node.name.StartsWith("CP_", System.StringComparison.Ordinal))
            {
                sceneCheckpoints.Add(new CheckpointPoint(node.name, node.position.x, node.position.y));
                return;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                CollectCheckpoints(node.GetChild(i));
            }
        }

        private void PlacePlayer(string spawnId)
        {
            if (player == null)
            {
                return;
            }

            Vector2 position = new Vector2(1.2f, -1.04f); // 关卡数据里的默认站姿
            CheckpointPoint point = FindCheckpoint(spawnId);
            if (point != null)
            {
                position = new Vector2(point.X, point.Y);
            }
            else
            {
                Log("没有找到出生点 " + spawnId + "，使用默认坐标 (1.2, -1.04)。", true);
            }

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.position = position;
                body.simulated = true;
            }

            player.position = position;
        }

        private Inventory ResolveInventory()
        {
            if (playerInventory != null)
            {
                return playerInventory;
            }

            if (player == null)
            {
                return null;
            }

            playerInventory = player.GetComponentInChildren<Inventory>(true);
            return playerInventory;
        }

        private void RestoreFromCheckpoint(bool restoreInventory)
        {
            CheckpointData checkpoint = checkpoints.Current;
            if (checkpoint == null)
            {
                return;
            }

            checkpoint.RestoreQuest(quest);

            if (!restoreInventory)
            {
                return;
            }

            int restored = checkpoint.RestoreInventory(ResolveInventory());
            Log("检查点恢复：道具 " + restored + " 件、标记 " + checkpoint.Flags.Length + " 个。");
        }

        /// <summary>
        /// 重试/换区后重置场景内的临时状态：躲藏占用、抓捕锁定、失败复位。
        /// 用 Unity 消息而不是强类型引用，避免把成员1 的玩家脚本绑进流程模块。
        /// </summary>
        private void ResetSceneActors(bool isRetry)
        {
            foreach (HidingSpot spot in FindObjectsOfType<HidingSpot>(true))
            {
                spot.ForceRelease();
            }

            if (player != null)
            {
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.velocity = Vector2.zero;
                }

                player.SendMessage("ResetAfterFailure", SendMessageOptions.DontRequireReceiver);
            }

            if (isRetry)
            {
                Log("重试：已释放躲藏占用并复位玩家临时状态。");
            }
        }

        private void TrackCheckpoints()
        {
            if (player == null || currentRegion == null || sceneCheckpoints.Count == 0)
            {
                return;
            }

            CheckpointPoint best = FindBestCheckpoint(out int order);
            if (best == null)
            {
                return;
            }

            CheckpointData current = checkpoints.Current;
            if (current != null &&
                string.Equals(current.SceneId, currentRegion.Id, System.StringComparison.Ordinal) &&
                string.Equals(current.SpawnId, best.SpawnId, System.StringComparison.Ordinal))
            {
                return;
            }

            checkpoints.Save(currentRegion.Id, best.SpawnId, ResolveInventory(), quest, order);
            Log("通过检查点 " + best.SpawnId + "（x=" + best.X.ToString("0.00") + "）");
        }

        private CheckpointPoint FindBestCheckpoint(out int orderOut)
        {
            orderOut = 0;
            CheckpointPoint best = null;

            if (player == null)
            {
                return null;
            }

            float x = player.position.x;
            foreach (CheckpointPoint point in sceneCheckpoints)
            {
                if (x + 0.05f >= point.X && (best == null || point.Order > best.Order))
                {
                    best = point;
                }
            }

            if (best != null)
            {
                orderOut = best.Order;
            }

            return best;
        }

        private CheckpointPoint FindCheckpoint(string spawnId)
        {
            if (string.IsNullOrEmpty(spawnId))
            {
                return null;
            }

            foreach (CheckpointPoint point in sceneCheckpoints)
            {
                if (string.Equals(point.SpawnId, spawnId, System.StringComparison.Ordinal))
                {
                    return point;
                }
            }

            return null;
        }

        private void CheckRegionExit()
        {
            if (currentRegion == null || player == null)
            {
                return;
            }

            if (!RegionCatalog.TryResolveExit(currentRegion.Id, player.position.x, quest,
                                               out RegionInfo next, out string reason))
            {
                if (!string.IsNullOrEmpty(reason) &&
                    !string.Equals(reason, lastBlockedExitReason, System.StringComparison.Ordinal))
                {
                    lastBlockedExitReason = reason;
                    Log("出口条件未满足：" + reason, true);
                }

                return;
            }

            lastBlockedExitReason = null;
            Log("离开 " + currentRegion.Id + " 区，前往 " + next.Id + " 区");
            StartTransition(next, next.EntrySpawnId, replaceCheckpoint: true);
        }

        private void ResetSession()
        {
            checkpoints.Clear();
            sceneCheckpoints.Clear();
            lastBlockedExitReason = null;

            if (quest != null && quest.Clear() > 0)
            {
                Log("新一局：清空任务标记。");
            }
        }

        private void Log(string message, bool warning = false)
        {
            if (!verboseLog && !warning)
            {
                return;
            }

            if (warning)
            {
                Debug.LogWarning("[D225.流程] " + message, this);
            }
            else
            {
                Debug.Log("[D225.流程] " + message, this);
            }
        }

        // ================================================================== 内部：数据结构

        private sealed class CheckpointPoint
        {
            public CheckpointPoint(string spawnId, float x, float y)
            {
                SpawnId = spawnId;
                X = x;
                Y = y;
            }

            public string SpawnId { get; }

            public float X { get; }

            public float Y { get; }

            public int Order { get; set; }
        }
    }
}
