using System;
using System.Linq;
using HarmonyLib;

namespace Chef
{
    /// <summary>
    /// 食用油是游戏液体系统里的新液体（用户 2026-11-12：直接当成一种新的液体，能装进游戏的桶、瓶子里，油锅里放一个桶）。
    /// 往游戏的液体表 Liquid.Liquids / LiquidDict 里加四种成分（游戏到处都按当前表的长度现算，加在后面是安全的）：
    ///   食用油 —— 真正的油，纯净度 = 它占的比例；
    ///   老化油 —— 每炸一晚 4% 的食用油变成老化油，颗粒很小，滤不掉（所以“二次食用油”回不到 100%）；
    ///   大颗粒炸渣 —— 颗粒 200：滤油网（过滤尺寸 150）和滤水器（22）都能滤，但很伤滤水器；
    ///   小颗粒油渣 —— 颗粒 40：只有滤水器滤得掉。
    /// 过滤就是游戏自己的滤水壶：油倒进装着滤网 / 滤水器的滤水壶时，游戏的 WaterHelper.HandleFilter 按颗粒大小去掉成分、扣滤芯耐久。
    /// 油里不能有水：有水油锅就不炸。
    ///
    /// 游戏会把所有液体容器当成“水”：按水的纯净度显示“沟渠水”、定价、算水类商品。以油为主的容器用 Harmony 补丁改掉
    /// （关掉水的纯净度特征、按油定价、不算水类商品），见 Patches。
    /// </summary>
    internal static class Oil
    {
        internal const string OilId = "chaos5090_cooking_oil";
        internal const string AgedId = "chaos5090_aged_oil";
        internal const string CrumbsId = "chaos5090_fry_crumbs";
        internal const string ResidueId = "chaos5090_fry_residue";
        private static readonly string[] Ids = { OilId, AgedId, CrumbsId, ResidueId };

        internal const int Unit = 1000;                // 游戏的液体单位：1 ml = 1000
        internal const int AbsorbMl = 100;             // 每炸一份吸掉的油（按比例从整桶里取）
        internal const int CrumbsMl = 12;              // 每炸一份产生的大颗粒炸渣
        internal const int ResidueMl = 6;              // 每炸一份产生的小颗粒油渣
        internal const double AgingPerNight = 0.04;    // 每炸一晚，食用油里变成老化油的比例
        internal const int MinToFryMl = 1000;          // 桶里至少要有这么多才炸

        internal static string OilName() => Lang.T("食用油", "Cooking oil");
        private static string AgedName() => Lang.T("老化油", "Aged oil");
        private static string CrumbsName() => Lang.T("大颗粒炸渣", "Large fry crumbs");
        private static string ResidueName() => Lang.T("小颗粒油渣", "Fine oil residue");

        /// <summary>登记四种液体（只登记一次；之后只刷新显示名字，切换语言后跟着变）。</summary>
        internal static void Register()
        {
            Add(OilId, OilName(), particle: 5, color: "d9a62e", filterCost: 2);
            Add(AgedId, AgedName(), particle: 5, color: "8a5a1c", filterCost: 2);
            Add(CrumbsId, CrumbsName(), particle: 200, color: "5a3a1e", filterCost: 5);
            Add(ResidueId, ResidueName(), particle: 40, color: "7a5a34", filterCost: 2);
        }

        private static void Add(string id, string name, int particle, string color, int filterCost)
        {
            if (Liquid.LiquidDict.TryGetValue(id, out var existing))
            {
                existing.displayName = name;
                return;
            }
            var l = new Liquid
            {
                displayName = name,
                identifier = id,
                part = "PART_" + id.ToUpperInvariant(),
                currentPart = "CURRENT_PART_" + id.ToUpperInvariant(),
                particleSize = particle,
                color = color,
                colorMultiplier = 1,
                canBeRemovedByPurifier = false,   // 净水器不碰油（它是按“水的纯净度”去除杂质的）
                filterMinimumPurity = 0,          // 能滤的全部滤掉
                filterDurabilityUsage = filterCost,
                filterVolumeRemoved = Unit,       // 每滤 1 ml 扣 filterCost 点耐久
            };
            Liquid.Liquids.Add(l);
            Liquid.LiquidDict[id] = l;
        }

        // ------------------------------------------------------------------ 读容器

        internal struct Mix
        {
            internal int Oil, Aged, Crumbs, Residue, Water, Other;   // 单位：游戏液体单位
            internal int OilParts => Oil + Aged + Crumbs + Residue;
            internal int Total => OilParts + Water + Other;
            internal double Purity => Total <= 0 ? 0 : (double)Oil / Total;
            internal int Ml(int units) => units / Unit;
        }

