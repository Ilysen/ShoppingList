using XRL;
using XRL.World;
using XRL.World.Parts;

namespace Ilysen.ShoppingList.Scripts
{
    [HasCallAfterGameLoaded]
    public class LoadGameHandler
    {
        [CallAfterGameLoaded]
        public static void AfterLoaded()
        {
            The.Player?.RequirePart<Ilysen_ShoppingList_ShoppingListPart>();
        }
    }

    [PlayerMutator]
    public class NewCharacterHandler : IPlayerMutator
    {
        public void mutate(GameObject player)
        {
            player.AddPart<Ilysen_ShoppingList_ShoppingListPart>();
        }
    }
}
