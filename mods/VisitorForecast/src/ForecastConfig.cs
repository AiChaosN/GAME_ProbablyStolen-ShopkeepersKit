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

        internal KeyCode Hotkey = KeyCode.F9;
        internal int Runs = 300;
        internal bool IncludeOtherMods;
        internal bool NativeUI = true;

        private ConfigEntry<string> hotkeyEntry;
        private ConfigEntry<string> runsEntry;
        internal static readonly string[] RunChoices = { "100", "200", "300", "500", "800", "1000", "1500", "2000" };
        private ConfigEntry<bool> modsEntry;
        private ConfigEntry<bool> nativeEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            hotkeyEntry = cfg.Define("hotkey", DefaultHotkey, "General", () => "热键（打开/关闭预测窗口）",
                choices: null,
                tooltip: () => "Unity KeyCode 名称，例如 F9、F7、F10、Insert、Home。改完立即生效。默认 F9（创意工坊的作弊菜单默认占用 F8）。");
            // 用下拉框而不是滑块：游戏的设置界面画整数滑块时不显示数值
            runsEntry = cfg.Define("runs", "300", "General", () => "每次预测的模拟次数",
                choices: RunChoices,
                tooltip: () => "次数越多概率越准，但按键时卡顿越久。默认 300。");
            modsEntry = cfg.Define("includeOtherMods", false, "General", () => "预测时也运行其他 mod 的顾客生成钩子",
                tooltip: () => "默认关闭：其他 mod 的内部状态不在本 mod 的快照范围内，开启后它们在沙盒里被反复调用可能改变其状态。");
            nativeEntry = cfg.Define("nativeUI", true, "General", () => "使用游戏原生风格的窗口",
                tooltip: () => "开启：用游戏自己的平板界面显示预测（可拖动、挡住鼠标点击）。关闭：改用旧版简易窗口。原生窗口出错时会自动退回简易窗口。");
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
                Runs = int.TryParse((runsEntry?.Value ?? "300").Trim(), out int r) ? Mathf.Clamp(r, 20, 2000) : 300;
                IncludeOtherMods = modsEntry?.Value ?? false;
                NativeUI = nativeEntry?.Value ?? true;
            }
            catch (Exception e)
            {
                Log.Warn("读取配置失败，使用默认值: " + e.Message);
            }
        }

    }
}
