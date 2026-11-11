using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedScientist
{
    /// <summary>一条词条：基础（性能 / 效率 / 品质三项数值）或特殊词条（游戏的模组词条 id，例如 MODULE_EFFECT_PREMIUM）。</summary>
    internal sealed class Affix
    {
        internal const int Cap = 500;   // 单张基础芯片每项数值上限（用户定的）

        internal bool IsBase;
        internal int P, E, Q;
        internal string Effect;

        internal static Affix Base(int p, int e, int q) => new Affix { IsBase = true, P = Clamp(p), E = Clamp(e), Q = Clamp(q) };
        internal static Affix Special(string id) => new Affix { IsBase = false, Effect = id };

        private static int Clamp(int v) => Math.Max(-Cap, Math.Min(Cap, v));

        internal bool IsEmpty => IsBase ? P == 0 && E == 0 && Q == 0 : string.IsNullOrEmpty(Effect);

        /// <summary>芯片价值：基础芯片 = 三项绝对值之和的一半（一块 16 性能的普通模组 ≈ 8），特殊词条芯片固定 30。</summary>
        internal long Value => IsBase ? Math.Max(1, (Math.Abs(P) + Math.Abs(E) + Math.Abs(Q)) / 2) : 30;

        internal string Serialize() => IsBase ? "B:" + P + "," + E + "," + Q : "E:" + Effect;

        internal static Affix Parse(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 3) return null;
            if (s.StartsWith("B:"))
            {
                var parts = s.Substring(2).Split(',');
                if (parts.Length != 3) return null;
                if (int.TryParse(parts[0], out var p) && int.TryParse(parts[1], out var e) && int.TryParse(parts[2], out var q)) return Base(p, e, q);
                return null;
            }
            if (s.StartsWith("E:")) return Special(s.Substring(2));
            return null;
        }

        internal static string Signed(int v) => v >= 0 ? "+" + v : v.ToString();

        internal string Describe()
        {
            if (IsBase)
                return Lang.T("性能 " + Signed(P) + " / 效率 " + Signed(E) + " / 品质 " + Signed(Q),
                              "Perf " + Signed(P) + " / Eff " + Signed(E) + " / Qual " + Signed(Q));
            return EffectName(Effect);
        }

        internal static string EffectName(string id)
        {
            try
            {
                if (id != null && ModuleEffectHelper.moduleEffects.TryGetValue(id, out var fx) && !string.IsNullOrEmpty(fx.displayName)) return fx.displayName;
            }
            catch { }
            return id ?? "?";
        }

        /// <summary>游戏的特殊词条在生成时把数值乘了一个倍数（ModuleEffectHelper.moduleEffects 的 onCreateAction → SetBaseStatMult）；提取时要除回去。</summary>
        internal static float Multiplier(string effect)
        {
            switch (effect)
            {
                case "MODULE_EFFECT_PREMIUM": return 1.2f;
                case "MODULE_EFFECT_INFERIOR": return 0.8f;
                case "MODULE_EFFECT_OVERVOLTED": return 2f;
                case "MODULE_EFFECT_DEGRADING": return 2f;
                case "MODULE_EFFECT_NEGATIVE_FEEDBACK": return 1.25f;
                default: return 1f;
            }
        }
    }

    /// <summary>两种芯片物品（新物品 id，ModHook.OnModItemDirectoryInit 登记）。芯片内容存在物品自己的 tag 里，名字里写明内容。</summary>
    internal static class Chips
    {
        internal const string BaseId = "chaos5090_as_chip_base";
        internal const string EffectId = "chaos5090_as_chip_effect";
        private const string PTag = "CHAOS5090_AS_P", ETag = "CHAOS5090_AS_E", QTag = "CHAOS5090_AS_Q", EffectTag = "CHAOS5090_AS_EFFECT";

        internal static bool IsChip(GameItem it) => it != null && (it.identifier == BaseId || it.identifier == EffectId);

        internal static GameItem CreateEmptyBase()
        {
            var it = ItemDirectory.CreateEmptyItem().SetSpriteAndShapeFromMod(ScientistMod.ModId, "as_chip_base");
            it.name = Lang.T("基础芯片", "Base Chip");
            it.shortDescription = BaseDesc();
            it.SetGameItemType("MATERIAL");
            it.unitValue = 1L;
            return it;
        }

        internal static GameItem CreateEmptyEffect()
        {
            var it = ItemDirectory.CreateEmptyItem().SetSpriteAndShapeFromMod(ScientistMod.ModId, "as_chip_effect");
            it.name = Lang.T("特殊词条芯片", "Affix Chip");
            it.shortDescription = EffectDesc();
            it.SetGameItemType("MATERIAL");
            it.unitValue = 30L;
            return it;
        }

        internal static string BaseDesc() =>
            Lang.T("记录一组模组数值（性能 / 效率 / 品质）。放进词条镶嵌机，镶到有空词条槽的空白模组上。",
                   "Holds one set of module stats (performance / efficiency / quality). Inlay it into a blank module with a free slot using the Affix Inlayer.");

        internal static string EffectDesc() =>
            Lang.T("记录一个模组特殊词条（优质、过压……）。放进词条镶嵌机，镶到有空词条槽的空白模组上；一个模组最多 2 个特殊词条。",
                   "Holds one special module affix (Premium, Overvolted, ...). Inlay it into a blank module with a free slot using the Affix Inlayer; a module holds at most 2 special affixes.");

        /// <summary>芯片名字（空芯片没有内容时用通用名）。</summary>
        internal static string NameOf(Affix a, bool isBase)
        {
            if (a == null || a.IsEmpty) return isBase ? Lang.T("基础芯片", "Base Chip") : Lang.T("特殊词条芯片", "Affix Chip");
            return a.IsBase ? Lang.T("基础芯片（", "Base Chip (") + a.Describe() + Lang.T("）", ")")
                            : Lang.T("特殊词条芯片（", "Affix Chip (") + a.Describe() + Lang.T("）", ")");
        }

        /// <summary>做一块写好内容的芯片。</summary>
        internal static GameItem Make(Affix a)
        {
            if (a == null || a.IsEmpty) return null;
            var it = DirectoryMaster.Item(a.IsBase ? BaseId : EffectId);
            if (it == null) return null;
            if (a.IsBase)
            {
                it.ModifyTag(PTag, s => s.Enable().SetInt(a.P));
                it.ModifyTag(ETag, s => s.Enable().SetInt(a.E));
                it.ModifyTag(QTag, s => s.Enable().SetInt(a.Q));
                it.name = NameOf(a, true);
            }
            else
            {
                it.ModifyTag(EffectTag, s => s.Enable().SetString(a.Effect));
                it.name = NameOf(a, false);
            }
            it.unitValue = a.Value;
            return it;
        }

        internal static Affix Read(GameItem it)
        {
            if (it == null) return null;
            try
            {
                Affix a = null;
                if (it.identifier == BaseId)
                    a = Affix.Base(it.GetTagReadonly(PTag).GetInt(), it.GetTagReadonly(ETag).GetInt(), it.GetTagReadonly(QTag).GetInt());
                else if (it.identifier == EffectId)
                {
                    var id = it.GetTagReadonly(EffectTag).GetString();
                    if (!string.IsNullOrEmpty(id)) a = Affix.Special(id);
                }
                return a;
            }
            catch { }
            return null;
        }
    }

    /// <summary>
    /// 定制模组 = 用空白模组（blank_module）镶了词条的模组。镶进去的词条按顺序记在模组的 tag 里，
    /// 每次变动都从这份记录重新算：三项数值 = 所有基础芯片相加，再按顺序加特殊词条（用游戏自己的 AddEffectToModule，倍率、互斥、最多 2 个都按游戏规则）。
    /// </summary>
    internal static class CustomModules
    {
        private const string ChipsTag = "CHAOS5090_AS_CHIPS";
        internal const int MaxSpecial = 2;   // 游戏的模组只有两个词条位（MODULE_EFFECT1_TAG / MODULE_EFFECT2_TAG）

        internal static List<Affix> Read(GameItem module)
        {
            var list = new List<Affix>();
            try
            {
                var s = module?.GetTagReadonly(ChipsTag).GetString();
                if (string.IsNullOrEmpty(s)) return list;
                foreach (var part in s.Split(';'))
                {
                    var a = Affix.Parse(part);
                    if (a != null) list.Add(a);
                }
            }
            catch { }
            return list;
        }

        internal static int FreeSlots(GameItem module) => Math.Max(0, BlankModules.SlotsOf(module) - Read(module).Count);

        /// <summary>按当前规则刷新名字（读档时用：槽数从 1/2/3 改成 2/4/6 后，旧的空白模组名字里还写着旧槽数）。</summary>
        internal static void RefreshName(GameItem module)
        {
            int tier = BlankModules.TierOf(module);
            if (tier == 0) return;
            int n = Read(module).Count, slots = BlankModules.SlotsForTier(tier);
            string want = n == 0 ? BlankModules.TierName(tier)
                : Lang.T("定制模组（已镶 " + n + "/" + slots + "）", "Custom Module (" + n + "/" + slots + " slots used)");
            if (module.name != want) module.name = want;
        }

        /// <summary>这个特殊词条还能不能加到这份记录上（不重复、不和已有的互斥、特殊词条不超过 2 个、只能用在模组上的词条）。</summary>
        internal static bool CanAddSpecial(List<Affix> list, string effect)
        {
            if (string.IsNullOrEmpty(effect)) return false;
            if (!ModuleEffectHelper.moduleEffects.TryGetValue(effect, out var fx) || fx.effectType == ModuleEffectHelper.ModuleEffect.ModuleEffectType.NodeOnly) return false;
            var specials = list.Where(a => !a.IsBase).Select(a => a.Effect).ToList();
            if (specials.Count >= MaxSpecial || specials.Contains(effect)) return false;
            foreach (var other in specials)
            {
                if (fx.incompatibleWithEffects != null && fx.incompatibleWithEffects.Contains(other)) return false;
                if (ModuleEffectHelper.moduleEffects.TryGetValue(other, out var ofx) && ofx.incompatibleWithEffects != null && ofx.incompatibleWithEffects.Contains(effect)) return false;
            }
            return true;
        }

        /// <summary>写入记录并重算模组：数值、特殊词条、价值、名字。</summary>
        internal static void Write(GameItem module, List<Affix> list)
        {
            int tier = Math.Max(1, BlankModules.TierOf(module));
            int slots = BlankModules.SlotsForTier(tier);
            var bases = list.Where(a => a.IsBase).ToList();
            var specials = list.Where(a => !a.IsBase).ToList();
            var ordered = bases.Concat(specials).ToList();
            module.ModifyTag(ChipsTag, s => s.Enable().SetString(string.Join(";", ordered.Select(a => a.Serialize()))));
            if (ordered.Count == 0) module.DisableTag(ChipsTag);

            int p = bases.Sum(a => a.P), e = bases.Sum(a => a.E), q = bases.Sum(a => a.Q);
            module.ModifyTag("BONUS_PERCENTAGE_PERFORMANCE_INT", s => s.Enable().SetInt(p));
            module.ModifyTag("BONUS_PERCENTAGE_EFFICIENCY_INT", s => s.Enable().SetInt(e));
            module.ModifyTag("BONUS_PERCENTAGE_QUALITY_INT", s => s.Enable().SetInt(q));
            foreach (var tag in new[] { "MODULE_EFFECT1_TAG", "MODULE_EFFECT2_TAG" })
            {
                if (!module.IsTag(tag)) continue;
                module.ModifyTag(tag, s => s.SetString(""));
                module.DisableTag(tag);
            }
            foreach (var tag in new[] { ModuleEffectHelper.LEARNING_ALGO_MAX_PERFORMANCE_INT, ModuleEffectHelper.LEARNING_ALGO_MAX_EFFICIENCY_INT, ModuleEffectHelper.LEARNING_ALGO_MAX_QUALITY_INT })
                if (module.IsTag(tag)) module.DisableTag(tag);

            module.unitValue = BlankModules.TierValue(tier) + ordered.Sum(a => a.Value);
            foreach (var a in specials) ModuleEffectHelper.AddEffectToModule(module, a.Effect);   // 倍率、价值调整都按游戏规则
            ModuleEffectHelper.ResetAllTempStat(module);

            module.name = ordered.Count == 0 ? BlankModules.TierName(tier)
                : Lang.T("定制模组（已镶 " + ordered.Count + "/" + slots + "）", "Custom Module (" + ordered.Count + "/" + slots + " slots used)");
            try { module.Validate(); } catch { }
        }

        /// <summary>
        /// 从一个模组提取词条。定制模组：按记录原样取出。游戏原生模组：特殊词条各成一块芯片；基础数值除掉特殊词条的倍率
        /// （学习算法的数值存在 LEARNING_ALGO_MAX_*，是原值的 2 倍）。
        /// </summary>
        internal static List<Affix> Extract(GameItem module)
        {
            var custom = Read(module);
            if (custom.Count > 0) return custom;

            var effects = new List<string>();
            foreach (var tag in new[] { "MODULE_EFFECT1_TAG", "MODULE_EFFECT2_TAG" })
            {
                try
                {
                    if (!module.IsTag(tag)) continue;
                    var id = module.GetTagReadonly(tag).GetString();
                    if (!string.IsNullOrEmpty(id)) effects.Add(id);
                }
                catch { }
            }
            int p, e, q;
            if (effects.Contains("MODULE_EFFECT_LEARNING_ALGORITHM"))
            {
                p = module.GetTagReadonly(ModuleEffectHelper.LEARNING_ALGO_MAX_PERFORMANCE_INT).GetInt() / 2;
                e = module.GetTagReadonly(ModuleEffectHelper.LEARNING_ALGO_MAX_EFFICIENCY_INT).GetInt() / 2;
                q = module.GetTagReadonly(ModuleEffectHelper.LEARNING_ALGO_MAX_QUALITY_INT).GetInt() / 2;
            }
            else
            {
                float mult = effects.Aggregate(1f, (m, id) => m * Affix.Multiplier(id));
                p = (int)Math.Round(module.GetTagReadonly("BONUS_PERCENTAGE_PERFORMANCE_INT").GetInt() / mult);
                e = (int)Math.Round(module.GetTagReadonly("BONUS_PERCENTAGE_EFFICIENCY_INT").GetInt() / mult);
                q = (int)Math.Round(module.GetTagReadonly("BONUS_PERCENTAGE_QUALITY_INT").GetInt() / mult);
            }
            var list = new List<Affix>();
            var b = Affix.Base(p, e, q);
            if (!b.IsEmpty) list.Add(b);
            foreach (var id in effects)
            {
                if (ModuleEffectHelper.moduleEffects.TryGetValue(id, out var fx) && fx.effectType == ModuleEffectHelper.ModuleEffect.ModuleEffectType.NodeOnly) continue;
                list.Add(Affix.Special(id));
            }
            return list;
        }

        /// <summary>
        /// 能放进提取机的模组：自己的、通用型的机器模组（游戏的 性能 / 效率 / 品质 / 超频 / 节能 / 精细 / 流线 / 神经核心，以及定制模组）。
        /// 违禁品（神经核心）和赃物也收，提取出来的芯片和空白模组都是干净的（用户 2026-11-12 定：不让违禁 / 赃物标记跟着走）。
        /// 不收：功能型模组（化学、农业、熔炉……带 ADDITIONAL_EFFECT_STRING）、节点、损坏 / 报废的、什么都提不出的。
        /// </summary>
        internal static bool Extractable(GameItem it) => RefuseReason(it) == null;

        /// <summary>不能提取的原因（给玩家看）；能提取时返回 null。</summary>
        internal static string RefuseReason(GameItem it)
        {
            try
            {
                if (it == null) return Lang.T("没有东西", "Nothing");
                if (!GeneralHelper.IsItemOwned(it)) return Lang.T("这不是你的东西", "That isn't yours");
                if (!it.IsGameItemType("MODULE") || !it.IsTag("MODULE_TAG")) return Lang.T("词条提取机只收模组", "The Affix Extractor only takes modules");
                if (!it.IsTag("SHIP_MODULE_TAG") || it.GetTagReadonly("MODULE_TYPE").GetString() != "MODULE_TYPE_UNIVERSAL" || it.IsTag("ADDITIONAL_EFFECT_STRING"))
                    return Lang.T("这是功能型模组（化学、农业、熔炉……），按“装了几个”起作用，没有可以提取的数值", "This is a functional module (chemistry, farming, furnace, ...); it works by count and has no stats to extract");
                if (it.IsGameItemType("NODE")) return Lang.T("节点没有自己的数值，不能提取", "Nodes have no stats of their own");
                if (it.identifier == "system_module_corrupt" || it.identifier == "system_module_ruined") return Lang.T("损坏 / 报废的模组不能提取", "Corrupt or ruined modules can't be extracted");
                if (Extract(it).Count == 0) return Lang.T("这个模组没有数值也没有词条，提取不出东西", "This module has no stats or affixes to extract");
                return null;
            }
            catch { return Lang.T("这个模组不能提取", "This module can't be extracted"); }
        }
    }
}
