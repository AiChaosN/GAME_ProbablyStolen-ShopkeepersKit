using System;

namespace Conveniences
{
    /// <summary>
    /// 入口。ModLoader 会对本程序集调用 Harmony.PatchAll（本 mod 不用 [HarmonyPatch] 特性，那一步什么也不做），
    /// 然后依次调用 Init / OnEnable。补丁在 Init 里手动打。没有热键、没有界面，不需要常驻对象。
    /// </summary>
    public sealed class ConveniencesMod : IMod
    {
        internal static ConveniencesConfig Config;
        internal static string ModId = "chaos5090.conveniences";

        public void Init(ModManifest manifest)
        {
            ModId = manifest?.ID ?? ModId;
            Config = new ConveniencesConfig();
            try { Config.Init(ModId); }
            catch (Exception e) { Log.Warn("配置初始化失败，使用默认值（电池、滤水器都开）: " + e.Message); }

            Swap.Apply();
            Log.Info("已加载：" + (Swap.Applied ? "拖到机器上换电池" + (Config.Batteries ? "开" : "关") + "，滤水器拖到水罐上换滤水器" + (Config.Filters ? "开" : "关") +
                                                 (Swap.HighlightApplied ? "" : "（悬停提示补丁未生效）")
                                               : "补丁未生效，按原版处理"));
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            Swap.Remove();
        }
    }

    /// <summary>配置存在游戏自带的 ModConfig（ModConfigs/&lt;mod ID&gt;.json），改装件界面的“设置”里能改。</summary>
    internal sealed class ConveniencesConfig
    {
        internal bool Batteries = true;
        internal bool Filters = true;

        private ConfigEntry<bool> batteryEntry;
        private ConfigEntry<bool> filterEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            batteryEntry = cfg.Define("batteries", true, "General", () => Lang.T("拖到机器上换电池", "Drop a battery on a machine to swap"),
                tooltip: () => Lang.T("把电池拖到机器上（不用打开机器）：换下机器里的旧电池，旧电池放回新电池原来的位置。机器电池槽空着时，原版本来就能直接拖进去。",
                    "Drop a battery on a machine without opening it: it replaces the battery inside, and the old one goes where the new one was. With an empty battery slot the base game already lets you drop it in."));
            filterEntry = cfg.Define("filters", true, "General", () => Lang.T("滤水器拖到水罐上换滤水器", "Drop a water filter on a water pitcher to swap"),
                tooltip: () => Lang.T("把滤水器拖到水罐上（不用打开）：换下里面的旧滤水器，旧的放回新的原来的位置。窗口第一层有滤芯槽的其他设备（例如循环水养殖系统）也一样。",
                    "Drop a water filter on a water pitcher without opening it: it replaces the filter inside, and the old one goes where the new one was. Works for other items with a filter slot at the top of their window (e.g. the Recirculating Aquaculture System) too."));
            batteryEntry.OnChanged += _ => Apply();
            filterEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save();
        }

        private void Apply()
        {
            Batteries = batteryEntry?.Value ?? true;
            Filters = filterEntry?.Value ?? true;
        }
    }
}
