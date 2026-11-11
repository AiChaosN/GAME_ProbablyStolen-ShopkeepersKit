using System;

namespace Chef
{
    /// <summary>
    /// 两条路拿到厨具：
    ///   - 职业“厨师”（ModHelper.AddStartingScenario，职业选择界面），开局全套；
    ///   - 别的职业：花王尔德的恩惠买人脉“退休大厨”（ModHelper.AddNetworkUpgrade，和游戏的“退休农夫 / 退休化学家”一样），立刻送一套入门厨具。
    /// 厨师开局时把“退休大厨”直接设成已解锁（state = 2，和游戏给农夫职业预先解锁“退休农夫”一样），不会再送一套。
    ///
    /// 人脉升级不能在 mod 初始化（主菜单）时登记：ModHelper.AddNetworkUpgrade 最后会读 PlayerStore.Instance，
    /// 主菜单里还没有存档，这个 getter 会去 new 一个 PlayerStore，在构造函数里崩掉（0.1.0 的日志里就是这样）。
    /// 所以等存档建好（读档 OnGameLoadedInit、新开局 OnNewGameStarted、开局发放 OnStart）再登记；
    /// 游戏读档时会把登记过的升级补进存档（NetworkUpgrade.AddMissingAfterLoad），重复登记没关系。
    /// </summary>
    internal static class Scenario
    {
        internal const string Id = "chaos5090_chef";
        internal const string IconSprite = "chef_icon";
        internal const string UpgradeId = "CHAOS5090_CHEF_RETIRED_CHEF";
        private const int UpgradeCost = 600;          // 退休农夫 350、养殖 750、退休化学家 1000
        private const int StartCashChange = -200;     // 开局厨具和一桶油价值较高

        internal static void Register(string modId)
        {
            ModHelper.AddStartingScenario(modId, Id,
                () => Lang.T("厨师", "Chef"),
                () => Lang.T("开快餐店的厨师。油锅把营养果炸成薯条、把肉炸成炸鸡；食品加工器把肉、营养果、填料草做成肉汉堡，再配成超级大套餐。"
                             + "品质看肉的千卡、油的纯净度、食材比例和机器上的模组，米其林级的菜价格 +200%。食用油炸多了会脏：大颗粒用滤油网滤，小颗粒用滤水壶滤。",
                             "A fast-food chef. The deep fryer turns nutrifruit into fries and meat into fried chicken; the food processor turns meat, nutrifruit and fillerweed into burgers and plates super combos. "
                             + "Quality depends on the meat's calories, oil purity, ingredient ratio and the machines' modules; Michelin-grade food sells for +200%. Oil gets dirty with use: strain large crumbs with the strainer and fine residue with a water pitcher."),
                new Func<string>[]
                {
                    () => Lang.T("油锅、食品加工器（各附一块电池）", "Deep fryer and food processor (each with a battery)"),
                    () => Lang.T("两桶食用油（各 10 L）、一张滤油网", "Two jugs of cooking oil (10 L each) and an oil strainer"),
                    () => Lang.T("厨师菜谱", "Chef's recipe book"),
                    () => Lang.T("一些营养果、填料草和肉", "Some nutrifruit, fillerweed and meat"),
                },
                OnStart,
                iconKey: modId + ":" + IconSprite,
                tag: () => Lang.T("快餐", "Fast food"),
                tagTone: StartingScenario.TagTone.Positive);
        }

        /// <summary>登记王尔德的人脉升级“退休大厨”。只在存档已经建好时做（见类说明）。</summary>
        internal static void EnsureUpgrade()
        {
            if (Game.Store == null) return;
            try { RegisterUpgrade(ChefMod.ModId); }
            catch (Exception e) { Log.Error("登记人脉升级“退休大厨”失败: " + Log.Describe(e)); }
        }

        private static void RegisterUpgrade(string modId)
        {
            ModHelper.AddNetworkUpgrade(modId, UpgradeId, UpgradeCost, "Chef",
                () => Lang.T("退休大厨", "Retired Chef"),
                () => Lang.T("立即获取一套入门级厨具：油锅、食品加工器、一桶食用油、一张滤油网和厨师菜谱。（解锁快餐内容。）",
                             "Immediately receive a starter set of kitchen equipment: a deep fryer, a food processor, a jug of cooking oil, an oil strainer and the chef's recipe book. (Unlocks fast-food content.)"),
                OnUnlock,
                iconKey: modId + ":" + IconSprite,
                subtitleLabel: () => Lang.T("烹饪", "Cooking"));
        }

        internal static bool IsChef()
        {
            try { return ModHelper.IsStartingScenario(Id); }
            catch { return false; }
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
            Give(inv, DirectoryMaster.Item(Machines.FryerId), "油锅");
            Give(inv, DirectoryMaster.Item(Machines.ProcessorId), "食品加工器");
            Give(inv, PreBuiltItemHelper.EnergyCredit(100), "电池");
            Give(inv, PreBuiltItemHelper.EnergyCredit(100), "电池");
            Give(inv, DirectoryMaster.Item(Items.BarrelId), "食用油桶");
            Give(inv, DirectoryMaster.Item(Items.BarrelId), "食用油桶");
            Give(inv, DirectoryMaster.Item(Items.StrainerId), "滤油网");
            Give(inv, Recipe.Spawn(), "厨师菜谱");
            for (int i = 0; i < 4; i++) Give(inv, DirectoryMaster.Item(Ingredients.Nutrifruit), "营养果");
            for (int i = 0; i < 2; i++) Give(inv, DirectoryMaster.Item(Ingredients.Fillerweed), "填料草");
            Give(inv, DirectoryMaster.Item("raw_meat"), "生肉（标准）");
            for (int i = 0; i < 2; i++) Give(inv, DirectoryMaster.Item("fat_meat"), "肥肉");
            Give(inv, DirectoryMaster.Item("processed_meat"), "泥炭合成肉");
            store.playerCash += StartCashChange;
            EnsureUpgrade();
            try
            {
                var up = ModHelper.GetNetworkUpgrade(UpgradeId);
                if (up != null) up.state = 2;   // 厨师已经有全套，不再卖“退休大厨”
            }
            catch (Exception e) { Log.Warn("把“退休大厨”设成已解锁失败: " + e.Message); }
            Log.Info("厨师开局：已发放开局物品，资金 " + StartCashChange);
        }

        private static void OnUnlock()
        {
            try
            {
                foreach (var (item, what) in new[]
                {
                    (DirectoryMaster.Item(Machines.FryerId), "油锅"),
                    (DirectoryMaster.Item(Machines.ProcessorId), "食品加工器"),
                    (DirectoryMaster.Item(Items.BarrelId), "食用油桶"),
                    (DirectoryMaster.Item(Items.StrainerId), "滤油网"),
                    (Recipe.Spawn(), "厨师菜谱"),
                })
                {
                    if (item == null) Log.Warn("退休大厨：生成失败 " + what);
                    else GraphUtils.EmporiumTryAdd(item);
                }
                Log.Info("退休大厨：送了一套入门厨具");
                Ui.Notify(Lang.T("退休大厨送来一套厨具，在仓库里", "The retired chef sent a set of kitchen equipment to your storage"));
            }
            catch (Exception e) { Log.Error("退休大厨送厨具出错: " + Log.Describe(e)); }
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
