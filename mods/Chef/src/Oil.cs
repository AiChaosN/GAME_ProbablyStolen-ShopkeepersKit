using System;

namespace Chef
{
    /// <summary>
    /// 食用油。自己记在物品的 tag 上（单位毫升），不走游戏的液体系统：游戏的液体容器一装东西就被当成“水”
    /// （价格按水的纯净度算、算“水”类商品、鼠标指着显示“沟渠水 纯度 90% 以下”），油桶会被当成脏水。
    ///
    /// 油是四样东西的混合，倒来倒去按比例走：
    ///   新油（Fresh）——真正的食用油；
    ///   老化油（Aged）——每炸一晚，新油里有一部分变成老化油，滤不掉（所以滤干净的“二次食用油”也回不到 100%）；
    ///   大颗粒炸渣（Big）——滤油网滤；
    ///   小颗粒油渣（Small）——装着滤水器的滤水壶滤。
    /// 纯净度 = 新油 / 总量。
    /// </summary>
    internal static class Oil
    {
        internal const string CapTag = "CHAOS5090_CHEF_OIL_CAP";
        private const string FreshTag = "CHAOS5090_CHEF_OIL_FRESH";
        private const string AgedTag = "CHAOS5090_CHEF_OIL_AGED";
        private const string BigTag = "CHAOS5090_CHEF_OIL_BIG";
        private const string SmallTag = "CHAOS5090_CHEF_OIL_SMALL";

        internal const int AbsorbPerItem = 100;   // 每炸一份吸掉的油
        internal const int BigPerItem = 12;       // 每炸一份产生的大颗粒炸渣
        internal const int SmallPerItem = 6;      // 每炸一份产生的小颗粒油渣
        internal const double AgingPerNight = 0.04;   // 每炸一晚，新油里变成老化油的比例
        internal const int MinToFry = 1000;       // 油锅里至少要有这么多油才炸

        // 滤油网：每滤掉 StrainerMlPerPoint 毫升大颗粒掉 1 点耐久
        internal const int StrainerMlPerPoint = 5;
        // 滤水器（照游戏自己的规则：每滤 1 ml 掉 2 点耐久）；大颗粒会把滤嘴堵坏：每 1 ml 掉 10 点
        internal const int FilterCostSmall = 2;
        internal const int FilterCostBig = 10;

        internal struct Mix
        {
            internal int Fresh, Aged, Big, Small;
            internal int Total => Fresh + Aged + Big + Small;
            internal double Purity => Total <= 0 ? 0 : (double)Fresh / Total;
        }

        internal static bool IsHolder(GameItem it) => it != null && it.IsTag(CapTag);

        internal static int Capacity(GameItem it) => it == null ? 0 : it.GetTagReadonly(CapTag).GetInt();

        internal static void InitHolder(GameItem it, int capacityMl, int freshMl)
        {
            it.ModifyTag(CapTag, s => s.Enable().SetInt(capacityMl));
            Write(it, new Mix { Fresh = Math.Min(capacityMl, Math.Max(0, freshMl)) });
        }

        internal static Mix Read(GameItem it)
        {
            if (it == null) return default;
            return new Mix
            {
                Fresh = Math.Max(0, it.GetTagReadonly(FreshTag).GetInt()),
                Aged = Math.Max(0, it.GetTagReadonly(AgedTag).GetInt()),
                Big = Math.Max(0, it.GetTagReadonly(BigTag).GetInt()),
                Small = Math.Max(0, it.GetTagReadonly(SmallTag).GetInt()),
            };
        }

        internal static void Write(GameItem it, Mix m)
        {
            if (it == null) return;
            it.ModifyTag(FreshTag, s => s.Enable().SetInt(Math.Max(0, m.Fresh)));
            it.ModifyTag(AgedTag, s => s.Enable().SetInt(Math.Max(0, m.Aged)));
            it.ModifyTag(BigTag, s => s.Enable().SetInt(Math.Max(0, m.Big)));
            it.ModifyTag(SmallTag, s => s.Enable().SetInt(Math.Max(0, m.Small)));
            if (it.identifier == Items.BarrelId) Items.UpdateBarrelValue(it, m);
        }

        internal static int Free(GameItem it) => Math.Max(0, Capacity(it) - Read(it).Total);

        /// <summary>按比例从混合物里取出 amount 毫升（四样东西各取一部分），返回取出来的那份。</summary>
        internal static Mix Take(ref Mix m, int amount)
        {
            var taken = new Mix();
            int total = m.Total;
            if (total <= 0 || amount <= 0) return taken;
            if (amount >= total)
            {
                taken = m;
                m = new Mix();
                return taken;
            }
            taken.Aged = (int)((long)m.Aged * amount / total);
            taken.Big = (int)((long)m.Big * amount / total);
            taken.Small = (int)((long)m.Small * amount / total);
            taken.Fresh = Math.Min(m.Fresh, amount - taken.Aged - taken.Big - taken.Small);   // 取整剩下的算新油
            m.Fresh -= taken.Fresh;
            m.Aged -= taken.Aged;
            m.Big -= taken.Big;
            m.Small -= taken.Small;
            return taken;
        }

        /// <summary>从 src 倒进 dst，能倒多少倒多少；返回倒了多少毫升。</summary>
        internal static int Pour(GameItem src, GameItem dst)
        {
            if (!IsHolder(src) || !IsHolder(dst) || ReferenceEquals(src, dst)) return 0;
            var a = Read(src);
            var b = Read(dst);
            int amount = Math.Min(a.Total, Free(dst));
            if (amount <= 0) return 0;
            var moved = Take(ref a, amount);
            b.Fresh += moved.Fresh;
            b.Aged += moved.Aged;
            b.Big += moved.Big;
            b.Small += moved.Small;
            Write(src, a);
            Write(dst, b);
            return amount;
        }

