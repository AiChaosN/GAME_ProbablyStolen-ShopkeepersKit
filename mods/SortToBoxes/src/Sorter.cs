using System;
using System.Collections.Generic;
using System.Linq;

namespace SortToBoxes
{
    /// <summary>一次归箱的结果（按物品件数计，一叠算一件）。</summary>
    internal sealed class SortReport
    {
        internal int Considered;   // 参与归箱的散件
        internal int Moved;        // 整件放进了箱子
        internal int Partial;      // 一叠里只放进去一部分
        internal int NoMatch;      // 没有对应的箱子
        internal int NoRoom;       // 有对应的箱子，但都放不下
        internal readonly Dictionary<string, int> PerBox = new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly List<string> Details = new List<string>();

        internal void CountBox(string name)
        {
            PerBox.TryGetValue(name, out int n);
            PerBox[name] = n + 1;
        }
    }

    /// <summary>
    /// 核心规则：
    /// 1. 箱子 = 仓库里（含箱中箱）属于你的“储藏”类物品（储藏区、简易储藏区、工具箱、机器区…）和背包。
    ///    垃圾桶、档案夹、种子包、上锁的物资箱、机器都不算。
    /// 2. 箱子“装哪类货”由它里面现有的东西决定（统计在归箱开始前做一次，本次放进去的东西不改变判断）。
    /// 3. 每件散件挑箱子：有一模一样物品的箱子优先；否则按商品类型重合度排序，
    ///    且至少一种类型在箱内占比 ≥ 最低占比。放不下就试下一个，全都放不下就留在原地。
    /// 4. 放置调用游戏自己的 GraphUtils.TryAcceptAll（自动找空位、可叠加就叠加），箱子原有的放入限制照常生效。
    /// </summary>
    internal static class Sorter
    {
        // 只表示档次/改装性/容器本身的类型，不代表“这是哪一类货”，不参与匹配
        private static readonly HashSet<string> IgnoredTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "MODDABLE", "MAKESHIFT", "MIDLINE", "LOWTECH", "STORAGE", "NODE"
        };

        internal sealed class Box
        {
            internal GameItem Item;
            internal string Name;
            internal int Order;
            internal int Total;
            internal readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly Dictionary<string, int> Types = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private struct Candidate
        {
            internal Box Box;
            internal double Score;
        }

        internal static bool IsBox(GameItem it)
        {
            if (it == null || it.contentWindow == null) return false;
            if (!GeneralHelper.IsItemOwned(it)) return false;
            if (it.IsGameItemType("MACHINE") || it.IsGameItemType("SUPPLY_CRATE") || it.IsTag("LOCKED_CONTAINER_TAG")) return false;
            return it.IsGameItemType("STORAGE") || it.IsTag("BACKPACK_TAG");
        }

        /// <summary>这件东西能不能被搬去箱子。不能搬的（固定设施、钉住的、别人的…）不计入报告。</summary>
        internal static bool IsSortable(GameItem x)
        {
            if (x == null || x.parentInventory == null) return false;
            if (x is GameItemElement el && el.IsDestroyed()) return false;
            if (!GeneralHelper.IsItemOwned(x)) return false;
            if (ItemPinHelper.IsPinned(x) || DebrisHelper.IsDebris(x)) return false;
            if (IsBox(x)) return false;
            // 容器类、机器、飞船系统（在仓库里工作的设施）不搬
            if (x.IsTag("CONTAINER_TAG") || x.IsTag("BACKPACK_TAG") || x.IsTag("SYSTEM_TAG")) return false;
            if (x.IsGameItemType("MACHINE")) return false;
            try
            {
                if (ReferenceEquals(PlayerStore.Instance?.GetCurrentlyNegociatedItem(), x)) return false;
            }
            catch { }
            return x.MayRemove();
        }

        /// <summary>收集仓库里的所有箱子（含放在箱子里的箱子）并统计各自装了什么。</summary>
        internal static List<Box> CollectBoxes()
        {
            var result = new List<Box>();
            var inv = EmporiumEntry.Instance?.invElement;
            if (inv == null) return result;

            Func<GameItem, bool> isBox = SafeIsBox;
            var found = GraphUtils.FindChildrenType<GameItem>(inv, isBox, isBox);
            // FindChildrenType 用 HashSet，顺序不固定；按唯一 id 排序，让同分时的选择稳定
            found.Sort((a, b) => SafeUid(a).CompareTo(SafeUid(b)));
            int order = 0;
            foreach (var item in found)
            {
                var box = new Box { Item = item, Order = order++, Name = SafeName(item) };
                foreach (var content in GraphUtils.FindChildrenType<GameItem>(item))
                {
                    // 机器、容器、设施本身从不归箱，也不拿来判断箱子装哪类货
                    // （否则机器区会因为机器带有“工具”等类型而吸走普通工具）
                    if (content == null || !CountsAsContent(content)) continue;
                    box.Total++;
                    if (!string.IsNullOrEmpty(content.identifier))
                    {
                        box.Ids.TryGetValue(content.identifier, out int n);
                        box.Ids[content.identifier] = n + 1;
                    }
                    foreach (var t in MatchTypes(content))
                    {
                        box.Types.TryGetValue(t, out int c);
                        box.Types[t] = c + 1;
                    }
                }
                result.Add(box);
            }
            return result;
        }

