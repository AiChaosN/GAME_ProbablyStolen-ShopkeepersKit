using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CounterOverflow
{
    /// <summary>
    /// 窗口“看得见的框”的位置。PixelWindow.rectTransform 是窗口的根节点，尺寸不等于看得见的框
    /// （真正的框是私有的 outsideBorder 节点，大小 = widthPixels × heightPixels），直接用根节点会让挂在旁边的按钮离得很远。
    /// 字段查找以字面量 typeof(PixelWindow) 为接收者（游戏扫描器据此不要求额外权限）；找不到时按大小在子节点里找。
    /// </summary>
    internal static class WindowRect
    {
        private static FieldInfo borderField;
        private static bool fieldTried;

        internal static RectTransform Visible(PixelWindow w)
        {
            if (w == null) return null;
            var root = w.rectTransform;
            try
            {
                if (!fieldTried)
                {
                    fieldTried = true;
                    borderField = AccessTools.Field(typeof(PixelWindow), "outsideBorder");
                }
                if (borderField != null && borderField.GetValue(w) is TreeNodeRender border && border.rectTransform != null)
                    return border.rectTransform;
            }
            catch { }

            // 退路：子节点里大小等于窗口像素尺寸的那个（外框或背景，两者范围相同）
            try
            {
                if (root == null) return null;
                int width = w.widthPixels, height = w.heightPixels;
                for (int i = 0; i < root.childCount; i++)
                {
                    if (!(root.GetChild(i) is RectTransform rt) || !rt.gameObject.activeInHierarchy) continue;
                    var s = rt.sizeDelta;
                    if (Mathf.Abs(s.x - width) <= 1f && Mathf.Abs(s.y - height) <= 2f) return rt;
                }
            }
            catch { }
            return root;
        }
    }
}
