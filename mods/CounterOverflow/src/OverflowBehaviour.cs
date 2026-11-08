using System;
using UnityEngine;

namespace CounterOverflow
{
    /// <summary>
    /// 按键与调度：
    ///   ,（默认）  上一页        .（默认）  下一页        测试模式下 Ctrl+.  让顾客多摆一批货
    /// 只有货分成多页时翻页键才起作用（平时按 , . 什么都不做，不打扰）。
    /// 每 0.2 秒检查一次顾客是否离开；货第一次分页时提示一次。
    /// </summary>
    internal sealed class OverflowBehaviour : MonoBehaviour
    {
        private float nextTick;
        private int announcedPages;
        private readonly PageBar bar = new PageBar();

        private void OnDestroy()
        {
            try { bar.Destroy(); } catch { }
        }

        private void Update()
        {
            var cfg = OverflowMod.Config;
            try
            {
                if (cfg != null && !IsTyping())
                {
                    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    if (Input.GetKeyDown(cfg.NextKey))
                    {
                        if (ctrl && cfg.TestMode) RunTest();
                        else if (!ctrl) Next();
                    }
                    else if (Input.GetKeyDown(cfg.PrevKey) && !ctrl) Prev();
                }

                if (Time.unscaledTime >= nextTick)
                {
                    nextTick = Time.unscaledTime + 0.2f;
                    Overflow.Tick();
                }

                // 货第一次分成多页（或页数变多）：1 秒后提示一次，避免盖住顾客刚来时的其他提示
                if (!Overflow.Active) announcedPages = 0;
                else if (Overflow.PageCount > announcedPages && Overflow.LastPagedAt >= 0f && Time.unscaledTime - Overflow.LastPagedAt >= 1f)
                {
                    announcedPages = Overflow.PageCount;
                    string keys = OverflowConfig.KeyLabel(cfg?.PrevKey ?? KeyCode.Comma) + " / " + OverflowConfig.KeyLabel(cfg?.NextKey ?? KeyCode.Period);
                    Notify(Lang.T("柜台放不下，货分成了 " + Overflow.PageCount + " 页：点下方的翻页条，或按 " + keys + " 翻页",
                                  "The counter is full: goods split into " + Overflow.PageCount + " pages. Use the page bar below or press " + keys + " to flip"));
                }
            }
            catch (Exception e)
            {
                Log.Error("Update 出错: " + Log.Describe(e));
            }

            bar.Tick(cfg != null && cfg.PageBar); // 自己吞异常
        }

        internal static void Next() => Flip(+1);
        internal static void Prev() => Flip(-1);

        private static void Flip(int delta)
        {
            try
            {
                if (!Overflow.Active) return;
                if ((ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem) ||
                    (ItemMultiSelectHandler.current != null && ItemMultiSelectHandler.current.IsGroupDragging))
                {
                    Notify(Lang.T("先放下手里的东西再翻页", "Put down what you are holding before flipping"));
                    return;
                }
                int target = Overflow.CurrentPage + delta;
                if (target < 0)
                {
                    Notify(Lang.T("已经是第一页", "Already on the first page"));
                    return;
                }
                if (target >= Overflow.PageCount)
                {
                    Notify(Lang.T("已经是最后一页", "Already on the last page"));
                    return;
                }
                var msg = Overflow.GoTo(target);
                if (msg != null) Notify(msg);
            }
            catch (Exception e)
            {
                Log.Error("翻页出错: " + Log.Describe(e));
            }
        }

        private static void RunTest()
        {
            if (Game.Store?.currentClientInstance == null)
            {
                Notify(Lang.T("测试模式：柜台前要有顾客", "Test mode: a customer must be at the counter"));
                return;
            }
            int n = Overflow.SpawnTestBatch();
            Notify(Lang.T("测试：顾客多摆出 " + n + " 件货" + (Overflow.Active ? "，共 " + Overflow.PageCount + " 页" : ""),
                          "Test: the customer laid out " + n + " more item(s)" + (Overflow.Active ? ", " + Overflow.PageCount + " pages" : "")));
        }

        internal static void Notify(string msg)
        {
            try
            {
                if (StoreUIManager.Instance != null) StoreUIManager.Instance.Notify(msg);
                else Log.Info(msg);
            }
            catch (Exception e)
            {
                Log.Info(msg + "（提示框不可用: " + e.Message + "）");
            }
        }

        private static bool IsTyping()
        {
            try { return StoreUIManager.IsAnyTyping(); }
            catch { return false; }
        }
    }
}
