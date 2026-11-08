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
        internal static string ModId = "chaos5090.counter-overflow";
        private static GameObject host;
        private static bool hooked;

        public void Init(ModManifest manifest)
        {
            ModId = manifest?.ID ?? ModId;
            try
            {
                Config = new OverflowConfig();
                Config.Init(ModId);
            }
            catch (Exception e)
            {
                Config = new OverflowConfig();
                Log.Warn("配置初始化失败，使用默认值（开启 / , .）: " + e.Message);
            }
            Config.RegisterGameOptions(ModId);

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

            Log.Info("已加载：" + (Overflow.Applied ? "柜台放不下的货改为分页" : "补丁未生效，按原版处理") +
                     "，上一页 " + OverflowConfig.KeyLabel(Config.PrevKey) + "，下一页 " + OverflowConfig.KeyLabel(Config.NextKey) +
                     (Config.TestMode ? "，测试模式开（Ctrl+" + OverflowConfig.KeyLabel(Config.NextKey) + " 让顾客多摆一批货）" : ""));
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
            try { ModHelper.RemoveGameOptions(ModId); } catch { }
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

    /// <summary>
    /// 配置存在游戏自带的 ModConfig（ModConfigs/&lt;mod ID&gt;.json），改装件界面的“设置”里能改；
    /// 两个翻页键另外登记到游戏的“选项”界面（按一下键即可重新绑定），两处改的是同一份设置。
    /// </summary>
    internal sealed class OverflowConfig
    {
        internal const string DefaultPrev = "Z";   // 左手区，挨着游戏的 Q/E 旋转
        internal const string DefaultNext = "X";

        internal bool Enabled = true;
        internal KeyCode PrevKey = KeyCode.Z;
        internal KeyCode NextKey = KeyCode.X;
        internal bool PageBar = true;
        internal bool TestMode;

        private ConfigEntry<bool> enabledEntry;
        private ConfigEntry<string> prevEntry;
        private ConfigEntry<string> nextEntry;
        private ConfigEntry<bool> barEntry;
        private ConfigEntry<bool> testEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            enabledEntry = cfg.Define("enabled", true, "General", () => Lang.T("柜台放不下的货分页显示（不销毁）", "Page goods that don't fit (don't destroy them)"),
                tooltip: () => Lang.T("关闭后恢复原版：顾客往柜台摆货时，放不下的直接销毁。", "Off = base game: goods that don't fit on the counter are destroyed."));
            prevEntry = cfg.Define("prevKey", DefaultPrev, "General", () => Lang.T("上一页", "Previous page"),
                choices: null,
                tooltip: () => Lang.T("Unity KeyCode 名称或直接填符号，默认 Z。也可以在游戏的“选项”里按键绑定。", "Unity KeyCode name or a symbol; default Z. You can also bind it in the game's Options."));
            nextEntry = cfg.Define("nextKey", DefaultNext, "General", () => Lang.T("下一页", "Next page"),
                choices: null,
                tooltip: () => Lang.T("Unity KeyCode 名称或直接填符号，默认 X。也可以在游戏的“选项”里按键绑定。", "Unity KeyCode name or a symbol; default X. You can also bind it in the game's Options."));
            barEntry = cfg.Define("pageBar", true, "General", () => Lang.T("显示翻页条", "Show the page bar"),
                tooltip: () => Lang.T("货分成多页时，在顾客头上的提示框正下方显示“上一页 / 第 1/3 页 / 下一页”，可以直接点。", "When goods are paged, show \"Prev / Page 1/3 / Next\" right below the notification above the customer; the buttons are clickable."));
            testEntry = cfg.Define("testMode", false, "Test", () => Lang.T("测试模式", "Test mode"),
                tooltip: () => Lang.T("开启后，柜台前有顾客时按 Ctrl+下一页键，会让顾客按原版流程再往柜台摆一批大件设备，用来制造柜台溢出、测试分页。这些货可以照常买，测试完请关掉。",
                    "When on, press Ctrl+next-page key with a customer at the counter to make them lay out an extra batch of large equipment through the normal game code, overflowing the counter to test paging. Those items can be bought, so turn this off afterwards."));
            enabledEntry.OnChanged += _ => Apply();
            prevEntry.OnChanged += _ => Apply();
            nextEntry.OnChanged += _ => Apply();
            barEntry.OnChanged += _ => Apply();
            testEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save();
        }

        /// <summary>在游戏“选项”里加一组按键绑定（按一下键即可改）。</summary>
        internal void RegisterGameOptions(string modId)
        {
            try
            {
                const string section = "keys";
                ModHelper.AddGameOptionSection(modId, section, () => Lang.T("掌柜帮手·柜台不丢货", "Shopkeeper's Kit: Counter Overflow"));
                ModHelper.AddGameOptionKeyBind(modId, section, "prev_page", () => Lang.T("柜台上一页", "Counter: previous page"), () => PrevKey, k => SetKey(prevEntry, k, true),
                    () => Lang.T("顾客的货分成多页时，翻到上一页。默认 ,（逗号）", "When a customer's goods are paged, go to the previous page. Default , (comma)"));
                ModHelper.AddGameOptionKeyBind(modId, section, "next_page", () => Lang.T("柜台下一页", "Counter: next page"), () => NextKey, k => SetKey(nextEntry, k, false),
                    () => Lang.T("顾客的货分成多页时，翻到下一页。默认 .（句号）", "When a customer's goods are paged, go to the next page. Default . (period)"));
            }
            catch (Exception e)
            {
                Log.Warn("登记游戏选项失败（仍可在改装件设置里改按键）: " + e.Message);
            }
        }

        private void SetKey(ConfigEntry<string> entry, KeyCode key, bool prev)
        {
            if (key == KeyCode.None) return;
            if (entry != null) entry.Set(key.ToString());   // Set 会触发 Apply 并保存
            else if (prev) PrevKey = key;
            else NextKey = key;
        }

        internal static string KeyLabel(KeyCode k) => KeyNames.Label(k);

        private static KeyCode Parse(ConfigEntry<string> entry, string fallback)
        {
            var fb = (KeyCode)Enum.Parse(typeof(KeyCode), fallback);
            return KeyNames.Parse(entry?.Value ?? fallback, fb, Log.Warn);
        }

        private void Apply()
        {
            try
            {
                Enabled = enabledEntry?.Value ?? true;
                PrevKey = Parse(prevEntry, DefaultPrev);
                NextKey = Parse(nextEntry, DefaultNext);
                if (PrevKey == NextKey)
                {
                    Log.Warn("上一页和下一页设成了同一个键，改回默认的 Z 和 X");
                    PrevKey = KeyCode.Z;
                    NextKey = KeyCode.X;
                }
                PageBar = barEntry?.Value ?? true;
                TestMode = testEntry?.Value ?? false;
            }
            catch (Exception e)
            {
                Log.Warn("读取配置失败，使用默认值: " + e.Message);
            }
        }
    }
}
