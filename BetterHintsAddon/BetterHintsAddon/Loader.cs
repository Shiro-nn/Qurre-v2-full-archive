using HarmonyLib;
using Hints;
using Qurre.API;
using Qurre.API.Addons;
using System.Linq;
using UnityEngine.SceneManagement;
namespace BHA
{
    internal class Loader : ICharacterLoader
    {
        private static Harmony hInstance;
        internal const string HintToken = "sufh*&Y(s7ghf7wY67f8WFGG*F&^WS&8";
        internal const string CorTag = "QurreAddonBetterHints.Coroutine.Update";
        internal static Qurre.HintSender Sender = new();
        internal static bool BetterHints = false;

        public void Init()
        {
            MEC.Timing.CallDelayed(5f, () => Qurre.ConfigsCall.UpdateConfig());

            hInstance = new Harmony("qurre.bha");
            {
                var original = AccessTools.Method(typeof(HintDisplay), nameof(HintDisplay.Show));
                var method = SymbolExtensions.GetMethodInfo((HintDisplay __instance, Hint hint) => Patch(__instance, hint));
                hInstance.Patch(original, new HarmonyMethod(method));
            }
            try
            {
                var original = AccessTools.Method(typeof(JsonConfig), "Init");
                var method = SymbolExtensions.GetMethodInfo(() => Qurre.ConfigsCall.UpdateConfig);
                hInstance.Patch(original, postfix: new HarmonyMethod(method));
            }
            catch { }
            hInstance.PatchAll();

            Core.InjectEventMethod(AccessTools.Method(typeof(Loader), nameof(Loader.Waiting)));
            SceneManager.sceneUnloaded += (Scene _) => { MEC.Timing.KillCoroutines(CorTag); Manager.ClearFields(); };
        }

        [Qurre.API.Attributes.EventMethod(Qurre.Events.RoundEvents.Waiting)]
        static void Waiting() => MEC.Timing.RunCoroutine(Manager.Сycle(), CorTag);

        private static bool Patch(HintDisplay __instance, Hint hint)
        {
            if (!BetterHints) return true;
            if (hint is not TextHint _h) return true;
            if (_h.Parameters.Any(x => Yes(x))) return true;
            if (!Player.List.TryFind(out var pl, x => x.ReferenceHub.netIdentity.netId == __instance.netId))
                return false;
            Manager.ShowHint(pl, _h.Text, _h.DurationScalar);
            return false;
            static bool Yes(HintParameter hp)
            {
                if (hp is not StringHintParameter shp) return false;
                return shp.Value.Contains(HintToken);
            }
        }
    }
}