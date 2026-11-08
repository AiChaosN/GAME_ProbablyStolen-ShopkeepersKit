using System;
using UnityEngine;

namespace CounterOverflow
{
    /// <summary>
    /// 顾客头上提示框正下方的翻页条：“&lt; 上一页 (,)   第 2/4 页 · 还有 12 件没摆出   (.) 下一页 &gt;”。
    /// 用游戏自带的 CustomUI 框架建在 paper 图层（游戏自己的储物协调器窗口也在这一层），每帧跟着柜台窗口的位置走。
    /// 只有货分成多页时才显示。只影响界面：连续出错就自动停用（仍可用按键翻页）。
    /// </summary>
    internal sealed class PageBar
    {
        private RectTransform root;
        private CustomUIElement label, prevButton, nextButton;
        private int shownVersion = -1;
        private KeyCode shownPrev, shownNext;
        private readonly Vector3[] corners = new Vector3[4];
        private int errors;
        private bool disabled;

        internal void Tick(bool enabled)
        {
            if (disabled) return;
            try
            {
                var entry = EmporiumEntry.Instance;
                var window = entry != null ? entry.frontInvWindow : null;
                bool show = enabled && Overflow.Active && CustomUIManager.Instance != null && PlayerStore.Instance != null &&
                            window != null && window.IsVisible() && !(MapUIManager.Instance != null && MapUIManager.Instance.isOutside);
                if (!show)
                {
                    if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
                    return;
                }
                var cfg = OverflowMod.Config;
                if (root != null && cfg != null && (cfg.PrevKey != shownPrev || cfg.NextKey != shownNext)) Destroy(); // 换了按键：重建按钮文字
                if (root == null) Create();
                if (root == null) return;
                if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
                if (shownVersion != Overflow.Version) Refresh();
                Place(window);
                errors = 0;
            }
            catch (Exception e)
            {
                if (++errors >= 30)
                {
                    disabled = true;
                    Destroy();
                    Log.Warn("翻页条连续出错，已停用（仍可用按键翻页）: " + e.Message);
                }
            }
        }

        internal void Destroy()
        {
            try { if (root != null) UnityEngine.Object.Destroy(root.gameObject); } catch { }
            root = null;
            label = prevButton = nextButton = null;
            shownVersion = -1;
        }

        private void Create()
        {
            var mgr = CustomUIManager.Instance;
            string layerId = "overlay";
            try { if (CustomUIManager.HasLayer("paper")) layerId = "paper"; } catch { }
            var layer = mgr.GetLayerRoot(layerId);
            if (layer == null) return;
            var cfg = OverflowMod.Config ?? new OverflowConfig();
            var go = new GameObject("chaos5090_counter_overflow_pagebar", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);   // 底边中点对齐柜台窗口的顶边中点：条在柜台正上方
            rt.sizeDelta = new Vector2(420f, 40f);
            go.AddComponent<StoreUI>();         // 鼠标在条上时，游戏视为“在界面上”，不会误操作后面的物品
            bool meta = false;
            try { meta = CustomUIManager.IsMetaLayer(layerId); } catch { }
            var b = mgr.BuildInto(rt, meta, "chaos5090_counter_overflow_pagebar");
            b.BeginRow(10f, TextAnchor.MiddleCenter);
            b.AddButton("< 上一页 (" + OverflowConfig.KeyLabel(cfg.PrevKey) + ")", OverflowBehaviour.Prev);
            prevButton = b.Last;
            b.AddLabel("第 1/1 页");
            label = b.Last;
            b.AddButton("(" + OverflowConfig.KeyLabel(cfg.NextKey) + ") 下一页 >", OverflowBehaviour.Next);
            nextButton = b.Last;
            b.End();
            shownPrev = cfg.PrevKey;
            shownNext = cfg.NextKey;
            root = rt;
            shownVersion = -1;
        }

        private void Refresh()
        {
            shownVersion = Overflow.Version;
            int count = Overflow.PageCount, cur = Overflow.CurrentPage;
            int hidden = Overflow.HiddenCount();
            try { label?.SetText("第 " + (cur + 1) + "/" + count + " 页" + (hidden > 0 ? " · 还有 " + hidden + " 件没摆出" : "")); } catch { }
            try { if (prevButton?.button != null) prevButton.button.interactable = cur > 0; } catch { }
            try { if (nextButton?.button != null) nextButton.button.interactable = cur < count - 1; } catch { }
        }

        /// <summary>
        /// 放在顾客头上那条提示（游戏的提示框，“第 1/3 页”就显示在那里）的正下方；
        /// 找不到提示框时退回柜台正上方。
        /// </summary>
        private void Place(PixelWindow window)
        {
            if (root == null) return;
            var layer = root.parent as RectTransform;
            if (layer == null) return;

            RectTransform anchor = null;
            try
            {
                var rep = ReputationUIManager.Instance;
                if (rep != null && rep.reputationNotifier != null) anchor = rep.reputationNotifier.transform as RectTransform;
            }
            catch { anchor = null; }

            Vector2 screen;
            if (anchor != null)
            {
                anchor.GetWorldCorners(corners);                       // 0 左下, 1 左上, 2 右上, 3 右下
                Vector3 bottomCenter = (corners[0] + corners[3]) * 0.5f;
                screen = RectTransformUtility.WorldToScreenPoint(CameraOf(anchor), bottomCenter);
                screen.y -= 4f;
                root.pivot = new Vector2(0.5f, 1f);                    // 顶边中点对齐提示框底边中点：条在提示框正下方
            }
            else
            {
                var winRt = WindowRect.Visible(window);              // 看得见的柜台框（不是窗口根节点）
                if (winRt == null) return;
                winRt.GetWorldCorners(corners);
                Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
                screen = RectTransformUtility.WorldToScreenPoint(CameraOf(winRt), topCenter);
                screen.y += 4f;
                root.pivot = new Vector2(0.5f, 0f);
            }
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(layer, screen, CameraOf(layer), out var world))
                root.position = world;
        }

        private static Camera CameraOf(Transform t)
        {
            var canvas = t.GetComponentInParent<Canvas>();
            var rootCanvas = canvas != null ? canvas.rootCanvas : null;
            if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return rootCanvas.worldCamera;
        }
    }
}
