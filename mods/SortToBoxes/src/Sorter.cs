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
    /// 归箱规则（按货所属的大类 Categories）。归箱的单位是“栏”：没升级的箱子就一栏，用扩容套件升级过的储藏区每一栏各算一个目标。
    /// 0. 设了“不参与归箱”的栏：从不往里放，里面的东西也不拿来判断；
    /// 1. 勾选过分类的栏：只收勾选范围内的货（最优先）；
    /// 2. 没勾选的栏：有一模一样物品的优先；其次是装着同类物品的栏（这类物品越多越优先）；
    /// 3. 首选的栏放不下就试下一个；
    /// 4. 都放满了，再放进同一个箱子里空着的、没勾分类的栏（升级箱子多出来的空栏当作这个箱子的后备空间）。
    ///    一个空栏在一次归箱里只接一类货，不会把两类货混在一起；
    /// 5. 还是放不下就留在原地（清空柜台时放回仓库）。
    /// 栏里“装什么”在本次搬运开始前统计一次，本次放进去的东西不改变本次的判断。
    /// 放置调用游戏自己的 GraphUtils.TryAcceptAll，直接对那一栏的格子库存（自动找空位、能叠就叠），箱子原有的放入限制照常生效。
    /// 注意不能对箱子物品本身调用：游戏的 PixelWindow 只会往它的第一个格子库存里放，升级箱子的第 2 栏起就永远用不上。
    /// </summary>
    internal static class Sorter
    {
        /// <summary>一个归箱目标 = 某个箱子的某一栏。</summary>
        internal sealed class Box
        {
            internal GameItem Item;            // 箱子
            internal GameInventory Inv;        // 这一栏的格子库存
            internal int Index;                // 第几栏（从 0 起）
            internal string Name;
            internal int Order;
            internal bool NoSort;              // 不参与归箱
            internal HashSet<string> Labels;   // 勾选的分类；null = 按内容判断
            internal readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly Dictionary<string, int> Cats = new Dictionary<string, int>(StringComparer.Ordinal);   // 栏里每个大类有几件
            internal int Total;
            internal string SpareFor;          // 本次归箱里，这个空栏已经当作哪一类货的后备空间

            /// <summary>能不能当作同箱其它栏放满后的后备空间：空的、没勾分类、参与归箱，且本次还没接别的类。</summary>
            internal bool IsSpareFor(string cat) =>
                !NoSort && Labels == null && Total == 0 && (SpareFor == null || SpareFor == cat) && Item.parentInventory != null;
        }

        /// <summary>收集所有箱子的所有栏（含“不参与归箱”的栏，标记 NoSort，排序时跳过）。</summary>
        internal static List<Box> CollectBoxes()
        {
            var result = new List<Box>();
            int order = 0;
            foreach (var item in Stock.FindBoxes())
            {
                var comps = Stock.Compartments(item);
                for (int i = 0; i < comps.Count; i++)
                {
                    var inv = comps[i];
                    if (inv == null) continue;
                    var box = new Box
                    {
                        Item = item, Inv = inv, Index = i, Order = order++,
                        Name = Stock.TargetName(item, i, comps.Count),
                        NoSort = Stock.NoSort.IsOn(item, i),
                        Labels = BoxLabels.Get(item, i),
                    };
                    Count(box);
                    result.Add(box);
                }
            }
            return result;
        }

        /// <summary>统计这一栏现在装着什么（箱中箱、机器之类的设施不算）。</summary>
        private static void Count(Box box)
        {
            foreach (var content in Stock.Children(box.Inv))
            {
                if (content == null || Stock.IsFixture(content)) continue;
                box.Total++;
                if (!string.IsNullOrEmpty(content.identifier))
                {
                    box.Ids.TryGetValue(content.identifier, out int n);
                    box.Ids[content.identifier] = n + 1;
                }
                var cat = Categories.Of(content);
                box.Cats.TryGetValue(cat, out int c);
                box.Cats[cat] = c + 1;
            }
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
                    string cat = Categories.Of(x);
                    var ranked = Rank(x, boxes);
                    int start = x.unitCount;
                    int remaining = start;
                    var placedIn = new List<string>();
                    foreach (var b in ranked)
                    {
                        if (remaining <= 0) break;
                        int left = Mover.Move(x, b.Inv, remaining, b.Name, b.Item);
                        if (left < remaining)
                        {
                            placedIn.Add(b.Name);
                            remaining = left;
                            if (b.IsSpareFor(cat)) b.SpareFor = cat;   // 空栏接了这一类，本次就不再接别的类
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
                            report.Details.Add(itemName + "：没有对应的箱子（类别：" + Categories.Name(Categories.Of(x)) + "）");
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

        /// <summary>给一件货排出可去的栏，最合适的在前；最后附上这些栏所在箱子里的空栏（后备空间）。</summary>
        internal static List<Box> Rank(GameItem x, List<Box> boxes)
        {
            string id = x.identifier ?? "";
            string cat = Categories.Of(x);
            var scored = new List<KeyValuePair<double, Box>>();
            foreach (var b in boxes)
            {
                if (b.NoSort || b.Inv == null || ReferenceEquals(b.Item, x) || b.Item.parentInventory == null) continue;
                b.Ids.TryGetValue(id, out int same);
                double score;
                if (b.Labels != null)
                {
                    // 勾选过分类：勾了这件物品本身 > 勾了它所属的大类；都没勾就不收
                    if (b.Labels.Contains(BoxLabels.ItemKey(id))) score = 3500 + same;
                    else if (b.Labels.Contains(BoxLabels.CatKey(cat))) score = 3000 + same;
                    else continue;
                }
                else if (same > 0)
                {
                    score = 2000 + same;
                }
                else if (b.Cats.TryGetValue(cat, out int c) && c > 0)
                {
                    score = 1000 + c;
                }
                else continue;
                scored.Add(new KeyValuePair<double, Box>(score, b));
            }
            var ranked = scored
                .OrderByDescending(p => p.Key)
                .ThenBy(p => p.Value.Order)
                .Select(p => p.Value)
                .ToList();

            // 后备空间：匹配上的栏所在的箱子里，空着的、没勾分类的其它栏（按栏的顺序）
            var spares = new List<Box>();
            foreach (var r in ranked)
                foreach (var b in boxes)
                    if (ReferenceEquals(b.Item, r.Item) && b.Inv != null && b.IsSpareFor(cat) && !ranked.Contains(b) && !spares.Contains(b))
                        spares.Add(b);
            ranked.AddRange(spares);
            return ranked;
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

        /// <summary>把 x 的 amount 个移进 target（格子库存），返回没移走的数量。owner = target 所属的箱子（检查玩家能不能往里放）。</summary>
        internal static int Move(GameItem x, GraphNodeStorage target, int amount, string targetName, GameItem owner = null)
        {
            if (x == null || target == null || amount <= 0) return amount;
            try
            {
                if (owner != null && !GeneralHelper.MayPlayerInsertInto(owner)) return amount;
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
