using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 箱子分类面板：用游戏自带的 CustomUI 框架（与游戏的“储物协调器”同一层、同一风格）建一个可拖动的窗口。
    /// 只列出本存档里见过的物品（SeenItems），按类型（第一个标签）分组：
    ///   - 每组开头一个“整类”勾选框：这一类的货都进这个箱子，包括以后才见到的同类；
    ///   - 组里每件见过的物品一个勾选框（带图标）：只收这一种。
    /// 勾选立即保存到箱子的 tag（随存档保存）。
    /// </summary>
    internal static class BoxLabelPanel
    {
        private const string WindowId = "chaos5090_stb_box_labels";
        private const string NoType = "";

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

                // 箱里现有物品的主类型
                var inBox = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var it in GraphUtils.FindChildrenType<GameItem>(box))
                {
                    if (it == null || Stock.IsFixture(it)) continue;
                    var p = Stock.PrimaryType(it) ?? NoType;
                    inBox.TryGetValue(p, out int n);
                    inBox[p] = n + 1;
                }

                // 见过的物品按类型分组；已勾选但还没见过实物的类型也要列出来，方便取消
                var groups = SeenItems.All
                    .Where(i => i.Type != "MACHINE" && i.Type != "STORAGE")
                    .GroupBy(i => i.Type ?? NoType)
                    .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Name, StringComparer.CurrentCulture).ToList(), StringComparer.Ordinal);
                foreach (var t in selected.Where(t => !t.StartsWith("@") && !groups.ContainsKey(t)).ToList())
                    groups[t] = new List<SeenItems.Info>();
                var order = groups.Keys
                    .OrderBy(k => k == NoType ? 1 : 0)
                    .ThenByDescending(k => groups[k].Count)
                    .ThenBy(k => Stock.TypeName(k), StringComparer.CurrentCulture)
                    .ToList();

                var b = mgr.CreateWindow(WindowId, "箱子分类 · " + Stock.Name(box), LayerId())
                    .SetSize(760f, 640f)
                    .SetDraggable(true)
                    .SetCloseOnEscape();
                b.AddLabel("勾“整类”：这一类的货都进这个箱子，包括以后才见到的同类（类 = 物品提示里排在最前面的 [类型]）。勾单件：只收这一种。一个都不勾 = 按箱子里现有的东西自动判断。");
                b.AddLabel("这里只列出你在这个存档里见过的货，见过的越多，列表越长。名字后面的 ×N 是仓库和箱子里现有的件数。");
                string now = inBox.Count == 0
                    ? "箱子现在是空的"
                    : "箱里现有：" + string.Join("、", inBox.OrderByDescending(kv => kv.Value).Take(6).Select(kv => (kv.Key == NoType ? "无类型" : Stock.TypeName(kv.Key)) + " " + kv.Value));
                b.AddLabel(now);

                b.BeginScroll(420f);
                if (order.Count == 0) b.AddLabel("还没有见过任何货。");
                foreach (var type in order)
                {
                    var items = groups[type];
                    b.AddSeparator();
                    if (type == NoType)
                    {
                        b.AddHeader("没有类型的货（只能逐件勾选）");
                    }
                    else
                    {
                        string t = type;
                        b.AddToggle("【" + Stock.TypeName(t) + "】整类" + (items.Count > 0 ? "（见过 " + items.Count + " 种）" : ""), selected.Contains(t), on =>
                        {
                            if (on) selected.Add(t);
                            else selected.Remove(t);
                            BoxLabels.Set(box, selected);
                        });
                    }
                    if (items.Count == 0) continue;
                    b.BeginGrid(3, 232f, 34f, 4f);
                    foreach (var info in items)
                    {
                        string key = BoxLabels.ItemKey(info.Id);
                        stockCount.TryGetValue(info.Id, out int have);
                        b.BeginRow(4f, TextAnchor.MiddleLeft);
                        var icon = SeenItems.Icon(info);
                        if (icon != null) b.AddImage(icon, 28f, 28f);
                        b.AddToggle((string.IsNullOrEmpty(info.Name) ? info.Id : info.Name) + (have > 0 ? " ×" + have : ""), selected.Contains(key), on =>
                        {
                            if (on) selected.Add(key);
                            else selected.Remove(key);
                            BoxLabels.Set(box, selected);
                        });
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
