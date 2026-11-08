using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace VisitorForecast.Sandbox
{
    /// <summary>
    /// 沙盒运行期间的拦截与跟踪：
    /// - 拦截所有把东西放到柜台/改动已有物品/弹 UI/解锁成就的调用；
    /// - 跟踪沙盒里新建的物品与窗口（它们都会实例化 GameObject），每轮结束后销毁。
    /// 所有补丁在 Active == false 时立即放行，对正常游戏没有影响。
    /// </summary>
    internal static class SandboxGuard
    {
        internal static bool Active;
        internal static bool IncludeOtherMods;

        private static readonly HashSet<object> created = new HashSet<object>(RefComparer.Instance);
        private static readonly List<GameItem> createdItems = new List<GameItem>();
        private static readonly List<PixelWindow> createdWindows = new List<PixelWindow>();
        internal static readonly Dictionary<string, int> Suppressed = new Dictionary<string, int>();
        internal static int DestroyedObjects;
        internal static int MovedExisting; // 渲染根下出现了“沙盒前就存在”的物品/窗口节点（不应发生）

        private static readonly HashSet<Transform> rootChildrenBefore = new HashSet<Transform>();
        private static readonly HashSet<Transform> alreadyDestroyed = new HashSet<Transform>();
        private static readonly List<Transform> roots = new List<Transform>();

        internal static bool IsCreated(object o) => o != null && created.Contains(o);

        internal static void Count(MethodBase m)
        {
            string key = m == null ? "?" : m.DeclaringType?.Name + "." + m.Name;
            Suppressed.TryGetValue(key, out int c);
            Suppressed[key] = c + 1;
        }

        /// <summary>预测开始前调用：记下渲染根节点下已有的子物体。</summary>
        internal static void BeginForecast()
        {
            Suppressed.Clear();
            DestroyedObjects = 0;
            MovedExisting = 0;
            created.Clear();
            createdItems.Clear();
            createdWindows.Clear();
            rootChildrenBefore.Clear();
            alreadyDestroyed.Clear();
            roots.Clear();
            try
            {
                var rh = RenderHandler.current;
                if (rh != null)
                {
                    if (rh.tempRoot != null) roots.Add(rh.tempRoot.transform);
                    if (rh.windowRoot != null) roots.Add(rh.windowRoot.transform);
                    if (rh.overlayRoot != null) roots.Add(rh.overlayRoot.transform);
                }
                foreach (var r in roots)
                    for (int i = 0; i < r.childCount; i++) rootChildrenBefore.Add(r.GetChild(i));
            }
            catch (Exception e)
            {
                Log.Warn("记录渲染根节点失败: " + e.Message);
            }
        }

        /// <summary>每轮沙盒结束时调用（仍处于 Active 状态）：销毁本轮新建的物品、窗口和渲染节点。</summary>
        internal static void DestroyCreatedThisRun()
        {
            foreach (var item in createdItems)
            {
                try
                {
                    if (item is GameItemElement gie && !gie.IsDestroyed())
                    {
                        var h = gie.handler;
                        gie.Destroy(); // 已在 created 集合里，DestroyPrefix 会放行
                        if (h != null) alreadyDestroyed.Add(h.transform);
                        DestroyedObjects++;
                    }
                }
                catch { }
            }
            foreach (var w in createdWindows)
            {
                try
                {
                    var h = w.handler;
                    if (h != null && !alreadyDestroyed.Contains(h.transform))
                    {
                        alreadyDestroyed.Add(h.transform);
                        UnityEngine.Object.Destroy(h.gameObject);
                        DestroyedObjects++;
                    }
                }
                catch { }
            }
            // 兜底：渲染根下本轮新出现的子物体（例如没挂到窗口上的界面元素）也是沙盒产物。
            // 但如果它属于一件沙盒之前就存在的物品/窗口（说明有代码把已有物品挪了位置），绝不销毁，只记下来。
            foreach (var r in roots)
            {
                try
                {
                    if (r == null) continue;
                    for (int i = 0; i < r.childCount; i++)
                    {
                        var c = r.GetChild(i);
                        if (rootChildrenBefore.Contains(c) || alreadyDestroyed.Contains(c)) continue;
                        alreadyDestroyed.Add(c);
                        var tnr = c.GetComponent<TreeNodeRender>();
                        if (tnr != null)
                        {
                            if (tnr.element is GameItem gi && !IsCreated(gi)) { MovedExisting++; continue; }
                            if (tnr.window != null && !IsCreated(tnr.window)) { MovedExisting++; continue; }
                        }
                        UnityEngine.Object.Destroy(c.gameObject);
                        DestroyedObjects++;
                    }
                }
                catch { }
            }
            createdItems.Clear();
            createdWindows.Clear();
            created.Clear();
        }

        internal static void EndForecast()
        {
            created.Clear();
            createdItems.Clear();
            createdWindows.Clear();
            rootChildrenBefore.Clear();
            alreadyDestroyed.Clear();
            roots.Clear();
        }

        // ================================================================ Harmony 补丁方法

        /// <summary>void 方法：沙盒中直接跳过。</summary>
        public static bool SkipVoid(MethodBase __originalMethod)
        {
            if (!Active) return true;
            Count(__originalMethod);
            return false;
        }

        /// <summary>bool 方法：沙盒中跳过并返回 false。</summary>
        public static bool SkipBoolFalse(MethodBase __originalMethod, ref bool __result)
        {
            if (!Active) return true;
            Count(__originalMethod);
            __result = false;
            return false;
        }

        /// <summary>GameItemElement.Destroy：沙盒中只允许销毁沙盒自己新建的物品。</summary>
        public static bool DestroyPrefix(GameItem __instance, MethodBase __originalMethod)
        {
            if (!Active || IsCreated(__instance)) return true;
            Count(__originalMethod);
            return false;
        }

        /// <summary>GameItem.EnableTag/DisableTag：沙盒中不许改动已有物品。</summary>
        public static bool ItemTagBoolPrefix(GameItem __instance, MethodBase __originalMethod, ref bool __result)
        {
            if (!Active || IsCreated(__instance)) return true;
            Count(__originalMethod);
            __result = false;
            return false;
        }

        /// <summary>GameItem.ModifyTag：沙盒中不许改动已有物品。</summary>
        public static bool ItemModifyTagPrefix(GameItem __instance, MethodBase __originalMethod, ref GameItem __result)
        {
            if (!Active || IsCreated(__instance)) return true;
            Count(__originalMethod);
            __result = __instance;
            return false;
        }

        /// <summary>GameItem.AddItemFeature 等 void 实例方法。</summary>
        public static bool ItemVoidPrefix(GameItem __instance, MethodBase __originalMethod)
        {
            if (!Active || IsCreated(__instance)) return true;
            Count(__originalMethod);
            return false;
        }

        /// <summary>StolenHelper.SetHeat(GameItem item, int heat)。</summary>
        public static bool SetHeatPrefix(GameItem item, MethodBase __originalMethod)
        {
            if (!Active || IsCreated(item)) return true;
            Count(__originalMethod);
            return false;
        }

        /// <summary>GameItem 构造函数前缀：记录沙盒中新建的物品（构造体执行之前就登记，后续改标签不被拦）。</summary>
        public static void TrackItemPrefix(GameItem __instance)
        {
            if (!Active || __instance == null) return;
            if (created.Add(__instance)) createdItems.Add(__instance);
        }

        public static void TrackWindowPrefix(PixelWindow __instance)
        {
            if (!Active || __instance == null) return;
            if (created.Add(__instance)) createdWindows.Add(__instance);
        }

        /// <summary>ModHook.FireOnGenerateCustomer*/FireOnLootRolled：默认不让其他 mod 在沙盒里跑。</summary>
        public static bool ModHookPrefix(MethodBase __originalMethod)
        {
            if (!Active) return true;
            if (!IncludeOtherMods)
            {
                Count(__originalMethod);
                return false;
            }
            SourceTagger.Push(__originalMethod);
            return true;
        }

        /// <summary>只跳过、不打来源标签（用于 FireOnLootRolled 这类与顾客无关的钩子）。</summary>
        public static bool ModHookSkipOnlyPrefix(MethodBase __originalMethod)
        {
            if (!Active || IncludeOtherMods) return true;
            Count(__originalMethod);
            return false;
        }

        public static void ModHookPostfix(MethodBase __originalMethod)
        {
            if (!Active || !IncludeOtherMods) return;
            SourceTagger.Pop(__originalMethod);
        }
    }
}
