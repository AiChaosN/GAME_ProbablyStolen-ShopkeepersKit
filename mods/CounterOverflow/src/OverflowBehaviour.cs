using System;
using UnityEngine;

namespace CounterOverflow
{
    /// <summary>
    /// 每 0.2 秒检查一次：顾客走了就清队列，柜台有空位就把排队的货摆出来。
    /// 热键（默认 F3）翻页；测试模式下 Ctrl+F3 让顾客多摆一批货。
    /// 有货排队时弹提示（同一位顾客的新排队合并成一条，延迟 1 秒，避免盖住顾客刚来时的其他提示）。
    /// </summary>
    internal sealed class OverflowBehaviour : MonoBehaviour
    {
        private float nextTick;
        private int announcedFor = -1;     // 上次提示时累计的排队件数
        private float announceAt = -1f;

        private void Update()
        {
            try
            {
                var cfg = OverflowMod.Config;
                if (cfg == null) return;

                if (Input.GetKeyDown(cfg.Hotkey) && !IsTyping())
                {
                    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    if (ctrl && cfg.TestMode) RunTest();
                    else if (!ctrl) RunFlip();
                }

                if (Time.unscaledTime >= nextTick)
                {
                    nextTick = Time.unscaledTime + 0.2f;
                    Overflow.Tick();
                    if (Overflow.Pending.Count == 0) announcedFor = -1;
                }

                // 有新的货排队：1 秒后提示一次
                if (Overflow.Pending.Count > 0 && Overflow.QueuedThisClient != announcedFor && Overflow.LastQueuedAt >= 0f)
                {
                    if (announceAt < 0f) announceAt = Time.unscaledTime + 1f;
                    if (Time.unscaledTime >= announceAt)
                    {
                        announceAt = -1f;
                        announcedFor = Overflow.QueuedThisClient;
                        Notify("柜台放不下：还有 " + Overflow.Pending.Count + " 件货在排队。买走或挪开柜台上的货会自动补上，按 " + cfg.Hotkey + " 翻页");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Update 出错: " + Log.Describe(e));
            }
        }

        private static void RunFlip()
        {
            if (PlayerStore.Instance == null || EmporiumEntry.Instance == null) return;
            if (Overflow.Pending.Count == 0)
            {
                // 没有排队的货时不打扰（这个键可能被别的东西用到），只在有顾客时轻提示
                if (PlayerStore.Instance.currentClientInstance != null) Notify("没有排队的货，柜台上就是全部了");
                return;
            }
            var (down, up) = Overflow.Flip();
            Notify("翻页：换上 " + up + " 件" + (Overflow.Pending.Count > 0 ? "，还有 " + Overflow.Pending.Count + " 件排队" : "，排队的货都摆出来了") +
                   (down > 0 ? "（换下的 " + down + " 件排到队尾）" : ""));
        }

        private static void RunTest()
        {
            if (PlayerStore.Instance?.currentClientInstance == null)
            {
                Notify("测试模式：柜台前要有顾客");
                return;
            }
            int n = Overflow.SpawnTestBatch();
            Notify("测试：顾客多摆出 " + n + " 件货" + (Overflow.Pending.Count > 0 ? "，其中 " + Overflow.Pending.Count + " 件在排队" : ""));
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
