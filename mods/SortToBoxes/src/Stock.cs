using System;
using System.Collections.Generic;
using System.Linq;

namespace SortToBoxes
{
    /// <summary>
    /// “库存”相关的判定：什么算箱子、什么货能搬、货的主类型（第一个标签）、某个格子是不是在仓库里。
    /// 库存 = 仓库（EmporiumEntry.invElement）最外层 + 仓库里所有箱子（含箱中箱）里的东西。
    /// </summary>
    internal static class Stock
    {
        // 表示档次/改装性的类型，不代表“这是哪一类货”；找主类型时跳过
        private static readonly HashSet<string> NonCategoryTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "MODDABLE", "MAKESHIFT", "MIDLINE", "LOWTECH",
        };

        internal static GameGridInventory Warehouse => EmporiumEntry.Instance?.invElement;
        internal static GameGridInventory Counter => EmporiumEntry.Instance?.backInvinvElement;

        /// <summary>箱子 = 属于你的“储藏”类物品（储藏区、工具箱、机器区…）和背包。垃圾桶、档案夹、种子包、上锁的物资箱、机器都不算。</summary>
        internal static bool IsBox(GameItem it)
        {
            try
            {
                if (it == null || it.contentWindow == null) return false;
                if (!GeneralHelper.IsItemOwned(it)) return false;
                if (it.IsGameItemType("MACHINE") || it.IsGameItemType("SUPPLY_CRATE") || it.IsTag("LOCKED_CONTAINER_TAG")) return false;
                return it.IsGameItemType("STORAGE") || it.IsTag("BACKPACK_TAG");
            }
            catch { return false; }
        }

        /// <summary>容器、机器、在仓库里工作的设施：从不搬，也不拿来判断箱子装哪类货。</summary>
        internal static bool IsFixture(GameItem it)
        {
            try
            {
                if (IsBox(it)) return true;
                if (it.IsTag("CONTAINER_TAG") || it.IsTag("BACKPACK_TAG") || it.IsTag("SYSTEM_TAG")) return true;
                return it.IsGameItemType("MACHINE");
            }
            catch { return true; }
        }

        /// <summary>
        /// 这件东西能不能被本 mod 搬动。
        /// allowNegotiated：正在议价的那件是否也能搬（把柜台清空时可以）。
        /// </summary>
        internal static bool IsMovable(GameItem x, bool allowNegotiated = false)
        {
            try
            {
                if (x == null || x.parentInventory == null) return false;
                if (x is GameItemElement el && el.IsDestroyed()) return false;
                if (!GeneralHelper.IsItemOwned(x)) return false;
                if (ItemPinHelper.IsPinned(x) || DebrisHelper.IsDebris(x)) return false;
                if (IsFixture(x)) return false;
                if (!allowNegotiated && ReferenceEquals(PlayerStore.Instance?.GetCurrentlyNegociatedItem(), x)) return false;
                return x.MayRemove();
            }
            catch { return false; }
        }

        /// <summary>主类型 = 物品提示里第一个 [标签]（跳过“可改装/简易”这类档次标签）。没有类型时返回 null。</summary>
        internal static string PrimaryType(GameItem it)
        {
            try
            {
                var types = it?.itemTypes;
                if (types == null) return null;
                foreach (var t in types)
                    if (!string.IsNullOrEmpty(t) && !NonCategoryTypes.Contains(t)) return t;
            }
            catch { }
            return null;
        }

        /// <summary>inv 是不是仓库本身，或仓库里某个箱子（含箱中箱）的格子。</summary>
        internal static bool IsStockInventory(GameInventory inv)
        {
            var warehouse = Warehouse;
            if (inv == null || warehouse == null) return false;
            try
            {
                for (int depth = 0; depth < 8 && inv != null; depth++)
                {
                    if (ReferenceEquals(inv, warehouse)) return true;
                    var parent = inv.GetParentItem();
                    if (parent == null || !IsBox(parent)) return false;
                    inv = parent.parentInventory;
                }
            }
            catch { }
            return false;
        }

