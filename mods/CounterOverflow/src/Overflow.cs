using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CounterOverflow
{
    /// <summary>
    /// 柜台分页：
    /// 原版里顾客往柜台（顾客那一侧 = EmporiumEntry.frontInvinvElement）摆货时，放不下的货会被直接销毁
    /// （GraphUtils.DestroyIfUnplaced(..., "front counter full")），柜台一格都没有时甚至不生成（"offer skipped"）。
    /// 本 mod 把放不下的货分到后面的“页”里：每一页是一个看不见的格子，形状和柜台当时一样，所以一页正好摆满一个柜台。
    /// 只有翻页时才换货（不会自动补位：你把货拿下来，空出的格子留给你放回去）。
    /// 顾客离开时，没摆出来的页按原版处理柜台剩货的方式处理：顾客的货销毁，属于你的（剧情赠品等）放进仓库。
    /// </summary>
    internal static class Overflow
    {
        private const string FrontFullContext = "front counter full";

        // pages[i] = 第 i+1 页的货存放的格子；正在柜台上显示的那一页，格子为 null 或空
        private static readonly List<GameGridInventory> pages = new List<GameGridInventory>();
        private static int current;
        private static StoreClientInstance owner;
        private static float ownerlessSince = -1f;

        internal static int PageCount => pages.Count;
        internal static int CurrentPage => current;
        internal static bool Active => pages.Count > 1;
        internal static int Version;          // 页面变化计数（界面据此刷新）
        internal static float LastPagedAt = -1f;

        internal static GameGridInventory Front => EmporiumEntry.Instance?.frontInvinvElement;

        private static StoreClientInstance CurrentClient()
        {
            try { return PlayerStore.Instance?.currentClientInstance; }
            catch { return null; }
        }

        /// <summary>隐藏页里的货有多少件（不含正在显示的这一页）。</summary>
        internal static int HiddenCount()
        {
            int n = 0;
            for (int i = 0; i < pages.Count; i++)
                if (i != current && pages[i] != null) n += pages[i].childItems.Count;
            return n;
        }

        private static GameGridInventory NewPageGrid()
        {
            var front = Front;
            int w = 18, h = 4;
            try
            {
                if (front?.inventoryShape != null)
                {
                    w = Math.Max(1, front.inventoryShape.width);
                    h = Math.Max(1, front.inventoryShape.height);
                }
            }
            catch { }
            var grid = new GameGridInventory(w, h);
            grid.mayInventoryAddItemFunc = (_, __) => true;
            try { grid.handler?.gameObject.SetActive(false); } catch { }
            return grid;
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

        /// <summary>“放不下就销毁”→ 改为放进后面的页。只管顾客往柜台摆货这一处（context = "front counter full"）。</summary>
        private static bool DestroyPrefix(GameItem item, string context, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal) return true;
            try
            {
                if (context != FrontFullContext || !(OverflowMod.Config?.Enabled ?? false)) return true;
                if (item == null || item.parentInventory != null) return true;
                if (item is GameItemElement el && el.IsDestroyed()) return true;
                if (!AddToPages(item)) return true;
                __result = true;   // 对调用方来说“已经摆上了”（在后面的页里）
                return false;
            }
            catch (Exception e)
            {
                Log.Warn("分页失败，按原版处理: " + e.Message);
                return true;
            }
        }

        /// <summary>柜台一格都没有时，原版直接跳过不生成；这里改为照常生成，交给上面的分页。</summary>
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
                Log.Warn("生成分页物品失败，按原版处理: " + e.Message);
                return true;
            }
        }

        // ------------------------------------------------------------------ 分页

        /// <summary>把一件放不下的货放进最后一页；最后一页满了（或正在显示）就新开一页。</summary>
        private static bool AddToPages(GameItem item)
        {
            var cur = CurrentClient();
            if (pages.Count == 0)
            {
                owner = cur;
                pages.Add(null);   // 第 1 页 = 柜台上正在显示的货
                current = 0;
            }
            else if (owner == null) owner = cur;

            int last = pages.Count - 1;
            int start = item.unitCount;
            if (last != current && pages[last] != null && GraphUtils.TryAcceptAll(pages[last], item) == 0)
            {
                Changed(item, last);   // 全放进去了（可叠加的货也可能并进了已有的一叠）
                return true;
            }
            var grid = NewPageGrid();
            int left = GraphUtils.TryAcceptAll(grid, item);
            if (left > 0 && left >= start && grid.childItems.Count == 0)
                return false;   // 比整个柜台还大，永远摆不出来：按原版处理
            pages.Add(grid);
            Changed(item, pages.Count - 1);
            return left == 0;      // 一叠只放进去一部分时，剩下的交给原版处理
        }

        private static void Changed(GameItem item, int page)
        {
            Version++;
            LastPagedAt = Time.unscaledTime;
            Log.Info("柜台放不下，放到第 " + (page + 1) + " 页：" + Name(item) + (item.unitCount > 1 ? " ×" + item.unitCount : "") + "（共 " + pages.Count + " 页）");
        }

        /// <summary>翻到第 target 页（从 0 开始）。返回提示文字；没有翻页时返回 null。</summary>
        internal static string GoTo(int target)
        {
            var front = Front;
            if (front == null || pages.Count <= 1) return null;
            if (target < 0 || target >= pages.Count || target == current) return null;

            // 1. 把柜台上顾客的货（你还没买的）收进当前页的格子
            if (pages[current] == null) pages[current] = NewPageGrid();
            var stash = pages[current];
            var spill = new List<GameItem>();
            foreach (var it in front.childItems.Where(x => x != null && !GeneralHelper.IsItemOwned(x)).ToList())
            {
                if (!Place(it, stash)) spill.Add(it);   // 按原坐标收进本页（同形状的格子，一般都能原样放下）
            }
            // 极少数情况下（摆放顺序不同）收不回同形状的格子：放到最后新开的一页
            foreach (var it in spill)
            {
                var grid = NewPageGrid();
                if (MoveInto(it, grid)) pages.Add(grid);
                else Log.Warn("翻页时 " + Name(it) + " 收不进任何一页，留在柜台上");
            }

            // 2. 把目标页的货摆上柜台
            var show = pages[target];
            int up = 0, stuck = 0;
            if (show != null)
            {
                foreach (var it in show.childItems.ToList())
                {
                    if (Place(it, front)) up++;   // 按原坐标摆回柜台：你拿走的那件留下的空位不会被占
                    else stuck++;
                }
            }
            current = target;

            // 3. 去掉已经空了的页（货都被买走了）。
            //    但秤上（你这一侧的柜台）还放着顾客的货时不删：那件货可能就是从某个空页拿下来的，要留着那一页让你放回去。
            bool customerGoodsAway = false;
            try
            {
                var back = EmporiumEntry.Instance?.backInvinvElement;
                customerGoodsAway = back != null && back.childItems.Any(x => x != null && !GeneralHelper.IsItemOwned(x));
            }
            catch { customerGoodsAway = true; }
            for (int i = pages.Count - 1; i >= 0 && pages.Count > 1 && !customerGoodsAway; i--)
            {
                if (i == current) continue;
                if (pages[i] != null && pages[i].childItems.Count == 0)
                {
                    pages.RemoveAt(i);
                    if (i < current) current--;
                }
            }
            // 只剩一页且没有摆不出来的货 = 回到原版状态
            if (pages.Count == 1 && (pages[0] == null || pages[0].childItems.Count == 0)) pages[0] = null;
            Version++;
            Log.Info("翻到第 " + (current + 1) + "/" + pages.Count + " 页：摆出 " + up + " 件" + (stuck > 0 ? "，" + stuck + " 件柜台放不下" : ""));
            string msg = pages.Count > 1 ? "第 " + (current + 1) + "/" + pages.Count + " 页" : "货都在柜台上了";
            if (stuck > 0) msg += "（本页有 " + stuck + " 件放不下：柜台上有别的东西占着位置）";
            return msg;
        }

        /// <summary>先按物品原来的坐标/朝向放（保持每一页的摆法，空位留着）；那个位置被占了才自动找空位。</summary>
        private static bool Place(GameItem it, GameGridInventory target)
        {
            GridShape shape = null;
            try { if (it.modifiedShape != null) shape = new GridShapeBuilder(it.modifiedShape); } catch { }
            if (shape != null && PlaceExact(it, target, shape)) return true;
            return MoveInto(it, target);
        }

        private static bool PlaceExact(GameItem it, GameGridInventory target, GridShape shape)
        {
            try
            {
                var slot = target.TryInventorySlot(it, shape);   // 只检查这个位置：会不会越界、和别的货重叠
                if (slot == null || slot.numTransfer < it.unitCount) return false;
                slot.TryAcceptOnce(it.unitCount);
                return ReferenceEquals(it.parentInventory, target);
            }
            catch
            {
                return false;
            }
        }

        private static bool MoveInto(GameItem it, GameGridInventory target)
        {
            try
            {
                return GraphUtils.TryAcceptAll(target, it) == 0 && ReferenceEquals(it.parentInventory, target);
            }
            catch (Exception e)
            {
                Log.Warn("移动 " + Name(it) + " 出错: " + e.Message);
                return false;
            }
        }

        /// <summary>每隔一小段时间调用：顾客走了（或换了人）就收尾。</summary>
        internal static void Tick()
        {
            if (pages.Count == 0) return;
            var cur = CurrentClient();
            if (owner == null && cur != null) owner = cur;
            if (cur != null && ReferenceEquals(cur, owner))
            {
                ownerlessSince = -1f;
                return;
            }
            if (cur == null && owner == null)
            {
                // 还没有顾客实例（极少见）：等一会儿再说
                if (ownerlessSince < 0f) ownerlessSince = Time.unscaledTime;
                if (Time.unscaledTime - ownerlessSince < 10f) return;
            }
            Release(cur == null ? "顾客离开" : "换了顾客");
        }

        /// <summary>顾客走了：没摆出来的页里，顾客的货销毁（与原版处理柜台剩货一致），属于你的放进仓库。</summary>
        internal static void Release(string why)
        {
            int destroyed = 0, kept = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                var grid = pages[i];
                if (grid == null) continue;
                foreach (var item in grid.childItems.ToList())
                {
                    try
                    {
                        if (item == null || (item is GameItemElement el && el.IsDestroyed())) continue;
                        if (GeneralHelper.IsItemOwned(item))
                        {
                            var wh = EmporiumEntry.Instance?.invElement;
                            if (wh != null && MoveInto(item, wh))
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
                        Log.Warn("清理分页物品出错: " + e.Message);
                    }
                }
            }
            bool had = pages.Count > 1;
            Reset();
            if (had) Log.Info(why + "，收起没摆出来的页：销毁 " + destroyed + " 件（顾客的）" + (kept > 0 ? "，" + kept + " 件属于你的放进仓库" : ""));
        }

        /// <summary>读档时：旧的分页作废（物品已随场景销毁）。</summary>
        internal static void Reset()
        {
            pages.Clear();
            current = 0;
            owner = null;
            ownerlessSince = -1f;
            Version++;
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
