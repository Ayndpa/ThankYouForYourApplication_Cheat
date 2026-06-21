using HarmonyLib;
using UnityEngine;

namespace CheatMenu
{
    // ════════════════════════════════════════════
    //  Harmony 补丁 — 鼠标在作弊窗口上时屏蔽游戏点击输入
    // ════════════════════════════════════════════

    [HarmonyPatch(typeof(Input))]
    internal static class InputBlockPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Input.GetMouseButtonDown))]
        [HarmonyPatch(nameof(Input.GetMouseButton))]
        [HarmonyPatch(nameof(Input.GetMouseButtonUp))]
        private static bool Prefix(ref bool __result)
        {
            if (Plugin.BlockMouseInput)
            {
                __result = false;
                return false;   // 跳过原始方法
            }
            return true;        // 正常执行
        }
    }
}
