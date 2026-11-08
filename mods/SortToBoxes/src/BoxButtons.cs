using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SortToBoxes
{
    /// <summary>
    /// 打开的箱子窗口右上角外侧挂一个“分类”按钮（点它打开分类面板）。
    /// 按钮放在 CustomUI 的图层里，每帧跟着箱子窗口的位置走；箱子窗口关掉时隐藏。
    /// 这部分只影响界面：连续出错就自动停用（分类面板仍可用 Ctrl+归箱热键 打开）。
    /// </summary>
    internal sealed class BoxButtons
    {
        private sealed class Entry
        {
            internal GameItem Box;
            internal RectTransform Root;
        }

        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly Vector3[] corners = new Vector3[4];
        private float nextScan;
        private int errors;
        private bool disabled;

        internal void Tick(bool enabled)
        {
            if (disabled) return;
            try
            {
                bool inStore = enabled && CustomUIManager.Instance != null && EmporiumEntry.Instance != null &&
                               PlayerStore.Instance != null && !(MapUIManager.Instance != null && MapUIManager.Instance.isOutside);
                if (!inStore)
                {
                    if (entries.Count > 0) Clear();
                    return;
                }
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + 0.25f;
                    Rescan();
                }
                foreach (var e in entries.Values) Place(e);
                errors = 0;
            }
            catch (Exception ex)
            {
                if (++errors >= 30)
                {
                    disabled = true;
                    Clear();
                    Log.Warn("箱子旁的“分类”按钮连续出错，已停用（仍可用 Ctrl+归箱热键 指着箱子打开分类面板）: " + ex.Message);
                }
            }
        }

        internal void Clear()
        {
            foreach (var e in entries.Values)
            {
                try { if (e.Root != null) UnityEngine.Object.Destroy(e.Root.gameObject); } catch { }
            }
            entries.Clear();
        }

        private void Rescan()
        {
            var visible = new Dictionary<int, GameItem>();
            foreach (var box in Stock.FindBoxes())
            {
                var w = box.contentWindow;
                if (w != null && w.IsVisible()) visible[Stock.Uid(box)] = box;
            }
            foreach (var uid in entries.Keys.ToList())
            {
                if (visible.TryGetValue(uid, out var b) && ReferenceEquals(b, entries[uid].Box) && entries[uid].Root != null) continue;
                try { if (entries[uid].Root != null) UnityEngine.Object.Destroy(entries[uid].Root.gameObject); } catch { }
                entries.Remove(uid);
            }
            foreach (var kv in visible)
            {
                if (entries.ContainsKey(kv.Key)) continue;
                var root = Create(kv.Value, kv.Key);
                if (root != null) entries[kv.Key] = new Entry { Box = kv.Value, Root = root };
            }
        }

        private static RectTransform Create(GameItem box, int uid)
        {
            var mgr = CustomUIManager.Instance;
            string layerId = BoxLabelPanel.LayerId();
            var layer = mgr.GetLayerRoot(layerId);
            if (layer == null) return null;
            var go = new GameObject("chaos5090_stb_box_button_" + uid, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(1f, 0f); // 右下角对齐箱子窗口的右上角，按钮落在窗口上方
            rt.sizeDelta = new Vector2(96f, 36f);
            go.AddComponent<StoreUI>();     // 鼠标在按钮上时，游戏视为“在界面上”，不会误操作后面的物品
            bool meta = false;
            try { meta = CustomUIManager.IsMetaLayer(layerId); } catch { }
            var b = mgr.BuildInto(rt, meta, "chaos5090_stb_box_button_" + uid);
            var target = box;
            b.AddButton("分类", () => BoxLabelPanel.Open(target));
            b.WithTooltip("设置这个箱子收哪些货");
            return rt;
        }

        private void Place(Entry e)
        {
            if (e.Root == null || e.Box == null) return;
            var w = e.Box.contentWindow;
            bool show = w != null && w.IsVisible();
            if (e.Root.gameObject.activeSelf != show) e.Root.gameObject.SetActive(show);
            if (!show) return;
            var winRt = w.rectTransform;
            winRt.GetWorldCorners(corners);                 // 0 左下, 1 左上, 2 右上, 3 右下
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(CameraOf(winRt), corners[2]);
            var layer = e.Root.parent as RectTransform;
            if (layer != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(layer, screen, CameraOf(layer), out var world))
                e.Root.position = world;
        }

        private static Camera CameraOf(Transform t)
        {
            var canvas = t.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas : null;
            if (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return root.worldCamera;
        }
    }
}
