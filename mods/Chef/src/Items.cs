using System;

namespace Chef
{
    /// <summary>
    /// 食用油桶：游戏的液体容器（WaterHelper.InitLiquidContainerItem），出厂装满 10 L 食用油。
    ///   能和游戏的其他液体容器互相倒（游戏自己的拖放），能放进油锅的“油桶”槽。
    /// 滤油网：游戏的滤芯（WATER_FILTER，过滤尺寸 150），装进滤水壶里用：油倒进滤水壶时滤掉大颗粒炸渣（颗粒 200），
    ///   小颗粒油渣（颗粒 40）漏过去，要再用滤水器（过滤尺寸 22）滤。
    /// </summary>
    internal static class Items
    {
        internal const string BarrelId = "chaos5090_chef_oil_barrel";
        internal const string StrainerId = "chaos5090_chef_strainer";
        internal const int BarrelMl = 10000;
        internal const int StrainerDurability = 2000;
        internal const int StrainerSize = 150;   // 和游戏的棉布一样粗

        internal static string BarrelName() => Lang.T("食用油桶", "Cooking Oil Jug");

        internal static string BarrelDesc() => Lang.T(
            "10 L 的桶，出厂装满食用油。和游戏的其他桶、瓶子一样能互相倒；放进油锅的“油桶”槽就能炸东西。油里不能混水。",
            "A 10 L jug, filled with cooking oil. Pours to and from the game's other containers like any jug; put it in the deep fryer's \"Oil\" slot to fry. Keep water out of the oil.");

        internal static string StrainerName() => Lang.T("滤油网", "Oil Strainer");

        internal static string StrainerDesc() => Lang.T(
            "粗滤芯（过滤尺寸 " + StrainerSize + "），装进滤水壶里用：把油倒进滤水壶，大颗粒炸渣会被滤掉，小颗粒油渣要再用滤水器滤。每滤 1 ml 炸渣掉 5 点耐久。",
            "A coarse filter (size " + StrainerSize + ") for the water pitcher: pour oil into the pitcher to strain out large crumbs; fine residue needs a water filter. Loses 5 durability per ml of crumbs.");

        internal static GameItem CreateBarrel()
        {
            ChefMod.EnsureLiquids();   // 第一次生成油桶时（读档、开局发放）登记食用油
            var it = ItemDirectory.CreateEmptyItem().SetSpriteAndShapeFromMod(ChefMod.ModId, "chef_oil_barrel");
            it.name = BarrelName();
            it.shortDescription = BarrelDesc();
            WaterHelper.InitLiquidContainerItem(it, BarrelMl, 20, isFilterContainer: false, isGraduated: true);
            try { AudioHelper.InitPlasticContainer(it); } catch { }
            Oil.Fill(it, BarrelMl);
            return it;
        }

        internal static GameItem CreateStrainer()
        {
            var it = ItemDirectory.CreateEmptyItem().SetSpriteAndShapeFromMod(ChefMod.ModId, "chef_strainer");
            it.name = StrainerName();
            it.shortDescription = StrainerDesc();
            it.SetGameItemType("HOUSEHOLD_GOOD");
            it.SetGameItemType("FILTER");
            it.ModifyTag("FILTER_SIZE_TAG", s => s.Enable().SetInt(StrainerSize));
            it.EnableTag("WATER_FILTER");
            it.SetValue(60L);
            DurabilityHelper.InitItemWithDurabilityPricingMode2(it, StrainerDurability, 5);
            try { AudioHelper.InitMetalMachine(it); } catch { }
            return it;
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
