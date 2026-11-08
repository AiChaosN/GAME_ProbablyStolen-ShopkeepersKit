using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VisitorForecast.Sandbox;
using Debug = UnityEngine.Debug;

namespace VisitorForecast.Forecast
{
    internal sealed class RunData
    {
        internal readonly List<ClientObs> Queue = new List<ClientObs>();
        internal readonly List<ClientObs> Waiting = new List<ClientObs>();
    }

    /// <summary>
    /// 预测主流程：
    /// 1. 抓取状态快照（StateGraph）与物品世界指纹；
    /// 2. 打开拦截（SandboxGuard.Active），关闭 Unity 日志；
    /// 3. 重复 N 次：换随机种子 → 跑一遍开门时的顾客生成 → 记录队列 → 销毁新建物品 → 把状态写回快照；
    /// 4. 最终全量恢复 + 恢复 Random.state；
    /// 5. 安全网：重新抓一份快照与物品指纹，与开始前逐项比对，有差异就报警并锁定，直到重新读档。
    /// </summary>
    internal sealed class ForecastEngine
    {
        internal bool Tainted;
        internal string TaintSummary;
        private object taintedStore; // 报警时的 PlayerStore 实例；换了存档对象（新游戏/读档）就解除锁定
        private bool warmedUp;
        private string registryNote; // 类型表与当前游戏不一致时的提示（游戏更新后）

        internal void OnGameLoaded()
        {
            Tainted = false;
            TaintSummary = null;
            taintedStore = null;
        }

        internal ForecastResult Run(ForecastConfig cfg)
        {
            if (!Game.InGame(out var ps, out var ss))
                return Message("还没有进入存档（或存档尚未加载完）。");

            switch (ps.storeState)
            {
                case PlayerStore.StoreState.OPEN:
                    return ActualQueue(ps, ss);
                case PlayerStore.StoreState.CLOSED:
                    return Message("今天已经打烊。请在明天早上拉起卷帘门（开门）之前按 " + KeyNames.Label(cfg.Hotkey) + " 预测当天来客。");
            }
            if (Patches.CriticalMissing)
                return Message("关键的防副作用补丁没有成功应用（游戏版本可能已更新）。为了不影响存档，预测功能已禁用。详情见 Player.log 中的 [VisitorForecast] 行。");
            if (Tainted && !ReferenceEquals(taintedStore, ps)) OnGameLoaded();
            if (Tainted)
            {
                var r = Message("上一次预测后发现游戏状态与预测前不一致，为安全起见已停止预测，请重新读档后再用。");
                r.Warning = "⚠ 安全网报警：" + TaintSummary;
                return r;
            }
            try
            {
                return Forecast(ps, ss, cfg);
            }
            catch (Exception e)
            {
                Log.Error("预测失败: " + Log.Describe(e));
                return Message("预测失败：" + e.GetType().Name + "（详见 Player.log）。游戏状态已尝试恢复。");
            }
        }

        private static ForecastResult Message(string text) =>
            new ForecastResult { Kind = ForecastResult.Mode.Message, Header = "访客预测", Message = text };

        // ================================================================== forecast

