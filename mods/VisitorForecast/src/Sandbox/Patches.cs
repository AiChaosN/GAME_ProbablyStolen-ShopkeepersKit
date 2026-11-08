using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace VisitorForecast.Sandbox
{
    /// <summary>
    /// 手动打补丁（不用 [HarmonyPatch] 特性）：某个目标在游戏更新后找不到时只禁用对应功能，
    /// 不会让整个 mod 加载失败。关键的“防副作用”补丁缺任何一个，预测功能就整体禁用。
    /// </summary>
    internal static class Patches
    {
        internal const string HarmonyId = "VisitorForecast.sandbox";
        private static Harmony harmony;

        internal static readonly List<string> Failed = new List<string>();
        internal static bool CriticalMissing;
        internal static bool RefreshCounterItemSkipped;
        internal static int Applied;

        private sealed class Spec
        {
            internal string Desc;
            internal bool Critical;
            internal MethodBase Target;
            internal HarmonyMethod Prefix, Postfix;
            internal Action OnSuccess;
        }

        // 所有查找都以字面量 typeof(...) 为接收者（游戏的 ModScanner 据此判断不需要 reflection-external 权限）
        private static HarmonyMethod Guard(string name)
        {
            var m = AccessTools.Method(typeof(SandboxGuard), name);
            if (m == null) throw new MissingMethodException(nameof(SandboxGuard), name);
            return new HarmonyMethod(m);
        }

        private static HarmonyMethod Tagger(string name)
        {
            var m = AccessTools.Method(typeof(SourceTagger), name);
            if (m == null) throw new MissingMethodException(nameof(SourceTagger), name);
            return new HarmonyMethod(m);
        }

        /// <summary>找到的方法必须是 owner 自己声明的（AccessTools 也会返回基类的同名方法）。</summary>
        private static MethodBase Own(MethodBase m, Type owner) => m != null && m.DeclaringType == owner ? m : null;

        private static MethodBase Find(Func<MethodBase> lookup)
        {
            try { return lookup(); }
            catch { return null; }
        }

        private static List<Spec> BuildSpecs()
        {
            var specs = new List<Spec>();

            void Add(bool critical, string desc, MethodBase target, HarmonyMethod pre, HarmonyMethod post = null, Action ok = null)
            {
                specs.Add(new Spec { Desc = desc, Critical = critical, Target = target, Prefix = pre, Postfix = post, OnSuccess = ok });
            }

            // ---- 关键：沙盒副作用拦截 -------------------------------------------------
            var skipBool = Guard(nameof(SandboxGuard.SkipBoolFalse));
            var skipVoid = Guard(nameof(SandboxGuard.SkipVoid));
            Add(true, "PlayerStore.TryAddDirectSellingItemToTable(string)", Own(Find(() => AccessTools.Method(typeof(PlayerStore), "TryAddDirectSellingItemToTable",
                new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(int) })), typeof(PlayerStore)), skipBool);
            Add(true, "PlayerStore.TryAddDirectSellingItemToTable(GameItem)", Own(Find(() => AccessTools.Method(typeof(PlayerStore), "TryAddDirectSellingItemToTable",
                new[] { typeof(GameItem), typeof(bool), typeof(bool), typeof(bool), typeof(int) })), typeof(PlayerStore)), skipBool);
            Add(true, "PlayerStore.AddDirectSellingItemToTableMiddle", Own(Find(() => AccessTools.Method(typeof(PlayerStore), "AddDirectSellingItemToTableMiddle",
                new[] { typeof(GameItem), typeof(bool) })), typeof(PlayerStore)), skipVoid);
            Add(true, "PlayerStore.AddDirectToWeightedTable", Own(Find(() => AccessTools.Method(typeof(PlayerStore), "AddDirectToWeightedTable",
                new[] { typeof(GameItem), typeof(bool) })), typeof(PlayerStore)), skipVoid);
            Add(true, "GameItemElement.Destroy", Own(Find(() => AccessTools.Method(typeof(GameItemElement), "Destroy", Type.EmptyTypes)), typeof(GameItemElement)),
                Guard(nameof(SandboxGuard.DestroyPrefix)));
            Add(true, "ReputationUIManager.NotifyReputation", Own(Find(() => AccessTools.Method(typeof(ReputationUIManager), "NotifyReputation",
                new[] { typeof(string), typeof(int) })), typeof(ReputationUIManager)), skipVoid);
            Add(true, "ReputationUIManager.Notify", Own(Find(() => AccessTools.Method(typeof(ReputationUIManager), "Notify",
                new[] { typeof(string), typeof(string) })), typeof(ReputationUIManager)), skipVoid);
            Add(true, "AchievementTracker.Unlock", Own(Find(() => AccessTools.Method(typeof(AchievementTracker), "Unlock",
                new[] { typeof(string) })), typeof(AchievementTracker)), skipVoid);
            Add(true, "AchievementTracker.IncrementCountAndUnlock", Own(Find(() => AccessTools.Method(typeof(AchievementTracker), "IncrementCountAndUnlock",
                new[] { typeof(string), typeof(int), typeof(string) })), typeof(AchievementTracker)), skipVoid);
            Add(true, "DialogUIManager.StartDialogue", Own(Find(() => AccessTools.Method(typeof(DialogUIManager), "StartDialogue",
                new[] { typeof(Dialogue), typeof(bool) })), typeof(DialogUIManager)), skipBool);
            Add(true, "PlantList.FreeNutrifruit", Own(Find(() => AccessTools.Method(typeof(PlantList), "FreeNutrifruit", Type.EmptyTypes)), typeof(PlantList)), skipVoid);
            Add(true, "StolenHelper.SetHeat", Own(Find(() => AccessTools.Method(typeof(StolenHelper), "SetHeat",
                new[] { typeof(GameItem), typeof(int) })), typeof(StolenHelper)), Guard(nameof(SandboxGuard.SetHeatPrefix)));

            // 跟踪新建物品/窗口（构造函数前缀：构造体执行前登记）
            Add(true, "GameItem..ctor", Find(() => AccessTools.Constructor(typeof(GameItem), new[] { typeof(string), typeof(PixelWindow) })),
                Guard(nameof(SandboxGuard.TrackItemPrefix)));
            Add(true, "PixelWindow..ctor", Find(() => AccessTools.Constructor(typeof(PixelWindow),
                new[] { typeof(int), typeof(int), typeof(bool), typeof(int), typeof(int), typeof(string), typeof(WindowStyle) })),
                Guard(nameof(SandboxGuard.TrackWindowPrefix)));

            // ---- 次要：已有物品的标签/特性保护（缺了只是少一道保险，安全网仍会检测） ----------
            Add(false, "GameItem.EnableTag", Own(Find(() => AccessTools.Method(typeof(GameItem), "EnableTag",
                new[] { typeof(string), typeof(bool) })), typeof(GameItem)), Guard(nameof(SandboxGuard.ItemTagBoolPrefix)));
            Add(false, "GameItem.DisableTag", Own(Find(() => AccessTools.Method(typeof(GameItem), "DisableTag",
                new[] { typeof(string), typeof(bool) })), typeof(GameItem)), Guard(nameof(SandboxGuard.ItemTagBoolPrefix)));
            Add(false, "GameItem.ModifyTag", Own(Find(() => AccessTools.Method(typeof(GameItem), "ModifyTag",
                new[] { typeof(string), typeof(Action<TagState>), typeof(bool) })), typeof(GameItem)), Guard(nameof(SandboxGuard.ItemModifyTagPrefix)));
            Add(false, "GameItem.AddItemFeature", Own(Find(() => AccessTools.Method(typeof(GameItem), "AddItemFeature",
                new[] { typeof(ItemFeature) })), typeof(GameItem)), Guard(nameof(SandboxGuard.ItemVoidPrefix)));

            // OnShutterOpened 末尾的 RefreshCounterItem 会改柜台物品；能跳过它，沙盒就直接调用 OnShutterOpened
            Add(false, "PlayerStore.RefreshCounterItem", Own(Find(() => AccessTools.Method(typeof(PlayerStore), "RefreshCounterItem", Type.EmptyTypes)), typeof(PlayerStore)),
                skipVoid, null, () => RefreshCounterItemSkipped = true);
            Add(false, "PlayerStore.LogQueuedClient", Own(Find(() => AccessTools.Method(typeof(PlayerStore), "LogQueuedClient", Type.EmptyTypes)), typeof(PlayerStore)), skipVoid);

            // 其他 mod 的生成钩子（默认在沙盒里不运行）
            var hookPre = Guard(nameof(SandboxGuard.ModHookPrefix));
            var hookPost = Guard(nameof(SandboxGuard.ModHookPostfix));
            foreach (var n in new[] { "FireOnGenerateCustomerVeryEarly", "FireOnGenerateCustomerEarly", "FireOnGenerateCustomerNormal",
                                      "FireOnGenerateCustomerLate", "FireOnGenerateCustomerVeryLate" })
            {
                var name = n;
                Add(false, "ModHook." + name, Own(Find(() => AccessTools.Method(typeof(ModHook), name, new[] { typeof(StoreClientManager) })), typeof(ModHook)), hookPre, hookPost);
            }
            Add(false, "ModHook.FireOnLootRolled", Own(Find(() => AccessTools.Method(typeof(ModHook), "FireOnLootRolled", new[] { typeof(LootRollContext) })), typeof(ModHook)),
                Guard(nameof(SandboxGuard.ModHookSkipOnlyPrefix)));

            // ---- 来源标签 ---------------------------------------------------------------
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var candidates = new List<MethodInfo>();
            try { candidates.AddRange(typeof(StoreClientManager).GetMethods(all)); } catch { }
            try { candidates.AddRange(typeof(LouBombHelper).GetMethods(all)); } catch { }
            try { candidates.AddRange(typeof(PlayerStore).GetMethods(all)); } catch { }
            var stepPre = Tagger(nameof(SourceTagger.StepPrefix));
            var stepPost = Tagger(nameof(SourceTagger.StepPostfix));
            foreach (var name in SourceTagger.Labels.Keys)
            {
                if (name.StartsWith("FireOn", StringComparison.Ordinal)) continue; // 上面已处理
                Type owner = typeof(StoreClientManager);
                if (name == "QueueVisit") owner = typeof(LouBombHelper);
                else if (name == "HandleFutureClientQueue") owner = typeof(PlayerStore);
                var found = false;
                foreach (var m in candidates)
                {
                    if (m.DeclaringType != owner || m.Name != name || m.IsAbstract || m.ContainsGenericParameters) continue;
                    Add(false, owner.Name + "." + name, m, stepPre, stepPost);
                    found = true;
                }
                if (!found) Add(false, owner.Name + "." + name, null, null);
            }
            Add(false, "StoreClientManager.PickClient", Own(Find(() => AccessTools.Method(typeof(StoreClientManager), "PickClient", Type.EmptyTypes)), typeof(StoreClientManager)),
                null, Tagger(nameof(SourceTagger.PickClientPostfix)));
            Add(false, "StoreClientManager.AddNextClient", Own(Find(() => AccessTools.Method(typeof(StoreClientManager), "AddNextClient",
                new[] { typeof(StoreClient) })), typeof(StoreClientManager)), Tagger(nameof(SourceTagger.AddNextClientPrefix)));
            return specs;
        }

        internal static void Apply()
        {
            harmony = new Harmony(HarmonyId);
            foreach (var s in BuildSpecs())
            {
                if (s.Target == null)
                {
                    Fail(s.Critical, s.Desc + " (未找到)");
                    continue;
                }
                try
                {
                    harmony.Patch(s.Target, s.Prefix, s.Postfix);
                    Applied++;
                    s.OnSuccess?.Invoke();
                }
                catch (Exception e)
                {
                    Fail(s.Critical, s.Desc + " (" + e.GetType().Name + ": " + e.Message + ")");
                }
            }
            if (Failed.Count > 0) Log.Warn("以下补丁目标未找到或打补丁失败: " + string.Join(", ", Failed));
            Log.Info("已应用 " + Applied + " 个补丁" + (CriticalMissing ? "；关键补丁缺失，预测功能已禁用" : ""));
        }

        /// <summary>只解析目标、不打补丁（离线自检用）。</summary>
        internal static List<string> DryRun()
        {
            var lines = new List<string>();
            foreach (var s in BuildSpecs())
                lines.Add((s.Target == null ? (s.Critical ? "MISSING-CRITICAL " : "MISSING ") : "ok ") + s.Desc +
                          (s.Target != null ? " :: " + s.Target : ""));
            return lines;
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(harmony.Id); }
            catch (Exception e) { Log.Warn("移除补丁失败: " + e.Message); }
        }

        private static void Fail(bool critical, string what)
        {
            Failed.Add(what);
            if (critical) CriticalMissing = true;
        }
    }
}
