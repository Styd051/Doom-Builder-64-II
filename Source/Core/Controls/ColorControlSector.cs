
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;

#endregion

namespace CodeImp.DoomBuilder.Controls
{
	// Doom 64. This edits one of the five colored lights of a sector: its LIGHTS index,
	// its color (picked, or typed as a hex value), its tag and its intensity.
	internal partial class ColorControlSector : UserControl
	{
		#region ================== Constants

		private const float LIGHTINCVALUE = 0.235f;
		private const float LIGHTDECVALUE = -0.1825f;

		#endregion

		#region ================== Variables

		// The light as it was when the form opened
		private PixelColor initialcolor;
		private int initialtag;
		private string initialindex;

		// styd: the +/- clicks in the order they were made, and the color they lead to. When the
		// color still is that color, the clicks are replayed on the own color of every sector.
		private readonly List<float> intensitysteps = new List<float>();
		private PixelColor expectedcolor;

		private bool preventchanges;

		#endregion

		#region ================== Properties

		public string Label { get { return label.Text; } set { label.Text = value; } }

		public PixelColor Color
		{
			get { return PixelColor.FromColor(panel.BackColor); }
			set
			{
				panel.BackColor = System.Drawing.Color.FromArgb(255, value.r, value.g, value.b);
				preventchanges = true;
				hexbox.Text = Color.ToHex();
				preventchanges = false;
			}
		}

		#endregion

		#region ================== Constructor

		// Constructor
		public ColorControlSector()
		{
			// Initialize
			InitializeComponent();
		}

		#endregion

		#region ================== Methods

		// This shows a light
		public void Setup(Lights light)
		{
			initialcolor = light.color;
			initialtag = light.tag;
			initialindex = Lights.GetDisplayIndex(light);
			intensitysteps.Clear();
			expectedcolor = initialcolor;

			this.Color = light.color;
			tagbox.Text = light.tag.ToString(CultureInfo.InvariantCulture);
			indexbox.Text = initialindex;
		}

		// styd: this applies what was changed here to the light of a sector, and tells if anything was
		// changed. A light that was not touched is left exactly as it is, in every selected sector.
		public bool Apply(ref Lights light)
		{
			int newtag = tagbox.GetResult(initialtag);
			string newindex = indexbox.Text.Trim();
			bool tagchanged = (newtag != initialtag);
			bool colorchanged = (this.Color.ToInt() != initialcolor.ToInt());
			bool indexchanged = (newindex != initialindex);
			if(!colorchanged && !tagchanged && !indexchanged) return false;

			if((intensitysteps.Count > 0) && (this.Color.ToInt() == expectedcolor.ToInt()))
			{
				// Only the +/- buttons were used: replay the clicks on the color of this sector
				foreach(float step in intensitysteps) light.SetIntensity(step);
			}
			else if(colorchanged)
			{
				// An explicit color (picked, typed, or adjusted after the +/- clicks)
				light.color = this.Color;
			}

			if(tagchanged)
			{
				light.tag = (UInt16)General.Clamp(newtag, General.Map.FormatInterface.MinTag, General.Map.FormatInterface.MaxTag);
				if(light.tag != 0) light.isDirect = false;
			}

			// styd: manual LIGHTS index editing, like the "Light NNN" field of DEX Editor. An index of
			// 256 and up asks for that entry of the LIGHTS lump; it is only really shared with another
			// sector when the color and tag match as well (see Doom64MapSetIO.AddLightGetIndex).
			if(indexchanged)
			{
				int index;
				if(newindex.Length == 0)
				{
					// No specific entry wanted anymore
					light.hasOriginalIndex = false;
				}
				else if(int.TryParse(newindex, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
				{
					if(index >= Lights.FIRST_LIGHTS_INDEX)
					{
						light.hasOriginalIndex = true;
						light.originalIndex = index - Lights.FIRST_LIGHTS_INDEX;
					}
					else
					{
						// Below 256 the index is the gray level itself
						byte gray = (byte)General.Clamp(index, 0, 255);
						light.color = new PixelColor(255, gray, gray, gray);
						light.tag = 0;
						light.isDirect = true;
						light.hasOriginalIndex = false;
					}
				}
			}

			return true;
		}

		// This makes the color brighter or darker
		private void ChangeIntensity(float step)
		{
			Lights light = new Lights(this.Color.r, this.Color.g, this.Color.b, 0);
			light.SetIntensity(step);
			this.Color = light.color;
			intensitysteps.Add(step);
			expectedcolor = light.color;
		}

		#endregion

		#region ================== Events

		// Color clicked
		private void panel_Click(object sender, EventArgs e)
		{
			// Show color dialog
			dialog.Color = panel.BackColor;
			if(dialog.ShowDialog(this.ParentForm) == DialogResult.OK)
				this.Color = PixelColor.FromColor(dialog.Color);
		}

		// Hex value typed (iori84)
		private void hexbox_TextChanged(object sender, EventArgs e)
		{
			if(preventchanges) return;

			// Force uppercase, strip leading # and anything after 6 digits
			string text = hexbox.Text.Replace("#", "").ToUpperInvariant();
			if(text.Length > 6) text = text.Substring(0, 6);
			if(text != hexbox.Text)
			{
				preventchanges = true;
				hexbox.Text = text;
				hexbox.SelectionStart = text.Length;
				preventchanges = false;
			}

			// A complete value sets the color
			int value;
			if((text.Length == 6) && int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value))
				panel.BackColor = System.Drawing.Color.FromArgb(255, (value >> 16) & 255, (value >> 8) & 255, value & 255);
		}

		// Done typing: show the color that is in effect
		private void hexbox_Leave(object sender, EventArgs e)
		{
			preventchanges = true;
			hexbox.Text = this.Color.ToHex();
			preventchanges = false;
		}

		// styd: this finds a new (unused) tag
		private void newtag_Click(object sender, EventArgs e)
		{
			tagbox.Text = General.Map.Map.GetNewTag().ToString(CultureInfo.InvariantCulture);
		}

		private void brighter_Click(object sender, EventArgs e)
		{
			ChangeIntensity(LIGHTINCVALUE);
		}

		private void darker_Click(object sender, EventArgs e)
		{
			ChangeIntensity(LIGHTDECVALUE);
		}

		#endregion
	}
}