        private ForecastResult Forecast(PlayerStore ps, StoreStation ss, ForecastConfig cfg)
        {
            int n = cfg.Runs;
            int day = ss.dayCounter;
            var total = Stopwatch.StartNew();

            if (!warmedUp)
            {
                int failedTypes = GameTypes.WarmUp();
                warmedUp = true;
                if (failedTypes > 0) Log.Warn(failedTypes + " 个类型的静态字段无法读取，已跳过");
                CheckRegistry();
            }

            var worldBefore = WorldFingerprint.Take();
            var before = StateGraph.Capture();
            long captureMs = total.ElapsedMilliseconds;
            var randomState = UnityEngine.Random.state;
            bool logWas = Debug.unityLogger.logEnabled;

            var runs = new List<RunData>(n);
            int errors = 0;
            string firstError = null;
            var hot = new List<int>();
            var hotSet = new HashSet<int>();
            var dirty = new HashSet<int>();
            bool useHot = false;
            double fullMs = 0;
            int fullCount = 0, hotRestores = 0;
            long writes = 0;
            int restoreErrorsBefore = before.RestoreErrors;

            SandboxGuard.IncludeOtherMods = cfg.IncludeOtherMods;
            SandboxGuard.BeginForecast();
            Log.Buffering = true;
            try
            {
                Debug.unityLogger.logEnabled = false;
                SandboxGuard.Active = true;
                for (int i = 0; i < n; i++)
                {
                    int seed = unchecked(day * 7919 + i * 104729 + 0x2545F491);
                    UnityEngine.Random.InitState(seed);
                    before.SwapRandoms(seed);
                    try
                    {
                        runs.Add(RunOnce(ps, ss));
                    }
                    catch (Exception e)
                    {
                        errors++;
                        if (firstError == null) firstError = Log.Describe(e);
                    }
                    finally
                    {
                        SourceTagger.EndRun();
                        try { SandboxGuard.DestroyCreatedThisRun(); } catch { }
                        bool full = !useHot || i < 16 || i % 16 == 0 || i == n - 1;
                        dirty.Clear();
                        var t0 = Stopwatch.GetTimestamp();
                        if (full)
                        {
                            writes += before.RestoreAll(dirty);
                            fullMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                            fullCount++;
                        }
                        else
                        {
                            writes += before.RestoreSubset(hot, dirty);
                            hotRestores++;
                        }
                        foreach (var d in dirty) if (hotSet.Add(d)) hot.Add(d);
                        // 全量检查很慢时改用“热点节点 + 每 16 轮一次全量”的策略；最终仍会全量恢复与校验
                        if (!useHot && fullCount == 4 && fullMs / 4 * n > 800) useHot = true;
                    }
                }
            }
            finally
            {
                SandboxGuard.Active = false;
                try { writes += before.RestoreAll(null); } catch (Exception e) { Log.Error("最终恢复失败: " + e.Message); }
                try { before.RestoreAll(null); } catch { }
                UnityEngine.Random.state = randomState;
                Debug.unityLogger.logEnabled = logWas;
                Log.Buffering = false;
                SandboxGuard.EndForecast();
            }
            long sandboxMs = total.ElapsedMilliseconds;
            // 注意：从这里到安全网比对结束，不写任何日志（日志会进游戏的 LogWriter 缓冲；虽然已排除，仍尽量保持干净）

            // ---------------------------------------------------------- 安全网
            List<string> diffs;
            try
            {
                var after = StateGraph.Capture();
                diffs = StateGraph.Compare(before, after, 60);
                var worldAfter = WorldFingerprint.Take();
                diffs.AddRange(WorldFingerprint.Diff(worldBefore, worldAfter, 20));
            }
            catch (Exception e)
            {
                // 无法确认状态一致时按“不一致”处理
                diffs = new List<string> { "安全网校验本身出错: " + e.GetType().Name + ": " + e.Message };
                Log.Error("安全网校验出错: " + Log.Describe(e));
            }
            if (!UnityEngine.Random.state.Equals(randomState)) diffs.Add("UnityEngine.Random.state 未能恢复");
            Log.Flush();
            if (SandboxGuard.MovedExisting > 0) diffs.Add("沙盒中有 " + SandboxGuard.MovedExisting + " 个原有物品/窗口的界面节点被移动到了渲染根下");
            if (before.RestoreErrors > restoreErrorsBefore)
                Log.Warn("恢复时有 " + (before.RestoreErrors - restoreErrorsBefore) + " 个节点写回出错，首个: " + before.FirstRestoreError);

            string supp = string.Join(", ", SandboxGuard.Suppressed.Select(kv => kv.Key + "×" + kv.Value));
            Log.Info(string.Format("第 {0} 天预测：{1} 次（失败 {2}），快照 {3} 节点/{4} 槽，跳过 {5} 个类型，抓取 {6} ms，沙盒合计 {7} ms，写回 {8} 次，全量检查 {9} 次（均 {10:0.0} ms），热点检查 {11} 次（{12} 个热点），销毁沙盒对象 {13} 个，拦截: {14}",
                day, n, errors, before.Nodes.Count, before.SlotCount, before.SkippedRootTypes, captureMs, sandboxMs, writes,
                fullCount, fullCount > 0 ? fullMs / fullCount : 0, hotRestores, hot.Count, SandboxGuard.DestroyedObjects,
                supp.Length == 0 ? "无" : supp));
            if (firstError != null) Log.Warn("沙盒中有 " + errors + " 次运行抛出异常，首个: " + firstError);

            string warning = null;
            if (diffs.Count > 0)
            {
                Tainted = true;
                taintedStore = ps;
                TaintSummary = diffs.Count + " 处差异，首个：" + diffs[0];
                Log.Error("安全网：预测前后游戏状态不一致（" + diffs.Count + " 处，最多列 80 条）。已停止后续预测，直到重新读档。差异路径：\n  " + string.Join("\n  ", diffs));
                warning = "⚠ 安全网报警：预测前后游戏状态有 " + diffs.Count + " 处不一致（详见 Player.log）。建议不要存档，直接重新读档；在重新读档前不再允许预测。";
            }

            var result = Aggregate(runs, n, errors, day, total.ElapsedMilliseconds);
            result.Warning = warning;
            if (errors > 0)
                result.Note = (result.Note == null ? "" : result.Note + "  ") + "有 " + errors + " 次模拟出错（已忽略，详见日志）。";
            if (registryNote != null)
                result.Note = (result.Note == null ? "" : result.Note + "  ") + registryNote;
            return result;
        }

