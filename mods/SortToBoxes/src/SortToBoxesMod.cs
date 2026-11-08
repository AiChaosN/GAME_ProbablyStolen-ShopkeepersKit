using System;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 入口。ModLoader 会对本程序集调用 Harmony.PatchAll（本 mod 不用 [HarmonyPatch] 特性，那一步什么也不做），
    /// 然后依次调用 Init / OnEnable。补丁在 Init 里手动打，失败只影响自动归箱。
    /// </summary>
    public sealed class SortToBoxesMod : IMod
    {
        internal static SortConfig Config;
        private static GameObject host;
        private static bool hooked;

        public void Init(ModManifest manifest)
        {
            string id = manifest?.ID ?? "chaos5090.sort-to-boxes";
            try
            {
                Config = new SortConfig();
                Config.Init(id);
            }
            catch (Exception e)
            {
                Config = new SortConfig();
                Log.Warn("配置初始化失败，使用默认值（F7 / 自动归箱关）: " + e.Message);
            }

            Patches.Apply();

            try
            {
                if (host == null)
                {
                    host = new GameObject("SortToBoxes");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    host.AddComponent<SortBehaviour>();
                }
            }
            catch (Exception e)
            {
                Log.Error("创建热键对象失败: " + Log.Describe(e));
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

            Log.Info("已加载，热键 " + Config.Hotkey + "，买下时自动归箱 " + (Config.AutoOnPurchase ? "开" : "关") +
                     (Patches.Applied ? "" : "（自动归箱补丁未生效）"));
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
            try
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                host = null;
            }
            catch { }
        }

        private static void OnGameLoaded()
        {
            try { SortBehaviour.Instance?.OnGameLoaded(); }
            catch (Exception e) { Log.Warn("读档回调出错: " + e.Message); }
        }
    }
}
