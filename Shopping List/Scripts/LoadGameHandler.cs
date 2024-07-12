using XRL;
using XRL.World;
using XRL.World.Parts;

namespace Ceres.ShoppingList.Scripts
{
	[HasCallAfterGameLoaded]
	public class LoadGameHandler
	{
		[CallAfterGameLoaded]
		public static void AfterLoaded()
		{
			The.Player?.RequirePart<Ceres_ShoppingList_ShoppingListPart>();
		}
	}

	[PlayerMutator]
	public class NewCharacterHandler : IPlayerMutator
	{
		public void mutate(GameObject player)
		{
			player.RequirePart<Ceres_ShoppingList_ShoppingListPart>();
		}
	}
}
