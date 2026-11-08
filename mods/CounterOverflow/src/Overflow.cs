using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CounterOverflow
{
    /// <summary>
    /// 柜台溢出排队：
    /// 原版里顾客往柜台（顾客那一侧 = EmporiumEntry.frontInvinvElement）摆货时，放不下的货会被直接销毁
    /// （GraphUtils.DestroyIfUnplaced(..., "front counter full")），柜台一格都没有时甚至不生成（"offer skipped"）。
    /// 本 mod 把这些货改为“排队”：暂存在一个看不见的格子里，柜台腾出位置后自动摆出来；也可以按热键翻页。
    /// 顾客离开时，排队的货按原版处理柜台剩货的方式处理：顾客的货销毁，属于你的（剧情赠品等）放进仓库。
    /// </summary>
    internal static class Overflow
    {
        private const string FrontFullContext = "front counter full";

        internal static readonly List<GameItem> Pending = new List<GameItem>();
        private static StoreClientInstance owner;
        private static float ownerlessSince = -1f;
        private static GameGridInventory park;

        internal static int QueuedThisClient;   // 本位顾客累计排队过的件数（提示用）
        internal static float LastQueuedAt = -1f;

        internal static GameGridInventory Front => EmporiumEntry.Instance?.frontInvinvElement;

        private static GameGridInventory Park
        {
            get
            {
                if (park == null)
                {
                    park = new GameGridInventory(48, 48);
                    park.mayInventoryAddItemFunc = (_, __) => true;
                    try { park.handler?.gameObject.SetActive(false); } catch { }
                }
                return park;
            }
        }

        private static StoreClientInstance CurrentClient()
        {
            try { return PlayerStore.Instance?.currentClientInstance; }
            catch { return null; }
        }

        // ------------------------------------------------------------------ 补丁

        private const string HarmonyId = "CounterOverflow.patches";
        private static Harmony harmony;
        internal static bool Applied { get; private set; }

        internal static void Apply()
        {
            try
            {
                harmony = new Harmony(HarmonyId);
                var destroy = AccessTools.Method(typeof(GraphUtils), "DestroyIfUnplaced", new[] { typeof(GameItem), typeof(string) });
                var addById = AccessTools.Method(typeof(PlayerStore), "TryAddDirectSellingItemToTable",
                    new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(int) });
                if (destroy == null || destroy.DeclaringType != typeof(GraphUtils))
                {
                    Log.Warn("找不到 GraphUtils.DestroyIfUnplaced，柜台不丢货不可用");
                    return;
                }
                // 最后执行：其他 mod（例如访客预测的沙盒）要求跳过时，这里就什么都不做
                harmony.Patch(destroy, prefix: new HarmonyMethod(AccessTools.Method(typeof(Overflow), nameof(DestroyPrefix))) { priority = Priority.Last });
                if (addById != null && addById.DeclaringType == typeof(PlayerStore))
                    harmony.Patch(addById, prefix: new HarmonyMethod(AccessTools.Method(typeof(Overflow), nameof(AddByIdPrefix))) { priority = Priority.Last });
                else
                    Log.Warn("找不到 PlayerStore.TryAddDirectSellingItemToTable(string, ...)：柜台完全满时仍会有货不生成");
                Applied = true;
            }
            catch (Exception e)
            {
                Log.Error("打补丁失败，柜台不丢货不可用: " + Log.Describe(e));
            }
        }

        internal static void Remove()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            harmony = null;
            Applied = false;
        }

        /// <summary>“放不下就销毁”→ 改为排队。只管顾客往柜台摆货这一处（context = "front counter full"）。</summary>
        private static bool DestroyPrefix(GameItem item, string context, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal) return true;
            try
            {
                if (context != FrontFullContext || !(OverflowMod.Config?.Enabled ?? false)) return true;
                if (item == null || item.parentInventory != null) return true;
                if (item is GameItemElement el && el.IsDestroyed()) return true;
                if (!Queue(item)) return true;
                __result = true;   // 对调用方来说“已经摆上了”（稍后才会真正出现在柜台上）
                return false;
            }
            catch (Exception e)
            {
                Log.Warn("排队失败，按原版处理: " + e.Message);
                return true;
            }
        }

        /// <summary>柜台一格都没有时，原版直接跳过不生成；这里改为照常生成，交给上面的排队。</summary>
        private static bool AddByIdPrefix(PlayerStore __instance, string identifier, bool isOwend, bool isStolen, bool ignorePerk, int forcedHeat,
                                          ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal) return true;
            try
            {
                if (!(OverflowMod.Config?.Enabled ?? false)) return true;
                var front = Front;
                if (front == null || front.CanTakeAnything()) return true; // 还有空位：走原版
                var item = DirectoryMaster.Item(identifier);
                if (item == null) return true;
                __result = __instance.TryAddDirectSellingItemToTable(item, isOwend, isStolen, ignorePerk, forcedHeat);
                return false;
            }
            catch (Exception e)
            {
                Log.Warn("生成排队物品失败，按原版处理: " + e.Message);
                return true;
            }
        }

        // ------------------------------------------------------------------ 排队与摆出

        private static bool Queue(GameItem item)
        {
            var cur = CurrentClient();
            if (Pending.Count == 0 || owner == null) owner = cur;
            int left = GraphUtils.TryAcceptAll(Park, item);
            if (left > 0 || item.parentInventory == null) return false;
            Pending.Add(item);
            QueuedThisClient++;
            LastQueuedAt = Time.unscaledTime;
            Log.Info("柜台放不下，排队：" + Name(item) + (item.unitCount > 1 ? " ×" + item.unitCount : "") + "（排队中 " + Pending.Count + " 件）");
            return true;
        }

        /// <summary>每隔一小段时间调用：顾客走了就清掉队列；柜台有空位就把排队的货摆出来。返回本次摆出的件数。</summary>
        internal static int Tick()
        {
            if (Pending.Count == 0) return 0;
            var cur = CurrentClient();
            if (owner == null && cur != null) owner = cur;
            if (cur == null || !ReferenceEquals(cur, owner))
            {
                if (cur == null && owner == null)
                {
                    // 还没有顾客实例（极少见）：等一会儿再说
                    if (ownerlessSince < 0f) ownerlessSince = Time.unscaledTime;
                    if (Time.unscaledTime - ownerlessSince < 10f) return 0;
                }
                Release(cur == null ? "顾客离开" : "换了顾客");
                return 0;
            }
            ownerlessSince = -1f;
            return Fill();
        }

        /// <summary>把排队的货按顺序摆上柜台，直到放不下为止（形状放不下的那件跳过，试后面的）。</summary>
        internal static int Fill()
        {
            var front = Front;
            if (front == null || Pending.Count == 0) return 0;
            int placed = 0;
            foreach (var item in Pending.ToList())
            {
                if (!front.CanTakeAnything()) break;
                if (item == null || (item is GameItemElement el && el.IsDestroyed()))
                {
                    Pending.Remove(item);
                    continue;
                }
                try
                {
                    GraphUtils.TryAcceptAll(front, item);
                }
                catch (Exception e)
                {
                    Log.Warn("摆出 " + Name(item) + " 出错: " + e.Message);
                    continue;
                }
                if (ReferenceEquals(item.parentInventory, front))
                {
                    Pending.Remove(item);
                    placed++;
                }
            }
            return placed;
        }

        /// <summary>翻页：柜台上顾客的货（你还没买的）排到队尾，换上排队的货。返回 (换下, 摆出)。</summary>
        internal static (int down, int up) Flip()
        {
            var front = Front;
            if (front == null || Pending.Count == 0) return (0, 0);
            var showing = front.childItems.Where(it => it != null && !GeneralHelper.IsItemOwned(it)).ToList();
            int down = 0;
            foreach (var it in showing)
            {
                try
                {
                    if (GraphUtils.TryAcceptAll(Park, it) == 0 && ReferenceEquals(it.parentInventory, park))
                    {
                        Pending.Add(it);
                        down++;
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("换下 " + Name(it) + " 出错: " + e.Message);
                }
            }
            int up = Fill();
            Log.Info("翻页：换下 " + down + " 件，摆出 " + up + " 件，排队中 " + Pending.Count + " 件");
            return (down, up);
        }

        /// <summary>顾客走了：顾客的货销毁（与原版处理柜台剩货一致），属于你的放进仓库。</summary>
        internal static void Release(string why)
        {
            if (Pending.Count == 0) return;
            int destroyed = 0, kept = 0;
            foreach (var item in Pending.ToList())
            {
                try
                {
                    if (item == null || (item is GameItemElement el && el.IsDestroyed())) continue;
                    if (GeneralHelper.IsItemOwned(item))
                    {
                        var wh = EmporiumEntry.Instance?.invElement;
                        if (wh != null && GraphUtils.TryAcceptAll(wh, item) == 0)
                        {
                            kept++;
                            continue;
                        }
                    }
                    item.parentInventory?.Expel(item);
                    item.Destroy();
                    destroyed++;
                }
                catch (Exception e)
                {
                    Log.Warn("清理排队物品出错: " + e.Message);
                }
            }
            Pending.Clear();
            owner = null;
            ownerlessSince = -1f;
            QueuedThisClient = 0;
            Log.Info(why + "，清掉排队的货：销毁 " + destroyed + " 件（顾客的）" + (kept > 0 ? "，" + kept + " 件属于你的放进仓库" : ""));
        }

        /// <summary>读档时：旧的队列作废（物品已随场景销毁）。</summary>
        internal static void Reset()
        {
            Pending.Clear();
            owner = null;
            ownerlessSince = -1f;
            QueuedThisClient = 0;
            LastQueuedAt = -1f;
        }

        internal static string Name(GameItem it)
        {
            try
            {
                var n = it.GetDisplayName(true);
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            return it?.identifier ?? "?";
        }

        // ------------------------------------------------------------------ 测试模式

        // 体积大的设备，用来人为制造柜台溢出（都是原版会由卖家摆出来的东西）
        private static readonly string[] TestItems =
        {
            "fridge", "water_tank", "moisture_farm", "evaporator", "toolbox", "bottle_printer", "water_purifier",
            "auto_butcher", "aquascan", "storage_bay_expansion_kit", "desequencer", "aug_scanner", "black_lamp",
            "magnifier", "labeler", "ingot_book", "stamp_guide", "cigarette_guide",
        };

        /// <summary>测试：让当前顾客照原版流程再往柜台摆一批大件。返回生成的件数。</summary>
        internal static int SpawnTestBatch()
        {
            var ps = PlayerStore.Instance;
            if (ps == null || CurrentClient() == null) return 0;
            int n = 0;
            foreach (var id in TestItems)
            {
                try
                {
                    if (ps.TryAddDirectSellingItemToTable(id)) n++;
                }
                catch (Exception e)
                {
                    Log.Warn("测试物品 " + id + " 生成失败: " + e.Message);
                }
            }
            Log.Info("测试：让顾客多摆出 " + n + " 件");
            return n;
        }
    }
}
