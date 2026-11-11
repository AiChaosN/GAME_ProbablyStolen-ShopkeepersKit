using System;
using System.Collections.Generic;
using System.Linq;

namespace Chef
{
    internal enum Dish { Fries, Chicken, Burger, Combo }

    /// <summary>
    /// 品质分（满分 100）。用户 2026-11-12 定的规则：食材 / 油最多 50 分，剩下的 50 分只能靠机器上的品质模组。
    ///   薯条（100）：营养果每 300 千卡 5 分，最多 15；油的纯净度 ≥95% / ≥85% / ≥60% 得 35 / 20 / 5，更低 0；
    ///   炸鸡（200）：肉每 1000 千卡 5 分，最多 15；油同上；
    ///   肉汉堡（400）：肉要超过 1000 千卡、填料草超过 600、营养果超过 400 才能做；肉每多 500 千卡 +10 分；
    ///                  比例 肉 : 菜（填料草）: 面（营养果）= 5 : 3 : 2，偏离多少扣多少；食材部分最多 50；
    ///   超级大套餐（900）：薯条、炸鸡、汉堡的分按 1 : 2 : 4 加权平均；
    ///   品质模组：品质加成每 +2% 加 1 分，最多 50。
    /// 档次：米其林 ≥98（+300%）、主厨 ≥90（+100%）、美味 ≥75（+50%）、普通 ≥50（原价）、难以下咽 &lt;50（−50%）。
    /// “每 300 千卡 5 分”这类按整档算（向下取整）。
    /// </summary>
    internal static class Quality
    {
        internal const int Awful = 0, Normal = 1, Tasty = 2, ChefTier = 3, Michelin = 4;

        internal static int TierOf(int score) => score >= 98 ? Michelin : score >= 90 ? ChefTier : score >= 75 ? Tasty : score >= 50 ? Normal : Awful;

        internal static double Multiplier(int tier) => tier switch { Michelin => 4.0, ChefTier => 2.0, Tasty => 1.5, Normal => 1.0, _ => 0.5 };

        internal static string TierName(int tier) => tier switch
        {
            Michelin => Lang.T("米其林", "Michelin"),
            ChefTier => Lang.T("主厨", "Chef's"),
            Tasty => Lang.T("美味", "Tasty"),
            Normal => Lang.T("普通", "Plain"),
            _ => Lang.T("难以下咽", "Inedible"),
        };

        internal static string PriceChange(int tier) => tier switch
        {
            Michelin => "+300%",
            ChefTier => "+100%",
            Tasty => "+50%",
            Normal => "±0",
            _ => "−50%",
        };

        internal const int IngredientMax = 50, ModuleMax = 50;
        internal const int FruitMax = 15, ChickenMeatMax = 15, OilMax = 35, BurgerMeatMax = 50;
        internal const int BurgerMinMeat = 1000, BurgerMinWeed = 600, BurgerMinFruit = 400;   // 千卡，要“超过”
        internal const double IdealMeat = 0.5, IdealWeed = 0.3, IdealFruit = 0.2;
        internal const double RatioPenaltyPerUnit = 50;   // 三项偏差（绝对值相加，0~2）× 50 = 扣分

        internal static int FruitPoints(int kcal) => Math.Min(FruitMax, kcal / 300 * 5);
        internal static int ChickenMeatPoints(int kcal) => Math.Min(ChickenMeatMax, kcal / 1000 * 5);
        internal static int OilPoints(double purity) => purity >= 0.95 ? 35 : purity >= 0.85 ? 20 : purity >= 0.60 ? 5 : 0;
        internal static int BurgerMeatPoints(int meatKcal) => meatKcal <= BurgerMinMeat ? 0 : Math.Min(BurgerMeatMax, (meatKcal - BurgerMinMeat) / 500 * 10);

        /// <summary>比例偏离的扣分（≤ 0）。</summary>
        internal static int RatioPenalty(double meat, double weed, double fruit)
        {
            double total = meat + weed + fruit;
            if (total <= 0) return 0;
            double d = Math.Abs(meat / total - IdealMeat) + Math.Abs(weed / total - IdealWeed) + Math.Abs(fruit / total - IdealFruit);
            return -(int)Math.Round(d * RatioPenaltyPerUnit);
        }

