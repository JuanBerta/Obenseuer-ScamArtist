using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ScamArtistMod
{
	// While the scam ledger is open, the game's key bindings and mouse/movement axes report nothing,
	// so the camera doesn't turn and no game hotkey fires (Esc included: the ledger closes instead).
	[HarmonyPatch]
	internal static class LedgerBlockBool
	{
		private static readonly string[] Names = { "IsKeyDown", "IsKeyUp", "IsKey", "GetKeyDown", "GetKeyDownKeyboardOnly", "GetKeyUp", "GetKey", "GetKeyWithHold", "GetAuxiliaryReloadDown" };
		private static IEnumerable<MethodBase> TargetMethods()
		{
			return AccessTools.GetDeclaredMethods(typeof(InputManager)).Where(m => Names.Contains(m.Name) && m.ReturnType == typeof(bool)).Cast<MethodBase>();
		}
		private static bool Prefix(ref bool __result)
		{
			if (!ScamArtistPlugin.LedgerOpen) return true;
			__result = false;
			return false;
		}
	}

	[HarmonyPatch]
	internal static class LedgerBlockAxis
	{
		private static IEnumerable<MethodBase> TargetMethods()
		{
			return AccessTools.GetDeclaredMethods(typeof(InputManager)).Where(m => m.Name.StartsWith("Get") && m.Name.EndsWith("Input") && m.ReturnType == typeof(float)).Cast<MethodBase>();
		}
		private static bool Prefix(ref float __result)
		{
			if (!ScamArtistPlugin.LedgerOpen) return true;
			__result = 0f;
			return false;
		}
	}
}
