using System;
using System.Collections.Generic;
using UnityEngine;
using VisitorForecast.Forecast;

namespace VisitorForecast.UI
{
    /// <summary>
    /// 备用的 IMGUI 窗口（1.1 版的界面）：原生窗口不可用（主菜单等）、出错或在设置里关闭时使用。
    /// 系统中文字体；鼠标点击只对游戏里检查 hoveringStoreUI 的操作有遮挡效果。
    /// </summary>
    internal sealed class ImguiForecastWindow : IDisposable
    {
        private Rect windowRect = new Rect(60, 60, 980, 620);
        private Vector2 scroll;
        private ForecastResult lastResult;
        private float scale = 1f;

        private Font font;
        private bool stylesReady;
        private GUIStyle windowStyle, headerStyle, subStyle, sectionStyle, groupStyle, cellStyle, cellBoldStyle, warnStyle, noteStyle, buttonStyle, rowAltStyle, rowStyle, headRowStyle, markStyle;
        private Texture2D bgTex, altTex, headTex;

        private const int WindowId = 0x56464331;
        private static readonly float[] CertainWidths = { 180, 72, 70, 290, 80, 108, 112 };
        private static readonly string[] CertainCols = { "名字", "阵营", "来做什么", "想要", "预算", "来源", "排队位置" };
        private static readonly float[] OtherWidths = { 180, 72, 70, 390, 80, 120 };
        private static readonly string[] OtherCols = { "名字", "阵营", "来做什么", "想要", "预算", "概率" };

        private ForecastBehaviour owner;

        internal void OnGUI(ForecastBehaviour behaviour)
        {
            owner = behaviour;
            var old = GUI.matrix;
            try
            {
                EnsureStyles();
                scale = Mathf.Clamp(Screen.height / 1080f, 0.8f, 2.5f);
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                float maxW = Screen.width / scale, maxH = Screen.height / scale;
                windowRect.width = Mathf.Min(windowRect.width, maxW - 10);
                windowRect.height = Mathf.Min(windowRect.height, maxH - 10);
                windowRect.x = Mathf.Clamp(windowRect.x, 0, Mathf.Max(0, maxW - 80));
                windowRect.y = Mathf.Clamp(windowRect.y, 0, Mathf.Max(0, maxH - 40));
                windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "访客预测 · Visitor Forecast", windowStyle);
            }
            catch (ExitGUIException)
            {
                throw; // IMGUI 的正常控制流
            }
            catch (Exception e)
            {
                behaviour.OnImguiError(e);
            }
            finally
            {
                GUI.matrix = old;
            }
        }

        internal bool ContainsScreenPoint(Vector2 screenPos)
        {
            // 屏幕坐标 y 向上，IMGUI 向下；窗口按 scale 缩放
            var p = new Vector2(screenPos.x / scale, (Screen.height - screenPos.y) / scale);
            return windowRect.Contains(p);
        }