        /// <summary>品质模组：品质加成每 +2% 加 1 分，最多 50。</summary>
        internal static int ModulePoints(GameItem machine)
        {
            int bonus = 0;
            try { bonus = MachineryHelper.GetCurrentQualityBonus(machine); } catch { }
            return Math.Max(0, Math.Min(ModuleMax, bonus / 2));
        }

        /// <summary>性能模组：每满 +100% 多做一份（向下取整）。返回一次做几份。</summary>
        internal static int Copies(GameItem machine)
        {
            int perf = 0;
            try { perf = MachineryHelper.GetCurrentPerformanceBonus(machine); } catch { }
            return 1 + Math.Max(0, perf) / 100;
        }
    }

    /// <summary>一份菜的得分明细（存在菜自己的 tag 上，鼠标指着时显示）。不适用的项是 Score.None。</summary>
    internal struct Score
    {
        internal const int None = int.MinValue;
        internal int Meat, Fruit, Oil, Ratio, Module;

        private static int V(int x) => x == None ? 0 : x;

        /// <summary>食材 / 油 / 比例部分（0~50）+ 模组（0~50）。</summary>
        internal int Total => Math.Max(0, Math.Min(Quality.IngredientMax, V(Meat) + V(Fruit) + V(Oil) + V(Ratio))) + Math.Max(0, V(Module));

        internal static Score Empty => new Score { Meat = None, Fruit = None, Oil = None, Ratio = None, Module = None };
    }

    /// <summary>四道菜：物品定义、按得分生成、读回得分。</summary>
    internal static class Food
    {
        internal const string FriesId = "chaos5090_chef_fries";
        internal const string ChickenId = "chaos5090_chef_fried_chicken";
        internal const string BurgerId = "chaos5090_chef_burger";
        internal const string ComboId = "chaos5090_chef_combo";

        private const string ScoreTag = "CHAOS5090_CHEF_SCORE";
        private const string MeatTag = "CHAOS5090_CHEF_S_MEAT", FruitTag = "CHAOS5090_CHEF_S_FRUIT", OilTag = "CHAOS5090_CHEF_S_OIL",
                             RatioTag = "CHAOS5090_CHEF_S_RATIO", ModuleTag = "CHAOS5090_CHEF_S_MODULE", ComboTag = "CHAOS5090_CHEF_S_COMBO";

        internal static string Id(Dish d) => d switch { Dish.Fries => FriesId, Dish.Chicken => ChickenId, Dish.Burger => BurgerId, _ => ComboId };
        private static string Sprite(Dish d) => d switch { Dish.Fries => "chef_fries", Dish.Chicken => "chef_fried_chicken", Dish.Burger => "chef_burger", _ => "chef_combo" };
        internal static long BasePrice(Dish d) => d switch { Dish.Fries => 100L, Dish.Chicken => 200L, Dish.Burger => 400L, _ => 900L };

        internal static string DishName(Dish d) => d switch
        {
            Dish.Fries => Lang.T("薯条", "Fries"),
            Dish.Chicken => Lang.T("炸鸡", "Fried Chicken"),
            Dish.Burger => Lang.T("肉汉堡", "Meat Burger"),
            _ => Lang.T("超级大套餐", "Super Combo"),
        };

        internal static string Describe(Dish d) => d switch
        {
            Dish.Fries => Lang.T("营养果切条下油锅炸的。品质看营养果的千卡、油的纯净度和油锅的品质模组。", "Nutrifruit cut into sticks and deep-fried. Quality depends on the nutrifruit's calories, oil purity and the fryer's quality modules."),
            Dish.Chicken => Lang.T("肉裹粉下油锅炸的。品质看肉的千卡、油的纯净度和油锅的品质模组。", "Meat, coated and deep-fried. Quality depends on the meat's calories, oil purity and the fryer's quality modules."),
            Dish.Burger => Lang.T("营养果面包胚夹肉饼和填料草。品质看肉的量、三样食材的比例和加工器的品质模组。", "A nutrifruit bun with a meat patty and fillerweed. Quality depends on the amount of meat, the ratio of the three ingredients and the processor's quality modules."),
            _ => Lang.T("汉堡、薯条、炸鸡各一份。品质按 汉堡 4 : 炸鸡 2 : 薯条 1 加权平均。", "A burger, fries and fried chicken. Quality is the weighted average: burger 4 : chicken 2 : fries 1."),
        };

