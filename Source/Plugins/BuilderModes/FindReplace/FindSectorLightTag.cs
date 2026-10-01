#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	// styd: searches/replaces the colored lights of Doom 64 sectors by their tag (the Tag field of
	// each light in the Edit Sector window, the one that light effects and macros look for; not the
	// tag of the sector, which "Sector Tags" covers).
	//
	// Replacing changes the tag only and keeps the color of every light that was found.
	[FindReplace("Sector Light Tag", BrowseButton = false)]
	internal class FindSectorLightTag : BaseFindSectorLight
	{
		#region ================== Methods

		// This is called to perform a search (and replace)
		// Returns a list of items to show in the results list
		// replacewith is null when not replacing
		public override FindReplaceObject[] Find(string value, bool withinselection, bool replace, string replacewith, bool keepselection)
		{
			List<FindReplaceObject> objs = new List<FindReplaceObject>();

			// Interpret the replacement, with the same bounds as for a sector tag
			int replacetag = 0;
			if(replace)
			{
				if(!int.TryParse(replacewith, NumberStyles.Integer, CultureInfo.InvariantCulture, out replacetag)
					|| (replacetag < General.Map.FormatInterface.MinTag) || (replacetag > General.Map.FormatInterface.MaxTag)
					|| (replacetag < 0) || (replacetag > UInt16.MaxValue))
				{
					MessageBox.Show("Invalid replace value for this search type!", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return objs.ToArray();
				}
			}

			// Interpret the number given. A tag that cannot exist simply finds nothing.
			int searchtag;
			if(!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out searchtag)) return objs.ToArray();

			// Go for all sectors, all five lights
			foreach(Sector s in GetSectors(withinselection))
			{
				for(int i = 0; i < NUM_LIGHTS; i++)
				{
					// Tag matches?
					Lights light = GetLight(s, i);
					if(light.tag == searchtag)
					{
						// Replace. The light is no longer the entry of the LIGHTS lump it came from.
						if(replace) SetLight(s, i, new Lights(light.color.r, light.color.g, light.color.b, (UInt16)replacetag));
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
