using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdvancedScientist
{
    /// <summary>
    /// 两台都是点窗口标题栏的按钮（“提取 / 镶嵌”，MachineButtons）立刻做、从电池扣一次电（0.3.0 起；之前是每晚做一次）。
    /// ② 词条提取机：放一个模组进去，把它的词条做成芯片（基础芯片 + 特殊词条芯片），模组变回空白模组。
    ///   游戏原生的模组 → 低级空白模组（1 个词条槽）；定制模组 → 同档的空白模组（词条原样取出）。
    /// ④ 词条镶嵌机：放一个空白模组 / 定制模组和若干芯片，把芯片镶到空着的词条槽上（先基础芯片，再特殊词条芯片），
    ///   特殊词条最多 2 个、不能重复、不能和已有的互斥（游戏自己的规则）；镶不上的芯片留在格子里。
    /// 两台都照游戏的酒窖搭：电池槽 + 输入 + 输出。
    /// </summary>
    internal static class AffixMachines
    {
        internal const string ExtractorId = "chaos5090_as_affix_extractor";
        internal const string InlayerId = "chaos5090_as_affix_inlayer";
        private const int PowerPerNight = 30;

        private static GameItem NewMachine(PixelWindow window, string sprite, string name, string desc, long value)
        {
            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(window).SetSpriteAndShapeFromMod(ScientistMod.ModId, sprite);
            machine.name = name;
            machine.shortDescription = desc;
            machine.unitValue = value;
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG");
            machine.EnableTag("STANDARD_MACHINE_TAG");
            try { AudioHelper.InitMetalMachine(machine); } catch { }
            return machine;
        }

        private static void Give(GameInventory output, GameItem item)
        {
            if (item == null) return;
            if (GraphUtils.TryAcceptAll(output, item) > 0 || item.parentInventory == null)
                GraphUtils.EmporiumTryAdd(item);   // 输出格放不下：放进仓库，不丢
        }

        // ------------------------------------------------------------------ ② 提取机

        internal static GameItem CreateExtractor()
        {
            var battery = new GameSlotInventory();
            var input = new GameSlotInventory();
            var output = new GameGridInventory(5, 3);
            var window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Lang.T("模组", "Module"), input, "input"),
                MachineHelper.Panel(Lang.T("产出", "Output"), output, "output"));
            var machine = NewMachine(window, "as_affix_extractor", Lang.T("词条提取机", "Affix Extractor"),
                Lang.T("放一个模组进去，点窗口标题栏的“提取”按钮（耗电）：数值成一块基础芯片，每个特殊词条成一块特殊词条芯片；模组变回空白模组（原生模组变成低级空白模组，定制模组变回同档的空白模组）。神经核心等违禁品、赃物也能提取；功能型模组、节点、损坏的不收。",
                       "Put a module in and click \"Extract\" in the title bar (uses power): the stats become a base chip and each special affix an affix chip; the module turns back into a blank module (game modules become low-grade blanks, custom modules blanks of the same grade). Contraband such as neural cores and stolen goods can be extracted too; functional modules, nodes and corrupt modules are refused."),
                600L);
            MachineHelper.SetupBatterySlot(battery, machine);
            MachineryHelper.InitMachinery(machine, PowerPerNight);
            input.mayInventoryAddItemFunc = (item, _) =>
            {
                var why = CustomModules.RefuseReason(item);
                if (why != null) HintRefusal(item, why);
                return why == null;
            };
            output.mayInventoryAddItemFunc = (item, _) => item != null && (Chips.IsChip(item) || item.identifier == BlankModules.VanillaId);
            MachineButtons.Register(window, machine, () => Lang.T("提取", "Extract"), () => Extract(machine, battery, input, output));
            return machine;
        }

        // 放不进提取机时（松手那一刻）提示原因；同一件东西 1 秒内只提示一次
        private static GameItem lastRefused;
        private static float lastRefusedAt = -10f;

        private static void HintRefusal(GameItem item, string why)
        {
            try
            {
                if (item == null || !Input.GetMouseButtonUp(0)) return;
                if (ReferenceEquals(item, lastRefused) && Time.unscaledTime - lastRefusedAt < 1f) return;
                lastRefused = item;
                lastRefusedAt = Time.unscaledTime;
                MachineButtons.Notify(why);
            }
            catch { }
        }

        private static string Extract(GameItem machine, GameSlotInventory battery, GameSlotInventory input, GameGridInventory output)
        {
            var module = input.childItem;
            if (module == null) return Lang.T("先在“模组”槽里放一个模组", "Put a module in the \"Module\" slot first");
            var why = CustomModules.RefuseReason(module);
            if (why != null) return why;
            var affixes = CustomModules.Extract(module);
            bool custom = CustomModules.Read(module).Count > 0;

            // 产出格至少要放得下一个空白模组，否则这一晚不做（芯片放不下时进仓库）
            var probe = DirectoryMaster.Item(BlankModules.VanillaId);
            bool room = probe != null && GraphUtils.CanAccept(output, probe);
            probe?.Destroy();
            if (!room) return Lang.T("产出格满了，先把里面的东西拿走", "The output grid is full; take things out first");
            if (!MachineHelper.TryDrawCyclePower(machine, battery))
                return Lang.T("没电：在电源槽里放一块有电的电池", "No power: put a charged battery in the power slot");

            string from = module.GetDisplayName(true);
            GameItem blank;
            if (custom)
            {
                // 定制模组：清空记录，自己变回同档空白模组，挪到产出格
                CustomModules.Write(module, new List<Affix>());
                blank = module;
                input.Expel(module);
            }
            else
            {
                blank = BlankModules.Create(1);
                input.Expel(module);
                module.Destroy();
            }
            Give(output, blank);
            foreach (var a in affixes) Give(output, Chips.Make(a));
            machine.Validate();
            Log.Info("词条提取机：" + from + " → " + string.Join("、", affixes.Select(a => a.Describe())) + "，模组变回 " + blank?.name);
            return Lang.T("提取了 " + affixes.Count + " 块芯片：", "Extracted " + affixes.Count + " chip(s): ") + string.Join(Lang.T("、", ", "), affixes.Select(a => a.Describe()));
        }

        // ------------------------------------------------------------------ ④ 镶嵌机

        internal static GameItem CreateInlayer()
        {
            var battery = new GameSlotInventory();
            var moduleSlot = new GameSlotInventory();
            var chips = new GameGridInventory(4, 2);
            var window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Lang.T("空白模组", "Blank module"), moduleSlot, "module"),
                MachineHelper.Panel(Lang.T("芯片", "Chips"), chips, "chips"));
            var machine = NewMachine(window, "as_affix_inlayer", Lang.T("词条镶嵌机", "Affix Inlayer"),
                Lang.T("放一个空白模组（或还有空槽的定制模组）和芯片，点窗口标题栏的“镶嵌”按钮（耗电），把芯片镶到空着的词条槽上：低 / 中 / 高级空白模组有 1 / 2 / 3 个槽；先镶基础芯片（数值相加），再镶特殊词条芯片（最多 2 个、不能互斥）。镶不上的芯片留在格子里。",
                       "Put in a blank module (or a custom module with free slots) and chips, then click \"Inlay\" in the title bar (uses power) to inlay chips into free slots: low / mid / high grade blanks have 1 / 2 / 3 slots; base chips first (stats add up), then affix chips (at most 2, no conflicting ones). Chips that can't be inlaid stay in the grid."),
                600L);
            MachineHelper.SetupBatterySlot(battery, machine);
            MachineryHelper.InitMachinery(machine, PowerPerNight);
            moduleSlot.mayInventoryAddItemFunc = (item, _) => item != null && GeneralHelper.IsItemOwned(item) && item.identifier == BlankModules.VanillaId;
            chips.mayInventoryAddItemFunc = (item, _) => item != null && GeneralHelper.IsItemOwned(item) && Chips.IsChip(item);
            MachineButtons.Register(window, machine, () => Lang.T("镶嵌", "Inlay"), () => Inlay(machine, battery, moduleSlot, chips));
            return machine;
        }

        private static string Inlay(GameItem machine, GameSlotInventory battery, GameSlotInventory moduleSlot, GameGridInventory chipGrid)
        {
            var module = moduleSlot.childItem;
            if (module == null || module.identifier != BlankModules.VanillaId)
                return Lang.T("先在“空白模组”槽里放一个空白模组", "Put a blank module in the \"Blank module\" slot first");
            var list = CustomModules.Read(module);
            int free = BlankModules.SlotsOf(module) - list.Count;
            if (free <= 0) return Lang.T("这个模组的词条槽满了（" + list.Count + "/" + BlankModules.SlotsOf(module) + "）", "This module's slots are full (" + list.Count + "/" + BlankModules.SlotsOf(module) + ")");

            // 先排好这一晚要镶哪些（基础芯片在前），确定有东西可镶再耗电
            var candidates = chipGrid.childItems.Where(Chips.IsChip).ToList()
                .Select(c => (item: c, affix: Chips.Read(c)))
                .Where(x => x.affix != null && !x.affix.IsEmpty)
                .OrderBy(x => x.affix.IsBase ? 0 : 1)
                .ToList();
            var plan = new List<(GameItem item, Affix affix)>();
            var trial = new List<Affix>(list);
            foreach (var x in candidates)
            {
                if (plan.Count >= free) break;
                if (!x.affix.IsBase && !CustomModules.CanAddSpecial(trial, x.affix.Effect)) continue;
                plan.Add(x);
                trial.Add(x.affix);
            }
            if (plan.Count == 0)
                return candidates.Count == 0 ? Lang.T("“芯片”格里没有芯片", "No chips in the \"Chips\" grid")
                                             : Lang.T("这些芯片镶不上：特殊词条最多 2 个、不能重复、不能和已有的互斥", "These chips can't be inlaid: at most 2 special affixes, no duplicates or conflicting ones");
            if (!MachineHelper.TryDrawCyclePower(machine, battery))
                return Lang.T("没电：在电源槽里放一块有电的电池", "No power: put a charged battery in the power slot");
            foreach (var x in plan)
            {
                var it = x.item;
                if (it.unitCount > 1) it.GetSplitItem(1)?.Destroy();
                else
                {
                    chipGrid.Expel(it);
                    it.Destroy();
                }
            }
            CustomModules.Write(module, trial);
            machine.Validate();
            Log.Info("词条镶嵌机：镶上 " + string.Join("、", plan.Select(x => x.affix.Describe())) + " → " + module.name);
            return Lang.T("镶上 ", "Inlaid ") + string.Join(Lang.T("、", ", "), plan.Select(x => x.affix.Describe())) + " → " + module.name;
        }
    }
}
