using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SortToBoxes
{
    /// <summary>
    /// “买下时自动归箱”：在 PlayerStore.MoveDealItemToInventory 之后接手。
    /// 游戏在收购、批发、以物易物成交时都调用它，把货移进仓库（设置里“买入物品移入库存”开着、且不在外面时）。
    /// 只处理确实落进了仓库的货；外出、关了那个设置、或被“购买留柜”之类的 mod 留在柜台时都不动。
    /// 手动打补丁（不用 [HarmonyPatch]），找不到目标时只禁用自动归箱，热键照常可用。
    /// </summary>
    internal static class Patches
    {
        private const string HarmonyId = "SortToBoxes.auto";
        private static Harmony harmony;

        internal static bool Applied { get; private set; }

        internal static void Apply()
        {
            try
            {
                var target = AccessTools.Method(typeof(PlayerStore), "MoveDealItemToInventory", new[] { typeof(GameItem) });
                var postfix = AccessTools.Method(typeof(Patches), nameof(MoveDealItemPostfix));
                if (target == null || target.DeclaringType != typeof(PlayerStore) || postfix == null)
                {
                    Log.Warn("找不到 PlayerStore.MoveDealItemToInventory，“买下时自动归箱”不可用（热键归箱不受影响）");
                    return;
                }
                harmony = new Harmony(HarmonyId);
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Applied = true;
            }
            catch (Exception e)
            {
                Log.Warn("自动归箱补丁失败（热键归箱不受影响）: " + e.Message);
            }
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            harmony = null;
            Applied = false;
        }

        private static void MoveDealItemPostfix(GameItem item)
        {
            try
            {
                var cfg = SortToBoxesMod.Config;
                if (cfg == null || !cfg.AutoOnPurchase || item == null) return;
                var inv = EmporiumEntry.Instance?.invElement;
                if (inv == null || !ReferenceEquals(item.parentInventory, inv)) return;
                var boxes = Sorter.CollectBoxes();
                if (boxes.Count == 0) return;
                var report = Sorter.SortItems(new List<GameItem> { item }, boxes, cfg.MinMatchPercent);
                if (report.Details.Count > 0) Log.Info("自动归箱：" + string.Join("；", report.Details));
                SortBehaviour.Instance?.ReportAuto(report);
            }
            catch (Exception e)
            {
                // 成交流程里绝不能抛出
                Log.Warn("自动归箱出错: " + e.Message);
            }
        }
    }
}
