using System;
using System.Collections.Generic;
using System.Linq;

namespace SortToBoxes
{
    /// <summary>一次搬运的结果（按物品件数计，一叠算一件）。</summary>
    internal sealed class SortReport
    {
        internal int Considered;   // 参与的件数
        internal int Moved;        // 整件放进了箱子 / 柜台
        internal int Partial;      // 一叠里只放进去一部分
        internal int ToWarehouse;  // 没有对应箱子，放回了仓库（只在清空柜台时发生）
        internal int NoMatch;      // 没有对应的箱子，留在原地
        internal int NoRoom;       // 有地方可去但放不下
        internal readonly Dictionary<string, int> PerTarget = new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly List<string> Details = new List<string>();

        internal int Placed => Moved + Partial;

        internal void Count(string name)
        {
            PerTarget.TryGetValue(name, out int n);
            PerTarget[name] = n + 1;
        }
    }

    /// <summary>
    /// 归箱规则（以物品提示里第一个 [标签] 为准）：
    /// 1. 勾选过分类的箱子：只收主类型在勾选范围内的货（最优先）；
    /// 2. 没勾选的箱子：有一模一样物品的优先；其次是装着同主类型物品的箱子（这类物品越多越优先）；
    /// 3. 首选箱子放不下就试下一个，都放不下就留在原地（清空柜台时放回仓库）。
    /// 箱子“装什么”在本次搬运开始前统计一次，本次放进去的东西不改变本次的判断。
    /// 放置调用游戏自己的 GraphUtils.TryAcceptAll（自动找空位、能叠就叠），箱子原有的放入限制照常生效。
    /// </summary>
    internal static class Sorter
    {
        internal sealed class Box
        {
            internal GameItem Item;
            internal string Name;
            internal int Order;
            internal HashSet<string> Labels;   // 勾选的分类；null = 按内容判断
            internal readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly Dictionary<string, int> Primary = new Dictionary<string, int>(StringComparer.Ordinal);
            internal int Total;
        }

        internal static List<Box> CollectBoxes()
        {
            var result = new List<Box>();
            int order = 0;
            foreach (var item in Stock.FindBoxes())
            {
                var box = new Box { Item = item, Order = order++, Name = Stock.Name(item), Labels = BoxLabels.Get(item) };
                foreach (var content in GraphUtils.FindChildrenType<GameItem>(item))
                {
                    if (content == null || Stock.IsFixture(content)) continue;
                    box.Total++;
                    if (!string.IsNullOrEmpty(content.identifier))
                    {
                        box.Ids.TryGetValue(content.identifier, out int n);
                        box.Ids[content.identifier] = n + 1;
                    }
                    var p = Stock.PrimaryType(content);
                    if (p != null)
                    {
                        box.Primary.TryGetValue(p, out int c);
                        box.Primary[p] = c + 1;
                    }
                }
                result.Add(box);
            }
            return result;
        }

