using System;
using System.Collections.Generic;
using System.Reflection;

namespace VisitorForecast.Sandbox
{
    /// <summary>
    /// 游戏类型的字段表。表本身由 tools/RegistryGen 在构建时生成（src/Generated/GameTypeRegistry.cs）：
    /// 每个类型一行 <c>R(typeof(X), typeof(X).GetFields(I), typeof(X).GetFields(S), root)</c>，
    /// 所有字段查询的接收者都是字面量 typeof(游戏类型)，运行时只按 obj.GetType() 查表，不对计算出来的类型做成员查询。
    /// 不在表里的类型（游戏的私有嵌套类型、编译器生成的闭包等）按“不透明引用”处理。
    /// </summary>
    internal static partial class GameTypeRegistry
    {
        internal const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        internal const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal sealed class Entry
        {
            internal Type Type;
            internal FieldInfo[] Declared;   // 本类型声明的实例字段
            internal FieldInfo[] Static;     // 本类型声明的静态字段
            internal bool Root;              // 静态字段是否作为快照的根
        }

        private static Dictionary<Type, Entry> map;
        private static Dictionary<Type, CollOps> colls;
        internal static int FailedChunks;
        internal static string FirstFailure;

        internal static int Count => map?.Count ?? 0;

        internal static void EnsureBuilt()
        {
            if (map != null) return;
            map = new Dictionary<Type, Entry>();
            colls = new Dictionary<Type, CollOps>();
            // 每块单独 try：游戏更新后某个类型不存在了，JIT 那一块会失败，只丢那一块
            foreach (var chunk in Chunks)
            {
                try { chunk(); }
                catch (Exception e)
                {
                    FailedChunks++;
                    if (FirstFailure == null) FirstFailure = e.GetType().Name + ": " + e.Message;
                }
            }
            try { Colls(); }
            catch (Exception e)
            {
                FailedChunks++;
                if (FirstFailure == null) FirstFailure = e.GetType().Name + ": " + e.Message;
            }
        }

        private static void R(Type t, FieldInfo[] instanceFields, FieldInfo[] staticFields, bool root)
        {
            if (t == null) return;
            map[t] = new Entry { Type = t, Declared = instanceFields ?? new FieldInfo[0], Static = staticFields ?? new FieldInfo[0], Root = root };
        }

        private static void Coll(CollOps ops) => colls[ops.Target] = ops;

        internal static bool TryGet(Type t, out Entry e)
        {
            EnsureBuilt();
            return map.TryGetValue(t, out e);
        }

        internal static IEnumerable<Entry> Entries
        {
            get
            {
                EnsureBuilt();
                return map.Values;
            }
        }

        internal static CollOps CollOpsFor(Type t)
        {
            EnsureBuilt();
            return colls.TryGetValue(t, out var ops) ? ops : null;
        }

        /// <summary>
        /// 游戏程序集里有、但表里没有的可见类型（游戏更新后才会出现）。
        /// typeof(游戏类型).Assembly.GetTypes() 是扫描器认可的写法（接收者是字面量 typeof 的 Assembly）。
        /// </summary>
        internal static List<string> UnregisteredVisibleTypes()
        {
            EnsureBuilt();
            var list = new List<string>();
            Type[] types;
            try
            {
                types = typeof(PlayerStore).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }
            foreach (var t in types)
            {
                if (t == null || !t.IsVisible || t.IsInterface || t.IsEnum || t.IsGenericTypeDefinition) continue;
                if (typeof(Delegate).IsAssignableFrom(t)) continue;
                if (map.ContainsKey(t)) continue;
                var outer = t;
                while (outer.DeclaringType != null) outer = outer.DeclaringType;
                if (IsGuardName(outer.Name)) continue; // 加载器保护类型本来就不在表里
                list.Add(t.FullName);
            }
            return list;
        }

        private static bool IsGuardName(string n) =>
            n == "ModLoader" || n == "ModTrust" || n == "ModTrustRecord" || n == "ModFingerprint" || n == "ModTrustList" ||
            n == "ModScanner" || n == "ModCapability" || n == "ModSecurityDebug" || n == "ModSteamRules" || n == "ModSteamGuard" ||
            n == "ModWorkshop" || n == "SteamManager";
    }

    // ====================================================================== 集合还原器

    /// <summary>
    /// 没有非泛型增删接口的集合（HashSet/Queue/Stack/…）的强类型还原器；具体的 T 由生成器按游戏字段里出现的类型生成。
    /// 快照一律通过 IEnumerable 读取（枚举顺序），还原时尽量只做必要的改动。
    /// </summary>
    internal abstract class CollOps
    {
        internal abstract Type Target { get; }

        /// <summary>把集合内容还原成 items（枚举顺序）。返回是否发生了写入。</summary>
        internal abstract bool Restore(object target, object[] items);

        protected static bool SameSequence<T>(IEnumerable<T> seq, object[] items)
        {
            int i = 0;
            foreach (var x in seq)
            {
                if (i >= items.Length || !StateGraph.Same(x, items[i])) return false;
                i++;
            }
            return i == items.Length;
        }
    }

    internal sealed class SetOps<T> : CollOps
    {
        internal override Type Target => typeof(HashSet<T>);

        internal override bool Restore(object target, object[] items)
        {
            var set = (HashSet<T>)target;
            if (SameSequence(set, items)) return false;
            // 先只删掉沙盒里新加的元素（倒序删除，空出的槽位按原顺序复用），内容和顺序对上就不必重建
            var want = new HashSet<T>(set.Comparer);
            foreach (var x in items) want.Add((T)x);
            var cur = new List<T>(set);
            for (int i = cur.Count - 1; i >= 0; i--)
                if (!want.Contains(cur[i])) set.Remove(cur[i]);
            if (SameSequence(set, items)) return true;
            set.Clear();
            foreach (var x in items) set.Add((T)x);
            return true;
        }
    }

    internal sealed class SortedSetOps<T> : CollOps
    {
        internal override Type Target => typeof(SortedSet<T>);

        internal override bool Restore(object target, object[] items)
        {
            var set = (SortedSet<T>)target;
            if (SameSequence(set, items)) return false;
            set.Clear();
            foreach (var x in items) set.Add((T)x);
            return true;
        }
    }

    internal sealed class QueueOps<T> : CollOps
    {
        internal override Type Target => typeof(Queue<T>);

        internal override bool Restore(object target, object[] items)
        {
            var q = (Queue<T>)target;
            if (SameSequence(q, items)) return false;
            q.Clear();
            foreach (var x in items) q.Enqueue((T)x);
            return true;
        }
    }

    internal sealed class StackOps<T> : CollOps
    {
        internal override Type Target => typeof(Stack<T>);

        internal override bool Restore(object target, object[] items)
        {
            var s = (Stack<T>)target;
            if (SameSequence(s, items)) return false;
            s.Clear();
            // 枚举顺序是出栈顺序，要倒着压回去
            for (int i = items.Length - 1; i >= 0; i--) s.Push((T)items[i]);
            return true;
        }
    }

    internal sealed class LinkedListOps<T> : CollOps
    {
        internal override Type Target => typeof(LinkedList<T>);

        internal override bool Restore(object target, object[] items)
        {
            var l = (LinkedList<T>)target;
            if (SameSequence(l, items)) return false;
            l.Clear();
            foreach (var x in items) l.AddLast((T)x);
            return true;
        }
    }
}
