using System;
using System.Collections.Generic;
using System.Linq;

namespace SortToBoxes
{
    /// <summary>
    /// 归类入箱的“大类”：每件货只属于一个类，用尽量少的类覆盖尽量多的货（游戏自带的“类型”有 50 多种，很多只有一两件，一件货还常挂好几个）。
    /// 已知物品查表（src/Generated/ItemCategories.cs，由 tools/categories.py 根据游戏物品目录 + 类型生成）；
    /// 表里没有的（游戏更新加的新物品、其他 mod 的物品）按类型套用同一套规则（FromTypes）。
    /// </summary>
    internal static partial class Categories
    {
        internal const string Other = "other";

        // 面板里的显示顺序
        internal static readonly string[] Order =
        {
            "food", "drink", "household", "medical", "chemical", "weapon", "material",
            "tool", "module", "farming", "animal", "paper", "luxury", Other,
        };

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "food", "食物" }, { "drink", "饮料与水" }, { "household", "日用品" }, { "medical", "医疗" },
            { "chemical", "化学" }, { "weapon", "武器与弹药" }, { "material", "材料" }, { "tool", "工具与设备" },
            { "module", "模组" }, { "farming", "种子与农业" }, { "animal", "养殖" }, { "paper", "文件证件" },
            { "luxury", "奢侈品与收藏" }, { Other, "其他" },
        };

        // 小色块用的颜色：借游戏给代表性类型配的颜色（TypeHelper.GetTypeColor）
        private static readonly Dictionary<string, string> ColorTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "food", "FOOD" }, { "drink", "WATER" }, { "household", "HOUSEHOLD_GOOD" }, { "medical", "MEDICAL" },
            { "chemical", "SUBSTANCE" }, { "weapon", "WEAPON" }, { "material", "MATERIAL" }, { "tool", "TOOL" },
            { "module", "MODULE" }, { "farming", "SEED" }, { "animal", "FOOD" }, { "paper", "DOCUMENT" },
            { "luxury", "LUXURY_ITEM" },
        };

        internal static string Name(string key) => key != null && Names.TryGetValue(key, out var n) ? n : "其他";

        internal static string ColorType(string key) => key != null && ColorTypes.TryGetValue(key, out var t) ? t : "";

        internal static string Of(GameItem it)
        {
            if (it == null) return Other;
            List<string> types = null;
            try { types = it.itemTypes; } catch { }
            return Of(it.identifier, types);
        }

        internal static string Of(string id, IList<string> types)
        {
            if (!string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var c)) return c;
            return FromTypes(types);
        }

        private static readonly HashSet<string> Drink = Set("BREVAGE", "WATER", "WATER_RATION", "ALCOHOL");
        private static readonly HashSet<string> Food = Set("FOOD", "PROCESSED_FOOD", "TREAT", "FRESH_FOOD");
        private static readonly HashSet<string> Weapon = Set("WEAPON", "FIREARM", "MELEE_WEAPON", "AMMUNITION", "MUNITION", "GUN_MOD", "ARMOR");
        private static readonly HashSet<string> Med = Set("MEDICAL", "MEDICAL_TOOL", "ORGAN");
        private static readonly HashSet<string> Chem = Set("CHEMICAL_SUPPLIES", "NARCOTIC", "POISON", "SUBSTANCE");
        private static readonly HashSet<string> Module = Set("MODULE", "HYDROPONIC_MODULE", "PRINTER_MODULE", "EVAPORATOR_MODULE", "ALARM_MODULE", "NODE");
        private static readonly HashSet<string> Farm = Set("SEED", "MODIFIED_SEED", "FARMING_SUPPLIES");
        private static readonly HashSet<string> Paper = Set("DOCUMENT", "ACCESS_CARD", "LOTTERY");
        private static readonly HashSet<string> Lux = Set("ART", "LUXURY_ITEM");
        private static readonly HashSet<string> House = Set("HOUSEHOLD_GOOD", "WATER_PURIFICATION_SUPPLY", "FILTER");
        private static readonly HashSet<string> Mat = Set("MATERIAL", "BASIC_MATERIAL", "REFINED_MATERIAL", "JUNK");
        private static readonly HashSet<string> Tool = Set("TOOL", "ENERGY_CELL");

        private static HashSet<string> Set(params string[] a) => new HashSet<string>(a, StringComparer.Ordinal);

        private static int FirstIndex(IList<string> t, HashSet<string> g)
        {
            for (int i = 0; i < t.Count; i++) if (g.Contains(t[i])) return i;
            return 99;
        }

        /// <summary>只看类型的分类规则（与 tools/categories.py 里不依赖目录的部分一致）。</summary>
        internal static string FromTypes(IList<string> types)
        {
            var t = types ?? (IList<string>)Array.Empty<string>();
            bool Has(HashSet<string> g) => t.Any(g.Contains);
            if (Has(Drink)) return "drink";
            if (Has(Weapon) && FirstIndex(t, Weapon) <= FirstIndex(t, Tool)) return "weapon";
            if (FirstIndex(t, Med) == 0) return "medical";
            if (FirstIndex(t, Food) == 0) return "food";
            if (Has(Chem)) return "chemical";
            if (Has(Module)) return "module";
            if (Has(Farm)) return "farming";
            if (Has(Paper)) return "paper";
            if (Has(Lux) && FirstIndex(t, Lux) < FirstIndex(t, House)) return "luxury";
            if (Has(House)) return "household";
            if (Has(Mat)) return "material";
            if (Has(Tool)) return "tool";
            if (Has(Food)) return "food";
            if (Has(Med)) return "medical";
            return Other;
        }
    }
}
