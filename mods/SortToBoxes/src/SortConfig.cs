using System;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 配置用游戏自带的 ModConfig 保存（ModConfigs/&lt;mod ID&gt;.json），也能在游戏的改装件界面点“设置”修改。
    /// 不自己读写文件，所以 mod 不需要 “files” 权限。
    /// </summary>
    internal sealed class SortConfig
    {
        internal const string DefaultHotkey = "F7";
        internal const string DefaultSellHotkey = "F4";

        internal KeyCode Hotkey = KeyCode.F7;
        internal KeyCode SellHotkey = KeyCode.F4;
        internal bool AutoOnPurchase;
        internal bool AutoSameOnCounter = true;
        internal bool BoxButtons = true;
        internal bool BoxIcons = true;

        private ConfigEntry<string> hotkeyEntry;
        private ConfigEntry<string> sellHotkeyEntry;
        private ConfigEntry<bool> autoEntry;
        private ConfigEntry<bool> sameEntry;
        private ConfigEntry<bool> buttonsEntry;
        private ConfigEntry<bool> iconsEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            hotkeyEntry = cfg.Define("hotkey", DefaultHotkey, "General", () => "归箱热键",
                choices: null,
                tooltip: () => "Unity KeyCode 名称，例如 F7、F10、Insert。热键：仓库散件归箱；指着柜台：把柜台上你的货放回箱子/仓库；Ctrl+热键 指着箱子：设置箱子分类；Shift+热键：开关“买下时自动归箱”。");
            sellHotkeyEntry = cfg.Define("sellHotkey", DefaultSellHotkey, "General", () => "上柜台热键",
                choices: null,
                tooltip: () => "热键：把当前买家会买的货全部摆上柜台；指着某件货：把它的同款全部摆上柜台；Shift+热键：开关“放一件上柜台时带上同款”。");
            autoEntry = cfg.Define("autoOnPurchase", false, "General", () => "买下顾客的货时自动归箱",
                tooltip: () => "成交（收购、批发、以物易物）时游戏把货移进仓库后，直接送进对应的箱子；没有对应箱子或放不下就留在仓库。");
            sameEntry = cfg.Define("autoSameOnCounter", true, "General", () => "放一件上柜台时带上同款",
                tooltip: () => "柜台前有买家时，你从仓库或箱子里拿一件货放上柜台，库存里一模一样的货会自动跟着摆上去。");
            buttonsEntry = cfg.Define("boxButtons", true, "General", () => "箱子窗口旁显示“分类”按钮",
                tooltip: () => "打开箱子时，在窗口右上角外侧显示一个“分类”按钮，点它设置这个箱子收哪些货。关掉后仍可用 Ctrl+归箱热键 指着箱子设置。");
            iconsEntry = cfg.Define("boxIcons", true, "General", () => "在箱子上显示里面物品的小图标",
                tooltip: () => "仓库里的箱子（储藏区等）正中间画一个小图标：箱子里第一件物品的图标；空箱子勾了分类时用同类物品的图标或类型颜色。只影响显示。");
            hotkeyEntry.OnChanged += _ => Apply();
            iconsEntry.OnChanged += _ => Apply();
            sellHotkeyEntry.OnChanged += _ => Apply();
            autoEntry.OnChanged += _ => Apply();
            sameEntry.OnChanged += _ => Apply();
            buttonsEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save(); // 让配置文件带着默认值出现，方便手改
        }

        internal void SetAuto(bool on)
        {
            if (autoEntry != null) autoEntry.Set(on); // Set 会触发 Apply 并保存
            else AutoOnPurchase = on;
        }

        internal void SetAutoSame(bool on)
        {
            if (sameEntry != null) sameEntry.Set(on);
            else AutoSameOnCounter = on;
        }

        private static KeyCode ParseKey(ConfigEntry<string> entry, string fallback)
        {
            var text = (entry?.Value ?? fallback).Trim();
            if (Enum.TryParse(text, true, out KeyCode k) && k != KeyCode.None) return k;
            Log.Warn("无法识别的热键 '" + text + "'，改用 " + fallback);
            return (KeyCode)Enum.Parse(typeof(KeyCode), fallback);
        }

        private void Apply()
        {
            try
            {
                Hotkey = ParseKey(hotkeyEntry, DefaultHotkey);
                SellHotkey = ParseKey(sellHotkeyEntry, DefaultSellHotkey);
                if (SellHotkey == Hotkey)
                {
                    Log.Warn("上柜台热键与归箱热键相同，上柜台改用 " + (Hotkey == KeyCode.F4 ? DefaultHotkey : DefaultSellHotkey));
                    SellHotkey = Hotkey == KeyCode.F4 ? KeyCode.F7 : KeyCode.F4;
                }
                AutoOnPurchase = autoEntry?.Value ?? false;
                AutoSameOnCounter = sameEntry?.Value ?? true;
                BoxButtons = buttonsEntry?.Value ?? true;
                BoxIcons = iconsEntry?.Value ?? true;
            }
            catch (Exception e)
            {
                Log.Warn("读取配置失败，使用默认值: " + e.Message);
            }
        }
    }
}
