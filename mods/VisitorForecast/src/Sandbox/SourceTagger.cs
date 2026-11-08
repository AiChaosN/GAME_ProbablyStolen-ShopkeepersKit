using System.Collections.Generic;
using System.Reflection;

namespace VisitorForecast.Sandbox
{
    /// <summary>
    /// 给沙盒里生成的每位顾客打上“来源”标签：对 StoreClientManager 的各个 Handle* 步骤打前缀/后缀，
    /// 步骤结束时把队列里还没有标签的顾客记为该步骤。内层步骤先结束，所以嵌套时内层标签优先。
    /// </summary>
    internal static class SourceTagger
    {
        // 方法名 -> 中文来源
        internal static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "HandleShowCaseSecurity", "展示柜违禁检查" },
            { "HandleImportantScriptedClient", "剧情" },
            { "HandleWildBailout", "王尔德救助" },
            { "HandleCartelClient", "卡特尔剧情" },
            { "HandleEleanorEarlyClient", "剧情" },
            { "HandleShowcaseClient", "展示柜买家" },
            { "HandleChemicalFeedbackClient", "化学品反馈" },
            { "HandleSkipClient", "开局剧情" },
            { "HandleAlwayClient", "每周日程" },
            { "HandleBlackmailClient", "开局特性" },
            { "HandleScalperClient", "开局特性" },
            { "HandleCaseEnjoyerClient", "收藏家" },
            { "HandleClientFromEvent", "市场事件" },
            { "HandleClientFromIntro", "教程剧情" },
            { "HandleInspectionClient", "治安检查" },
            { "HandleSupplierClient", "每周日程" },
            { "HandleContentUnlockClient", "每周日程" },
            { "HandleCharacterLine", "剧情" },
            { "HandleExpeditionClient", "远征雇员" },
            { "HandleInformationClient", "情报贩子" },
            { "HandleInventorClient", "发明家" },
            { "HandleJacksonStorage", "发明家" },
            { "HandleMinorClient", "新面孔" },
            { "HandleAugClient", "改造人" },
            { "HandleWantedClient", "通缉犯" },
            { "HandleNormalClient", "招牌与证书" },
            { "HandlePerkClient", "声望特权" },
            { "HandleMarketClient", "市场短缺过剩" },
            { "HandleServiceClient", "王尔德服务" },
            { "HandleSawyerCrewClient", "剧情" },
            { "HandleEleanorUncleClient", "剧情" },
            { "CheckRentDay", "房租日" },
            { "HandleJohnSmithVandalVisit", "通缉犯" },
            { "QueueVisit", "剧情" },                       // LouBombHelper.QueueVisit
            { "HandleFutureClientQueue", "预约回访" },       // PlayerStore.HandleFutureClientQueue
            { "FireOnGenerateCustomerVeryEarly", "其他 mod" },
            { "FireOnGenerateCustomerEarly", "其他 mod" },
            { "FireOnGenerateCustomerNormal", "其他 mod" },
            { "FireOnGenerateCustomerLate", "其他 mod" },
            { "FireOnGenerateCustomerVeryLate", "其他 mod" },
        };

        internal const string RandomLabel = "随机顾客";
        internal const string ExistingLabel = "已在队列";
        internal const string UnknownLabel = "其他";

        private static readonly Dictionary<object, string> sources = new Dictionary<object, string>(RefComparer.Instance);
        private static readonly HashSet<object> insertedFirst = new HashSet<object>(RefComparer.Instance);
        private static readonly Stack<string> labelStack = new Stack<string>();
        private static List<StoreClient> stack;

        internal static void BeginRun(List<StoreClient> clientStack)
        {
            sources.Clear();
            insertedFirst.Clear();
            labelStack.Clear();
            stack = clientStack;
            if (stack == null) return;
            foreach (var c in stack)
                if (c != null) sources[c] = ExistingLabel;
        }

        internal static void EndRun()
        {
            sources.Clear();
            insertedFirst.Clear();
            labelStack.Clear();
            stack = null;
        }

        internal static string SourceOf(StoreClient c) =>
            c != null && sources.TryGetValue(c, out var s) ? s : UnknownLabel;

        internal static bool WasInsertedFirst(StoreClient c) => c != null && insertedFirst.Contains(c);

        internal static void Push(MethodBase m)
        {
            string label = m != null && Labels.TryGetValue(m.Name, out var l) ? l : UnknownLabel;
            labelStack.Push(label);
        }

        internal static void Pop(MethodBase m)
        {
            string label = labelStack.Count > 0 ? labelStack.Pop() : UnknownLabel;
            TagUntagged(label);
        }

        private static void TagUntagged(string label)
        {
            if (stack == null) return;
            foreach (var c in stack)
                if (c != null && !sources.ContainsKey(c)) sources[c] = label;
        }

        // ---------------------------------------------------------------- Harmony 补丁方法

        public static void StepPrefix(MethodBase __originalMethod)
        {
            if (!SandboxGuard.Active) return;
            Push(__originalMethod);
        }

        public static void StepPostfix(MethodBase __originalMethod)
        {
            if (!SandboxGuard.Active) return;
            Pop(__originalMethod);
        }

        /// <summary>StoreClientManager.PickClient 的返回值是随机客流抽出来的人。</summary>
        public static void PickClientPostfix(StoreClient __result)
        {
            if (!SandboxGuard.Active || __result == null) return;
            if (!sources.ContainsKey(__result)) sources[__result] = RandomLabel;
        }

        /// <summary>AddNextClient 把人插到队首。</summary>
        public static void AddNextClientPrefix(StoreClient storeClient)
        {
            if (!SandboxGuard.Active || storeClient == null) return;
            insertedFirst.Add(storeClient);
        }
    }
}
