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

        internal static void OnGameLoaded()
        {
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
