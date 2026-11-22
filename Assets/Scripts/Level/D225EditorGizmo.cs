// D225EditorGizmo.cs —— 编辑器辅助色块的运行时隐藏开关（成员3 · 彭靖毅）
//
// 关卡生成器会在场景里放一批 Gizmo_ 色块（检查点、巡逻点、出生点、教学提示带），
// 它们只是给编辑器和组员看的定位标记，正式游戏画面里不该出现。
//
// 这个脚本只做一件事：进入运行时把自己的 SpriteRenderer 关掉。
// 编辑器视图里照常可见，所以摆位、对图不受影响。
//
// 如果成员6 的构建流程需要更彻底的做法（例如 Build 时整批剔除），
// 直接用 D225/关卡/9. 清理辅助色块 菜单把它们从场景里删掉也可以。

using UnityEngine;

namespace D225.Level
{
    [AddComponentMenu("D225/关卡/编辑器辅助标记 (D225EditorGizmo)")]
    [DisallowMultipleComponent]
    public sealed class D225EditorGizmo : MonoBehaviour
    {
        void Awake()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
        }
    }
}
