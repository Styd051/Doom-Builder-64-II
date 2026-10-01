#region ================== Namespaces

using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	// styd: searches/replaces the colored lights of Doom 64 sectors by their LIGHTS index.
	//
	// The value is the index as the Edit Sector window shows it (Lights.GetDisplayIndex):
	// 0 to 255 is a plain gray level, 256 and up is entry "index - 256" of the LIGHTS lump.
	//
	// Replacing never makes up a color: it copies the light (color, tag and index) from a sector
	// that already uses the target index. When no sector uses that index yet, replacing is
	// refused: make that light in the Edit Sector window first.
	[FindReplace("Sector Light Index", BrowseButton = false)]
	internal class FindSectorLightIndex : BaseFindSectorLight
	{
		#region ================== Methods

		// This finds the first light in the map (any sector, any of the five lights) that
		// has the given index. Returns false when there is none.
		private static bool FindLightByIndex(string index, out Lights result)
		{
			foreach(Sector s in General.Map.Map.Sectors)
			{
				for(int i = 0; i < NUM_LIGHTS; i++)
				{
					Lights light = GetLight(s, i);
					if(Lights.GetDisplayIndex(light) == index)
					{
						result = light;
						return true;
					}
				}
			}

			result = new Lights();
			return false;
		}

		// This is called to perform a search (and replace)
		// Returns a list of items to show in the results list
		// replacewith is null when not replacing
		public override FindReplaceObject[] Find(string value, bool withinselection, bool replace, string replacewith, bool keepselection)
		{
			List<FindReplaceObject> objs = new List<FindReplaceObject>();

			// Interpret the replacement. It must be the index of a light that exists in the map.
			Lights replacelight = new Lights();
			if(replace)
			{
				int replaceindex;
				if(!int.TryParse(replacewith, NumberStyles.Integer, CultureInfo.InvariantCulture, out replaceindex) || (replaceindex < 0)
					|| !FindLightByIndex(replaceindex.ToString(CultureInfo.InvariantCulture), out replacelight))
				{
					MessageBox.Show("Invalid replace value for this search type! A light with that index must already exist somewhere in the map (make it in the Edit Sector window first).", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return objs.ToArray();
				}
			}

			// Interpret the index given
			int searchindex;
			if(!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out searchindex) || (searchindex < 0))
			{
				MessageBox.Show("Invalid search value for this search type! Enter the index as the Edit Sector window shows it (0-255 for a gray level, 256 and up for an entry of the LIGHTS lump).", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return objs.ToArray();
			}
			string search = searchindex.ToString(CultureInfo.InvariantCulture);

			// Go for all sectors, all five lights
			foreach(Sector s in GetSectors(withinselection))
			{
				for(int i = 0; i < NUM_LIGHTS; i++)
				{
					// Index matches?
					if(Lights.GetDisplayIndex(GetLight(s, i)) == search)
					{
						// Replace
						if(replace) SetLight(s, i, replacelight);
						objs.Add(MakeResult(s, i));
					}
				}
			}

			if(replace) UpdateMap();
			return objs.ToArray();
		}

		#endregion
	}
}
