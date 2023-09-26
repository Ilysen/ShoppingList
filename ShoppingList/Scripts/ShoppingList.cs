using System;
using System.Collections.Generic;
using System.Linq;
using XRL.Language;
using XRL.Liquids;
using XRL.Messages;
using XRL.UI;
using XRL.World.Encounters.EncounterObjectBuilders;

namespace XRL.World.Parts
{
    [Serializable]
    public class Ilysen_ShoppingList_ShoppingListPart : IPart
    {
        /// <summary>
        /// The string command used to toggle hauling. Should correspond to the key in Abilities.xml.
        /// </summary>
        public static readonly string ShoppingListCommand = "Ilysen_ShoppingList_ConfigureShoppingList";

        /// <summary>
        /// The <see cref="Guid"/> of the active ability that's used to keep track of 
        /// </summary>
        public Guid ActivatedAbility;

        public override void Attach()
        {
            if (ActivatedAbility == Guid.Empty)
                ActivatedAbility = ParentObject.AddActivatedAbility("Shopping List", ShoppingListCommand, "Skill", Silent: true);
            base.Attach();
        }

        public override void Remove()
        {
            RemoveMyActivatedAbility(ref ActivatedAbility, ParentObject);
            base.Remove();
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == CommandEvent.ID || ID == ZoneActivatedEvent.ID;
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command == ShoppingListCommand && E.Actor == ParentObject)
            {
            ConfigureList:
                switch (Popup.ShowOptionList("What would you like to do with your shopping list?",
                    new List<string>() { $"Show it ({Wishlist.Count} item{(Wishlist.Count == 1 ? "" : "s")})", "Add something", "Remove something", "Cancel" },
                    new List<char>() { '1', '2', '3', '4' },
                    AllowEscape: true))
                {
                    case 0:
                        if (Wishlist.Count == 0)
                            Popup.Show("Your shopping list is empty.");
                        else
                            Popup.Show("Current shopping list: " + Grammar.MakeAndList(Wishlist.Keys.ToArray()));
                        goto ConfigureList;
                    case 1:
                    QueryBlueprint:
                        string s = Popup.AskString("Enter an item or liquid.", ReturnNullForEscape: true);
                        if (s.IsNullOrEmpty())
                            goto ConfigureList;
                        if (FindLiquid(s, out BaseLiquid liquid))
                        {
                            string liquidName = liquid.GetName();
                            if (Popup.ShowYesNo($"Add {liquidName} to your shopping list?") == DialogResult.Yes)
                            {
                                if (!Wishlist.ContainsValue(liquid.ID))
                                    Wishlist.Add(liquidName, liquid.ID);
                                Popup.Show($"Added {liquidName} to your shopping list.");
                            }
                            goto ConfigureList;
                        }
                        else
                        {
                            WishResult wr = WishSearcher.SearchForBlueprint(s);
                            if (!wr.Result.IsNullOrEmpty() && wr.NegativeMarks == 0 && GameObjectFactory.Factory.Blueprints.TryGetValue(wr.Result, out GameObjectBlueprint bp) && bp.GetPartParameter("Physics", "Takeable", true) && !bp.HasTag("Creature"))
                            {
                                string displayName = GetNameFor(bp);
                                if (Popup.ShowYesNo($"Add {displayName} to your shopping list?") == DialogResult.Yes)
                                {
                                    if (!Wishlist.ContainsValue(bp.Name))
                                        Wishlist.Add(displayName, bp.Name);
                                    Popup.Show($"Added {displayName} to your shopping list.");
                                }
                                goto ConfigureList;
                            }
                            else
                            {
                                Popup.Show($"Blueprint not found for query '{s}'. Check your spelling or narrow your search!");
                                goto QueryBlueprint;
                            }
                        }
                    case 2:
                        if (Wishlist.Count == 0)
                            Popup.Show("There are no items in your shopping list.");
                        else
                        {
                            List<string> toRemove = new List<string>();
                            List<int> indexesToRemove = Popup.PickSeveral("Pick the shopping list entries you'd like to remove.", Wishlist.Keys.ToArray(), AllowEscape: true);
                            if (!indexesToRemove.IsNullOrEmpty())
                                foreach (int i in indexesToRemove)
                                    toRemove.Add(Wishlist.ElementAt(i).Key);
                            if (toRemove.Count > 0)
                            {
                                Popup.Show($"Removed the following from your shopping list: {Grammar.MakeAndList(toRemove.ToArray())}");
                                foreach (string key in toRemove)
                                    Wishlist.Remove(key);
                            }
                        }
                        goto ConfigureList;
                }
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(ZoneActivatedEvent E)
        {
            if (Wishlist.Count == 0)
                return base.HandleEvent(E);
            foreach (GameObject go in E.Zone.GetObjects().Where(x => x.HasPart<Restocker>() || x.HasPart<GenericInventoryRestocker>()))
            {
                SortedList<string, GameObject> stockedObjects = new SortedList<string, GameObject>();
                foreach (GameObject go2 in go.Inventory.Objects.Where(x => TradeUI.ValidForTrade(x, go)))
                {
                    GameObjectBlueprint bp = go2.GetBlueprint();
                    if (!go2.Understood())
                        continue;
                    string goName = go2.an();
                    if (Wishlist.Values.Contains(bp.Name) || Wishlist.Values.Contains(bp.Inherits) && !stockedObjects.Keys.Contains(goName))
                        stockedObjects.Add(goName, go2);
                    else if (go2.LiquidVolume?.Volume > 0)
                    {
                        BaseLiquid primaryLiquid = go2.LiquidVolume.GetPrimaryLiquid();
                        if (Wishlist.Values.Contains(go2.LiquidVolume.GetPrimaryLiquid().ID) && !stockedObjects.Keys.Contains(primaryLiquid.GetName()))
                            stockedObjects.Add(primaryLiquid.GetName(), go2);
                    }
                    else
                        continue;
                }
                if (stockedObjects.Count == 0)
                    continue;
                MessageQueue.AddPlayerMessage($"{go.The + go.ShortDisplayName} {The.Player.DescribeDirectionToward(go)} is stocking {Grammar.MakeAndList(stockedObjects.Keys.ToArray())} from your shopping list!", "M");
            }
            return base.HandleEvent(E);
        }

        private bool FindLiquid(string s, out BaseLiquid liquid)
        {
            IEnumerable<BaseLiquid> liquidMatches = LiquidVolume.getAllLiquids().Where(x => x.ID.ToLower() == s.ToLower() || x.Name.Strip().ToLower() == s.ToLower());
            if (liquidMatches.Count() > 0)
            {
                liquid = liquidMatches.ElementAt(0);
                return true;
            }
            liquid = null;
            return false;
        }

        private string GetNameFor(GameObjectBlueprint bp)
        {
            string toReturn = bp.DisplayName();
            if (bp.HasPart("CyberneticsBaseItem"))
                toReturn += " implant";
            return toReturn;
        }

        public SortedList<string, string> Wishlist = new SortedList<string, string>();
    }
}
