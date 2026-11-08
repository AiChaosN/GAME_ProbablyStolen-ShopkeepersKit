using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace VisitorForecast.Sandbox
{
    internal sealed class RefComparer : IEqualityComparer<object>
    {
        internal static readonly RefComparer Instance = new RefComparer();
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object o) => o == null ? 0 : RuntimeHelpers.GetHashCode(o);
    }

    internal enum NodeKind : byte { Static, Object, Array, List, Dict, Coll }

    internal sealed class Node
    {
        internal int Index;
        internal NodeKind Kind;
        internal object Target;          // Static 节点为 null
        internal Type StaticType;        // 仅 Static 节点
        internal FieldInfo[] Fields;     // Static / Object
        internal object[] Values;        // Static / Object 的字段值；Dict 的 value
        internal object[] Items;         // Array / List / Coll
        internal object[] Keys;          // Dict
        internal CollOps Ops;            // Coll（null = 没有该集合类型的还原器：只记录、不能还原，有变化时由安全网报出）
        internal int Parent = -1;
        internal int ParentSlot = -1;
        internal string Via;             // 经由 struct 字段到达时的子路径
        internal object Key => (object)StaticType ?? Target;
    }

    internal struct RandomSlot
    {
        internal Node Node;
        internal int Field;
    }

    /// <summary>
    /// 游戏状态图的浅快照：从 Assembly-CSharp 类型的静态字段出发，广度优先遍历可达的
    /// “纯 C# 数据对象”（Assembly-CSharp 中定义的 class/struct）以及其中的集合（List/Dictionary/HashSet/数组…），
    /// 每个对象只记录它自己的字段值（引用照原样存，不深拷贝）。
    /// 恢复时把每个节点的字段/集合内容写回同一个实例——因为所有可达节点都各自恢复了，
    /// 整个图就回到了快照时刻；沙盒里新建的对象自然变得不可达。
    /// 字段来自构建时生成的类型表（GameTypeRegistry）；集合只通过公开接口（IList/IDictionary/IEnumerable）
    /// 和强类型还原器读写，不碰 System/Unity 类型的内部字段。
    /// 不进入：UnityEngine.Object（除非直接挂在静态字段上的游戏单例，只记录游戏声明的字段）、
    /// GameItem/背包/窗口等 UI 图（GraphNodeStorage/PixelElement）、委托、System.Random（单独处理）、
    /// 类型表里没有的游戏类型（私有嵌套类型等）以及其他程序集里的非集合对象（按引用当作叶子）。
    /// </summary>
    internal sealed class StateGraph
    {
        internal static readonly object Unreadable = new object();

        internal readonly List<Node> Nodes = new List<Node>(4096);
        internal readonly Dictionary<object, Node> Map = new Dictionary<object, Node>(RefComparer.Instance);
        internal readonly List<RandomSlot> RandomSlots = new List<RandomSlot>();
        internal int SlotCount;
        internal int SkippedRootTypes;

        private readonly Queue<Node> pending = new Queue<Node>();
        private const int MaxNodes = 3000000;

        // ------------------------------------------------------------------ capture

        internal static StateGraph Capture()
        {
            var g = new StateGraph();
            foreach (var root in GameTypes.StaticRoots)
            {
                var values = new object[root.Fields.Length];
                bool ok = true;
                for (int i = 0; i < root.Fields.Length; i++)
                {
                    try { values[i] = root.Fields[i].GetValue(null); }
                    catch { ok = false; break; }
                }
                if (!ok) { g.SkippedRootTypes++; continue; }
                var node = new Node { Kind = NodeKind.Static, StaticType = root.Type, Fields = root.Fields, Values = values };
                g.Add(node);
                g.SlotCount += values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    g.NoteRandom(node, i);
                    g.Visit(values[i], node.Index, i, null, true, 0);
                }
            }
            while (g.pending.Count > 0)
            {
                if (g.Nodes.Count > MaxNodes) throw new InvalidOperationException("state graph too large (" + g.Nodes.Count + " nodes)");
                g.Fill(g.pending.Dequeue());
            }
            return g;
        }

        private void Add(Node n)
        {
            n.Index = Nodes.Count;
            Nodes.Add(n);
            Map[n.Key] = n;
        }

        private void NoteRandom(Node node, int field)
        {
            if (node.Values[field] is Random && !node.Fields[field].IsInitOnly)
                RandomSlots.Add(new RandomSlot { Node = node, Field = field });
        }

        private void Visit(object v, int parent, int slot, string via, bool fromStaticField, int depth)
        {
            if (v == null) return;
            var meta = TypeMeta.Of(v.GetType());
            if (meta.IsValueType)
            {
                if (depth > 3) return;
                if (meta.StructRefFields.Length > 0)
                {
                    foreach (var f in meta.StructRefFields)
                    {
                        object fv;
                        try { fv = f.GetValue(v); } catch { continue; }
                        Visit(fv, parent, slot, (via ?? "") + "." + f.Name, false, depth + 1);
                    }
                }
                else if (meta.IsTuple && v is ITuple tuple)
                {
                    // ValueTuple 通过公开的 ITuple 接口读取各项
                    for (int i = 0; i < tuple.Length; i++)
                        Visit(tuple[i], parent, slot, (via ?? "") + ".Item" + (i + 1), false, depth + 1);
                }
                return;
            }
            if (meta.Kind == null) return;
            if (meta.IsUnityObject)
            {
                if (!fromStaticField) return;
                var uo = v as UnityEngine.Object;
                if (uo == null) return; // 已销毁的 Unity 对象
            }
            if (Map.ContainsKey(v)) return;
            var kind = meta.Kind.Value;
            if (kind == NodeKind.List && (((IList)v).IsReadOnly || ((IList)v).IsFixedSize)) return;
            if (kind == NodeKind.Dict && (((IDictionary)v).IsReadOnly || ((IDictionary)v).IsFixedSize)) return;
            var n = new Node { Kind = kind, Target = v, Fields = meta.Fields, Ops = meta.Ops, Parent = parent, ParentSlot = slot, Via = via };
            Add(n);
            pending.Enqueue(n);
        }

        private void Fill(Node n)
        {
            switch (n.Kind)
            {
                case NodeKind.Object:
                {
                    var vals = new object[n.Fields.Length];
                    for (int i = 0; i < vals.Length; i++)
                    {
                        try { vals[i] = n.Fields[i].GetValue(n.Target); }
                        catch { vals[i] = Unreadable; }
                    }
                    n.Values = vals;
                    SlotCount += vals.Length;
                    for (int i = 0; i < vals.Length; i++)
                    {
                        if (ReferenceEquals(vals[i], Unreadable)) continue;
                        NoteRandom(n, i);
                        Visit(vals[i], n.Index, i, null, false, 0);
                    }
                    break;
                }
                case NodeKind.Array:
                case NodeKind.List:
                {
                    var list = (IList)n.Target;
                    int c = list.Count;
                    var items = new object[c];
                    for (int i = 0; i < c; i++) items[i] = list[i];
                    n.Items = items;
                    SlotCount += c;
                    for (int i = 0; i < c; i++) Visit(items[i], n.Index, i, null, false, 0);
                    break;
                }
                case NodeKind.Dict:
                {
                    var d = (IDictionary)n.Target;
                    var keys = new object[d.Count];
                    var vals = new object[d.Count];
                    int i = 0;
                    var e = d.GetEnumerator();
                    while (e.MoveNext() && i < keys.Length)
                    {
                        keys[i] = e.Key;
                        vals[i] = e.Value;
                        i++;
                    }
                    n.Keys = keys;
                    n.Values = vals;
                    SlotCount += 2 * i;
                    for (int k = 0; k < i; k++)
                    {
                        Visit(keys[k], n.Index, 2 * k, null, false, 0);
                        Visit(vals[k], n.Index, 2 * k + 1, null, false, 0);
                    }
                    break;
                }
                case NodeKind.Coll:
                {
                    var tmp = new List<object>();
                    foreach (var x in (IEnumerable)n.Target) tmp.Add(x);
                    n.Items = tmp.ToArray();
                    SlotCount += n.Items.Length;
                    for (int i = 0; i < n.Items.Length; i++) Visit(n.Items[i], n.Index, i, null, false, 0);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ restore

        internal static bool Same(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            var t = a.GetType();
            if (t != b.GetType()) return false;
            if (t.IsValueType || a is string) return a.Equals(b);
            return false;
        }

        /// <summary>把节点写回快照内容。返回是否发生了写入。</summary>
        internal static bool RestoreNode(Node n)
        {
            bool changed = false;
            switch (n.Kind)
            {
                case NodeKind.Static:
                case NodeKind.Object:
                {
                    object target = n.Target;
                    if (n.Kind == NodeKind.Object && target is UnityEngine.Object uo && uo == null) return false;
                    var fields = n.Fields;
                    var vals = n.Values;
                    for (int i = 0; i < fields.Length; i++)
                    {
                        var saved = vals[i];
                        if (ReferenceEquals(saved, Unreadable)) continue;
                        var f = fields[i];
                        object cur = f.GetValue(target);
                        if (!Same(cur, saved))
                        {
                            f.SetValue(target, saved);
                            changed = true;
                        }
                    }
                    break;
                }
                case NodeKind.Array:
                {
                    var list = (IList)n.Target;
                    var items = n.Items;
                    int c = Math.Min(list.Count, items.Length);
                    for (int i = 0; i < c; i++)
                    {
                        if (!Same(list[i], items[i])) { list[i] = items[i]; changed = true; }
                    }
                    break;
                }
                case NodeKind.List:
                    changed = RestoreList((IList)n.Target, n.Items);
                    break;
                case NodeKind.Dict:
                    changed = RestoreDict((IDictionary)n.Target, n.Keys, n.Values);
                    break;
                case NodeKind.Coll:
                {
                    if (n.Ops != null)
                    {
                        changed = n.Ops.Restore(n.Target, n.Items);
                        break;
                    }
                    int i = 0;
                    bool differs = false;
                    foreach (var x in (IEnumerable)n.Target)
                    {
                        if (i >= n.Items.Length || !Same(x, n.Items[i])) { differs = true; break; }
                        i++;
                    }
                    if (differs || i != n.Items.Length)
                        throw new InvalidOperationException("没有 " + n.Target.GetType().Name + " 的还原器，无法写回");
                    break;
                }
            }
            return changed;
        }

        /// <summary>
        /// List：只改有变化的位置，多了从尾部删、少了从尾部加（不 Clear，不改变容量）。内容与顺序完全还原。
        /// </summary>
        private static bool RestoreList(IList list, object[] items)
        {
            bool changed = false;
            int n = items.Length;
            int common = Math.Min(list.Count, n);
            for (int i = 0; i < common; i++)
            {
                if (!Same(list[i], items[i])) { list[i] = items[i]; changed = true; }
            }
            while (list.Count > n) { list.RemoveAt(list.Count - 1); changed = true; }
            for (int i = list.Count; i < n; i++) { list.Add(items[i]); changed = true; }
            return changed;
        }

        /// <summary>
        /// Dictionary：键和顺序都没变时只用索引器改值；沙盒只新增了键时，倒序删掉这些键（空出的槽位以后按原顺序复用）；
        /// 其他情况（有键被删、顺序变了）清空后按快照顺序重新加入。枚举内容与顺序总是完全还原。
        /// </summary>
        private static bool RestoreDict(IDictionary d, object[] keys, object[] vals)
        {
            if (d.Count == keys.Length && SameKeyOrder(d, keys))
                return SetValues(d, keys, vals);

            var want = new HashSet<object>();
            foreach (var k in keys) want.Add(k);
            var current = new List<object>(d.Count);
            foreach (var k in d.Keys) current.Add(k);
            for (int i = current.Count - 1; i >= 0; i--)
                if (!want.Contains(current[i])) d.Remove(current[i]);

            if (d.Count == keys.Length && SameKeyOrder(d, keys))
            {
                SetValues(d, keys, vals);
                return true;
            }
            d.Clear();
            for (int i = 0; i < keys.Length; i++) d.Add(keys[i], vals[i]);
            return true;
        }

        private static bool SameKeyOrder(IDictionary d, object[] keys)
        {
            int i = 0;
            foreach (var k in d.Keys)
            {
                if (i >= keys.Length || !Same(k, keys[i])) return false;
                i++;
            }
            return i == keys.Length;
        }

        private static bool SetValues(IDictionary d, object[] keys, object[] vals)
        {
            bool changed = false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!Same(d[keys[i]], vals[i])) { d[keys[i]] = vals[i]; changed = true; }
            }
            return changed;
        }

        /// <summary>全量恢复（检查所有节点），dirty 收集发生写入的节点下标。</summary>
        internal int RestoreErrors;
        internal string FirstRestoreError;

        internal int RestoreAll(HashSet<int> dirty)
        {
            int writes = 0;
            for (int i = 0; i < Nodes.Count; i++) writes += RestoreOne(i, dirty);
            return writes;
        }

        internal int RestoreSubset(List<int> indices, HashSet<int> dirty)
        {
            int writes = 0;
            for (int k = 0; k < indices.Count; k++) writes += RestoreOne(indices[k], dirty);
            return writes;
        }

        private int RestoreOne(int i, HashSet<int> dirty)
        {
            try
            {
                if (!RestoreNode(Nodes[i])) return 0;
                dirty?.Add(i);
                return 1;
            }
            catch (Exception e)
            {
                // 单个节点写回失败不能中断其余节点的恢复；安全网随后会报告差异
                RestoreErrors++;
                if (FirstRestoreError == null) FirstRestoreError = PathOf(Nodes[i]) + ": " + e.GetType().Name + ": " + e.Message;
                return 0;
            }
        }

        // ------------------------------------------------------------------ random swap

        /// <summary>
        /// 把可写字段里的 System.Random 换成本轮的新实例（同一个原实例映射到同一个替身）。
        /// 原实例的内部状态因此从未被沙盒消耗；恢复时字段被写回原实例。
        /// </summary>
        internal void SwapRandoms(int seed)
        {
            if (RandomSlots.Count == 0) return;
            var map = new Dictionary<object, Random>(RefComparer.Instance);
            int k = 0;
            foreach (var s in RandomSlots)
            {
                var orig = s.Node.Values[s.Field];
                if (!map.TryGetValue(orig, out var r))
                {
                    r = new Random(unchecked(seed * 31 + 17 * ++k));
                    map[orig] = r;
                }
                try { s.Node.Fields[s.Field].SetValue(s.Node.Target, r); } catch { }
            }
        }

        // ------------------------------------------------------------------ verification

        /// <summary>
        /// 安全网：把 before（预测开始前的快照）与 after（全部结束后重新抓取的快照）逐节点逐槽对比。
        /// 两者都是对“同一批静态根”的独立遍历，after 不复用 before 的任何数据。
        /// </summary>
        internal static List<string> Compare(StateGraph before, StateGraph after, int max)
        {
            var diffs = new List<string>();
            foreach (var a in before.Nodes)
            {
                if (diffs.Count >= max) break;
                if (!after.Map.TryGetValue(a.Key, out var b))
                {
                    diffs.Add("不再可达: " + before.PathOf(a));
                    continue;
                }
                CompareNode(before, a, b, diffs, max);
            }
            if (diffs.Count < max)
            {
                foreach (var b in after.Nodes)
                {
                    if (diffs.Count >= max) break;
                    if (!before.Map.ContainsKey(b.Key)) diffs.Add("新增可达: " + after.PathOf(b));
                }
            }
            return diffs;
        }

        private static void CompareNode(StateGraph g, Node a, Node b, List<string> diffs, int max)
        {
            switch (a.Kind)
            {
                case NodeKind.Static:
                case NodeKind.Object:
                    for (int i = 0; i < a.Values.Length && diffs.Count < max; i++)
                    {
                        if (ReferenceEquals(a.Values[i], Unreadable) || ReferenceEquals(b.Values[i], Unreadable)) continue;
                        if (!Same(a.Values[i], b.Values[i]))
                            diffs.Add(g.PathOf(a) + "." + a.Fields[i].Name + ": " + Describe(a.Values[i]) + " -> " + Describe(b.Values[i]));
                    }
                    break;
                case NodeKind.Array:
                case NodeKind.List:
                case NodeKind.Coll:
                    if (a.Items.Length != b.Items.Length)
                    {
                        diffs.Add(g.PathOf(a) + ": Count " + a.Items.Length + " -> " + b.Items.Length);
                        break;
                    }
                    for (int i = 0; i < a.Items.Length && diffs.Count < max; i++)
                        if (!Same(a.Items[i], b.Items[i]))
                            diffs.Add(g.PathOf(a) + "[" + i + "]: " + Describe(a.Items[i]) + " -> " + Describe(b.Items[i]));
                    break;
                case NodeKind.Dict:
                    if (a.Keys.Length != b.Keys.Length)
                    {
                        diffs.Add(g.PathOf(a) + ": Count " + a.Keys.Length + " -> " + b.Keys.Length);
                        break;
                    }
                    for (int i = 0; i < a.Keys.Length && diffs.Count < max; i++)
                    {
                        if (!Same(a.Keys[i], b.Keys[i]))
                            diffs.Add(g.PathOf(a) + " key#" + i + ": " + Describe(a.Keys[i]) + " -> " + Describe(b.Keys[i]));
                        else if (!Same(a.Values[i], b.Values[i]))
                            diffs.Add(g.PathOf(a) + "[" + Describe(a.Keys[i]) + "]: " + Describe(a.Values[i]) + " -> " + Describe(b.Values[i]));
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ paths

        internal string PathOf(Node n)
        {
            var parts = new List<string>();
            int guard = 0;
            while (n != null && guard++ < 40)
            {
                if (n.Kind == NodeKind.Static)
                {
                    parts.Add(n.StaticType.FullName);
                    break;
                }
                if (n.Parent < 0) { parts.Add("?"); break; }
                var p = Nodes[n.Parent];
                parts.Add(SlotName(p, n.ParentSlot) + (n.Via ?? ""));
                n = p;
            }
            parts.Reverse();
            var sb = new StringBuilder();
            foreach (var s in parts)
            {
                if (sb.Length > 0 && !s.StartsWith("[", StringComparison.Ordinal)) sb.Append('.');
                sb.Append(s);
            }
            return sb.ToString();
        }

        private static string SlotName(Node p, int slot)
        {
            switch (p.Kind)
            {
                case NodeKind.Static:
                case NodeKind.Object:
                    return slot >= 0 && slot < p.Fields.Length ? p.Fields[slot].Name : "?";
                case NodeKind.Dict:
                    int e = slot / 2;
                    if (e >= 0 && p.Keys != null && e < p.Keys.Length)
                        return (slot % 2 == 0 ? "[key " : "[") + Describe(p.Keys[e]) + "]";
                    return "[?]";
                default:
                    return "[" + slot + "]";
            }
        }

        internal static string Describe(object v)
        {
            if (v == null) return "null";
            if (ReferenceEquals(v, Unreadable)) return "<unreadable>";
            if (v is string s) return "\"" + (s.Length > 60 ? s.Substring(0, 60) + "…(" + s.Length + ")" : s) + "\"";
            var t = v.GetType();
            if (t.IsPrimitive || t.IsEnum || v is decimal)
                return Convert.ToString(v, CultureInfo.InvariantCulture);
            if (t.IsValueType)
            {
                var str = v.ToString();
                return str.Length > 80 ? str.Substring(0, 80) + "…" : str;
            }
            return t.Name + "#" + RuntimeHelpers.GetHashCode(v).ToString("x");
        }
    }

    /// <summary>每个运行时类型的分类缓存（只用 Type 的公开属性和类型表，不做成员查询）。</summary>
    internal sealed class TypeMeta
    {
        internal NodeKind? Kind;
        internal bool IsValueType;
        internal bool IsUnityObject;
        internal bool IsTuple;
        internal FieldInfo[] Fields = new FieldInfo[0];
        internal FieldInfo[] StructRefFields = new FieldInfo[0];
        internal CollOps Ops;

        private static readonly Dictionary<Type, TypeMeta> cache = new Dictionary<Type, TypeMeta>();

        internal static TypeMeta Of(Type t)
        {
            if (cache.TryGetValue(t, out var m)) return m;
            m = Build(t);
            cache[t] = m;
            return m;
        }

        private static bool IsScalar(Type t) =>
            t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t.IsPointer ||
            t == typeof(IntPtr) || t == typeof(UIntPtr);

        private static bool IsSetLike(Type def) =>
            def == typeof(HashSet<>) || def == typeof(SortedSet<>) || def == typeof(Queue<>) || def == typeof(Stack<>) || def == typeof(LinkedList<>);

        private static TypeMeta Build(Type t)
        {
            var m = new TypeMeta();
            if (IsScalar(t)) { m.IsValueType = t.IsValueType; return m; }
            if (t.IsValueType)
            {
                m.IsValueType = true;
                if (GameTypes.IsGame(t))
                {
                    // 游戏的 struct：字段来自类型表，只保留可能含引用的字段
                    var fields = GameTypes.InstanceFields(t);
                    if (fields != null)
                    {
                        var list = new List<FieldInfo>();
                        foreach (var f in fields)
                            if (!IsScalar(f.FieldType)) list.Add(f);
                        m.StructRefFields = list.ToArray();
                    }
                }
                else
                {
                    m.IsTuple = typeof(ITuple).IsAssignableFrom(t);
                }
                return m;
            }
            if (typeof(Delegate).IsAssignableFrom(t)) return m;
            if (typeof(Random).IsAssignableFrom(t)) return m;
            if (typeof(MemberInfo).IsAssignableFrom(t)) return m;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                if (GameTypes.IsGame(t))
                {
                    var fields = GameTypes.InstanceFields(t);
                    if (fields != null)
                    {
                        m.IsUnityObject = true;
                        m.Kind = NodeKind.Object;
                        m.Fields = fields;
                    }
                }
                return m;
            }
            if (GameTypes.IsWorldGraphType(t)) return m;
            if (t.IsArray)
            {
                if (t.GetArrayRank() == 1) m.Kind = NodeKind.Array;
                return m;
            }
            if (typeof(IDictionary).IsAssignableFrom(t)) { m.Kind = NodeKind.Dict; return m; }
            if (typeof(IList).IsAssignableFrom(t)) { m.Kind = NodeKind.List; return m; }
            if (t.IsGenericType && IsSetLike(t.GetGenericTypeDefinition()))
            {
                // 内容照样记录（安全网能看到变化）；有生成的强类型还原器时才能写回
                m.Kind = NodeKind.Coll;
                m.Ops = GameTypeRegistry.CollOpsFor(t);
                return m;
            }
            if (GameTypes.IsGame(t))
            {
                var fields = GameTypes.InstanceFields(t);
                if (fields != null)
                {
                    m.Kind = NodeKind.Object;
                    m.Fields = fields;
                }
            }
            return m;
        }
    }
}
