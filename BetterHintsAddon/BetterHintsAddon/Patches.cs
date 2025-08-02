using HarmonyLib;
using Qurre.API.Addons.BetterHints;
using Mg = Qurre.API.Addons.BetterHints.Manager;
namespace BHA
{
	[HarmonyPatch(typeof(Mg), nameof(Mg.Sender), MethodType.Getter)]
	internal static class GetSender
	{
        [HarmonyPrefix]
		public static bool Prefix(out ISender __result)
		{
			__result = Loader.Sender;
			return false;
		}
	}
}