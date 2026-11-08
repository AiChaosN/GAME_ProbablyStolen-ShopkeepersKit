using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VisitorForecast.Forecast;

namespace VisitorForecast.UI
{
    /// <summary>
    /// 用游戏自己的平板界面显示预测：复制“结束一天”平板的预制体（EndOfDayUIManager.tabletPrefab），
    /// 通过 CustomUIManager.CreateWindow 放到 paper 层（游戏的纸质/平板面板所在层；没有时退到 overlay 层），
    /// 内容用 TabletUI / TabletStyle 的同款控件拼出来，字体走游戏的 FontLoader（中文字体）。
    ///
    /// 平板布局（与游戏的 TabletStyle 常量一致）：
    ///   sectionHeader  标题“访客预测”/“实际队列”
    ///   infoHost       第 N 天 · 星期X + 两块统计（必来 / 其他）
    ///   body(scroll)   必来列表 → 其他客人（按来源分组）
    ///   footer         模拟次数/用时 + 说明
    ///   buttonHost     重新预测 / 关闭
    /// 所有构建都可能抛异常（预制体结构变了等），由 ForecastBehaviour 捕获后退回 IMGUI 窗口。
    /// </summary>
    internal sealed class NativeForecastWindow
    {
        internal const string WindowId = "VisitorForecast.window";

        private const float RowW = TabletStyle.BODY_ROW_W;    // 502
        private const float PadX = TabletStyle.BODY_PAD_X;    // 12
        private const float TagH = TabletStyle.TAG_H;         // 23
        private const float RightW = 120f;
        private const float ChipH = 48f;
        private const float ChipLabelW = 64f;
        private const float DefaultOffsetX = 550f;            // 默认放在左侧（与谈判平板的 +550 对称）

        private static readonly Color TagLight = TabletStyle.Hex("f4f0e6");

        private readonly ForecastBehaviour owner;
        private CustomUIWindow window;
        private TabletFrame frame;
        private RectTransform info;
        private Transform homeParent;
        private bool paperLayer;
        private TMP_FontAsset forcedFont;   // 非中文语言时强制使用游戏的简体中文字体（本 mod 的文字都是中文）
        private string builtSignature;
        private ForecastResult.Mode? builtKind;
        private Vector2? savedPosition;

        internal NativeForecastWindow(ForecastBehaviour owner)
        {
            this.owner = owner;
        }

        internal bool IsShown => window != null && window.root != null && window.root.activeSelf;

        internal bool Available(out string why)
        {
            why = null;
            if (CustomUIManager.Instance == null) why = "没有 CustomUIManager";
            else if (FontLoader.Instance == null) why = "没有 FontLoader";
            else if (EndOfDayUIManager.Instance == null || EndOfDayUIManager.Instance.tabletPrefab == null) why = "当前场景没有游戏的平板界面";
            return why == null;
        }

        // ================================================================== 窗口生命周期

        private void EnsureWindow()
        {
            if (window != null && window.root != null && frame != null) return;
            ForgetWindow();

            var mgr = CustomUIManager.Instance;
            var prefab = EndOfDayUIManager.Instance.tabletPrefab;
            paperLayer = CustomUIManager.HasLayer(CustomUIManager.LAYER_PAPER);
            var builder = mgr.CreateWindow(WindowId, null, paperLayer ? CustomUIManager.LAYER_PAPER : CustomUIManager.LAYER_OVERLAY, prefab);
            if (builder == null) throw new InvalidOperationException("CustomUIManager.CreateWindow 返回 null");
            var w = builder.Handle;
            try
            {
                if (w == null || w.root == null) throw new InvalidOperationException("平板窗口没有创建出来");
                // 与游戏的谈判平板相同：SetDraggable 在没有 CustomUIWindowFrame 时给根节点加 UIDragHandler
                builder.SetDraggable(true);
                // paper 层的 Esc 由游戏自己处理（关掉最上层的面板）；overlay 层交给 CustomUIManager
                if (!paperLayer) builder.SetCloseOnEscape(true);
                builder.OnClose(() => OnWindowClosed(w));
                builder.GetWindow(); // 先隐藏，Render 里再显示

                var f = w.root.GetComponent<TabletFrame>();
                if (f == null) throw new InvalidOperationException("平板预制体上没有 TabletFrame");
                Require(f.sectionHeader, "sectionHeader");
                Require(f.infoHost, "infoHost");
                Require(f.separatorTop, "separatorTop");
                Require(f.body, "body");
                Require(f.content, "content");
                Require(f.footer, "footer");
                Require(f.buttonHost, "buttonHost");

                w.root.name = "VisitorForecastTablet";
                homeParent = w.root.transform.parent;

                // StoreUI：让游戏把鼠标悬停在本窗口上视为“在界面上”（不再点到后面的物品）；paper 层的锚点通常已自动加上
                if (w.root.GetComponent<StoreUI>() == null) w.root.AddComponent<StoreUI>().isPaperUI = paperLayer;
                if (paperLayer && w.root.GetComponent<PaperPanelFocusStamp>() == null) w.root.AddComponent<PaperPanelFocusStamp>();

                if (f.closeButton != null)
                {
                    f.closeButton.onClick.AddListener(() => owner.Hide());
                    TabletUI.NoKeyboardFocus(f.closeButton);
                }
                if (f.arrowsWatermark != null) f.arrowsWatermark.gameObject.SetActive(false);
                if (f is EndOfDayTabletFrame eod && eod.infoLine != null) eod.infoLine.gameObject.SetActive(false);
                PrepareText(f.sectionHeader);

                info = TabletUI.NewRect(f.infoHost, "VF_Info", 0f, 0f, TabletStyle.INNER_W, 100f);
                AddRaycastBlocker(f, w);
                PlaceInitially(w);

                frame = f;
                window = w;
            }
            catch
            {
                try { w?.Close(); } catch { }
                ForgetWindow();
                throw;
            }
        }

