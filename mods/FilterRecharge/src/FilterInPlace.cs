using System;
using System.Collections.Generic;

namespace FilterRecharge
{
    /// <summary>
    /// 原地过滤：把高级滤水器拖到装着水的容器（大桶、储水罐……）上，桶里的水直接过滤一遍，不用倒进水罐再倒回来。
    /// 和游戏的紫外线灯（拖到容器上杀菌）是同一种用法。
    ///
    /// 等价于把整桶水倒过一个装着这个滤水器的水罐：对每种液体调用游戏自己的 WaterHelper.HandleFilter
    /// （TransferLiquid 倒进带滤芯的容器时用的就是它）——去掉多少杂质、滤水器消耗多少耐久都和手动一样；
    /// 按水罐的容量（3000 ml）分批过滤：手动倒的时候大桶也是一罐一罐倒过滤芯的，每批单独算下限（filterMinimumPurity）
    /// 和游戏 HandleFilter 每次最多 10000 次的循环上限，所以和手动倒的结果一致；滤水器耐久用完后，剩下的批次原样通过（手动也是这样）。
    /// 之后用游戏的 WaterFeatureHelper.OnLiquidChanged / UpdateWaterFeature 刷新水质显示、价值和贴图：
    /// 测过水质（显示的是真实纯度）的桶显示新的真实纯度；没测过的仍显示你以为的纯度，不替你偷看。
    ///
    /// 不处理：酒类容器、带滤芯槽的容器（水罐等，拖上去是换件 / 充能）、空的、不是你的。
    /// 设置里可以关（filterInPlace，默认开）。
    /// </summary>
    internal static class FilterInPlace
    {
        private const string LiquidTag = "LIQUID_CONTAINER_TAG";
        // 游戏内部的液体单位是千分之一毫升（容量标签 = 毫升 × 1000，GetTotalCapacityML 除以 1000；提示里也除以 1000 显示）。
        // 1.2.0 把一罐当成了 3000 个内部单位（= 3 ml），批次小了 1000 倍：20 L 的储水罐算成 6667 罐，又卡，
        // 而且 HandleFilter 每批每种杂质至少扣 1 点耐久，耐久被多扣了几十倍（用户 2026-11-12 报告，一罐水用光了 6210 耐久）。
        private const int UnitsPerMl = 1000;
        private const int PitcherCapacity = 3000 * UnitsPerMl;   // 水罐容量 3000 ml（AmenitiesItemDirectory.WaterPitcher：InitLiquidContainerItem(…, 3000, …)）

        internal static bool Applies(GameItem src, GameItem target)
        {
            try
            {
                var cfg = FilterRechargeMod.Config;
                if (cfg == null || !cfg.FilterInPlace) return false;
                if (src == null || target == null || ReferenceEquals(src, target)) return false;
                if (src.identifier != Recharge.AdvancedId || src.unitCount != 1) return false;
                if (!GeneralHelper.IsItemOwned(src) || !GeneralHelper.IsItemOwned(target)) return false;
                if (!target.IsTag(LiquidTag) || target.IsTag("WINE_CONTAINER_TAG") || target.IsTag("FILTER_CONTAINER_TAG")) return false;
                if (WaterHelper.IsEmpty(target)) return false;
                if (GraphUtils.FindParentEquals(src, target)) return false;
                return true;
            }
            catch { return false; }
        }

