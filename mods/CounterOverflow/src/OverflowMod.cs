using System;
using UnityEngine;

namespace CounterOverflow
{
    /// <summary>
    /// 入口。ModLoader 会对本程序集调用 Harmony.PatchAll（本 mod 不用 [HarmonyPatch] 特性，那一步什么也不做），
    /// 然后依次调用 Init / OnEnable。补丁在 Init 里手动打。
    /// </summary>
    public sealed class OverflowMod : IMod
    {
        internal static OverflowConfig Config;
        private static GameObject host;
        private static bool hooked;

        public void Init(ModManifest manifest)
        {
            string id = manifest?.ID ?? "chaos5090.counter-overflow";
            try
            {
                Config = new OverflowConfig();
                Config.Init(id);
            }
            catch (Exception e)
            {
                Config = new OverflowConfig();
                Log.Warn("配置初始化失败，使用默认值（开启 / F3）: " + e.Message);
            }

            Overflow.Apply();

            try
            {
                if (host == null)
                {
                    host = new GameObject("CounterOverflow");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    host.AddComponent<OverflowBehaviour>();
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

            Log.Info("已加载：" + (Overflow.Applied ? "柜台放不下的货改为排队" : "补丁未生效，按原版处理") + "，翻页 " + Config.Hotkey +
                     (Config.TestMode ? "，测试模式开（Ctrl+" + Config.Hotkey + " 让顾客多摆一批货）" : ""));
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
            Overflow.Remove();
            try
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                host = null;
            }
            catch { }
        }

        private static void OnGameLoaded()
        {
            try { Overflow.Reset(); }
            catch (Exception e) { Log.Warn("读档回调出错: " + e.Message); }
        }
    }

    internal sealed class OverflowConfig
    {
        internal const string DefaultHotkey = "F3";

        internal bool Enabled = true;
        internal KeyCode Hotkey = KeyCode.F3;
        internal bool TestMode;

        private ConfigEntry<bool> enabledEntry;
        private ConfigEntry<string> hotkeyEntry;
        private ConfigEntry<bool> testEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            enabledEntry = cfg.Define("enabled", true, "General", () => "柜台放不下的货改为排队（不销毁）",
                tooltip: () => "关闭后恢复原版：顾客往柜台摆货时，放不下的直接销毁。");
            hotkeyEntry = cfg.Define("hotkey", DefaultHotkey, "General", () => "翻页热键",
                choices: null,
                tooltip: () => "Unity KeyCode 名称。有货在排队时按它：柜台上你还没买的顾客的货排到队尾，换上排队的货。默认 F3。");
            testEntry = cfg.Define("testMode", false, "Test", () => "测试模式",
                tooltip: () => "开启后，柜台前有顾客时按 Ctrl+翻页热键，会让顾客按原版流程再往柜台摆一批大件设备（冰箱、水箱、集湿器……），用来制造柜台溢出、测试排队和翻页。这些货可以照常买，测试完请关掉。");
            enabledEntry.OnChanged += _ => Apply();
            hotkeyEntry.OnChanged += _ => Apply();
            testEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save();
        }

        private void Apply()
        {
            try
            {
                Enabled = enabledEntry?.Value ?? true;
                var text = (hotkeyEntry?.Value ?? DefaultHotkey).Trim();
                if (Enum.TryParse(text, true, out KeyCode k) && k != KeyCode.None) Hotkey = k;
                else
                {
                    Hotkey = KeyCode.F3;
                    Log.Warn("无法识别的热键 '" + text + "'，改用 " + DefaultHotkey);
                }
                TestMode = testEntry?.Value ?? false;
            }
            catch (Exception e)
            {
                Log.Warn("读取配置失败，使用默认值: " + e.Message);
            }
        }
    }
}
