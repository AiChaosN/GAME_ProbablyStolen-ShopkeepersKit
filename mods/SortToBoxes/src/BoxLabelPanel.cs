using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 箱子分类面板：用游戏自带的 CustomUI 框架（与游戏的“储物协调器”同一层、同一风格）建一个可拖动的窗口。
    /// 只列出本存档里见过的物品（SeenItems），按大类（Categories，每件货只属于一个类）分组：
    ///   - 勾“整类”：下面的货全部勾上，这一类以后才见到的货也进这个箱子（存 #类别）；
    ///   - 取消“整类”：下面的货全部取消；
    ///   - 单独勾/取消某件货：只影响这一件（存 @物品id）。整类勾着时取消其中一件，会改成“除了这件以外逐件勾选”。
    /// 勾选立即保存到箱子的 tag（随存档保存）。
    /// 用扩容套件升级过的储藏区有多栏：每栏单独设置（分类、不参与归箱、不参与上柜台），面板顶上的栏按钮切换正在设置的栏。
    /// </summary>
    internal static class BoxLabelPanel
    {
        private const string WindowId = "chaos5090_stb_box_labels";

        // 展开着的大类（本次游戏内记住，所有箱子共用）。默认全部收起，只显示“整类”那一行和已勾件数
        private static readonly HashSet<string> Expanded = new HashSet<string>(StringComparer.Ordinal);

        private sealed class Section
        {
            internal string Cat;
            internal GameObject Grid;
            internal CustomUIElement Button;
        }

        private static void SetExpanded(Section sec, bool on, bool zh)
        {
            if (sec?.Grid == null) return;
            if (on) Expanded.Add(sec.Cat); else Expanded.Remove(sec.Cat);
            try
            {
                sec.Grid.SetActive(on);
                sec.Button?.SetText(on ? (zh ? "收起" : "Hide") : (zh ? "展开" : "Show"));
                var content = sec.Grid.transform.parent as RectTransform;
                if (content != null) CustomUIBuilder.RebuildLayout(content);
            }
            catch { }
        }

        internal static string LayerId()
        {
            try { return CustomUIManager.HasLayer("paper") ? "paper" : "overlay"; }
            catch { return "overlay"; }
        }

        /// <summary>打开某个箱子第 tab 栏（从 0 起）的分类面板；tab &lt; 0 = 箱子窗口当前显示的那栏。</summary>
        internal static void Open(GameItem box, int tab = -1)
        {
            try
            {
                if (box == null || !Stock.IsBox(box)) return;
                int count = Stock.CompartmentCount(box);
                if (tab < 0 || tab >= count) tab = Stock.SelectedCompartment(box);
                if (tab >= count) tab = 0;
                bool multi = count > 1;
                var mgr = CustomUIManager.Instance;
                if (mgr == null)
                {
                    SortBehaviour.Notify(Lang.T("现在打不开分类面板（界面还没准备好）", "Can't open the category panel right now (UI not ready)"));
                    return;
                }

                Vector2? oldPos = null;
                var old = mgr.GetWindow(WindowId);
                if (old != null && old.IsAlive) oldPos = old.Rect.anchoredPosition;

                SeenItems.ScanNow();   // 先把眼前的东西记上
                var selected = BoxLabels.Get(box, tab) ?? new HashSet<string>(StringComparer.Ordinal);
                bool noSort = Stock.NoSort.IsOn(box, tab);

                // 库存里每种物品有几件（显示在名字后面）
                var stockCount = new Dictionary<string, int>(StringComparer.Ordinal);
                var liveNames = new Dictionary<string, string>(StringComparer.Ordinal);   // 手上有的货用游戏当前语言的名字
                foreach (var it in Stock.AllGoods())
                {
                    if (string.IsNullOrEmpty(it.identifier)) continue;
                    stockCount.TryGetValue(it.identifier, out int n);
                    stockCount[it.identifier] = n + Math.Max(1, it.unitCount);
                    if (!liveNames.ContainsKey(it.identifier))
                    {
                        try { liveNames[it.identifier] = it.GetDisplayName(false); } catch { }
                    }
                }

                // 这一栏现有物品的大类
                var inBox = new Dictionary<string, int>(StringComparer.Ordinal);
                var comps = Stock.Compartments(box);
                var inv = tab < comps.Count ? comps[tab] : null;
                foreach (var it in Stock.Children(inv))
                {
                    if (it == null || Stock.IsFixture(it)) continue;
                    var c = Categories.Of(it);
                    inBox.TryGetValue(c, out int n);
                    inBox[c] = n + 1;
                }

                // 见过的物品按大类分组；勾过但还没见过实物的类也列出来，方便取消
                var groups = SeenItems.All
                    .GroupBy(i => string.IsNullOrEmpty(i.Cat) ? Categories.Other : i.Cat)
                    .ToDictionary(g => g.Key, g => g.OrderBy(i => liveNames.TryGetValue(i.Id, out var ln) ? ln : i.Name, StringComparer.CurrentCulture).ToList(), StringComparer.Ordinal);
                foreach (var key in selected.Where(t => t.StartsWith("#")).Select(t => t.Substring(1)))
                    if (!groups.ContainsKey(key)) groups[key] = new List<SeenItems.Info>();
                var order = groups.Keys
                    .OrderBy(k => { int i = Array.IndexOf(Categories.Order, k); return i < 0 ? 999 : i; })
                    .ToList();

                bool zh = Lang.Zh;
                string unit = multi ? (zh ? "这一栏" : "this tab") : (zh ? "这个箱子" : "this box");
                int tabIndex = tab;
                var b = mgr.CreateWindow(WindowId, (zh ? "箱子分类 · " : "Box categories · ") + Stock.TargetName(box, tab, count), LayerId())
                    .SetSize(760f, multi ? 760f : 680f)
                    .SetDraggable(true)
                    .SetCloseOnEscape();

                if (multi)
                {
                    // 升级过的箱子：每栏单独设置，点栏名切换
                    b.AddLabel(zh ? "这个箱子升级过，有 " + count + " 栏，每栏单独设置。空着、没勾分类的栏是这个箱子的后备空间：其它栏放满了会接着放进去（一次归箱只接一类货）。"
                                  : "This box is upgraded with " + count + " tabs; each tab has its own settings. An empty tab with nothing ticked is spare room: when the box's other tabs are full, items continue into it (one category per sort).");
                    b.BeginRow(6f, TextAnchor.MiddleLeft);
                    for (int i = 0; i < count; i++)
                    {
                        int idx = i;
                        string name = Stock.CompartmentName(box, i);
                        bool cur = i == tab;
                        b.AddButton(cur ? (zh ? "【" + name + "】" : "[" + name + "]") : name, () => { if (idx != tabIndex) Open(box, idx); });
                    }
                    b.End();
                }

                b.AddLabel(zh ? "勾“整类”：这一类的货全部进" + unit + "，包括以后才见到的同类；也可以只勾其中几件。一个都不勾 = 按" + unit + "里现有的东西自动判断。"
                               : "Tick \"whole category\" to send every item of that category to " + unit + ", including ones you only see later; or tick just some items. Nothing ticked = decide by what " + unit + " already holds.");
                b.AddLabel(zh ? "只列出你在这个存档里见过的货，见过的越多，列表越长。名字后面的 ×N 是仓库和箱子里现有的件数。"
                               : "Only items you have seen in this save are listed; the list grows as you see more. ×N after a name is how many you have in storage and boxes.");
                string now = inBox.Count == 0
                    ? (zh ? unit + "现在是空的" : (multi ? "This tab" : "The box") + " is empty")
                    : (zh ? unit + "现有：" : (multi ? "In this tab: " : "In the box: ")) + string.Join(zh ? "、" : ", ", inBox.OrderByDescending(kv => kv.Value).Take(6).Select(kv => Categories.Name(kv.Key) + " " + kv.Value));
                b.AddLabel(now);
                var target = box;
                string sk = KeyNames.Label(SortToBoxesMod.Config?.SellHotkey ?? KeyCode.C);
                b.AddToggle(zh ? "不参与归箱：归箱、买下自动归箱、柜台收回都不往" + unit + "里放东西（例如你自己用的药品区、装备区）"
                               : "Keep out of sorting: sorting, auto-sort on purchase and taking items back from the counter never put anything into " + unit + " (e.g. your own medicine or gear)",
                    noSort, on =>
                    {
                        Stock.NoSort.Set(target, tabIndex, on);
                        Open(target, tabIndex);   // 重画：不参与归箱时分类列表不生效，收起来
                    });
                b.AddToggle(zh ? "不参与上柜台：" + sk + " 和“带上同款”都不会拿" + unit + "里的东西（放你自己要用的设备、不想卖的东西）"
                               : "Keep out of the counter: " + sk + " and \"bring identical items\" never take anything from " + unit + " (for your own equipment and things you won't sell)",
                    Stock.NoSell.IsOn(box, tab), on => Stock.NoSell.Set(target, tabIndex, on));
                b.AddSeparator();

                if (noSort)
                {
                    // 不参与归箱：分类勾选不生效，不列出来（勾选本身保留，关掉开关后照旧）
                    b.AddLabel(zh ? unit + "不参与归箱，分类勾选暂不生效（勾选会保留，关掉上面的开关后恢复）。" + unit + "里的东西你自己放、自己拿，归箱不会动它们。"
                                  : "Kept out of sorting: category ticks don't apply (they're kept and come back when you turn the switch off). You manage what's in " + unit + "; sorting never touches it.");
                    b.BeginRow(8f, TextAnchor.MiddleCenter);
                    b.AddButton(zh ? "完成" : "Done", () =>
                    {
                        try { mgr.CloseWindow(WindowId); } catch { }
                    });
                    b.End();
                    if (oldPos.HasValue) b.SetPosition(oldPos.Value);
                    else b.Center();
                    b.Show();
                    return;
                }

                // 大类默认收起：点每类前面的“展开”看里面的货；这两个按钮一次展开 / 收起全部
                var sections = new List<Section>();
                b.BeginRow(8f, TextAnchor.MiddleLeft);
                b.AddButton(zh ? "全部展开" : "Show all", () => { foreach (var sec in sections) SetExpanded(sec, true, zh); });
                b.AddButton(zh ? "全部收起" : "Hide all", () => { foreach (var sec in sections) SetExpanded(sec, false, zh); });
                b.End();

                b.BeginScroll(400f);
                if (order.Count == 0) b.AddLabel(zh ? "还没有见过任何货。" : "You haven't seen any goods yet.");
                foreach (var cat in order)
                {
                    var items = groups[cat];
                    string catKey = BoxLabels.CatKey(cat);
                    bool catOn = selected.Contains(catKey);
                    var itemToggles = new Dictionary<string, CustomUIElement>(StringComparer.Ordinal);
                    CustomUIElement catToggle = null;
                    var section = new Section { Cat = cat };

                    // 标题行写着这一类勾了几件，收起时也看得到
                    Func<string> catLabel = () =>
                    {
                        int n = items.Count;
                        int ticked = selected.Contains(catKey) ? n : items.Count(i => selected.Contains(BoxLabels.ItemKey(i.Id)));
                        return zh ? "【" + Categories.Name(cat) + "】整类" + (n > 0 ? "（已勾 " + ticked + "/" + n + "）" : "")
                                  : "[" + Categories.Name(cat) + "] whole category" + (n > 0 ? " (" + ticked + "/" + n + " ticked)" : "");
                    };

                    b.AddSeparator();
                    b.BeginRow(6f, TextAnchor.MiddleLeft);
                    if (items.Count > 0)
                    {
                        b.AddButton(Expanded.Contains(cat) ? (zh ? "收起" : "Hide") : (zh ? "展开" : "Show"),
                            () => SetExpanded(section, !Expanded.Contains(section.Cat), zh));
                        section.Button = b.Last;
                    }
                    b.AddToggle(catLabel(), catOn, on =>
                    {
                        selected.Remove(catKey);
                        foreach (var info in items) selected.Remove(BoxLabels.ItemKey(info.Id));   // 整类勾着时不需要逐件记
                        if (on) selected.Add(catKey);
                        foreach (var t in itemToggles.Values) t?.SetBool(on, notify: false);
                        BoxLabels.Set(box, tabIndex, selected);
                        catToggle?.SetText(catLabel());
                    });
                    catToggle = b.Last;
                    b.End();
                    if (items.Count == 0) continue;

                    b.BeginGrid(3, 232f, 34f, 4f);
                    foreach (var info in items)
                    {
                        string id = info.Id;
                        string key = BoxLabels.ItemKey(id);
                        stockCount.TryGetValue(id, out int have);
                        b.BeginRow(4f, TextAnchor.MiddleLeft);
                        var icon = SeenItems.Icon(info);
                        if (icon != null) b.AddImage(icon, 28f, 28f);
                        string shown = liveNames.TryGetValue(id, out var live) && !string.IsNullOrEmpty(live) ? live : (string.IsNullOrEmpty(info.Name) ? id : info.Name);
                        b.AddToggle(shown + (have > 0 ? " ×" + have : ""), catOn || selected.Contains(key), on =>
                        {
                            if (on)
                            {
                                if (!selected.Contains(catKey)) selected.Add(key);
                            }
                            else
                            {
                                if (selected.Remove(catKey))
                                {
                                    // 整类勾着时取消其中一件：改成其余的逐件勾选
                                    foreach (var other in items)
                                        if (other.Id != id) selected.Add(BoxLabels.ItemKey(other.Id));
                                    catToggle?.SetBool(false, notify: false);
                                }
                                selected.Remove(key);
                            }
                            BoxLabels.Set(box, tabIndex, selected);
                            catToggle?.SetText(catLabel());
                        });
                        itemToggles[id] = b.Last;
                        b.End();
                    }
                    b.End();

                    // 物品格子 = 行的父节点（BeginGrid 建的节点）：收起时整块隐藏，不用重画面板，滚动位置不变
                    try
                    {
                        var first = itemToggles.Values.FirstOrDefault(t => t?.root != null);
                        section.Grid = first?.root.transform.parent?.parent?.gameObject;
                    }
                    catch { section.Grid = null; }
                    if (section.Grid != null)
                    {
                        sections.Add(section);
                        if (!Expanded.Contains(cat)) section.Grid.SetActive(false);
                    }
                    else section.Button?.SetVisible(false);   // 找不到格子：不提供折叠，照旧全部显示
                }
                b.End();

                b.BeginRow(8f, TextAnchor.MiddleCenter);
                b.AddButton(zh ? "全部取消（改回自动判断）" : "Clear all (decide automatically)", () =>
                {
                    BoxLabels.Set(box, tabIndex, null);
                    Open(box, tabIndex);
                });
                b.AddButton(zh ? "完成" : "Done", () =>
                {
                    try { mgr.CloseWindow(WindowId); } catch { }
                });
                b.End();
                if (oldPos.HasValue) b.SetPosition(oldPos.Value);
                else b.Center();
                b.Show();
            }
            catch (Exception e)
            {
                Log.Error("打开分类面板失败: " + Log.Describe(e));
                SortBehaviour.Notify(Lang.T("打开分类面板失败，详见日志", "Failed to open the category panel; see the log"));
            }
        }
    }
}
