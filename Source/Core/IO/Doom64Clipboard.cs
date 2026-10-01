
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
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Types;

#endregion

namespace CodeImp.DoomBuilder.IO
{
	// Doom 64 copy/paste and prefabs.
	// The clipboard and prefab streams are made for UDMF: flags are translated to UDMF fields on copy
	// and back on paste, and that loses everything UDMF has no name for (most Doom 64 linedef flags,
	// the activation type, the switch setup, the sector flags and the colored lights).
	// These streams do carry the custom fields of every element, so the raw Doom 64 data is put in
	// custom fields of the copied elements and taken out again when they are pasted. The UDMF
	// translation still happens, so that pasting to or from another map format keeps what both have.
	internal static class Doom64Clipboard
	{
		#region ================== Constants

		private const string PREFIX = "doom64_";
		private const string FLAGS = PREFIX + "flags";
		private const string ACTIVATE = PREFIX + "activate";
		private const string SWITCHMASK = PREFIX + "switchmask";
		private const string COLOR = PREFIX + "color";				// + 1..5: floor, ceiling, thing, upper wall, lower wall
		private const string COLORTAG = PREFIX + "colortag";		// + 1..5
		private const string COLORINDEX = PREFIX + "colorindex";	// + 1..5

		// How the LIGHTS index of a color is stored: the index itself, or one of these
		private const int INDEX_NONE = -1;
		private const int INDEX_DIRECT = -2;

		// What Doom Builder 64 wrote in its prefabs and on its clipboard
		private const string LEGACY_ACTIVATE = "activate";
		private const string LEGACY_SWITCHMASK = "switchmask";
		private const string LEGACY_COLOR = "color";				// + 1..5, same order

		private const int NUM_COLORS = 5;

		#endregion

		#region ================== Copy

		// This puts the Doom 64 data of the copied elements in their custom fields.
		// Call this before the flags are translated to UDMF.
		public static void Stash(MapSet copyset)
		{
			foreach(Linedef l in copyset.Linedefs)
			{
				SetValue(l.Fields, FLAGS, unchecked((int)Doom64MapSetIO.MakeFlags(l.Flags)));
				SetValue(l.Fields, ACTIVATE, l.Activate);
				SetValue(l.Fields, SWITCHMASK, l.SwitchMask);
			}

			foreach(Sector s in copyset.Sectors)
			{
				SetValue(s.Fields, FLAGS, unchecked((int)Doom64MapSetIO.MakeFlags(s.Flags)));

				Lights[] lights = GetLights(s);
				for(int i = 0; i < NUM_COLORS; i++)
				{
					string number = (i + 1).ToString();
					SetValue(s.Fields, COLOR + number, lights[i].color.ToInt());
					SetValue(s.Fields, COLORTAG + number, lights[i].tag);
					SetValue(s.Fields, COLORINDEX + number, lights[i].hasOriginalIndex ? lights[i].originalIndex : (lights[i].isDirect ? INDEX_DIRECT : INDEX_NONE));
				}
			}

			foreach(Thing t in copyset.Things)
			{
				SetValue(t.Fields, FLAGS, unchecked((int)Doom64MapSetIO.MakeFlags(t.Flags)));
			}
		}

		#endregion

		#region ================== Paste

		// This sets the pasted (marked) elements up for the Doom 64 map format. Elements that were
		// copied from a Doom 64 map get exactly what they had, anything else is translated from UDMF.
		public static void Restore(MapSet map)
		{
			foreach(Linedef l in map.Linedefs) if(l.Marked) Restore(l);
			foreach(Sidedef sd in map.Sidedefs) if(sd.Marked) sd.TranslateFromUDMF();
			foreach(Sector s in map.Sectors) if(s.Marked) Restore(s);
			foreach(Thing t in map.Things) if(t.Marked) Restore(t);
		}

		private static void Restore(Linedef l)
		{
			bool found = l.Fields.ContainsKey(FLAGS);
			uint flags = unchecked((uint)GetValue(l.Fields, FLAGS, 0));
			int activate = GetValue(l.Fields, ACTIVATE, 0);
			int switchmask = GetValue(l.Fields, SWITCHMASK, 0);

			// From Doom Builder 64? It wrote the flags by their number.
			if(!found)
			{
				flags = Doom64MapSetIO.MakeFlags(l.Flags);
				if((flags != 0) || l.Fields.ContainsKey(LEGACY_ACTIVATE) || l.Fields.ContainsKey(LEGACY_SWITCHMASK))
				{
					found = true;
					activate = GetValue(l.Fields, LEGACY_ACTIVATE, 0);
					switchmask = GetValue(l.Fields, LEGACY_SWITCHMASK, 0);
				}
			}

			// This removes all fields and makes the flags that UDMF can tell
			l.TranslateFromUDMF();

			if(found)
			{
				l.ClearFlags();
				foreach(KeyValuePair<string, bool> f in Doom64MapSetIO.MakeStringFlags(flags & ~(uint)Linedef.SWITCH_MASK, General.Map.Config.SortedLinedefFlags))
					l.SetFlag(f.Key, f.Value);
				l.Activate = (activate & ~Doom64MapSetIO.ACTION_MASK);
				l.SwitchMask = (switchmask & Linedef.SWITCH_MASK);
			}
		}