        private void DrawWindow(int id)
        {
            var result = owner.Result;
            if (!ReferenceEquals(result, lastResult))
            {
                // 预测结果换了就回到顶部；实际队列每秒刷新时保持滚动位置
                if (lastResult == null || result == null || lastResult.Kind != ForecastResult.Mode.ActualQueue || result.Kind != ForecastResult.Mode.ActualQueue)
                    scroll = Vector2.zero;
                lastResult = result;
            }

            GUILayout.Space(4);
            if (owner.Busy || result == null)
            {
                GUILayout.Label(ForecastView.BusyText(owner.Runs), headerStyle);
            }
            else
            {
                GUILayout.Label(result.Header ?? "", headerStyle);
                if (!string.IsNullOrEmpty(result.SubHeader)) GUILayout.Label(result.SubHeader, subStyle);
                if (!string.IsNullOrEmpty(result.Warning)) GUILayout.Label(result.Warning, warnStyle);
                if (!string.IsNullOrEmpty(result.Note)) GUILayout.Label(result.Note, noteStyle);
                if (!string.IsNullOrEmpty(result.Message)) GUILayout.Label(result.Message, subStyle);

                scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
                if (result.Kind == ForecastResult.Mode.Forecast)
                {
                    GUILayout.Space(6);
                    GUILayout.Label("必来（" + result.CertainCount + " 人）", sectionStyle);
                    CertainTable(ForecastView.SortedCertain(result.Certain), ForecastView.EmptyCertain);
                    GUILayout.Space(8);
                    GUILayout.Label("其他客人：" + ForecastView.Range(result.OthersMin, result.OthersMax) + " 人" +
                        string.Format("（平均 {0:0.0} 人，按来源分组；随机顾客等每次读档都会重新随机）", result.OthersMean), sectionStyle);
                    var groups = ForecastView.GroupOthers(result.Possible);
                    if (groups.Count == 0) GUILayout.Label("（没有其他可能来的客人）", noteStyle);
                    foreach (var g in groups) OtherGroup(g);
                }
                else if (result.Kind == ForecastResult.Mode.ActualQueue)
                {
                    GUILayout.Space(6);
                    GUILayout.Label(ForecastView.ActualTitle, sectionStyle);
                    CertainTable(result.Actual, "（队列已空）");
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(4);
            GUILayout.Label(ForecastView.Footnote(owner.HotkeyLabel), noteStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重新预测", buttonStyle, GUILayout.Width(120))) owner.RequestRun(true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭", buttonStyle, GUILayout.Width(90))) owner.Hide();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, 10000, 28));
        }

        private void HeaderRow(string[] names, float[] widths)
        {
            GUILayout.BeginHorizontal(headRowStyle);
            for (int c = 0; c < names.Length; c++) GUILayout.Label(names[c], cellBoldStyle, GUILayout.Width(widths[c]));
            GUILayout.EndHorizontal();
        }

        private void FactionCell(Row r, float width)
        {
            var old = GUI.contentColor;
            if (ForecastView.HasFaction(r)) GUI.contentColor = Color.Lerp(ForecastView.FactionColor(r.FactionId), Color.white, 0.35f);
            GUILayout.Label(r.Faction, cellStyle, GUILayout.Width(width));
            GUI.contentColor = old;
        }

        private void CertainTable(List<Row> rows, string empty)
        {
            if (rows.Count == 0)
            {
                GUILayout.Label(empty, noteStyle);
                return;
            }
            HeaderRow(CertainCols, CertainWidths);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                GUILayout.BeginHorizontal(i % 2 == 0 ? rowAltStyle : rowStyle);
                string name = r.Name + (r.AtDoor ? " [门口等候]" : "");
                GUILayout.Label(name, r.Warn || r.AtDoor ? markStyle : cellStyle, GUILayout.Width(CertainWidths[0]));
                FactionCell(r, CertainWidths[1]);
                GUILayout.Label(r.Intent, cellStyle, GUILayout.Width(CertainWidths[2]));
                GUILayout.Label(r.Wants, cellStyle, GUILayout.Width(CertainWidths[3]));
                GUILayout.Label(r.Budget, cellStyle, GUILayout.Width(CertainWidths[4]));
                GUILayout.Label(r.Source, cellStyle, GUILayout.Width(CertainWidths[5]));
                GUILayout.Label(r.Chance, cellStyle, GUILayout.Width(CertainWidths[6]));
                GUILayout.EndHorizontal();
            }
        }

        private void OtherGroup(ForecastView.SourceGroup g)
        {
            GUILayout.Space(6);
            GUILayout.Label(g.Source + " · " + ForecastView.Expected(g.Expected), groupStyle);
            if (g.Rows.Count > 0)
            {
                HeaderRow(OtherCols, OtherWidths);
                for (int i = 0; i < g.Rows.Count; i++)
                {
                    var r = g.Rows[i];
                    GUILayout.BeginHorizontal(i % 2 == 0 ? rowAltStyle : rowStyle);
                    string name = r.Name + (r.AtDoor ? " [门口等候]" : "");
                    GUILayout.Label(name, r.Warn || r.AtDoor ? markStyle : cellStyle, GUILayout.Width(OtherWidths[0]));
                    FactionCell(r, OtherWidths[1]);
                    GUILayout.Label(r.Intent, cellStyle, GUILayout.Width(OtherWidths[2]));
                    GUILayout.Label(r.Wants, cellStyle, GUILayout.Width(OtherWidths[3]));
                    GUILayout.Label(r.Budget, cellStyle, GUILayout.Width(OtherWidths[4]));
                    GUILayout.Label(ForecastView.Percent(r.Probability), cellStyle, GUILayout.Width(OtherWidths[5]));
                    GUILayout.EndHorizontal();
                }
            }
            if (g.Minor.Count > 0)
                GUILayout.Label(ForecastView.MinorLine(g) + "：" + ForecastView.MinorNames(g, 12), noteStyle);
        }

        // ------------------------------------------------------------------ styles

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            try
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun", "Noto Sans CJK SC", "Noto Sans SC", "PingFang SC", "Arial Unicode MS" }, 16);
            }
            catch (Exception e)
            {
                font = null;
                Log.Warn("创建中文字体失败，使用默认字体: " + e.Message);
            }
            bgTex = Solid(new Color(0.09f, 0.10f, 0.12f, 0.97f));
            altTex = Solid(new Color(1f, 1f, 1f, 0.05f));
            headTex = Solid(new Color(1f, 1f, 1f, 0.10f));

            windowStyle = new GUIStyle(GUI.skin.window) { font = font, fontSize = 15, padding = new RectOffset(10, 10, 26, 8) };
            windowStyle.normal.background = bgTex;
            windowStyle.onNormal.background = bgTex;
            windowStyle.normal.textColor = new Color(0.95f, 0.85f, 0.55f);
            windowStyle.onNormal.textColor = windowStyle.normal.textColor;

            cellStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, wordWrap = true, richText = false, padding = new RectOffset(3, 3, 2, 2) };
            cellStyle.normal.textColor = new Color(0.90f, 0.90f, 0.90f);
            cellBoldStyle = new GUIStyle(cellStyle) { fontStyle = FontStyle.Bold };
            cellBoldStyle.normal.textColor = new Color(0.75f, 0.85f, 1f);
            markStyle = new GUIStyle(cellStyle);
            markStyle.normal.textColor = new Color(1f, 0.72f, 0.35f);
            headerStyle = new GUIStyle(cellStyle) { fontSize = 17, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(1f, 0.92f, 0.65f);
            subStyle = new GUIStyle(cellStyle) { fontSize = 13 };
            subStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            sectionStyle = new GUIStyle(cellStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
            sectionStyle.normal.textColor = new Color(0.55f, 0.90f, 0.60f);
            groupStyle = new GUIStyle(cellStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
            groupStyle.normal.textColor = new Color(0.95f, 0.85f, 0.55f);
            warnStyle = new GUIStyle(cellStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
            warnStyle.normal.textColor = new Color(1f, 0.30f, 0.30f);
            noteStyle = new GUIStyle(cellStyle) { fontSize = 12 };
            noteStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 14 };
            rowStyle = new GUIStyle { margin = new RectOffset(0, 0, 0, 0), padding = new RectOffset(2, 2, 1, 1) };
            rowAltStyle = new GUIStyle(rowStyle);
            rowAltStyle.normal.background = altTex;
            headRowStyle = new GUIStyle(rowStyle);
            headRowStyle.normal.background = headTex;
            stylesReady = true;
        }

        public void Dispose()
        {
            if (bgTex != null) UnityEngine.Object.Destroy(bgTex);
            if (altTex != null) UnityEngine.Object.Destroy(altTex);
            if (headTex != null) UnityEngine.Object.Destroy(headTex);
        }
    }
}
