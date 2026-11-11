using System;

namespace DropSwap
{
    /// <summary>
    /// 界面语言跟随游戏：用游戏自己判断语言的同一个函数 LocHelper.GetCurrentLocaleCode()
    /// （中文时为 "zh" 或 "zh-Hans"）。中文显示中文，其他所有语言显示英文。
    /// 每次取文字时都重新判断，所以游戏里中途切换语言后，之后的提示、面板会跟着变。
    /// 只写进日志的文字（排查问题用）保持中文。
    /// </summary>
    internal static class Lang
    {
        internal static bool Zh
        {
            get
            {
                try
                {
                    var code = LocHelper.GetCurrentLocaleCode() ?? "";
                    return code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }
        }

        internal static string T(string zh, string en) => Zh ? zh : en;
    }
}
