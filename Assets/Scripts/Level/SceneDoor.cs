using UnityEngine;
using UnityEngine.SceneManagement;
using D225.Quest;
using D225.Level;

namespace D225.Interact
{
    /// <summary>
    /// 场景门：按 E 进入另一段场景（宿舍房间 ↔ 走廊 ↔ 楼梯间）。
    /// 只回答三件事：现在能不能开、提示写什么、开了之后去哪。
    /// 正式的过场（淡入淡出、音效、加载界面）由成员6 在此之上扩展。
    /// </summary>
    [AddComponentMenu("D225/关卡/场景门 (SceneDoor)")]
    public sealed class SceneDoor : InteractableBase
    {
        [Header("门牌")]
        [Tooltip("门牌号，用于提示与任务日志，例如 225 / 224 / 楼梯间。")]
        [SerializeField]
        private string doorPlate = "225";

        [Header("去哪")]
        [Tooltip("目标场景名：Assets/Scenes 下的文件名，不带 .unity。")]
        [SerializeField]
        private string sceneName = "Scene_B_Corridor";

        [Tooltip("目标场景里的出生点 id；留空表示沿用场景默认位置。")]
        [SerializeField]
        private string spawnId = "";

        [Tooltip("本门 id，写进路由，便于以后「从哪扇门回来」定位。")]
        [SerializeField]
        private string doorId = "";

        [Header("条件")]
        [Tooltip("锁着的门：按 E 只给原因，不切换场景。")]
        [SerializeField]
        private bool locked;

        [Tooltip("锁门提示（提示里会带上门牌号）。")]
        [SerializeField]
        private string lockedMessage = "门锁着。熄灯后宿舍门都自动落锁了。";

        [Tooltip("需要先立起的任务标记；保留 None 表示不需要条件。")]
        [SerializeField]
        private PuzzleFlag requiredFlag = PuzzleFlag.None;

        [Tooltip("条件未满足时的提示。")]
        [SerializeField]
        private string blockedMessage = "门从里面反锁了……得先解决别的事。";

        [Header("表现")]
        [Tooltip("进门前的字幕（可留空）。")]
        [SerializeField]
        private string enterMessage = "";

        public override string DisplayName =>
            string.IsNullOrWhiteSpace(doorPlate) ? base.DisplayName : doorPlate;

        public string DoorPlate => doorPlate;

        public string SceneName => sceneName;

        public string SpawnId => spawnId;

        public bool IsLocked => locked;

        protected override InteractionState EvaluateStateCore(InteractionContext context)
        {
            if (locked)
            {
                return InteractionState.Unavailable;
            }

            if (requiredFlag != PuzzleFlag.None)
            {
                if (context.Quest == null || !context.Quest.HasFlag(requiredFlag))
                {
                    return InteractionState.Unavailable;
                }
            }

            return InteractionState.Usable;
        }

        public override string BlockedReason(InteractionContext context)
        {
            if (locked)
            {
                return string.IsNullOrWhiteSpace(doorPlate)
                    ? lockedMessage
                    : lockedMessage + "（门牌 " + doorPlate + "）";
            }

            if (requiredFlag != PuzzleFlag.None)
            {
                return blockedMessage;
            }

            return base.BlockedReason(context);
        }

        protected override InteractionResult Execute(InteractionContext context)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[D225] 场景门没有配置目标场景：" + name, this);
                return InteractionResult.Rejected("这扇门通向哪里还不清楚");
            }

            D225SceneRouter.SnapshotQuestProgress(context.Quest ?? QuestLogLocator.Resolve());
            D225SceneRouter.Depart(spawnId, string.IsNullOrWhiteSpace(doorId) ? doorPlate : doorId);
            Debug.Log("[D225] 场景门 " + DisplayName + " → " + sceneName + "（出生点 " + spawnId + "）", this);
            SceneManager.LoadScene(sceneName);

            return InteractionResult.Ok(string.IsNullOrEmpty(enterMessage) ? "门开了。" : enterMessage);
        }

        /// <summary>编辑器工具使用：一次配好门牌、目标场景、出生点与锁定状态。</summary>
        public void Configure(string plate, string scene, string spawn, bool isLocked)
        {
            doorPlate = plate;
            sceneName = scene;
            spawnId = spawn;
            locked = isLocked;
            doorId = string.IsNullOrWhiteSpace(doorId) ? plate : doorId;
        }

        // 门上不画任何色块（背景图里的门框已经是视觉），只在编辑器里画线框方便摆位。
        void OnDrawGizmosSelected()
        {
            var bc = GetComponent<BoxCollider2D>();
            if (bc == null) return;

            Gizmos.color = locked
                ? new Color(0.85f, 0.32f, 0.32f, 0.85f)
                : new Color(0.30f, 0.82f, 0.45f, 0.85f);
            var c = transform.TransformPoint(bc.offset);
            var s = new Vector3(bc.size.x * transform.lossyScale.x,
                                bc.size.y * transform.lossyScale.y, 0f);
            Gizmos.DrawWireCube(c, s);

            var label = string.IsNullOrEmpty(doorPlate) ? name : doorPlate;
            var target = string.IsNullOrEmpty(sceneName) ? "（未连接）" : sceneName;
            var spawn = string.IsNullOrEmpty(spawnId) ? "-" : spawnId;
            Gizmos.color = Color.white;
            UnityEditor.Handles.Label(transform.position + Vector3.up * (bc.size.y * 0.5f + 0.25f),
                label + (locked ? " · 锁" : "") + " → " + target + " / " + spawn);
        }
    }
}
