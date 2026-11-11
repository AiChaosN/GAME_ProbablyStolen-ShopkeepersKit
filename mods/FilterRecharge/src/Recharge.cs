using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FilterRecharge
{
    /// <summary>
    /// 滤水器充能：把一个滤水器（普通 / 高级 / 水培芦苇滤芯）拖到高级滤水器上，把它剩下的耐久度转给高级滤水器。
    /// 高级滤水器的耐久度上限提高到 普通滤水器满耐久 × 倍数（默认 400 × 20 = 8000）。
    /// 也可以拖到装着高级滤水器的东西上（水罐、养殖缸等窗口第一层的滤芯槽），不用打开它。
    ///
    /// 走的是游戏原版“对物品使用”的流程（GameItem.MayTarget / CanTarget / Target，电池拖到电池上互换也是这个流程）：
    /// MayTarget / CanTarget 后置把这一对算作“可以使用”（悬停显示绿色“对 X 使用 Y”），Target 前置执行充能并跳过原版
    /// （原版对这一对本来没有任何效果）。只对自己的单件物品生效；框选一起拖时一件接一件充（游戏对选中的每一件依次“使用”）。
    ///
    /// 耐久度的存法与原版相同（DurabilityHelper 的 当前 / 上限 / 默认 三个标签）。充能时把上限和默认值一起设成新的上限：
    /// 只改上限的话，原版的估价公式里“上限 / 默认”这一项会让售价翻十倍。这样充满时售价与原版满耐久时相同，没充满按比例打折。
    /// </summary>
    internal static class Recharge
    {
        internal const string AdvancedId = "water_filter_adv";
        // 能拿来充能的滤水器。韧棉织物（kotton_fabric）也算滤芯，但它是做衣服的材料，不拿来充，免得误拖掉；紫外线灯没有耐久度。
        private static readonly HashSet<string> SourceIds = new HashSet<string> { "water_filter", "water_filter_adv", "hydroreed_filter" };
        internal const int NormalFilterDurability = 400;   // 普通滤水器满耐久（PreBuiltItemHelper.WATER_FILTER_DURABILITY）

        private const string FilterTag = "WATER_FILTER";
        private const string DurabilityTag = "DURABILITY_TAG";

        private const string HarmonyId = "FilterRecharge.patches";
        private static Harmony harmony;
        internal static bool Applied { get; private set; }

        // 正在自己判断时为 true（判断里会调用被补丁的函数，避免绕回来）
        private static bool busy;

        private static readonly FieldInfo dragItemField = AccessTools.Field(typeof(ItemMouseDragHandler), "currentItem");

        internal static int Cap => NormalFilterDurability * Math.Max(1, FilterRechargeMod.Config?.Multiplier ?? 20);

        // ------------------------------------------------------------------ 补丁

        internal static void Apply()
        {
            try
            {
                harmony = new Harmony(HarmonyId);
                var may = AccessTools.Method(typeof(GameItem), "MayTarget", new[] { typeof(GameItem) });
                var can = AccessTools.Method(typeof(GameItem), "CanTarget", new[] { typeof(GameItem) });
                var target = AccessTools.Method(typeof(GameItem), "Target", new[] { typeof(GameItem) });
                if (may == null || can == null || target == null ||
                    may.DeclaringType != typeof(GameItem) || can.DeclaringType != typeof(GameItem) || target.DeclaringType != typeof(GameItem))
                {
                    Log.Warn("找不到 GameItem.MayTarget / CanTarget / Target（游戏更新了？），滤水器充能不可用");
                    return;
                }
                harmony.Patch(may, postfix: new HarmonyMethod(AccessTools.Method(typeof(Recharge), nameof(MayTargetPostfix))));
                harmony.Patch(can, postfix: new HarmonyMethod(AccessTools.Method(typeof(Recharge), nameof(CanTargetPostfix))));
                // 先于其他 mod 的 Target 前置执行（例如便利替换：装着高级滤水器时，拖滤水器上去是充能，不是换件）
                harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(Recharge), nameof(TargetPrefix))) { priority = Priority.High });
                Applied = true;
            }
            catch (Exception e)
            {
                Log.Error("打补丁失败，滤水器充能不可用: " + Log.Describe(e));
                Remove();
                return;
            }

            // 只影响界面：拖到装着高级滤水器的水罐上时显示绿色，而不是红色的“放不进去”
            try
            {
                var slotMay = AccessTools.Method(typeof(GameSlotInventory), "MayHaveValidInventorySlot", new[] { typeof(GameItem) });
                if (slotMay != null && slotMay.DeclaringType == typeof(GameSlotInventory))
                    harmony.Patch(slotMay, postfix: new HarmonyMethod(AccessTools.Method(typeof(Recharge), nameof(SlotMayPostfix))));
                else Log.Warn("找不到 GameSlotInventory.MayHaveValidInventorySlot：充能照常可用，只是拖到水罐上悬停时显示红色");
            }
            catch (Exception e)
            {
                Log.Warn("悬停提示补丁失败（充能照常可用）: " + e.Message);
            }
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            harmony = null;
            Applied = false;
        }

        private static void MayTargetPostfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (__result || busy) return;
            try { if (Find(__instance, targetItem) != null || FilterInPlace.Applies(__instance, targetItem)) __result = true; }
            catch (Exception e) { Log.Warn("判断能否充能出错: " + e.Message); }
        }

        private static void CanTargetPostfix(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            if (__result || busy) return;
            try { if (Find(__instance, targetItem) != null || FilterInPlace.Applies(__instance, targetItem)) __result = true; }
            catch (Exception e) { Log.Warn("判断能否充能出错: " + e.Message); }
        }

        private static bool TargetPrefix(GameItem __instance, GameItem targetItem, bool __runOriginal)
        {
            if (!__runOriginal || busy) return true;
            try
            {
                var adv = Find(__instance, targetItem);
                if (adv != null)
                {
                    Charge(__instance, adv);
                    return false;
                }
                // 高级滤水器拖到装着水的容器上：原地过滤一遍（原版对这一对没有任何效果）
                if (FilterInPlace.Applies(__instance, targetItem))
                {
                    busy = true;
                    try { FilterInPlace.Run(__instance, targetItem); }
                    finally { busy = false; }
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                busy = false;
                Log.Error("充能出错: " + Log.Describe(e));
                return true;
            }
        }

        /// <summary>
        /// 悬停显示：鼠标正拖着滤水器、指着的水罐里这个槽装着要充能的高级滤水器时，告诉原版“这个槽现在放不进”
        /// （本来就放不进，原版只是没查有没有被占），于是原版改走“对物品使用”的分支，显示绿色。只在鼠标拖动这件物品时生效。
        /// </summary>
        private static void SlotMayPostfix(GameSlotInventory __instance, GameItem item, ref bool __result)
        {
            if (!__result || busy || item == null) return;
            try
            {
                if (!IsSource(item) || !(IsMouseDragging(item) || IsGroupDraggingItem(item))) return;
                var adv = __instance.childItem;
                if (!IsAdvanced(adv)) return;
                var owner = GraphUtils.FindParentType<GameItem>(__instance);
                if (owner != null && ReferenceEquals(Find(item, owner), adv)) __result = false;
            }
            catch { }
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

        // ------------------------------------------------------------------ 判断

        private static bool IsSource(GameItem item) =>
            item != null && item.identifier != null && SourceIds.Contains(item.identifier) && item.IsTag(FilterTag) && item.IsTag(DurabilityTag);

        private static bool IsAdvanced(GameItem item) =>
            item != null && item.identifier == AdvancedId && item.IsTag(DurabilityTag);

        /// <summary>
        /// 返回要充能的高级滤水器；不满足条件时返回 null。
        /// src：自己的单件滤水器，不是多选一起拖。target：自己的高级滤水器本身，或者窗口第一层的槽里装着高级滤水器的物品（水罐等）。
        /// 高级滤水器已满、src 已用完时也返回它（松手时提示，而不是让别的 mod 把高级滤水器换下来）。
        /// </summary>
        internal static GameItem Find(GameItem src, GameItem target)
        {
            if (!(FilterRechargeMod.Config?.Enabled ?? false)) return null;
            if (src == null || target == null || ReferenceEquals(src, target)) return null;
            if (!IsSource(src) || src.unitCount != 1) return null;
            if (!GeneralHelper.IsItemOwned(src) || !GeneralHelper.IsItemOwned(target)) return null;
            // 框选一起拖也可以：游戏对选中的每一件依次“使用”，一件接一件充进去（提示合并成一条，见 Batch）
            if (IsAdvanced(target)) return target;

            busy = true;
            try
            {
                if (target.contentWindow == null) return null;
                if (GraphUtils.FindParentEquals(src, target)) return null;   // 从这个东西自己的窗口里拖出来的
                foreach (var slot in Slots(target))
                {
                    var cur = slot.childItem;
                    if (cur != null && !ReferenceEquals(cur, src) && IsAdvanced(cur) && GeneralHelper.IsItemOwned(cur)) return cur;
                }
                return null;
            }
            finally { busy = false; }
        }

        /// <summary>target 窗口第一层的槽（单槽窗口或机器面板），不进入里面装着的物品。</summary>
        private static IEnumerable<GameSlotInventory> Slots(GameItem target)
        {
            PixelElement el = null;
            try { el = target.contentWindow?.childElement; } catch { }
            if (el is GameSlotInventory single)
            {
                yield return single;
            }
            else if (el is MachineryPanelRow row)
            {
                for (int i = 0; i < 64; i++)
                {
                    GraphNodeStorage st = null;
                    try { st = row.GetStorage(i); } catch { }
                    if (st == null) break;
                    if (st is GameSlotInventory s) yield return s;
                }
            }
        }

        private static bool IsGroupDraggingItem(GameItem item)
        {
            try
            {
                var multi = ItemMultiSelectHandler.current;
                return multi != null && multi.IsGroupDraggingItem(item);
            }
            catch { return false; }
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

        // ------------------------------------------------------------------ 充能

        // 同一帧里连续充（框选一起拖）的汇总：下一帧只提示一次
        private static class Batch
        {
            internal static GameItem Adv;
            internal static int Count, Moved, UsedUp, Skipped, From, Now, Max;
            internal static bool Scheduled;

            internal static void Add(GameItem adv, int moved, bool usedUp, bool skipped, int from, int now, int max)
            {
                if (!ReferenceEquals(Adv, adv) || !Scheduled)
                {
                    if (Scheduled) Flush();
                    Adv = adv; Count = Moved = UsedUp = Skipped = 0; From = from;
                }
                if (skipped) Skipped++; else { Count++; Moved += moved; if (usedUp) UsedUp++; }
                Now = now; Max = max;
                if (!Scheduled)
                {
                    Scheduled = true;
                    try { LetterboxManager.RunNextUpdate(Flush); }
                    catch { Flush(); }
                }
            }

            internal static void Flush()
            {
                if (!Scheduled) return;
                Scheduled = false;
                string advName = Name(Adv);
                string msg;
                if (Count == 0)
                    msg = Lang.T(advName + "已经满了（" + Now + "/" + Max + "）", advName + " is already full (" + Now + "/" + Max + ")");
                else
                {
                    msg = Lang.T(advName + "耐久度 +" + Moved + " → " + Now + "/" + Max, advName + " durability +" + Moved + " → " + Now + "/" + Max);
                    if (Count > 1) msg += Lang.T("（" + Count + " 个滤水器）", " (" + Count + " filters)");
                    if (Skipped > 0) msg += Lang.T("，满了，另外 " + Skipped + " 个没用上", "; full, " + Skipped + " more left unused");
                }
                Notify(msg);
                Adv = null;
            }
        }

        private static void Charge(GameItem src, GameItem adv)
        {
            int cap = Cap;
            int advCur = DurabilityHelper.GetRealDurability(adv);
            int advMax = DurabilityHelper.GetMaxDurability(adv);
            int srcCur = DurabilityHelper.GetRealDurability(src);
            string advName = Name(adv), srcName = Name(src);

            if (srcCur <= 0)
            {
                Notify(Lang.T(srcName + "已经用完了", srcName + " is used up"));
                return;
            }
            int newMax = Math.Max(advMax, cap);   // 上限只升不降（以前用更大的倍数充过的不会被截掉）
            int room = newMax - advCur;
            if (room <= 0)
            {
                Batch.Add(adv, 0, false, true, advCur, advCur, newMax);
                return;
            }

            int moved = Math.Min(srcCur, room);
            busy = true;
            try
            {
                // 上限和默认值一起改（见类说明：只改上限会让售价翻十倍）；SetDurability 同时把“表面耐久度”设成真实值
                DurabilityHelper.SetDurability(adv, advCur + moved, newMax, newMax);
                int srcMax = DurabilityHelper.GetMaxDurability(src), srcDef = DurabilityHelper.GetDefDurability(src);
                DurabilityHelper.SetDurability(src, srcCur - moved, srcMax, srcDef > 0 ? srcDef : srcMax);
            }
            finally { busy = false; }

            try { adv.Validate(); } catch { }
            try { src.Validate(); } catch { }

            Batch.Add(adv, moved, srcCur - moved <= 0, false, advCur, advCur + moved, newMax);
            Log.Info("充能：" + srcName + " " + srcCur + " → " + (srcCur - moved) + "，" + advName + " " + advCur + "/" + advMax + " → " + (advCur + moved) + "/" + newMax);
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

        private static string Name(GameItem it)
        {
            try
            {
                var n = it.GetDisplayName(true);
                return string.IsNullOrEmpty(n) ? it.identifier ?? "?" : n;
            }
            catch { return it?.identifier ?? "?"; }
        }
    }
}