        /// <summary>
        /// 快照用的类型字段表是构建时按游戏程序集生成的。游戏更新后可能有新类型不在表里：
        /// 它们的状态不受快照/安全网保护，所以在日志和窗口里提示更新 mod（预测照常进行）。
        /// </summary>
        private void CheckRegistry()
        {
            try
            {
                string mvid = typeof(PlayerStore).Assembly.ManifestModule.ModuleVersionId.ToString();
                bool sameBuild = string.Equals(mvid, GameTypeRegistry.GeneratedMvid, StringComparison.OrdinalIgnoreCase);
                var missing = GameTypeRegistry.UnregisteredVisibleTypes();
                Log.Info("类型表：" + GameTypeRegistry.Count + " 个游戏类型" + (sameBuild ? "（与当前游戏版本一致）" : "（游戏程序集与生成时不同）") +
                         (GameTypeRegistry.FailedChunks > 0 ? "，" + GameTypeRegistry.FailedChunks + " 块注册失败：" + GameTypeRegistry.FirstFailure : "") +
                         (missing.Count > 0 ? "，" + missing.Count + " 个可见类型不在表里：" + string.Join(", ", missing.Take(10)) : ""));
                if (missing.Count > 0 || GameTypeRegistry.FailedChunks > 0)
                    registryNote = "游戏似乎更新过：有 " + (missing.Count + GameTypeRegistry.FailedChunks) + " 处类型不在本 mod 的类型表里，这部分状态不受快照保护，请更新 mod。";
            }
            catch (Exception e)
            {
                Log.Warn("检查类型表出错: " + e.Message);
            }
        }

        private static RunData RunOnce(PlayerStore ps, StoreStation ss)
        {
            var scm = ps.storeClientManager;
            SourceTagger.BeginRun(scm.clientStack);
            if (Patches.RefreshCounterItemSkipped)
            {
                // 与真实开门完全相同的入口；其中改柜台物品的 RefreshCounterItem 已被补丁跳过
                ps.OnShutterOpened();
            }
            else
            {
                if (ss.dayCounter >= 8) ps.isNewspaperUnlocked = true;
                ps.CheckCrime();
                SoldItem.HandleSoldItem();
                ps.callNextWhenPossible = false;
                ps.lockShutterButtonFlag = 0;
                scm.GenerateClient();
                ps.HandleFutureClientQueue();
                scm.clientStack.RemoveAll(c => c == null);
            }

            var rd = new RunData();
            var stack = ps.storeClientManager.clientStack;
            for (int i = 0; i < stack.Count; i++)
            {
                var c = stack[i];
                if (c == null) continue;
                rd.Queue.Add(ClientObs.From(c, SourceTagger.SourceOf(c), i + 1, SourceTagger.WasInsertedFirst(c)));
            }
            CollectWaiting(ps, ss, rd.Waiting);
            return rd;
        }

