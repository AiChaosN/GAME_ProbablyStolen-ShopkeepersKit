using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Conveniences
{
    /// <summary>
    /// 便利替换：把电池拖到机器上、把滤水器拖到水罐上（不用打开它们），直接换掉里面的旧件，旧件放回新件原来的位置。
    ///
    /// 原版的拖放（ItemMouseDragHandler）落在一件物品上时：
    ///   1. 这件物品里有空的、能放的格子 → 放进去（槽是空的时原版本来就能直接拖进去，本 mod 不管）；
    ///   2. 否则看“对物品使用”（GameItem.MayTarget / CanTarget / Target，例如电池拖到另一块电池上互换）。
    /// 槽里已经有旧件时，第 1 步找不到空位，原版就什么都不做（还标红）。本 mod 在第 2 步接手：
    /// 把这种情况算作“对机器使用电池”，Target 时换件。所有补丁只在
    /// “拖的是单件、自己的电池/滤芯，目标里有装着同类旧件、而且接受新件的槽，并且目标里没有空位可放”时才起作用。
    ///
    /// 换件优先用游戏自己的 GraphUtils.SwapItemsInPlace（原版电池互换用的就是它：新件进槽，旧件放到新件原来的格子）；
    /// 旧件形状不同放不回原格子时，退而把旧件放进新件原来所在的库存（任意空位），再把新件放进槽；放不下就不换，提示一下。
    /// </summary>
    internal static class Swap
    {
        internal enum Kind { None, Battery, Filter }

        private const string BatteryTag = "power_source_item";
        private const string FilterTag = "WATER_FILTER";
        private const string RechargeableMachineTag = "MACHINE_RECHARGABLE_TAG";

        private const string HarmonyId = "Conveniences.patches";
        private static Harmony harmony;
        internal static bool Applied { get; private set; }
        internal static bool HighlightApplied { get; private set; }

        // 正在自己判断/换件时为 true：期间不改游戏的返回值（判断里会调用被补丁的函数，避免绕回来）
        private static bool busy;

        private static readonly FieldInfo dragItemField = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

        // ------------------------------------------------------------------ 补丁

        internal static void Apply()
        {
            try { harmony = new Harmony(HarmonyId); }
            catch (Exception e)
            {
                Log.Error("Harmony 初始化失败，便利替换不可用: " + e.Message);
                return;
            }

            try
            {
                var may = AccessTools.Method(typeof(GameItem), "MayTarget", new[] { typeof(GameItem) });
                var can = AccessTools.Method(typeof(GameItem), "CanTarget", new[] { typeof(GameItem) });
                var target = AccessTools.Method(typeof(GameItem), "Target", new[] { typeof(GameItem) });
                if (may == null || can == null || target == null ||
                    may.DeclaringType != typeof(GameItem) || can.DeclaringType != typeof(GameItem) || target.DeclaringType != typeof(GameItem))
                {
                    Log.Warn("找不到 GameItem.MayTarget / CanTarget / Target（游戏更新了？），便利替换不可用");
                    return;
                }
                harmony.Patch(may, postfix: new HarmonyMethod(AccessTools.Method(typeof(Swap), nameof(MayTargetPostfix))));
                harmony.Patch(can, postfix: new HarmonyMethod(AccessTools.Method(typeof(Swap), nameof(CanTargetPostfix))));
                harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(Swap), nameof(TargetPrefix))));
                Applied = true;
            }
            catch (Exception e)
            {
                Log.Error("打补丁失败，便利替换不可用: " + Log.Describe(e));
                Remove();
                return;
            }

            // 只影响界面：拖着新件悬停在机器上时显示绿色和“把 X 用在 Y 上”，而不是红色的“放不进去”
            try
            {
                var slotMay = AccessTools.Method(typeof(GameSlotInventory), "MayHaveValidInventorySlot", new[] { typeof(GameItem) });
                if (slotMay != null && slotMay.DeclaringType == typeof(GameSlotInventory))
                {
                    harmony.Patch(slotMay, postfix: new HarmonyMethod(AccessTools.Method(typeof(Swap), nameof(SlotMayPostfix))));
                    HighlightApplied = true;
                }
                else Log.Warn("找不到 GameSlotInventory.MayHaveValidInventorySlot：换件照常可用，只是悬停时仍显示红色");
            }
            catch (Exception e)
            {
                Log.Warn("悬停提示补丁失败（换件照常可用）: " + e.Message);
            }
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            harmony = null;
            Applied = false;
            HighlightApplied = false;
        }

        private static void MayTargetPostfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (__result || busy) return;
            try { if (Find(__instance, targetItem, out _, out _)) __result = true; }
            catch (Exception e) { Log.Warn("判断能否换件出错: " + e.Message); }
        }

        private static void CanTargetPostfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (__result || busy) return;
            try { if (Find(__instance, targetItem, out _, out _)) __result = true; }
            catch (Exception e) { Log.Warn("判断能否换件出错: " + e.Message); }
        }

        /// <summary>
        /// 原版对这一对物品没有任何“使用”效果、而本 mod 的换件条件成立时：换件，并跳过原版（原版本来什么都不会做）。
        /// 原版有自己的使用效果时（例如电池拖到电池上），完全交给原版。
        /// </summary>
        private static bool TargetPrefix(GameItem __instance, GameItem targetItem, bool __runOriginal)
        {
            // 别的 mod 已经接手了这次使用（例如滤水器充能：装着高级滤水器时拖滤水器上去是充能）：什么都不做
            if (!__runOriginal || busy) return true;
            try
            {
                if (!Find(__instance, targetItem, out var slot, out var old)) return true;
                bool vanilla;
                busy = true;
                try { vanilla = __instance.MayTarget(targetItem) && __instance.CanTarget(targetItem); }
                finally { busy = false; }
                if (vanilla) return true;
                DoSwap(__instance, targetItem, slot, old);
                return false;
            }
            catch (Exception e)
            {
                busy = false;
                Log.Error("换件出错: " + Log.Describe(e));
                return true;
            }
        }

        /// <summary>
        /// 悬停显示：鼠标正拖着新件、指着的机器里这个槽装着旧件时，告诉原版“这个槽现在放不进”
        /// （本来就放不进，原版只是没查有没有被占），于是原版改走“对物品使用”的分支，显示绿色。
        /// 只在鼠标拖动这件物品时生效，不影响其他任何代码。
        /// </summary>
        private static void SlotMayPostfix(GameSlotInventory __instance, GameItem item, ref bool __result)
        {
            if (!__result || busy || item == null) return;
            try
            {
                if (KindOf(item) == Kind.None || !IsMouseDragging(item)) return;
                var owner = GraphUtils.FindParentType<GameItem>(__instance);
                if (owner == null) return;
                if (Find(item, owner, out var slot, out _) && ReferenceEquals(slot, __instance)) __result = false;
            }
            catch { }
        }

        // ------------------------------------------------------------------ 判断

        internal static Kind KindOf(GameItem item)
        {
            if (item == null) return Kind.None;
            if (item.IsTag(BatteryTag)) return Kind.Battery;
            if (item.IsTag(FilterTag)) return Kind.Filter;
            return Kind.None;
        }

        private static bool Enabled(Kind k)
        {
            var cfg = ConveniencesMod.Config;
            if (cfg == null) return false;
            return k == Kind.Battery ? cfg.Batteries : k == Kind.Filter && cfg.Filters;
        }

        /// <summary>
        /// 换件条件：src 是自己的单件电池/滤芯；target 是自己的物品，里面有一个槽装着同类旧件、而且接受 src；
        /// target 里没有空位能直接放 src（有空位就交给原版放进去）；src 不在 target 里面；不是多选一起拖。
        /// </summary>
        internal static bool Find(GameItem src, GameItem target, out GameSlotInventory slot, out GameItem old)
        {
            slot = null;
            old = null;
            if (src == null || target == null || ReferenceEquals(src, target)) return false;
            var kind = KindOf(src);
            if (kind == Kind.None || !Enabled(kind)) return false;
            if (target.contentWindow == null) return false;
            if (src.parentInventory == null || target.parentInventory == null) return false;
            if (!GeneralHelper.IsItemOwned(src) || !GeneralHelper.IsItemOwned(target)) return false;
            if (IsGroupDragging()) return false;

            busy = true;
            try
            {
                if (GraphUtils.FindParentEquals(src, target)) return false;      // 从这台机器自己的窗口里拖出来的
                foreach (var s in Slots(target, kind))
                {
                    var cur = s.childItem;
                    if (cur == null || ReferenceEquals(cur, src) || KindOf(cur) != kind) continue;
                    if (ReferenceEquals(src.parentInventory, s)) continue;
                    if (!s.MayHaveValidInventorySlot(src)) continue;               // 槽的规则（例如水罐不收紫外线灯）
                    // 机器里还有能直接放的空位：交给原版放进去
                    var free = target.TryFindOneValidInventorySlot(src);
                    if (free != null && free.numTransfer > 0) return false;
                    slot = s;
                    old = cur;
                    return true;
                }
                return false;
            }
            finally { busy = false; }
        }

        /// <summary>target 窗口第一层的槽（不进入里面装着的物品）。电池另外加上机器的正式电池槽。</summary>
        private static IEnumerable<GameSlotInventory> Slots(GameItem target, Kind kind)
        {
            var seen = new HashSet<GameSlotInventory>();
            if (kind == Kind.Battery && target.IsTag(RechargeableMachineTag))
            {
                GameSlotInventory battery = null;
                try { battery = MachineHelper.GetBatterySlot(target); } catch { }
                if (battery != null && seen.Add(battery)) yield return battery;
            }
            PixelElement el = null;
            try { el = target.contentWindow?.childElement; } catch { }
            if (el is GameSlotInventory single)
            {
                if (seen.Add(single)) yield return single;
            }
            else if (el is MachineryPanelRow row)
            {
                for (int i = 0; i < 64; i++)
                {
                    GraphNodeStorage st = null;
                    try { st = row.GetStorage(i); } catch { }
                    if (st == null) break;
                    if (st is GameSlotInventory s && seen.Add(s)) yield return s;
                }
            }
        }

        private static bool IsGroupDragging()
        {
            try
            {
                var multi = ItemMultiSelectHandler.current;
                return multi != null && multi.IsGroupDragging;
            }
            catch { return false; }
        }

        private static bool IsMouseDragging(GameItem item)
        {
            try
            {
                var drag = ItemMouseDragHandler.current;
                if (drag == null || !drag.IsDraggingItem || dragItemField == null) return false;
                return ReferenceEquals(dragItemField.GetValue(drag), item);
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------ 换件

        private static void DoSwap(GameItem src, GameItem target, GameSlotInventory slot, GameItem old)
        {
            string what = Lang.T(KindOf(src) == Kind.Battery ? "电池" : "滤水器", KindOf(src) == Kind.Battery ? "battery" : "filter");
            busy = true;
            try
            {
                // 1. 单件：用游戏自己的原地互换（新件进槽，旧件放到新件原来的格子、原来的朝向）
                if (src.unitCount == 1 && GraphUtils.SwapItemsInPlace(src, old) &&
                    ReferenceEquals(src.parentInventory, slot) && old.parentInventory != null)
                {
                    Log.Info("换件：" + Name(old) + " → " + Name(src) + "（" + Name(target) + "）");
                    return;
                }

                // 2. 退路：旧件放进新件原来所在的库存的任意空位，再从新件里拿一件放进槽
                var from = src.parentInventory;
                if (from == null || !ReferenceEquals(old.parentInventory, slot))
                {
                    Log.Warn("换件取消：物品位置已经变了");
                    return;
                }
                if (GraphUtils.TryAcceptAll(from, old) > 0 || !ReferenceEquals(old.parentInventory, from))
                {
                    if (old.parentInventory == null) GraphUtils.TryAcceptAll(slot, old);   // 保险：回到槽里
                    Notify(Lang.T("没有地方放换下来的旧" + what + "，没有换", "No room for the old " + what + "; nothing swapped"));
                    Log.Info("换件取消：" + Name(from) + " 放不下换下来的 " + Name(old));
                    return;
                }
                var marker = slot.TryFindOneValidInventorySlot(src);
                int moved = 0;
                if (marker != null && marker.numTransfer > 0) moved = marker.TryAcceptOnce(1);
                if (moved < 1 || slot.childItem == null || ReferenceEquals(slot.childItem, old))
                {
                    GraphUtils.TryAcceptAll(slot, old);   // 新件放不进槽：把旧件放回去
                    Notify(Lang.T("新" + what + "放不进去，没有换", "The new " + what + " doesn't fit; nothing swapped"));
                    Log.Info("换件取消：" + Name(src) + " 放不进 " + Name(target) + " 的槽");
                    return;
                }
                Log.Info("换件（旧件放到空位）：" + Name(old) + " → " + Name(slot.childItem) + "（" + Name(target) + "）");
            }
            finally { busy = false; }
        }

        private static void Notify(string msg)
        {
            try
            {
                if (StoreUIManager.Instance != null) StoreUIManager.Instance.Notify(msg);
                else Log.Info(msg);
            }
            catch { Log.Info(msg); }
        }

        internal static string Name(object node)
        {
            try
            {
                if (node is GameItem it)
                {
                    var n = it.GetDisplayName(true);
                    return string.IsNullOrEmpty(n) ? it.identifier ?? "?" : n;
                }
                return node?.GetType().Name ?? "?";
            }
            catch { return "?"; }
        }
    }
}
