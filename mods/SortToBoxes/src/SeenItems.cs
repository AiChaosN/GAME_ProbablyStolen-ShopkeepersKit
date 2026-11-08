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
    /// 每种物品一行：id、主类型（第一个标签）、名字、贴图图集、贴图名（用来在面板里画图标，不用生成物品）、大类。
    /// </summary>
    internal static class SeenItems
    {
        private const string DataKey = "seen_items_v1";
        private const float ScanInterval = 2f;

        internal sealed class Info
        {
            internal string Id, Type, Name, Atlas, Sprite, Cat, NameLang;   // NameLang：名字是用哪种语言记的（zh/en）
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
                    foreach (var it in AllItems(inv))
                    {
                        try { Add(it); }
                        catch (Exception ex) { ReportOnce(ex); }
                    }
                }
                if (dirty) Save();
            }
            catch (Exception ex)
            {
                ReportOnce(ex);
            }
        }

        private static bool reported;

        /// <summary>出错只记一次完整的出错位置，免得刷屏。</summary>
        private static void ReportOnce(Exception ex)
        {
            if (reported) return;
            reported = true;
            Log.Warn("记录见过的物品出错（只记第一次）: " + Log.Describe(ex));
        }

        /// <summary>
        /// 自己遍历物品树：游戏的 GraphUtils.FindAllChildrenType 不检查空的子节点，遇到就抛空引用（1.4.0 在柜台/展示柜上碰到过）。
        /// 这里逐个跳过空节点、读不了子节点的节点，并防止重复访问。
        /// </summary>
        private static List<GameItem> AllItems(GraphNodeStorage root)
        {
            var result = new List<GameItem>();
            var queue = new Queue<GraphNodeStorage>();
            var visited = new HashSet<object>();
            queue.Enqueue(root);
            int guard = 0;
            while (queue.Count > 0 && guard++ < 50000)
            {
                var node = queue.Dequeue();
                if (node == null || !visited.Add(node)) continue;
                if (!ReferenceEquals(node, root) && node is GameItem gi) result.Add(gi);
                List<GraphNodeStorage> children = null;
                try { children = node.children; } catch { }
                if (children == null) continue;
                foreach (var c in children)
                    if (c != null) queue.Enqueue(c);
            }
            return result;
        }

        private static void Add(GameItem it)
        {
            if (it == null || string.IsNullOrEmpty(it.identifier)) return;
            string lang = Lang.Zh ? "zh" : "en";
            if (seen.TryGetValue(it.identifier, out var known))
            {
                // 游戏换了语言：再次见到时把名字更新成当前语言
                if (known.NameLang != lang)
                {
                    try { known.Name = it.GetDisplayName(false); known.NameLang = lang; dirty = true; } catch { }
                }
                return;
            }
            if (Stock.IsFixture(it)) return;   // 箱子、机器、设施本身不归箱，不列
            var info = new Info { Id = it.identifier, Type = Stock.PrimaryType(it) ?? "", Cat = Categories.Of(it), NameLang = lang };
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
                string cat = f.Length >= 6 && f[5].Length > 0 ? f[5] : Categories.Of(f[0], new[] { f[1] });
                string nameLang = f.Length >= 7 ? f[6] : "";
                seen[f[0]] = new Info { Id = f[0], Type = f[1], Name = f[2], Atlas = f[3], Sprite = f[4], Cat = cat, NameLang = nameLang };
            }
        }

        private static void Save()
        {
            var sb = new StringBuilder();
            foreach (var i in seen.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                sb.Append(Clean(i.Id)).Append('\t').Append(Clean(i.Type)).Append('\t').Append(Clean(i.Name)).Append('\t')
                  .Append(Clean(i.Atlas)).Append('\t').Append(Clean(i.Sprite)).Append('\t').Append(Clean(i.Cat)).Append('\t').Append(Clean(i.NameLang)).Append('\n');
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