        /// <summary>仓库里的所有箱子（含箱中箱），按唯一 id 排序。</summary>
        internal static List<GameItem> FindBoxes()
        {
            var warehouse = Warehouse;
            if (warehouse == null) return new List<GameItem>();
            Func<GameItem, bool> isBox = IsBox;
            var found = GraphUtils.FindChildrenType<GameItem>(warehouse, isBox, isBox);
            found.Sort((a, b) => Uid(a).CompareTo(Uid(b)));
            return found;
        }

        /// <summary>库存里所有能搬的货（仓库最外层 + 箱子里），不含箱子/机器等设施。</summary>
        internal static List<GameItem> AllGoods()
        {
            var warehouse = Warehouse;
            if (warehouse == null) return new List<GameItem>();
            Func<GameItem, bool> notFixture = it => it != null && !IsFixture(it);
            Func<GameItem, bool> isBox = IsBox;
            return GraphUtils.FindChildrenType<GameItem>(warehouse, notFixture, isBox)
                .Where(it => IsMovable(it))
                .ToList();
        }

        internal static int Uid(GameItem it)
        {
            try { return it.GetUniqueID(); }
            catch { return 0; }
        }

        internal static string Name(GameItem it)
        {
            try
            {
                var n = it.GetDisplayName(true);
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            return it?.identifier ?? "?";
        }

        private static readonly Dictionary<string, string> typeNames = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>类型的中文显示名（与物品提示里的 [标签] 相同）。</summary>
        internal static string TypeName(string type)
        {
            if (string.IsNullOrEmpty(type)) return "无类型";
            if (typeNames.TryGetValue(type, out var s)) return s;
            string name = type;
            try
            {
                var loc = TypeHelper.GetTypeDisplayName(type);
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("type_", StringComparison.OrdinalIgnoreCase)) name = loc;
            }
            catch { }
            typeNames[type] = name;
            return name;
        }
    }

    /// <summary>
    /// 箱子的“分类”标签：玩家在分类面板里勾选的类型，存成箱子自己的一个 tag（字符串值，逗号分隔），随存档保存。
    /// 卸载 mod 后这个 tag 留在存档里也不影响游戏。
    /// </summary>
    internal static class BoxLabels
    {
        internal const string Tag = "CHAOS5090_STB_BOX_TYPES";

        /// <summary>勾选单件物品时存的键：@物品id（类型直接存类型名，例如 FOOD）。</summary>
        internal static string ItemKey(string itemId) => "@" + itemId;

        internal static bool Accepts(HashSet<string> labels, GameItem it)
        {
            if (labels == null || it == null) return false;
            if (!string.IsNullOrEmpty(it.identifier) && labels.Contains(ItemKey(it.identifier))) return true;
            var p = Stock.PrimaryType(it);
            return p != null && labels.Contains(p);
        }

        /// <summary>勾选的类型；没设置时返回 null（= 按箱里现有物品判断）。</summary>
        internal static HashSet<string> Get(GameItem box)
        {
            try
            {
                // 直接读箱子自己存的 state（随存档保存的那份），不依赖派生的 modifiedState 是否已刷新
                var dict = box?.state?.dict;
                if (dict == null || !dict.TryGetValue(Tag, out var st) || st == null || !st.IsEnabled()) return null;
                var s = st.GetString();
                if (string.IsNullOrEmpty(s)) return null;
                var set = new HashSet<string>(s.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0), StringComparer.Ordinal);
                return set.Count > 0 ? set : null;
            }
            catch { return null; }
        }

        internal static void Set(GameItem box, IEnumerable<string> types)
        {
            if (box == null) return;
            var list = (types ?? Enumerable.Empty<string>()).Where(t => !string.IsNullOrEmpty(t)).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            try
            {
                if (list.Count == 0)
                {
                    box.ModifyTag(Tag, st => st.SetString(""), skipValidate: true);
                    box.DisableTag(Tag);
                }
                else
                {
                    string csv = string.Join(",", list);
                    box.ModifyTag(Tag, st => st.Enable().SetString(csv));
                }
            }
            catch (Exception e)
            {
                Log.Warn("保存箱子分类失败: " + e.Message);
            }
        }
    }
}
