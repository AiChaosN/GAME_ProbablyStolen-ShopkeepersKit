using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedScientist
{
    /// <summary>
    /// 科学家机器窗口标题栏里、关闭按钮（×）左边的动作按钮（“制造 / 提取 / 镶嵌”），点一下立刻做，不用等过夜。
    /// 做法和归类入箱的“分类”按钮一样（照游戏自己的关闭按钮）：一个 TreeNodeRender 挂在窗口自己的节点下，和 × 同一个父节点、同样的锚点，
    /// 用 InitEventHandler + eventHandler.mouseClickFunc 响应点击；按钮跟着窗口移动、随窗口显示隐藏。
    /// 关闭按钮的节点是 PixelWindow 的私有字段 topCloseButton，用字面量 typeof(PixelWindow) 查找（扫描器不要求权限）。
    /// 机器在物品工厂里登记自己的窗口和动作（Register），这里每帧只看登记过的窗口，不用全仓库扫描。
    /// 只影响界面：连续出错就自动停用。
    /// </summary>
    internal sealed class MachineButtons
    {
        private const string NodeName = "CHAOS5090_AS_ActionButton";
        private const float Gap = 2f;
        private const float PadX = 5f;

        private sealed class Registration
        {
            internal GameItem Machine;
            internal Func<string> Label;
            internal Func<string> Run;   // 返回给玩家看的结果
        }

        private sealed class Entry
        {
            internal PixelWindow Window;
            internal Registration Reg;
            internal TreeNodeRender Node;
            internal TreeNodeRender Text;
        }

        private static readonly Dictionary<PixelWindow, Registration> registry = new Dictionary<PixelWindow, Registration>();
        private readonly Dictionary<PixelWindow, Entry> entries = new Dictionary<PixelWindow, Entry>();
        private float nextScan;
        private int errors;
        private bool disabled;
        private static FieldInfo closeField;
        private static bool closeFieldTried;
        private static Sprite plate;

        /// <summary>机器的物品工厂里调用：这个窗口打开时显示一个按钮，点了执行 run。</summary>
        internal static void Register(PixelWindow window, GameItem machine, Func<string> label, Func<string> run)
        {
            if (window == null || machine == null) return;
            registry[window] = new Registration { Machine = machine, Label = label, Run = run };
        }

        internal void Tick()
        {
            if (disabled) return;
            try
            {
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + 0.2f;
                    Rescan();
                }
                foreach (var e in entries.Values) Place(e);
            }
            catch (Exception ex)
            {
                if (errors == 0) Log.Warn("机器按钮出错（只记第一次）: " + Log.Describe(ex));
                if (++errors >= 30)
                {
                    disabled = true;
                    Clear();
                    Log.Warn("机器窗口上的按钮连续出错，已停用: " + ex.Message);
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

        private static bool Gone(GameItem it)
        {
            try { return it == null || (it is GameItemElement el && el.IsDestroyed()); }
            catch { return true; }
        }

        private void Rescan()
        {
            // 机器没了（卖掉、销毁、换了存档）：去掉登记
            foreach (var w in registry.Keys.ToList())
                if (Gone(registry[w].Machine) || w.handler == null) registry.Remove(w);

            foreach (var w in entries.Keys.ToList())
            {
                var e = entries[w];
                bool keep = registry.TryGetValue(w, out var reg) && ReferenceEquals(reg, e.Reg) && e.Node != null && w.IsVisible();
                if (keep) continue;
                try { if (e.Node != null) UnityEngine.Object.Destroy(e.Node.gameObject); } catch { }
                entries.Remove(w);
            }
            foreach (var kv in registry)
            {
                if (entries.ContainsKey(kv.Key)) continue;
                bool visible;
                try { visible = kv.Key.IsVisible(); } catch { visible = false; }
                if (!visible) continue;
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

        private Entry Create(PixelWindow w, Registration reg)
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
            node.image.enabled = true;
            node.rectTransform.localScale = Vector3.one;

            var text = TreeNodeRender.InstantiateText();
            if (text != null)
            {
                text.SetParent(node);
                text.gameObject.name = NodeName + "_Text";
                var t = text.text;
                t.text = SafeLabel(reg);
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center;
                t.enableWordWrapping = false;
                t.raycastTarget = false;
                t.enabled = true;
                t.color = TextColor;
                var trt = text.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                trt.localScale = Vector3.one;
            }

            var e = new Entry { Window = w, Reg = reg, Node = node, Text = text };
            node.InitEventHandler();
            node.eventHandler.mouseClickFunc.Add(_ =>
            {
                string msg;
                try
                {
                    msg = GeneralHelper.IsItemOwned(reg.Machine) ? reg.Run?.Invoke()
                        : Lang.T("这台机器还不是你的", "This machine isn't yours yet");
                }
                catch (Exception ex)
                {
                    Log.Error("机器动作出错: " + Log.Describe(ex));
                    msg = Lang.T("出错了，详见日志", "Something went wrong; see the log");
                }
                if (!string.IsNullOrEmpty(msg)) Notify(msg);
                return true;
            });
            node.eventHandler.mouseEnterFunc.Add(_ => { node.image.color = HoverColor; return true; });
            node.eventHandler.mouseExitFunc.Add(_ => { node.image.color = NormalColor; return true; });
            node.rectTransform.SetAsLastSibling();
            node.gameObject.SetActive(true);
            Place(e);
            return e;
        }

        private static string SafeLabel(Registration reg)
        {
            try { return reg.Label?.Invoke() ?? "?"; }
            catch { return "?"; }
        }

        internal static void Notify(string msg)
        {
            try
            {
                if (StoreUIManager.Instance != null) StoreUIManager.Instance.Notify(msg);
                else Log.Info(msg);
            }
            catch { Log.Info(msg); }
        }

        // 仿照旁边的关闭按钮 ×：浅灰底、深色描边、深酒红色的字；鼠标移上去底色变亮
        private static readonly Color NormalColor = new Color(0.78f, 0.76f, 0.75f, 1f);
        private static readonly Color HoverColor = new Color(0.92f, 0.90f, 0.89f, 1f);
        private static readonly Color TextColor = new Color(0.36f, 0.15f, 0.20f, 1f);

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
                    string want = SafeLabel(e.Reg);   // 游戏里切换语言后跟着变
                    if (t.text != want) t.text = want;
                    textW = Mathf.Max(24f, Mathf.Ceil(t.preferredWidth));
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
                float p = crt.pivot.x;
                float x = crt.anchoredPosition.x - p * crt.sizeDelta.x - Gap - (1f - p) * width;
                var pos = new Vector2(x, crt.anchoredPosition.y);
                if ((rt.anchoredPosition - pos).sqrMagnitude > 0.01f) rt.anchoredPosition = pos;
            }
            else
            {
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
            var border = new Color(0.16f, 0.15f, 0.17f, 1f);
            var fill = Color.white;
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

    /// <summary>每帧驱动机器按钮。</summary>
    internal sealed class ScientistBehaviour : MonoBehaviour
    {
        private readonly MachineButtons buttons = new MachineButtons();

        private void Update() => buttons.Tick();

        private void OnDestroy()
        {
            try { buttons.Clear(); } catch { }
        }
    }
}
