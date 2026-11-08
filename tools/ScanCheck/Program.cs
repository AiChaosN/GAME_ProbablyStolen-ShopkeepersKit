using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
var folder = args[0];
var managed = args[1];
// 游戏里 ModLoader 启动时会把 Managed 目录的程序集名填进 gameAssemblyNames；脚手架里手动填上
var names = new HashSet<string>(System.IO.Directory.GetFiles(managed, "*.dll").Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);
names.Add("mscorlib"); names.Add("netstandard");
typeof(ModLoader).GetField("gameAssemblyNames", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, names);
var dlls = System.IO.Directory.GetFiles(folder, "*.dll");
var r = ModScanner.Scan(dlls, dlls.Select(Path.GetFileNameWithoutExtension), folder, true);
Console.WriteLine("Error: " + (r.Error ?? "(none)") + " | Refused: " + r.Refused + " | MethodsScanned: " + r.MethodsScanned);
Console.WriteLine("Capabilities: [" + string.Join(", ", r.Capabilities) + "]");
Console.WriteLine("HardFlags: " + r.HardFlags.Count);
foreach (var f in r.HardFlags) Console.WriteLine("  H " + f);
Console.WriteLine("Findings: " + r.Findings.Count);
foreach (var f in r.Findings) Console.WriteLine("  F " + f);
