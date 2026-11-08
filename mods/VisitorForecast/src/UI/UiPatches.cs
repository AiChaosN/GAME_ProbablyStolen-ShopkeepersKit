using System;
using HarmonyLib;
using UnityEngine;

namespace VisitorForecast.UI
{
    /// <summary>
    /// 游戏用 StoreUIManager.hoveringStoreUI 判断“鼠标在界面上”（拖物品、框选、热键等都会看它），
    /// 它只检查游戏自己的几块画布。本 mod 的窗口在 overlay 层（或简易 IMGUI 窗口）时不在其中，
    /// 所以在 CheckRaycast 之后补一句：鼠标在本 mod 窗口上 → 视为在界面上。只会置 true，不会改成 false。
    /// 与沙盒无关；打不上补丁时只是少一层遮挡，不影响其他功能。
    /// </summary>
    internal static class UiPatches
    {
        private const string HarmonyId = "VisitorForecast.ui";
        private static Harmony harmony;

        internal static void Apply()
        {
            try
            {
                var target = AccessTools.Method(typeof(StoreUIManager), "CheckRaycast", Type.EmptyTypes);
                var postfix = AccessTools.Method(typeof(UiPatches), nameof(CheckRaycastPostfix));
                if (target == null || postfix == null)
                {
                    Log.Warn("找不到 StoreUIManager.CheckRaycast，窗口遮挡鼠标只依赖 UI 射线");
                    return;
                }
                harmony = new Harmony(HarmonyId);
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception e)
            {
                Log.Warn("界面补丁失败（只影响鼠标遮挡）: " + e.Message);
            }
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            harmony = null;
        }

        private static void CheckRaycastPostfix(StoreUIManager __instance)
        {
            try
            {
                if (__instance == null || __instance.hoveringStoreUI) return;
                var b = ForecastBehaviour.Instance;
                if (b != null && b.IsPointerOverWindow(Input.mousePosition)) __instance.hoveringStoreUI = true;
            }
            catch
            {
                // 每帧都会调用，绝不能抛出
            }
        }
    }
}
