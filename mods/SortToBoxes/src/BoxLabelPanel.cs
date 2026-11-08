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
    /// </summary>
    internal static class BoxLabelPanel
    {
        private const string WindowId = "chaos5090_stb_box_labels";

        internal static string LayerId()
        {
            try { return CustomUIManager.HasLayer("paper") ? "paper" : "overlay"; }
            catch { return "overlay"; }
        }

        internal static void Open(GameItem box)
        {
            try
            {
                if (box == null || !Stock.IsBox(box)) return;
                var mgr = CustomUIManager.Instance;
                if (mgr == null)
                {
                    SortBehaviour.Notify("现在打不开分类面板（界面还没准备好）");
                    return;
                }

                Vector2? oldPos = null;
                var old = mgr.GetWindow(WindowId);
                if (old != null && old.IsAlive) oldPos = old.Rect.anchoredPosition;

                SeenItems.ScanNow();   // 先把眼前的东西记上
                var selected = BoxLabels.Get(box) ?? new HashSet<string>(StringComparer.Ordinal);

                // 库存里每种物品有几件（显示在名字后面）
                var stockCount = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var it in Stock.AllGoods())
                {
                    if (string.IsNullOrEmpty(it.identifier)) continue;
                    stockCount.TryGetValue(it.identifier, out int n);
                    stockCount[it.identifier] = n + Math.Max(1, it.unitCount);
                }

                // 箱里现有物品的大类
                var inBox = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var it in GraphUtils.FindChildrenType<GameItem>(box))
                {
                    if (it == null || Stock.IsFixture(it)) continue;
                    var c = Categories.Of(it);
                    inBox.TryGetValue(c, out int n);
                    inBox[c] = n + 1;
                }

                // 见过的物品按大类分组；勾过但还没见过实物的类也列出来，方便取消
                var groups = SeenItems.All
                    .GroupBy(i => string.IsNullOrEmpty(i.Cat) ? Categories.Other : i.Cat)
                    .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Name, StringComparer.CurrentCulture).ToList(), StringComparer.Ordinal);
                foreach (var key in selected.Where(t => t.StartsWith("#")).Select(t => t.Substring(1)))
                    if (!groups.ContainsKey(key)) groups[key] = new List<SeenItems.Info>();
                var order = groups.Keys
                    .OrderBy(k => { int i = Array.IndexOf(Categories.Order, k); return i < 0 ? 999 : i; })
                    .ToList();

                var b = mgr.CreateWindow(WindowId, "箱子分类 · " + Stock.Name(box), LayerId())
                    .SetSize(760f, 640f)
                    .SetDraggable(true)
                    .SetCloseOnEscape();
                b.AddLabel("勾“整类”：这一类的货全部进这个箱子，包括以后才见到的同类；也可以只勾其中几件。一个都不勾 = 按箱子里现有的东西自动判断。");
                b.AddLabel("只列出你在这个存档里见过的货，见过的越多，列表越长。名字后面的 ×N 是仓库和箱子里现有的件数。");
                string now = inBox.Count == 0
                    ? "箱子现在是空的"
                    : "箱里现有：" + string.Join("、", inBox.OrderByDescending(kv => kv.Value).Take(6).Select(kv => Categories.Name(kv.Key) + " " + kv.Value));
                b.AddLabel(now);

                b.BeginScroll(420f);
                if (order.Count == 0) b.AddLabel("还没有见过任何货。");
                foreach (var cat in order)
                {
                    var items = groups[cat];
                    string catKey = BoxLabels.CatKey(cat);
                    bool catOn = selected.Contains(catKey);
                    var itemToggles = new Dictionary<string, CustomUIElement>(StringComparer.Ordinal);
                    CustomUIElement catToggle = null;

                    b.AddSeparator();
                    b.AddToggle("【" + Categories.Name(cat) + "】整类" + (items.Count > 0 ? "（见过 " + items.Count + " 种）" : ""), catOn, on =>
                    {
                        selected.Remove(catKey);
                        foreach (var info in items) selected.Remove(BoxLabels.ItemKey(info.Id));   // 整类勾着时不需要逐件记
                        if (on) selected.Add(catKey);
                        foreach (var t in itemToggles.Values) t?.SetBool(on, notify: false);
                        BoxLabels.Set(box, selected);
                    });
                    catToggle = b.Last;
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
                        b.AddToggle((string.IsNullOrEmpty(info.Name) ? id : info.Name) + (have > 0 ? " ×" + have : ""), catOn || selected.Contains(key), on =>
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
                            BoxLabels.Set(box, selected);
                        });
                        itemToggles[id] = b.Last;
                        b.End();
                    }
                    b.End();
                }
                b.End();

                b.BeginRow(8f, TextAnchor.MiddleCenter);
                b.AddButton("全部取消（改回自动判断）", () =>
                {
                    BoxLabels.Set(box, null);
                    Open(box);
                });
                b.AddButton("完成", () =>
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
                SortBehaviour.Notify("打开分类面板失败，详见日志");
            }
        }
    }
}
