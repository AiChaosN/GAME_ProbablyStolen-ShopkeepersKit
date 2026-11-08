using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 本存档里“见过”的物品：在仓库（含箱子、机器里）、柜台两侧、展示柜里出现过的每一种物品。
    /// 分类面板只列这些，见过的越多列表越长，没见过的不会剧透。
    /// 记录用游戏的 ModHelper.SetModData 存进当前存档（每个存档各一份，随存档保存）：
    /// 每种物品一行：id、主类型（第一个标签）、名字、贴图图集、贴图名（用来在面板里画图标，不用生成物品）。
    /// </summary>
    internal static class SeenItems
    {
        private const string DataKey = "seen_items_v1";
        private const float ScanInterval = 2f;

        internal sealed class Info
        {
            internal string Id, Type, Name, Atlas, Sprite;
        }

        private static readonly Dictionary<string, Info> seen = new Dictionary<string, Info>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static bool loaded, dirty;
        private static float nextScan;

        internal static IEnumerable<Info> All => seen.Values;

        internal static void OnGameLoaded()
        {
            seen.Clear();
            iconCache.Clear();
            loaded = false;
            dirty = false;
            nextScan = 0f;
        }

        /// <summary>每帧调用；每 2 秒扫一次店里的物品，有新的就记下并存进存档数据。</summary>
        internal static void Tick()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + ScanInterval;
            ScanNow();
        }

        internal static void ScanNow()
        {
            try
            {
                if (PlayerStore.Instance == null || EmporiumEntry.Instance == null) return;
                EnsureLoaded();
                var e = EmporiumEntry.Instance;
                foreach (var inv in new GameInventory[] { e.invElement, e.backInvinvElement, e.frontInvinvElement, e.showcaseElement })
                {
                    if (inv == null) continue;
                    foreach (var it in GraphUtils.FindAllChildrenType<GameItem>(inv)) Add(it);
                }
                if (dirty) Save();
            }
            catch (Exception ex)
            {
                Log.Warn("记录见过的物品出错: " + ex.Message);
            }
        }

        private static void Add(GameItem it)
        {
            if (it == null || string.IsNullOrEmpty(it.identifier) || seen.ContainsKey(it.identifier)) return;
            if (Stock.IsFixture(it)) return;   // 箱子、机器、设施本身不归箱，不列
            var info = new Info { Id = it.identifier, Type = Stock.PrimaryType(it) ?? "" };
            try { info.Name = it.GetDisplayName(false); } catch { info.Name = it.identifier; }   // 原名，不用标签机起的名字
            try { info.Atlas = it.spriteAtlasPath ?? ""; info.Sprite = it.spritePath ?? ""; } catch { info.Atlas = info.Sprite = ""; }
            seen[info.Id] = info;
            if (it is GameItemElement el)
            {
                try { var sp = el.background?.image?.sprite; if (sp != null) iconCache[info.Id] = sp; } catch { }
            }
            dirty = true;
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            string data = null;
            try { data = ModHelper.GetModData(SortToBoxesMod.ModId, DataKey); } catch { }
            if (string.IsNullOrEmpty(data)) return;
            foreach (var line in data.Split('\n'))
            {
                var f = line.Split('\t');
                if (f.Length < 5 || string.IsNullOrEmpty(f[0])) continue;
                seen[f[0]] = new Info { Id = f[0], Type = f[1], Name = f[2], Atlas = f[3], Sprite = f[4] };
            }
        }

        private static void Save()
        {
            var sb = new StringBuilder();
            foreach (var i in seen.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                sb.Append(Clean(i.Id)).Append('\t').Append(Clean(i.Type)).Append('\t').Append(Clean(i.Name)).Append('\t')
                  .Append(Clean(i.Atlas)).Append('\t').Append(Clean(i.Sprite)).Append('\n');
            }
            try
            {
                ModHelper.SetModData(SortToBoxesMod.ModId, DataKey, sb.ToString());
                dirty = false;
            }
            catch (Exception e)
            {
                Log.Warn("保存见过的物品失败: " + e.Message);
            }
        }

        private static string Clean(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        /// <summary>物品图标：先用见过的实物的贴图，否则按记录的图集路径加载。</summary>
        internal static Sprite Icon(Info info)
        {
            if (info == null) return null;
            if (iconCache.TryGetValue(info.Id, out var sp)) return sp;
            sp = null;
            try
            {
                if (!string.IsNullOrEmpty(info.Atlas) && !string.IsNullOrEmpty(info.Sprite))
                    sp = RenderHandler.LoadFromAtlas(info.Atlas, info.Sprite);
            }
            catch { sp = null; }
            iconCache[info.Id] = sp;
            return sp;
        }
    }
}
