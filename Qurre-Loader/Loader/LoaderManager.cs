using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class LoaderManager
{
    public static string AppDataDir { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string QurreDir { get; } = Path.Combine(AppDataDir, "Qurre");
    public static string PluginsDir { get; } = Path.Combine(QurreDir, "Plugins");
    public static string DependDir { get; } = Path.Combine(PluginsDir, "Depends");

    public static byte[] ReadFile(string path)
        => File.ReadAllBytes(path);
    internal static bool Loaded(string a)
        => LocalLoaded.Any(x => x.Path == a);

    internal static readonly List<AssemblyDep> LocalLoaded = new();
}