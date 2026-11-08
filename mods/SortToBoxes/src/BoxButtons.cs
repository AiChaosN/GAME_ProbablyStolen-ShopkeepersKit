using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SortToBoxes
{
    /// <summary>
    /// 打开的箱子窗口标题栏里、关闭按钮（×）左边的“分类”按钮，点它打开分类面板。
    /// 做法与游戏自己的关闭按钮一样：一个 TreeNodeRender 挂在窗口自己的节点下（和 × 同一个父节点、同样的锚点），
    /// 用 InitEventHandler + eventHandler.mouseClickFunc 响应点击。所以按钮跟着窗口移动、随窗口显示隐藏，和 × 永远贴着。
    /// 关闭按钮的节点是 PixelWindow 的私有字段 topCloseButton，用字面量 typeof(PixelWindow) 查找（扫描器不要求权限）。
    /// 只影响界面：连续出错就自动停用（分类面板仍可用 Ctrl+归箱热键 打开）。
    /// </summary>
    internal sealed class BoxButtons
    {
        private const string NodeName = "CHAOS5090_STB_CategoryButton";
        private const float Gap = 2f;          // 和 × 之间的间距（像素）
        private const float PadX = 4f;         // 文字左右留白

        private sealed class Entry
        {
            internal GameItem Box;
            internal PixelWindow Window;
            internal TreeNodeRender Node;
            internal TreeNodeRender Text;
            internal bool Hover;
        }

        private readonly Dictionary<PixelWindow, Entry> entries = new Dictionary<PixelWindow, Entry>();
        private float nextScan;
        private int errors;
        private bool disabled;
        private static FieldInfo closeField;
        private static bool closeFieldTried;
        private static Sprite plate;

        internal void Tick(bool enabled)
        {
            if (disabled) return;
            try
            {
                bool inStore = enabled && EmporiumEntry.Instance != null && PlayerStore.Instance != null && RenderHandler.current != null;
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
                    Log.Warn("箱子窗口上的“分类”按钮连续出错，已停用（仍可用 Ctrl+归箱热键 指着箱子打开分类面板）: " + ex.Message);
                }
            }
        }

        internal void Clear()
        {
            foreach (var e in entries.Values)
            {
                try { if (e.Node != null) UnityEngine.Object.Destroy(e.Node.gameObject); } catch { }
            }
            entries.Clear();
        }

        private void Rescan()
        {
            var visible = new Dictionary<PixelWindow, GameItem>();
            foreach (var box in Stock.FindBoxes())
            {
                var w = box.contentWindow;
                if (w != null && w.IsVisible() && !visible.ContainsKey(w)) visible[w] = box;
            }
            foreach (var w in entries.Keys.ToList())
            {
                var e = entries[w];
                if (visible.TryGetValue(w, out var b) && ReferenceEquals(b, e.Box) && e.Node != null) continue;
                try { if (e.Node != null) UnityEngine.Object.Destroy(e.Node.gameObject); } catch { }
                entries.Remove(w);
            }
            foreach (var kv in visible)
            {
                if (entries.ContainsKey(kv.Key)) continue;
                var e = Create(kv.Key, kv.Value);
                if (e != null) entries[kv.Key] = e;
            }
        }

        private static TreeNodeRender CloseButton(PixelWindow w)
        {
            try
            {
                if (!closeFieldTried)
                {
                    closeFieldTried = true;
                    closeField = AccessTools.Field(typeof(PixelWindow), "topCloseButton");
                }
                return closeField?.GetValue(w) as TreeNodeRender;
            }
            catch { return null; }
        }

        private Entry Create(PixelWindow w, GameItem box)
        {
            var parent = w.handler;
            if (parent == null) return null;
            for (int i = parent.transform.childCount - 1; i >= 0; i--)   // 清掉可能残留的旧按钮
            {
                var child = parent.transform.GetChild(i);
                if (child != null && child.name == NodeName) UnityEngine.Object.Destroy(child.gameObject);
            }

            var node = TreeNodeRender.Instantiate();
            if (node == null || node.image == null || node.rectTransform == null)
            {
                if (node != null) UnityEngine.Object.Destroy(node.gameObject);
                return null;
            }
            node.SetParent(parent);
            node.gameObject.name = NodeName;
            node.image.sprite = Plate();
            node.image.type = Image.Type.Sliced;
            node.image.color = NormalColor;
            node.image.raycastTarget = true;
            node.rectTransform.localScale = Vector3.one;

            var text = TreeNodeRender.InstantiateText();
            if (text != null)
            {
                text.SetParent(node);
                text.gameObject.name = NodeName + "_Text";
                var t = text.text;
                t.text = Lang.T("分类", "Sort");
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center;
                t.enableWordWrapping = false;
                t.raycastTarget = false;
                try { t.color = RenderHandler.current.textColorRed; } catch { }
                var trt = text.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                trt.localScale = Vector3.one;
            }

            var e = new Entry { Box = box, Window = w, Node = node, Text = text };
            node.InitEventHandler();
            var target = box;
            node.eventHandler.mouseClickFunc.Add(_ =>
            {
                BoxLabelPanel.Open(target);
                return true;
            });
            node.eventHandler.mouseEnterFunc.Add(_ =>
            {
                e.Hover = true;
                node.image.color = HoverColor;
                return true;
            });
            node.eventHandler.mouseExitFunc.Add(_ =>
            {
                e.Hover = false;
                node.image.color = NormalColor;
                return true;
            });
            node.rectTransform.SetAsLastSibling();
            node.gameObject.SetActive(true);
            Place(e);
            return e;
        }

        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color HoverColor = new Color(1f, 0.92f, 0.75f, 1f);

        /// <summary>每帧跟 × 对齐（窗口改大小时 × 会移动）。</summary>
        private static void Place(Entry e)
        {
            var node = e.Node;
            if (node == null || e.Window == null) return;
            var rt = node.rectTransform;
            float textW = 32f;
            try
            {
                var t = e.Text?.text;
                if (t != null)
                {
                    string want = Lang.T("分类", "Sort");   // 游戏里切换语言后跟着变
                    if (t.text != want) t.text = want;
                    textW = Mathf.Ceil(t.preferredWidth);
                }
            }
            catch { }
            float width = textW + PadX * 2f;

            var close = CloseButton(e.Window);
            if (close != null && close.rectTransform != null)
            {
                var crt = close.rectTransform;
                rt.anchorMin = crt.anchorMin;
                rt.anchorMax = crt.anchorMax;
                rt.pivot = crt.pivot;
                float height = Mathf.Max(8f, crt.sizeDelta.y);
                var size = new Vector2(width, height);
                if ((rt.sizeDelta - size).sqrMagnitude > 0.01f) rt.sizeDelta = size;
                // × 的左边：本按钮右边缘 = × 左边缘 - 间距。两者轴心相同（p），
                // × 左边缘 = x× - p·w×，本按钮的 x = 右边缘 - (1-p)·宽度
                float p = crt.pivot.x;
                float x = crt.anchoredPosition.x - p * crt.sizeDelta.x - Gap - (1f - p) * width;
                var pos = new Vector2(x, crt.anchoredPosition.y);
                if ((rt.anchoredPosition - pos).sqrMagnitude > 0.01f) rt.anchoredPosition = pos;
            }
            else
            {
                // 找不到 ×：放在标题栏右上角（窗口坐标），尽量不挡东西
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                float height = Math.Max(8, e.Window.titleHeight);
                rt.sizeDelta = new Vector2(width, height);
                rt.anchoredPosition = new Vector2(e.Window.widthPixels - width - e.Window.xPadding - 20f, -e.Window.yPadding);
            }
        }

        /// <summary>带边框的小底板（九宫格，可任意拉伸）。</summary>
        private static Sprite Plate()
        {
            if (plate != null) return plate;
            const int n = 12;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            var border = new Color(0.33f, 0.24f, 0.18f, 1f);
            var fill = new Color(0.93f, 0.88f, 0.76f, 1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool corner = (x == 0 || x == n - 1) && (y == 0 || y == n - 1);
                    bool edge = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                    tex.SetPixel(x, y, corner ? new Color(0, 0, 0, 0) : edge ? border : fill);
                }
            tex.Apply();
            plate = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
            return plate;
        }
    }
}
