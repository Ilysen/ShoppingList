using System;
using System.Collections.Generic;
using System.Linq;
using XRL.Core;
using XRL.Language;
using XRL.Liquids;
using XRL.Messages;
using XRL.UI;
using XRL.World.Encounters.EncounterObjectBuilders;

namespace XRL.World.Parts
{
	/// <summary>
	/// This part is added to the player object and handles all of the logic of the shopping list.
	/// </summary>
	[Serializable]
	public class Ava_ShoppingList_ShoppingListPart : IPart
	{
		/// <summary>
		/// The string command used to open the shopping list menu. Should correspond to the key in Abilities.xml.
		/// </summary>
		public static readonly string ShoppingListCommand = "Ava_ShoppingList_ConfigureShoppingList";

		/// <summary>
		/// The <see cref="Guid"/> of the active ability that's used to open the shopping list.
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

		public override bool WantTurnTick()
		{
			return deferredObjects.Count > 0;
		}

		public override void TurnTick(long TurnNumber)
		{
			//MessageQueue.AddPlayerMessage($"Turn ticking. Deferred objects count: {deferredObjects.Count}");
			for (int i = 0; i < deferredObjects.Count; i++)
			{
				KeyValuePair<GameObject, Restocker> obj = deferredObjects.ElementAt(i);
				//MessageQueue.AddPlayerMessage($"  {obj.Key.DisplayName}");
				if (obj.Key == null || obj.Value == null)
				{
					deferredObjects.Remove(obj.Key);
					continue;
				}
				//MessageQueue.AddPlayerMessage($"  Will classify for checking if {obj.Value.NextRestockTick} > {XRLCore.CurrentTurn}");
				if (obj.Value.NextRestockTick > XRLCore.CurrentTurn)
				{
					//MessageQueue.AddPlayerMessage("    Classifies. Removing");
					CheckObjectInventory(obj.Key);
					deferredObjects.Remove(obj.Key);
					continue;
				}
			}
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
					new List<string>() { $"Show it ({Wishlist.Count} item{(Wishlist.Count == 1 ? "" : "s")})", "Add something", "Remove something", "Check vendors in current zone", "Cancel" },
					new List<char>() { '1', '2', '3', '4', '5' },
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
						string s = Popup.AskString("Enter an item blueprint (fuzzy) or liquid name (must be an exact name or ID).", ReturnNullForEscape: true);
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
					case 3:
						CheckObjectsInZone(ParentObject.CurrentZone);
						goto ConfigureList;
				}
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			if (Wishlist.Count > 0)
				CheckObjectsInZone(E.Zone);
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Checks every object in the provided <see cref="Zone"/> to see if they're stocking anything from the shopping list.
		/// </summary>
		private void CheckObjectsInZone(Zone z)
		{
			foreach (GameObject go in z.GetObjects().Where(x => ShouldCheckObject(x)))
				CheckObjectInventory(go);
		}

		/// <summary>
		/// Searches the inventory of the provided <see cref="GameObject"/> to see if there are any matches to our shopping list.
		/// If there are, then we show a message and apply a highlighter part if the setting is enabled.
		/// </summary>
		internal void CheckObjectInventory(GameObject go)
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
			{
				go.RemovePart<Ava_ShoppingList_Highlighter>();
				return;
			}
			MessageQueue.AddPlayerMessage($"{go.The + go.ShortDisplayName} {The.Player.DescribeDirectionToward(go)} is stocking {Grammar.MakeAndList(stockedObjects.Keys.ToArray())} from your shopping list!", "M");
			if (ShouldHighlight)
				go.RequirePart<Ava_ShoppingList_Highlighter>().CachedObjects = stockedObjects.Values.ToList();
		}

		/// <summary>
		/// Determines if the provided <see cref="GameObject"/> should have their inventory checked for shopping list items.
		/// If they have a <see cref="Restocker"/> or <see cref="GenericInventoryRestocker"/> part and are not about to restock, this returns true.
		/// </summary>
		private bool ShouldCheckObject(GameObject go)
		{
			Restocker res = go.GetPart<Restocker>();
			if (res != null && res.NextRestockTick <= XRLCore.CurrentTurn)
			{
				//MessageQueue.AddPlayerMessage($"{go.DisplayName} is about to restock and so we are moving them to the deferred list ");
				deferredObjects[go] = res;
				return false;
			}
			GenericInventoryRestocker gir = go.GetPart<GenericInventoryRestocker>();
			if (gir != null && gir.RestockFrequency <= XRLCore.CurrentTurn - gir.LastRestockTick)
				return false;
			return res != null || gir != null;
		}

		/// <summary>
		/// Checks if the provided string equals the ID or stripped display name of any liquid type.
		/// </summary>
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

		/// <summary>
		/// Returns the display name we should use for the provided <see cref="GameObjectBlueprint"/>.
		/// This is to make it more clear what some things (like cybernetics implants) are.
		/// </summary>
		private string GetNameFor(GameObjectBlueprint bp)
		{
			string toReturn = bp.DisplayName();
			if (bp.HasPart("CyberneticsBaseItem"))
				toReturn = "[{{W|Implant}}] - " + toReturn;
			return toReturn;
		}

		private bool ShouldHighlight => Options.GetOption("Ava_ShoppingList_HighlightVendorsWithItems").EqualsNoCase("Yes");

		/// <summary>
		/// This is a workaround for the current inability to effectively track when a <see cref="Restocker"/> restocks its inventory.
		/// If the part is about to restock it, we add it to this list, and then iterate through it every turn thereafter,
		/// checking the restock time of each associated part and manually triggering a list check on the associated <see cref="GameObject"/>
		/// if it's no longer about to fire (i.e. we can safely assume a restock has happened).
		/// <br/><br/>
		/// Ideally, this'll be removed if we ever get an event to track restocks.
		/// </summary>
		private readonly Dictionary<GameObject, Restocker> deferredObjects = new Dictionary<GameObject, Restocker>();

		/// <summary>
		/// A list of blueprint paths/liquid IDs associated to their relevant display names.
		/// We use the display names for showing these in the UI in a readable way;
		/// most of the logic here is done with the values instead.
		/// </summary>
		public SortedList<string, string> Wishlist = new SortedList<string, string>();
	}
}
