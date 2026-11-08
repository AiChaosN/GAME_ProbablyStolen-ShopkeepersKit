using System;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 配置用游戏自带的 ModConfig 保存（ModConfigs/SortToBoxes.json），也能在游戏的改装件界面点“设置”修改。
    /// 不自己读写文件，所以 mod 不需要 “files” 权限。
    /// </summary>
    internal sealed class SortConfig
    {
        internal const string DefaultHotkey = "F7";

        internal KeyCode Hotkey = KeyCode.F7;
        internal bool AutoOnPurchase;
        internal int MinMatchPercent = 20;

        private ConfigEntry<string> hotkeyEntry;
        private ConfigEntry<bool> autoEntry;
        private ConfigEntry<int> minMatchEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            hotkeyEntry = cfg.Define("hotkey", DefaultHotkey, "General", () => "热键（归类入箱）",
                choices: null,
                tooltip: () => "Unity KeyCode 名称，例如 F7、F10、Insert。按热键：把仓库里的散件归进对应箱子（鼠标指着柜台时整理柜台）。Shift+热键：开关“买下时自动归箱”。默认 F7（F5/F6/F8/F9 已被其他 mod 占用）。");
            autoEntry = cfg.Define("autoOnPurchase", false, "General", () => "买下顾客的货时自动归箱",
                tooltip: () => "开启后，每次成交（收购、批发、以物易物）游戏把货移进仓库时，直接送进对应的箱子；没有对应箱子或放不下就照旧留在仓库。游戏里按 Shift+热键 也能切换。");
            minMatchEntry = cfg.Define("minMatchPercent", 20, "General", () => "按类型归箱的最低占比（%）",
                min: 0, max: 100,
                tooltip: () => "箱子里与这件货同类型的物品至少要占多少比例，才算“对应的箱子”。避免一个杂物箱把什么都吸走。箱子里有一模一样的物品时不受这个限制。默认 20。");
            hotkeyEntry.OnChanged += _ => Apply();
            autoEntry.OnChanged += _ => Apply();
            minMatchEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save(); // 让配置文件带着默认值出现，方便手改
        }

        internal void SetAuto(bool on)
        {
            if (autoEntry != null) autoEntry.Set(on); // Set 会触发 Apply 并保存
            else AutoOnPurchase = on;
        }

        private void Apply()
        {
            try
            {
                var text = (hotkeyEntry?.Value ?? DefaultHotkey).Trim();
                if (Enum.TryParse(text, true, out KeyCode k) && k != KeyCode.None) Hotkey = k;
                else
                {
                    Hotkey = KeyCode.F7;
                    Log.Warn("无法识别的热键 '" + text + "'，改用 " + DefaultHotkey);
                }
                AutoOnPurchase = autoEntry?.Value ?? false;
                MinMatchPercent = Mathf.Clamp(minMatchEntry?.Value ?? 20, 0, 100);
            }
            catch (Exception e)
            {
                Log.Warn("读取配置失败，使用默认值: " + e.Message);
            }
        }
    }
}