        private static void Require(UnityEngine.Object o, string name)
        {
            if (o == null) throw new InvalidOperationException("平板预制体缺少 " + name);
        }

        /// <summary>预制体里的标题可能带本地化组件，会把我们写的文字改回去：照 CustomUIWindow.SetTitle 的做法关掉。</summary>
        private static void PrepareText(TMP_Text t)
        {
            if (t == null) return;
            foreach (var b in t.GetComponents<Behaviour>())
                if (b != null && b.GetType().Name == "LocalizeStringEvent") b.enabled = false;
            var loc = t.GetComponent<TMPLocalizer>();
            if (loc != null) loc.ChangeFontOnly = true;
        }

        /// <summary>整块平板放一张透明的射线目标，保证点在窗口任何位置都不会穿透到游戏里。</summary>
        private static void AddRaycastBlocker(TabletFrame f, CustomUIWindow w)
        {
            Transform host = f.panel != null ? (Transform)f.panel : w.root.transform;
            var img = TabletUI.NewImage(host, "VF_RaycastBlocker", 0f, 0f, 0f, 0f, null, Color.clear, raycast: true);
            TabletUI.Stretch(img.rectTransform);
            img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            img.transform.SetAsFirstSibling();
        }

        private void PlaceInitially(CustomUIWindow w)
        {
            var rt = w.Rect;
            if (rt == null) return;
            if (savedPosition.HasValue)
            {
                rt.anchoredPosition = savedPosition.Value;
                return;
            }
            var parent = rt.parent as RectTransform;
            bool centered = rt.anchorMin == rt.anchorMax && Mathf.Abs(rt.anchorMin.x - 0.5f) < 0.01f && Mathf.Abs(rt.pivot.x - 0.5f) < 0.01f;
            if (parent == null || !centered) return;
            float room = (parent.rect.width - rt.rect.width * rt.localScale.x) * 0.5f;
            if (room > 0f) rt.anchoredPosition = new Vector2(-Mathf.Min(DefaultOffsetX, room), rt.anchoredPosition.y);
        }

        private void SavePosition()
        {
            try
            {
                var rt = window?.Rect;
                if (rt != null && rt.parent == homeParent) savedPosition = rt.anchoredPosition;
            }
            catch { }
        }

        private void OnWindowClosed(CustomUIWindow w)
        {
            if (!ReferenceEquals(w, window)) return;
            SavePosition();
            ForgetWindow();
        }

        private void ForgetWindow()
        {
            window = null;
            frame = null;
            info = null;
            homeParent = null;
            builtSignature = null;
            builtKind = null;
        }

        internal void Hide()
        {
            if (!IsShown) return;
            SavePosition();
            window.Hide();
        }

        internal void Destroy()
        {
            var w = window;
            ForgetWindow();
            if (w != null && w.root != null) w.Close();
        }

