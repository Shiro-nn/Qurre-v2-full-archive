using System.Reflection;

internal readonly struct AssemblyDep
{
    internal readonly Assembly Assembly { get; }
    internal readonly string Path { get; }

    internal AssemblyDep(Assembly assembly, string path)
    {
        Assembly = assembly;
        Path = path;
    }
}