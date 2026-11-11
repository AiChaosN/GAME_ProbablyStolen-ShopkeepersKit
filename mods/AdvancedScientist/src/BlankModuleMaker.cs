using System;
using System.Linq;

namespace AdvancedScientist
{
    /// <summary>
    /// ① 空白模组制造机：照游戏的“酒窖”（MachineWineRack）搭——电池槽 + 材料格 + 产出格，每晚结算时耗一次电、做一个空白模组。
    /// 材料够哪档做哪档，优先做高档（配方见 Recipe）。产出格放不下、没电时这一晚不做，也不扣材料。
    /// 机器每晚的处理挂在 onCycleEndSlotItemFunc：游戏每晚对你所有的物品调用一次，放在哪里都行；
    /// 放在机器区里时，高级科学家的“一天两天”会让它多做一次（TwoDays）。
    /// </summary>
    internal static class BlankModuleMaker
    {
        internal const string Id = "chaos5090_as_blank_module_maker";
        internal const string Sprite = "as_blank_module_maker";
        private const int PowerPerNight = 30;

        internal static GameItem Create()
        {
            var battery = new GameSlotInventory();
            // 材料都很大：电子元件 / 金属锭 / 废金属 3×2，线缆 2×2，而且不叠放。配方最多要 38 格（中级），
            // 0.2.0 的 6×3 连低级配方（24 格）都放不下（用户 2026-11-12 报告）。9×6 一行正好放 3 个 3×2。
            var input = new GameGridInventory(9, 6);
            var output = new GameGridInventory(4, 4);   // 4 个 2×2 空白模组（机器区一晚做两个，攒几天也放得下）
            var window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Lang.T("材料", "Materials"), input, "input"),
                MachineHelper.Panel(Lang.T("产出", "Output"), output, "output"));

            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(window).SetSpriteAndShapeFromMod(ScientistMod.ModId, Sprite);
            machine.name = Lang.T("空白模组制造机", "Blank Module Maker");
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

            machine.onCycleEndSlotItemFunc = (self, inv, slot) =>
            {
                try { Process(machine, battery, input, output); }
                catch (Exception e) { Log.Error("空白模组制造机处理出错: " + Log.Describe(e)); }
            };
            return machine;
        }

        private static string Describe()
        {
            string lines = string.Join(Lang.T("；", "; "), Recipe.All.Reverse().Select(r => BlankModules.TierName(r.Tier) + " = " + r.Describe()));
            return Lang.T("每晚耗电，用材料格里的材料做一个空白模组；材料够哪档做哪档，优先做高档。", "Each night it uses power to make one blank module from the materials; it makes the best grade the materials allow. ") + lines;
        }

        private static void Process(GameItem machine, GameSlotInventory battery, GameGridInventory input, GameGridInventory output)
        {
            var recipe = Recipe.Best(input);
            if (recipe == null) return;
            var made = BlankModules.Create(recipe.Tier);
            if (made == null)
            {
                Log.Warn("做不出空白模组：游戏里找不到 blank_module");
                return;
            }
            if (!GraphUtils.CanAccept(output, made))
            {
                made.Destroy();
                Log.Info("空白模组制造机：产出格满了，今晚不做");
                return;
            }
            if (!MachineHelper.TryDrawCyclePower(machine, battery))
            {
                made.Destroy();
                Log.Info("空白模组制造机：没电，今晚不做");
                return;
            }
            recipe.Consume(input);
            if (GraphUtils.TryAcceptAll(output, made) > 0)
            {
                GraphUtils.EmporiumTryAdd(made);   // 理论上不会发生（上面检查过）：放进仓库，不丢
            }
            machine.Validate();
            Log.Info("空白模组制造机：做出 " + made.name + "（" + recipe.Describe() + "）");
        }
    }
}
