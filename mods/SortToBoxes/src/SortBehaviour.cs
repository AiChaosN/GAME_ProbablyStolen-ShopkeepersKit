using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 热键与调度（默认 F7 = 归箱热键，F4 = 上柜台热键）：
    ///   F7            仓库散件归箱
    ///   指着柜台 F7    柜台上你的货放回箱子，没有对应箱子的放回仓库
    ///   Ctrl+F7       指着箱子（箱子本身、或打开的箱子窗口里）：打开分类面板
    ///   Shift+F7      开关“买下时自动归箱”
    ///   F4            把当前买家会买的货全部摆上柜台
    ///   指着某件货 F4  把它的同款全部摆上柜台
    ///   Shift+F4      开关“放一件上柜台时带上同款”
    /// 自动归箱的提示延后显示：成交那一刻游戏会弹声望提示，两者共用同一个提示框，立刻弹会把声望提示盖掉。
    /// </summary>
    internal sealed class SortBehaviour : MonoBehaviour
    {
        internal static SortBehaviour Instance;

        private const float AutoToastDelay = 3f;
        private int autoPendingMoved;
        private int autoPendingLeft;
        private float autoToastAt = -1f;

        // “带上同款”：补丁里登记，下一帧执行
        private readonly List<KeyValuePair<string, GameItem>> sameQueue = new List<KeyValuePair<string, GameItem>>();
        private int sameQueuedFrame = -1;

        private readonly BoxButtons buttons = new BoxButtons();
        private readonly BoxIcons icons = new BoxIcons();

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            try { buttons.Clear(); } catch { }
            try { icons.Clear(); } catch { }
        }

        internal void OnGameLoaded()
        {
            autoPendingMoved = 0;
            autoPendingLeft = 0;
            autoToastAt = -1f;
            sameQueue.Clear();
            buttons.Clear();
            icons.Clear();
            SeenItems.OnGameLoaded();
        }

        internal void QueueSame(GameItem moved)
        {
            if (moved == null || string.IsNullOrEmpty(moved.identifier)) return;
            string key = Stock.VariantKey(moved);   // 同款按看得到的标签区分（酒精瓶 ≠ 水瓶，不同等级的水 ≠ 同款）
            if (sameQueue.Any(p => p.Key == key)) return;
            sameQueue.Add(new KeyValuePair<string, GameItem>(key, moved));
            sameQueuedFrame = Time.frameCount;
        }

        private void Update()
        {
            var cfg = SortToBoxesMod.Config;
            try
            {
                if (cfg != null && !IsTyping())
                {
                    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    if (Input.GetKeyDown(cfg.Hotkey))
                    {
                        if (shift) ToggleAuto(cfg);
                        else if (ctrl) OpenLabelsUnderMouse();
                        else RunSort();
                    }
                    else if (Input.GetKeyDown(cfg.SellHotkey))
                    {
                        if (shift) ToggleAutoSame(cfg);
                        else RunSell();
                    }
                }

                if (sameQueue.Count > 0 && Time.frameCount > sameQueuedFrame) RunQueuedSame();

                SeenItems.Tick();   // 记录本存档见过的物品（分类面板用）

                if (autoToastAt >= 0f && Time.unscaledTime >= autoToastAt)
                {
                    autoToastAt = -1f;
                    string msg = Lang.T("已自动归箱 " + autoPendingMoved + " 件", "Auto-sorted " + autoPendingMoved + " item(s)");
                    if (autoPendingLeft > 0) msg += Lang.T("，" + autoPendingLeft + " 件没有对应箱子或放不下，留在仓库", "; " + autoPendingLeft + " had no matching box or didn't fit and stayed in storage");
                    autoPendingMoved = 0;
                    autoPendingLeft = 0;
                    Notify(msg);
                }
            }
            catch (Exception e)
            {
                sameQueue.Clear();
                Log.Error("Update 出错: " + Log.Describe(e));
            }

            buttons.Tick(cfg != null && cfg.BoxButtons); // 两者都自己吞异常
            icons.Tick(cfg != null && cfg.BoxIcons);
        }

        /// <summary>由成交补丁调用：攒一下，几秒后合并成一条提示。</summary>
        internal void ReportAuto(SortReport r)
        {
            if (r == null || r.Considered == 0) return;
            autoPendingMoved += r.Placed;
            autoPendingLeft += r.NoMatch + r.NoRoom + r.Partial;
            if (r.Placed > 0) autoToastAt = Time.unscaledTime + AutoToastDelay;
        }

        // ---------------------------------------------------------------- 归箱

        private static bool Ready(bool notify)
        {
            var entry = EmporiumEntry.Instance;
            if (entry == null || entry.invElement == null || Game.Store == null) return false; // 主菜单等
            if (MapUIManager.Instance != null && MapUIManager.Instance.isOutside)
            {
                if (notify) Notify(Lang.T("外出时用不了", "Not available while you are out"));
                return false;
            }
            if ((ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem) ||
                (ItemMultiSelectHandler.current != null && ItemMultiSelectHandler.current.IsGroupDragging))
            {
                if (notify) Notify(Lang.T("先放下手里的东西", "Put down what you are holding first"));
                return false;
            }
            return true;
        }

        private static void RunSort()
        {
            if (!Ready(true)) return;
            var entry = EmporiumEntry.Instance;
            var hovered = HoveredInventory();
            if (hovered != null && (ReferenceEquals(hovered, entry.backInvinvElement) || ReferenceEquals(hovered, entry.frontInvinvElement)))
            {
                var r = Counter.ClearCounter(hovered);
                Notify(r.Considered == 0 ? Lang.T("柜台上没有你的货", "None of your items are on the counter") : Summary(r, Kind.Return));
                LogReport("清空柜台", r);
                return;
            }

            var boxes = Sorter.CollectBoxes();
            if (boxes.Count == 0)
            {
                Notify(Lang.T("仓库里没有可用的箱子（储藏区、工具箱、背包）", "No usable boxes in storage (storage bays, toolbox, backpacks)"));
                return;
            }
            if (boxes.All(b => b.Total == 0 && b.Labels == null))
            {
                Notify(Lang.T("箱子都是空的：往箱子里放一件样品，或 Ctrl+" + KeyNames.Label(SortToBoxesMod.Config.Hotkey) + " 指着箱子勾选分类",
                    "All boxes are empty: put a sample item in a box, or point at a box and press Ctrl+" + KeyNames.Label(SortToBoxesMod.Config.Hotkey) + " to pick its categories"));
                return;
            }
            var report = Sorter.SortItems(entry.invElement.childItems.ToList(), boxes);
            Notify(report.Considered == 0 ? Lang.T("仓库里没有要归箱的散件", "Nothing loose in storage to sort") : Summary(report, Kind.Sort));
            LogReport("归箱（箱子 " + boxes.Count + " 个）", report);
        }

        private static void OpenLabelsUnderMouse()
        {
            if (!Ready(true)) return;
            var box = BoxUnderMouse();
            if (box == null)
            {
                Notify(Lang.T("把鼠标指着一个箱子（或打开的箱子窗口里）再按 Ctrl+" + KeyNames.Label(SortToBoxesMod.Config.Hotkey),
                    "Point at a box (or inside an open box window) and press Ctrl+" + KeyNames.Label(SortToBoxesMod.Config.Hotkey)));
                return;
            }
            BoxLabelPanel.Open(box);
        }

        private static void ToggleAuto(SortConfig cfg)
        {
            bool on = !cfg.AutoOnPurchase;
            cfg.SetAuto(on);
            Notify(Lang.T("买下时自动归箱：" + (on ? "已开启" : "已关闭"), "Auto-sort on purchase: " + (on ? "on" : "off")));
            Log.Info("买下时自动归箱：" + (on ? "开" : "关"));
        }

        // ---------------------------------------------------------------- 上柜台

        private static void RunSell()
        {
            if (!Ready(true)) return;
            var item = HoveredItem();
            if (item != null && !string.IsNullOrEmpty(item.identifier) && GeneralHelper.IsItemOwned(item) && !Stock.IsFixture(item))
            {
                var r = Counter.PutSame(Stock.VariantKey(item), null);
                Notify(r.Considered == 0 ? Lang.T("仓库和箱子里没有 " + Stock.Name(item) + " 的同款", "No more " + Stock.Name(item) + " in storage or boxes")
                                          : Summary(r, Kind.Counter, Lang.T(Stock.Name(item) + " 的同款", "copies of " + Stock.Name(item))));
                LogReport("同款上柜台：" + Stock.Name(item), r);
                return;
            }
            if (!Counter.HasBuyer(out var client))
            {
                Notify(Lang.T("柜台前没有要买东西的顾客（指着某件货按 " + KeyNames.Label(SortToBoxesMod.Config.SellHotkey) + " 可把它的同款摆上柜台）",
                    "No buyer at the counter (point at an item and press " + KeyNames.Label(SortToBoxesMod.Config.SellHotkey) + " to put its copies on the counter)"));
                return;
            }
            var report = Counter.PutBuyable(client);
            Notify(report.Considered == 0 ? Lang.T("仓库和箱子里没有这位顾客要的货", "Nothing in storage or boxes that this customer wants")
                                           : Summary(report, Kind.Counter, Lang.T("顾客要的货", "items the customer wants")));
            LogReport("买家要的货上柜台", report);
        }

        private void RunQueuedSame()
        {
            var jobs = sameQueue.ToList();
            sameQueue.Clear();
            int placed = 0, noRoom = 0;
            foreach (var job in jobs)
            {
                var r = Counter.PutSame(job.Key, job.Value);
                placed += r.Placed;
                noRoom += r.NoRoom + r.Partial;
                if (r.Considered > 0) LogReport("放一件带同款：" + (job.Value?.identifier ?? "?"), r);
            }
            if (placed > 0 || noRoom > 0)
            {
                string msg = Lang.T("带上同款 " + placed + " 件", "Brought " + placed + " identical item(s)");
                if (noRoom > 0) msg += Lang.T("，" + noRoom + " 件柜台放不下", "; " + noRoom + " didn't fit on the counter");
                Notify(msg + Lang.T("（Shift+" + KeyNames.Label(SortToBoxesMod.Config.SellHotkey) + " 可关闭）", " (Shift+" + KeyNames.Label(SortToBoxesMod.Config.SellHotkey) + " to turn off)"));
            }
        }

        private static void ToggleAutoSame(SortConfig cfg)
        {
            bool on = !cfg.AutoSameOnCounter;
            cfg.SetAutoSame(on);
            Notify(Lang.T("放一件上柜台时带上同款：" + (on ? "已开启" : "已关闭"), "Bring identical items when placing one: " + (on ? "on" : "off")));
            Log.Info("放一件上柜台时带上同款：" + (on ? "开" : "关"));
        }

        // ---------------------------------------------------------------- 工具

        private static GameInventory HoveredInventory()
        {
            try { return RenderHandler.RaycastFindParentElement<GameInventory>(Input.mousePosition); }
            catch { return null; }
        }

        private static GameItem HoveredItem()
        {
            try { return RenderHandler.RaycastElement<GameItemElement>(Input.mousePosition); }
            catch { return null; }
        }

        /// <summary>鼠标下的箱子：指着箱子本身，或指着打开的箱子窗口里（含其中的物品）。</summary>
        private static GameItem BoxUnderMouse()
        {
            var item = HoveredItem();
            if (item != null && Stock.IsBox(item)) return item;
            GameInventory inv = item?.parentInventory ?? HoveredInventory();
            try
            {
                var parent = inv?.GetParentItem();
                if (parent != null && Stock.IsBox(parent)) return parent;
            }
            catch { }
            return null;
        }

        private enum Kind { Sort, Return, Counter }

        private static string Summary(SortReport r, Kind kind, string what = null)
        {
            bool zh = Lang.Zh;
            var parts = new List<string>();
            if (r.Placed > 0)
            {
                string detail = "";
                if (kind != Kind.Counter)
                {
                    var targets = r.PerTarget.OrderByDescending(kv => kv.Value).ToList();
                    string list = string.Join(zh ? "、" : ", ", targets.Take(3).Select(kv => kv.Key + " " + kv.Value));
                    if (targets.Count > 3) list += zh ? " 等" : ", ...";
                    detail = zh ? "（" + list + "）" : " (" + list + ")";
                }
                string verb = kind == Kind.Sort ? (zh ? "归箱" : "Sorted")
                            : kind == Kind.Return ? (zh ? "放回" : "Put back")
                            : (zh ? "摆上柜台" : "Put on the counter:");
                parts.Add(zh ? verb + " " + (what != null ? what + " " : "") + r.Placed + " 件" + detail
                             : verb + " " + r.Placed + (what != null ? " " + what : " item(s)") + detail);
            }
            if (r.ToWarehouse > 0) parts.Add(zh ? r.ToWarehouse + " 件放回仓库" : r.ToWarehouse + " back to storage");
            if (r.NoMatch > 0) parts.Add(zh ? r.NoMatch + " 件没有对应箱子" : r.NoMatch + " with no matching box");
            int noRoom = r.NoRoom + r.Partial;
            if (noRoom > 0) parts.Add(zh ? noRoom + " 件" + (kind == Kind.Counter ? "柜台" : "箱子") + "放不下"
                                         : noRoom + " didn't fit " + (kind == Kind.Counter ? "on the counter" : "in the boxes"));
            return parts.Count == 0 ? (zh ? "没有可搬的东西" : "Nothing to move") : string.Join(zh ? "；" : "; ", parts);
        }

        private static void LogReport(string title, SortReport r)
        {
            Log.Info(title + "：共 " + r.Considered + " 件，放好 " + r.Placed + "，回仓库 " + r.ToWarehouse + "，无对应 " + r.NoMatch + "，放不下 " + r.NoRoom +
                     (r.Details.Count > 0 ? "\n  " + string.Join("\n  ", r.Details) : ""));
        }

        internal static void Notify(string msg)
        {
            try
            {
                if (StoreUIManager.Instance != null) StoreUIManager.Instance.Notify(msg);
                else Log.Info(msg);
            }
            catch (Exception e)
            {
                Log.Info(msg + "（提示框不可用: " + e.Message + "）");
            }
        }

        private static bool IsTyping()
        {
            try { return StoreUIManager.IsAnyTyping() || KeyBindHelper.SuppressHotkeys; }   // 正在打字，或正在游戏设置里改键
            catch { return false; }
        }
    }
}