        /// <summary>油锅炸完一晚：炸了 items 份。吸油、老化、产生残渣。</summary>
        internal static void AfterFrying(ref Mix m, int items)
        {
            if (items <= 0) return;
            Take(ref m, items * AbsorbPerItem);
            int aged = (int)(m.Fresh * AgingPerNight);
            m.Fresh -= aged;
            m.Aged += aged;
            m.Big += items * BigPerItem;
            m.Small += items * SmallPerItem;
        }

        /// <summary>这锅油能炸几份。</summary>
        internal static int Portions(Mix m) => m.Total < MinToFry ? 0 : (m.Total - MinToFry) / AbsorbPerItem + 1;

        // ------------------------------------------------------------------ 过滤

        /// <summary>滤油网滤大颗粒。返回给玩家看的结果。</summary>
        internal static string Strain(GameItem holder, GameItem strainer)
        {
            var m = Read(holder);
            if (m.Total <= 0) return Lang.T("里面没有油", "There's no oil in it");
            if (m.Big <= 0) return Lang.T("油里没有大颗粒炸渣，不用滤", "There are no large crumbs in the oil");
            int durability = DurabilityHelper.GetRealDurability(strainer);
            if (durability <= 0) return Lang.T("滤油网已经用坏了", "The strainer is worn out");
            int can = durability * StrainerMlPerPoint;
            int removed = Math.Min(m.Big, can);
            int cost = (removed + StrainerMlPerPoint - 1) / StrainerMlPerPoint;
            m.Big -= removed;
            Write(holder, m);
            DurabilityHelper.ChangeDurability(strainer, -cost);
            strainer.Validate();
            holder.Validate();
            string left = m.Big > 0 ? Lang.T("，还剩 " + m.Big + " ml（滤油网用坏了）", ", " + m.Big + " ml left (the strainer is worn out)") : "";
            return Lang.T("滤掉大颗粒炸渣 " + removed + " ml，滤油网耐久 −" + cost, "Strained out " + removed + " ml of large crumbs, strainer durability −" + cost)
                   + left + Lang.T("。纯净度 ", ". Purity ") + Percent(Read(holder).Purity);
        }

        /// <summary>用滤水壶里的滤水器滤小颗粒（大颗粒也会被滤掉，但很伤滤嘴）。</summary>
        internal static string PitcherFilter(GameItem holder, GameItem pitcher)
        {
            var m = Read(holder);
            if (m.Total <= 0) return Lang.T("里面没有油", "There's no oil in it");
            GameItem filter = null;
            try { filter = WaterHelper.GetFilterItem(pitcher); } catch { }
            if (filter == null) return Lang.T("滤水壶里没有装滤水器", "The pitcher has no water filter in it");
            int durability = DurabilityHelper.GetRealDurability(filter);
            if (durability <= 0) return Lang.T("滤水器已经用坏了", "The water filter is worn out");
            if (m.Big <= 0 && m.Small <= 0) return Lang.T("油里没有残渣，不用滤", "There's no residue in the oil");

            int usedPoints = 0;
            // 先过大颗粒（会堵滤嘴），再过小颗粒
            int bigRemoved = Math.Min(m.Big, durability / FilterCostBig);
            usedPoints += bigRemoved * FilterCostBig;
            m.Big -= bigRemoved;
            int smallRemoved = Math.Min(m.Small, (durability - usedPoints) / FilterCostSmall);
            usedPoints += smallRemoved * FilterCostSmall;
            m.Small -= smallRemoved;
            if (bigRemoved == 0 && smallRemoved == 0) return Lang.T("滤水器剩的耐久不够用", "The water filter doesn't have enough durability left");
            Write(holder, m);
            DurabilityHelper.ChangeDurability(filter, -usedPoints);
            filter.Validate();
            holder.Validate();

            string msg = Lang.T("滤掉小颗粒油渣 " + smallRemoved + " ml", "Filtered out " + smallRemoved + " ml of fine residue");
            if (bigRemoved > 0) msg += Lang.T("、大颗粒炸渣 " + bigRemoved + " ml（大颗粒很伤滤嘴，先用滤油网）", " and " + bigRemoved + " ml of large crumbs (large crumbs wear filters fast; use the strainer first)");
            msg += Lang.T("，滤水器耐久 −" + usedPoints, ", filter durability −" + usedPoints);
            if (m.Small > 0 || m.Big > 0) msg += Lang.T("（还没滤完）", " (not all of it)");
            return msg + Lang.T("。纯净度 ", ". Purity ") + Percent(Read(holder).Purity);
        }

        // ------------------------------------------------------------------ 显示

        internal static string Percent(double p) => (p * 100).ToString("0.0") + "%";

        internal static string Ml(int ml) => ml >= 1000 ? (ml / 1000.0).ToString("0.0#") + " L" : ml + " ml";

        internal static string StateName(Mix m)
        {
            if (m.Total <= 0) return Lang.T("空的", "Empty");
            if (m.Purity < 0.70) return Lang.T("废油", "Spent oil");
            if (m.Big > 0 || m.Small > 0) return Lang.T("用过的油", "Used oil");
            if (m.Aged > 0) return Lang.T("二次食用油", "Recycled oil");
            return Lang.T("新食用油", "Fresh oil");
        }

        internal static string Amount(int ml)
        {
            if (ml <= 0) return Lang.T("没有", "none");
            string n = Lang.T("（" + ml + " ml）", " (" + ml + " ml)");
            if (ml < 50) return Lang.T("少量", "a little") + n;
            if (ml < 200) return Lang.T("中等", "some") + n;
            return Lang.T("很多", "a lot") + n;
        }
    }
}
