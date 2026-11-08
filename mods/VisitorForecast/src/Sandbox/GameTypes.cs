using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VisitorForecast.Sandbox
{
    internal sealed class StaticRoot
    {
        internal Type Type;
        internal FieldInfo[] Fields;
    }

    /// <summary>
    /// Assembly-CSharp 的类型信息：静态根、实例字段、哪些类型属于“物品/界面图”。
    /// 字段一律来自构建时生成的类型表（GameTypeRegistry），不在运行时对任意类型做成员查询。
    /// </summary>
    internal static class GameTypes
    {
        internal static readonly Assembly GameAsm = typeof(PlayerStore).Assembly;

        private static List<StaticRoot> staticRoots;
        private static readonly Dictionary<Type, FieldInfo[]> instanceFields = new Dictionary<Type, FieldInfo[]>();

        internal static bool IsGame(Type t) => t != null && t.Assembly == GameAsm;

        /// <summary>
        /// 游戏里“物品—背包—窗口”这张 UI 图上的对象（GameItem、GameInventory、PixelWindow……）。
        /// 它们挂着大量 Unity 对象，不进入快照；改动由 WorldFingerprint 单独检测，
        /// 沙盒里新建的由 SandboxGuard 跟踪并销毁。
        /// </summary>
        internal static bool IsWorldGraphType(Type t) =>
            typeof(GameItem).IsAssignableFrom(t) ||
            typeof(GraphNodeStorage).IsAssignableFrom(t) ||
            typeof(PixelElement).IsAssignableFrom(t) ||
            typeof(GraphNodeWindow).IsAssignableFrom(t);

        /// <summary>类型表里标成“根”的类型的静态字段（排除常量、指针、ThreadStatic）。</summary>
        internal static List<StaticRoot> StaticRoots
        {
            get
            {
                if (staticRoots != null) return staticRoots;
                var list = new List<StaticRoot>();
                foreach (var e in GameTypeRegistry.Entries)
                {
                    if (!e.Root || e.Static.Length == 0) continue;
                    try
                    {
                        var fields = e.Static
                            .Where(f => !f.IsLiteral && !f.FieldType.IsPointer && !f.IsDefined(typeof(ThreadStaticAttribute), false))
                            .ToArray();
                        if (fields.Length > 0) list.Add(new StaticRoot { Type = e.Type, Fields = fields });
                    }
                    catch
                    {
                        // 个别类型出错就跳过
                    }
                }
                // 固定顺序（字典枚举顺序与生成顺序一致，这里再按名字排一次，保证前后两次抓取一致）
                list.Sort((a, b) => string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
                staticRoots = list;
                return list;
            }
        }

        /// <summary>
        /// 只取 Assembly-CSharp 里声明的实例字段（不碰 MonoBehaviour/System 基类的内部字段）。
        /// 继承链上任何一个游戏类型不在类型表里时返回 null（该对象按不透明引用处理）。
        /// </summary>
        internal static FieldInfo[] InstanceFields(Type t)
        {
            if (instanceFields.TryGetValue(t, out var cached)) return cached;
            var list = new List<FieldInfo>();
            bool complete = true;
            for (var cur = t; cur != null && IsGame(cur); cur = cur.BaseType)
            {
                if (!GameTypeRegistry.TryGet(cur, out var e))
                {
                    complete = false;
                    break;
                }
                foreach (var f in e.Declared)
                {
                    if (f.IsLiteral || f.FieldType.IsPointer) continue;
                    list.Add(f);
                }
            }
            var arr = complete ? list.ToArray() : null;
            instanceFields[t] = arr;
            return arr;
        }

        /// <summary>
        /// 读取一次所有静态字段，触发尚未运行的类型初始化器。这样第一次预测的“前/后”快照
        /// 不会因为某个类型恰好在中途被初始化而产生假差异。返回读取失败的类型数。
        /// </summary>
        internal static int WarmUp()
        {
            GameTypeRegistry.EnsureBuilt();
            int failed = 0;
            foreach (var root in StaticRoots)
            {
                foreach (var f in root.Fields)
                {
                    try { f.GetValue(null); }
                    catch { failed++; break; }
                }
            }
            return failed;
        }
    }
}
