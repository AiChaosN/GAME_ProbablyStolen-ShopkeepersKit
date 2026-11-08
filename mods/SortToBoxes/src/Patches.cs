using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SortToBoxes
{
    /// <summary>
    /// 两个补丁，都手动打（不用 [HarmonyPatch]）。某个目标找不到时只禁用对应功能，热键照常可用。
    ///
    /// 1. 买下时自动归箱：PlayerStore.MoveDealItemToInventory 之后接手。
    ///    游戏在收购、批发、以物易物成交时调用它把货移进仓库；只处理确实落进了仓库的货。
    ///
    /// 2. 放一件上柜台时带上同款：SlotMarker.TryAcceptOnce 前后。
    ///    拖放、Shift 快速移动、游戏自己的 SwapInv 最后都走这里，而且调用前物品还在原来的格子里，
    ///    所以能准确知道“从哪来、到哪去”：只有从库存（仓库/箱子）放到柜台你这一侧、且柜台前有买家时才触发。
    ///    以物易物成交时游戏把顾客那一侧的货挪过来（来源是顾客那侧），不会误触发；本 mod 自己的搬运也不会。
    /// </summary>
    internal static class Patches
    {
        private const string HarmonyId = "SortToBoxes.patches";
        private static Harmony harmony;

        internal static bool AutoPurchaseApplied { get; private set; }
        internal static bool CounterApplied { get; private set; }

        internal static void Apply()
        {
            try { harmony = new Harmony(HarmonyId); }
            catch (Exception e)
            {
                Log.Warn("Harmony 初始化失败，自动功能不可用（热键不受影响）: " + e.Message);
                return;
            }

            try
            {
                var target = AccessTools.Method(typeof(PlayerStore), "MoveDealItemToInventory", new[] { typeof(GameItem) });
                var postfix = AccessTools.Method(typeof(Patches), nameof(MoveDealItemPostfix));
                if (target != null && target.DeclaringType == typeof(PlayerStore) && postfix != null)
                {
                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    AutoPurchaseApplied = true;
                }
                else Log.Warn("找不到 PlayerStore.MoveDealItemToInventory，“买下时自动归箱”不可用");
            }
            catch (Exception e)
            {
                Log.Warn("自动归箱补丁失败: " + e.Message);
            }

            try
            {
                var target = AccessTools.Method(typeof(SlotMarker), "TryAcceptOnce", new[] { typeof(int) });
                var prefix = AccessTools.Method(typeof(Patches), nameof(AcceptPrefix));
                var postfix = AccessTools.Method(typeof(Patches), nameof(AcceptPostfix));
                if (target != null && target.DeclaringType == typeof(SlotMarker) && prefix != null && postfix != null)
                {
                    harmony.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                    CounterApplied = true;
                }
                else Log.Warn("找不到 SlotMarker.TryAcceptOnce，“放一件上柜台带上同款”不可用");
            }
            catch (Exception e)
            {
                Log.Warn("柜台补丁失败: " + e.Message);
            }
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            harmony = null;
            AutoPurchaseApplied = false;
            CounterApplied = false;
        }

        private static void MoveDealItemPostfix(GameItem item)
        {
            try
            {
                var cfg = SortToBoxesMod.Config;
                if (cfg == null || !cfg.AutoOnPurchase || item == null) return;
                var warehouse = Stock.Warehouse;
                if (warehouse == null || !ReferenceEquals(item.parentInventory, warehouse)) return;
                var boxes = Sorter.CollectBoxes();
                if (boxes.Count == 0) return;
                var report = Sorter.SortItems(new List<GameItem> { item }, boxes);
                if (report.Details.Count > 0) Log.Info("自动归箱：" + string.Join("；", report.Details));
                SortBehaviour.Instance?.ReportAuto(report);
            }
            catch (Exception e)
            {
                // 成交流程里绝不能抛出
                Log.Warn("自动归箱出错: " + e.Message);
            }
        }

        // __state = 放置前物品所在的格子
        private static void AcceptPrefix(SlotMarker __instance, out GameInventory __state)
        {
            __state = null;
            try
            {
                if (Mover.Active) return;
                var cfg = SortToBoxesMod.Config;
                if (cfg == null || !cfg.AutoSameOnCounter) return;
                var counter = Stock.Counter;
                if (counter == null || !ReferenceEquals(__instance?.inventory, counter)) return;
                __state = __instance.item?.parentInventory;
            }
            catch { __state = null; }
        }

        private static void AcceptPostfix(SlotMarker __instance, int __result, GameInventory __state)
        {
            try
            {
                if (__state == null || __result <= 0 || Mover.Active) return;
                if (!Stock.IsStockInventory(__state)) return;     // 不是从仓库/箱子拿来的
                var moved = __instance?.item;                    // 拆叠时这里已经是拆出来、放上柜台的那一份
                if (moved == null || !GeneralHelper.IsItemOwned(moved)) return;
                if (!Counter.HasBuyer(out _)) return;
                SortBehaviour.Instance?.QueueSame(moved);         // 下一帧再搬，不在游戏的放置流程中途改库存
            }
            catch (Exception e)
            {
                Log.Warn("柜台补丁出错: " + e.Message);
            }
        }
    }
}
