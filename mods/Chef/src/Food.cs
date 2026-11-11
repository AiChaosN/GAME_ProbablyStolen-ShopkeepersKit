using System;
using System.Collections.Generic;
using System.Linq;

namespace Chef
{
    internal enum Dish { Fries, Chicken, Burger, Combo }

    /// <summary>
    /// 品质分（满分 100）→ 档次 → 价格倍率（用户 2026-11-12 定：米其林 = 价值 +200%）。
    ///   炸鸡：肉的千卡 35 + 油的纯净度 45 + 模组 20
    ///   薯条：营养果的千卡 25 + 油的纯净度 55 + 模组 20
    ///   汉堡：肉的千卡 30 + 食材比例 50 + 模组 20（不过油）
    ///   超级大套餐：取三样里最低的分
    /// </summary>
    internal static class Quality
    {
        internal const int Awful = 0, Normal = 1, Tasty = 2, ChefTier = 3, Michelin = 4;

        internal static int TierOf(int score) => score >= 90 ? Michelin : score >= 75 ? ChefTier : score >= 55 ? Tasty : score >= 30 ? Normal : Awful;

        internal static double Multiplier(int tier) => tier switch { Michelin => 3.0, ChefTier => 2.0, Tasty => 1.5, Normal => 1.0, _ => 0.5 };

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
            Michelin => "+200%",
            ChefTier => "+100%",
            Tasty => "+50%",
            Normal => "±0",
            _ => "−50%",
        };

        // ---- 各项得分

        internal const double MeatFullKcal = 1200;   // 标准生肉
        internal const double FruitFullKcal = 384;   // 大营养果
        internal const double OilZero = 0.70, OilFull = 0.98;
        internal const double ModuleFull = 100;      // 模组品质加成 +100% 拿满分
        internal const double IdealMeat = 0.55, IdealFruit = 0.30, IdealWeed = 0.15;
        internal const double RatioZero = 0.5;       // 三样比例偏差（绝对值相加）到 0.5 就是 0 分

        internal static int MeatPoints(double kcal, int weight) => Pts(weight, kcal / MeatFullKcal);
        internal static int FruitPoints(double kcal, int weight) => Pts(weight, kcal / FruitFullKcal);
        internal static int OilPoints(double purity, int weight) => Pts(weight, (purity - OilZero) / (OilFull - OilZero));

        internal static int ModulePoints(GameItem machine)
        {
            int bonus = 0;
            try { bonus = MachineryHelper.GetCurrentQualityBonus(machine); } catch { }
            return Pts(20, bonus / ModuleFull);
        }

        internal static int RatioPoints(double meat, double fruit, double weed)
        {
            double total = meat + fruit + weed;
            if (total <= 0) return 0;
            double d = Math.Abs(meat / total - IdealMeat) + Math.Abs(fruit / total - IdealFruit) + Math.Abs(weed / total - IdealWeed);
            return Pts(50, 1 - d / RatioZero);
        }

        private static int Pts(int weight, double frac) => (int)Math.Round(weight * Math.Max(0, Math.Min(1, frac)));
    }

    /// <summary>一份菜的得分明细（存在菜自己的 tag 上，鼠标指着时显示）。不适用的项是 -1。</summary>
    internal struct Score
    {
        internal int Meat, Fruit, Oil, Ratio, Module;
        internal int Total => Math.Max(0, Meat) + Math.Max(0, Fruit) + Math.Max(0, Oil) + Math.Max(0, Ratio) + Math.Max(0, Module);
        internal static Score Empty => new Score { Meat = -1, Fruit = -1, Oil = -1, Ratio = -1, Module = -1 };
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
        internal static long BasePrice(Dish d) => d switch { Dish.Fries => 200L, Dish.Chicken => 200L, Dish.Burger => 300L, _ => 900L };

        internal static string DishName(Dish d) => d switch
        {
            Dish.Fries => Lang.T("薯条", "Fries"),
            Dish.Chicken => Lang.T("炸鸡", "Fried Chicken"),
            Dish.Burger => Lang.T("肉汉堡", "Meat Burger"),
            _ => Lang.T("超级大套餐", "Super Combo"),
        };

        internal static string Describe(Dish d) => d switch
        {
            Dish.Fries => Lang.T("营养果切条下油锅炸的。品质看油的纯净度和营养果的千卡。", "Nutrifruit cut into sticks and deep-fried. Quality depends on oil purity and the nutrifruit's calories."),
            Dish.Chicken => Lang.T("肉裹粉下油锅炸的。品质看肉的千卡和油的纯净度。", "Meat, coated and deep-fried. Quality depends on the meat's calories and oil purity."),
            Dish.Burger => Lang.T("营养果面包胚夹肉饼和填料草。品质看肉的千卡和三样食材的比例。", "A nutrifruit bun with a meat patty and fillerweed. Quality depends on the meat's calories and the ratio of the three ingredients."),
            _ => Lang.T("汉堡、薯条、炸鸡各一份。品质取三样里最低的。", "A burger, fries and fried chicken. Quality is that of the lowest of the three."),
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

        /// <summary>套餐：品质取三样里最低的。</summary>
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
                b.AddLine(" " + Lang.T("取汉堡、薯条、炸鸡里最低的分", "Lowest score of the burger, fries and chicken"), true, RenderHandler.ColorPalette.LightGray);
                return;
            }
            var parts = new List<string>();
            void Part(string tag, string zh, string en, int max)
            {
                int v = it.GetTagReadonly(tag).GetInt();
                if (it.IsTag(tag) && v >= 0) parts.Add(Lang.T(zh, en) + " " + v + "/" + max);
            }
            var d = DishOf(it);
            Part(MeatTag, "肉", "Meat", d == Dish.Burger ? 30 : 35);
            Part(FruitTag, "营养果", "Nutrifruit", 25);
            Part(OilTag, "油", "Oil", d == Dish.Fries ? 55 : 45);
            Part(RatioTag, "比例", "Ratio", 50);
            Part(ModuleTag, "模组", "Modules", 20);
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
