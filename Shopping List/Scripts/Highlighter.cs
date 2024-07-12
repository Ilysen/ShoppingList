using System;
using System.Collections.Generic;
using XRL.Core;
using XRL.UI;

namespace XRL.World.Parts
{
	/// <summary>
	/// This is a visual part added to any creature that <see cref="Ceres_ShoppingList_ShoppingListPart"/> flags as having items from the configured shopping list.
	/// It is automatically removed if all objects in the list <see cref="CachedObjects"/> (populated on creation) are null or not present in the inventory anymore.
	/// </summary>
	[Serializable]
	public class Ceres_ShoppingList_Highlighter : IScribedPart
	{
		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Object.RegisterPartEvent(this, "EncumbranceChanged");
			base.Register(Object, Registrar);
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
				foreach (GameObject go in CachedObjects)
					if (go == null || !ParentObject.Inventory.HasObject(go))
						CachedObjects.Remove(go);
				if (CachedObjects.Count == 0)
				{
					ParentObject.RemovePart(this);
					return base.Render(E);
				}
				ShouldUpdateObjectList = false;
			}
			if (XRLCore.CurrentFrame % 60 <= 5)
			{
				if (!_flipped)
				{
					_flipColor = !_flipColor;
					_flipped = true;
				}
			}
			else
				_flipped = false;
			E.ApplyColors(_flipColor ? $"&{CachedHighlightColor.ToLower()}" : $"&{CachedHighlightColor}", 81);
			return base.Render(E);
		}

		/// <summary>
		/// Used for animating the highlighter's flashing. If <c>true</c>, the lowercase form of <see cref="CachedHighlightColor"/> will be used,
		/// instead of using it as-is.
		/// </summary>
		private bool _flipColor = false;
		/// <summary>
		/// Used for animating the highlighter's flashing. When <see cref="_flipColor"/> is changed, this value becomes <c>true</c>; and the next time
		/// it would be changed, this value is set to <c>false</c> instead, and the cycle continues.
		/// This effectively slows down the animation to happen half as fast as it normally could if we were just checking the frame interval.
		/// </summary>
		private bool _flipped = false;

		/// <summary>
		/// Auto-getter for <see cref="_cachedHighlightColor"/>. This refreshes on object load, and is cached as a micro-optimization.
		/// </summary>
		private string CachedHighlightColor
		{
			get
			{
				if (_cachedHighlightColor != null)
					return _cachedHighlightColor;
				string newColor = Options.GetOption("Ceres_ShoppingList_HighlightColor");
				// All of the other colors -- red, blue, green, etc -- all happen to start with the character that designates their color code
				// Yellow, however, does not; the code for yellow is W, so we have to set it manually here instead of just fetching it quickly
				if (newColor.EqualsNoCase("Yellow"))
					newColor = "W";
				else
					newColor = newColor[0].ToString();
				_cachedHighlightColor = newColor;
				return newColor;
			}
		}

		/// <summary>
		/// The code for the color that this actor will be highlighted in, if applicable.
		/// <br/><br/>
		/// <b>This should never be used on its own</b> -- instead, use <see cref="CachedHighlightColor"/>.
		/// </summary>
		private string _cachedHighlightColor;

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
