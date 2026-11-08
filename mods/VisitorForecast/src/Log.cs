using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisitorForecast
{
    /// <summary>
    /// 统一的日志前缀。沙盒运行期间 Unity 日志是关着的，所以那段时间的消息先进缓冲区，
    /// 等日志重新打开后再 Flush。
    /// </summary>
    internal static class Log
    {
        private const string Prefix = "[VisitorForecast] ";
        private static readonly List<KeyValuePair<LogType, string>> buffer = new List<KeyValuePair<LogType, string>>();
        internal static bool Buffering;

        internal static void Info(string msg) => Write(LogType.Log, msg);
        internal static void Warn(string msg) => Write(LogType.Warning, msg);
        internal static void Error(string msg) => Write(LogType.Error, msg);

        private static void Write(LogType type, string msg)
        {
            if (Buffering)
            {
                if (buffer.Count < 200) buffer.Add(new KeyValuePair<LogType, string>(type, msg));
                return;
            }
            try
            {
                switch (type)
                {
                    case LogType.Error: Debug.LogError(Prefix + msg); break;
                    case LogType.Warning: Debug.LogWarning(Prefix + msg); break;
                    default: Debug.Log(Prefix + msg); break;
                }
            }
            catch
            {
                // 日志本身绝不能把游戏带崩
            }
        }

        internal static void Flush()
        {
            if (Buffering || buffer.Count == 0) return;
            var copy = buffer.ToArray();
            buffer.Clear();
            foreach (var kv in copy) Write(kv.Key, kv.Value);
        }

        internal static string Describe(Exception e)
        {
            if (e == null) return "(null)";
            var inner = e;
            while (inner.InnerException != null && (inner is System.Reflection.TargetInvocationException || inner is TypeInitializationException))
                inner = inner.InnerException;
            return inner.GetType().Name + ": " + inner.Message + "\n" + inner.StackTrace;
        }
    }
}
