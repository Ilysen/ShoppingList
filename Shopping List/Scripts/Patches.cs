using HarmonyLib;
using XRL;
using XRL.World.Encounters.EncounterObjectBuilders;
using XRL.World.Parts;

namespace Ava.ShoppingList.HarmonyPatches
{
	/// <summary>
	/// This is a postfix used as one of two workarounds for the inability to easily track a merchant restocking.
	/// <see cref="GenericInventoryRestocker"/> has a function it uses (unlike <see cref="Restocker"/>), so we can easily listen for it
	/// and then manually call a shopping list check on that object afterwards.
	/// </summary>
	[HarmonyPatch(typeof(GenericInventoryRestocker))]
	class ShoppingList_GenericInventoryRestocker
	{
		[HarmonyPostfix]
		[HarmonyPatch(nameof(GenericInventoryRestocker.PerformStock))]
		static void PerformStockPatch(GenericInventoryRestocker __instance)
		{
			The.Player?.GetPart<Ava_ShoppingList_ShoppingListPart>()?.CheckObjectInventory(__instance.ParentObject);
		}
	}
}
