using System.Reflection;
using HarmonyLib;

namespace VisitorForecast
{
    /// <summary>
    /// 安全地拿游戏单例。PlayerStore.Instance / StoreStation.Instance 的 getter 在为空时会 new 一个新对象，
    /// 在主菜单里调用会凭空造出存档对象，所以这里直接读私有静态字段 instance。
    /// </summary>
    internal static class Game
    {
        private static readonly FieldInfo psField = AccessTools.Field(typeof(PlayerStore), "instance");
        private static readonly FieldInfo ssField = AccessTools.Field(typeof(StoreStation), "instance");

        internal static PlayerStore PlayerStoreOrNull
        {
            get
            {
                try { return psField?.GetValue(null) as PlayerStore; }
                catch { return null; }
            }
        }

        internal static StoreStation StoreStationOrNull
        {
            get
            {
                try { return ssField?.GetValue(null) as StoreStation; }
                catch { return null; }
            }
        }

        internal static bool InGame(out PlayerStore ps, out StoreStation ss)
        {
            ps = PlayerStoreOrNull;
            ss = StoreStationOrNull;
            if (ps == null || ss == null || ps.storeClientManager == null || ps.storeClientManager.clientStack == null) return false;
            return EmporiumEntry.Instance != null;
        }
    }
}
