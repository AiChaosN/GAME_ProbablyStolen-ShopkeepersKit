using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedScientist
{
    /// <summary>
    /// 三档空白模组（都是游戏原有的 2×2 “空白模组” blank_module）：
    ///   低级 = 2 个词条槽，中级 = 4 个，高级 = 6 个（0.3.2 起，用户定；之前是 1 / 2 / 3）。
    /// 档次（1 / 2 / 3）存在物品自己的 tag 里（随存档保存），槽数 = 档次 × 2，所以旧存档里做好的空白模组自动变成新槽数；
    /// 名字和价值也一起改；没有这个 tag 的空白模组（游戏原生的）算低级。
    /// 用原有的物品 id 而不是新 id：卸载本 mod 后，中级 / 高级会退回成普通空白模组，不会凭空消失。
    /// </summary>
    internal static class BlankModules
    {
        internal const string VanillaId = "blank_module";
        internal const string SlotsTag = "CHAOS5090_AS_SLOTS";   // 名字沿用旧的，存的是档次 1 / 2 / 3

        /// <summary>档次 1 / 2 / 3；不是空白模组时返回 0，游戏原生的空白模组算 1。</summary>
        internal static int TierOf(GameItem it)
        {
            if (it == null || it.identifier != VanillaId) return 0;
            try { return Math.Max(1, Math.Min(3, it.GetTagReadonly(SlotsTag).GetInt())); }
            catch { return 1; }
        }

        internal static int SlotsForTier(int tier) => Math.Max(1, Math.Min(3, tier)) * 2;

        internal static int SlotsOf(GameItem it)
        {
            int tier = TierOf(it);
            return tier == 0 ? 0 : SlotsForTier(tier);
        }

        internal static string TierName(int tier) =>
            tier >= 3 ? Lang.T("高级空白模组（6 个词条槽）", "High-grade Blank Module (6 slots)")
          : tier == 2 ? Lang.T("中级空白模组（4 个词条槽）", "Mid-grade Blank Module (4 slots)")
                      : Lang.T("低级空白模组（2 个词条槽）", "Low-grade Blank Module (2 slots)");

        // 中级、高级的价值跟材料成本差不多（普通电子元件 33、金属锭 50、线缆 16）：中级 4×99 ≈ 396，高级 6×99 ≈ 594，做出来卖不出差价。
        // 低级保持原版空白模组的 60：提取原生模组也会出低级空白模组，定高了等于凭空送钱。
        internal static long TierValue(int tier) => tier >= 3 ? 590L : tier == 2 ? 390L : 60L;

        internal static GameItem Create(int tier)
        {
            var it = DirectoryMaster.Item(VanillaId);
            if (it == null) return null;
            tier = Math.Max(1, Math.Min(3, tier));
            it.ModifyTag(SlotsTag, s => s.Enable().SetInt(tier));
            it.name = TierName(tier);
            it.unitValue = TierValue(tier);
            return it;
        }
    }

    /// <summary>空白模组制造机的三档配方：普通电子元件 + 金属锭 + 线缆，低 / 中 / 高级各 2 / 4 / 6 个（用户 2026-11-12 定）。</summary>
    internal sealed class Recipe
    {
        internal int Tier;
        internal (string id, int count)[] Needs;

        internal static readonly Recipe[] All =
        {
            new Recipe { Tier = 3, Needs = new[] { ("common_electronic", 6), ("metal_ingot", 6), ("wire", 6) } },
            new Recipe { Tier = 2, Needs = new[] { ("common_electronic", 4), ("metal_ingot", 4), ("wire", 4) } },
            new Recipe { Tier = 1, Needs = new[] { ("common_electronic", 2), ("metal_ingot", 2), ("wire", 2) } },
        };

        internal static readonly HashSet<string> Materials = new HashSet<string>(All.SelectMany(r => r.Needs.Select(n => n.id)));

        internal static string MaterialName(string id)
        {
            switch (id)
            {
                case "rare_electronic": return Lang.T("稀有电子元件", "Rare Electronic Parts");
                case "common_electronic": return Lang.T("普通电子元件", "Common Electronic Parts");
                case "metal_ingot": return Lang.T("金属锭", "Metal Ingot");
                case "wire": return Lang.T("线缆", "Wire");
                case "scrap_metal": return Lang.T("废金属", "Scrap Metal");
                default: return id;
            }
        }

        internal string Describe() => string.Join(" + ", Needs.Select(n => MaterialName(n.id) + "×" + n.count));

        private static IEnumerable<GameItem> Owned(GameInventory inv, string id) =>
            inv.childItems.Where(x => x != null && x.identifier == id && GeneralHelper.IsItemOwned(x)).ToList();

        internal bool CanMake(GameInventory inv) =>
            Needs.All(n => Owned(inv, n.id).Sum(x => Math.Max(1, x.unitCount)) >= n.count);

        /// <summary>按配方扣材料：整件够扣就整件销毁，一叠里只扣一部分就把那部分分出来销毁。</summary>
        internal void Consume(GameInventory inv)
        {
            foreach (var (id, count) in Needs)
            {
                int left = count;
                foreach (var x in Owned(inv, id))
                {
                    if (left <= 0) break;
                    int n = Math.Max(1, x.unitCount);
                    if (n <= left)
                    {
                        left -= n;
                        x.Destroy();
                    }
                    else
                    {
                        var part = x.GetSplitItem(left);
                        part?.Destroy();
                        left = 0;
                    }
                }
            }
        }

        internal static Recipe Best(GameInventory inv) => All.FirstOrDefault(r => r.CanMake(inv));
    }
}
