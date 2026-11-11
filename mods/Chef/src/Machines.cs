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
            "“油桶”槽放一桶食用油（游戏的任何桶、瓶子都行，油里不能有水），电源槽放电池。炸篮里放营养果、肉，每晚炸好放进出锅格：营养果 → 薯条，肉 → 炸鸡。"
            + "每份吸掉 100 ml 油，炸得越多油越脏：把油倒进装着滤油网的滤水壶滤掉大颗粒炸渣，再倒进装着滤水器的滤水壶滤掉小颗粒油渣。品质看营养果 / 肉的千卡、油的纯净度和品质模组。",
            "Put a container of cooking oil in the \"Oil\" slot (any of the game's jugs or bottles; no water in the oil) and a battery in the power slot. Put nutrifruit and meat in the basket; each night they are fried into the output: nutrifruit → fries, meat → fried chicken. "
            + "Each portion soaks up 100 ml of oil and the oil gets dirtier: pour it through a water pitcher with an oil strainer to remove large crumbs, then through one with a water filter to remove fine residue. Quality depends on the calories, oil purity and quality modules.");

        internal static GameItem CreateFryer()
        {
            var battery = new GameSlotInventory();
            var modules = new GameGridInventory(ModuleShape, 7);
            var oilSlot = new GameSlotInventory();
            var basket = new GameGridInventory(6, 4);
            var output = new GameGridInventory(6, 6);
            var note = NoteSlot();
            var window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Lang.T("模组", "Modules"), modules, "module"),
                MachineHelper.Panel(Lang.T("油桶", "Oil"), oilSlot, "container"),
                MachineHelper.Panel(Lang.T("炸篮", "Basket"), basket, "input"),
                MachineHelper.Panel(Lang.T("出锅", "Output"), output, "output"),
                MachineHelper.NotePanel(note));
            var machine = NewMachine(window, "chef_fryer", FryerName(), FryerDesc(), 450L, FryerPower, battery, modules);
            oilSlot.mayInventoryAddItemFunc = (item, _) => Owned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
            try { oilSlot.SetBackgroundFadeSprite("Items/water_container4", "xl"); } catch { }   // 和游戏的净水器一样，空槽里画一个淡淡的桶
            basket.mayInventoryAddItemFunc = (item, _) => Owned(item) && (Ingredients.IsMeat(item) || Ingredients.IsFruit(item));
            output.mayInventoryAddItemFunc = (item, _) => Food.IsDish(item);
            machine.onCycleEndSlotItemFunc = (self, inv, slot) =>
            {
                try { Fry(machine, battery, modules, oilSlot, basket, output); }
                catch (Exception e) { Log.Error("油锅处理出错: " + Log.Describe(e)); }
            };
            return machine;
        }

        /// <summary>油锅“油桶”槽里的容器（鼠标说明用）。面板顺序：电源 0、模组 1、油桶 2。</summary>
        internal static GameItem FryerContainer(GameItem fryer)
        {
            try { return (MachineHelper.GetSlot(fryer, 2) as GameSlotInventory)?.childItem; }
            catch { return null; }
        }

        private static void Fry(GameItem machine, GameSlotInventory battery, GameGridInventory modules, GameSlotInventory oilSlot, GameGridInventory basket, GameGridInventory output)
        {
            if (!Owned(machine)) return;
            var food = basket.childItems.Where(i => Owned(i) && (Ingredients.IsMeat(i) || Ingredients.IsFruit(i))).ToList();
            if (food.Count == 0) return;
            var container = oilSlot.childItem;
            var oil = Oil.Read(container);
            string why = container == null ? "没放油桶" : Oil.CannotFry(oil);
            if (why != null)
            {
                Log.Info("油锅：" + why + "，今晚不炸");
                return;
            }
            int portions = Oil.Portions(oil);
            if (!CanPower(machine, battery))
            {
                Log.Info("油锅：没电，今晚不炸");
                return;
            }

            int oilPts = Quality.OilPoints(oil.Purity);
            int module = Quality.ModulePoints(machine);
            int copies = Quality.Copies(machine);
            int fried = 0, made = 0;
            bool full = false;
            var names = new List<string>();
            foreach (var it in food)
            {
                bool fruit = Ingredients.IsFruit(it);
                int kcal = Ingredients.UnitKcal(it);
                int units = Math.Max(1, it.unitCount);
                for (int u = 0; u < units && fried < portions && !full; u++)
                {
                    var s = Score.Empty;
                    s.Module = module;
                    s.Oil = oilPts;
                    if (fruit) s.Fruit = Quality.FruitPoints(kcal);
                    else s.Meat = Quality.ChickenMeatPoints(kcal);
                    int thisPortion = 0;
                    for (int c = 0; c < copies; c++)   // 性能模组：每满 +100% 多出一份
                    {
                        var dish = Food.Make(fruit ? Dish.Fries : Dish.Chicken, s, kcal + 90);
                        if (dish == null || !GraphUtils.CanAccept(output, dish))
                        {
                            dish?.Destroy();
                            full = true;
                            break;
                        }
                        GraphUtils.TryAcceptAll(output, dish);
                        thisPortion++;
                        if (c == 0) names.Add(dish.name);
                    }
                    if (thisPortion == 0) break;
                    Ingredients.ConsumeOne(basket, it);
                    fried++;
                    made += thisPortion;
                }
                if (full || fried >= portions) break;
            }
            if (fried <= 0)
            {
                Log.Info("油锅：出锅格满了，今晚不炸");
                return;
            }
            DrawPower(machine, battery);
            Oil.AfterFrying(container, fried);
            try { MachineHelper.OnMachineActioned(machine, modules); } catch { }
            machine.Validate();
            Log.Info("油锅：炸了 " + fried + " 份食材，出了 " + made + " 份（" + string.Join("、", names) + "），油纯净度 " + Oil.Percent(oil.Purity) + " → " + Oil.Percent(Oil.Read(container).Purity));
        }

        // ================================================================== 食品加工器

        internal static string ProcessorName() => Lang.T("食品加工器", "Food Processor");

        internal static string ProcessorDesc() => Lang.T(
            "“食材”格放肉、填料草、营养果，点标题栏的“做汉堡”，用掉格子里全部食材做一个肉汉堡：肉要超过 1000 千卡、填料草超过 600、营养果超过 400；"
            + "肉越多分越高，千卡比例越接近 肉 5 : 填料草 3 : 营养果 2 越好。“配餐”格放汉堡、薯条、炸鸡各一份，点“配套餐”做超级大套餐。每次耗一点电；品质模组加分。",
            "Put meat, fillerweed and nutrifruit in \"Ingredients\" and click \"Burger\" in the title bar to turn all of them into one meat burger: it needs more than 1000 kcal of meat, 600 of fillerweed and 400 of nutrifruit; "
            + "more meat scores higher, and the closer the calorie ratio is to meat 5 : fillerweed 3 : nutrifruit 2, the better. Put a burger, fries and fried chicken in \"Plating\" and click \"Combo\" for a super combo. Each click uses a little power; quality modules add points.");

        internal static GameItem CreateProcessor()
        {
            var battery = new GameSlotInventory();
            var modules = new GameGridInventory(ModuleShape, 7);
            var ingredients = new GameGridInventory(8, 6);
            var plating = new GameGridInventory(7, 4);
            var output = new GameGridInventory(6, 6);
            var note = NoteSlot();
            var window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Lang.T("模组", "Modules"), modules, "module"),
                MachineHelper.Panel(Lang.T("食材", "Ingredients"), ingredients, "input"),
                MachineHelper.Panel(Lang.T("配餐", "Plating"), plating, "plating"),
                MachineHelper.Panel(Lang.T("产出", "Output"), output, "output"),
                MachineHelper.NotePanel(note));
            var machine = NewMachine(window, "chef_processor", ProcessorName(), ProcessorDesc(), 400L, ProcessorPower, battery, modules);
            ingredients.mayInventoryAddItemFunc = (item, _) => Owned(item) && Ingredients.IsIngredient(item);
            plating.mayInventoryAddItemFunc = (item, _) => Owned(item) && (item.identifier == Food.BurgerId || item.identifier == Food.FriesId || item.identifier == Food.ChickenId);
            output.mayInventoryAddItemFunc = (item, _) => Food.IsDish(item);
            Buttons.Register(window, machine,
                (() => Lang.T("做汉堡", "Burger"), () => MakeBurger(machine, battery, modules, ingredients, output)),
                (() => Lang.T("配套餐", "Combo"), () => MakeCombo(machine, battery, modules, plating, output)));
            return machine;
        }

        /// <summary>把 copies 份同样的菜放进产出格，返回放进去几份（放不下就停）。</summary>
        private static int Produce(GameGridInventory output, int copies, Func<GameItem> make, out string name)
        {
            name = null;
            int n = 0;
            for (int c = 0; c < copies; c++)
            {
                var dish = make();
                if (dish == null || !GraphUtils.CanAccept(output, dish))
                {
                    dish?.Destroy();
                    break;
                }
                GraphUtils.TryAcceptAll(output, dish);
                name = dish.name;
                n++;
            }
            return n;
        }

        private static string MakeBurger(GameItem machine, GameSlotInventory battery, GameGridInventory modules, GameGridInventory ingredients, GameGridInventory output)
        {
            var all = ingredients.childItems.Where(i => Owned(i) && Ingredients.IsIngredient(i)).ToList();
            int meat = all.Where(Ingredients.IsMeat).Sum(Ingredients.Kcal);
            int weed = all.Where(Ingredients.IsWeed).Sum(Ingredients.Kcal);
            int fruit = all.Where(Ingredients.IsFruit).Sum(Ingredients.Kcal);
            var lacking = new List<string>();
            if (meat <= Quality.BurgerMinMeat) lacking.Add(Lang.T("肉 " + meat + "（要超过 " + Quality.BurgerMinMeat + "）", "meat " + meat + " (needs more than " + Quality.BurgerMinMeat + ")"));
            if (weed <= Quality.BurgerMinWeed) lacking.Add(Lang.T("填料草 " + weed + "（要超过 " + Quality.BurgerMinWeed + "）", "fillerweed " + weed + " (needs more than " + Quality.BurgerMinWeed + ")"));
            if (fruit <= Quality.BurgerMinFruit) lacking.Add(Lang.T("营养果 " + fruit + "（要超过 " + Quality.BurgerMinFruit + "）", "nutrifruit " + fruit + " (needs more than " + Quality.BurgerMinFruit + ")"));
            if (lacking.Count > 0) return Lang.T("食材不够（千卡）：", "Not enough ingredients (kcal): ") + string.Join(Lang.T("、", ", "), lacking);
            if (!CanPower(machine, battery)) return Lang.T("没电：在电源槽里放一块有电的电池", "No power: put a charged battery in the power slot");

            var s = Score.Empty;
            s.Meat = Quality.BurgerMeatPoints(meat);
            s.Ratio = Quality.RatioPenalty(meat, weed, fruit);
            s.Module = Quality.ModulePoints(machine);
            int n = Produce(output, Quality.Copies(machine), () => Food.Make(Dish.Burger, s, meat + weed + fruit), out var name);
            if (n == 0) return Lang.T("“产出”格满了，先把做好的拿走", "\"Output\" is full; take the finished food out first");
            DrawPower(machine, battery);
            Ingredients.ConsumeAll(ingredients, all);
            try { MachineHelper.OnMachineActioned(machine, modules); } catch { }
            machine.Validate();

            double total = meat + weed + fruit;
            string ratio = Pct(meat / total) + " : " + Pct(weed / total) + " : " + Pct(fruit / total);
            string count = n > 1 ? " ×" + n : "";
            Log.Info("食品加工器：做出 " + name + count + "，" + s.Total + " 分（肉 " + s.Meat + "、比例 " + s.Ratio + "、模组 " + s.Module + "），千卡比例 " + ratio);
            return Lang.T("做出 " + name + count + "，" + s.Total + " 分（肉 " + s.Meat + "，比例 " + s.Ratio + "，模组 " + s.Module + "）。肉 : 填料草 : 营养果 = " + ratio + "（理想 50 : 30 : 20）",
                          "Made " + name + count + ", " + s.Total + " pts (meat " + s.Meat + ", ratio " + s.Ratio + ", modules " + s.Module + "). Meat : fillerweed : nutrifruit = " + ratio + " (ideal 50 : 30 : 20)");
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
            if (!CanPower(machine, battery)) return Lang.T("没电：在电源槽里放一块有电的电池", "No power: put a charged battery in the power slot");

            // 加权平均：薯条 1、炸鸡 2、汉堡 4
            int score = (int)Math.Round((Math.Max(0, Food.ScoreOf(fries)) + 2.0 * Math.Max(0, Food.ScoreOf(chicken)) + 4.0 * Math.Max(0, Food.ScoreOf(burger))) / 7.0);
            int kcal = Ingredients.UnitKcal(burger) + Ingredients.UnitKcal(fries) + Ingredients.UnitKcal(chicken);
            int n = Produce(output, Quality.Copies(machine), () => Food.MakeCombo(score, kcal), out var name);
            if (n == 0) return Lang.T("“产出”格满了，先把做好的拿走", "\"Output\" is full; take the finished food out first");
            DrawPower(machine, battery);
            Ingredients.ConsumeOne(plating, burger);
            Ingredients.ConsumeOne(plating, fries);
            Ingredients.ConsumeOne(plating, chicken);
            try { MachineHelper.OnMachineActioned(machine, modules); } catch { }
            machine.Validate();
            string count = n > 1 ? " ×" + n : "";
            Log.Info("食品加工器：做出 " + name + count + "（" + score + " 分）");
            return Lang.T("做出 " + name + count + "，" + score + " 分", "Made " + name + count + ", " + score + " pts");
        }

        // ================================================================== 说明便签（和游戏的净水器、水培架一样：一张便利贴放在机器的便签槽里）

        private const string NoteTag = "CHAOS5090_CHEF_NOTE";

        private static GameSlotInventory NoteSlot()
        {
            var slot = new GameSlotInventory();
            slot.mayInventoryAddItemFunc = (item, _) => item != null && item.identifier == "postit";
            try { slot.SetBackgroundFadeSprite("Items/items_tool", "postit"); } catch { }
            return slot;
        }

        private static string ModuleLine() => Lang.T(
            "[模组影响]： 效率影响功耗 性能影响产量（每满 +100% 每份多出一个） 质量影响品质分（每 +2% 加 1 分，最多 50 分）",
            "[Module Impact]:    Efficiency: Power Consumption   Performance: Output (+1 per full +100%)   Quality: Score (+1 per +2%, up to 50)");

        /// <summary>给机器放一张说明便签（开局 / 王尔德送机器时调用；物品工厂里不能放子物品）。</summary>
        internal static GameItem WithNote(GameItem machine)
        {
            if (machine == null) return null;
            try
            {
                string kind = machine.identifier == FryerId ? "fryer" : machine.identifier == ProcessorId ? "processor" : null;
                if (kind == null) return machine;
                var note = DirectoryMaster.Item("postit");
                if (note == null) return machine;
                note.ModifyTag(NoteTag, s => s.Enable().SetString(kind));
                ApplyNote(note);
                if (GraphUtils.TryAcceptAll(machine, note) > 0 || note.parentInventory == null) note.Destroy();
            }
            catch (Exception e) { Log.Warn("放说明便签出错: " + e.Message); }
            return machine;
        }

        internal static bool IsOurNote(GameItem it) => it != null && it.identifier == "postit" && !string.IsNullOrEmpty(it.GetTagReadonly(NoteTag).GetString());

        /// <summary>按当前语言写便签的文字（读档时也调用）。</summary>
        internal static void ApplyNote(GameItem note)
        {
            string kind = note.GetTagReadonly(NoteTag).GetString();
            if (kind == "fryer")
            {
                note.name = Lang.T("油锅说明", "Deep Fryer Instructions");
                note.shortDescription = Lang.T(
                    "放入一桶食用油（油里不能有水）、要炸的营养果和肉，以及一个电量充足的电源。油锅在夜间运作：营养果炸成薯条，肉炸成炸鸡，每份吸掉 100 ml 油。油越纯净越好，详见厨师菜谱。",
                    "Insert a container of cooking oil (no water in it), nutrifruit and meat to fry, and a power source with enough energy. The fryer works overnight: nutrifruit becomes fries, meat becomes fried chicken, each portion soaking up 100 ml of oil. The purer the oil the better; see the chef's recipe book.");
            }
            else
            {
                note.name = Lang.T("食品加工器说明", "Food Processor Instructions");
                note.shortDescription = Lang.T(
                    "“食材”格放肉、填料草、营养果，点“做汉堡”；“配餐”格放汉堡、薯条、炸鸡，点“配套餐”。需要一个电量充足的电源，按一下立刻做好。详见厨师菜谱。",
                    "Put meat, fillerweed and nutrifruit in \"Ingredients\" and click \"Burger\"; put a burger, fries and fried chicken in \"Plating\" and click \"Combo\". Needs a power source with enough energy; it works instantly. See the chef's recipe book.");
            }
            note.flavorText = ModuleLine();
            note.SetGameItemType("DOCUMENT");
            note.EnableTag("IMPORTANT_TAG");
            note.unitValue = 0L;
        }

        private static string Pct(double f) => Math.Round(f * 100).ToString("0") + "%";
    }
}
