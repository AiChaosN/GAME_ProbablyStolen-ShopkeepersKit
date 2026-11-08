using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 热键处理：
    ///   热键        → 归箱（默认整理仓库；鼠标指着柜台时整理柜台上属于你的货）
    ///   Shift+热键  → 开关“买下时自动归箱”
    /// 自动归箱的提示延后显示：成交那一刻游戏会弹声望提示，两者共用同一个提示框，立刻弹会把声望提示盖掉。
    /// </summary>
    internal sealed class SortBehaviour : MonoBehaviour
    {
        internal static SortBehaviour Instance;

        private const float AutoToastDelay = 3f;
        private int autoPendingMoved;
        private int autoPendingLeft;
        private float autoToastAt = -1f;

        private void Awake() => Instance = this;

        internal void OnGameLoaded()
        {
            autoPendingMoved = 0;
            autoPendingLeft = 0;
            autoToastAt = -1f;
        }

        private void Update()
        {
            try
            {
                var cfg = SortToBoxesMod.Config;
                if (cfg != null && Input.GetKeyDown(cfg.Hotkey) && !IsTyping())
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ToggleAuto(cfg);
                    else RunManual(cfg);
                }
                if (autoToastAt >= 0f && Time.unscaledTime >= autoToastAt)
                {
                    autoToastAt = -1f;
                    string msg = "已自动归箱 " + autoPendingMoved + " 件";
                    if (autoPendingLeft > 0) msg += "，" + autoPendingLeft + " 件没有对应箱子或放不下，留在仓库";
                    autoPendingMoved = 0;
                    autoPendingLeft = 0;
                    Notify(msg);
                }
            }
            catch (Exception e)
            {
                Log.Error("Update 出错: " + Log.Describe(e));
            }
        }

        /// <summary>由成交补丁调用：攒一下，几秒后合并成一条提示。</summary>
        internal void ReportAuto(SortReport r)
        {
            if (r == null || r.Considered == 0) return;
            autoPendingMoved += r.Moved + r.Partial;
            autoPendingLeft += r.NoMatch + r.NoRoom + r.Partial;
            if (r.Moved + r.Partial > 0) autoToastAt = Time.unscaledTime + AutoToastDelay;
        }

        private static void ToggleAuto(SortConfig cfg)
        {
            bool on = !cfg.AutoOnPurchase;
            cfg.SetAuto(on);
            Notify("买下时自动归箱：" + (on ? "已开启" : "已关闭"));
            Log.Info("买下时自动归箱：" + (on ? "开" : "关"));
        }

        private static void RunManual(SortConfig cfg)
        {
            var entry = EmporiumEntry.Instance;
            if (entry == null || entry.invElement == null || PlayerStore.Instance == null) return; // 主菜单等
            if (MapUIManager.Instance != null && MapUIManager.Instance.isOutside)
            {
                Notify("外出时不能归箱");
                return;
            }
            if ((ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem) ||
                (ItemMultiSelectHandler.current != null && ItemMultiSelectHandler.current.IsGroupDragging))
            {
                Notify("先放下手里的东西再归箱");
                return;
            }

            GameInventory source = PickSource(entry, out string sourceName);
            var boxes = Sorter.CollectBoxes();
            if (boxes.Count == 0)
            {
                Notify("仓库里没有可用的箱子（储藏区、工具箱、背包）");
                return;
            }
            if (boxes.All(b => b.Total == 0))
            {
                Notify("箱子都是空的：先往每个箱子里放一件样品，它就会收同类的货");
                return;
            }

            var snapshot = source.childItems.ToList();
            var report = Sorter.SortItems(snapshot, boxes, cfg.MinMatchPercent);
            Notify(Summary(report, sourceName));
            Log.Info("归箱（" + sourceName + "）：箱子 " + boxes.Count + " 个，散件 " + report.Considered + " 件，归箱 " + report.Moved +
                     "，部分 " + report.Partial + "，无对应 " + report.NoMatch + "，放不下 " + report.NoRoom +
                     (report.Details.Count > 0 ? "\n  " + string.Join("\n  ", report.Details) : ""));
        }

        /// <summary>鼠标指着柜台（任一侧）时整理柜台，否则整理仓库。</summary>
        private static GameInventory PickSource(EmporiumEntry entry, out string name)
        {
            GameInventory hovered = null;
            try { hovered = RenderHandler.RaycastFindParentElement<GameInventory>(Input.mousePosition); }
            catch { }
            if (hovered != null && (ReferenceEquals(hovered, entry.backInvinvElement) || ReferenceEquals(hovered, entry.frontInvinvElement)))
            {
                name = "柜台";
                return hovered;
            }
            name = "仓库";
            return entry.invElement;
        }

        private static string Summary(SortReport r, string sourceName)
        {
            if (r.Considered == 0) return sourceName + "里没有要归箱的散件";
            var parts = new List<string>();
            int placed = r.Moved + r.Partial;
            if (placed > 0)
            {
                var boxes = r.PerBox.OrderByDescending(kv => kv.Value).ToList();
                string detail = string.Join("、", boxes.Take(3).Select(kv => kv.Key + " " + kv.Value));
                if (boxes.Count > 3) detail += " 等";
                parts.Add("归箱 " + placed + " 件（" + detail + "）");
            }
            if (r.NoMatch > 0) parts.Add(r.NoMatch + " 件没有对应箱子");
            int noRoom = r.NoRoom + r.Partial;
            if (noRoom > 0) parts.Add(noRoom + " 件箱子放不下");
            return string.Join("；", parts);
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
            try { return StoreUIManager.IsAnyTyping(); }
            catch { return false; }
        }
    }
}
