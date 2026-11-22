using System.Collections.Generic;
using D225.Quest;
using UnityEngine;

namespace D225.Level
{
    /// <summary>
    /// 跨场景跳转的路由信息：SceneDoor 写入，SceneSpawnPoint 读取。
    /// 成员6 接管场景切换时可以直接替换本类，只要保留 Depart / NextSpawnId 的语义。
    /// </summary>
    public static class D225SceneRouter
    {
        /// <summary>目标场景里要使用的出生点 id；为空表示沿用场景默认位置。</summary>
        public static string NextSpawnId = string.Empty;

        /// <summary>玩家刚才离开的那扇门（留给成员6 做过场与返回定位）。</summary>
        public static string LastDoorId = string.Empty;

        // 跨场景任务进度快照。每个场景各有一个 QuestLogProvider（任务进度是场景内的），
        // 所以从这里离开时把已立起的标记抄一份，进新场景时再灌回去，避免来回走一趟进度就清空。
        // 成员6 的 GameManager 接管持久化后，这两个方法可以删掉。
        private static readonly HashSet<PuzzleFlag> CarriedFlags = new HashSet<PuzzleFlag>();

        public static void Depart(string spawnId, string doorId)
        {
            NextSpawnId = spawnId ?? string.Empty;
            LastDoorId = doorId ?? string.Empty;
        }

        /// <summary>离开场景前调用：把当前进度抄进快照。</summary>
        public static void SnapshotQuestProgress(IQuestLog log)
        {
            CarriedFlags.Clear();
            if (log == null)
            {
                return;
            }

            foreach (PuzzleFlag flag in log.RaisedFlags)
            {
                CarriedFlags.Add(flag);
            }
        }

        /// <summary>进入场景后调用：把快照里的标记灌回当前场景的任务日志（幂等）。</summary>
        public static void RestoreQuestProgress(IQuestLog log)
        {
            if (log == null || CarriedFlags.Count == 0)
            {
                return;
            }

            foreach (PuzzleFlag flag in CarriedFlags)
            {
                log.SetFlag(flag);
            }
        }

        public static void Clear()
        {
            NextSpawnId = string.Empty;
            LastDoorId = string.Empty;
        }
    }

    /// <summary>
    /// 场景出生点：带 id 的空物体。跨场景跳转后，若路由里记录的 id 与自身一致，
    /// 就把玩家挪到这里（随后清空路由）。场景里放一个就够，多放也只有匹配的那个会生效。
    /// </summary>
    [AddComponentMenu("D225/关卡/场景出生点 (SceneSpawnPoint)")]
    public sealed class SceneSpawnPoint : MonoBehaviour
    {
        [Tooltip("出生点 id，与 SceneDoor 的「目标出生点」对应，例如 225 / 224 / 楼梯口。")]
        [SerializeField] private string spawnId = string.Empty;

        private void Start()
        {
            // 先恢复任务进度：不管这次从哪个出生点进来，进度都要接上。
            // 场景里有多个出生点时会被调用多次，RestoreQuestProgress 是幂等的。
            D225SceneRouter.RestoreQuestProgress(QuestLogLocator.Resolve());

            if (string.IsNullOrEmpty(spawnId))
            {
                return;
            }

            if (D225SceneRouter.NextSpawnId != spawnId)
            {
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[D225] 场景出生点找不到 Player，保持场景默认位置：" + name, this);
                D225SceneRouter.Clear();
                return;
            }

            player.transform.position = transform.position;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = transform.position;
                body.velocity = Vector2.zero;
            }

            Debug.Log("[D225] 玩家从 " + spawnId + " 出生点进入场景。", this);
            D225SceneRouter.Clear();
        }

        /// <summary>编辑器工具与自测使用。</summary>
        public void SetSpawnId(string id)
        {
            spawnId = id;
        }

        public string SpawnId => spawnId;
    }
}
