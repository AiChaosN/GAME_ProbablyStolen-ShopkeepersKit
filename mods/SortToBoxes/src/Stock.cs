using System;
using System.Collections.Generic;
using System.Linq;

namespace SortToBoxes
{
    /// <summary>
    /// “库存”相关的判定：什么算箱子、什么货能搬、某个格子是不是在仓库里（货属于哪个大类见 Categories）。
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
                if (!allowNegotiated && ReferenceEquals(Game.Store?.GetCurrentlyNegociatedItem(), x)) return false;
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
            var found = Children(warehouse, isBox, isBox);
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
            return Children(warehouse, notFixture, isBox)
                .Where(it => IsMovable(it))
                .ToList();
        }

        /// <summary>
        /// 与游戏的 GraphUtils.FindChildrenType 语义相同（收集 root 下符合 include 的物品；物品只有 descend 为真时才往里找），
        /// 但会跳过空的子节点、读不了子节点的节点：游戏那个函数不检查空节点，遇到就抛空引用。
        /// </summary>
        internal static List<GameItem> Children(GraphNodeStorage root, Func<GameItem, bool> include = null, Func<GameItem, bool> descend = null)
        {
            var result = new List<GameItem>();
            if (root == null) return result;
            var queue = new Queue<GraphNodeStorage>();
            var visited = new HashSet<object>();
            queue.Enqueue(root);
            int guard = 0;
            while (queue.Count > 0 && guard++ < 50000)
            {
                var node = queue.Dequeue();
                if (node == null || !visited.Add(node)) continue;
                if (!ReferenceEquals(node, root) && node is GameItem gi)
                {
                    bool take = false, down = false;
                    try { take = include == null || include(gi); } catch { }
                    try { down = descend != null && descend(gi); } catch { }
                    if (take) result.Add(gi);
                    if (!down) continue;
                }
                List<GraphNodeStorage> children = null;
                try { children = node.children; } catch { }
                if (children == null) continue;
                foreach (var c in children)
                    if (c != null) queue.Enqueue(c);
            }
            return result;
        }

        // ---------------------------------------------------------------- “同款”与“不出售”

        // 水和水里的杂质：水质（纯净水……地沟水）就由这些决定，而杂质多少是隐藏信息，要检测才知道，所以不拿它们来区分
        private static readonly HashSet<string> WaterFamily = new HashSet<string>(StringComparer.Ordinal)
        {
            "water", "heavy_metal", "organic_waste", "microplastic", "microbe", "chemical_contaminant",
            "physical_contaminant", "mineral", "chlorine", "debris",
        };

        /// <summary>只读一个 tag 的整数值（读物品自己的 modifiedState，不会像 TagSystem.GetTag 那样顺手新建条目）。</summary>
        private static int ReadInt(GameItem it, string tag)
        {
            try
            {
                var dict = it?.modifiedState?.dict;
                if (dict != null && tag != null && dict.TryGetValue(tag, out var st) && st != null) return st.GetInt();
            }
            catch { }
            return 0;
        }

        /// <summary>瓶子里装的“别的液体”（水和杂质以外的：酒精、试剂、提取物……），按 id 排序；不是液体容器或没有时为空。</summary>
        internal static List<string> ProductLiquids(GameItem it)
        {
            var r = new List<string>();
            try
            {
                if (it == null || !it.IsTag("LIQUID_CONTAINER_TAG")) return r;
                foreach (var l in Liquid.Liquids)
                {
                    if (l == null || string.IsNullOrEmpty(l.identifier) || WaterFamily.Contains(l.identifier)) continue;
                    if (ReadInt(it, l.currentPart) > 0) r.Add(l.identifier);
                }
            }
            catch { }
            r.Sort(StringComparer.Ordinal);
            return r;
        }

        /// <summary>
        /// 物品提示里看得到的等级（水质、药品纯度、品相等带“等级”的特征）：检测过的显示真实等级，没检测过的显示表面等级，
        /// 和玩家在游戏里看到的一样，不会泄露没检测出来的信息。
        /// </summary>
        internal static List<string> VisibleGrades(GameItem it)
        {
            var r = new List<string>();
            try
            {
                var feats = it?.itemFeatures;
                if (feats == null) return r;
                foreach (var f in feats)
                {
                    if (f == null || !f.useCondition || f.isDisabled) continue;
                    string d = f.IsExposed() ? f.GetBaseRealDisplay() : f.GetBaseFakeDisplay();
                    if (!string.IsNullOrEmpty(d)) r.Add((f.category ?? "") + "=" + d);
                }
            }
            catch { }
            r.Sort(StringComparer.Ordinal);
            return r;
        }

        /// <summary>
        /// “同款”的判断依据：同一种物品，且类型标签、看得到的等级、瓶子里的“别的液体”都一样。
        /// 所以装酒精的瓶子和装水的瓶子不算同款，表面等级不同的水也不算。
        /// </summary>
        internal static string VariantKey(GameItem it)
        {
            if (it == null) return "";
            var types = new List<string>();
            try { if (it.itemTypes != null) types.AddRange(it.itemTypes.Where(t => !string.IsNullOrEmpty(t))); } catch { }
            types.Sort(StringComparer.Ordinal);
            return (it.identifier ?? "") + "|" + string.Join(",", types) + "|" + string.Join(",", ProductLiquids(it)) + "|" + string.Join(";", VisibleGrades(it));
        }

        /// <summary>
        /// 你自己调配的混合液：普通瓶子里装了水以外的液体（不是正规的化学成品、也不是酒瓶）。
        /// 游戏把装液体的瓶子都当“水”卖，这种瓶子摆上柜台会被买水的顾客按水价收走，所以 F4 不摆。
        /// </summary>
        internal static bool IsOwnMixture(GameItem it)
        {
            try
            {
                if (it == null || !it.IsTag("LIQUID_CONTAINER_TAG")) return false;
                if (it.IsTag("CHEMICAL_PRODUCT_TAG") || it.IsTag("WINE_CONTAINER_TAG")) return false;
                return ProductLiquids(it).Count > 0;
            }
            catch { return false; }
        }

        internal const string NoSellTag = "CHAOS5090_STB_NO_SELL";

        /// <summary>箱子是否设成了“不参与上柜台”（存在箱子自己的 tag 上，随存档保存）。</summary>
        internal static bool IsNoSellBox(GameItem box)
        {
            try
            {
                var dict = box?.state?.dict;
                return dict != null && dict.TryGetValue(NoSellTag, out var st) && st != null && st.IsEnabled();
            }
            catch { return false; }
        }

        internal static void SetNoSell(GameItem box, bool on)
        {
            if (box == null) return;
            try
            {
                if (on) box.EnableTag(NoSellTag);
                else box.DisableTag(NoSellTag);
            }
            catch (Exception e)
            {
                Log.Warn("保存“不参与上柜台”失败: " + e.Message);
            }
        }

        /// <summary>这件货是否在某个“不参与上柜台”的箱子里（含箱中箱）。</summary>
        internal static bool InNoSellBox(GameItem it)
        {
            try
            {
                var inv = it?.parentInventory;
                for (int depth = 0; depth < 8 && inv != null; depth++)
                {
                    var parent = inv.GetParentItem();
                    if (parent == null) return false;
                    if (IsBox(parent) && IsNoSellBox(parent)) return true;
                    inv = parent.parentInventory;
                }
            }
            catch { }
            return false;
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
    /// 箱子的“分类”标签：玩家在分类面板里勾选的大类（#类别）和单件物品（@物品id），
    /// 存成箱子自己的一个 tag（字符串值，逗号分隔），随存档保存。
    /// 卸载 mod 后这个 tag 留在存档里也不影响游戏。
    /// </summary>
    internal static class BoxLabels
    {
        internal const string Tag = "CHAOS5090_STB_BOX_TYPES";

        /// <summary>勾选单件物品时存的键：@物品id。</summary>
        internal static string ItemKey(string itemId) => "@" + itemId;

        /// <summary>勾选整个大类时存的键：#类别（例如 #food）。</summary>
        internal static string CatKey(string category) => "#" + category;

        internal static bool Accepts(HashSet<string> labels, GameItem it)
        {
            if (labels == null || it == null) return false;
            if (!string.IsNullOrEmpty(it.identifier) && labels.Contains(ItemKey(it.identifier))) return true;
            return labels.Contains(CatKey(Categories.Of(it)));
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
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var raw in s.Split(','))
                {
                    var x = raw.Trim();
                    if (x.Length == 0) continue;
                    // 1.2 存的是游戏的类型名（例如 FOOD）：换算成它所属的大类
                    if (x[0] != '@' && x[0] != '#') x = CatKey(Categories.FromTypes(new[] { x }));
                    set.Add(x);
                }
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
