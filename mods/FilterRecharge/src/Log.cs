using System;
using UnityEngine;

namespace FilterRecharge
{
    /// <summary>统一日志前缀，写进 Player.log；日志本身绝不能把游戏带崩。</summary>
    internal static class Log
    {
        private const string Prefix = "[FilterRecharge] ";

        internal static void Info(string msg) { try { Debug.Log(Prefix + msg); } catch { } }
        internal static void Warn(string msg) { try { Debug.LogWarning(Prefix + msg); } catch { } }
        internal static void Error(string msg) { try { Debug.LogError(Prefix + msg); } catch { } }

        internal static string Describe(Exception e)
        {
            if (e == null) return "(null)";
            return e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
        }
    }
}
