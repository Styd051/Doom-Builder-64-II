#region ================== Namespaces

using System.Collections.Generic;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	// styd: the find types for the colored lights of Doom 64 sectors. A sector has five lights
	// (floor, ceiling, thing, upper wall, lower wall) and all of them are searched in one pass,
	// just like "Sector Flat" does for the floor and ceiling textures.
	internal abstract class BaseFindSectorLight : BaseFindSector
	{
		#region ================== Constants

		protected const int NUM_LIGHTS = 5;

		private static readonly string[] LIGHT_NAMES = { "Floor Color", "Ceiling Color", "Thing Color", "Upper Wall Color", "Lower Wall Color" };

		#endregion

		#region ================== Methods

		// Only for Doom 64 maps: the lights mean nothing in the other map formats
		public override bool DetermineVisiblity()
		{
			return General.Map.DOOM64;
		}

		// This returns one of the five lights of a sector
		protected static Lights GetLight(Sector s, int index)
		{
			switch(index)
			{
				case 0: return s.FloorColor;
				case 1: return s.CeilColor;
				case 2: return s.ThingColor;
				case 3: return s.TopColor;
				default: return s.LowerColor;
			}
		}

		// This changes one of the five lights of a sector
		protected static void SetLight(Sector s, int index, Lights light)
		{
			switch(index)
			{
				case 0: s.FloorColor = light; break;
				case 1: s.CeilColor = light; break;
				case 2: s.ThingColor = light; break;
				case 3: s.TopColor = light; break;
				default: s.LowerColor = light; break;
			}
		}

		// This makes the item to show in the results list for a light of a sector
		protected static FindReplaceObject MakeResult(Sector s, int index)
		{
			return new FindReplaceObject(s, "Sector " + s.Index + " " + LIGHT_NAMES[index]);
		}

		// Where to search
		protected static ICollection<Sector> GetSectors(bool withinselection)
		{
			return (withinselection ? General.Map.Map.GetSelectedSectors(true) : General.Map.Map.Sectors);
		}

		// This must be done after lights were replaced: the sectors are drawn with these colors
		protected static void UpdateMap()
		{
			General.Map.Map.Update();
			General.Map.IsChanged = true;
		}

		#endregion
	}
}