        internal static bool IsDish(GameItem it) => it != null && (it.identifier == FriesId || it.identifier == ChickenId || it.identifier == BurgerId || it.identifier == ComboId);

        internal static Dish? DishOf(GameItem it)
        {
            if (it == null) return null;
            switch (it.identifier)
            {
                case FriesId: return Dish.Fries;
                case ChickenId: return Dish.Chicken;
                case BurgerId: return Dish.Burger;
                case ComboId: return Dish.Combo;
                default: return null;
            }
        }

        /// <summary>物品工厂：一份“普通”的菜（读档时游戏先用它建物品，再把存档里的 tag 写回去）。</summary>
        internal static GameItem CreateBase(Dish d)
        {
            var it = ItemDirectory.CreateEmptyItem().SetSpriteAndShapeFromMod(ChefMod.ModId, Sprite(d));
            it.name = DishName(d);
            it.shortDescription = Describe(d);
            it.SetGameItemType("FOOD");
            it.unitValue = BasePrice(d);
            FoodItemHelper.InitFoodItem(it, d == Dish.Combo ? 2500 : d == Dish.Burger ? 1800 : 600, isCanned: false);
            return it;
        }

        /// <summary>按得分做一份菜。</summary>
        internal static GameItem Make(Dish d, Score s, int kcal)
        {
            var it = DirectoryMaster.Item(Id(d));
            if (it == null) return null;
            Set(it, ScoreTag, s.Total);
            Set(it, MeatTag, s.Meat);
            Set(it, FruitTag, s.Fruit);
            Set(it, OilTag, s.Oil);
            Set(it, RatioTag, s.Ratio);
            Set(it, ModuleTag, s.Module);
            if (kcal > 0) FoodItemHelper.SetCalories(it, kcal);
            Apply(it);
            return it;
        }

        /// <summary>套餐：品质是三样的加权平均（薯条 1、炸鸡 2、汉堡 4）。</summary>
        internal static GameItem MakeCombo(int minScore, int kcal)
        {
            var it = DirectoryMaster.Item(ComboId);
            if (it == null) return null;
            Set(it, ScoreTag, minScore);
            Set(it, ComboTag, 1);
            if (kcal > 0) FoodItemHelper.SetCalories(it, kcal);
            Apply(it);
            return it;
        }

        internal static int ScoreOf(GameItem it) => it == null || !it.IsTag(ScoreTag) ? -1 : it.GetTagReadonly(ScoreTag).GetInt();

        /// <summary>按得分刷新名字、价格、类别（做菜时、读档时、切换语言后）。</summary>
        internal static void Apply(GameItem it)
        {
            var d = DishOf(it);
            if (d == null) return;
            int score = ScoreOf(it);
            if (score < 0)
            {
                it.name = DishName(d.Value);
                it.shortDescription = Describe(d.Value);
                return;
            }
            int tier = Quality.TierOf(score);
            it.name = DishName(d.Value) + Lang.T("（" + Quality.TierName(tier) + "）", " (" + Quality.TierName(tier) + ")");
            it.shortDescription = Describe(d.Value);
            it.unitValue = (long)Math.Round(BasePrice(d.Value) * Quality.Multiplier(tier));
            if (tier >= Quality.ChefTier) it.SetGameItemType("LUXURY_ITEM");
            else it.RemoveGameItemType("LUXURY_ITEM");
        }

