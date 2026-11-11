using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedScientist
{
    /// <summary>
    /// 职业能力：机器区里的机器一天按两天运转。
    /// 游戏每晚（PlayerStore.EndNight）对你所有的物品依次调用 早 / 主 / 晚 三轮 onCycleEnd* 委托，机器的产出、耗电都在里面。
    /// 这里在那之后（ModHook.OnGoingSleepLate）对直接放在机器区（machine_bay / machine_bay_ext）里的机器再跑一遍这三轮，
    /// 做法和游戏自己的涡轮增压器（MachineTurboBoosterAdv.BoostMachine）、调试面板的“跳过一晚”一样。
    /// 每台机器自己从电池再扣一次电，电不够那一轮就不做。
    /// 期间把 CurrentConverterHelper.isNightCycle 设为 true：电流转换器在夜里由包月电费覆盖，白天才按度收钱。
    /// 不加倍：冰箱（只多耗电）、警报器和幻影投影仪（加成是赋值不是累加）、发电类（充电器、能量块、反应堆、增殖电池）、坏掉的机器。
    /// 只对高级科学家的存档生效，设置里可以关。
    /// </summary>
    internal static class TwoDays
    {
        private static readonly HashSet<string> BayIds = new HashSet<string> { "machine_bay", "machine_bay_ext" };
        private static readonly HashSet<string> Excluded = new HashSet<string>
        {
            "recharger", "powerblock", "makeshift_reactor", "energy_credit_breeder", "mirage_projector", "security_alarm",
        };

        internal static bool InBay(GameItem it)
        {
            try
            {
                var bay = it?.parentInventory?.GetParentItem();
                return bay != null && bay.identifier != null && BayIds.Contains(bay.identifier);
            }
            catch { return false; }
        }

        private static bool Eligible(GameItem it)
        {
            if (it == null || !it.IsGameItemType("MACHINE") || !GeneralHelper.IsItemOwned(it)) return false;
            if (it.identifier != null && Excluded.Contains(it.identifier)) return false;
            if (it.IsTag("FRIDGE_TAG") || it.IsTag("BROKEN_MACHINE_TAG")) return false;
            return InBay(it);
        }

        internal static void OnGoingSleepLate()
        {
            try
            {
                if (!(ScientistMod.Config?.TwoDays ?? false) || !ModHelper.IsStartingScenario(Scenario.Id)) return;
                var store = Game.Store;
                if (store == null) return;
                var machines = store.FindAllItem().Where(Eligible).ToList();
                if (machines.Count == 0) return;

                bool was = CurrentConverterHelper.isNightCycle;
                CurrentConverterHelper.isNightCycle = true;
                try
                {
                    foreach (var m in machines) Run(m, m.onCycleEndEarlySlotItemFunc, "早");
                    foreach (var m in machines) Run(m, m.onCycleEndSlotItemFunc, "主");
                    foreach (var m in machines) Run(m, m.onCycleEndLateSlotItemFunc, "晚");
                }
                finally { CurrentConverterHelper.isNightCycle = was; }
                Log.Info("机器区多运转一天：" + machines.Count + " 台（" + string.Join("、", machines.Select(Name).Distinct().Take(12)) + "）");
            }
            catch (Exception e)
            {
                Log.Error("机器区多运转一天出错: " + Log.Describe(e));
            }
        }

        private static void Run(GameItem m, Action<GameItem, GameInventory, SlotMarker> f, string pass)
        {
            if (f == null) return;
            try { f(m, m.parentInventory, m.GetIncompleteSlotInInventory()); }
            catch (Exception e) { Log.Warn(Name(m) + " 第二天（" + pass + "）出错: " + e.Message); }
        }

        private static string Name(GameItem it)
        {
            try { return it.GetDisplayName(true); }
            catch { return it?.identifier ?? "?"; }
        }
    }
}
