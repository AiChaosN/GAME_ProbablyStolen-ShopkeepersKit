using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 箱子分类面板：用游戏自带的 CustomUI 框架（与游戏的“储物协调器”同一层、同一风格）建一个可拖动的窗口，
    /// 每个商品类型一个勾选框。勾选立即保存到箱子的 tag（随存档保存）。
    /// </summary>
    internal static class BoxLabelPanel
    {
        private const string WindowId = "chaos5090_stb_box_labels";

        // 作为“第一个标签”出现过的类型，按常见程度排列（机器、储藏类不归箱，不列出）
        private static readonly string[] CommonTypes =
        {
            "FOOD", "HOUSEHOLD_GOOD", "LUXURY_ITEM", "ART", "MEDICAL", "WEAPON", "MELEE_WEAPON", "AMMUNITION",
            "GUN_MOD", "TOOL", "MATERIAL", "DOCUMENT", "MODULE", "HYDROPONIC_MODULE", "PRINTER_MODULE",
            "ACCESS_CARD", "CHEMICAL_SUPPLIES", "SUBSTANCE", "ALCOHOL", "NARCOTIC", "POISON", "TREAT",
            "PROCESSED_FOOD", "WATER_RATION", "WATER_PURIFICATION_SUPPLY", "MODIFIED_SEED", "SEED",
            "FARMING_SUPPLIES", "ENERGY_CELL", "SUPPLY_CRATE", "CONTRABAND", "LOTTERY", "JUNK", "NODE",
            "EVAPORATOR_MODULE", "ALARM_MODULE",
        };

        internal static string LayerId()
        {
            try { return CustomUIManager.HasLayer("paper") ? "paper" : "overlay"; }
            catch { return "overlay"; }
        }

        internal static bool IsOpen()
        {
            try { return CustomUIManager.Instance != null && CustomUIManager.Instance.IsOpen(WindowId); }
            catch { return false; }
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

                var selected = BoxLabels.Get(box) ?? new HashSet<string>(StringComparer.Ordinal);

                // 箱里现有物品的主类型，以及库存里各主类型的货有多少（帮你判断该勾哪些）
                var inBox = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var it in GraphUtils.FindChildrenType<GameItem>(box))
                {
                    if (it == null || Stock.IsFixture(it)) continue;
                    var p = Stock.PrimaryType(it) ?? "";
                    inBox.TryGetValue(p, out int n);
                    inBox[p] = n + 1;
                }
                var inStock = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var it in Stock.AllGoods())
                {
                    var p = Stock.PrimaryType(it);
                    if (p == null) continue;
                    inStock.TryGetValue(p, out int n);
                    inStock[p] = n + 1;
                }

                var types = CommonTypes.ToList();
                foreach (var t in inStock.Keys.Concat(inBox.Keys).Concat(selected))
                    if (!string.IsNullOrEmpty(t) && !types.Contains(t) && t != "MACHINE" && t != "STORAGE") types.Add(t);

                var b = mgr.CreateWindow(WindowId, "箱子分类 · " + Stock.Name(box), LayerId())
                    .SetSize(640f, 600f)
                    .SetDraggable(true)
                    .SetCloseOnEscape();
                b.AddLabel("勾选后，这个箱子只收“第一个标签”属于勾选类型的货（第一个标签 = 物品提示里排在最前的 [类型]）。一个都不勾 = 按箱子里现有的东西自动判断。");
                string now = inBox.Count == 0
                    ? "箱子现在是空的"
                    : "箱里现有：" + string.Join("、", inBox.OrderByDescending(kv => kv.Value).Take(6).Select(kv => (kv.Key == "" ? "无类型" : Stock.TypeName(kv.Key)) + " " + kv.Value));
                b.AddLabel(now);
                b.AddLabel("括号里是仓库和箱子里这类货的件数。");
                b.BeginScroll(380f);
                b.BeginGrid(3, 196f, 34f, 4f);
                foreach (var t in types)
                {
                    string type = t;
                    inStock.TryGetValue(type, out int count);
                    string label = Stock.TypeName(type) + (count > 0 ? " (" + count + ")" : "");
                    b.AddToggle(label, selected.Contains(type), on =>
                    {
                        if (on) selected.Add(type);
                        else selected.Remove(type);
                        BoxLabels.Set(box, selected);
                    });
                }
                b.End();
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
