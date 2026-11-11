using System;

namespace AdvancedScientist
{
    /// <summary>
    /// 职业“高级科学家”：用游戏的官方接口 ModHelper.AddStartingScenario 注册，出现在原版的职业选择界面（第 2 页）。
    /// 开局物品在 onStart 里发放（游戏在 NewGameData.HandleInitialItem 的 MODDED 分支里调用，那时存档对象已经建好）。
    /// 存档里记着开局职业（PlayerStore.startScenarioId），之后用 ModHelper.IsStartingScenario(Id) 判断。
    /// </summary>
    internal static class Scenario
    {
        internal const string Id = "chaos5090_advanced_scientist";
        internal const string IconSprite = "as_icon";
        private const int StartCashChange = -200;   // 基础 600；开局设备价值较高，扣 200（化学家扣 300）

        internal static void Register(string modId)
        {
            ModHelper.AddStartingScenario(modId, Id,
                () => Lang.T("高级科学家", "Advanced Scientist"),
                () => Lang.T("精通模组工艺的科学家。用空白模组制造机把电子元件和金属做成空白模组（低 / 中 / 高级，可镶 2 / 4 / 6 个词条），用词条提取机（找杰克逊博士买）把模组的词条做成芯片，再用词条镶嵌机镶到空白模组上。职业能力：机器区里的机器每天按两天运转（设置里可关）。",
                             "A scientist skilled in module crafting. The Blank Module Maker turns electronic parts and metal into blank modules (low / mid / high grade, 2 / 4 / 6 slots); the Affix Extractor (sold by Dr. Jackson) turns a module's affixes into chips, and the Affix Inlayer puts chips into blank modules. Ability: machines in the Machine Bay run two days' worth every day (can be turned off in the settings)."),
                new Func<string>[]
                {
                    () => Lang.T("机器区", "Machine Bay"),
                    () => Lang.T("空白模组制造机（附一块电池和一份低级空白模组的材料）", "Blank Module Maker (with a battery and materials for one low-grade blank module)"),
                    () => Lang.T("词条镶嵌机（附一块电池）", "Affix Inlayer (with a battery)"),
                    () => Lang.T("三块随机模组（性能 / 效率 / 品质）", "Three random modules (performance / efficiency / quality)"),
                },
                OnStart,
                iconKey: modId + ":" + IconSprite,
                tag: () => Lang.T("模组工艺", "Module crafting"),
                tagTone: StartingScenario.TagTone.Positive);
        }

        private static void OnStart()
        {
            var store = Game.Store;
            var inv = EmporiumEntry.Instance?.invElement;
            if (store == null || inv == null)
            {
                Log.Error("开局发放失败：存档或仓库还没准备好");
                return;
            }
            Give(inv, DirectoryMaster.Item("machine_bay"), "机器区");
            Give(inv, DirectoryMaster.Item(BlankModuleMaker.Id), "空白模组制造机");
            Give(inv, DirectoryMaster.Item(AffixMachines.InlayerId), "词条镶嵌机");
            try { ModHelper.SetModDataBool(ScientistMod.ModId, Progression.InlayerGivenKey, true); } catch { }
            Give(inv, PreBuiltItemHelper.EnergyCredit(100), "电池");
            Give(inv, PreBuiltItemHelper.EnergyCredit(100), "电池");
            Give(inv, ItemSpawner.Spawn("random_performance_module"), "随机性能模组");
            Give(inv, ItemSpawner.Spawn("random_efficiency_module"), "随机效率模组");
            Give(inv, ItemSpawner.Spawn("random_quality_module"), "随机品质模组");
            for (int i = 0; i < 2; i++) Give(inv, DirectoryMaster.Item("common_electronic"), "普通电子元件");
            for (int i = 0; i < 2; i++) Give(inv, DirectoryMaster.Item("metal_ingot"), "金属锭");
            for (int i = 0; i < 2; i++) Give(inv, DirectoryMaster.Item("wire"), "线缆");
            store.playerCash += StartCashChange;
            Log.Info("高级科学家开局：已发放开局物品，资金 " + StartCashChange);
        }

        private static void Give(GameInventory inv, GameItem item, string what)
        {
            if (item == null)
            {
                Log.Warn("开局物品生成失败：" + what);
                return;
            }
            GraphUtils.EmporiumTryAdd(inv, item);
        }
    }
}
