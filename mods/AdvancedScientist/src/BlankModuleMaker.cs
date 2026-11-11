using System;
using System.Linq;

namespace AdvancedScientist
{
    /// <summary>
    /// ① 空白模组制造机：照游戏的“酒窖”（MachineWineRack）搭——电池槽 + 材料格 + 产出格。
    /// 点窗口标题栏的“制造”按钮（MachineButtons）立刻做一个空白模组，从电池扣一次电（0.3.0 起；之前是每晚做一个）。
    /// 材料够哪档做哪档，优先做高档（配方见 Recipe）。产出格放不下、没电时不做，也不扣材料。
    /// </summary>
    internal static class BlankModuleMaker
    {
        internal const string Id = "chaos5090_as_blank_module_maker";
        internal const string Sprite = "as_blank_module_maker";
        private const int PowerPerNight = 30;

        internal static GameItem Create()
        {
            var battery = new GameSlotInventory();
            // 材料都很大：普通电子元件、金属锭 3×2，线缆 2×2，都不叠放。高级配方 6 + 6 + 6 = 12 件 3×2 + 6 件 2×2 = 96 格；
            // 12×10 = 120 格，留点余量（0.2.0 的 6×3 连低级都放不下）。
            var input = new GameGridInventory(12, 10);
            var output = new GameGridInventory(4, 4);   // 4 个 2×2 空白模组（机器区一晚做两个，攒几天也放得下）
            var window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Lang.T("材料", "Materials"), input, "input"),
                MachineHelper.Panel(Lang.T("产出", "Output"), output, "output"));

            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(window).SetSpriteAndShapeFromMod(ScientistMod.ModId, Sprite);
            machine.name = Name();
            machine.shortDescription = Describe();
            machine.unitValue = 400L;
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG");
            machine.EnableTag("STANDARD_MACHINE_TAG");   // 和游戏的标准机器一样，涡轮增压器也能用
            try { AudioHelper.InitMetalMachine(machine); } catch { }
            MachineHelper.SetupBatterySlot(battery, machine);
            MachineryHelper.InitMachinery(machine, PowerPerNight);

            input.mayInventoryAddItemFunc = (item, _) => item != null && GeneralHelper.IsItemOwned(item) && Recipe.Materials.Contains(item.identifier);
            output.mayInventoryAddItemFunc = (item, _) => item != null && item.identifier == BlankModules.VanillaId;
            input.onSlotAddItemLateFunc = delegate { machine.Validate(); };
            input.onSlotRemoveItemLateFunc = delegate { machine.Validate(); };

            MachineButtons.Register(window, machine, () => Lang.T("制造", "Make"), () => Make(machine, battery, input, output));
            return machine;
        }

        internal static string Name() => Lang.T("空白模组制造机", "Blank Module Maker");

        internal static string Describe()
        {
            string lines = string.Join(Lang.T("；", "; "), Recipe.All.Reverse().Select(r => BlankModules.TierName(r.Tier) + " = " + r.Describe()));
            return Lang.T("点窗口标题栏的“制造”按钮，耗电用材料格里的材料做一个空白模组；材料够哪档做哪档，优先做高档。", "Click \"Make\" in the window's title bar to use power and make one blank module from the materials; it makes the best grade the materials allow. ") + lines;
        }

        /// <summary>做一个空白模组；返回给玩家看的结果。</summary>
        internal static string Make(GameItem machine, GameSlotInventory battery, GameGridInventory input, GameGridInventory output)
        {
            var recipe = Recipe.Best(input);
            if (recipe == null)
                return Lang.T("材料不够。低级 = ", "Not enough materials. Low grade = ") + Recipe.All.Last().Describe();
            var made = BlankModules.Create(recipe.Tier);
            if (made == null)
            {
                Log.Warn("做不出空白模组：游戏里找不到 blank_module");
                return Lang.T("做不出空白模组（游戏里找不到这个物品）", "Can't make a blank module (item missing in the game)");
            }
            if (!GraphUtils.CanAccept(output, made))
            {
                made.Destroy();
                return Lang.T("产出格满了，先把做好的拿走", "The output grid is full; take the finished modules out first");
            }
            if (!MachineHelper.TryDrawCyclePower(machine, battery))
            {
                made.Destroy();
                return Lang.T("没电：在电源槽里放一块有电的电池", "No power: put a charged battery in the power slot");
            }
            recipe.Consume(input);
            if (GraphUtils.TryAcceptAll(output, made) > 0)
            {
                GraphUtils.EmporiumTryAdd(made);   // 理论上不会发生（上面检查过）：放进仓库，不丢
            }
            machine.Validate();
            Log.Info("空白模组制造机：做出 " + made.name + "（" + recipe.Describe() + "）");
            return Lang.T("做出 ", "Made ") + made.name;
        }
    }
}