        /// <summary>
        /// 开门后 OnLateShutterOpen 会让这些人直接出现在柜台前（不进队列）。
        /// 条件在本轮生成之后判断，与真实顺序一致。
        /// </summary>
        private static void CollectWaiting(PlayerStore ps, StoreStation ss, List<ClientObs> list)
        {
            bool someone = ps.currentClientInstance != null;
            void Add(Func<StoreClient> make)
            {
                try
                {
                    var c = make();
                    if (c != null) list.Add(ClientObs.From(c, "早上在门口等", 0, false));
                }
                catch { }
            }
            if (StoreReputation.IsPerkUnlocked("LLLYNCH")) { Add(StoreClientListMorning.CreateMobLeader); someone = true; }
            if (StoreReputation.IsPerkUnlocked("SEC_IMMEDIATE_ARREST")) { Add(StoreClientListMorning.CreateSecLeader); someone = true; }
            if (StoreReputation.IsPerkUnlocked("UL_HITMAN")) { Add(StoreClientListMorning.CreateULHitman); someone = true; }
            if (StoreReputation.IsPerkUnlocked("REV_HIT"))
            {
                list.Add(new ClientObs
                {
                    Id = "__rev_bomb", Name = "（革命军在柜台放了炸弹）", Faction = "FACTION_REVOLUTION",
                    Intent = StoreClient.ClientIntent.SPECIAL, Tags = new string[0], ItemIds = new string[0], Source = "早上在门口等",
                });
            }
            if (StoreReputation.IsPerkUnlocked("CARTEL_RETIRED")) { Add(StoreClientListMorning.CreateCartelRetired); someone = true; }
            if (!someone && StoreClientData.CanLouMorningVisit(ss.dayCounter)) Add(StoreClientListWanted.CreateWanted5Morning);
        }

        // ================================================================== aggregation

        private static ForecastResult Aggregate(List<RunData> runs, int requested, int errors, int day, long ms)
        {
            var res = new ForecastResult { Kind = ForecastResult.Mode.Forecast };
            int ok = runs.Count;
            string weekday = Text.Weekdays[((day % 7) + 7) % 7];
            res.Day = day;
            res.Weekday = weekday;
            if (ok == 0)
            {
                res.Kind = ForecastResult.Mode.Message;
                res.Header = "第 " + day + " 天 · " + weekday;
                res.Message = "所有模拟都失败了，无法给出预测（详见 Player.log）。";
                return res;
            }

            var queueAggs = new Dictionary<string, ClientAgg>();
            var waitAggs = new Dictionary<string, ClientAgg>();
            foreach (var rd in runs)
            {
                AddRun(queueAggs, rd.Queue);
                AddRun(waitAggs, rd.Waiting.Where(w => w.Id != "__rev_bomb").ToList());
            }

            // 必来 = 每一次模拟都出现的人（排队的 + 早上在门口等的）
            var certainQueue = queueAggs.Values.Where(a => a.RunsPresent >= ok).ToList();
            var certainWait = waitAggs.Values.Where(a => a.RunsPresent >= ok).ToList();
            var certainIds = new HashSet<string>(certainQueue.Select(a => a.Id));
            var certainWaitIds = new HashSet<string>(certainWait.Select(a => a.Id));

            // 每次模拟里必来者各自至少出现几次（同一个 id 一天来两次时按最少次数算必来）
            var minPerRun = new Dictionary<string, int>();
            foreach (var rd in runs)
            {
                var cnt = new Dictionary<string, int>();
                foreach (var o in rd.Queue) if (certainIds.Contains(o.Id)) { cnt.TryGetValue(o.Id, out int c); cnt[o.Id] = c + 1; }
                foreach (var o in rd.Waiting) if (certainWaitIds.Contains(o.Id)) { cnt.TryGetValue("w:" + o.Id, out int c); cnt["w:" + o.Id] = c + 1; }
                foreach (var kv in cnt)
                    minPerRun[kv.Key] = minPerRun.TryGetValue(kv.Key, out int m) ? Math.Min(m, kv.Value) : kv.Value;
            }
            int certainCount = minPerRun.Values.Sum();
            res.CertainCount = certainCount;

            var others = new List<int>();
            foreach (var rd in runs)
            {
                int total = rd.Queue.Count + rd.Waiting.Count(w => w.Id != "__rev_bomb");
                others.Add(Math.Max(0, total - certainCount));
            }
            res.OthersMin = others.Min();
            res.OthersMax = others.Max();
            res.OthersMean = others.Average();

            res.Header = string.Format("第 {0} 天 · {1} · 必来 {2} 人 · 其他 {3} 人", day, weekday, certainCount,
                res.OthersMin == res.OthersMax ? res.OthersMin.ToString() : res.OthersMin + "–" + res.OthersMax);
            res.SubHeader = string.Format("模拟 {0} 次{1} · 用时 {2} ms", ok, errors > 0 ? "（另有 " + errors + " 次出错）" : "", ms);

            foreach (var a in certainQueue) res.Certain.Add(MakeCertainRow(a, true, minPerRun.TryGetValue(a.Id, out int mq) ? mq : 1));
            foreach (var a in certainWait) res.Certain.Add(MakeCertainRow(a, false, minPerRun.TryGetValue("w:" + a.Id, out int mw) ? mw : 1));
            res.Certain.Sort((x, y) => x.SortPos.CompareTo(y.SortPos));

            // 其他客人：不是每次都出现的人，逐个列出（界面按来源分组，组内按概率排序）
            foreach (var a in queueAggs.Values)
                if (a.RunsPresent < ok) res.Possible.Add(MakeRow(a, ok, true));
            foreach (var a in waitAggs.Values)
            {
                if (a.RunsPresent >= ok) continue;
                var row = MakeRow(a, ok, false);
                row.AtDoor = true;
                res.Possible.Add(row);
            }
            res.Possible.Sort((x, y) =>
            {
                int c = y.Probability.CompareTo(x.Probability);
                return c != 0 ? c : string.CompareOrdinal(x.Name, y.Name);
            });

            if (runs.Any(rd => rd.Waiting.Any(w => w.Id == "__rev_bomb")))
                res.Note = (res.Note == null ? "" : res.Note + "  ") + "革命军会在柜台放炸弹（暗杀名单）。";
            return res;
        }

