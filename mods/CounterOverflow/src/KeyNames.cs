using System;
using UnityEngine;

namespace CounterOverflow
{
    /// <summary>
    /// 按键名：既认 Unity KeyCode 名称（F9、Slash、Comma…），也认直接输入的单个字符（/ , . ; ' [ ] - = ` \ 字母 数字），
    /// 这样在设置里填“/”也能用。显示时把符号键写回符号。
    /// </summary>
    internal static class KeyNames
    {
        internal static bool TryParse(string text, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (text.Length == 1)
            {
                char c = text[0];
                switch (c)
                {
                    case '/': key = KeyCode.Slash; break;
                    case '\\': key = KeyCode.Backslash; break;
                    case ',': key = KeyCode.Comma; break;
                    case '.': key = KeyCode.Period; break;
                    case ';': key = KeyCode.Semicolon; break;
                    case '\'': key = KeyCode.Quote; break;
                    case '[': key = KeyCode.LeftBracket; break;
                    case ']': key = KeyCode.RightBracket; break;
                    case '-': key = KeyCode.Minus; break;
                    case '=': key = KeyCode.Equals; break;
                    case '`': key = KeyCode.BackQuote; break;
                    default:
                        if (c >= '0' && c <= '9') key = KeyCode.Alpha0 + (c - '0');
                        else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) key = KeyCode.A + (char.ToUpperInvariant(c) - 'A');
                        break;
                }
                if (key != KeyCode.None) return true;
            }
            return Enum.TryParse(text, true, out key) && key != KeyCode.None;
        }

        internal static KeyCode Parse(string text, KeyCode fallback, Action<string> warn = null)
        {
            if (TryParse(text, out var k)) return k;
            warn?.Invoke("无法识别的按键 '" + text + "'，改用 " + Label(fallback));
            return fallback;
        }

        internal static string Label(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.BackQuote: return "`";
                default: return k.ToString();
            }
        }
    }
}