        internal static Mix Read(GameItem c)
        {
            var m = new Mix();
            if (c == null || !c.IsTag("LIQUID_CONTAINER_TAG")) return m;
            foreach (var l in Liquid.Liquids)
            {
                int v = c.GetTagReadonly(l.currentPart).GetInt();
                if (v <= 0) continue;
                switch (l.identifier)
                {
                    case OilId: m.Oil += v; break;
                    case AgedId: m.Aged += v; break;
                    case CrumbsId: m.Crumbs += v; break;
                    case ResidueId: m.Residue += v; break;
                    case "water": m.Water += v; break;
                    default: m.Other += v; break;
                }
            }
            return m;
        }

        /// <summary>容器里主要是油（油的四种成分加起来超过一半）：按油显示、定价。</summary>
        internal static bool IsOilContainer(GameItem c)
        {
            if (c == null || !c.IsTag("LIQUID_CONTAINER_TAG")) return false;
            var m = Read(c);
            return m.Total > 0 && m.OilParts * 2 > m.Total;
        }

        /// <summary>能不能拿来炸：要有油、不能有水。返回不能炸的原因，能炸返回 null。</summary>
        internal static string CannotFry(Mix m)
        {
            if (m.Total <= 0) return Lang.T("桶里没有油", "The container is empty");
            if (m.Water > 0) return Lang.T("油里混了水，不能炸", "There's water in the oil; it can't be used for frying");
            if (m.Oil <= 0) return Lang.T("桶里没有食用油", "There's no cooking oil in the container");
            if (m.Total < MinToFryMl * Unit) return Lang.T("油不够（至少 " + MinToFryMl + " ml 才炸）", "Not enough oil (needs at least " + MinToFryMl + " ml)");
            return null;
        }

        /// <summary>这桶油能炸几份。</summary>
        internal static int Portions(Mix m) => CannotFry(m) != null ? 0 : (m.Total - MinToFryMl * Unit) / (AbsorbMl * Unit) + 1;

        /// <summary>炸完一晚：每份按比例吸掉 100 ml，食用油老化 4%，加进炸渣和油渣。</summary>
        internal static void AfterFrying(GameItem c, int portions)
        {
            if (c == null || portions <= 0) return;
            // 吸油：按比例从所有成分里取
            int[] parts = WaterHelper.GetLiquidParts(c);
            int[] taken = WaterHelper.SplitProportionally(parts, portions * AbsorbMl * Unit);
            for (int i = 0; i < Liquid.Liquids.Count; i++)
            {
                if (taken[i] <= 0) continue;
                int left = Math.Max(0, parts[i] - taken[i]);
                c.ModifyTag(Liquid.Liquids[i].currentPart, s => s.Enable().SetInt(left), skipValidate: true);
            }
            var m = Read(c);
            int aged = (int)(m.Oil * AgingPerNight);
            Set(c, OilId, m.Oil - aged);
            Set(c, AgedId, m.Aged + aged);
            Set(c, CrumbsId, m.Crumbs + portions * CrumbsMl * Unit);
            Set(c, ResidueId, m.Residue + portions * ResidueMl * Unit);
            Changed(c);
        }