        /// <summary>
        /// 把 items 逐件归进 boxes。items 应是调用前的快照。
        /// fallback 不为空时，没有对应箱子或放不下的货放进 fallback（清空柜台时 = 仓库）。
        /// </summary>
        internal static SortReport SortItems(IEnumerable<GameItem> items, List<Box> boxes, GameInventory fallback = null, bool allowNegotiated = false)
        {
            var report = new SortReport();
            using (Mover.Begin())
            {
                foreach (var x in items)
                {
                    if (!Stock.IsMovable(x, allowNegotiated)) continue;
                    report.Considered++;
                    string itemName = Stock.Name(x);
                    var ranked = Rank(x, boxes);
                    int start = x.unitCount;
                    int remaining = start;
                    var placedIn = new List<string>();
                    foreach (var b in ranked)
                    {
                        if (remaining <= 0) break;
                        int left = Mover.Move(x, b.Item, remaining, b.Name);
                        if (left < remaining)
                        {
                            placedIn.Add(b.Name);
                            remaining = left;
                        }
                    }

                    bool toWarehouse = false;
                    if (remaining > 0 && fallback != null && !ReferenceEquals(x.parentInventory, fallback))
                    {
                        int left = Mover.Move(x, fallback, remaining, "仓库");
                        if (left < remaining)
                        {
                            toWarehouse = true;
                            remaining = left;
                        }
                    }

                    foreach (var name in placedIn.Distinct()) report.Count(name);
                    if (toWarehouse)
                    {
                        report.ToWarehouse++;
                        report.Details.Add(itemName + " → 仓库" + (ranked.Count == 0 ? "（没有对应箱子）" : placedIn.Count == 0 ? "（箱子放不下）" : "（箱子只放下一部分）") +
                                           (remaining > 0 ? "，还有 " + remaining + " 个哪都放不下" : ""));
                        continue;
                    }
                    if (placedIn.Count == 0)
                    {
                        if (ranked.Count == 0)
                        {
                            report.NoMatch++;
                            report.Details.Add(itemName + "：没有对应的箱子（主类型 " + Stock.TypeName(Stock.PrimaryType(x)) + "）");
                        }
                        else
                        {
                            report.NoRoom++;
                            report.Details.Add(itemName + "：对应的箱子（" + string.Join("、", ranked.Select(c => c.Name).Distinct()) + "）放不下");
                        }
                        continue;
                    }
                    if (remaining > 0)
                    {
                        report.Partial++;
                        report.Details.Add(itemName + "：放进 " + string.Join("、", placedIn.Distinct()) + " " + (start - remaining) + "/" + start + "，其余放不下");
                    }
                    else
                    {
                        report.Moved++;
                        report.Details.Add(itemName + (start > 1 ? " ×" + start : "") + " → " + string.Join("、", placedIn.Distinct()));
                    }
                }
            }
            return report;
        }

        /// <summary>给一件货排出可去的箱子，最合适的在前。</summary>
        internal static List<Box> Rank(GameItem x, List<Box> boxes)
        {
            string id = x.identifier ?? "";
            string primary = Stock.PrimaryType(x);
            var scored = new List<KeyValuePair<double, Box>>();
            foreach (var b in boxes)
            {
                if (ReferenceEquals(b.Item, x) || b.Item.parentInventory == null) continue;
                b.Ids.TryGetValue(id, out int same);
                double score;
                if (b.Labels != null)
                {
                    // 勾选过分类：只看勾选的类型
                    if (primary == null || !b.Labels.Contains(primary)) continue;
                    score = 3000 + same;
                }
                else if (same > 0)
                {
                    score = 2000 + same;
                }
                else if (primary != null && b.Primary.TryGetValue(primary, out int c) && c > 0)
                {
                    score = 1000 + c;
                }
                else continue;
                scored.Add(new KeyValuePair<double, Box>(score, b));
            }
            return scored
                .OrderByDescending(p => p.Key)
                .ThenBy(p => p.Value.Order)
                .Select(p => p.Value)
                .ToList();
        }
    }

    /// <summary>
    /// 所有搬运都经过这里：统一异常处理，并在搬运期间打开 Active 标记，
    /// 让“放一件上柜台带上同款”的补丁忽略本 mod 自己的搬运。
    /// </summary>
    internal static class Mover
    {
        private static int depth;
        internal static bool Active => depth > 0;

        internal sealed class Scope : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                depth--;
            }
        }

        internal static Scope Begin()
        {
            depth++;
            return new Scope();
        }

        /// <summary>把 x 的 amount 个移进 target（箱子物品或格子），返回没移走的数量。</summary>
        internal static int Move(GameItem x, GraphNodeStorage target, int amount, string targetName)
        {
            if (x == null || target == null || amount <= 0) return amount;
            try
            {
                if (target is GameItem boxItem && !GeneralHelper.MayPlayerInsertInto(boxItem)) return amount;
                using (Begin())
                {
                    return GraphUtils.TryAcceptAll(target, x, amount);
                }
            }
            catch (Exception e)
            {
                Log.Warn("放入 " + targetName + " 出错（" + Stock.Name(x) + "）: " + e.Message);
                return amount;
            }
        }
    }
}
