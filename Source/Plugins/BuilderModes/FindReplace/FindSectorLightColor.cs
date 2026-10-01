#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	// styd: searches/replaces the colored lights of Doom 64 sectors by their color (not by their
	// index). "Sector Light Index" finds which entry of the LIGHTS lump is used, this one finds
	// which appearance is used, whatever the index or the tag behind it: two entries of the LIGHTS
	// lump with other tags can have the very same color, and only this search finds them both.
	//
	// The value is a 6-digit hex color "RRGGBB" (a leading # is allowed), the same as in the
	// Edit Sector window. The browse button opens a color dialog.
	//
	// Replacing keeps the tag that every light has.
	[FindReplace("Sector Light Color", BrowseButton = true)]
	internal class FindSectorLightColor : BaseFindSectorLight
	{
		#region ================== Properties

		public override Image BrowseImage { get { return Properties.Resources.ColorPick; } }

		#endregion

		#region ================== Methods

		// This reads a hex color. Returns false (rather than throwing) on anything
		// else, so that a clean message can be shown.
		private static bool TryParseHexColor(string value, out PixelColor color)
		{
			string trimmed = (value ?? string.Empty).Trim().TrimStart('#');
			if(trimmed.Length == 6)
			{
				try
				{
					color = PixelColor.FromHex(trimmed);
					return true;
				}
				catch(FormatException) { }
			}

			color = new PixelColor();
			return false;
		}

		// This is called when the browse button is pressed
		public override string Browse(string initialvalue)
		{
			PixelColor initial;
			if(!TryParseHexColor(initialvalue, out initial)) initial = new PixelColor(255, 128, 128, 128);

			ColorDialog dialog = new ColorDialog();
			dialog.AllowFullOpen = true;
			dialog.AnyColor = true;
			dialog.FullOpen = true;
			dialog.Color = Color.FromArgb(initial.r, initial.g, initial.b);
			if(dialog.ShowDialog(BuilderPlug.Me.FindReplaceForm) == DialogResult.OK)
				return PixelColor.FromColor(dialog.Color).ToHex();

			return initialvalue;
		}

		// This is called to perform a search (and replace)
		// Returns a list of items to show in the results list
		// replacewith is null when not replacing
		public override FindReplaceObject[] Find(string value, bool withinselection, bool replace, string replacewith, bool keepselection)
		{
			List<FindReplaceObject> objs = new List<FindReplaceObject>();

			// Interpret the replacement
			PixelColor replacecolor = new PixelColor();
			if(replace && !TryParseHexColor(replacewith, out replacecolor))
			{
				MessageBox.Show("Invalid replace value for this search type! Enter a 6-digit hex color (e.g. FF8000), or use the browse button to pick one.", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return objs.ToArray();
			}

			// Interpret the color given
			PixelColor searchcolor;
			if(!TryParseHexColor(value, out searchcolor))
			{
				MessageBox.Show("Invalid search value for this search type! Enter a 6-digit hex color (e.g. FF8000), or use the browse button to pick one.", "Find and Replace", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return objs.ToArray();
			}

			// Go for all sectors, all five lights
			foreach(Sector s in GetSectors(withinselection))
			{
				for(int i = 0; i < NUM_LIGHTS; i++)
				{
					// Color matches? The tag and the index do not matter here.
					Lights light = GetLight(s, i);
					if((light.color.r == searchcolor.r) && (light.color.g == searchcolor.g) && (light.color.b == searchcolor.b))
					{
						// Replace
						if(replace) SetLight(s, i, new Lights(replacecolor.r, replacecolor.g, replacecolor.b, light.tag));
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
