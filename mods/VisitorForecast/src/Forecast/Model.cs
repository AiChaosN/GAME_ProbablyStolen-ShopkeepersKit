using System.Collections.Generic;

namespace VisitorForecast.Forecast
{
    /// <summary>沙盒中观察到的一位顾客（只存原始数据，本地化在沙盒结束后再做）。</summary>
    internal sealed class ClientObs
    {
        internal string Id;
        internal string Name;
        internal string Faction;
        internal StoreClient.ClientIntent Intent;
        internal string[] Tags;
        internal string[] ItemIds;
        internal int? Budget;
        internal string Source;
        internal int Position;      // 1 起
        internal bool InsertedFirst;
        internal bool IsAug;
        internal bool IsWanted;
        internal string AttractedType;

        internal static ClientObs From(StoreClient c, string source, int position, bool insertedFirst)
        {
            return new ClientObs
            {
                Id = c.identifier ?? "NO_ID",
                Name = c.displayName ?? "",
                Faction = c.clientFaction ?? "NONE",
                Intent = c.clientIntent,
                Tags = c.clientBuyingTagList != null ? c.clientBuyingTagList.ToArray() : new string[0],
                ItemIds = c.clientBuyingIdList != null ? c.clientBuyingIdList.ToArray() : new string[0],
                Budget = c.useClientBudget ? c.GetBudget() : (int?)null,
                Source = source,
                Position = position,
                InsertedFirst = insertedFirst,
                IsAug = c.isAug,
                IsWanted = c.isWanted,
                AttractedType = c.attractedType,
            };
        }
    }

    /// <summary>按 identifier 汇总的一行。</summary>
    internal sealed class ClientAgg
    {
        internal string Id;
        internal int RunsPresent;
        internal int TotalCount;
        internal int MaxPerRun;
        internal readonly Dictionary<string, int> Names = new Dictionary<string, int>();
        internal readonly Dictionary<string, int> Factions = new Dictionary<string, int>();
        internal readonly Dictionary<StoreClient.ClientIntent, int> Intents = new Dictionary<StoreClient.ClientIntent, int>();
        internal readonly Dictionary<string, int> Sources = new Dictionary<string, int>();
        internal readonly HashSet<string> Tags = new HashSet<string>();
        internal readonly HashSet<string> ItemIds = new HashSet<string>();
        internal readonly List<int> Positions = new List<int>();
        internal int BudgetMin = int.MaxValue, BudgetMax = int.MinValue, BudgetRuns;
        internal int AugRuns, WantedRuns, FirstRuns;
        internal string AttractedType;

        internal void Add(ClientObs o)
        {
            TotalCount++;
            Inc(Names, o.Name);
            Inc(Factions, o.Faction);
            Intents.TryGetValue(o.Intent, out int ic);
            Intents[o.Intent] = ic + 1;
            Inc(Sources, o.Source);
            foreach (var t in o.Tags) Tags.Add(t);
            foreach (var t in o.ItemIds) ItemIds.Add(t);
            Positions.Add(o.Position);
            if (o.Budget.HasValue)
            {
                BudgetRuns++;
                if (o.Budget.Value < BudgetMin) BudgetMin = o.Budget.Value;
                if (o.Budget.Value > BudgetMax) BudgetMax = o.Budget.Value;
            }
            if (o.IsAug) AugRuns++;
            if (o.IsWanted) WantedRuns++;
            if (o.InsertedFirst) FirstRuns++;
            if (AttractedType == null && !string.IsNullOrEmpty(o.AttractedType)) AttractedType = o.AttractedType;
        }

        private static void Inc(Dictionary<string, int> d, string k)
        {
            k = k ?? "";
            d.TryGetValue(k, out int c);
            d[k] = c + 1;
        }

        internal static T Top<T>(Dictionary<T, int> d)
        {
            T best = default(T);
            int bc = -1;
            foreach (var kv in d)
                if (kv.Value > bc) { best = kv.Key; bc = kv.Value; }
            return best;
        }
    }

    /// <summary>界面上的一行（已本地化）。</summary>
    internal sealed class Row
    {
        internal string Name, Faction, Intent, Wants, Budget, Source, Chance;
        internal double Probability;
        internal double SortPos;
        internal bool Warn; // 改造人/通缉 等需要提醒的

        // 以下只给界面排版用（Name = BaseName + " [Marks]"）
        internal string BaseName;
        internal readonly List<string> Marks = new List<string>();
        internal string FactionId;
        internal string SourceKey;   // 最常见的来源（不带“等”），用于“其他客人”分组
        internal double Expected;    // 平均每次模拟来几次（TotalCount / 模拟次数）
        internal bool AtDoor;        // 早上在门口等（开门后直接站在柜台前）
        internal bool AtCounter;     // 实际队列：正在柜台前
    }

    internal sealed class ForecastResult
    {
        internal enum Mode { Forecast, ActualQueue, Message }

        internal Mode Kind;
        internal int Day = -1;        // 界面用：天数与星期（Message 模式下可能没有）
        internal string Weekday;
        internal int CertainCount;    // 必来人数（含同一人来多次）
        internal string Header;
        internal string SubHeader;
        internal string Message;
        internal readonly List<Row> Certain = new List<Row>();
        internal readonly List<Row> Possible = new List<Row>();
        internal readonly List<Row> Waiting = new List<Row>();
        internal readonly List<Row> Actual = new List<Row>();
        internal int OthersMin, OthersMax;   // 除必来者以外的来客人数区间
        internal double OthersMean;
        internal string Warning;      // 安全网红字
        internal string Note;         // 其他提示（错误次数等）
    }
}
