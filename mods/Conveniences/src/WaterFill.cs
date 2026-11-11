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
    /// 按完还拿着瓶子，可以接着对下一个容器按（例如拿着大桶给一排小桶灌水）。
    /// 原版只在松手时倒水，拖动途中液体不会变；途中倒水后游戏会更新水位贴图（SpriteHelper.UpdateWaterContainerSprite
    /// 对一部分容器会重设贴图和形状），正拖着的东西被这样一改就画回原格子、拖动却没结束（1.1.0 的问题）。
    /// 游戏自己在 WaterFeatureHelper 里也特意跳过“正在拖的东西”的贴图更新；这里同样：按键处理期间暂缓手上这件的贴图更新，
    /// 等放下以后（Tick 发现不再拖着它）再补上。1.1.1 用“按完就结束拖动”绕开，但这样没法连续灌好几个，1.1.2 改成现在的做法。
    /// 水龙头要先解锁锈水或溢流水管道（游戏规则：没解锁时拖到水龙头上什么都不发生）。
    /// </summary>
    internal static class WaterFill
    {
        private const string LiquidTag = "LIQUID_CONTAINER_TAG";
        private const int MaxTrips = 1000;
        // StoreStation.Instance 的 getter 在没有存档时会新建一个对象：直接读私有静态字段（字面量 typeof，扫描器不要求权限）
        private static readonly FieldInfo stationField = AccessTools.Field(typeof(StoreStation), "instance");

        // 按键处理期间为 true：这时手上那件的水位贴图更新暂缓（见类说明）
        private static bool acting;
        private static GameItem pendingSprite;
        private const string HarmonyId = "Conveniences.water";
        private static Harmony harmony;

        internal static void Apply()
        {
            try
            {
                harmony = new Harmony(HarmonyId);
                var m = AccessTools.Method(typeof(SpriteHelper), "UpdateWaterContainerSprite", new[] { typeof(GameItem) });
                if (m != null && m.DeclaringType == typeof(SpriteHelper))
                    harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(WaterFill), nameof(SpritePrefix))));
                else Log.Warn("找不到 SpriteHelper.UpdateWaterContainerSprite：灌水后手上的瓶子可能会跳回原位");
            }
            catch (Exception e) { Log.Warn("灌水贴图补丁失败（灌水照常可用）: " + e.Message); }
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            harmony = null;
        }

        private static bool SpritePrefix(GameItem gameItem)
        {
            try
            {
                if (!acting || gameItem == null) return true;
                if (!ReferenceEquals(gameItem, Swap.DraggedItem())) return true;
                pendingSprite = gameItem;   // 放下以后再更新
                return false;
            }
            catch { return true; }
        }

        /// <summary>每帧调用：手上那件放下了，就补上暂缓的贴图更新。</summary>
        internal static void Tick()
        {
            var it = pendingSprite;
            if (it == null || ReferenceEquals(Swap.DraggedItem(), it)) return;
            pendingSprite = null;
            try
            {
                if (it is GameItemElement el && el.IsDestroyed()) return;
                SpriteHelper.UpdateWaterContainerSprite(it);
                it.Validate();
            }
            catch (Exception e) { Log.Warn("补更新瓶子贴图出错: " + e.Message); }
        }

        internal static void OnKey()
        {
            var held = Swap.DraggedItem();
            var snap = DragVisual.Capture(held);
            acting = true;
            try { Act(); }
            finally
            {
                acting = false;
                // 倒水时游戏会刷新这件物品（改标签、加特征都会 Validate），刷新会把拖动的“影子”收回原格子并藏起来
                // （GameItemElement.ValidateSelect → ToggleFakeBackground(false) + 影子位置复位）：看起来瓶子回到了原位，
                // 其实还拿在手上（用户 2026-11-12 反馈“看起来很怪”）。这里恢复成拖动时的样子：位置、朝向都放回去。
                snap?.Restore();
            }
        }

        /// <summary>拖动时的“影子”（跟着鼠标的半透明副本）的状态：开始拖动时游戏用 UpdateFakeReal 设置，这里照样恢复。</summary>
        private sealed class DragVisual
        {
            private GameItemElement item;
            private Vector3 position;
            private Vector3 rotEuler, rotScale;
            private Vector2 rotPivot, rotMin, rotMax;

            internal static DragVisual Capture(GameItemElement el)
            {
                try
                {
                    if (el == null || el.fakeMiddle == null || el.fakeRotator == null) return null;
                    var r = el.fakeRotator.rectTransform;
                    return new DragVisual
                    {
                        item = el,
                        position = el.fakeMiddle.rectTransform.position,
                        rotEuler = r.localEulerAngles, rotScale = r.localScale, rotPivot = r.pivot, rotMin = r.anchorMin, rotMax = r.anchorMax,
                    };
                }
                catch { return null; }
            }

            internal void Restore()
            {
                try
                {
                    if (item == null || item.IsDestroyed() || !ReferenceEquals(Swap.DraggedItem(), item)) return;   // 已经放下了：不用管
                    item.UpdateFakeReal(1);   // 格子里的本体藏起来、影子显示（拖整件；本功能只处理单件瓶子）
                    var r = item.fakeRotator.rectTransform;
                    r.anchorMin = rotMin; r.anchorMax = rotMax; r.pivot = rotPivot; r.localScale = rotScale; r.localEulerAngles = rotEuler;
                    item.fakeMiddle.rectTransform.position = position;
                }
                catch (Exception e) { Log.Warn("恢复拖动样子出错: " + e.Message); }
            }
        }

        private static void Act()
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
                Log.Info("灌水：" + Name(bottle) + " → " + Name(target) + " " + (poured / 1000.0).ToString("0.#") + " ml");
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
                target.Validate();   // 手上的瓶子不 Validate：放下后 Tick 会补
            }
            catch { }
            int leftover = WaterHelper.GetTotalVolume(bottle);
            // 游戏内部的液体单位是千分之一毫升
            string addedMl = (added / 1000.0).ToString("0.#"), leftMl = (leftover / 1000.0).ToString("0.#");
            Notify(Lang.T("往" + Name(target) + "灌了 " + addedMl + " ml 自来水（来回 " + trips + " 趟）" + (leftover > 0 ? "，瓶子里还剩 " + leftMl + " ml" : ""),
                          "Filled " + Name(target) + " with " + addedMl + " ml of tap water (" + trips + " trips)" + (leftover > 0 ? "; " + leftMl + " ml left in the bottle" : "")));
            Log.Info("灌水（自来水）：" + Name(target) + " +" + addedMl + " ml，" + trips + " 趟");
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
