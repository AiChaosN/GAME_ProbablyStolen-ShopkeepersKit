using System;

namespace Chef
{
    /// <summary>
    /// 食用油桶、滤油网，以及“拖到某个东西上”的动作（游戏的 mayThisTargetItemFunc / canThisTargetItemFunc / onThisTargetItemFunc，
    /// 挂在被拖的那件物品上）：
    ///   油桶 → 油锅 / 另一个油桶：倒油；
    ///   滤油网 → 油锅 / 油桶：滤大颗粒；
    ///   油桶 / 油锅 → 装着滤水器的滤水壶：滤小颗粒。
    /// 这些委托不进存档，读档时游戏用物品工厂重建物品，所以每次都会挂上。
    /// </summary>
    internal static class Items
    {
        internal const string BarrelId = "chaos5090_chef_oil_barrel";
        internal const string StrainerId = "chaos5090_chef_strainer";
        internal const int BarrelMl = 10000;
        private const long BarrelEmptyValue = 20;
        private const string PitcherId = "water_pitcher";

        internal static string BarrelName() => Lang.T("食用油桶", "Cooking Oil Jug");

        internal static string BarrelDesc() => Lang.T(
            "一桶食用油（10 L）。拖到油锅上倒油；拖到装着滤水器的滤水壶上滤掉小颗粒油渣；滤油网拖到它上面滤掉大颗粒炸渣。",
            "A jug of cooking oil (10 L). Drop it on a deep fryer to pour the oil in; drop it on a water pitcher with a filter to filter out fine residue; drop a strainer on it to strain out large crumbs.");

        internal static string StrainerName() => Lang.T("滤油网", "Oil Strainer");

        internal static string StrainerDesc() => Lang.T(
            "拖到油锅或食用油桶上，滤掉油里的大颗粒炸渣。每滤 " + Oil.StrainerMlPerPoint + " ml 掉 1 点耐久。",
            "Drop it on a deep fryer or an oil jug to strain out large crumbs. Loses 1 durability per " + Oil.StrainerMlPerPoint + " ml strained.");

        internal static GameItem CreateBarrel()
        {
            var it = ItemDirectory.CreateEmptyItem().SetSpriteAndShapeFromMod(ChefMod.ModId, "chef_oil_barrel");
            it.name = BarrelName();
            it.shortDescription = BarrelDesc();
            it.SetGameItemType("HOUSEHOLD_GOOD");
            Oil.InitHolder(it, BarrelMl, BarrelMl);
            UpdateBarrelValue(it, Oil.Read(it));   // 物品工厂里还没有 identifier，Oil.Write 认不出是油桶，这里自己算
            try { AudioHelper.InitPlasticContainer(it); } catch { }
            InitOilSource(it);
            return it;
        }

        /// <summary>油桶的价值：空桶 20 + 新油每升 12。</summary>
        internal static void UpdateBarrelValue(GameItem it, Oil.Mix m) => it.unitValue = BarrelEmptyValue + (long)m.Fresh * 12 / 1000;

        internal static GameItem CreateStrainer()
        {
            var it = ItemDirectory.CreateEmptyItem().SetSpriteAndShapeFromMod(ChefMod.ModId, "chef_strainer");
            it.name = StrainerName();
            it.shortDescription = StrainerDesc();
            it.SetGameItemType("HOUSEHOLD_GOOD");
            it.unitValue = 60L;
            DurabilityHelper.InitItemWithDurabilityPricingMode2(it, 200, 5);
            try { AudioHelper.InitMetalMachine(it); } catch { }
            it.mayThisTargetItemFunc = (s, t) => Owned(s) && Owned(t) && Oil.IsHolder(t);
            it.canThisTargetItemFunc = it.mayThisTargetItemFunc;
            it.onThisTargetItemFunc = (s, t) => Ui.Run(() => Oil.Strain(t, s));
            return it;
        }

        /// <summary>油桶、油锅：可以拖去倒油、拖到滤水壶上过滤。</summary>
        internal static void InitOilSource(GameItem it)
        {
            it.mayThisTargetItemFunc = (s, t) => Owned(s) && Owned(t) && (CanPourInto(s, t) || IsPitcher(t));
            it.canThisTargetItemFunc = it.mayThisTargetItemFunc;
            it.onThisTargetItemFunc = (s, t) => Ui.Run(() =>
            {
                if (IsPitcher(t)) return Oil.PitcherFilter(s, t);
                int ml = Oil.Pour(s, t);
                s.Validate();
                t.Validate();
                if (ml <= 0) return Oil.Free(t) <= 0 ? Lang.T("装满了", "It's full") : Lang.T("没有油可倒", "There's no oil to pour");
                return Lang.T("倒了 " + Oil.Ml(ml) + "，现在 " + Oil.Ml(Oil.Read(t).Total) + " / " + Oil.Ml(Oil.Capacity(t)),
                              "Poured " + Oil.Ml(ml) + ", now " + Oil.Ml(Oil.Read(t).Total) + " / " + Oil.Ml(Oil.Capacity(t)));
            });
        }

        /// <summary>只有油桶能倒进别的东西（油锅里的油只能倒掉，不往外倒）。</summary>
        private static bool CanPourInto(GameItem s, GameItem t) => s.identifier == BarrelId && Oil.IsHolder(t) && !ReferenceEquals(s, t);

        private static bool IsPitcher(GameItem t) => t != null && t.identifier == PitcherId;

        private static bool Owned(GameItem it)
        {
            try { return it != null && GeneralHelper.IsItemOwned(it); }
            catch { return false; }
        }
    }

    /// <summary>给玩家的提示。</summary>
    internal static class Ui
    {
        internal static void Notify(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            try
            {
                if (StoreUIManager.Instance != null) StoreUIManager.Instance.Notify(msg);
                else Log.Info(msg);
            }
            catch { Log.Info(msg); }
        }

        /// <summary>执行一个动作并提示结果；出错只记日志。</summary>
        internal static void Run(Func<string> action)
        {
            string msg;
            try { msg = action(); }
            catch (Exception e)
            {
                Log.Error("动作出错: " + Log.Describe(e));
                msg = Lang.T("出错了，详见日志", "Something went wrong; see the log");
            }
            Notify(msg);
        }
    }
}
