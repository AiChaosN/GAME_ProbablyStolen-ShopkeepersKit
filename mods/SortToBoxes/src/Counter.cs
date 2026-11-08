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
            try { return Game.Store?.currentClientInstance?.GetClientBlueprint(); }
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
        /// 把当前买家会买的货全部摆上柜台（贵的先放，柜台放不下时先保证这些）。
        /// 不摆：玩家装备类（游戏标为 IMPORTANT 的：鉴定指南、扫描仪、背包、机器…）、钉住的、展示柜里的、
        /// “不参与上柜台”的箱子里的、你自己调配的混合液（普通瓶子里装了水以外的液体）；
        /// 默认也不摆顾客买不起的（onlyAffordable）。
        /// </summary>
        internal static SortReport PutBuyable(StoreClient client)
        {
            var report = new SortReport();
            var counter = Stock.Counter;
            if (counter == null || client == null) return report;
            bool onlyAffordable = SortToBoxesMod.Config?.OnlyAffordable ?? true;
            var goods = Stock.AllGoods()
                .Where(it => !it.IsTag("IMPORTANT_TAG") && !Stock.InNoSellBox(it) && !Stock.IsOwnMixture(it) && Wants(client, it))
                .Where(it => !onlyAffordable || Affordable(client, it))
                .Select(it => new { it, ok = Affordable(client, it), v = Value(it) })
                .OrderByDescending(p => p.ok)
                .ThenByDescending(p => p.v)
                .Select(p => p.it)
                .ToList();
            PutOnCounter(goods, counter, report);
            return report;
        }

        /// <summary>
        /// 把 sample 的同款全部摆上柜台（exclude 除外）。同款 = Stock.VariantKey 相同：同一种物品，
        /// 且类型标签、看得到的等级（水质、药品纯度……）、瓶子里的“别的液体”都一样。
        /// “不参与上柜台”的箱子里的不拿。
        /// </summary>
        internal static SortReport PutSame(string variantKey, GameItem exclude)
        {
            var report = new SortReport();
            var counter = Stock.Counter;
            if (counter == null || string.IsNullOrEmpty(variantKey)) return report;
            var goods = Stock.AllGoods()
                .Where(it => !ReferenceEquals(it, exclude) && !Stock.InNoSellBox(it) && Stock.VariantKey(it) == variantKey)
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

        /// <summary>
        /// 把柜台上（你这一侧）与 sample 同款的货（含 sample 本身）收回：能归箱的归箱，其余放回仓库。
        /// 和“指着库存里的货摆同款上柜台”是同一个键的反方向。
        /// </summary>
        internal static SortReport TakeSame(GameItem sample)
        {
            var counter = Stock.Counter;
            if (counter == null || sample == null) return new SortReport();
            string key = Stock.VariantKey(sample);
            var items = counter.childItems
                .Where(it => it != null && (ReferenceEquals(it, sample) || Stock.VariantKey(it) == key))
                .ToList();
            return Sorter.SortItems(items, Sorter.CollectBoxes(), Stock.Warehouse, allowNegotiated: true);
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
