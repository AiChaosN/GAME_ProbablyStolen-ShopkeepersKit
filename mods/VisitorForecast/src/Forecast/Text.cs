using System;
using System.Collections.Generic;
using System.Linq;

namespace VisitorForecast.Forecast
{
    /// <summary>中文显示文本。只在沙盒结束、安全网比对完成之后调用（本地化查询会写游戏的缓存）。</summary>
    internal static class Text
    {
        internal static readonly string[] Weekdays = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };

        private static readonly Dictionary<string, string> factionFallback = new Dictionary<string, string>
        {
            { "FACTION_LOWER_LEVEL", "下层区" },
            { "FACTION_UPPER_LEVEL", "上层区" },
            { "FACTION_SECURITY", "治安部" },
            { "FACTION_BLACK_MARKET", "黑市" },
            { "FACTION_REVOLUTION", "革命军" },
            { "FACTION_CARTEL", "13区卡特尔" },
            { "FACTION_TOURIST", "游客" },
            { "FACTION_CHURCH", "教会" },
            { "FACTION_MIDDLE", "中层" },
            { "NONE", "—" },
        };

        internal static string Faction(string id)
        {
            if (string.IsNullOrEmpty(id)) return "—";
            try
            {
                var n = StoreReputation.GetFactionNameByID(id);
                if (!string.IsNullOrEmpty(n) && n != "NONE") return n;
            }
            catch { }
            return factionFallback.TryGetValue(id, out var f) ? f : id;
        }

        internal static string Intent(StoreClient.ClientIntent i)
        {
            switch (i)
            {
                case StoreClient.ClientIntent.BUY: return "买";
                case StoreClient.ClientIntent.SELL: return "卖";
                case StoreClient.ClientIntent.SELLNBUY: return "买卖";
                case StoreClient.ClientIntent.INSPECTION: return "检查";
                case StoreClient.ClientIntent.DIALOGUE: return "对话";
                case StoreClient.ClientIntent.BARTER: return "以物易物";
                case StoreClient.ClientIntent.RENT: return "收租";
                case StoreClient.ClientIntent.LOAN_SHARK: return "放贷/收债";
                case StoreClient.ClientIntent.SPECIAL: return "特殊事务";
                case StoreClient.ClientIntent.INFORMATION_DEALER: return "卖情报";
                case StoreClient.ClientIntent.PROCUREMENT_OFFER: return "委托订货";
                case StoreClient.ClientIntent.PROCUREMENT_COLLECT: return "来取货";
                case StoreClient.ClientIntent.WHOLESALE: return "批发";
                case StoreClient.ClientIntent.APPRAISAL_SERVICE: return "鉴定服务";
                case StoreClient.ClientIntent.GUNSMITH: return "枪械定制";
                case StoreClient.ClientIntent.EXPEDITION: return "远征";
                default: return "未知";
            }
        }

        private static readonly Dictionary<string, string> typeCache = new Dictionary<string, string>();

        internal static string TypeName(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "";
            if (typeCache.TryGetValue(tag, out var s)) return s;
            string name = tag;
            try { name = LocHelper.GetLocalizedMechanicOrDefault("type_" + tag.ToLowerInvariant(), tag); }
            catch { }
            typeCache[tag] = name;
            return name;
        }

        private static readonly Dictionary<string, string> itemCache = new Dictionary<string, string>();

        /// <summary>物品显示名：先查游戏自己的名字缓存，再查语言表；绝不为了取名去新建物品。</summary>
        internal static string ItemName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (itemCache.TryGetValue(id, out var s)) return s;
            string name = null;
            try
            {
                var ps = Game.PlayerStoreOrNull;
                if (ps != null && ps.itemDisplayNameCache != null && ps.itemDisplayNameCache.TryGetValue(id, out var cached)) name = cached;
            }
            catch { }
            if (name == null)
            {
                try { name = LocHelper.GetLocalizedOrDefault("Item", "item_" + id + "_name", id); }
                catch { name = id; }
            }
            itemCache[id] = name;
            return name;
        }

        internal static string Wants(ClientAgg a, StoreClient.ClientIntent intent)
        {
            string id = BaseId(a.Id);
            string buys = BuyList(a);
            ClientTables.Sells.TryGetValue(id, out var sellsText);
            ClientTables.Buys.TryGetValue(id, out var buysText);
            sellsText = Shorten(sellsText, 110);
            buysText = Shorten(buysText, 110);
            if (string.IsNullOrEmpty(buys)) buys = buysText;
            switch (intent)
            {
                case StoreClient.ClientIntent.SELL:
                case StoreClient.ClientIntent.WHOLESALE:
                    return "卖：" + (string.IsNullOrEmpty(sellsText) ? "卖货" : sellsText);
                case StoreClient.ClientIntent.SELLNBUY:
                    return "买：" + (string.IsNullOrEmpty(buys) ? "—" : buys) + "；卖：" + (string.IsNullOrEmpty(sellsText) ? "卖货" : sellsText);
                case StoreClient.ClientIntent.BUY:
                    return string.IsNullOrEmpty(buys) ? "—" : buys;
                case StoreClient.ClientIntent.BARTER:
                    return !string.IsNullOrEmpty(sellsText) ? "换：" + sellsText : (string.IsNullOrEmpty(buys) ? "以物易物" : buys);
                default:
                    if (!string.IsNullOrEmpty(buys)) return buys;
                    if (!string.IsNullOrEmpty(sellsText)) return "卖：" + sellsText;
                    return "—";
            }
        }

        private static string BuyList(ClientAgg a)
        {
            var parts = new List<string>();
            foreach (var t in a.Tags.OrderBy(x => x, StringComparer.Ordinal)) parts.Add(TypeName(t));
            foreach (var i in a.ItemIds.OrderBy(x => x, StringComparer.Ordinal)) parts.Add(ItemName(i));
            var uniq = parts.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            if (uniq.Count == 0) return null;
            if (uniq.Count > 6) return string.Join("、", uniq.Take(6)) + " 等 " + uniq.Count + " 类";
            return string.Join("、", uniq);
        }

        /// <summary>去掉沙盒里给伪装改造人加的后缀，方便查表。</summary>
        internal static string BaseId(string id)
        {
            if (id == null) return "";
            const string aug = "_disguised_aug";
            return id.EndsWith(aug, StringComparison.Ordinal) ? id.Substring(0, id.Length - aug.Length) : id;
        }

        internal static string Budget(ClientAgg a)
        {
            if (a.BudgetRuns == 0) return "—";
            if (a.BudgetMin == a.BudgetMax) return a.BudgetMin.ToString();
            return a.BudgetMin + "–" + a.BudgetMax;
        }

        internal static string Percent(double p)
        {
            if (p >= 0.9995) return "必来";
            if (p < 0.005) return "<1%";
            return Math.Round(p * 100.0).ToString("0") + "%";
        }

        internal static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max - 1) + "…";
        }
    }
}