        /// <summary>必来者的一行：不显示概率，只显示排队位置。</summary>
        private static Row MakeCertainRow(ClientAgg a, bool queued, int times)
        {
            int savedMax = a.MaxPerRun;
            a.MaxPerRun = times; // 名字后的 ×N 只表示"一定会来几次"，多出来的次数算进"其他客人"
            var row = MakeRow(a, a.RunsPresent, queued);
            a.MaxPerRun = savedMax;
            if (!queued)
            {
                row.Chance = "开门时在柜台前";
                row.SortPos = -1;
                row.AtDoor = true;
            }
            else
            {
                var pos = a.Positions.OrderBy(x => x).ToList();
                int lo = pos[(int)(pos.Count * 0.1)], hi = pos[Math.Min(pos.Count - 1, (int)(pos.Count * 0.9))];
                row.Chance = lo == hi ? "第 " + lo + " 位" : "第 " + lo + "–" + hi + " 位";
            }
            return row;
        }

        private static void AddRun(Dictionary<string, ClientAgg> aggs, List<ClientObs> obs)
        {
            var perRun = new Dictionary<string, int>();
            foreach (var o in obs)
            {
                if (!aggs.TryGetValue(o.Id, out var a)) aggs[o.Id] = a = new ClientAgg { Id = o.Id };
                a.Add(o);
                perRun.TryGetValue(o.Id, out int c);
                perRun[o.Id] = c + 1;
            }
            foreach (var kv in perRun)
            {
                var a = aggs[kv.Key];
                a.RunsPresent++;
                if (kv.Value > a.MaxPerRun) a.MaxPerRun = kv.Value;
            }
        }

