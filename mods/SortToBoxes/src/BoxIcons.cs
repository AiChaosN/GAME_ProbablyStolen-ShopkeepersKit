using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SortToBoxes
{
    /// <summary>
    /// 在仓库里的箱子（储藏区等）图标正中间画一个小图标，一眼看出这个柜子装的是什么：
    ///   - 箱子里有东西：画第一件物品（按格子位置，左上角那件）的图标；勾选过分类时优先选属于勾选类型的那件；
    ///   - 勾选了分类但箱子是空的：借库存里一件同类物品的图标；库存里也没有，就画该类型颜色的小色块。
    /// 图标是挂在箱子物品渲染节点（GameItemElement.handler）下的一个子节点，跟着箱子一起移动、随窗口一起显示隐藏；
    /// 拖动箱子时隐藏，避免留下残影。不拦截鼠标点击。
    /// 与“物品名称常显 / Show Item Names”兼容：它的名称条默认在底部、分页角标在左下角，本图标在正中间。
    /// 只影响显示：不改存档；连续出错就自动停用。
    /// </summary>
    internal sealed class BoxIcons
    {
        private const string NodeName = "CHAOS5090_STB_BoxIcon";
        private const float SizeRatio = 0.55f;   // 图标边长 = 箱子较短边 × 比例

        private sealed class Entry
        {
            internal GameItemElement Box;
            internal TreeNodeRender Node;
            internal string Key;          // 当前显示内容，没变就不重设
        }

        private readonly Dictionary<GameItemElement, Entry> entries = new Dictionary<GameItemElement, Entry>();
        private readonly Vector3[] corners = new Vector3[4];
        private float nextScan;
        private int errors;
        private bool disabled;
        private static Sprite chipSprite;

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
                    nextScan = Time.unscaledTime + 0.5f;
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
                    Log.Warn("箱子图标连续出错，已停用（不影响归箱与上柜台）: " + ex.Message);
                }
            }
        }

        internal void Clear()
        {
            foreach (var e in entries.Values) DestroyNode(e);
            entries.Clear();
        }

        private static void DestroyNode(Entry e)
        {
            try { if (e?.Node != null) UnityEngine.Object.Destroy(e.Node.gameObject); } catch { }
            if (e != null) e.Node = null;
        }

        private void Rescan()
        {
            var boxes = Stock.FindBoxes().OfType<GameItemElement>().Where(b => b.handler != null).ToList();
            var alive = new HashSet<GameItemElement>(boxes);
            foreach (var dead in entries.Keys.Where(k => k == null || k.handler == null || !alive.Contains(k)).ToList())
            {
                DestroyNode(entries[dead]);
                entries.Remove(dead);
            }

            List<GameItem> stock = null; // 只有空箱子勾了分类时才需要，懒加载
            foreach (var box in boxes)
            {
                Sprite sprite;
                Color tint;
                string key = Describe(box, ref stock, out sprite, out tint);
                entries.TryGetValue(box, out var e);
                if (key == null)
                {
                    if (e != null)
                    {
                        DestroyNode(e);
                        entries.Remove(box);
                    }
                    continue;
                }
                if (e == null)
                {
                    e = new Entry { Box = box };
                    entries[box] = e;
                }
                if (e.Node == null) e.Node = Create(box);
                if (e.Node == null) continue;
                if (e.Key != key)
                {
                    e.Key = key;
                    var img = e.Node.image;
                    img.sprite = sprite;
                    img.color = tint;
                    img.preserveAspect = true;
                }
            }
        }

        /// <summary>算出箱子该显示什么；不显示时返回 null。返回的 key 用来判断显示内容有没有变。</summary>
        private static string Describe(GameItem box, ref List<GameItem> stock, out Sprite sprite, out Color tint)
        {
            sprite = null;
            tint = Color.white;
            var labels = BoxLabels.Get(box);
            var contents = GraphUtils.FindChildrenType<GameItem>(box)
                .Where(it => it != null && !Stock.IsFixture(it))
                .OrderBy(it => SafeMinY(it))
                .ThenBy(it => SafeMinX(it))
                .ToList();

            GameItem rep = null;
            if (labels != null) rep = contents.FirstOrDefault(it => labels.Contains(Stock.PrimaryType(it) ?? ""));
            if (rep == null) rep = contents.FirstOrDefault();
            if (rep == null && labels != null)
            {
                // 空箱子但勾了分类：借库存里一件同类物品的图标
                if (stock == null) stock = Stock.AllGoods();
                string first = labels.OrderBy(t => t, StringComparer.Ordinal).First();
                rep = stock.FirstOrDefault(it => labels.Contains(Stock.PrimaryType(it) ?? ""));
                if (rep == null)
                {
                    sprite = ChipSprite();
                    tint = TypeColor(first);
                    tint.a = 0.85f;
                    return "chip:" + first;
                }
            }
            if (rep == null) return null;
            sprite = SpriteOf(rep);
            if (sprite == null) return null;
            tint = new Color(1f, 1f, 1f, 0.95f);
            return "item:" + sprite.GetInstanceID();
        }

        private static Sprite SpriteOf(GameItem item)
        {
            try { return (item as GameItemElement)?.background?.image?.sprite; }
            catch { return null; }
        }

        private static Color TypeColor(string type)
        {
            try { return TypeHelper.GetTypeColor(type); }
            catch { return new Color(0.6f, 0.6f, 0.6f, 1f); }
        }

        private static int SafeMinX(GameItem it)
        {
            try { return it.modifiedShape.minX; } catch { return 0; }
        }

        private static int SafeMinY(GameItem it)
        {
            try { return it.modifiedShape.minY; } catch { return 0; }
        }

        private static TreeNodeRender Create(GameItemElement box)
        {
            var parent = box.handler;
            if (parent == null) return null;
            // 清掉可能残留的旧节点（例如 mod 重新加载）
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
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
            var img = node.image;
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;   // 不挡鼠标：照常能点、拖箱子
            img.preserveAspect = true;
            img.enabled = true;
            var rt = node.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.SetAsLastSibling();
            node.gameObject.SetActive(true);
            return node;
        }

        private void Place(Entry e)
        {
            var box = e.Box;
            var node = e.Node;
            if (box == null || node == null || box.handler == null) return;
            bool dragged = false;
            try { dragged = box.fakeBackground != null && box.fakeBackground.image != null && box.fakeBackground.image.enabled; } catch { }
            if (node.gameObject.activeSelf == dragged) node.gameObject.SetActive(!dragged);
            if (dragged) return;
            if (!TryGetBounds(box, out var bounds)) return;
            float side = Mathf.Max(4f, Mathf.Min(bounds.width, bounds.height) * SizeRatio);
            var rt = node.rectTransform;
            var size = new Vector2(side, side);
            if ((rt.sizeDelta - size).sqrMagnitude > 0.01f) rt.sizeDelta = size;
            var pos = bounds.center;
            if ((rt.anchoredPosition - pos).sqrMagnitude > 0.01f) rt.anchoredPosition = pos;
        }

        /// <summary>箱子贴图在 handler 局部坐标里的范围（以 handler 矩形左下角为原点），会正确处理旋转。</summary>
        private bool TryGetBounds(GameItemElement box, out Rect bounds)
        {
            bounds = default(Rect);
            var icon = box.background;
            var space = box.handler;
            if (icon == null || space == null || icon.rectTransform == null || space.rectTransform == null) return false;
            icon.rectTransform.GetWorldCorners(corners);
            var srt = space.rectTransform;
            Vector2 a = srt.InverseTransformPoint(corners[0]);
            Vector2 b = srt.InverseTransformPoint(corners[1]);
            Vector2 c = srt.InverseTransformPoint(corners[2]);
            Vector2 d = srt.InverseTransformPoint(corners[3]);
            Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            Vector2 max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            Vector2 origin = srt.rect.min;
            float w = max.x - min.x, h = max.y - min.y;
            if (w < 1f || h < 1f)
            {
                var r = srt.rect;
                if (r.width < 1f || r.height < 1f) return false;
                bounds = new Rect(0f, 0f, r.width, r.height);
                return true;
            }
            bounds = new Rect(min.x - origin.x, min.y - origin.y, w, h);
            return true;
        }

        /// <summary>白色圆角方块，用类型颜色着色。</summary>
        private static Sprite ChipSprite()
        {
            if (chipSprite != null) return chipSprite;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int dx = Math.Min(x, n - 1 - x), dy = Math.Min(y, n - 1 - y);
                    bool corner = dx + dy < 5;
                    bool border = dx < 2 || dy < 2;
                    var col = corner ? new Color(1f, 1f, 1f, 0f) : border ? new Color(0.75f, 0.75f, 0.75f, 1f) : Color.white;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            chipSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return chipSprite;
        }
    }
}
