using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisitorForecast.Forecast;

namespace VisitorForecast.UI
{
    /// <summary>两种窗口（原生平板 / IMGUI 备用）共用的排版数据：排序、分组、颜色、文字。</summary>
    internal static class ForecastView
    {
        internal const string Title = "访客预测";
        internal const string ActualTitle = "实际队列";
        internal const string EmptyCertain = "今天没有一定会来的人";
        internal const double MinorThreshold = 0.03;

        internal sealed class SourceGroup
        {
            internal string Source;
            internal double Expected;
            internal readonly List<Row> Rows = new List<Row>();
            internal readonly List<Row> Minor = new List<Row>(); // 各 <3%，折叠成一行
        }

        /// <summary>必来：门口等的人在最前，其余按排队位置。</summary>
        internal static List<Row> SortedCertain(IList<Row> rows)
        {
            var list = new List<Row>(rows ?? new Row[0]);
            var index = new Dictionary<Row, int>();
            for (int i = 0; i < list.Count; i++) index[list[i]] = i;
            list.Sort((x, y) =>
            {
                int c = (x.AtDoor ? 0 : 1).CompareTo(y.AtDoor ? 0 : 1);
                if (c == 0) c = x.SortPos.CompareTo(y.SortPos);
                return c != 0 ? c : index[x].CompareTo(index[y]);
            });
            return list;
        }

        /// <summary>其他客人按来源分组；组按预计人数降序，组内按概率降序，&lt;3% 的折叠。</summary>
        internal static List<SourceGroup> GroupOthers(IList<Row> rows)
        {
            var groups = new Dictionary<string, SourceGroup>();
            var order = new List<SourceGroup>();
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    string key = string.IsNullOrEmpty(r.SourceKey) ? "其他" : r.SourceKey;
                    if (!groups.TryGetValue(key, out var g))
                    {
                        g = new SourceGroup { Source = key };
                        groups[key] = g;
                        order.Add(g);
                    }
                    g.Expected += r.Expected > 0 ? r.Expected : r.Probability;
                    (r.Probability < MinorThreshold ? g.Minor : g.Rows).Add(r);
                }
            }
            Comparison<Row> byChance = (x, y) =>
            {
                int c = y.Probability.CompareTo(x.Probability);
                return c != 0 ? c : string.CompareOrdinal(x.Name ?? "", y.Name ?? "");
            };
            foreach (var g in order)
            {
                g.Rows.Sort(byChance);
                g.Minor.Sort(byChance);
            }
            return order.OrderByDescending(g => g.Expected).ThenBy(g => g.Source, StringComparer.Ordinal).ToList();
        }

        internal static string Range(int a, int b) => a == b ? a.ToString() : a + "–" + b;

        internal static string DayLine(ForecastResult r)
        {
            if (r == null || r.Day < 0) return "";
            return "第 " + r.Day + " 天 · " + (r.Weekday ?? "");
        }

        internal static string Expected(double e)
        {
            if (e < 0.05) return "不到 0.1 人";
            return "约 " + e.ToString("0.0") + " 人";
        }

        internal static string Percent(double p)
        {
            if (p >= 0.9995) return "100%";
            if (p < 0.005) return "<1%";
            return Math.Round(p * 100.0).ToString("0") + "%";
        }

        internal static string MinorLine(SourceGroup g)
        {
            return "其他 " + g.Minor.Count + " 种（各 <3%）";
        }

        internal static string MinorNames(SourceGroup g, int max)
        {
            var names = g.Minor.Take(max).Select(r => (r.BaseName ?? r.Name) + " " + Percent(r.Probability));
            string s = string.Join("、", names);
            if (g.Minor.Count > max) s += " 等";
            return s;
        }

        internal static string Footnote(string hotkey) => "预测基于按键那一刻的展示柜和状态；改动展示柜后请按 " + hotkey + " 或“重新预测”。";

        internal static string BusyText(int runs)
        {
            var ps = Game.PlayerStoreOrNull;
            if (ps != null && ps.storeState == PlayerStore.StoreState.OPEN) return "正在读取实际队列…";
            return "正在预测…（模拟 " + runs + " 次，游戏会卡顿一到几秒）";
        }

        // ------------------------------------------------------------------ 阵营颜色（StoreReputation.factionColor3）

        private static readonly Dictionary<string, string> factionHex = new Dictionary<string, string>
        {
            { "FACTION_LOWER_LEVEL", "98907c" },
            { "FACTION_UPPER_LEVEL", "ac7b9a" },
            { "FACTION_SECURITY", "b16556" },
            { "FACTION_BLACK_MARKET", "737064" },
            { "FACTION_REVOLUTION", "848c60" },
            { "FACTION_CARTEL", "737064" },
        };

        internal const string OtherFactionHex = "8a877f";

        internal static Color FactionColor(string factionId)
        {
            string hex = factionId != null && factionHex.TryGetValue(factionId, out var h) ? h : OtherFactionHex;
            return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.grey;
        }

        internal static bool HasFaction(Row r) =>
            !string.IsNullOrEmpty(r.Faction) && r.Faction != "—" && r.FactionId != "NONE";

        /// <summary>悬停提示里的完整信息（表格里放不下的“想要”全文也在这里）。</summary>
        internal static string Tooltip(Row r, bool showSource)
        {
            var lines = new List<string> { "<b>" + (r.BaseName ?? r.Name) + "</b>" };
            if (r.Marks.Count > 0) lines.Add("标记：" + string.Join("、", r.Marks));
            if (HasFaction(r)) lines.Add("阵营：" + r.Faction);
            lines.Add("来做什么：" + r.Intent);
            if (!string.IsNullOrEmpty(r.Wants) && r.Wants != "—") lines.Add("想要：" + r.Wants);
            if (!string.IsNullOrEmpty(r.Budget) && r.Budget != "—") lines.Add("预算：" + r.Budget);
            if (showSource && !string.IsNullOrEmpty(r.Source) && r.Source != "—") lines.Add("来源：" + r.Source);
            if (!string.IsNullOrEmpty(r.Chance)) lines.Add(r.Chance);
            return string.Join("\n", lines);
        }
    }
}