        /// <summary>把 items 逐件归进 boxes。items 应是调用前的快照（本函数会改变它们所在的库存）。</summary>
        internal static SortReport SortItems(IEnumerable<GameItem> items, List<Box> boxes, int minMatchPercent)
        {
            var report = new SortReport();
            double minShare = Math.Max(0, Math.Min(100, minMatchPercent)) / 100.0;
            foreach (var x in items)
            {
                bool sortable;
                try { sortable = IsSortable(x); }
                catch { sortable = false; }
                if (!sortable) continue;

                report.Considered++;
                string itemName = SafeName(x);
                var ranked = Rank(x, boxes, minShare);
                if (ranked.Count == 0)
                {
                    report.NoMatch++;
                    report.Details.Add(itemName + "：没有对应的箱子");
                    continue;
                }

                int start = x.unitCount;
                int remaining = start;
                var placedIn = new List<string>();
                foreach (var cand in ranked)
                {
                    if (remaining <= 0) break;
                    int left;
                    try
                    {
                        if (!GeneralHelper.MayPlayerInsertInto(cand.Box.Item)) continue;
                        left = GraphUtils.TryAcceptAll(cand.Box.Item, x, remaining);
                    }
                    catch (Exception e)
                    {
                        Log.Warn("放入 " + cand.Box.Name + " 出错（" + itemName + "）: " + e.Message);
                        continue;
                    }
                    if (left < remaining)
                    {
                        placedIn.Add(cand.Box.Name);
                        remaining = left;
                    }
                }

                if (placedIn.Count == 0)
                {
                    report.NoRoom++;
                    report.Details.Add(itemName + "：对应的箱子（" + string.Join("、", ranked.Select(c => c.Box.Name).Distinct()) + "）放不下");
                    continue;
                }
                foreach (var name in placedIn.Distinct()) report.CountBox(name);
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
            return report;
        }

        private static List<Candidate> Rank(GameItem x, List<Box> boxes, double minShare)
        {
            var list = new List<Candidate>();
            string id = x.identifier ?? "";
            var types = MatchTypes(x);
            foreach (var b in boxes)
            {
                if (b.Total == 0 || ReferenceEquals(b.Item, x)) continue;
                if (ReferenceEquals(b.Item.parentInventory, null)) continue;
                b.Ids.TryGetValue(id, out int same);
                double sum = 0, best = 0;
                foreach (var t in types)
                {
                    if (!b.Types.TryGetValue(t, out int c)) continue;
                    double share = (double)c / b.Total;
                    sum += share;
                    if (share > best) best = share;
                }
                double score;
                if (same > 0) score = 1000 + 100.0 * same / b.Total + sum; // 有同款：压过所有只按类型匹配的箱子
                else if (sum > 0 && best >= minShare) score = sum;
                else continue;
                list.Add(new Candidate { Box = b, Score = score });
            }
            list.Sort((p, q) =>
            {
                int c = q.Score.CompareTo(p.Score);
                if (c != 0) return c;
                c = q.Box.Total.CompareTo(p.Box.Total);
                return c != 0 ? c : p.Box.Order.CompareTo(q.Box.Order);
            });
            return list;
        }

        private static List<string> MatchTypes(GameItem item)
        {
            var result = new List<string>();
            var types = item?.itemTypes;
            if (types == null) return result;
            foreach (var t in types)
            {
                if (string.IsNullOrEmpty(t) || IgnoredTypes.Contains(t) || result.Contains(t)) continue;
                result.Add(t);
            }
            return result;
        }

        private static bool CountsAsContent(GameItem it)
        {
            try
            {
                if (IsBox(it)) return false;
                if (it.IsTag("CONTAINER_TAG") || it.IsTag("BACKPACK_TAG") || it.IsTag("SYSTEM_TAG")) return false;
                return !it.IsGameItemType("MACHINE");
            }
            catch { return false; }
        }

        private static bool SafeIsBox(GameItem it)
        {
            try { return IsBox(it); }
            catch { return false; }
        }

        private static int SafeUid(GameItem it)
        {
            try { return it.GetUniqueID(); }
            catch { return 0; }
        }

        internal static string SafeName(GameItem it)
        {
            try
            {
                var n = it.GetDisplayName(true);
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            return it?.identifier ?? "?";
        }
    }
}
