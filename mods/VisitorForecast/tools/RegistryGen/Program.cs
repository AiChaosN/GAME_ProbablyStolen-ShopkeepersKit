// 构建期生成器：读取游戏的 Assembly-CSharp.dll，为每个能用 typeof 写出来的游戏类型生成一行
//   R(typeof(X), typeof(X).GetFields(I), typeof(X).GetFields(S), root);
// 让 mod 里所有字段查询的接收者都是字面量 typeof(游戏类型)（游戏的 ModScanner 因此不要求 reflection-external 权限）；
// 同时为游戏字段里出现的 HashSet<T>/Queue<T>/Stack<T>/LinkedList<T>/SortedSet<T> 生成强类型的还原器。
//
// 用法：dotnet run --project tools/RegistryGen -- <Managed 目录> <输出 .cs>
// 这个工具本身在开发机上运行（.NET 8），可以随意使用反射；它不进入 mod 的 DLL。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

internal static class Program
{
    // 游戏 ModScanner.GuardTypes：对这些类型（及其嵌套类型）做 typeof / GetFields 会被判为硬性违规（loader-target）。
    // 这里只是保底名单；Main 里会再并上从游戏 Assembly-CSharp 读到的当前名单
    // （v0.46D 的某次更新加了 ModSteamAuth、PaperBookRegistry，旧的手抄名单漏了它们，导致 1.3.0 被拒绝加载）。
    private static readonly HashSet<string> GuardTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "ModLoader", "ModTrust", "ModTrustRecord", "ModFingerprint", "ModTrustList", "ModScanner", "ModCapability",
        "ModSecurityDebug", "ModSteamRules", "ModSteamGuard", "ModSteamAuth", "ModWorkshop", "SteamManager",
        "PaperBookRegistry",
    };

    // 不作为静态根（与 1.2 及以前的 GameTypes.excludedRootNames 相同）：模组加载器/信任/Steam/配置/日志缓冲
    private static readonly HashSet<string> NotRoots = new HashSet<string>(StringComparer.Ordinal)
    {
        "ModLoader", "ModTrust", "ModTrustRecord", "ModFingerprint", "ModTrustList", "ModScanner",
        "ModCapability", "ModSecurityDebug", "ModSteamRules", "ModSteamGuard", "ModWorkshop", "SteamManager",
        "ModHook", "ModConfig", "ModConfigWindow", "ModUIManager", "ModWarningUI", "LoadedMod", "ModHelper",
        "LogWriter",
    };

    private static readonly Dictionary<string, string> CollOps = new Dictionary<string, string>
    {
        { "System.Collections.Generic.HashSet`1", "SetOps" },
        { "System.Collections.Generic.SortedSet`1", "SortedSetOps" },
        { "System.Collections.Generic.Queue`1", "QueueOps" },
        { "System.Collections.Generic.Stack`1", "StackOps" },
        { "System.Collections.Generic.LinkedList`1", "LinkedListOps" },
    };

    private const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const int ChunkSize = 40;

    private static Assembly game;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: RegistryGen <Managed dir> <output .cs>");
            return 2;
        }
        string managed = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            var n = new AssemblyName(e.Name).Name;
            if (n == "mscorlib" || n == "netstandard" || n.StartsWith("System", StringComparison.Ordinal)) return null;
            var p = Path.Combine(managed, n + ".dll");
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        if (!MergeGameGuardTypes())
        {
            Console.Error.WriteLine("RegistryGen: 读不到游戏的 ModScanner.GuardTypes，无法确认受保护类型名单，中止（避免生成会被拒绝加载的 mod）");
            return 3;
        }
        Type[] all;
        try { all = game.GetTypes(); }
        catch (ReflectionTypeLoadException e) { all = e.Types.Where(t => t != null).ToArray(); }

        var types = new List<Type>();
        foreach (var t in all.OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            if (!Eligible(t)) continue;
            types.Add(t);
        }

        // 字段/基类里出现的、所有类型实参都可见的封闭泛型游戏类型（如 Directory<GameItem>）
        var closed = new SortedDictionary<string, Type>(StringComparer.Ordinal);
        var colls = new SortedDictionary<string, (string ops, Type elem)>(StringComparer.Ordinal);
        foreach (var t in types)
        {
            foreach (var f in Fields(t, I | S)) Walk(f.FieldType, closed, colls);
            if (t.BaseType != null) Walk(t.BaseType, closed, colls);
        }
        // 封闭泛型游戏类型自己的字段里也可能出现集合
        foreach (var t in closed.Values.ToList())
            foreach (var f in Fields(t, I | S)) Walk(f.FieldType, closed, colls);

        var regLines = new List<string>();
        int roots = 0;
        foreach (var t in types.Concat(closed.Values))
        {
            string cs = CsName(t);
            if (cs == null) continue;
            bool root = !t.IsGenericType && !NotRoots.Contains(Outermost(t).Name) &&
                        !Outermost(t).Name.StartsWith("ES3", StringComparison.Ordinal) &&
                        !Outermost(t).Name.StartsWith("Steam", StringComparison.Ordinal);
            if (root && Fields(t, S).Any(f => !f.IsLiteral)) roots++;
            regLines.Add($"            R(typeof({cs}), typeof({cs}).GetFields(I), typeof({cs}).GetFields(S), {(root ? "true" : "false")});");
        }
        var collLines = colls.Values.Select(c => $"            Coll(new {c.ops}<{CsName(c.elem)}>());").ToList();

        var mvid = game.ManifestModule.ModuleVersionId;
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// 由 tools/RegistryGen 根据游戏的 Assembly-CSharp.dll 生成，不要手改；游戏更新后重新运行 build.sh。");
        sb.AppendLine($"// 游戏类型 {regLines.Count} 个（其中有静态字段的静态根 {roots} 个），集合还原器 {collLines.Count} 个。");
        sb.AppendLine("// 每一行的字段查询都以字面量 typeof(游戏类型) 为接收者。");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#pragma warning disable 0612, 0618");
        sb.AppendLine("namespace VisitorForecast.Sandbox");
        sb.AppendLine("{");
        sb.AppendLine("    internal static partial class GameTypeRegistry");
        sb.AppendLine("    {");
        sb.AppendLine($"        internal const string GeneratedMvid = \"{mvid}\";");
        int chunks = (regLines.Count + ChunkSize - 1) / ChunkSize;
        sb.Append("        private static readonly System.Action[] Chunks = { ");
        sb.Append(string.Join(", ", Enumerable.Range(0, chunks).Select(i => "C" + i)));
        sb.AppendLine(" };");
        for (int c = 0; c < chunks; c++)
        {
            sb.AppendLine();
            sb.AppendLine($"        private static void C{c}()");
            sb.AppendLine("        {");
            foreach (var l in regLines.Skip(c * ChunkSize).Take(ChunkSize)) sb.AppendLine(l);
            sb.AppendLine("        }");
        }
        sb.AppendLine();
        sb.AppendLine("        private static void Colls()");
        sb.AppendLine("        {");
        foreach (var l in collLines) sb.AppendLine(l);
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        File.WriteAllText(args[1], sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"RegistryGen: {regLines.Count} types ({closed.Count} closed generic), {roots} static roots, {collLines.Count} collection ops -> {args[1]}");
        return 0;
    }

    /// <summary>把游戏当前的 ModScanner.GuardTypes 并进 GuardTypes / NotRoots；读不到时返回 false。</summary>
    private static bool MergeGameGuardTypes()
    {
        try
        {
            var field = game.GetType("ModScanner")?.GetField("GuardTypes", BindingFlags.Public | BindingFlags.Static);
            if (!(field?.GetValue(null) is string[] names) || names.Length == 0) return false;
            var added = names.Where(n => !string.IsNullOrEmpty(n) && GuardTypes.Add(n)).ToList();
            foreach (var n in names) if (!string.IsNullOrEmpty(n)) NotRoots.Add(n);
            Console.WriteLine("RegistryGen: 游戏受保护类型 " + names.Length + " 个" +
                              (added.Count > 0 ? "，保底名单之外新增: " + string.Join(", ", added) : ""));
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("RegistryGen: 读取 ModScanner.GuardTypes 失败: " + e.GetType().Name + ": " + e.Message);
            return false;
        }
    }

    private static FieldInfo[] Fields(Type t, BindingFlags flags)
    {
        try { return t.GetFields(flags); }
        catch { return new FieldInfo[0]; }
    }

    private static Type Outermost(Type t)
    {
        while (t.DeclaringType != null) t = t.DeclaringType;
        return t;
    }

    private static bool IsCompilerGenerated(Type t)
    {
        for (var c = t; c != null; c = c.DeclaringType)
            if (c.Name.IndexOf('<') >= 0 || c.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true;
        return false;
    }

    private static bool ObsoleteError(Type t)
    {
        for (var c = t; c != null; c = c.DeclaringType)
        {
            var o = c.GetCustomAttribute<ObsoleteAttribute>(false);
            if (o != null && o.IsError) return true;
        }
        return false;
    }

    /// <summary>能在 mod 里写成 typeof(...) 的非泛型游戏 class/struct（不含加载器保护类型）。</summary>
    private static bool Eligible(Type t)
    {
        try
        {
            if (!t.IsVisible || t.IsInterface || t.IsEnum || t.IsGenericTypeDefinition || t.ContainsGenericParameters) return false;
            if (typeof(Delegate).IsAssignableFrom(t)) return false;
            if (IsCompilerGenerated(t) || ObsoleteError(t)) return false;
            if (GuardTypes.Contains(Outermost(t).Name)) return false;
            return CsName(t) != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool Expressible(Type t)
    {
        if (t.IsArray) return Expressible(t.GetElementType());
        if (t.IsPointer || t.IsByRef || t.IsGenericParameter) return false;
        if (!t.IsVisible) return false;
        if (t.IsGenericType && t.GetGenericArguments().Any(a => !Expressible(a))) return false;
        var asm = t.Assembly == game ? "Assembly-CSharp" : t.Assembly.GetName().Name;
        if (asm == "Assembly-CSharp") return !GuardTypes.Contains(Outermost(t).Name) && !IsCompilerGenerated(t);
        // 其他程序集只允许 BCL 与 UnityEngine.CoreModule（mod 编译时都引用了）
        return asm == "mscorlib" || asm == "netstandard" || asm.StartsWith("System", StringComparison.Ordinal) || asm == "UnityEngine.CoreModule";
    }

    private static void Walk(Type ft, SortedDictionary<string, Type> closed, SortedDictionary<string, (string, Type)> colls)
    {
        if (ft == null) return;
        if (ft.IsArray) { Walk(ft.GetElementType(), closed, colls); return; }
        if (!ft.IsGenericType || ft.ContainsGenericParameters) return;
        foreach (var a in ft.GetGenericArguments()) Walk(a, closed, colls);
        if (!Expressible(ft)) return;
        if (ft.Assembly == game)
        {
            var cs = CsName(ft);
            if (cs != null && !closed.ContainsKey(cs)) closed[cs] = ft;
            return;
        }
        var def = ft.GetGenericTypeDefinition().FullName;
        if (def != null && CollOps.TryGetValue(def, out var ops))
        {
            var elem = ft.GetGenericArguments()[0];
            var key = ops + "<" + CsName(elem) + ">";
            if (!colls.ContainsKey(key)) colls[key] = (ops, elem);
        }
    }

    /// <summary>C# 源码里的类型名（global:: 全限定）；写不出来时返回 null。</summary>
    private static string CsName(Type t)
    {
        if (t.IsArray)
        {
            var e = CsName(t.GetElementType());
            return e == null ? null : e + "[" + new string(',', t.GetArrayRank() - 1) + "]";
        }
        if (t.IsPointer || t.IsByRef || t.IsGenericParameter) return null;
        if (t.IsNested)
        {
            if (t.DeclaringType.IsGenericType) return null; // 泛型类型里的嵌套类型：不处理
            var outer = CsName(t.DeclaringType);
            var inner = Plain(t);
            if (outer == null || inner == null) return null;
            return outer + "." + inner;
        }
        string ns = string.IsNullOrEmpty(t.Namespace) ? "" : t.Namespace + ".";
        var plain = Plain(t);
        return plain == null ? null : "global::" + ns + plain;
    }

    private static string Plain(Type t)
    {
        string name = t.Name;
        int tick = name.IndexOf('`');
        if (tick >= 0) name = name.Substring(0, tick);
        if (!t.IsGenericType) return name;
        var args = t.GetGenericArguments().Select(CsName).ToList();
        if (args.Any(a => a == null)) return null;
        return name + "<" + string.Join(", ", args) + ">";
    }
}
