using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chef
{
    /// <summary>
    /// 两台机器，都照游戏的净水器（MachinePurifier）搭：面板顺序 电源（0）、模组（1）、其他……
    /// 模组格必须是第 1 个面板：游戏的 MachineHelper.GetModuleInv / 模组格扩建 / 读档时恢复扩建都按这个下标找。
    /// 模组格用游戏的 MachineHelper.SetupModuleBay（装上 / 拿下模组时重算加成），品质加成读 MachineryHelper.GetCurrentQualityBonus。
    /// </summary>
    internal static class Machines
    {
        internal const string FryerId = "chaos5090_chef_fryer";
        internal const string ProcessorId = "chaos5090_chef_processor";
        internal const int FryerTankMl = 5000;
        private const int FryerPower = 20;      // 每晚
        private const int ProcessorPower = 5;   // 每按一次
        private const string ModuleShape = "10000010001000001110000010001000001";   // 游戏标准机器的模组格（7 宽，1 = 不能放）

        private static GameItem NewMachine(PixelWindow window, string sprite, string name, string desc, long value, int power,
                                           GameSlotInventory battery, GameGridInventory modules)
        {
            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(window).SetSpriteAndShapeFromMod(ChefMod.ModId, sprite);
            machine.name = name;
            machine.shortDescription = desc;
            machine.unitValue = value;
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG");
            machine.EnableTag("STANDARD_MACHINE_TAG");
            try { AudioHelper.InitMetalMachine(machine); } catch { }
            MachineHelper.SetupBatterySlot(battery, machine);
            MachineryHelper.InitMachinery(machine, power);
            machine.ModifyTag("MACHINE_QUALITY_RATIO_INT", s => s.Enable().SetInt(1));
            MachineHelper.SetupModuleBay(modules, machine);
            return machine;
        }

        private static bool Owned(GameItem it)
        {
            try { return it != null && GeneralHelper.IsItemOwned(it); }
            catch { return false; }
        }

        private static bool CanPower(GameItem machine, GameSlotInventory battery)
        {
            try { return battery.childItem != null && PowerHelper.CanDrawPowerSource(battery.childItem, MachineryHelper.GetMachinePowerUsage(machine)); }
            catch { return false; }
        }

        private static void DrawPower(GameItem machine, GameSlotInventory battery)
        {
            try { PowerHelper.DrawPowerSource(battery.childItem, MachineryHelper.GetMachinePowerUsage(machine)); } catch { }
        }

        // ================================================================== 油锅

        internal static string FryerName() => Lang.T("油锅", "Deep Fryer");

        internal static string FryerDesc() => Lang.T(
            "把食用油桶拖到油锅上倒油（最多 5 L），电源槽放电池。炸篮里放营养果、肉，每晚炸好放进出锅格：营养果 → 薯条，肉 → 炸鸡。"
            + "每份吸掉 100 ml 油，炸得越多油越脏：大颗粒炸渣用滤油网滤（拖到油锅上），小颗粒油渣把油锅拖到装着滤水器的滤水壶上滤。"
            + "油越纯净、肉和营养果的千卡越高、模组品质加成越高，炸出来越好。标题栏的“倒掉油”清空油槽。",
            "Drop a cooking oil jug on the fryer to pour oil in (up to 5 L) and put a battery in the power slot. Put nutrifruit and meat in the basket; each night they are fried into the output: nutrifruit → fries, meat → fried chicken. "
            + "Each portion soaks up 100 ml of oil and the oil gets dirtier: strain large crumbs with an oil strainer (drop it on the fryer), and filter fine residue by dropping the fryer on a water pitcher with a filter. "
            + "Purer oil, higher-calorie meat and nutrifruit and better quality modules make better food. \"Dump oil\" in the title bar empties the tank.");

        internal static GameItem CreateFryer()
        {
            var battery = new GameSlotInventory();
            var modules = new GameGridInventory(ModuleShape, 7);
            var basket = new GameGridInventory(6, 4);
            var output = new GameGridInventory(6, 6);
            var window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Lang.T("模组", "Modules"), modules, "module"),
                MachineHelper.Panel(Lang.T("炸篮", "Basket"), basket, "input"),
                MachineHelper.Panel(Lang.T("出锅", "Output"), output, "output"));
            var machine = NewMachine(window, "chef_fryer", FryerName(), FryerDesc(), 450L, FryerPower, battery, modules);
            Oil.InitHolder(machine, FryerTankMl, 0);
            Items.InitOilSource(machine);
            basket.mayInventoryAddItemFunc = (item, _) => Owned(item) && (Ingredients.IsMeat(item) || Ingredients.IsFruit(item));
            output.mayInventoryAddItemFunc = (item, _) => Food.IsDish(item);
            machine.onCycleEndSlotItemFunc = (self, inv, slot) =>
            {
                try { Fry(machine, battery, modules, basket, output); }
                catch (Exception e) { Log.Error("油锅处理出错: " + Log.Describe(e)); }
            };
            Buttons.Register(window, machine, (() => Lang.T("倒掉油", "Dump oil"), () => DumpOil(machine)));
            return machine;
        }

        private static void Fry(GameItem machine, GameSlotInventory battery, GameGridInventory modules, GameGridInventory basket, GameGridInventory output)
        {
            if (!Owned(machine)) return;
            var food = basket.childItems.Where(i => Owned(i) && (Ingredients.IsMeat(i) || Ingredients.IsFruit(i))).ToList();
            if (food.Count == 0) return;
            var oil = Oil.Read(machine);
            int portions = Oil.Portions(oil);
            if (portions <= 0)
            {
                Log.Info("油锅：油不够（" + oil.Total + " ml，至少要 " + Oil.MinToFry + " ml），今晚不炸");
                return;
            }
            if (!CanPower(machine, battery))
            {
                Log.Info("油锅：没电，今晚不炸");
                return;
            }

            double purity = oil.Purity;
            int module = Quality.ModulePoints(machine);
            int fried = 0;
            bool full = false;
            var made = new List<string>();
            foreach (var it in food)
            {
                bool fruit = Ingredients.IsFruit(it);
                int kcal = Ingredients.UnitKcal(it);
                int units = Math.Max(1, it.unitCount);
                for (int u = 0; u < units && fried < portions; u++)
                {
                    var s = Score.Empty;
                    s.Module = module;
                    if (fruit)
                    {
                        s.Fruit = Quality.FruitPoints(kcal, 25);
                        s.Oil = Quality.OilPoints(purity, 55);
                    }
                    else
                    {
                        s.Meat = Quality.MeatPoints(kcal, 35);
                        s.Oil = Quality.OilPoints(purity, 45);
                    }
                    var dish = Food.Make(fruit ? Dish.Fries : Dish.Chicken, s, kcal + 90);
                    if (dish == null) { full = true; break; }
                    if (!GraphUtils.CanAccept(output, dish))
                    {
                        dish.Destroy();
                        full = true;
                        break;
                    }
                    GraphUtils.TryAcceptAll(output, dish);
                    Ingredients.ConsumeOne(basket, it);
                    fried++;
                    made.Add(dish.name);
                }
                if (full || fried >= portions) break;
            }
            if (fried <= 0)
            {
                Log.Info("油锅：出锅格满了，今晚不炸");
                return;
            }
            DrawPower(machine, battery);
            Oil.AfterFrying(ref oil, fried);
            Oil.Write(machine, oil);
            try { MachineHelper.OnMachineActioned(machine, modules); } catch { }
            machine.Validate();
            Log.Info("油锅：炸了 " + fried + " 份（" + string.Join("、", made) + "），油纯净度 " + Oil.Percent(purity) + " → " + Oil.Percent(oil.Purity));
        }

        // 倒油要点两次（3 秒内），免得误点把新油倒了
        private static GameItem dumpArmed;
        private static float dumpArmedAt = -10f;

        private static string DumpOil(GameItem machine)
        {
            var m = Oil.Read(machine);
            if (m.Total <= 0) return Lang.T("油锅里没有油", "The fryer has no oil");
            if (!ReferenceEquals(dumpArmed, machine) || Time.unscaledTime - dumpArmedAt > 3f)
            {
                dumpArmed = machine;
                dumpArmedAt = Time.unscaledTime;
                return Lang.T("再点一次确认倒掉 " + Oil.Ml(m.Total) + " 油（" + Oil.StateName(m) + "，纯净度 " + Oil.Percent(m.Purity) + "）",
                              "Click again to dump " + Oil.Ml(m.Total) + " of oil (" + Oil.StateName(m) + ", purity " + Oil.Percent(m.Purity) + ")");
            }
            dumpArmed = null;
            Oil.Write(machine, new Oil.Mix());
            machine.Validate();
            return Lang.T("倒掉了 " + Oil.Ml(m.Total) + " 油", "Dumped " + Oil.Ml(m.Total) + " of oil");
        }

        // ================================================================== 食品加工器

        internal static string ProcessorName() => Lang.T("食品加工器", "Food Processor");

        internal static string ProcessorDesc() => Lang.T(
            "“食材”格放肉、营养果、填料草，点标题栏的“做汉堡”，用掉格子里全部食材做一个肉汉堡：千卡比例越接近 肉 55% : 营养果 30% : 填料草 15% 越好，肉的千卡越高越好。"
            + "“配餐”格放汉堡、薯条、炸鸡各一份，点“配套餐”做超级大套餐（品质取三样里最低的）。每次耗一点电；模组品质加成越高做得越好。",
            "Put meat, nutrifruit and fillerweed in \"Ingredients\" and click \"Burger\" in the title bar to turn all of them into one meat burger: the closer the calorie ratio is to meat 55% : nutrifruit 30% : fillerweed 15% and the more calories in the meat, the better. "
            + "Put a burger, fries and fried chicken in \"Plating\" and click \"Combo\" for a super combo (its quality is the lowest of the three). Each click uses a little power; quality modules help.");

        internal static GameItem CreateProcessor()
        {
            var battery = new GameSlotInventory();
            var modules = new GameGridInventory(ModuleShape, 7);
            var ingredients = new GameGridInventory(8, 6);
            var plating = new GameGridInventory(7, 4);
            var output = new GameGridInventory(6, 6);
            var window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Lang.T("模组", "Modules"), modules, "module"),
                MachineHelper.Panel(Lang.T("食材", "Ingredients"), ingredients, "input"),
                MachineHelper.Panel(Lang.T("配餐", "Plating"), plating, "plating"),
                MachineHelper.Panel(Lang.T("产出", "Output"), output, "output"));
            var machine = NewMachine(window, "chef_processor", ProcessorName(), ProcessorDesc(), 400L, ProcessorPower, battery, modules);
            ingredients.mayInventoryAddItemFunc = (item, _) => Owned(item) && Ingredients.IsIngredient(item);
            plating.mayInventoryAddItemFunc = (item, _) => Owned(item) && (item.identifier == Food.BurgerId || item.identifier == Food.FriesId || item.identifier == Food.ChickenId);
            output.mayInventoryAddItemFunc = (item, _) => Food.IsDish(item);
            Buttons.Register(window, machine,
                (() => Lang.T("做汉堡", "Burger"), () => MakeBurger(machine, battery, modules, ingredients, output)),
                (() => Lang.T("配套餐", "Combo"), () => MakeCombo(machine, battery, modules, plating, output)));
            return machine;
        }

        private static string MakeBurger(GameItem machine, GameSlotInventory battery, GameGridInventory modules, GameGridInventory ingredients, GameGridInventory output)
        {
            var all = ingredients.childItems.Where(i => Owned(i) && Ingredients.IsIngredient(i)).ToList();
            int meat = all.Where(Ingredients.IsMeat).Sum(Ingredients.Kcal);
            int fruit = all.Where(Ingredients.IsFruit).Sum(Ingredients.Kcal);
            int weed = all.Where(Ingredients.IsWeed).Sum(Ingredients.Kcal);
            if (meat <= 0) return Lang.T("“食材”格里要有肉", "Put some meat in \"Ingredients\"");
            if (fruit <= 0) return Lang.T("“食材”格里要有营养果（做面包胚）", "Put some nutrifruit in \"Ingredients\" (for the bun)");

            var s = Score.Empty;
            s.Meat = Quality.MeatPoints(meat, 30);
            s.Ratio = Quality.RatioPoints(meat, fruit, weed);
            s.Module = Quality.ModulePoints(machine);
            var burger = Food.Make(Dish.Burger, s, meat + fruit + weed);
            if (burger == null) return Lang.T("做不出汉堡（物品没登记上，详见日志）", "Couldn't make a burger (item not registered; see the log)");
            if (!GraphUtils.CanAccept(output, burger))
            {
                burger.Destroy();
                return Lang.T("“产出”格满了，先把做好的拿走", "\"Output\" is full; take the finished food out first");
            }
            if (!CanPower(machine, battery))
            {
                burger.Destroy();
                return Lang.T("没电：在电源槽里放一块有电的电池", "No power: put a charged battery in the power slot");
            }
            DrawPower(machine, battery);
            Ingredients.ConsumeAll(ingredients, all);
            GraphUtils.TryAcceptAll(output, burger);
            try { MachineHelper.OnMachineActioned(machine, modules); } catch { }
            machine.Validate();

            double total = meat + fruit + weed;
            string ratio = Pct(meat / total) + " : " + Pct(fruit / total) + " : " + Pct(weed / total);
            Log.Info("食品加工器：做出 " + burger.name + "，" + s.Total + " 分（肉 " + s.Meat + "、比例 " + s.Ratio + "、模组 " + s.Module + "），千卡比例 " + ratio);
            return Lang.T("做出 " + burger.name + "，" + s.Total + " 分。千卡比例 肉 : 营养果 : 填料草 = " + ratio + "（理想 55 : 30 : 15）",
                          "Made " + burger.name + ", " + s.Total + " pts. Calorie ratio meat : nutrifruit : fillerweed = " + ratio + " (ideal 55 : 30 : 15)");
        }

        private static string MakeCombo(GameItem machine, GameSlotInventory battery, GameGridInventory modules, GameGridInventory plating, GameGridInventory output)
        {
            GameItem Find(string id) => plating.childItems.FirstOrDefault(i => Owned(i) && i.identifier == id);
            var burger = Find(Food.BurgerId);
            var fries = Find(Food.FriesId);
            var chicken = Find(Food.ChickenId);
            var missing = new List<string>();
            if (burger == null) missing.Add(Food.DishName(Dish.Burger));
            if (fries == null) missing.Add(Food.DishName(Dish.Fries));
            if (chicken == null) missing.Add(Food.DishName(Dish.Chicken));
            if (missing.Count > 0)
                return Lang.T("“配餐”格里要有汉堡、薯条、炸鸡各一份，还差：", "\"Plating\" needs a burger, fries and fried chicken; missing: ") + string.Join(Lang.T("、", ", "), missing);

            int min = new[] { Food.ScoreOf(burger), Food.ScoreOf(fries), Food.ScoreOf(chicken) }.Min();
            int kcal = Ingredients.UnitKcal(burger) + Ingredients.UnitKcal(fries) + Ingredients.UnitKcal(chicken);
            var combo = Food.MakeCombo(Math.Max(0, min), kcal);
            if (combo == null) return Lang.T("做不出套餐（物品没登记上，详见日志）", "Couldn't make a combo (item not registered; see the log)");
            if (!GraphUtils.CanAccept(output, combo))
            {
                combo.Destroy();
                return Lang.T("“产出”格满了，先把做好的拿走", "\"Output\" is full; take the finished food out first");
            }
            if (!CanPower(machine, battery))
            {
                combo.Destroy();
                return Lang.T("没电：在电源槽里放一块有电的电池", "No power: put a charged battery in the power slot");
            }
            DrawPower(machine, battery);
            Ingredients.ConsumeOne(plating, burger);
            Ingredients.ConsumeOne(plating, fries);
            Ingredients.ConsumeOne(plating, chicken);
            GraphUtils.TryAcceptAll(output, combo);
            try { MachineHelper.OnMachineActioned(machine, modules); } catch { }
            machine.Validate();
            Log.Info("食品加工器：做出 " + combo.name + "（" + min + " 分）");
            return Lang.T("做出 ", "Made ") + combo.name;
        }

        private static string Pct(double f) => Math.Round(f * 100).ToString("0") + "%";
    }
}
