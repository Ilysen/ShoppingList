using System;
using System.Collections.Generic;
using System.Linq;
using XRL.Core;
using XRL.Language;
using XRL.Liquids;
using XRL.Messages;
using XRL.UI;
using XRL.World.Encounters.EncounterObjectBuilders;
using XRL.World.Tinkering;

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
					new List<string>() { $"Show it ({Wishlist.Count} item{(Wishlist.Count == 1 ? "" : "s")})", "Add something", "Remove something", "Check vendors in current zone", "Import data from code", "Export data to code" },
					new List<char>() { '1', '2', '3', '4', '5', '6' },
					AllowEscape: true))
				{
					case 0:
						if (Wishlist.Count == 0)
							Popup.Show("Your shopping list is empty.");
						else
							Popup.Show("Current shopping list: \n\n" + string.Join("\n", Wishlist.Keys));
						goto ConfigureList;
					case 1:
					QueryBlueprint:
						string s = Popup.AskString("Enter a query. Enter \"help\" or \"?\" for more info.", ReturnNullForEscape: true);
						if (s.IsNullOrEmpty())
							goto ConfigureList;
						if (s.EqualsNoCase("help") || s == "?")
						{
						Documentation:
							switch (Popup.ShowOptionList("What would you like help with?", new List<string> { "Searching for {{rules|items}}", "Searching for {{rules|data disks}}", "Searching for {{rules|items with a certain mod}}", "Searching for {{rules|pure liquids}}" }, AllowEscape: true))
							{
								case 0:
									Popup.Show("To find an item, {{rules|enter its display name or blueprint ID}} - the game will attempt to find an appropriate match and present it to you. This search can be fuzzy, but {{rules|try to be as exact as possible}} to ensure accuracy.");
									goto Documentation;
								case 1:
									Popup.Show("To find a {{rules|data disk for a particular artifact,}} search for the item itself. If you can build it, you'll have the option to add the data disk for that item (or to add both the item itself and the data disk) to your shopping list.\n\nTo find a {{rules|data disk for an item mod,}} search for the mod's name or part ID. This must be exact in order to find a match, but is not case sensitive.");
									goto Documentation;
								case 2:
									Popup.Show("To find items with a certain mod, prefix your search with {{W|modded:}} and then enter the exact display name or item mod in particular, like these examples:\n\n{{rules|modded:snail-encrusted}}\n{{rules|modded:sturdy}}\n{{rules|modded:fitted with suspensors}}\n\nThis must be exact in order to find a match, but is not case-sensitive.");
									goto Documentation;
								case 3:
									Popup.Show("To find pure liquids, {{rules|enter the display name or ID}} of the liquid you're looking for. This must be exact in order to find a match, but is not case sensitive.\n\nConveniently, many liquids have a shorthand version of their name as their ID; searching {{rules|cloning}}, for instance, will match with {{cloning|cloning draught}}!");
									goto Documentation;
							}
							goto ConfigureList;
						}
						if (s.ToLower().StartsWith("modded:"))
						{
							string itemMod = s.Split(':')[1];
							if (FindTinkerMod(itemMod, out TinkerData td))
							{
								if (Popup.ShowYesNo("Add items modded to be {{W|" + td.DisplayName + "}} to your shopping list?\n\n" + td.Description) == DialogResult.Yes)
								{
									AddToWishlist("Items modded to be {{W|" + td.DisplayName + "}}", $"Modded_{td.PartName}");
									Popup.Show("Added items modded to be {{W|" + td.DisplayName + "}} to your shopping list.");
								}
								goto ConfigureList;
							}
							Popup.Show($"Item mod not found for query '{s}'. Check your spelling or narrow your search.");
							goto QueryBlueprint;
						}
						if (FindTinkerMod(s, out TinkerData data) && data.Type == "Mod")
						{
							if (Popup.ShowYesNo("Add data disks for the {{W|" + data.DisplayName + "}} mod to your shopping list?") == DialogResult.Yes)
							{
								AddToWishlist("data disk: [{{W|Item mod}}] - {{C|" + data.DisplayName + "}}", data.PartName);
								Popup.Show("Added data disks for the {{W|" + data.DisplayName + "}} mod to your shopping list.");
							}
							goto ConfigureList;
						}
						if (FindLiquid(s, out BaseLiquid liquid))
						{
							string liquidName = liquid.GetName();
							if (Popup.ShowYesNo($"Add {liquidName} to your shopping list?") == DialogResult.Yes)
							{
								AddToWishlist(liquidName, liquid.ID);
								Popup.Show($"Added {liquidName} to your shopping list.");
							}
							goto ConfigureList;
						}
						if (FindBlueprint(s, out WishResult wr, out GameObjectBlueprint bp))
						{
							string displayName = GetNameFor(bp);
							bool canBeDisked = bp.GetPartParameter<bool>("TinkerItem", "CanBuild");
							if (!canBeDisked)
							{
								if (Popup.ShowYesNo($"Add {displayName} to your shopping list?") == DialogResult.Yes)
								{
									AddToWishlist(displayName, bp.Name);
									Popup.Show($"Added {displayName} to your shopping list.");
								}
							}
							else
							{
								int result = Popup.ShowOptionList($"Add {displayName} to your shopping list?", new List<string> { "Item only", "Item or data disk", "Data disk only" }, AllowEscape: true);
								switch (result)
								{
									case 0:
										AddToWishlist(displayName, bp.Name);
										Popup.Show($"Added {displayName} to your shopping list.");
										break;
									case 1:
										AddToWishlist(displayName, bp.Name);
										AddToWishlist($"data disk: {displayName}", $"DataDisk_{bp.Name}");
										Popup.Show($"Added {displayName} (item and data disk) to your shopping list.");
										break;
									case 2:
										AddToWishlist($"data disk: {displayName}", $"DataDisk_{bp.Name}");
										Popup.Show($"Added data disks for {displayName} to your shopping list.");
										break;
								}
							}
							goto ConfigureList;
						}
						Popup.Show($"Item mod not found for query '{s}'. Check your spelling or narrow your search.");
						goto ConfigureList;
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
								Popup.Show($"Removed the following from your shopping list:\n\n{string.Join("\n", toRemove)}");
								foreach (string key in toRemove)
									Wishlist.Remove(key);
							}
						}
						goto ConfigureList;
					case 3:
						CheckObjectsInZone(ParentObject.CurrentZone);
						goto ConfigureList;
					case 4:
						string importedCode = Popup.AskString("Paste a code here to import it into the list. {{r|Codes are not validated, and modifying them will cause erratic behavior.}}");
						if (!importedCode.IsNullOrEmpty())
						{
							List<string> newlyAdded = new List<string>();
							string[] entries = importedCode.Split('~');
							foreach (string entry in entries)
							{
								string[] breakdown = entry.Split('#');
								if (!Wishlist.Keys.Contains(breakdown[0]))
								{
									Wishlist.Add(breakdown[0], breakdown[1]);
									newlyAdded.Add(breakdown[0]);
								}
							}
							if (newlyAdded.Count == 0)
								Popup.Show("List imported successfully. No new items were added.");
							else
								Popup.Show($"List imported successfully. Added the following entries:\n\n{string.Join("\n", newlyAdded)}");
						}
						goto ConfigureList;
					case 5:
						List<string> toAssemble = new List<string>();
						foreach (var kvp in Wishlist)
							toAssemble.Add($"{kvp.Key}#{kvp.Value}");
						MessageQueue.AddPlayerMessage($"{Wishlist.Count} entries turned into: {toAssemble}");
						string exportedCode = string.Join("~", toAssemble);
						if (exportedCode.IsNullOrEmpty())
							Popup.Show("You have no shopping list to export!");
						else
							Popup.AskString("List exported to the following code. Copy this to your clipboard!", exportedCode);
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
				if (go2.TryGetPart(out DataDisk dd))
				{
					if ((dd.Data.Type == "Mod" && Wishlist.Values.Contains(dd.Data.PartName)) || Wishlist.Values.Contains($"DataDisk_{dd.Data.Blueprint}"))
					{
						stockedObjects.Add(goName, go2);
						continue;
					}
				}
				if (go2.LiquidVolume?.Volume > 0)
				{
					BaseLiquid primaryLiquid = go2.LiquidVolume.GetPrimaryLiquid();
					if (Wishlist.Values.Contains(go2.LiquidVolume.GetPrimaryLiquid().ID) && !stockedObjects.Keys.Contains(primaryLiquid.GetName()))
					{
						stockedObjects.Add(primaryLiquid.GetName(), go2);
						continue;
					}
				}
				foreach (string modName in Wishlist.Values.Where(x => x.StartsWith("Modded_")))
				{
					if (go2.HasPart(modName.Replace("Modded_", "")))
					{
						stockedObjects.Add(goName, go2);
						break;
					}
				}
			}
			if (stockedObjects.Count == 0)
			{
				go.RemovePart<Ava_ShoppingList_Highlighter>();
				return;
			}
			string directionToThing = go.CurrentZone == The.Player.CurrentZone ? The.Player.DescribeDirectionToward(go) : "in a nearby zone";
			MessageQueue.AddPlayerMessage($"{go.The + go.ShortDisplayName} {directionToThing} is stocking {Grammar.MakeAndList(stockedObjects.Keys.ToArray())} from your shopping list!", "M");
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

		private bool FindTinkerMod(string s, out TinkerData data)
		{
			foreach (TinkerData td in TinkerData.TinkerRecipes)
			{
				if (td.Type == "Mod" && td.PartName.ToLower().Contains(s.ToLower()) || td.DisplayName.Strip().ToLower().Contains(s.ToLower()))
				{
					data = td;
					return true;
				}
			}
			data = null;
			return false;
		}

		private bool FindBlueprint(string s, out WishResult wr, out GameObjectBlueprint bp)
		{
			WishResult foundResult = WishSearcher.SearchForBlueprint(s);
			if (!foundResult.Result.IsNullOrEmpty() && foundResult.NegativeMarks == 0)
			{
				wr = foundResult;
				if (GameObjectFactory.Factory.Blueprints.TryGetValue(wr.Result, out GameObjectBlueprint blueprint) && blueprint.GetPartParameter("Physics", "Takeable", true) && !blueprint.HasTag("Creature"))
				{
					bp = blueprint;
					return true;
				}
			}
			wr = null;
			bp = null;
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
			Gender g = Gender.Genders[bp.GetTag("Gender")];
			return g != null && g.Plural ? toReturn : Grammar.Pluralize(toReturn);
		}

		private void AddToWishlist(string key, string value)
		{
			if (!Wishlist.ContainsValue(value))
				Wishlist.Add(key, value);
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
