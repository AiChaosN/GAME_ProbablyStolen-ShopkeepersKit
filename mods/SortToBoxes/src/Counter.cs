using System;
using System.Collections.Generic;
using System.Linq;

namespace SortToBoxes
{
    /// <summary>
    /// 卖货助手：把库存（仓库 + 箱子）里的货摆上柜台（你这一侧 = EmporiumEntry.backInvinvElement），或把柜台清回库存。
    /// 柜台上的货是一件一件议价的（最后放上去的那件先谈，卖掉后自动轮到下一件），所以一次摆多件只是省掉拖动。
    /// 摆上柜台走游戏自己的放置函数，会照常触发“放上柜台”的游戏逻辑（成为当前议价物品、自动打开议价界面等）。
    /// </summary>
    internal static class Counter
    {
        internal static StoreClient CurrentClient()
        {
            try { return PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint(); }
            catch { return null; }
        }

        /// <summary>柜台前有没有想买东西的顾客（收购、批发、买卖兼有）。</summary>
        internal static bool HasBuyer(out StoreClient client)
        {
            client = CurrentClient();
            try { return client != null && client.IsClientBuying(); }
            catch { return false; }
        }

        private static bool Wants(StoreClient client, GameItem item)
        {
            try { return client.IsClientBuyingThisItem(item); }
            catch { return false; }
        }

        private static bool Affordable(StoreClient client, GameItem item)
        {
            try { return client.HasBudgetLeftToBuy(item); }
            catch { return true; }
        }

        private static long Value(GameItem item)
        {
            try { return item.GetNegociatedValue(); }
            catch { return 0; }
        }

        /// <summary>
        /// 把当前买家会买的货全部摆上柜台。买得起的排前面，同档里贵的先放（柜台放不下时，先保证这些）。
        /// 不摆：玩家装备类（游戏标为 IMPORTANT 的：鉴定指南、扫描仪、背包、机器…）、钉住的、展示柜里的。
        /// </summary>
        internal static SortReport PutBuyable(StoreClient client)
        {
            var report = new SortReport();
            var counter = Stock.Counter;
            if (counter == null || client == null) return report;
            var goods = Stock.AllGoods()
                .Where(it => !it.IsTag("IMPORTANT_TAG") && Wants(client, it))
                .Select(it => new { it, ok = Affordable(client, it), v = Value(it) })
                .OrderByDescending(p => p.ok)
                .ThenByDescending(p => p.v)
                .Select(p => p.it)
                .ToList();
            PutOnCounter(goods, counter, report);
            return report;
        }

        /// <summary>把与 sample 一模一样（同一物品 id）的货全部摆上柜台（sample 本身除外）。</summary>
        internal static SortReport PutSame(string identifier, GameItem exclude)
        {
            var report = new SortReport();
            var counter = Stock.Counter;
            if (counter == null || string.IsNullOrEmpty(identifier)) return report;
            var goods = Stock.AllGoods()
                .Where(it => !ReferenceEquals(it, exclude) && it.identifier == identifier)
                .ToList();
            PutOnCounter(goods, counter, report);
            return report;
        }

        private static void PutOnCounter(List<GameItem> goods, GameGridInventory counter, SortReport report)
        {
            using (Mover.Begin())
            {
                foreach (var x in goods)
                {
                    if (!Stock.IsMovable(x)) continue;
                    report.Considered++;
                    int start = x.unitCount;
                    int left = Mover.Move(x, counter, start, "柜台");
                    if (left >= start)
                    {
                        report.NoRoom++;
                        report.Details.Add(Stock.Name(x) + "：柜台放不下");
                    }
                    else if (left > 0)
                    {
                        report.Partial++;
                        report.Count("柜台");
                        report.Details.Add(Stock.Name(x) + "：摆上 " + (start - left) + "/" + start);
                    }
                    else
                    {
                        report.Moved++;
                        report.Count("柜台");
                        report.Details.Add(Stock.Name(x) + (start > 1 ? " ×" + start : "") + " → 柜台");
                    }
                }
            }
        }

        /// <summary>柜台上你的货：能归箱的归箱，其余放回仓库。顾客的东西不动。</summary>
        internal static SortReport ClearCounter(GameInventory counterSide)
        {
            var boxes = Sorter.CollectBoxes();
            var items = counterSide.childItems.ToList();
            return Sorter.SortItems(items, boxes, Stock.Warehouse, allowNegotiated: true);
        }
    }
}
