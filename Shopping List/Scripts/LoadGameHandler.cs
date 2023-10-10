using XRL;
using XRL.World;
using XRL.World.Parts;

namespace Ava.ShoppingList.Scripts
{
	[HasCallAfterGameLoaded]
	public class LoadGameHandler
	{
		[CallAfterGameLoaded]
		public static void AfterLoaded()
		{
			The.Player?.RequirePart<Ava_ShoppingList_ShoppingListPart>();
		}
	}

	[PlayerMutator]
	public class NewCharacterHandler : IPlayerMutator
	{
		public void mutate(GameObject player)
		{
			player.RequirePart<Ava_ShoppingList_ShoppingListPart>();
		}
	}
}
