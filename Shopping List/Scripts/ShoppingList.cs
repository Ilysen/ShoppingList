using System;
using System.Collections.Generic;
using System.Linq;
using XRL.Core;
using XRL.Language;
using XRL.Liquids;
using XRL.Messages;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Encounters.EncounterObjectBuilders;

namespace XRL.World.Parts
{
	/// <summary>
	/// This part is added to the player object and handles all of the logic of the shopping list.
	/// </summary>
	[Serializable]
	public class Ava_ShoppingList_ShoppingListPart : IPlayerPart
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
			return base.WantEvent(ID, cascade) ||
				ID == CommandEvent.ID ||
				ID == ZoneActivatedEvent.ID;
		}

		public override void Register(GameObject Object)
		{
			Object.RegisterPartEvent(this, "ObjectAddedToPlayerInventory");
			base.Register(Object);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "ObjectAddedToPlayerInventory")
			{
				GameObject go = E.GetGameObjectParameter("Object");
				if (go != null)
				{
					if (UnderstandsObject(go) && ShouldProactivelyRemove)
					{
						if (go.TryGetPart(out DataDisk dd))
						{
							if (ModWishlist.ContainsKey(dd.Data.PartName))
							{
								MessageQueue.AddPlayerMessage($"Removed {ModWishlist[dd.Data.PartName]} from your shopping list.");
								ModWishlist.Remove(dd.Data.PartName);
							}
							if (BlueprintWishlist.ContainsKey(dd.Data.Blueprint))
							{
								MessageQueue.AddPlayerMessage($"Removed {BlueprintWishlist[dd.Data.Blueprint]} from your shopping list.");
								BlueprintWishlist.Remove(dd.Data.Blueprint);
							}
						}
					}
				}
			}
			return base.FireEvent(E);
		}

		public override bool HandleEvent(CommandEvent E)
		{
			if (E.Command == ShoppingListCommand && E.Actor == ParentObject)
			{
			ConfigureList:
				var CachedList = CombinedWishlist;
				switch (Popup.ShowOptionList("What would you like to do with your shopping list?",
					new List<string>() { $"Show it ({CachedList.Count} item{(CachedList.Count == 1 ? "" : "s")})", "Add something", "Remove something", "Check vendors in current zone", "Import data from code", "Export data to code" },
					new List<char>() { '1', '2', '3', '4', '5', '6' },
					AllowEscape: true))
				{
					case 0:
						if (CachedList.Count == 0)
							Popup.Show("Your shopping list is empty.");
						else
							Popup.Show("Current shopping list: \n\n" + string.Join("\n", CachedList.Values));
						goto ConfigureList;
					case 1:
					QueryBlueprint:
						string s = Popup.AskString("Enter a query. Enter \"help\" or \"?\" for more info.", ReturnNullForEscape: true);
						if (s.IsNullOrEmpty())
							goto ConfigureList;
						s = s.ToLower();
						// Check for an information query and enter the documentation submenu until manually exited
						if (s.Equals("help") || s.Equals("?"))
						{
						Documentation:
							switch (Popup.ShowOptionList("What would you like help with?", new List<string> { "Searching for {{rules|items}}", "Searching for {{rules|data disks}}", "Searching for {{rules|items with a certain mod}}", "Searching for {{rules|pure liquids}}" }, AllowEscape: true))
							{
								case 0:
									Popup.Show("To find an item, {{rules|enter its display name or blueprint ID}} - the game will attempt to find an appropriate match and present it to you. This search can be fuzzy, but {{rules|try to be as exact as possible}} to ensure accuracy.");
									goto Documentation;
								case 1:
									Popup.Show("To find a {{rules|data disk for a particular artifact,}} search for the item itself. If you can build it, you'll have the option to add the data disk for that item (or to add both the item itself and the data disk) to your shopping list.\n\nTo find a {{rules|data disk for an item mod,}} prefix your search with {{W|mod:}}, then search for the mod's name or part ID. This must be exact in order to find a match, but is not case sensitive. Some examples include:\n\n{{rules|mod:slender}}\n{{rules|mod:overloaded}}\n{{rules|mod:jacked}}");
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
						string display;
						string key;
						// Check specifiers, starting with modded items
						if (s.StartsWith("modded:"))
						{
							string itemMod = s.Split(':')[1];
							if (FindTinkerMod(itemMod, out ModEntry me, false))
							{
								display = GetDisplayName(me, true);
								if (Popup.ShowYesNo("Add items modded to be {{W|" + me.TinkerDisplayName + "}} to your shopping list?") == DialogResult.Yes)
								{
									AddToWishlist(ref ModdedWishlist, me.Part, display);
									Popup.Show("Added items modded to be {{W|" + me.TinkerDisplayName + "}} to your shopping list.");
								}
								goto ConfigureList;
							}
							Popup.Show($"Item mod not found for query '{s}'. Check your spelling or narrow your search.");
							goto QueryBlueprint;
						}
						// Check for an exact match with a tinker mod that can be applied by the player
						if (s.StartsWith("mod:"))
						{
							string modName = s.Split(':')[1];
							if (FindTinkerMod(modName, out ModEntry data))
							{
								display = GetDisplayName(data, false);
								if (Popup.ShowYesNo("Add data disks for the {{W|" + data.TinkerDisplayName + "}} mod to your shopping list?") == DialogResult.Yes)
								{
									AddToWishlist(ref ModWishlist, data.Part, display);
									Popup.Show("Added data disks for the {{W|" + data.TinkerDisplayName + "}} mod to your shopping list.");
								}
								goto ConfigureList;
							}
						}
						// Check for an exact match with a liquid type
						if (FindLiquid(s, out BaseLiquid liquid))
						{
							key = liquid.ID;
							display = GetDisplayName(liquid);
							if (Popup.ShowYesNo($"Add {display} to your shopping list?") == DialogResult.Yes)
							{
								AddToWishlist(ref LiquidWishlist, key, display);
								Popup.Show($"Added {display} to your shopping list.");
							}
							goto ConfigureList;
						}
						// If all else fails, perform a fuzzy search for a valid object
						if (FindBlueprint(s, out GameObjectBlueprint bp))
						{
							display = GetDisplayName(bp);
							key = bp.Name;
							bool canBeDisked = bp.GetPartParameter<bool>("TinkerItem", "CanBuild");
							if (!canBeDisked)
							{
								if (Popup.ShowYesNo($"Add {display} to your shopping list?") == DialogResult.Yes)
								{
									AddToWishlist(ref ItemWishlist, key, display);
									Popup.Show($"Added {display} to your shopping list.");
								}
							}
							else
							{
								int result = Popup.ShowOptionList($"Add {display} to your shopping list?", new List<string> { "Item only", "Item or data disk", "Data disk only" }, AllowEscape: true);
								string diskName = GetDisplayName(bp, true);
								switch (result)
								{
									case 0:
										AddToWishlist(ref ItemWishlist, key, display);
										Popup.Show($"Added {display} to your shopping list.");
										break;
									case 1:
										AddToWishlist(ref ItemWishlist, key, display);
										AddToWishlist(ref BlueprintWishlist, bp.Name, diskName);
										Popup.Show($"Added {display} (item and data disk) to your shopping list.");
										break;
									case 2:
										AddToWishlist(ref BlueprintWishlist, bp.Name, diskName);
										Popup.Show($"Added data disks for {display} to your shopping list.");
										break;
								}
							}
							goto ConfigureList;
						}
						Popup.Show($"Item mod not found for query '{s}'. Check your spelling or narrow your search.");
						goto ConfigureList;
					case 2:
						if (CachedList.Count == 0)
							Popup.Show("There are no items in your shopping list.");
						else
						{
							Dictionary<string, string> toRemove = new Dictionary<string, string>();
							List<int> indexesToRemove = Popup.PickSeveral("Pick the shopping list entries you'd like to remove.", CachedList.Values.ToArray(), AllowEscape: true);
							if (!indexesToRemove.IsNullOrEmpty())
								foreach (int i in indexesToRemove)
									toRemove.Add(CachedList.ElementAt(i).Key, CachedList.ElementAt(i).Value);
							if (toRemove.Count > 0)
							{
								Popup.Show($"Removing the following from your shopping list:\n\n{string.Join("\n", toRemove.Values)}");
								foreach (var kvp in toRemove)
								{
									ItemWishlist.Remove(kvp.Key.Replace("Item:", ""));
									LiquidWishlist.Remove(kvp.Key.Replace("Liquid:", ""));
									ModWishlist.Remove(kvp.Key.Replace("ModDisk:", ""));
									ModdedWishlist.Remove(kvp.Key.Replace("Modded:", ""));
									BlueprintWishlist.Remove(kvp.Key.Replace("ItemDisk:", ""));
								}
							}
						}
						goto ConfigureList;
					case 3:
						CheckObjectsInZone(ParentObject.CurrentZone);
						goto ConfigureList;
					case 4:
						string importedCode = Popup.AskString("Paste a code here to import it into the list. Duplicates are automatically skipped. {{r|Modified codes may cause erratic behavior.}}");
						if (!importedCode.IsNullOrEmpty())
						{
							//MessageQueue.AddPlayerMessage("parsing code: " + importedCode);
							List<string> newlyAdded = new List<string>();
							string[] entries = importedCode.Split('~');
							foreach (string entry in entries)
							{
								string[] breakdown = entry.Split(':');
								//MessageQueue.AddPlayerMessage($"parsing entry {entry} with {breakdown.Count()} subcodes");
								if (breakdown.Count() < 2)
									continue;
								string nameFor = GetDisplayName(entry);
								//MessageQueue.AddPlayerMessage($"display name for {entry} returns {nameFor}");
								if (nameFor == string.Empty)
									continue;
								//MessageQueue.AddPlayerMessage($"first entry of breakdown: {breakdown[0]}");
								switch (breakdown[0])
								{
									case "Item":
										if (AddToWishlist(ref ItemWishlist, breakdown[1], nameFor))
											newlyAdded.Add(nameFor);
										break;
									case "Liquid":
										if (AddToWishlist(ref LiquidWishlist, breakdown[1], nameFor))
											newlyAdded.Add(nameFor);
										break;
									case "ModDisk":
										if (AddToWishlist(ref ModWishlist, breakdown[1], nameFor))
											newlyAdded.Add(nameFor);
										break;
									case "Modded":
										if (AddToWishlist(ref ModdedWishlist, breakdown[1], nameFor))
											newlyAdded.Add(nameFor);
										break;
									case "ItemDisk":
										if (AddToWishlist(ref BlueprintWishlist, breakdown[1], nameFor))
											newlyAdded.Add(nameFor);
										break;
								}
							}
							if (newlyAdded.Count == 0)
								Popup.Show("List imported successfully. No new items were added.");
							else
								Popup.Show($"List imported successfully. Added the following entries:\n\n{string.Join("\n", newlyAdded)}");
						}
						goto ConfigureList;
					case 5:
						string exportedCode = string.Empty;
						List<string> subentries = new List<string>();
						foreach (string entry in ItemWishlist.Keys)
							subentries.Add($"Item:{entry}");
						foreach (string entry in LiquidWishlist.Keys)
							subentries.Add($"Liquid:{entry}");
						foreach (string entry in ModWishlist.Keys)
							subentries.Add($"ModDisk:{entry}");
						foreach (string entry in ModdedWishlist.Keys)
							subentries.Add($"Modded:{entry}");
						foreach (string entry in BlueprintWishlist.Keys)
							subentries.Add($"ItemDisk:{entry}");
						exportedCode += string.Join("~", subentries);
						//MessageQueue.AddPlayerMessage($"{ItemWishlist.Count} entries turned into: {exportedCode}");
						if (exportedCode.IsNullOrEmpty())
							Popup.Show("You have no shopping list to export!");
						else
							Popup.AskString("List exported to the following code. Copy this to your clipboard:", exportedCode);
						goto ConfigureList;
				}
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			if (CombinedWishlist.Count > 0 && E.Zone == The.ActiveZone)
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
				if (!UnderstandsObject(go2) || !go2.WillTrade())
					continue;
				string goName = go2.an();
				if ((ItemWishlist.ContainsKey(bp.Name) || ItemWishlist.ContainsKey(bp.Inherits)) && !stockedObjects.ContainsValue(go2))
					stockedObjects.Add(goName, go2);
				if (go2.TryGetPart(out DataDisk dd))
				{
					if ((dd.Data.Type == "Mod" && ModWishlist.ContainsKey(dd.Data.PartName)) || BlueprintWishlist.ContainsKey(dd.Data.Blueprint))
					{
						stockedObjects.Add(goName, go2);
						continue;
					}
				}
				if (go2.LiquidVolume?.Volume > 0)
				{
					BaseLiquid primaryLiquid = go2.LiquidVolume.GetPrimaryLiquid();
					if (LiquidWishlist.ContainsKey(primaryLiquid.ID) && !stockedObjects.ContainsKey(primaryLiquid.GetName()))
					{
						stockedObjects.Add(primaryLiquid.GetName(), go2);
						continue;
					}
				}
				foreach (string modName in ModdedWishlist.Keys)
				{
					if (go2.HasPart(modName))
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
			if (go.TryGetPart(out Interesting i) && !i.RequirementsMet(The.Player))
				return false;
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
			IEnumerable<BaseLiquid> liquidMatches = LiquidVolume.getAllLiquids().Where(x => x.ID.ToLower() == s || x.Name.Strip().ToLower() == s);
			if (liquidMatches.Count() > 0)
			{
				liquid = liquidMatches.ElementAt(0);
				return true;
			}
			liquid = null;
			return false;
		}

		/// <summary>
		/// Checks if the provided string equals the ID or stripped display name of a tinkerable item mod.
		/// </summary>
		private bool FindTinkerMod(string s, out ModEntry data, bool onlyCanApply = true)
		{
			foreach (ModEntry me in ModificationFactory.ModList)
			{
				if (onlyCanApply && !me.TinkerAllowed)
					continue;
				if (me.Part.ToLower().Contains(s) || me.Part.EqualsNoCase(s) || me.TinkerDisplayName.Strip().ToLower().Contains(s))
				{
					data = me;
					return true;
				}
			}
			data = null;
			return false;
		}

		/// <summary>
		/// Attempts to find a valid object blueprint through WishSearcher's blueprint search algorithm.
		/// Only results with zero negative marks whose blueprints are takeable and not creatures will be selected.
		/// </summary>
		private bool FindBlueprint(string s, out GameObjectBlueprint bp)
		{
			WishResult foundResult = WishSearcher.SearchForBlueprint(s);
			if (!foundResult.Result.IsNullOrEmpty() && foundResult.NegativeMarks == 0)
			{
				if (GameObjectFactory.Factory.Blueprints.TryGetValue(foundResult.Result, out GameObjectBlueprint blueprint) && blueprint.GetPartParameter("Physics", "Takeable", true) && !blueprint.HasTag("Creature"))
				{
					bp = blueprint;
					return true;
				}
			}
			bp = null;
			return false;
		}

		private string GetDisplayName(ModEntry mod, bool moddedWith) => moddedWith ? "Items modded to be {{W|" + mod.TinkerDisplayName + "}}" : "data disk: [{{W|Item mod}}] - {{C|" + mod.TinkerDisplayName + "}}";

		private string GetDisplayName(GameObjectBlueprint bp, bool dataDisk = false)
		{
			if (dataDisk)
				return $"data disk: {bp.DisplayName()}";
			string toReturn = bp.DisplayName();
			if (bp.HasPart("CyberneticsBaseItem"))
				toReturn = "[{{W|Implant}}] - " + toReturn;
			else
			{
				Gender g = Gender.Genders[bp.GetTag("Gender")];
				if (g == null || !g.Plural)
					toReturn = Grammar.Pluralize(toReturn);
			}
			return toReturn;
		}

		private string GetDisplayName(BaseLiquid bl) => bl.GetName();

		private string GetDisplayName(string s)
		{
			string[] split = s.Split(':');
			if (s.StartsWith("Liquid:"))
			{
				if (FindLiquid(split[1], out BaseLiquid liquid))
					return GetDisplayName(liquid);
			}
			else if (s.StartsWith("Modded:"))
			{
				//MessageQueue.AddPlayerMessage($"fetching display name for data disk: {s}, search key: {split[1]}");
				if (FindTinkerMod(split[1], out ModEntry mod, false))
					return GetDisplayName(mod, true);
				//else
				//	MessageQueue.AddPlayerMessage("found no mod entry");
			}
			else if (s.StartsWith("ModDisk:"))
			{
				//MessageQueue.AddPlayerMessage($"fetching display name for data disk: {s}, search key: {split[1]}");
				if (FindTinkerMod(split[1], out ModEntry mod, false))
					return GetDisplayName(mod, false);
				//else
				//	MessageQueue.AddPlayerMessage("found no mod entry");
			}
			else if (s.StartsWith("ItemDisk:"))
			{
				if (FindBlueprint(split[1], out GameObjectBlueprint blue))
					return GetDisplayName(blue, true);
			}
			else if (s.StartsWith("Item:"))
			{
				if (FindBlueprint(split[1], out GameObjectBlueprint blue))
					return GetDisplayName(blue);
			}
			return string.Empty;
		}

		/// <summary>
		/// Adds a unique entry to the wishlist with a provided display name and value.
		/// This is effectively a wrapper for <c>Wishlist.Add(key, value)</c> but with a check to ensure no duplicates.
		/// </summary>
		private bool AddToWishlist(ref SortedList<string, string> list, string key, string value)
		{
			if (!list.ContainsKey(key))
			{
				list.Add(key, value);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Returns whether or not the given <see cref="GameObject"/> is considered to be fully understood for the purposes of shopping list notifications.
		/// Most objects just check <see cref="GameObject.Understood()"/>, but data disks have special handling that needs to be replicated here.
		/// </summary>
		private bool UnderstandsObject(GameObject Object)
		{
			if (!Object.Understood())
				return false;
			if (Object.HasPart<DataDisk>())
				if (!The.Player.HasSkill("Tinkering") && !Scanning.HasScanningFor(The.Player, Scanning.Scan.Tech))
					return false;
			return true;
		}

		/// <summary>
		/// Getter for a combined <see cref="Dictionary{TKey, TValue}"/> representing all of the wishlists merged together.
		/// To prevent duplicates, each entry's key is prefixed with a string depending on the wishlist it originates from.
		/// </summary>
		private Dictionary<string, string> CombinedWishlist
		{
			get
			{
				var toReturn = new Dictionary<string, string>();
				foreach (KeyValuePair<string, string> kvp in ItemWishlist)
					toReturn.Add($"Item:{kvp.Key}", kvp.Value);
				foreach (KeyValuePair<string, string> kvp in LiquidWishlist)
					toReturn.Add($"Liquid:{kvp.Key}", kvp.Value);
				foreach (KeyValuePair<string, string> kvp in ModWishlist)
					toReturn.Add($"ModDisk:{kvp.Key}", kvp.Value);
				foreach (KeyValuePair<string, string> kvp in ModdedWishlist)
					toReturn.Add($"Modded:{kvp.Key}", kvp.Value);
				foreach (KeyValuePair<string, string> kvp in BlueprintWishlist)
					toReturn.Add($"ItemDisk:{kvp.Key}", kvp.Value);
				return toReturn;
			}
		}

		/// <summary>
		/// Getter function for the "highlight vendors with items" setting.
		/// </summary>
		private bool ShouldHighlight => Options.GetOption("Ava_ShoppingList_HighlightVendorsWithItems").EqualsNoCase("Yes");

		/// <summary>
		/// Getter function for the "proactively remove data disks" setting.
		/// Could be expanded in the future.
		/// </summary>
		private bool ShouldProactivelyRemove => Options.GetOption("Ava_ShoppingList_ProactivelyRemoveItems").EqualsNoCase("Yes");

		/// <summary>
		/// This is a workaround for the current inability to effectively track when a <see cref="Restocker"/> restocks its inventory.
		/// If the part is about to restock it, we add it to this list, and then iterate through it every turn thereafter,
		/// checking the restock time of each associated part and manually triggering a list check on the associated <see cref="GameObject"/>
		/// if it's no longer about to fire (i.e. we can safely assume a restock has happened).
		/// <br/><br/>
		/// Ideally, this'll be removed if we ever get an event to track restocks.
		/// </summary>
		private readonly Dictionary<GameObject, Restocker> deferredObjects = new Dictionary<GameObject, Restocker>();

		/*
		 * Each of these dictionaries is used to track things from the player's shopping list.
		 * All of them use a blueprint/ID/etc as their key, and a display name as their value.
		 */

		/// <summary>
		/// Tracks specific items via their blueprint name.
		/// </summary>
		public SortedList<string, string> ItemWishlist = new SortedList<string, string>();

		/// <summary>
		/// Tracks pure liquids via their string ID.
		/// </summary>
		public SortedList<string, string> LiquidWishlist = new SortedList<string, string>();

		/// <summary>
		/// Tracks item mods via their part name. This list is used to check specifically for matching data disks.
		/// </summary>
		public SortedList<string, string> ModWishlist = new SortedList<string, string>();

		/// <summary>
		/// As <see cref="ModWishlist"/>, but tracks items with the particular part instead.
		/// </summary>
		public SortedList<string, string> ModdedWishlist = new SortedList<string, string>();

		/// <summary>
		/// As <see cref="ItemWishlist"/>, but tracks data disks for the blueprint instead.
		/// </summary>
		public SortedList<string, string> BlueprintWishlist = new SortedList<string, string>();
	}
}
