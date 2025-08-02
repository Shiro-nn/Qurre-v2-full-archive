namespace Qurre
{
    using BHA;
    using Qurre.API.Addons;

    internal class ConfigsCall
    {
        internal static JsonConfig Config { get; private set; }
        public static void UpdateConfig()
        {
            if (Config is null) Config = new("Qurre");
            try { _ = Config.JsonArray; } catch { return; }
            BHA.Loader.BetterHints = (bool)Config.SafeGetTokenValue("BetterHints", false, "Enable Addon [BetterHints]?");
        }
    }
}