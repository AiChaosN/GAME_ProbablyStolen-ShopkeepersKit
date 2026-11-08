using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace VisitorForecast.Sandbox
{
    /// <summary>
    /// 物品世界的指纹（StateGraph 不进入 GameItem 图，这里单独兜底）：
    /// 店里各个格子/柜台/展示柜/已售区里的每件物品的 id、唯一编号、数量、单价、特性数、
    /// 所在背包以及所有已启用标签的值。只读，不调用任何会改动物品的方法。
    /// </summary>
    internal static class WorldFingerprint
    {
        internal static List<string> Take()
        {
            var list = new List<string>();
            var ee = EmporiumEntry.Instance;
            if (ee == null) return list;
            var items = new List<GameItem>();
            try { items.AddRange(ee.GetAllItems()); } catch (Exception e) { list.Add("GetAllItems failed: " + e.GetType().Name); }
            try
            {
                var sold = ee.soldElement;
                if (sold != null) items.AddRange(sold.childItems);
            }
            catch { }
            var sb = new StringBuilder();
            foreach (var it in items)
            {
                if (it == null) continue;
                sb.Length = 0;
                try
                {
                    sb.Append(it.identifier).Append('#').Append(it.uniqueId)
                      .Append(" n=").Append(it.unitCount)
                      .Append(" v=").Append(it.unitValue)
                      .Append(" f=").Append(it.itemFeatures != null ? it.itemFeatures.Count : -1)
                      .Append(" in=").Append(it.parentInventory == null ? "-" : RuntimeHelpers.GetHashCode(it.parentInventory).ToString("x"));
                    var dict = it.state?.dict;
                    if (dict != null)
                    {
                        var tags = new List<string>();
                        foreach (var kv in dict)
                        {
                            var ts = kv.Value;
                            if (ts == null || !ts.valueEnabled) continue;
                            tags.Add(kv.Key + "=" + ts.valueInt + "/" + ts.valueLong + "/" + ts.valueBool + "/" +
                                     ts.valueFloat.ToString("R", CultureInfo.InvariantCulture) + "/" +
                                     ts.valueDouble.ToString("R", CultureInfo.InvariantCulture));
                        }
                        tags.Sort(StringComparer.Ordinal);
                        sb.Append(" tags[").Append(string.Join(",", tags)).Append(']');
                    }
                }
                catch (Exception e)
                {
                    sb.Append(" <err ").Append(e.GetType().Name).Append('>');
                }
                list.Add(sb.ToString());
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        internal static List<string> Diff(List<string> before, List<string> after, int max)
        {
            var diffs = new List<string>();
            var a = new HashSet<string>(before);
            var b = new HashSet<string>(after);
            foreach (var s in before)
            {
                if (diffs.Count >= max) break;
                if (!b.Contains(s)) diffs.Add("物品变化/消失: " + s);
            }
            foreach (var s in after)
            {
                if (diffs.Count >= max) break;
                if (!a.Contains(s)) diffs.Add("物品新增/变化后: " + s);
            }
            if (diffs.Count == 0 && before.Count != after.Count)
                diffs.Add("物品数量 " + before.Count + " -> " + after.Count);
            return diffs;
        }
    }
}
