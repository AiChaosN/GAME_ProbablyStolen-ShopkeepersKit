using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedScientist
{
    /// <summary>
    /// 怎么拿到机器：
    ///   - 杰克逊博士（夜里去他店里、或白天他来店里推销时）给高级科学家卖词条提取机，你还没有一台时才摆出来。
    ///     用游戏的 ModHook.OnPlaceInventorInventoryItemLate（他摆完自己的货之后），走 PlayerStore.AddDirectSellingItemToTable，和他其他的货一样买。
    ///   - 词条镶嵌机是开局物品；0.1.0 开局的高级科学家存档读档时补发一台（只发一次，记在存档的 ModData 里）。
    /// </summary>
    internal static class Progression
    {
        internal const string InlayerGivenKey = "inlayer_given";

        internal static void OnJacksonStock(List<GameItem> owned)
        {
            try
            {
                if (!ModHelper.IsStartingScenario(Scenario.Id)) return;
                var store = Game.Store;
                if (store == null) return;
                bool hasExtractor = owned != null && owned.Any(i => i != null && i.identifier == AffixMachines.ExtractorId);
                if (hasExtractor) return;
                var item = DirectoryMaster.Item(AffixMachines.ExtractorId);
                if (item == null)
                {
                    Log.Warn("杰克逊博士的货：生成词条提取机失败（新物品没登记上？）");
                    return;
                }
                store.AddDirectSellingItemToTable(item);
                Log.Info("杰克逊博士的货：加上词条提取机");
            }
            catch (Exception e)
            {
                Log.Warn("给杰克逊博士加货出错: " + e.Message);
            }
        }

        /// <summary>按当前版本刷新本 mod 物品的名字和说明；是本 mod 的物品返回 true。</summary>
        internal static bool RefreshTexts(GameItem it)
        {
            if (it == null) return false;
            switch (it.identifier)
            {
                case BlankModuleMaker.Id:
                    Set(it, BlankModuleMaker.Name(), BlankModuleMaker.Describe());
                    return true;
                case AffixMachines.ExtractorId:
                    Set(it, AffixMachines.ExtractorName(), AffixMachines.ExtractorDesc());
                    return true;
                case AffixMachines.InlayerId:
                    Set(it, AffixMachines.InlayerName(), AffixMachines.InlayerDesc());
                    return true;
                case Chips.BaseId:
                    Set(it, Chips.NameOf(Chips.Read(it), true), Chips.BaseDesc());
                    return true;
                case Chips.EffectId:
                    Set(it, Chips.NameOf(Chips.Read(it), false), Chips.EffectDesc());
                    return true;
                case BlankModules.VanillaId:
                    if (!it.IsTag(BlankModules.SlotsTag)) return false;   // 游戏原生的空白模组不动
                    CustomModules.RefreshName(it);
                    return true;
                default:
                    return false;
            }
        }

        private static void Set(GameItem it, string name, string desc)
        {
            if (it.name != name) it.name = name;
            if (it.shortDescription != desc) it.shortDescription = desc;
        }

        internal static void OnGameLoaded()
        {
            // 游戏存档会连物品的名字和说明一起存下来，读档时用存档里的旧文字（SaveManager 里 name / shortDescription 照存档恢复），
            // 所以改了配方、槽数或游戏语言后，已有的机器、芯片、空白模组还显示旧的。这里按当前版本和语言重新写一遍。
            try
            {
                var store = Game.Store;
                if (store != null)
                {
                    int n = 0;
                    foreach (var it in store.FindAllItem(false))
                        if (RefreshTexts(it)) n++;
                    if (n > 0) Log.Info("刷新了 " + n + " 件本 mod 物品的名字和说明");
                }
            }
            catch (Exception e) { Log.Warn("刷新物品说明出错: " + e.Message); }

            try
            {
                if (!ModHelper.IsStartingScenario(Scenario.Id)) return;
                if (ModHelper.GetModDataBool(ScientistMod.ModId, InlayerGivenKey)) return;
                var item = DirectoryMaster.Item(AffixMachines.InlayerId);
                if (item == null)
                {
                    Log.Warn("补发词条镶嵌机失败（新物品没登记上？）");
                    return;
                }
                GraphUtils.EmporiumTryAdd(item);
                ModHelper.SetModDataBool(ScientistMod.ModId, InlayerGivenKey, true);
                Log.Info("补发词条镶嵌机（这个存档是旧版开局的）");
                try { StoreUIManager.Instance?.Notify(Lang.T("高级科学家：补发了一台词条镶嵌机，在仓库里", "Advanced Scientist: an Affix Inlayer was added to your storage")); } catch { }
            }
            catch (Exception e)
            {
                Log.Warn("补发词条镶嵌机出错: " + e.Message);
            }
        }
    }
}
