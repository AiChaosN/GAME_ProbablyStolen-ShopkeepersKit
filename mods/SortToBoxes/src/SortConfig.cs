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
        internal bool OnlyAffordable = true;

        private ConfigEntry<string> hotkeyEntry;
        private ConfigEntry<string> sellHotkeyEntry;
        private ConfigEntry<bool> autoEntry;
        private ConfigEntry<bool> sameEntry;
        private ConfigEntry<bool> buttonsEntry;
        private ConfigEntry<bool> iconsEntry;
        private ConfigEntry<bool> affordEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            hotkeyEntry = cfg.Define("hotkey", DefaultHotkey, "General", () => Lang.T("归箱热键", "Sort hotkey"),
                choices: null,
                tooltip: () => Lang.T("Unity KeyCode 名称（例如 F7、Insert），也可以直接填一个符号或字母；在游戏“选项”里还能按一下键绑定。热键：仓库散件归箱；指着柜台：把柜台上你的货放回箱子/仓库；Ctrl+热键 指着箱子：设置箱子分类；Shift+热键：开关“买下时自动归箱”。",
                    "Unity KeyCode name (e.g. F7, Insert) or just a symbol or letter; you can also bind it by pressing a key in the game's Options. Key: sort loose items in storage; pointing at the counter: put your items there back into boxes/storage; Ctrl+key on a box: pick its categories; Shift+key: toggle auto-sort on purchase."));
            sellHotkeyEntry = cfg.Define("sellHotkey", DefaultSellHotkey, "General", () => Lang.T("上柜台热键", "Counter hotkey"),
                choices: null,
                tooltip: () => Lang.T("热键：把当前买家会买的货全部摆上柜台；指着某件货：把它的同款全部摆上柜台；Shift+热键：开关“放一件上柜台时带上同款”。",
                    "Key: put everything the current buyer would buy on the counter; pointing at an item: put all identical items on the counter; Shift+key: toggle \"bring identical items when placing one\"."));
            autoEntry = cfg.Define("autoOnPurchase", false, "General", () => Lang.T("买下顾客的货时自动归箱", "Auto-sort goods you buy"),
                tooltip: () => Lang.T("成交（收购、批发、以物易物）时游戏把货移进仓库后，直接送进对应的箱子；没有对应箱子或放不下就留在仓库。",
                    "When a deal (purchase, wholesale, barter) moves goods into storage, send them straight to their box; without a matching box, or if it's full, they stay in storage."));
            sameEntry = cfg.Define("autoSameOnCounter", false, "General", () => Lang.T("放一件上柜台时带上同款", "Bring identical items when placing one"),
                tooltip: () => Lang.T("柜台前有买家时，你从仓库或箱子里拿一件货放上柜台，库存里的同款会自动跟着摆上去。默认关：要整批摆，指着货按上柜台热键更直接。",
                    "While a buyer is at the counter, moving one item from storage or a box onto the counter brings all identical items from your stock with it. Off by default: to put a whole batch up, point at an item and press the counter hotkey."));
            buttonsEntry = cfg.Define("boxButtons", true, "General", () => Lang.T("箱子窗口标题栏显示“分类”按钮", "Show the Sort button in box windows"),
                tooltip: () => Lang.T("打开箱子时，在窗口标题栏的关闭按钮（×）左边显示一个“分类”按钮，点它设置这个箱子收哪些货。关掉后仍可用 Ctrl+归箱热键 指着箱子设置。",
                    "When a box is open, show a Sort button left of the close button (×) in its title bar; click it to choose what the box takes. Without it, use Ctrl+sort hotkey while pointing at the box."));
            iconsEntry = cfg.Define("boxIcons", true, "General", () => Lang.T("在箱子上显示里面物品的小图标", "Show a content badge on boxes"),
                tooltip: () => Lang.T("仓库里的箱子（储藏区等）右下角画一个小角标：箱子里第一件物品的图标；空箱子勾了分类时用同类物品的图标或类别颜色。只影响显示。",
                    "Draw a small badge in the bottom-right corner of each box in storage: the icon of its first item; an empty box with categories ticked shows an item of that category or the category's color. Display only."));
            affordEntry = cfg.Define("onlyAffordable", true, "General", () => Lang.T("上柜台时只摆顾客买得起的", "Counter key: only items the buyer can afford"),
                tooltip: () => Lang.T("开启时，F4 只把当前顾客预算买得起的货摆上柜台（买不起的摆上去他也不会买）。关闭则买不起的也摆，排在后面。",
                    "When on, F4 only puts items within the current buyer's budget on the counter (they won't buy the rest anyway). When off, unaffordable items are added after the others."));
            hotkeyEntry.OnChanged += _ => Apply();
            affordEntry.OnChanged += _ => Apply();
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
            var fb = (KeyCode)Enum.Parse(typeof(KeyCode), fallback);
            return KeyNames.Parse(entry?.Value ?? fallback, fb, Log.Warn);
        }

        /// <summary>在游戏“选项”里加两个按键绑定（按一下键即可改），和改装件设置里的是同一份设置。</summary>
        internal void RegisterGameOptions(string modId)
        {
            try
            {
                ModHelper.AddGameOptionSection(modId, "keys", () => Lang.T("掌柜帮手·归类入箱", "Shopkeeper's Kit: Sort To Boxes"));
                ModHelper.AddGameOptionKeyBind(modId, "keys", "hotkey", () => Lang.T("归箱（Shift：开关买下自动归箱，Ctrl：箱子分类）", "Sort (Shift: toggle auto-sort on purchase, Ctrl: box categories)"), () => Hotkey,
                    k => { if (k != KeyCode.None) hotkeyEntry?.Set(k.ToString()); },
                    () => Lang.T("仓库散件归箱；指着柜台：把柜台上你的货放回箱子/仓库。默认 F7", "Sort loose items in storage; pointing at the counter: put your items back into boxes/storage. Default F7"));
                ModHelper.AddGameOptionKeyBind(modId, "keys", "sell_hotkey", () => Lang.T("上柜台（Shift：开关放一件带同款）", "Counter (Shift: toggle bring identical items)"), () => SellHotkey,
                    k => { if (k != KeyCode.None) sellHotkeyEntry?.Set(k.ToString()); },
                    () => Lang.T("把当前买家会买的货摆上柜台；指着某件货：把它的同款摆上柜台。默认 F4", "Put everything the current buyer would buy on the counter; pointing at an item: put its copies on the counter. Default F4"));
            }
            catch (Exception e)
            {
                Log.Warn("登记游戏选项失败（仍可在改装件设置里改热键）: " + e.Message);
            }
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
                OnlyAffordable = affordEntry?.Value ?? true;
            }
            catch (Exception e)
            {
                Log.Warn("读取配置失败，使用默认值: " + e.Message);
            }
        }
    }
}