        internal bool ContainsScreenPoint(Vector2 screenPos)
        {
            if (window == null || window.root == null || !window.root.activeInHierarchy) return false;
            RectTransform rt = frame != null && frame.panel != null ? frame.panel : window.Rect;
            if (rt == null) return false;
            Camera cam = null;
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var rootCanvas = canvas.rootCanvas;
                if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = rootCanvas.worldCamera;
            }
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam);
        }

        // ================================================================== 渲染

        internal void Render()
        {
            EnsureWindow();
            UpdateFont();
            var r = owner.Result;
            bool busy = owner.Busy || r == null;
            string sig = Signature(busy, r);
            bool wasShown = IsShown;
            if (!wasShown)
            {
                window.Show();
                if (paperLayer) PaperPanelFocusStamp.BumpPanel(window.root);
            }
            if (wasShown && sig == builtSignature) return; // 实际队列每秒刷新，内容没变就不重建

            bool keepScroll = wasShown && !busy && r.Kind == ForecastResult.Mode.ActualQueue && builtKind == ForecastResult.Mode.ActualQueue;
            float scrollPos = keepScroll && frame.scroll != null ? frame.scroll.verticalNormalizedPosition : 1f;
            builtSignature = null;
            Build(busy, r);
            builtSignature = sig;
            builtKind = busy ? (ForecastResult.Mode?)null : r.Kind;
            if (frame.scroll != null)
            {
                if (keepScroll)
                {
                    Canvas.ForceUpdateCanvases();
                    frame.scroll.verticalNormalizedPosition = scrollPos;
                }
                else TabletUI.ScrollToTop(frame.scroll);
            }
        }

        private void UpdateFont()
        {
            string code = "";
            try { code = LocHelper.GetCurrentLocaleCode() ?? ""; } catch { }
            forcedFont = code.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? null : FontLoader.Instance?.simplifiedChineseFont;
        }

        private string Signature(bool busy, ForecastResult r)
        {
            var sb = new StringBuilder(256);
            sb.Append(forcedFont != null ? 'F' : 'N').Append('|').Append(owner.HotkeyLabel).Append('|');
            if (busy) return sb.Append("busy|").Append(ForecastView.BusyText(owner.Runs)).ToString();
            sb.Append((int)r.Kind).Append('|').Append(r.Header).Append('|').Append(r.SubHeader).Append('|').Append(r.Warning)
              .Append('|').Append(r.Note).Append('|').Append(r.Message).Append('|').Append(r.CertainCount).Append('|')
              .Append(r.OthersMin).Append('-').Append(r.OthersMax).Append('|');
            void Rows(List<Row> rows)
            {
                foreach (var x in rows)
                    sb.Append(x.Name).Append('\u0001').Append(x.Faction).Append('\u0001').Append(x.Intent).Append('\u0001').Append(x.Wants)
                      .Append('\u0001').Append(x.Budget).Append('\u0001').Append(x.Source).Append('\u0001').Append(x.Chance).Append('\u0002');
                sb.Append('|');
            }
            Rows(r.Certain);
            Rows(r.Actual);
            Rows(r.Possible);
            return sb.ToString();
        }

        private void Build(bool busy, ForecastResult r)
        {
            CustomUIBuilder.ClearContainer(frame.content);
            CustomUIBuilder.ClearContainer(frame.footer);
            CustomUIBuilder.ClearContainer(frame.buttonHost);
            CustomUIBuilder.ClearContainer(info);
            frame.infoHost.gameObject.SetActive(true); // 文字要在激活状态下才能量出正确宽度

            float infoH;
            ForecastResult.Mode kind;
            if (busy)
            {
                var ps = Game.PlayerStoreOrNull;
                bool open = ps != null && ps.storeState == PlayerStore.StoreState.OPEN;
                kind = open ? ForecastResult.Mode.ActualQueue : ForecastResult.Mode.Forecast;
                SetHeader(open ? ForecastView.ActualTitle : ForecastView.Title);
                infoH = InfoLine(ForecastView.BusyText(owner.Runs));
                CenterText("请稍候…");
            }
            else
            {
                kind = r.Kind;
                switch (r.Kind)
                {
                    case ForecastResult.Mode.Forecast:
                        SetHeader(ForecastView.Title);
                        infoH = ForecastInfo(r);
                        ForecastBody(r);
                        break;
                    case ForecastResult.Mode.ActualQueue:
                        SetHeader(ForecastView.ActualTitle);
                        infoH = ActualInfo(r);
                        ActualBody(r);
                        break;
                    default:
                        SetHeader(ForecastView.Title);
                        infoH = string.IsNullOrEmpty(ForecastView.DayLine(r)) ? 0f : InfoLine(ForecastView.DayLine(r));
                        MessageBody(r);
                        break;
                }
            }
            info.sizeDelta = new Vector2(TabletStyle.INNER_W, Mathf.Max(1f, infoH));
            TabletUI.LayoutSection(frame, infoH, infoH > 0f, TabletStyle.END_BODY_BOTTOM);
            Footer(busy, r, kind);
            Buttons(busy, kind);
        }

        private void SetHeader(string text)
        {
            var h = frame.sectionHeader;
            h.text = text;
            ApplyFont(h);
        }

        // ------------------------------------------------------------------ 信息区

        private float InfoLine(string text)
        {
            var t = Text(info, "Line", TabletStyle.INFO_PAD_X, 0f, TabletStyle.INNER_W - 2f * TabletStyle.INFO_PAD_X, TabletStyle.END_INFO_H,
                TabletStyle.END_INFO_SIZE, TabletStyle.INK, TextAlignmentOptions.MidlineLeft, text: text);
            TabletUI.AutoSize(t, 12f);
            return TabletStyle.END_INFO_H;
        }

        private float ForecastInfo(ForecastResult r)
        {
            float y = InfoLine(ForecastView.DayLine(r)) + 8f;
            float w = (TabletStyle.INNER_W - 2f * TabletStyle.INFO_PAD_X - 16f) * 0.5f;
            Chip(TabletStyle.INFO_PAD_X, y, w, "必来", r.CertainCount + " 人",
                "每一次模拟都会来的人（含早上在门口等、开门就站在柜台前的人）。");
            Chip(TabletStyle.INFO_PAD_X + w + 16f, y, w, "其他", ForecastView.Range(r.OthersMin, r.OthersMax) + " 人",
                string.Format("除必来者以外的来客人数（最少–最多），平均 {0:0.0} 人。\n随机顾客、情报贩子、新面孔等每次读档都会重新随机，下面按来源分组列出各自的概率。", r.OthersMean));
            return y + ChipH;
        }

        private float ActualInfo(ForecastResult r)
        {
            string day = ForecastView.DayLine(r);
            float y = InfoLine((day.Length > 0 ? day + " · " : "") + "已开门（每秒刷新）") + 8f;
            float w = (TabletStyle.INNER_W - 2f * TabletStyle.INFO_PAD_X - 16f) * 0.5f;
            Row current = r.Actual.Count > 0 && r.Actual[0].AtCounter ? r.Actual[0] : null;
            Chip(TabletStyle.INFO_PAD_X, y, w, "还剩", r.Actual.Count + " 人", "柜台前的顾客 + 还在排队的人。");
            Chip(TabletStyle.INFO_PAD_X + w + 16f, y, w, "柜台前", current != null ? (current.BaseName ?? current.Name) : "—", null);
            return y + ChipH;
        }

        private void Chip(float x, float y, float w, string label, string value, string tip)
        {
            var rt = TabletUI.NewRect(info, "Chip", x, y, w, ChipH);
            Border(rt, TabletStyle.INK);
            TabletUI.NewFlat(rt, "LabelPlate", 0f, 0f, ChipLabelW, ChipH, TabletStyle.INK);
            var l = Text(rt, "Label", 0f, 0f, ChipLabelW, ChipH, 18f, TabletStyle.SUB_HEADER_TEXT, TextAlignmentOptions.Center, text: label);
            TabletUI.AutoSize(l, 12f);
            var v = Text(rt, "Value", ChipLabelW + 12f, 0f, w - ChipLabelW - 20f, ChipH, 28f, TabletStyle.INK, TextAlignmentOptions.MidlineLeft, text: value);
            v.fontStyle = FontStyles.Bold;
            v.overflowMode = TextOverflowModes.Ellipsis;
            TabletUI.AutoSize(v, 14f);
            Tooltip(rt.gameObject, tip);
        }

        /// <summary>2px 边框：优先用皮肤里的 frame2（九宫格），没有就画四条线。</summary>
        private void Border(RectTransform rt, Color color)
        {
            if (frame.frame2 != null)
            {
                TabletUI.NewStretched(rt, "Border", frame.frame2, color);
                return;
            }
            float w = rt.sizeDelta.x, h = rt.sizeDelta.y;
            TabletUI.NewFlat(rt, "BorderT", 0f, 0f, w, 2f, color);
            TabletUI.NewFlat(rt, "BorderB", 0f, h - 2f, w, 2f, color);
            TabletUI.NewFlat(rt, "BorderL", 0f, 0f, 2f, h, color);
            TabletUI.NewFlat(rt, "BorderR", w - 2f, 0f, 2f, h, color);
        }

        // ------------------------------------------------------------------ 列表

        private void ForecastBody(ForecastResult r)
        {
            if (!string.IsNullOrEmpty(r.Warning)) Banner(r.Warning);
            if (!string.IsNullOrEmpty(r.Note)) NoteRow(r.Note);

            SectionHeader("必来", r.CertainCount + " 人");
            var certain = ForecastView.SortedCertain(r.Certain);
            if (certain.Count == 0) CenterText(ForecastView.EmptyCertain);
            foreach (var row in certain)
                VisitorRow(row, row.AtDoor ? "柜台前" : row.Chance, null, true);

            SectionHeader("其他客人", ForecastView.Range(r.OthersMin, r.OthersMax) + " 人 · 平均 " + r.OthersMean.ToString("0.0"));
            var groups = ForecastView.GroupOthers(r.Possible);
            if (groups.Count == 0)
            {
                CenterText("没有其他可能来的客人");
                return;
            }
            foreach (var g in groups)
            {
                GroupHeader(g);
                foreach (var row in g.Rows) VisitorRow(row, null, row.Probability, false);
                if (g.Minor.Count > 0) MinorRow(g);
            }
            NoteRow("其他客人每次读档都会重新随机，概率仅供参考；“约 N 人”是该来源的预计人数。");
        }

        private void ActualBody(ForecastResult r)
        {
            if (!string.IsNullOrEmpty(r.Warning)) Banner(r.Warning);
            SectionHeader("队列", r.Actual.Count + " 人");
            if (r.Actual.Count == 0) CenterText("队列已空");
            foreach (var row in r.Actual) VisitorRow(row, row.Chance, null, false);
        }

        private void MessageBody(ForecastResult r)
        {
            if (!string.IsNullOrEmpty(r.Warning)) Banner(r.Warning);
            if (!string.IsNullOrEmpty(r.Message)) WrappedRow(r.Message, 18f, TabletStyle.INK, 10f);
            if (!string.IsNullOrEmpty(r.Note)) NoteRow(r.Note);
        }

        private void SectionHeader(string left, string right)
        {
            const float h = TabletStyle.SUB_HEADER_H; // 28
            var root = TabletUI.RowRoot(frame.content, "SectionHeader", h + 6f);
            TabletUI.NewFlat(root, "Plate", 0f, 4f, RowW, h, TabletStyle.INK);
            var l = Text(root, "Left", PadX, 4f, 240f, h, 18f, TabletStyle.SUB_HEADER_TEXT, TextAlignmentOptions.MidlineLeft, text: left);
            l.fontStyle = FontStyles.Bold;
            var rt = Text(root, "Right", RowW - PadX - 240f, 4f, 240f, h, 16f, TabletStyle.SUB_HEADER_TEXT, TextAlignmentOptions.MidlineRight, text: right);
            TabletUI.AutoSize(rt, 12f);
        }

        private void GroupHeader(ForecastView.SourceGroup g)
        {
            const float h = 32f;
            var root = TabletUI.RowRoot(frame.content, "Group", h);
            TabletUI.NewFlat(root, "Bar", 0f, 7f, 4f, 20f, TabletStyle.INK);
            var l = Text(root, "Source", PadX, 6f, 300f, 22f, 18f, TabletStyle.INK, TextAlignmentOptions.MidlineLeft, text: g.Source);
            l.fontStyle = FontStyles.Bold;
            TabletUI.AutoSize(l, 12f);
            Text(root, "Expected", RowW - PadX - 160f, 6f, 160f, 22f, 16f, TabletStyle.INK, TextAlignmentOptions.MidlineRight, text: ForecastView.Expected(g.Expected));
            if (frame.dottedLine != null)
            {
                var dots = TabletUI.NewImage(root, "Dots", 2f, h - 3f, RowW - 2f, 2f, frame.dottedLine, TabletStyle.INK);
                dots.type = Image.Type.Tiled;
            }
            else TabletUI.NewFlat(root, "Line", 0f, h - 3f, RowW, 2f, TabletStyle.ROW_SEPARATOR);
        }

        /// <summary>
        /// 一位顾客两到三行：
        ///   名字 [门口等候][改造人][阵营] ……………… 排队位置 / 概率条+百分比
        ///   [来做什么] 想要（自动换行，最多三行，全文在悬停提示里）
        ///   预算 · 来源
        /// </summary>
        private void VisitorRow(Row r, string right, double? probability, bool showSource)
        {
            var root = TabletUI.RowRoot(frame.content, "Visitor", 60f);
            float x = PadX;
            float y = 4f;

            // 第一行：右侧
            float rightW = RightW;
            if (probability.HasValue)
            {
                double p = Math.Max(0.0, Math.Min(1.0, probability.Value));
                const float pctW = 50f, barW = 64f;
                rightW = pctW + 6f + barW;
                Text(root, "Chance", RowW - PadX - pctW, y, pctW, TagH, 18f, TabletStyle.INK, TextAlignmentOptions.MidlineRight, text: ForecastView.Percent(p));
                float bx = RowW - PadX - rightW;
                TabletUI.NewFlat(root, "BarTrack", bx, y + 8.5f, barW, 6f, TabletStyle.SCROLL_TRACK);
                TabletUI.NewFlat(root, "BarFill", bx, y + 8.5f, Mathf.Max(2f, barW * (float)p), 6f, TabletStyle.INK);
            }
            else if (!string.IsNullOrEmpty(right))
            {
                var rt = Text(root, "Right", RowW - PadX - rightW, y, rightW, TagH, 18f, TabletStyle.INK, TextAlignmentOptions.MidlineRight, text: right);
                TabletUI.AutoSize(rt, 12f);
            }

            // 第一行：标签（先量宽度，再决定名字能占多宽）
            var tags = TabletUI.NewRect(root, "Tags", 0f, y, 10f, TagH);
            float tx = 0f;
            if (r.AtCounter) tx += Tag(tags, tx, 0f, "正在柜台", TabletStyle.GREEN, TagLight) + 4f;
            if (r.AtDoor) tx += Tag(tags, tx, 0f, "门口等候", TabletStyle.GREEN, TagLight) + 4f;
            foreach (var m in r.Marks)
            {
                bool red = m != "插队";
                tx += Tag(tags, tx, 0f, m, red ? TabletStyle.BARGAIN_RED : TabletStyle.INK, red ? TabletStyle.TAG_RED_TEXT : TabletStyle.SCREEN) + 4f;
            }
            if (ForecastView.HasFaction(r)) tx += Tag(tags, tx, 0f, r.Faction, ForecastView.FactionColor(r.FactionId), TagLight) + 4f;
            float tagsW = tx > 0f ? tx - 4f : 0f;

            string nameText = string.IsNullOrEmpty(r.BaseName) ? (r.Name ?? "") : r.BaseName;
            var name = Text(root, "Name", x, y, 10f, TagH, 18f, TabletStyle.INK, TextAlignmentOptions.MidlineLeft, text: nameText);
            float line1 = RowW - PadX - rightW - 8f - x;
            float maxName = Mathf.Max(100f, line1 - (tagsW > 0f ? tagsW + 8f : 0f));
            float nameW = TabletUI.MeasureWidth(name, nameText);
            if (nameW > maxName)
            {
                nameW = maxName;
                name.overflowMode = TextOverflowModes.Ellipsis;
                TabletUI.AutoSize(name, 14f);
            }
            name.rectTransform.sizeDelta = new Vector2(nameW + 2f, TagH);
            tags.anchoredPosition = new Vector2(x + nameW + 8f, -y);
            tags.sizeDelta = new Vector2(Mathf.Max(10f, tagsW), TagH);
            y += TagH + 5f;

            // 第二行：来做什么 + 想要
            float ix = x;
            if (!string.IsNullOrEmpty(r.Intent)) ix += Tag(root, ix, y, r.Intent, TabletStyle.TAG_INTEREST_BG, TabletStyle.TAG_INTEREST_TEXT) + 8f;
            string wants = string.IsNullOrEmpty(r.Wants) ? "—" : r.Wants;
            float ww = RowW - PadX - ix;
            var wt = Text(root, "Wants", ix, y + 1f, ww, 22f, 16f, TabletStyle.INK, TextAlignmentOptions.TopLeft, wrap: true, text: wants);
            float pref = Mathf.Ceil(wt.GetPreferredValues(wants, ww, 0f).y);
            float wh = Mathf.Clamp(pref, 21f, 64f);
            if (pref > wh + 1f) wt.overflowMode = TextOverflowModes.Ellipsis;
            wt.rectTransform.sizeDelta = new Vector2(ww, wh);
            y += Mathf.Max(TagH, wh + 1f) + 3f;

            // 第三行：预算 · 来源
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(r.Budget) && r.Budget != "—") parts.Add("预算 " + r.Budget + Credit());
            if (showSource && !string.IsNullOrEmpty(r.Source) && r.Source != "—") parts.Add("来源 " + r.Source);
            if (parts.Count > 0)
            {
                var d = Text(root, "Details", x, y, RowW - 2f * PadX, 20f, 15f, TabletStyle.INK_HOVER, TextAlignmentOptions.MidlineLeft,
                    text: string.Join("　·　", parts));
                TabletUI.AutoSize(d, 12f);
                y += 20f;
            }
            y += 6f;
            TabletUI.NewFlat(root, "Separator", 0f, y, RowW, 2f, TabletStyle.ROW_SEPARATOR);
            y += 2f;
            SetRowHeight(root, y);
            Tooltip(root.gameObject, ForecastView.Tooltip(r, showSource));
        }

        private void MinorRow(ForecastView.SourceGroup g)
        {
            var root = TabletUI.RowRoot(frame.content, "Minor", 30f);
            var t = Text(root, "Text", PadX, 3f, RowW - 2f * PadX, 22f, 15f, TabletStyle.INK_HOVER, TextAlignmentOptions.MidlineLeft,
                text: ForecastView.MinorLine(g) + "：" + ForecastView.MinorNames(g, 8));
            t.overflowMode = TextOverflowModes.Ellipsis;
            TabletUI.NewFlat(root, "Separator", 0f, 28f, RowW, 2f, TabletStyle.ROW_SEPARATOR);
            Tooltip(root.gameObject, ForecastView.MinorLine(g) + "\n" + ForecastView.MinorNames(g, 40).Replace("、", "\n"));
        }

        private void Banner(string text)
        {
            float w = RowW - 2f * PadX;
            var root = TabletUI.RowRoot(frame.content, "Warning", 40f);
            var plate = TabletUI.NewFlat(root, "Plate", 0f, 4f, RowW, 32f, TabletStyle.RED);
            var t = Text(root, "Text", PadX, 10f, w, 22f, 16f, TagLight, TextAlignmentOptions.TopLeft, wrap: true, text: text);
            t.fontStyle = FontStyles.Bold;
            float h = Mathf.Max(20f, Mathf.Ceil(t.GetPreferredValues(text, w, 0f).y));
            t.rectTransform.sizeDelta = new Vector2(w, h);
            plate.rectTransform.sizeDelta = new Vector2(RowW, h + 12f);
            SetRowHeight(root, 4f + h + 12f + 8f);
        }

        private void NoteRow(string text) => WrappedRow(text, 15f, TabletStyle.INK_HOVER, 4f);

        private void WrappedRow(string text, float size, Color color, float padY)
        {
            float w = RowW - 2f * PadX;
            var root = TabletUI.RowRoot(frame.content, "Text", 30f);
            var t = Text(root, "Text", PadX, padY, w, 22f, size, color, TextAlignmentOptions.TopLeft, wrap: true, text: text);
            float h = Mathf.Max(size + 4f, Mathf.Ceil(t.GetPreferredValues(text, w, 0f).y));
            t.rectTransform.sizeDelta = new Vector2(w, h);
            SetRowHeight(root, padY * 2f + h);
        }

        private void CenterText(string text)
        {
            var root = TabletUI.RowRoot(frame.content, "Empty", 48f);
            Text(root, "Text", PadX, 0f, RowW - 2f * PadX, 46f, 18f, TabletStyle.INK_HOVER, TextAlignmentOptions.Center, text: text);
        }

        private static void SetRowHeight(RectTransform root, float h)
        {
            var le = root.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredHeight = h;
                le.minHeight = h;
            }
            root.sizeDelta = new Vector2(RowW, h);
        }

        // ------------------------------------------------------------------ 页脚与按钮

        private void Footer(bool busy, ForecastResult r, ForecastResult.Mode kind)
        {
            const float w = TabletStyle.FOOTER_ROW_W; // 506
            string stats = busy ? "" : (r.Kind == ForecastResult.Mode.Message ? "" : r.SubHeader ?? "");
            string note;
            if (kind == ForecastResult.Mode.Forecast && !busy) note = ForecastView.Footnote(owner.HotkeyLabel);
            else note = "按 " + owner.HotkeyLabel + " 打开 / 关闭本窗口；标题栏和边框可以拖动。";
            float y = 4f;
            if (stats.Length > 0)
            {
                var s = Text(frame.footer, "Stats", TabletStyle.FOOTER_PAD_X, y, w, 22f, 16f, TabletStyle.INK, TextAlignmentOptions.MidlineLeft, text: stats);
                TabletUI.AutoSize(s, 12f);
                y += 24f;
            }
            var n = Text(frame.footer, "Note", TabletStyle.FOOTER_PAD_X, y, w, 40f, 15f, TabletStyle.INK_HOVER, TextAlignmentOptions.TopLeft, wrap: true, text: note);
            n.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void Buttons(bool busy, ForecastResult.Mode kind)
        {
            // 两个按钮的位置与谈判平板一致（BUTTON_LEFT_X / BUTTON_RIGHT_X 减去 SCREEN_X）
            const float leftX = TabletStyle.BUTTON_LEFT_X - TabletStyle.SCREEN_X;
            const float rightX = TabletStyle.BUTTON_RIGHT_X - TabletStyle.SCREEN_X;
            string label = kind == ForecastResult.Mode.ActualQueue ? "立即刷新" : "重新预测";
            var rerun = TabletUI.BigButton(frame, frame.buttonHost, leftX, 0f, label, () => owner.RequestRun(true));
            var close = TabletUI.BigButton(frame, frame.buttonHost, rightX, 0f, "关闭", () => owner.Hide());
            ApplyFont(rerun.label);
            ApplyFont(close.label);
            rerun.SetInteractable(!busy);
        }

        // ------------------------------------------------------------------ 小工具

        /// <summary>TabletUI.NewText + 字体处理（中文语言下保持游戏按语言切换的字体；其他语言强制中文字体）。</summary>
        private TextMeshProUGUI Text(Transform parent, string name, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align, bool wrap = false, string text = null)
        {
            var t = TabletUI.NewText(frame, parent, name, x, y, w, h, FontLoader.FontRole.Noto_Sans, size, color, align, wrap);
            ApplyFont(t);
            if (text != null) t.text = text;
            return t;
        }

        private void ApplyFont(TMP_Text t)
        {
            if (t == null || forcedFont == null) return;
            var loc = t.GetComponent<TMPLocalizer>();
            if (loc != null) loc.enabled = false; // 否则换语言时会被换回不含中文的字体
            t.font = forcedFont;
        }

        /// <summary>与 TabletUI.Tag 相同的圆角标签，文字走 Text()（保证中文字形）。返回宽度。</summary>
        private float Tag(Transform parent, float x, float y, string text, Color background, Color textColor)
        {
            var rt = TabletUI.NewRect(parent, "Tag", x, y, 10f, TagH);
            TabletUI.NewStretched(rt, "Plate", frame.tagRounded, background); // 没有圆角贴图时就是纯色块
            var t = Text(rt, "Text", TabletStyle.TAG_PAD_X, 0f, 10f, TagH, TabletStyle.TAG_TEXT_SIZE, textColor, TextAlignmentOptions.MidlineLeft, text: text);
            float tw = TabletUI.MeasureWidth(t, text);
            t.rectTransform.sizeDelta = new Vector2(tw + 2f, TagH);
            float w = TabletStyle.TAG_PAD_X + tw + TabletStyle.TAG_PAD_X;
            rt.sizeDelta = new Vector2(w, TagH);
            return w;
        }

        private void Tooltip(GameObject go, string text)
        {
            if (go == null || string.IsNullOrEmpty(text)) return;
            if (paperLayer)
            {
                TabletUI.AddTooltip(go, () => text); // 游戏的 TooltipManager，与谈判平板相同
                return;
            }
            // overlay 层：游戏的提示框会被盖住，改用 CustomUIManager 的提示框（同在 overlay 画布上）
            var g = go.GetComponent<Graphic>();
            if (g == null)
            {
                var img = go.AddComponent<Image>();
                img.color = Color.clear;
                g = img;
            }
            g.raycastTarget = true;
            var trig = go.GetComponent<CustomUITooltipTrigger>();
            if (trig == null) trig = go.AddComponent<CustomUITooltipTrigger>();
            trig.tooltipText = text;
        }

        private static string Credit()
        {
            try { return GeneralHelper.CreditIcon(); }
            catch { return ""; }
        }
    }
}
