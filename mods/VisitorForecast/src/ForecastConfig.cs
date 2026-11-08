using System;
using UnityEngine;

namespace VisitorForecast
{
    /// <summary>
    /// 配置通过游戏自带的 ModConfig 保存（%USERPROFILE%/AppData/LocalLow/Questing Goose Studio/Probably Stolen/ModConfigs/VisitorForecast.json），
    /// 也可在游戏的 Mods 界面里点“设置”修改。用游戏的配置接口而不是自己读写 DLL 旁的文件，
    /// 是为了不申请 “files” 权限。
    /// </summary>
    internal sealed class ForecastConfig
    {
        internal const string DefaultHotkey = "F9";
        internal const string CheatMenuId = "chezhou.cheat-menu"; // 创意工坊“作弊菜单”，默认热键 F8

        internal KeyCode Hotkey = KeyCode.F9;
        internal int Runs = 300;
        internal bool IncludeOtherMods;
        internal bool NativeUI = true;

        private ConfigEntry<string> hotkeyEntry;
        private ConfigEntry<int> runsEntry;
        private ConfigEntry<bool> modsEntry;
        private ConfigEntry<bool> nativeEntry;
        private ConfigEntry<bool> conflictCheckEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            hotkeyEntry = cfg.Define("hotkey", DefaultHotkey, "General", () => "热键（打开/关闭预测窗口）",
                choices: null,
                tooltip: () => "Unity KeyCode 名称，例如 F9、F7、F10、Insert、Home。改完立即生效。默认 F9（创意工坊的作弊菜单默认占用 F8）。");
            runsEntry = cfg.Define("runs", 300, "General", () => "每次预测的模拟次数",
                min: 20, max: 2000,
                tooltip: () => "次数越多概率越准，但按键时卡顿越久。默认 300。");
            modsEntry = cfg.Define("includeOtherMods", false, "General", () => "预测时也运行其他 mod 的顾客生成钩子",
                tooltip: () => "默认关闭：其他 mod 的内部状态不在本 mod 的快照范围内，开启后它们在沙盒里被反复调用可能改变其状态。");
            nativeEntry = cfg.Define("nativeUI", true, "General", () => "使用游戏原生风格的窗口",
                tooltip: () => "开启：用游戏自己的平板界面显示预测（可拖动、挡住鼠标点击）。关闭：改用旧版简易窗口。原生窗口出错时会自动退回简易窗口。");
            conflictCheckEntry = cfg.Define("checkCheatMenuHotkey", true, "General", () => "启动时检查与作弊菜单的热键冲突（一次性）",
                tooltip: () => "开启时，下次进入游戏会检查一次：如果本 mod 的热键是 F8，而作弊菜单（chezhou.cheat-menu）已加载且也用 F8，就把本 mod 的热键改成 F9。检查完自动关闭，之后不会再改你的设置。");
            hotkeyEntry.OnChanged += _ => Apply();
            runsEntry.OnChanged += _ => Apply();
            modsEntry.OnChanged += _ => Apply();
            nativeEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save(); // 让配置文件带着默认值出现，方便手改
        }

        private void Apply()
        {
            try
            {
                var text = (hotkeyEntry?.Value ?? DefaultHotkey).Trim();
                if (Enum.TryParse(text, true, out KeyCode k) && k != KeyCode.None) Hotkey = k;
                else
                {
                    Hotkey = KeyCode.F9;
                    Log.Warn("无法识别的热键 '" + text + "'，改用 " + DefaultHotkey);
                }
                Runs = Mathf.Clamp(runsEntry?.Value ?? 300, 20, 2000);
                IncludeOtherMods = modsEntry?.Value ?? false;
                NativeUI = nativeEntry?.Value ?? true;
            }
            catch (Exception e)
            {
                Log.Warn("读取配置失败，使用默认值: " + e.Message);
            }
        }

        /// <summary>
        /// 一次性的热键冲突检查（1.2.0 起热键默认 F9）。必须在所有 mod 都加载完之后调用：
        /// ModLoader 在自己的 Awake 里同步加载全部 mod，所以本 mod 第一次 Update 时一定已经加载完毕。
        /// 只在“本 mod 的热键是 F8、作弊菜单已加载且它的热键也是 F8”时改成 F9；无论结果如何，检查完就把开关关掉。
        /// </summary>
        internal void CheckHotkeyConflictOnce()
        {
            if (conflictCheckEntry == null || hotkeyEntry == null || !conflictCheckEntry.Value) return;
            string mine = (hotkeyEntry.Value ?? "").Trim();
            bool cheatLoaded = CheatMenuLoaded(out string cheatKey);
            bool cheatUsesF8 = cheatKey == null || LastKeyPart(cheatKey).Equals("F8", StringComparison.OrdinalIgnoreCase);
            if (cheatLoaded && cheatUsesF8 && mine.Equals("F8", StringComparison.OrdinalIgnoreCase))
            {
                hotkeyEntry.Set(DefaultHotkey); // Set 会触发 Apply 并保存
                Log.Info("检测到作弊菜单 mod（" + CheatMenuId + "）也在用 F8，本 mod 的热键已自动从 F8 改为 " + DefaultHotkey +
                         "（只改这一次；想改回可在 mod 设置里修改 hotkey）。");
            }
            else
            {
                Log.Info("热键冲突检查：不需要修改（本 mod 热键 " + mine + "，作弊菜单" +
                         (cheatLoaded ? "已加载，热键 " + (cheatKey ?? "未知") : "未加载") + "）。");
            }
            conflictCheckEntry.Set(false);
        }

        private static bool CheatMenuLoaded(out string cheatKey)
        {
            cheatKey = null;
            bool loaded = false;
            var mods = ModLoader.Mods;
            if (mods != null)
            {
                foreach (var m in mods)
                {
                    if (m?.Manifest != null && m.Manifest.ID == CheatMenuId && m.IsEnabled)
                    {
                        loaded = true;
                        break;
                    }
                }
            }
            if (!loaded) return false;
            try
            {
                // 作弊菜单把自己的热键存在 ModConfig 的 "menu-hotkey"（如 "F8"、"Ctrl+F8"、"None"）
                var entry = ModConfig.Find(CheatMenuId)?.Get("menu-hotkey");
                if (entry?.BoxedValue is string s && s.Trim().Length > 0) cheatKey = s.Trim();
            }
            catch { }
            return true;
        }

        private static string LastKeyPart(string binding)
        {
            var parts = binding.Split('+');
            return parts[parts.Length - 1].Trim();
        }
    }
}
