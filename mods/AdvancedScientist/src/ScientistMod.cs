using System;

namespace AdvancedScientist
{
    /// <summary>
    /// 入口。ModLoader 会对本程序集调用 Harmony.PatchAll（本 mod 不用 [HarmonyPatch] 特性，那一步什么也不做），然后依次调用 Init / OnEnable。
    /// 本 mod 只用游戏的官方接口：ModHelper.AddStartingScenario（职业）、ModHook.OnModItemDirectoryInit（新物品）、
    /// ModHook.OnGoingSleepLate（每晚结算之后）。
    /// </summary>
    public sealed class ScientistMod : IMod
    {
        internal static ScientistConfig Config;
        internal static string ModId = "chaos5090.advanced-scientist";
        private static bool hooked;
        private static UnityEngine.GameObject host;

        public void Init(ModManifest manifest)
        {
            ModId = manifest?.ID ?? ModId;
            Config = new ScientistConfig();
            try { Config.Init(ModId); }
            catch (Exception e) { Log.Warn("配置初始化失败，使用默认值: " + e.Message); }

            try
            {
                if (!hooked)
                {
                    ModHook.OnModItemDirectoryInit += RegisterItems;
                    ModHook.OnGoingSleepLate += TwoDays.OnGoingSleepLate;
                    ModHook.OnPlaceInventorInventoryItemLate += Progression.OnJacksonStock;
                    ModHook.OnGameLoadedLate += Progression.OnGameLoaded;
                    hooked = true;
                }
            }
            catch (Exception e) { Log.Error("订阅游戏事件失败: " + Log.Describe(e)); }

            // 物品目录如果已经建好（例如开发者模式重新加载），直接登记
            try
            {
                var dir = UnityEngine.Object.FindObjectOfType<ModItemDirectory>();
                if (dir != null && dir.factoryDictionary != null) RegisterItems(dir);
            }
            catch (Exception e) { Log.Warn("查找物品目录失败（等游戏建好后再登记）: " + e.Message); }

            try
            {
                if (host == null)
                {
                    host = new UnityEngine.GameObject("AdvancedScientist");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    host.AddComponent<ScientistBehaviour>();   // 机器窗口上的“制造 / 提取 / 镶嵌”按钮
                }
            }
            catch (Exception e) { Log.Error("创建按钮对象失败: " + Log.Describe(e)); }

            try { Scenario.Register(ModId); }
            catch (Exception e) { Log.Error("登记职业失败: " + Log.Describe(e)); }

            Log.Info("已加载：职业“高级科学家”、空白模组制造机、词条提取机、词条镶嵌机；机器区一天两天" + (Config.TwoDays ? "开" : "关"));
        }

        private static void RegisterItems(ModItemDirectory dir)
        {
            try
            {
                if (dir == null) return;
                var items = new (string id, Func<GameItem> make, string name)[]
                {
                    (BlankModuleMaker.Id, BlankModuleMaker.Create, "空白模组制造机"),
                    (AffixMachines.ExtractorId, AffixMachines.CreateExtractor, "词条提取机"),
                    (AffixMachines.InlayerId, AffixMachines.CreateInlayer, "词条镶嵌机"),
                    (Chips.BaseId, Chips.CreateEmptyBase, "基础芯片"),
                    (Chips.EffectId, Chips.CreateEmptyEffect, "特殊词条芯片"),
                };
                var done = new System.Collections.Generic.List<string>();
                foreach (var (id, make, name) in items)
                {
                    if (dir.factoryDictionary != null && dir.factoryDictionary.ContainsKey(id)) continue;
                    done.Add(name + (dir.Add(id, make) ? "" : "（失败）"));
                }
                if (done.Count > 0) Log.Info("登记新物品：" + string.Join("、", done));
            }
            catch (Exception e) { Log.Error("登记新物品出错: " + Log.Describe(e)); }
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            try
            {
                if (hooked)
                {
                    ModHook.OnModItemDirectoryInit -= RegisterItems;
                    ModHook.OnGoingSleepLate -= TwoDays.OnGoingSleepLate;
                    ModHook.OnPlaceInventorInventoryItemLate -= Progression.OnJacksonStock;
                    ModHook.OnGameLoadedLate -= Progression.OnGameLoaded;
                    hooked = false;
                }
            }
            catch { }
            try { ModHelper.RemoveStartingScenarios(ModId); } catch { }
            try
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                host = null;
            }
            catch { }
        }
    }

    /// <summary>配置存在游戏自带的 ModConfig（ModConfigs/&lt;mod ID&gt;.json），改装件界面的“设置”里能改。</summary>
    internal sealed class ScientistConfig
    {
        internal bool TwoDays = true;
        private ConfigEntry<bool> twoDaysEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            twoDaysEntry = cfg.Define("twoDaysInMachineBay", true, "General", () => Lang.T("机器区里的机器一天按两天运转", "Machines in the Machine Bay run two days per day"),
                tooltip: () => Lang.T("高级科学家的职业能力：直接放在机器区里的机器每晚多运转一轮（多耗一次电）。冰箱、警报器、幻影投影仪、发电类设备、坏掉的机器不加倍。只对高级科学家的存档生效。",
                    "Advanced Scientist ability: machines placed directly in a Machine Bay run one extra cycle each night (using power again). Fridges, alarms, mirage projectors, power generators and broken machines are not doubled. Only in Advanced Scientist saves."));
            twoDaysEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save();
        }

        private void Apply()
        {
            TwoDays = twoDaysEntry?.Value ?? true;
        }
    }
}
