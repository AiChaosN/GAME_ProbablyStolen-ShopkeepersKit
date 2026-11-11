using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Conveniences
{
    /// <summary>
    /// 一键灌水：拖着瓶子（任何装液体的容器）时按灌水键（默认 W）：
    ///   - 指着水龙头：把瓶子装满自来水（和把瓶子拖到水龙头上一样，只是不用松手）；
    ///   - 指着另一个容器，瓶子里有东西：倒进去（和松手倒一样，用游戏自己的 Target，声音、滤芯都照原版）；
    ///   - 指着另一个容器，瓶子是空的：用自来水反复“装满瓶子 → 倒进去”，直到容器满。等于你自己在水龙头和容器之间来回跑，
    ///     水质（今天的锈水 / 溢流水纯度）、经过滤芯时的耗损都和手动一样，只是一次做完（只放一次声音）。
    /// 瓶子里有水时不会再接自来水，免得把你的净水和自来水混在一起。
    /// 水龙头要先解锁锈水或溢流水管道（游戏规则：没解锁时拖到水龙头上什么都不发生）。
    /// </summary>
    internal static class WaterFill
    {
        private const string LiquidTag = "LIQUID_CONTAINER_TAG";
        private const int MaxTrips = 1000;
        // StoreStation.Instance 的 getter 在没有存档时会新建一个对象：直接读私有静态字段（字面量 typeof，扫描器不要求权限）
        private static readonly FieldInfo stationField = AccessTools.Field(typeof(StoreStation), "instance");

        internal static void OnKey()
        {
            var bottle = Swap.DraggedItem();
            if (bottle == null || !bottle.IsTag(LiquidTag)) return;   // 没拖着装液体的东西：这个键什么都不做
            GameItem target = null;
            try { target = RenderHandler.RaycastElement<GameItem>(Input.mousePosition, bottle.fakeBackground); }
            catch { }
            if (target == null || ReferenceEquals(target, bottle)) return;
            if (!GeneralHelper.IsItemOwned(bottle))
            {
                Notify(Lang.T("这不是你的东西", "That isn't yours"));
                return;
            }
            if (bottle.unitCount > 1)
            {
                Notify(Lang.T("一次拿一个瓶子", "Hold one bottle at a time"));
                return;
            }

            var faucet = Faucet();
            if (faucet != null && ReferenceEquals(target, faucet))
            {
                FillFromTap(bottle, sound: true);
                return;
            }
            if (!target.IsTag(LiquidTag) || !GeneralHelper.IsItemOwned(target)) return;
            if (!(bottle.MayTarget(target) && bottle.CanTarget(target)))
            {
                Notify(Lang.T("这个容器现在不能往里倒", "Can't pour into that container right now"));
                return;
            }
            if (WaterHelper.GetFreeCapacity(target) <= 0)
            {
                Notify(Lang.T(Name(target) + "已经满了", Name(target) + " is already full"));
                return;
            }

            if (!WaterHelper.IsEmpty(bottle))
            {
                // 瓶子里有东西：只倒这一瓶，和松手倒完全一样
                int before = WaterHelper.GetTotalVolume(target);
                bottle.Target(target);
                int poured = WaterHelper.GetTotalVolume(target) - before;
                Log.Info("灌水：" + Name(bottle) + " → " + Name(target) + " " + poured + " ml");
                return;
            }

            // 空瓶子：用自来水反复装瓶、倒进去，直到容器满
            if (!TapAvailable())
            {
                Notify(Lang.T("水龙头还不能用：要先解锁锈水或溢流水管道", "The tap isn't available yet: unlock the rust water or gutterflow pipe first"));
                return;
            }
            int start = WaterHelper.GetTotalVolume(target);
            int trips = 0;
            for (; trips < MaxTrips && WaterHelper.GetFreeCapacity(target) > 0; trips++)
            {
                if (!FillFromTap(bottle, sound: false) || WaterHelper.IsEmpty(bottle)) break;
                int inBottle = WaterHelper.GetTotalVolume(bottle);
                WaterHelper.TransferLiquid(bottle, target);
                if (WaterHelper.GetTotalVolume(bottle) >= inBottle) break;   // 一点都没倒进去（例如容器不收）：停下，免得空转
            }
            int added = WaterHelper.GetTotalVolume(target) - start;
            try
            {
                WaterHelper.UpdateWaterBottleSound(bottle);
                WaterHelper.UpdateWaterBottleSound(target);
                if (added > 0) AudioHelper.PourWater();
                bottle.Validate();
                target.Validate();
            }
            catch { }
            int leftover = WaterHelper.GetTotalVolume(bottle);
            Notify(Lang.T("往" + Name(target) + "灌了 " + added + " ml 自来水（来回 " + trips + " 趟）" + (leftover > 0 ? "，瓶子里还剩 " + leftover + " ml" : ""),
                          "Filled " + Name(target) + " with " + added + " ml of tap water (" + trips + " trips)" + (leftover > 0 ? "; " + leftover + " ml left in the bottle" : "")));
            Log.Info("灌水（自来水）：" + Name(target) + " +" + added + " ml，" + trips + " 趟");
        }

        private static GameItem Faucet()
        {
            try { return EmporiumEntry.Instance?.faucet; }
            catch { return null; }
        }

        private static bool Unlocked(string id)
        {
            try { return NetworkUpgrade.GetUpgradeById(id)?.IsUnlocked() ?? false; }
            catch { return false; }
        }

        private static bool TapAvailable() => Unlocked("RUSTWATER_TAP") || Unlocked("GUTTERFLOW_TAP");

        /// <summary>和游戏的水龙头一样（WaterHelper 里拖到水龙头上的处理）：锈水管道优先，其次溢流水，按今天的纯度装满。</summary>
        private static bool FillFromTap(GameItem bottle, bool sound)
        {
            try
            {
                StoreStation station = null;
                try { station = stationField?.GetValue(null) as StoreStation; } catch { }
                if (station == null) return false;
                int purity;
                if (Unlocked("RUSTWATER_TAP")) purity = station.todayRustwaterPurity;
                else if (Unlocked("GUTTERFLOW_TAP")) purity = station.todayGutterflowPurity;
                else
                {
                    if (sound) Notify(Lang.T("水龙头还不能用：要先解锁锈水或溢流水管道", "The tap isn't available yet: unlock the rust water or gutterflow pipe first"));
                    return false;
                }
                if (WaterHelper.GetFreeCapacity(bottle) <= 0) return true;
                WaterHelper.AddWater(bottle, 1, 9999999, useRange: true, purity, purity);
                if (sound)
                {
                    AudioHelper.UseFaucet();
                    WaterHelper.UpdateWaterBottleSound(bottle);
                    bottle.Validate();
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("从水龙头装水出错: " + e.Message);
                return false;
            }
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
