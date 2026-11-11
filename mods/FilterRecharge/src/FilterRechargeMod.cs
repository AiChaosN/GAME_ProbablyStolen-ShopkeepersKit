using System;

namespace FilterRecharge
{
    /// <summary>
    /// 入口。ModLoader 会对本程序集调用 Harmony.PatchAll（本 mod 不用 [HarmonyPatch] 特性，那一步什么也不做），
    /// 然后依次调用 Init / OnEnable。补丁在 Init 里手动打。没有热键、没有界面，不需要常驻对象。
    /// </summary>
    public sealed class FilterRechargeMod : IMod
    {
        internal static FilterRechargeConfig Config;
        internal static string ModId = "chaos5090.filter-recharge";

        public void Init(ModManifest manifest)
        {
            ModId = manifest?.ID ?? ModId;
            Config = new FilterRechargeConfig();
            try { Config.Init(ModId); }
            catch (Exception e) { Log.Warn("配置初始化失败，使用默认值（开启，上限 20 倍）: " + e.Message); }

            Recharge.Apply();
            Log.Info("已加载：" + (Recharge.Applied ? "滤水器拖到高级滤水器上充能" + (Config.Enabled ? "开" : "关") + "，高级滤水器耐久度上限 " + Recharge.Cap
                                                    : "补丁未生效，按原版处理"));
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            Recharge.Remove();
        }
    }

    /// <summary>配置存在游戏自带的 ModConfig（ModConfigs/&lt;mod ID&gt;.json），改装件界面的“设置”里能改。</summary>
    internal sealed class FilterRechargeConfig
    {
        // 倍数用下拉框：游戏设置里的整数滑块不显示当前数值
        private static readonly string[] MultiplierChoices = { "5", "10", "20", "40" };

        internal bool Enabled = true;
        internal int Multiplier = 20;

        private ConfigEntry<bool> enabledEntry;
        private ConfigEntry<string> multiplierEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            enabledEntry = cfg.Define("enabled", true, "General", () => Lang.T("滤水器拖到高级滤水器上充能", "Drop a water filter on an Advanced Water Filter to recharge it"),
                tooltip: () => Lang.T("把滤水器（普通、高级、水培芦苇滤芯）拖到高级滤水器上，或拖到装着高级滤水器的水罐等东西上：它剩下的耐久度转给高级滤水器。关掉 = 原版。",
                    "Drop a water filter (regular, advanced or hydroreed) on an Advanced Water Filter, or on a pitcher or other item holding one: its remaining durability moves to the Advanced Water Filter. Off = base game."));
            multiplierEntry = cfg.Define("capMultiplier", "20", "General", () => Lang.T("高级滤水器耐久度上限（普通滤水器的倍数）", "Advanced filter durability cap (× a regular filter)"),
                choices: MultiplierChoices,
                tooltip: () => Lang.T("普通滤水器满耐久是 400。20 倍 = 上限 8000。已经充到更高上限的不会被降回去。",
                    "A regular water filter has 400 durability. 20× = a cap of 8000. Filters already charged to a higher cap keep it."));
            enabledEntry.OnChanged += _ => Apply();
            multiplierEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save();
        }

        private void Apply()
        {
            Enabled = enabledEntry?.Value ?? true;
            Multiplier = int.TryParse(multiplierEntry?.Value, out var m) && m > 0 ? m : 20;
        }
    }
}
