using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace D225.Core
{
    /// <summary>
    /// 流程启动器：只有主菜单场景（<see cref="RegionCatalog.BootSceneName"/>）会自动创建 <see cref="GameManager"/>。
    /// 这样在编辑器里单独打开 A/B/C 关卡场景调试时，行为与以前完全一样（不会凭空多出一个流程单例），
    /// 而从 Build 的第一个场景启动时，流程会自动接管并串起三段区域。
    /// </summary>
    public static class GameBootstrap
    {
        /// <summary>流程根节点名，便于在 Hierarchy 里辨认。</summary>
        public const string RootName = "[D225 Flow]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (GameManager.Instance != null)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, RegionCatalog.BootSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (UnityEngine.Object.FindObjectOfType<GameManager>() != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            root.AddComponent<GameManager>();
        }
    }
}