        internal static void Run(GameItem filter, GameItem target)
        {
            string fName = Name(filter), tName = Name(target);
            int dur = DurabilityHelper.GetRealDurability(filter);
            if (dur <= 0)
            {
                Notify(Lang.T(fName + "已经用完了，先充能", fName + " is used up; recharge it first"));
                return;
            }

            int before = WaterHelper.GetTotalVolume(target);
            var liquids = Liquid.Liquids;
            var left = new int[liquids.Count];      // 还没过滤的
            var kept = new int[liquids.Count];      // 过滤后留下的
            var orig = new int[liquids.Count];     // 过滤前
            for (int i = 0; i < liquids.Count; i++) orig[i] = left[i] = Math.Max(0, target.GetTagReadonly(liquids[i].currentPart).GetInt());
            int remaining = before, batches = 0;
            while (remaining > 0 && batches < 10000)
            {
                // 这一批：按比例从每种液体里取，总量 = 一罐（最后一批取完剩下的）
                int take = Math.Min(PitcherCapacity, remaining);
                var part = new int[liquids.Count];
                int sum = 0;
                for (int i = 0; i < liquids.Count; i++)
                {
                    part[i] = take == remaining ? left[i] : (int)((long)left[i] * take / remaining);
                    sum += part[i];
                }
                for (int i = 0; sum < take && i < liquids.Count; i++)   // 取整差的几 ml 补到还有剩的液体上
                {
                    int extra = Math.Min(take - sum, left[i] - part[i]);
                    if (extra > 0) { part[i] += extra; sum += extra; }
                }
                if (sum <= 0) break;
                for (int i = 0; i < liquids.Count; i++)
                {
                    if (part[i] <= 0) continue;
                    kept[i] += WaterHelper.HandleFilter(liquids[i].identifier, part[i], target, filter);
                    left[i] -= part[i];
                }
                remaining -= sum;
                batches++;
            }
            var changed = new List<string>();
            for (int i = 0; i < liquids.Count; i++)
            {
                int total = kept[i] + left[i];
                int old = target.GetTagReadonly(liquids[i].currentPart).GetInt();
                if (total == old) continue;
                target.ModifyTag(liquids[i].currentPart, s => s.Enable().SetInt(total), skipValidate: true);
                changed.Add(liquids[i].identifier + " " + old + "→" + total);
            }
            int removed = before - WaterHelper.GetTotalVolume(target);
            int used = dur - DurabilityHelper.GetRealDurability(filter);
            bool ranOut = DurabilityHelper.GetRealDurability(filter) <= 0;
            // 滤掉了能滤的杂质的多少（耐久中途用完时告诉玩家）
            long couldRemove = 0;
            for (int i = 0; i < liquids.Count; i++)
                if (kept[i] + left[i] < orig[i]) couldRemove += (long)(orig[i] * (1.0 - liquids[i].filterMinimumPurity));

            if (removed <= 0 && used <= 0)
            {
                Notify(Lang.T(tName + "里没有这个滤水器能滤掉的东西", "Nothing in " + tName + " that this filter can remove"));
                return;
            }

            // 刷新水质显示、价值、贴图（和游戏在液体变化后做的一样）
            try
            {
                var feature = WaterFeatureHelper.GetWaterFeature(target);
                bool exposed = feature != null && feature.IsExposed();
                WaterFeatureHelper.OnLiquidChanged(target, exposed);
                WaterFeatureHelper.UpdateWaterFeature(target, exposed);
                WaterHelper.UpdateWaterBottleSound(target);
                AudioHelper.UseWaterFilter();
                target.Validate();
                filter.Validate();
            }
            catch (Exception e) { Log.Warn("刷新水质显示出错: " + e.Message); }

            int now = DurabilityHelper.GetRealDurability(filter), max = DurabilityHelper.GetMaxDurability(filter);
            string ml = (removed / (double)UnitsPerMl).ToString("0.#");
            string msg = Lang.T("过滤了" + tName + "：去掉杂质 " + ml + " ml，" + fName + "耐久度 -" + used + " → " + now + "/" + max,
                                "Filtered " + tName + ": removed " + ml + " ml of impurities, " + fName + " durability -" + used + " → " + now + "/" + max);
            if (ranOut)
            {
                // 中途用完：只滤了一部分（剩下的原样通过，和手动倒一样）
                int pct = couldRemove > 0 ? (int)Math.Min(100, removed * 100L / couldRemove) : 0;
                msg += Lang.T("。耐久用完了，只滤了大约 " + pct + "%，充能后可以再滤", ". Durability ran out after about " + pct + "%; recharge and filter again");
            }
            Notify(msg);
            Log.Info("原地过滤：" + tName + " " + (before / (double)UnitsPerMl).ToString("0.#") + " ml，分 " + batches + " 罐，去掉 " + ml + " ml（内部单位：" + string.Join("，", changed) + "），" + fName + " 耐久 -" + used + (ranOut ? "（用完）" : ""));
        }

        private static void Notify(string msg)
        {
            try
            {
                if (StoreUIManager.Instance != null) StoreUIManager.Instance.Notify(msg);
                else Log.Info(msg);
            }
            catch { Log.Info(msg); }
        }

        private static string Name(GameItem it)
        {
            try
            {
                var n = it.GetDisplayName(true);
                return string.IsNullOrEmpty(n) ? it.identifier ?? "?" : n;
            }
            catch { return it?.identifier ?? "?"; }
        }
    }
}
