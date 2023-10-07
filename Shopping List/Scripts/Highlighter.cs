using System;
using System.Collections.Generic;
using XRL.Core;

namespace XRL.World.Parts
{
	/// <summary>
	/// This is a visual part added to any creature that <see cref="Ava_ShoppingList_ShoppingListPart"/> flags as having items from the configured shopping list.
	/// It is automatically removed if all objects in the list <see cref="CachedObjects"/> (populated on creation) are null or not present in the inventory anymore.
	/// </summary>
	[Serializable]
	public class Ava_ShoppingList_Highlighter : IPart
	{
		public override void Register(GameObject Object)
		{
			Object.RegisterPartEvent(this, "EncumbranceChanged");
			base.Register(Object);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "EncumbranceChanged")
				ShouldUpdateObjectList = true;
			return base.FireEvent(E);
		}

		public override bool Render(RenderEvent E)
		{
			if (ShouldUpdateObjectList)
			{
				//MessageQueue.AddPlayerMessage($"Updating highlight part for {ParentObject.DisplayName}");
				foreach (GameObject go in CachedObjects.ToArray())
					if (go == null || !ParentObject.Inventory.HasObject(go))
						CachedObjects.Remove(go);
				if (CachedObjects.Count == 0)
				{
					//MessageQueue.AddPlayerMessage($"No more cached objects remaining. Removing.");
					ParentObject.RemovePart(this);
					return base.Render(E);
				}
				//MessageQueue.AddPlayerMessage($"Continuing with highlight");
				ShouldUpdateObjectList = false;
			}
			if (XRLCore.CurrentFrame % 60 <= 5)
			{
				if (!flipped)
				{
					flipColor = !flipColor;
					flipped = true;
				}
			}
			else
				flipped = false;
			E.ApplyColors(flipColor ? "&m" : "&M", 81);
			return base.Render(E);
		}

		private bool flipColor = false;
		private bool flipped = false;

		/// <summary>
		/// If this is <c>true</c> when the game renders a frame, then it will search the parent object's inventory for jade and save the result to <see cref="hasJade"/>,
		/// then set itself to <c>false</c>.
		/// <br/><br/>
		/// This is set to <c>true</c> whenever an event of ID <c>"EncumbranceChanged"</c> is fired on the parent object.
		/// </summary>
		private bool ShouldUpdateObjectList = true;

		/// <summary>
		/// Whether or not the parent object's inventory has at least one item with <c>jade</c> in its name.
		/// </summary>
		internal List<GameObject> CachedObjects = new List<GameObject>();
	}
}