        private static Row MakeRow(ClientAgg a, int runs, bool showPosition)
        {
            double p = (double)a.RunsPresent / runs;
            var intent = ClientAgg.Top(a.Intents);
            string name = ClientAgg.Top(a.Names);
            if (string.IsNullOrEmpty(name)) name = a.Id;
            if (a.Names.Count > 1) name += " 等";
            if (a.MaxPerRun > 1) name += " ×" + a.MaxPerRun;
            string baseName = name;
            var marks = new List<string>();
            if (a.AugRuns > 0) marks.Add(a.AugRuns >= a.TotalCount ? "改造人" : "可能是改造人");
            if (a.WantedRuns > 0) marks.Add("通缉");
            if (a.FirstRuns > 0) marks.Add("插队");
            if (marks.Count > 0) name += " [" + string.Join("/", marks) + "]";

            string sourceKey = ClientAgg.Top(a.Sources);
            if (string.IsNullOrEmpty(sourceKey)) sourceKey = SourceTagger.UnknownLabel;
            string source = sourceKey;
            if (a.Sources.Count > 1) source += " 等";
            string factionId = ClientAgg.Top(a.Factions);

            var pos = a.Positions.OrderBy(x => x).ToList();
            double median = pos.Count > 0 ? pos[pos.Count / 2] : 0;
            string chance = Text.Percent(p);
            if (showPosition && pos.Count > 0)
            {
                int lo = pos[(int)(pos.Count * 0.1)], hi = pos[Math.Min(pos.Count - 1, (int)(pos.Count * 0.9))];
                chance += lo == hi ? " · 第" + lo + "位" : " · 第" + lo + "–" + hi + "位";
            }

            var row = new Row
            {
                Name = name,
                Faction = Text.Faction(factionId),
                Intent = Text.Intent(intent),
                Wants = Text.Wants(a, intent),
                Budget = Text.Budget(a),
                Source = source,
                Chance = chance,
                Probability = p,
                SortPos = median,
                Warn = a.AugRuns > 0 || a.WantedRuns > 0,
                BaseName = baseName,
                FactionId = factionId,
                SourceKey = sourceKey,
                Expected = runs > 0 ? (double)a.TotalCount / runs : 0,
            };
            row.Marks.AddRange(marks);
            return row;
        }

        // ================================================================== 开门后：实际队列

        private static ForecastResult ActualQueue(PlayerStore ps, StoreStation ss)
        {
            var res = new ForecastResult { Kind = ForecastResult.Mode.ActualQueue };
            int day = ss.dayCounter;
            string weekday = Text.Weekdays[((day % 7) + 7) % 7];
            res.Day = day;
            res.Weekday = weekday;
            var stack = ps.storeClientManager.clientStack;
            StoreClient current = null;
            try { current = ps.currentClientInstance?.GetClientBlueprint(); } catch { }
            int count = stack.Count(c => c != null) + (current != null ? 1 : 0);
            res.Header = "第 " + day + " 天 · " + weekday + " · 实际队列（已开门）：还剩 " + count + " 人";
            res.SubHeader = "商店已开门，以下是游戏当前真实的排队名单，不是预测。";
            if (current != null) res.Actual.Add(ActualRow(current, "柜台前"));
            int k = 1;
            foreach (var c in stack)
            {
                if (c == null) continue;
                res.Actual.Add(ActualRow(c, "排第" + k++ + "位"));
            }
            return res;
        }

        private static Row ActualRow(StoreClient c, string chance)
        {
            var o = ClientObs.From(c, "—", 0, false);
            var a = new ClientAgg { Id = o.Id };
            a.Add(o);
            a.RunsPresent = 1;
            var marks = new List<string>();
            if (c.isAug) marks.Add("改造人");
            if (c.isWanted) marks.Add("通缉");
            string baseName = string.IsNullOrEmpty(o.Name) ? o.Id : o.Name;
            var row = new Row
            {
                Name = baseName + (marks.Count > 0 ? " [" + string.Join("/", marks) + "]" : ""),
                Faction = Text.Faction(o.Faction),
                Intent = Text.Intent(o.Intent),
                Wants = Text.Wants(a, o.Intent),
                Budget = Text.Budget(a),
                Source = "—",
                Chance = chance,
                Probability = 1,
                BaseName = baseName,
                FactionId = o.Faction,
                SourceKey = "—",
                Expected = 1,
                AtCounter = chance == "柜台前",
            };
            row.Marks.AddRange(marks);
            return row;
        }
    }
}