		private static void Restore(Sector s)
		{
			bool found = s.Fields.ContainsKey(FLAGS);
			uint flags = unchecked((uint)GetValue(s.Fields, FLAGS, 0));

			// The colored lights, from this editor or from Doom Builder 64
			bool[] lightfound = new bool[NUM_COLORS];
			Lights[] lights = new Lights[NUM_COLORS];
			for(int i = 0; i < NUM_COLORS; i++)
			{
				string number = (i + 1).ToString();
				if(s.Fields.ContainsKey(COLOR + number))
				{
					PixelColor c = PixelColor.FromInt(GetValue(s.Fields, COLOR + number, 0));
					int tag = General.Clamp(GetValue(s.Fields, COLORTAG + number, 0), 0, ushort.MaxValue);
					int index = GetValue(s.Fields, COLORINDEX + number, INDEX_NONE);
					lights[i] = new Lights(c.r, c.g, c.b, (ushort)tag);
					lights[i].isDirect = (index == INDEX_DIRECT);
					lights[i].hasOriginalIndex = (index >= 0);
					lights[i].originalIndex = Math.Max(index, -1);
					lightfound[i] = true;
				}
				else if(!found && s.Fields.ContainsKey(LEGACY_COLOR + number))
				{
					PixelColor c = PixelColor.FromInt(GetValue(s.Fields, LEGACY_COLOR + number, 0));
					lights[i] = new Lights(c.r, c.g, c.b, 0);
					lightfound[i] = true;
				}
			}

			// From Doom Builder 64? It wrote the flags by their number.
			if(!found)
			{
				flags = Doom64MapSetIO.MakeFlags(s.Flags);
				found = (flags != 0) || (Array.IndexOf(lightfound, true) > -1);
			}

			// This removes all fields and flags
			s.TranslateFromUDMF();

			if(found)
			{
				foreach(KeyValuePair<string, bool> f in Doom64MapSetIO.MakeStringFlags(flags, General.Map.Config.SectorFlags.Keys))
					s.SetFlag(f.Key, f.Value);

				if(lightfound[0]) s.FloorColor = lights[0];
				if(lightfound[1]) s.CeilColor = lights[1];
				if(lightfound[2]) s.ThingColor = lights[2];
				if(lightfound[3]) s.TopColor = lights[3];
				if(lightfound[4]) s.LowerColor = lights[4];
			}
		}

		private static void Restore(Thing t)
		{
			bool found = t.Fields.ContainsKey(FLAGS);
			uint flags = unchecked((uint)GetValue(t.Fields, FLAGS, 0));

			// From Doom Builder 64? It wrote the flags by their number.
			if(!found)
			{
				flags = Doom64MapSetIO.MakeFlags(t.Flags);
				found = (flags != 0);
			}

			// This removes all fields and makes the flags that UDMF can tell
			t.TranslateFromUDMF();

			if(found)
			{
				t.ClearFlags();
				foreach(KeyValuePair<string, bool> f in Doom64MapSetIO.MakeStringFlags(flags, General.Map.Config.ThingFlags.Keys))
					t.SetFlag(f.Key, f.Value);
			}
		}

		// This removes the Doom 64 data from pasted (marked) elements. A UDMF map
		// keeps custom fields, and these mean nothing there.
		public static void Strip(MapSet map)
		{
			foreach(Linedef l in map.Linedefs) if(l.Marked) Strip(l.Fields);
			foreach(Sector s in map.Sectors) if(s.Marked) Strip(s.Fields);
			foreach(Thing t in map.Things) if(t.Marked) Strip(t.Fields);
		}

		private static void Strip(UniFields fields)
		{
			List<string> keys = new List<string>();
			foreach(string key in fields.Keys) if(key.StartsWith(PREFIX, StringComparison.Ordinal)) keys.Add(key);
			foreach(string key in keys) fields.Remove(key);
		}

		#endregion

		#region ================== Methods

		// The five colored lights of a sector, in the order they are stored here
		private static Lights[] GetLights(Sector s)
		{
			return new[] { s.FloorColor, s.CeilColor, s.ThingColor, s.TopColor, s.LowerColor };
		}

		private static void SetValue(UniFields fields, string key, int value)
		{
			fields[key] = new UniValue(UniversalType.Integer, value);
		}

		private static int GetValue(UniFields fields, string key, int defaultvalue)
		{
			UniValue value;
			if(!fields.TryGetValue(key, out value) || (value.Value == null)) return defaultvalue;

			try { return Convert.ToInt32(value.Value); }
			catch(Exception) { return defaultvalue; }
		}

		#endregion
	}
}