        internal static void Fill(GameItem c, int ml)
        {
            Set(c, OilId, Math.Min(ml * Unit, c.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").GetInt()));
            Changed(c);
        }

        private static void Set(GameItem c, string id, int units) =>
            c.ModifyTag(Liquid.GetCurrentPartFromId(id), s => s.Enable().SetInt(Math.Max(0, units)), skipValidate: true);

        /// <summary>液体变了：走游戏自己的刷新（显示、价格、贴图颜色），补丁会按油改掉。</summary>
        private static void Changed(GameItem c)
        {
            try { WaterFeatureHelper.OnLiquidChanged(c, exposeFeature: false); }
            catch (Exception e) { Log.Warn("刷新容器出错: " + e.Message); }
            try { c.Validate(); } catch { }
        }

        // ------------------------------------------------------------------ 显示

        internal static string Percent(double p) => (p * 100).ToString("0.0") + "%";

        internal static string Ml(int units)
        {
            int ml = units / Unit;
            return ml >= 1000 ? (ml / 1000.0).ToString("0.0#") + " L" : ml + " ml";
        }

        internal static string StateName(Mix m)
        {
            if (m.Total <= 0) return Lang.T("空的", "Empty");
            if (m.Water > 0) return Lang.T("混了水的油", "Oil with water in it");
            if (m.Purity < 0.70) return Lang.T("废油", "Spent oil");
            if (m.Crumbs > 0 || m.Residue > 0) return Lang.T("用过的油", "Used oil");
            if (m.Aged > 0) return Lang.T("二次食用油", "Recycled oil");
            return Lang.T("新食用油", "Fresh oil");
        }

        internal static string Amount(int units)
        {
            int ml = units / Unit;
            if (units <= 0) return Lang.T("没有", "none");
            string n = Lang.T("（" + ml + " ml）", " (" + ml + " ml)");
            if (ml < 50) return Lang.T("少量", "a little") + n;
            if (ml < 200) return Lang.T("中等", "some") + n;
            return Lang.T("很多", "a lot") + n;
        }

        /// <summary>鼠标指着装油的容器（或放着油桶的油锅）时的说明。</summary>
        internal static void Tooltip(RichTextBuilder b, Mix m, bool isFryer)
        {
            if (m.OilParts <= 0 && !isFryer) return;
            b.AddLine(" " + OilName() + " " + Ml(m.Total) + " · " + StateName(m), true, RenderHandler.ColorPalette.LightYellow);
            if (m.Total <= 0) return;
            var color = m.Water > 0 || m.Purity < 0.85 ? RenderHandler.ColorPalette.LightRed : m.Purity >= 0.95 ? RenderHandler.ColorPalette.LightGreen : RenderHandler.ColorPalette.LightYellow;
            b.AddLine(" " + Lang.T("油的纯净度 ", "Oil purity ") + Percent(m.Purity), true, color);
            b.AddLine(" " + CrumbsName() + Lang.T("：", ": ") + Amount(m.Crumbs) + " · " + ResidueName() + Lang.T("：", ": ") + Amount(m.Residue), true, RenderHandler.ColorPalette.LightGray);
            if (m.Aged > 0) b.AddLine(" " + AgedName() + Lang.T("（滤不掉）：", " (can't be filtered): ") + Ml(m.Aged), true, RenderHandler.ColorPalette.LightGray);
            if (m.Water > 0) b.AddLine(" " + Lang.T("混了水 " + Ml(m.Water) + "：油锅不能用", Ml(m.Water) + " of water mixed in: the fryer won't use it"), true, RenderHandler.ColorPalette.LightRed);
        }

        /// <summary>油的价值：食用油每升 12、老化油每升 4，渣子不值钱。</summary>
        internal static long Value(Mix m) => (long)m.Oil * 12 / (1000L * Unit) + (long)m.Aged * 4 / (1000L * Unit);
    }

    /// <summary>
    /// 让以油为主的液体容器不被当成水（游戏的 WaterFeatureHelper / WaterHelper 只认识水）。
    /// 都是后置补丁：先让游戏按水算完，再按油改掉——
    ///   关掉“水的纯净度”特征（不再显示“沟渠水 纯度 90% 以下”、不按水的纯净度加减价），
    ///   价值按油算，商品类别从“水”改成“家居用品”（买水的顾客不会来要）。
    /// 找不到要补的函数就跳过（游戏更新改了名字时不至于崩），日志里会记。
    /// </summary>
    internal static class Patches
    {
        private const string HarmonyId = "chaos5090.chef";
        private static Harmony harmony;

        internal static void Apply()
        {
            if (harmony != null) return;
            harmony = new Harmony(HarmonyId);
            var post = new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(AfterWaterUpdate)));
            int n = 0;
            foreach (var m in new[]
            {
                AccessTools.Method(typeof(WaterFeatureHelper), "OnLiquidChanged"),
                AccessTools.Method(typeof(WaterFeatureHelper), "InitWaterFeature"),
                AccessTools.Method(typeof(WaterFeatureHelper), "UpdateWaterFeature"),
                AccessTools.Method(typeof(WaterHelper), "UpdateContainerGoodType"),
            })
            {
                if (m == null) continue;
                try
                {
                    harmony.Patch(m, postfix: post);
                    n++;
                }
                catch (Exception e) { Log.Warn("补丁失败 " + m.Name + ": " + e.Message); }
            }
            Log.Info("油的显示 / 定价补丁：" + n + "/4");
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            harmony = null;
        }

        // 四个函数的第一个参数都是容器
        private static void AfterWaterUpdate(GameItem __0)
        {
            try
            {
                var c = __0;
                if (!Oil.IsOilContainer(c)) return;
                var f = c.FindItemFeatureByCategory("CATEGORY_WATER_PURITY");
                if (f != null) f.isDisabled = true;
                c.unitValue = Oil.Value(Oil.Read(c));
                if (c.IsGameItemType("WATER"))
                {
                    c.RemoveGameItemType("WATER");
                    c.SetGameItemType("HOUSEHOLD_GOOD");
                }
            }
            catch { }
        }
    }
}
