using System.Reflection;
using HarmonyLib;

namespace SortToBoxes
{
    /// <summary>
    /// 安全地拿游戏的 PlayerStore。PlayerStore.Instance 的 getter 在为空时会 new 一个新的 PlayerStore：
    /// 在主菜单（还没进存档）里调用，要么在构造函数里崩掉（日志里的空引用就是这么来的），要么凭空造出一个存档对象。
    /// 所以这里直接读它的私有静态字段 instance（字面量 typeof 查找，扫描器不要求权限），没有就返回 null。
    /// 本 mod 里不要直接写 PlayerStore.Instance。
    /// </summary>
    internal static class Game
    {
        private static readonly FieldInfo storeField = AccessTools.Field(typeof(PlayerStore), "instance");

        internal static PlayerStore Store
        {
            get
            {
                try { return storeField?.GetValue(null) as PlayerStore; }
                catch { return null; }
            }
        }
    }
}
