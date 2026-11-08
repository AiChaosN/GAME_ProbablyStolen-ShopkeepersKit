using System;
using UnityEngine;
using VisitorForecast.Sandbox;
using VisitorForecast.UI;

namespace VisitorForecast
{
    /// <summary>
    /// 入口。ModLoader 会对本程序集调用 Harmony.PatchAll（本 mod 不用 [HarmonyPatch] 特性，所以那一步什么也不做），
    /// 然后依次调用 Init / OnEnable。所有补丁在 Init 里手动打，任一失败都不会影响游戏本身。
    /// </summary>
    public sealed class VisitorForecastMod : IMod
    {
        internal static ForecastConfig Config;
        private static GameObject host;
        private static bool hooked;

        public void Init(ModManifest manifest)
        {
            string id = manifest?.ID ?? "chaos5090.visitor-forecast";
            try
            {
                Config = new ForecastConfig();
                Config.Init(id);
            }
            catch (Exception e)
            {
                Config = new ForecastConfig();
                Log.Warn("配置初始化失败，使用默认值（F9 / 300 次）: " + e.Message);
            }

            try
            {
                Patches.Apply();
            }
            catch (Exception e)
            {
                Patches.CriticalMissing = true;
                Log.Error("打补丁失败，预测功能禁用: " + Log.Describe(e));
            }

            UiPatches.Apply(); // 鼠标遮挡（只影响界面，失败不影响预测）

            try
            {
                if (host == null)
                {
                    host = new GameObject("VisitorForecast");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    host.AddComponent<ForecastBehaviour>();
                }
            }
            catch (Exception e)
            {
                Log.Error("创建界面对象失败: " + Log.Describe(e));
            }

            try
            {
                if (!hooked)
                {
                    ModHook.OnGameLoadedLate += OnGameLoaded;
                    hooked = true;
                }
            }
            catch (Exception e)
            {
                Log.Warn("订阅读档事件失败: " + e.Message);
            }

            Log.Info("已加载，热键 " + Config.Hotkey + "，每次模拟 " + Config.Runs + " 次");
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            try
            {
                if (hooked)
                {
                    ModHook.OnGameLoadedLate -= OnGameLoaded;
                    hooked = false;
                }
            }
            catch { }
            Patches.Remove();
            UiPatches.Remove();
            try
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                host = null;
            }
            catch { }
        }

        private static void OnGameLoaded()
        {
            try { ForecastBehaviour.Instance?.OnGameLoaded(); }
            catch (Exception e) { Log.Warn("读档回调出错: " + e.Message); }
        }
    }
}
