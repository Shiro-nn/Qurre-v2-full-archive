using System;
using System.IO;
using System.Net;
using System.Reflection;

static public class MainInitializator
{
    static bool _loaded;

    static internal void Init()
    {
        if (_loaded)
            return;

        ServerConsole.AddLog("[Loader] [Qurre] Initialization...", ConsoleColor.Yellow);

        if (!Directory.Exists(LoaderManager.QurreDir))
        {
            Directory.CreateDirectory(LoaderManager.QurreDir);
        }

        if (!File.Exists(Path.Combine(LoaderManager.QurreDir, "Qurre.dll")))
        {
            ServerConsole.AddLog("[Loader] [Error] Qurre.dll not found", ConsoleColor.Red);
            return;
        }

        LoadDependencies();
        InvokeAssembly(Assembly.Load(LoaderManager.ReadFile(Path.Combine(LoaderManager.QurreDir, "Qurre.dll"))), true);
        _loaded = true;
    }

    static void InvokeAssembly(Assembly assembly, bool log = false)
    {
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                try
                {
                    if (type.GetInterface("ICharacterLoader") == typeof(ICharacterLoader))
                    {
                        (Activator.CreateInstance(type) as ICharacterLoader).Init();
                    }
                }
                catch { }
            }
        }
        catch (Exception e)
        {
            if (log)
            {
                ServerConsole.AddLog($"{e}", ConsoleColor.Red);
            }
        }
    }

    static void LoadDependencies()
    {
        ServerConsole.AddLog("[Loader] [Qurre] Loading dependencies...", ConsoleColor.Magenta);

        if (!Directory.Exists(LoaderManager.PluginsDir))
        {
            ServerConsole.AddLog($"[Loader] [Qurre] Plugins directory not found. Creating: {LoaderManager.PluginsDir}", ConsoleColor.DarkYellow);
            Directory.CreateDirectory(LoaderManager.PluginsDir);
        }

        if (!Directory.Exists(LoaderManager.DependDir))
        {
            ServerConsole.AddLog($"[Loader] [Qurre] Dependencies directory not found. Creating: {LoaderManager.DependDir}", ConsoleColor.DarkYellow);
            Directory.CreateDirectory(LoaderManager.DependDir);
        }

        string[] needDeps = { "0Harmony.dll", "Newtonsoft.Json.dll" };
        for (int i = 0; i < needDeps.Length; i++)
        {
            string dep = needDeps[i];
            string path = Path.Combine(LoaderManager.DependDir, dep);

            if (LoaderManager.Loaded(path))
                continue;

            if (!File.Exists(path))
            {
                Download("https://cdn.scpsl.shop/qurre.sl/Dependencies/" + dep, dep);
            }

            Assembly assembly = Assembly.Load(LoaderManager.ReadFile(path));
            LoaderManager.LocalLoaded.Add(new(assembly, path));

            ServerConsole.AddLog("[Loader] [Qurre] Loaded dependency " + assembly.FullName, ConsoleColor.Blue);
        }

        static void Download(string url, string name)
        {
            ServerConsole.AddLog($"[Loader] [Qurre] {name} not found. Downloading {name}", ConsoleColor.Red);

            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();

            using Stream responseStream = response.GetResponseStream();
            using Stream fileStream = File.OpenWrite(Path.Combine(LoaderManager.DependDir, name));

            byte[] buffer = new byte[4096];
            int bytesRead = responseStream.Read(buffer, 0, 4096);

            while (bytesRead > 0)
            {
                fileStream.Write(buffer, 0, bytesRead);
                DateTime nowTime = DateTime.UtcNow;
                bytesRead = responseStream.Read(buffer, 0, 4096);
            }
        }
    }
}