        /// <summary>鼠标指着时的品质说明。</summary>
        internal static void Tooltip(RichTextBuilder b, GameItem it)
        {
            int score = ScoreOf(it);
            if (score < 0) return;
            int tier = Quality.TierOf(score);
            var color = tier switch
            {
                Quality.Michelin => RenderHandler.ColorPalette.Yellow,
                Quality.ChefTier => RenderHandler.ColorPalette.LightOrange,
                Quality.Tasty => RenderHandler.ColorPalette.LightGreen,
                Quality.Normal => RenderHandler.ColorPalette.VeryLightGray,
                _ => RenderHandler.ColorPalette.LightRed,
            };
            b.AddLine(" " + Lang.T("品质：", "Quality: ") + Quality.TierName(tier) + Lang.T("（" + score + " 分，价格 " + Quality.PriceChange(tier) + "）", " (" + score + " pts, price " + Quality.PriceChange(tier) + ")"), true, color);
            if (it.GetTagReadonly(ComboTag).GetInt() == 1)
            {
                b.AddLine(" " + Lang.T("汉堡、炸鸡、薯条的分按 4 : 2 : 1 加权平均", "Weighted average of burger, chicken and fries (4 : 2 : 1)"), true, RenderHandler.ColorPalette.LightGray);
                return;
            }
            var parts = new List<string>();
            void Part(string tag, string zh, string en, string max)
            {
                if (!it.IsTag(tag)) return;
                int v = it.GetTagReadonly(tag).GetInt();
                if (v == Score.None) return;
                parts.Add(Lang.T(zh, en) + " " + (max == null ? v.ToString() : v + "/" + max));
            }
            var d = DishOf(it);
            Part(MeatTag, "肉", "Meat", d == Dish.Burger ? Quality.BurgerMeatMax.ToString() : Quality.ChickenMeatMax.ToString());
            Part(FruitTag, "营养果", "Nutrifruit", Quality.FruitMax.ToString());
            Part(OilTag, "油", "Oil", Quality.OilMax.ToString());
            Part(RatioTag, "比例", "Ratio", null);
            Part(ModuleTag, "模组", "Modules", Quality.ModuleMax.ToString());
            if (parts.Count > 0) b.AddLine(" " + string.Join(" · ", parts), true, RenderHandler.ColorPalette.LightGray);
        }

        private static void Set(GameItem it, string tag, int v) => it.ModifyTag(tag, s => s.Enable().SetInt(v));
    }

    /// <summary>食材：肉（各种肉、泥炭合成肉、鱼肉……）、营养果、填料草。千卡用游戏自己的 CALORIE_VALUE_TAG。</summary>
    internal static class Ingredients
    {
        internal const string Nutrifruit = "nutrifruit";
        internal const string Fillerweed = "fillerweed";

        internal static bool IsMeat(GameItem it)
        {
            if (it == null || Food.IsDish(it) || !it.IsTag("CALORIE_VALUE_TAG")) return false;
            try { return FoodItemHelper.IsMeatFood(it); }
            catch { return false; }
        }

        internal static bool IsFruit(GameItem it) => it != null && it.identifier == Nutrifruit;
        internal static bool IsWeed(GameItem it) => it != null && it.identifier == Fillerweed;
        internal static bool IsIngredient(GameItem it) => IsMeat(it) || IsFruit(it) || IsWeed(it);

        /// <summary>一件（一叠）的千卡。</summary>
        internal static int Kcal(GameItem it) => it == null ? 0 : Math.Max(0, it.GetTagReadonly("CALORIE_VALUE_TAG").GetInt()) * Math.Max(1, it.unitCount);

        /// <summary>单个的千卡（一叠里的一个）。</summary>
        internal static int UnitKcal(GameItem it) => it == null ? 0 : Math.Max(0, it.GetTagReadonly("CALORIE_VALUE_TAG").GetInt());

        /// <summary>从一叠里拿走一个；拿完了就销毁。</summary>
        internal static void ConsumeOne(GameInventory inv, GameItem it)
        {
            if (it == null) return;
            if (it.unitCount > 1)
            {
                it.GetSplitItem(1)?.Destroy();
                return;
            }
            try { inv?.Expel(it); } catch { }
            it.Destroy();
        }

        internal static void ConsumeAll(GameInventory inv, IEnumerable<GameItem> items)
        {
            foreach (var it in items.ToList())
            {
                try { inv?.Expel(it); } catch { }
                it.Destroy();
            }
        }
    }
}
