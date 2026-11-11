using System;
using UnityEngine;

namespace Conveniences
{
    /// <summary>
    /// 入口。ModLoader 会对本程序集调用 Harmony.PatchAll（本 mod 不用 [HarmonyPatch] 特性，那一步什么也不做），
    /// 然后依次调用 Init / OnEnable。补丁在 Init 里手动打。灌水键由一个常驻的小对象（ConveniencesBehaviour）监听。
    /// </summary>
    public sealed class ConveniencesMod : IMod
    {
        internal static ConveniencesConfig Config;
        internal static string ModId = "chaos5090.conveniences";
        private static GameObject host;

        public void Init(ModManifest manifest)
        {
            ModId = manifest?.ID ?? ModId;
            Config = new ConveniencesConfig();
            try { Config.Init(ModId); }
            catch (Exception e) { Log.Warn("配置初始化失败，使用默认值（电池、滤水器都开）: " + e.Message); }

            Config.RegisterGameOptions(ModId);
            Swap.Apply();
            try
            {
                if (host == null)
                {
                    host = new GameObject("Conveniences");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    host.AddComponent<ConveniencesBehaviour>();
                }
            }
            catch (Exception e) { Log.Error("创建按键对象失败（灌水键不可用）: " + Log.Describe(e)); }
            Log.Info("已加载：" + (Swap.Applied ? "拖到机器上换电池" + (Config.Batteries ? "开" : "关") + "，滤水器拖到水罐上换滤水器" + (Config.Filters ? "开" : "关") +
                                                 (Swap.HighlightApplied ? "" : "（悬停提示补丁未生效）")
                                               : "换件补丁未生效") +
                     "，灌水键 " + KeyNames.Label(Config.WaterKey) + (Config.WaterFill ? "" : "（关）"));
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            Swap.Remove();
            try { ModHelper.RemoveGameOptions(ModId); } catch { }
            try
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                host = null;
            }
            catch { }
        }
    }

    /// <summary>配置存在游戏自带的 ModConfig（ModConfigs/&lt;mod ID&gt;.json），改装件界面的“设置”里能改。</summary>
    internal sealed class ConveniencesConfig
    {
        internal const string DefaultWaterKey = "W";   // 左手区，游戏和本系列其他 mod 都没用
        internal bool Batteries = true;
        internal bool Filters = true;
        internal bool WaterFill = true;
        internal KeyCode WaterKey = KeyCode.W;

        private ConfigEntry<bool> batteryEntry;
        private ConfigEntry<bool> filterEntry;
        private ConfigEntry<bool> waterEntry;
        private ConfigEntry<string> waterKeyEntry;

        internal void Init(string modId)
        {
            var cfg = ModHelper.GetConfig(modId);
            batteryEntry = cfg.Define("batteries", true, "General", () => Lang.T("拖到机器上换电池", "Drop a battery on a machine to swap"),
                tooltip: () => Lang.T("把电池拖到机器上（不用打开机器）：换下机器里的旧电池，旧电池放回新电池原来的位置。机器电池槽空着时，原版本来就能直接拖进去。",
                    "Drop a battery on a machine without opening it: it replaces the battery inside, and the old one goes where the new one was. With an empty battery slot the base game already lets you drop it in."));
            filterEntry = cfg.Define("filters", true, "General", () => Lang.T("滤水器拖到水罐上换滤水器", "Drop a water filter on a water pitcher to swap"),
                tooltip: () => Lang.T("把滤水器拖到水罐上（不用打开）：换下里面的旧滤水器，旧的放回新的原来的位置。窗口第一层有滤芯槽的其他设备（例如循环水养殖系统）也一样。",
                    "Drop a water filter on a water pitcher without opening it: it replaces the filter inside, and the old one goes where the new one was. Works for other items with a filter slot at the top of their window (e.g. the Recirculating Aquaculture System) too."));
            waterEntry = cfg.Define("waterFill", true, "General", () => Lang.T("一键灌水", "One-key water filling"),
                tooltip: () => Lang.T("拖着瓶子按灌水键：指着水龙头 = 装满自来水；指着容器、瓶子里有水 = 倒进去；指着容器、瓶子是空的 = 用自来水来回装瓶倒进去，直到容器满（要先解锁锈水或溢流水管道）。",
                    "Hold a bottle and press the water key: on the tap = fill it with tap water; on a container with water in the bottle = pour it in; on a container with an empty bottle = keep filling at the tap and pouring until the container is full (needs the rust water or gutterflow pipe)."));
            waterKeyEntry = cfg.Define("waterKey", DefaultWaterKey, "General", () => Lang.T("灌水键", "Water key"),
                choices: null,
                tooltip: () => Lang.T("Unity KeyCode 名称或直接填字母 / 符号，默认 W。也可以在游戏的“选项”里按键绑定。", "Unity KeyCode name or a letter / symbol; default W. You can also bind it in the game's Options."));
            batteryEntry.OnChanged += _ => Apply();
            filterEntry.OnChanged += _ => Apply();
            waterEntry.OnChanged += _ => Apply();
            waterKeyEntry.OnChanged += _ => Apply();
            Apply();
            cfg.Save();
        }

        private void Apply()
        {
            Batteries = batteryEntry?.Value ?? true;
            Filters = filterEntry?.Value ?? true;
            WaterFill = waterEntry?.Value ?? true;
            WaterKey = KeyNames.Parse(waterKeyEntry?.Value ?? DefaultWaterKey, KeyCode.W, Log.Warn);
        }

        /// <summary>在游戏“选项”里加灌水键的按键绑定（按一下键即可改），和改装件设置里改的是同一份。</summary>
        internal void RegisterGameOptions(string modId)
        {
            try
            {
                const string section = "keys";
                ModHelper.AddGameOptionSection(modId, section, () => Lang.T("掌柜帮手·便利替换", "Shopkeeper's Kit: Quick Swap"));
                ModHelper.AddGameOptionKeyBind(modId, section, "water_key", () => Lang.T("一键灌水", "One-key water filling"), () => WaterKey,
                    k => { if (k != KeyCode.None) waterKeyEntry?.Set(k.ToString()); },
                    () => Lang.T("拖着瓶子指着水龙头或容器时按：装水 / 倒水 / 用自来水灌满。默认 W", "While holding a bottle over the tap or a container: fill / pour / fill it up with tap water. Default W"));
            }
            catch (Exception e)
            {
                Log.Warn("登记游戏选项失败（仍可在改装件设置里改按键）: " + e.Message);
            }
        }
    }
}
