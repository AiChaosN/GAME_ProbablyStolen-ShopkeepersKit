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

        // ---------------------------------------------------------------- 箱子的“栏”

        /// <summary>
        /// 储藏区用了扩容套件后会分成多栏（游戏的 StorageExpansionHelper：最多 5 栏，每栏是一个独立的格子库存，双击栏名可改名）。
        /// 本 mod 把每一栏当成一个独立的归箱目标：分类、“不参与归箱”、“不参与上柜台”都按栏设置。没升级的箱子只有一栏。
        /// 栏的界面是 MachineryTabs，挂在箱子窗口里角色为 "storage" 的面板上（与游戏自己找栏的方式相同）。
        /// </summary>
        internal static MachineryTabs Tabs(GameItem box)
        {
            try { return (box?.contentWindow?.childElement as MachineryPanelRow)?.GetPanel("storage")?.content as MachineryTabs; }
            catch { return null; }
        }

        /// <summary>箱子的各栏，第 i 项 = 第 i+1 栏。读不到的栏留 null，保证下标与游戏的栏号一致。</summary>
        internal static List<GameInventory> Compartments(GameItem box)
        {
            var r = new List<GameInventory>();
            try
            {
                var tabs = Tabs(box);
                if (tabs != null && tabs.count > 0)
                {
                    for (int i = 0; i < tabs.count; i++)
                    {
                        GameInventory inv = null;
                        try { inv = tabs.GetTabInventory(i); } catch { }
                        r.Add(inv);
                    }
                    return r;
                }
                var w = box?.contentWindow;
                if (w != null)
                    foreach (var c in w.children)
                        if (c is GameInventory inv) r.Add(inv);
            }
            catch { }
            return r;
        }

        internal static int CompartmentCount(GameItem box) => Math.Max(1, Compartments(box).Count);

        /// <summary>inv 是箱子的第几栏（从 0 起）；不是它的栏时返回 -1。</summary>
        internal static int CompartmentIndex(GameItem box, GameInventory inv)
        {
            if (box == null || inv == null) return -1;
            var list = Compartments(box);
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], inv)) return i;
            return -1;
        }

        /// <summary>箱子窗口当前显示的是第几栏（从 0 起）。</summary>
        internal static int SelectedCompartment(GameItem box)
        {
            try
            {
                var tabs = Tabs(box);
                if (tabs != null && tabs.selected >= 0 && tabs.selected < tabs.count) return tabs.selected;
            }
            catch { }
            return 0;
        }

        /// <summary>栏名：和箱子窗口里栏头上显示的一样（玩家改过名就是改后的名字）。</summary>
        internal static string CompartmentName(GameItem box, int index)
        {
            try
            {
                var c = Tabs(box)?.GetCaption(index);
                if (!string.IsNullOrWhiteSpace(c)) return c.Trim();
            }
            catch { }
            return Lang.T("第" + (index + 1) + "栏", "Tab " + (index + 1));
        }

        /// <summary>日志和提示里用的名字：只有一栏时 = 箱子名，多栏时 = 箱子名·栏名。</summary>
        internal static string TargetName(GameItem box, int index, int count)
        {
            string n = Name(box);
            return count > 1 ? n + "·" + CompartmentName(box, index) : n;
        }

        /// <summary>某件货所在的箱子和栏（直接装着它的那个箱子）；不在箱子里时 box 为 null。</summary>
        internal static bool Container(GameItem it, out GameItem box, out int index)
        {
            box = null;
            index = -1;
            try
            {
                var inv = it?.parentInventory;
                var parent = inv?.GetParentItem();
                if (parent == null || !IsBox(parent)) return false;
                box = parent;
                index = CompartmentIndex(parent, inv);
                return true;
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------- 按栏的开关：“不参与上柜台”“不参与归箱”

        /// <summary>
        /// 一个按栏的开关，存成箱子自己的两个 tag（随存档保存，卸载 mod 后留在存档里也不影响游戏）：
        ///   AllTag（开/关）：整个箱子都开着，包括以后再扩出来的栏；
        ///   TabsTag（字符串）：只有部分栏开着时，记开着的栏号（从 1 起，逗号分隔）。
        /// “不参与上柜台”的 AllTag 就是 1.5 起的 CHAOS5090_STB_NO_SELL，旧存档里的设置照旧对整个箱子生效。
        /// </summary>
        internal sealed class TabFlag
        {
            internal readonly string AllTag;
            internal readonly string TabsTag;
            internal TabFlag(string allTag, string tabsTag) { AllTag = allTag; TabsTag = tabsTag; }

            internal bool AllOn(GameItem box)
            {
                try
                {
                    var dict = box?.state?.dict;
                    return dict != null && dict.TryGetValue(AllTag, out var st) && st != null && st.IsEnabled();
                }
                catch { return false; }
            }

            private HashSet<int> TabSet(GameItem box)
            {
                var set = new HashSet<int>();
                try
                {
                    var dict = box?.state?.dict;
                    if (dict == null || !dict.TryGetValue(TabsTag, out var st) || st == null || !st.IsEnabled()) return set;
                    foreach (var raw in (st.GetString() ?? "").Split(','))
                        if (int.TryParse(raw.Trim(), out int n) && n >= 1) set.Add(n);
                }
                catch { }
                return set;
            }

            /// <summary>第 index 栏（从 0 起）是否开着。index &lt; 0 表示不知道是哪一栏：只看整个箱子的开关。</summary>
            internal bool IsOn(GameItem box, int index)
            {
                if (box == null) return false;
                if (AllOn(box)) return true;
                return index >= 0 && TabSet(box).Contains(index + 1);
            }

            internal void Set(GameItem box, int index, bool on)
            {
                if (box == null || index < 0) return;
                try
                {
                    int count = CompartmentCount(box);
                    var set = AllOn(box) ? new HashSet<int>(Enumerable.Range(1, count)) : TabSet(box);
                    if (on) set.Add(index + 1);
                    else set.Remove(index + 1);
                    set.RemoveWhere(n => n > count);
                    bool all = set.Count >= count;   // 每一栏都开着 = 整个箱子开着（以后再扩出来的栏也算）
                    if (all) box.EnableTag(AllTag);
                    else box.DisableTag(AllTag);
                    if (all || set.Count == 0)
                    {
                        var dict = box.state?.dict;
                        if (dict != null && dict.ContainsKey(TabsTag))   // 没存过就不去建一个空条目
                        {
                            box.ModifyTag(TabsTag, st => st.SetString(""), skipValidate: true);
                            box.DisableTag(TabsTag);
                        }
                    }
                    else
                    {
                        string csv = string.Join(",", set.OrderBy(n => n));
                        box.ModifyTag(TabsTag, st => st.Enable().SetString(csv));
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("保存箱子开关 " + AllTag + " 失败: " + e.Message);
                }
            }
        }

        /// <summary>“不参与上柜台”：上柜台键和“带上同款”都不拿这一栏里的东西。</summary>
        internal static readonly TabFlag NoSell = new TabFlag("CHAOS5090_STB_NO_SELL", "CHAOS5090_STB_NO_SELL_TABS");

        /// <summary>“不参与归箱”：归箱、买下自动归箱、柜台收回都不往这一栏放东西，这一栏里的东西也不拿来判断它装哪类货。</summary>
        internal static readonly TabFlag NoSort = new TabFlag("CHAOS5090_STB_NO_SORT", "CHAOS5090_STB_NO_SORT_TABS");

        /// <summary>这件货是否在某个“不参与上柜台”的栏里（含箱中箱：外层箱子那一栏设了也算）。</summary>
        internal static bool InNoSellBox(GameItem it)
        {
            try
            {
                var inv = it?.parentInventory;
                for (int depth = 0; depth < 8 && inv != null; depth++)
                {
                    var parent = inv.GetParentItem();
                    if (parent == null) return false;
                    if (IsBox(parent) && NoSell.IsOn(parent, CompartmentIndex(parent, inv))) return true;
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
    /// 箱子每一栏的“分类”标签：玩家在分类面板里勾选的大类（#类别）和单件物品（@物品id），
    /// 存成箱子自己的 tag（字符串值，逗号分隔），随存档保存：第 1 栏是 CHAOS5090_STB_BOX_TYPES（和 1.7 以前整个箱子的那个相同——
    /// 以前往升级过的箱子里归箱，游戏也只会放进第 1 栏，所以旧设置原样对应第 1 栏），第 N 栏（N ≥ 2）是 CHAOS5090_STB_BOX_TYPES_N。
    /// 卸载 mod 后这些 tag 留在存档里也不影响游戏。
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

        /// <summary>第 index 栏（从 0 起）的分类存在哪个 tag 里。</summary>
        internal static string TagOf(int index) => index <= 0 ? Tag : Tag + "_" + (index + 1);

        /// <summary>箱子所有栏勾选的并集（箱子角标用）；都没设置时返回 null。</summary>
        internal static HashSet<string> Union(GameItem box)
        {
            HashSet<string> all = null;
            int count = Stock.CompartmentCount(box);
            for (int i = 0; i < count; i++)
            {
                if (Stock.NoSort.IsOn(box, i)) continue;
                var one = Get(box, i);
                if (one == null) continue;
                if (all == null) all = new HashSet<string>(StringComparer.Ordinal);
                all.UnionWith(one);
            }
            return all;
        }

        /// <summary>第 index 栏（从 0 起）勾选的类型；没设置时返回 null（= 按这一栏里现有物品判断）。</summary>
        internal static HashSet<string> Get(GameItem box, int index = 0)
        {
            try
            {
                // 直接读箱子自己存的 state（随存档保存的那份），不依赖派生的 modifiedState 是否已刷新
                var dict = box?.state?.dict;
                if (dict == null || !dict.TryGetValue(TagOf(index), out var st) || st == null || !st.IsEnabled()) return null;
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

        internal static void Set(GameItem box, int index, IEnumerable<string> types)
        {
            if (box == null || index < 0) return;
            string tag = TagOf(index);
            var list = (types ?? Enumerable.Empty<string>()).Where(t => !string.IsNullOrEmpty(t)).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            try
            {
                if (list.Count == 0)
                {
                    box.ModifyTag(tag, st => st.SetString(""), skipValidate: true);
                    box.DisableTag(tag);
                }
                else
                {
                    string csv = string.Join(",", list);
                    box.ModifyTag(tag, st => st.Enable().SetString(csv));
                }
            }
            catch (Exception e)
            {
                Log.Warn("保存箱子分类失败: " + e.Message);
            }
        }
    }
}
