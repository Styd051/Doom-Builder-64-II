
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
using System.IO;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;

#endregion

namespace CodeImp.DoomBuilder.IO
{
	// villsa. Doom 64 (Doom64 EX) map format
	internal class Doom64MapSetIO : MapSetIO
	{
		#region ================== Constants

		// Lump record sizes
		private const int THING_SIZE = 14;
		private const int VERTEX_SIZE = 8;
		private const int LINEDEF_SIZE = 16;
		private const int SIDEDEF_SIZE = 12;
		private const int SECTOR_SIZE = 24;
		private const int LIGHT_SIZE = 6;

		// The linedef special holds the action in the low 9 bits and the activation type in the rest
		internal const int ACTION_MASK = 511;

		// Linedef flag bits that are kept in Linedef.SwitchMask instead of the flags
		internal const int SWITCH_TEXTURE_UPPER = 0x2000;
		internal const int SWITCH_TEXTURE_LOWER = 0x4000;
		internal const int SWITCH_DISPLAY_UPPER = 0x8000;
		internal const int SWITCH_CHECK_FLOOR_HEIGHT = 0x10000; // styd
		internal const int SWITCH_MASK = SWITCH_TEXTURE_UPPER | SWITCH_TEXTURE_LOWER | SWITCH_DISPLAY_UPPER | SWITCH_CHECK_FLOOR_HEIGHT;

		// The texture that stands for "no texture"
		private const string NO_TEXTURE_LUMP = "?";

		// Textures are stored as a hash of their name. A hash that matches no loaded
		// texture is shown under this kind of name, so that it is kept when saving.
		private const char HASH_NAME_PREFIX = '#';

		#endregion

		#region ================== Variables

		// LIGHTS lump entries collected while saving
		private List<Lights> light;
		private List<int> lightOriginalIndex; // styd: originalIndex of the Lights value that first created each entry in `light` during this save pass, or -1 if it had none. See AddLightGetIndex().
		private Dictionary<Sector, int[]> sectorWrittenIndices; // styd

		#endregion

		#region ================== Constructor / Disposer

		// Constructor
		public Doom64MapSetIO(WAD wad, MapManager manager) : base(wad, manager)
		{
		}

		#endregion

		#region ================== Properties

		public override int MaxSidedefs { get { return ushort.MaxValue; } }
		public override int MaxVertices { get { return ushort.MaxValue; } }
		public override int MaxLinedefs { get { return ushort.MaxValue; } }
		public override int MaxSectors { get { return ushort.MaxValue; } }
		public override int MaxThings { get { return int.MaxValue; } }
		public override int MinTextureOffset { get { return short.MinValue; } }
		public override int MaxTextureOffset { get { return short.MaxValue; } }
		public override int VertexDecimals { get { return 6; } } // Enough to keep every 16.16 fixed point value found in the game
		public override string DecimalsFormat { get { return "0.######"; } }
		public override bool HasLinedefTag { get { return true; } }
		public override bool HasThingTag { get { return true; } }
		public override bool HasThingAction { get { return false; } }
		public override bool HasCustomFields { get { return false; } }
		public override bool HasThingHeight { get { return true; } }
		public override bool HasActionArgs { get { return false; } }
		public override bool HasMixedActivations { get { return false; } }
		public override bool HasPresetActivations { get { return false; } }
		public override bool HasBuiltInActivations { get { return false; } }
		public override bool HasNumericLinedefFlags { get { return true; } }
		public override bool HasNumericThingFlags { get { return true; } }
		public override bool HasNumericLinedefActivations { get { return true; } }
		public override int MaxTag { get { return ushort.MaxValue; } }
		public override int MinTag { get { return ushort.MinValue; } }
		public override int MaxAction { get { return ACTION_MASK; } }
		public override int MinAction { get { return ushort.MinValue; } }
		public override int MaxArgument { get { return 0; } }
		public override int MinArgument { get { return 0; } }
		public override int MaxEffect { get { return ushort.MaxValue; } }
		public override int MinEffect { get { return ushort.MinValue; } }
		public override int MaxBrightness { get { return short.MaxValue; } }
		public override int MinBrightness { get { return short.MinValue; } }
		public override int MaxThingType { get { return ushort.MaxValue; } }
		public override int MinThingType { get { return ushort.MinValue; } } // Thing type 0 is the camera
		public override float MaxCoordinate { get { return short.MaxValue; } }
		public override float MinCoordinate { get { return short.MinValue; } }
		public override int MaxThingAngle { get { return short.MaxValue; } }
		public override int MinThingAngle { get { return short.MinValue; } }
		public override Dictionary<MapElementType, Dictionary<string, UniversalType>> UIFields { get { return uifields; } }

		#endregion

		#region ================== Texture names

		// This makes the hash that the game uses to refer to a texture
		internal static uint HashTextureName(string name)
		{
			unchecked
			{
				uint hash = 1315423911;
				for(int i = 0; (i < name.Length) && (name[i] != '\0'); i++)
				{
					hash ^= ((hash << 5) + char.ToUpperInvariant(name[i]) + (hash >> 2));
				}

				return hash % 65536;
			}
		}

		// This makes the name under which a hash is known until (or unless) a texture is found for it
		private static string MakeHashName(uint hash)
		{
			return HASH_NAME_PREFIX + hash.ToString("X4", CultureInfo.InvariantCulture);
		}

		// This gets the hash back from a name made by MakeHashName
		private static bool IsHashName(string name, out uint hash)
		{
			hash = 0;
			return (name.Length == 5) && (name[0] == HASH_NAME_PREFIX)
				&& uint.TryParse(name.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out hash);
		}

		// This returns the hash to write for a texture name
		private static ushort GetTextureHash(string name)
		{
			uint hash;
			if(IsHashName(name, out hash)) return (ushort)hash;
			if(name == "-") name = NO_TEXTURE_LUMP;
			return (ushort)HashTextureName(name);
		}

		// The map can only tell the hashes of its textures. This gives them their names, which requires
		// the resources to be loaded. Hashes for which no texture is loaded keep their hash name.
		internal static void ResolveTextureNames(MapSet map, DataManager data)
		{
			// Make the lookup table. The first texture with a hash wins, same as in the game.
			Dictionary<uint, string> names = new Dictionary<uint, string>();
			foreach(DataReader dr in data.Containers)
			{
				WADReader wr = dr as WADReader;
				if(wr == null) continue;

				foreach(string name in wr.GetTextureRangeNames())
				{
					uint namehash = HashTextureName(name);
					if(!names.ContainsKey(namehash)) names.Add(namehash, name);
				}
			}

			uint notexturehash = HashTextureName(NO_TEXTURE_LUMP);
			uint hash;
			string texture;

			foreach(Sector s in map.Sectors)
			{
				if(IsHashName(s.FloorTexture, out hash) && names.TryGetValue(hash, out texture)) s.SetFloorTexture(texture);
				if(IsHashName(s.CeilTexture, out hash) && names.TryGetValue(hash, out texture)) s.SetCeilTexture(texture);
			}

			foreach(Sidedef sd in map.Sidedefs)
			{
				if(IsHashName(sd.HighTexture, out hash))
				{
					if(hash == notexturehash) sd.SetTextureHigh("-");
					else if(names.TryGetValue(hash, out texture)) sd.SetTextureHigh(texture);
				}

				if(IsHashName(sd.MiddleTexture, out hash))
				{
					if(hash == notexturehash) sd.SetTextureMid("-");
					else if(names.TryGetValue(hash, out texture)) sd.SetTextureMid(texture);
				}

				if(IsHashName(sd.LowTexture, out hash))
				{
					if(hash == notexturehash) sd.SetTextureLow("-");
					else if(names.TryGetValue(hash, out texture)) sd.SetTextureLow(texture);
				}
			}
		}

		#endregion

		#region ================== Flags

		// This makes string flags from a bit field. Bits that the game configuration
		// does not know are added as well, so that they are kept when saving.
		private static Dictionary<string, bool> MakeStringFlags(uint flags, IEnumerable<string> knownflags)
		{
			Dictionary<string, bool> stringflags = new Dictionary<string, bool>(StringComparer.Ordinal);
			uint known = 0;

			foreach(string f in knownflags)
			{
				uint fnum;
				if(uint.TryParse(f, NumberStyles.Integer, CultureInfo.InvariantCulture, out fnum))
				{
					stringflags[f] = ((flags & fnum) == fnum);
					known |= fnum;
				}
			}

			uint unknown = (flags & ~known);
			for(int bit = 0; (unknown != 0) && (bit < 32); bit++)
			{
				uint fnum = (1u << bit);
				if((unknown & fnum) != 0)
				{
					stringflags[fnum.ToString(CultureInfo.InvariantCulture)] = true;
					unknown &= ~fnum;
				}
			}

			return stringflags;
		}

		// This makes a bit field from string flags
		private static uint MakeFlags(Dictionary<string, bool> stringflags)
		{
			uint flags = 0;
			foreach(KeyValuePair<string, bool> f in stringflags)
			{
				uint fnum;
				if(f.Value && uint.TryParse(f.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out fnum)) flags |= fnum;
			}

			return flags;
		}

		#endregion

		#region ================== Light Functions

		// This returns the light for a sector color index
		private static Lights GetLight(int cindex, Lights[] lights, int sectorindex)
		{
			Lights color;

			if(cindex >= Lights.FIRST_LIGHTS_INDEX)
			{
				cindex -= Lights.FIRST_LIGHTS_INDEX;
				if(cindex < lights.Length)
				{
					color = lights[cindex];
					color.isDirect = false; // styd: true LIGHTS input, to be preserved as such
				}
				else
				{
					General.ErrorLogger.Add(ErrorType.Warning, "Sector " + sectorindex + " references invalid light " + (cindex + Lights.FIRST_LIGHTS_INDEX) + ". The light has been reset.");
					color = new Lights(0, 0, 0, 0);
				}
			}
			else
			{
				byte c = (byte)cindex;
				color = new Lights(c, c, c, 0);
				color.isDirect = true; // styd: grayscale shortcut
			}

			color.color.a = 255;
			return color;
		}

		// styd: each color slot gets its own LIGHTS entry, unless it is explicitly linked to another one
		private int AddLightGetIndex(Lights l)
		{
			if(l.isDirect && l.color.r == l.color.g && l.color.r == l.color.b && l.tag == 0)
				return l.color.r;

			// styd: if this color has a known physical LIGHTS-lump position - either
			// loaded from an existing WAD, or requested manually via the "Index" field in
			// Edit Sector > Lights (matching DEX Editor's "Light NNN" field) - and another
			// entry already written during this save pass came from that EXACT same
			// position, reuse it. This restores/creates exact physical sharing between
			// sectors regardless of their Sector.Tag - confirmed necessary on map23 and
			// map29, where Sector_CopyLightsAndInterpolate targets several
			// differently-tagged sectors that need to share one LIGHTS entry so that
			// P_UpdateLightThinker's in-place mutation of that slot (p_lights.c) is visible
			// on all of them at once. It only merges entries that share a VERIFIED or
			// EXPLICITLY REQUESTED position, never a coincidence.
			if(l.hasOriginalIndex)
			{
				for(int i = 0; i < light.Count; i++)
				{
					if(lightOriginalIndex[i] == l.originalIndex &&
						light[i].color.r == l.color.r &&
						light[i].color.g == l.color.g &&
						light[i].color.b == l.color.b &&
						light[i].tag == l.tag)
					{
						return Lights.FIRST_LIGHTS_INDEX + i;
					}
				}
			}

			// styd: deduplicate identical tagged inputs (RGB+tag) unconditionally - safe regardless
			// of which sector owns them, since P_ChangeLightByTag/SetLightID always walks every entry
			// matching a given light-tag (P_FindLightFromLightTag), so sharing a physical index between
			// multiple sectors carrying the same light-tag is the correct, intended behavior.
			if(l.tag != 0)
			{
				for(int i = 0; i < light.Count; i++)
				{
					Lights existing = light[i];
					if(existing.tag == l.tag &&
						existing.color.r == l.color.r &&
						existing.color.g == l.color.g &&
						existing.color.b == l.color.b)
					{
						return Lights.FIRST_LIGHTS_INDEX + i;
					}
				}
			}

			// styd: coincidentally-identical untagged colors of different sectors are deliberately
			// NOT merged. The original compiler (DEX/blam) leaves them as separate entries, and a
			// re-saved, untouched map has to match the original LIGHTS lump byte-for-byte.
			int index = light.Count;
			light.Add(l);
			lightOriginalIndex.Add(l.hasOriginalIndex ? l.originalIndex : -1);
			return Lights.FIRST_LIGHTS_INDEX + index;
		}

		#endregion

		#region ================== Reading

		// This reads a map from the file and returns a MapSet
		public override MapSet Read(MapSet map, string mapname)
		{
			// Find the index where first map lump begins
			int firstindex = wad.FindLumpIndex(mapname) + 1;

			// Read vertices
			Dictionary<int, Vertex> vertexlink = ReadVertices(map, firstindex);

			// Read sectors
			Dictionary<int, Sector> sectorlink = ReadSectors(map, firstindex);

			// Read linedefs and sidedefs
			ReadLinedefs(map, firstindex, vertexlink, sectorlink);

			// Read things
			ReadThings(map, firstindex);

			// Remove unused vertices
			map.RemoveUnusedVertices();

			// The resources may already be loaded (when switching to another map in the same file).
			// Otherwise the texture names are resolved when loading the resources is done.
			if((manager != null) && (manager.Data != null) && !manager.Data.IsDisposed)
				ResolveTextureNames(map, manager.Data);

			// Return result;
			return map;
		}

		// This reads the THINGS from WAD file
		private void ReadThings(MapSet map, int firstindex)
		{
			// Get the lump from wad file
			Lump lump = wad.FindLump("THINGS", firstindex);
			if(lump == null) throw new Exception("Could not find required lump THINGS!");

			// Prepare to read the items
			MemoryStream mem = new MemoryStream(lump.Stream.ReadAllBytes());
			int num = (int)lump.Stream.Length / THING_SIZE;
			BinaryReader reader = new BinaryReader(mem);

			// Read items from the lump
			map.SetCapacity(0, 0, 0, 0, map.Things.Count + num);
			for(int i = 0; i < num; i++)
			{
				// Read properties from stream
				int x = reader.ReadInt16();
				int y = reader.ReadInt16();
				int z = reader.ReadInt16();
				int angle = reader.ReadInt16();
				int type = reader.ReadUInt16();
				uint flags = reader.ReadUInt16();
				int tag = reader.ReadUInt16();

				// Make string flags
				Dictionary<string, bool> stringflags = MakeStringFlags(flags, manager.Config.ThingFlags.Keys);

				// Create new item
				Thing t = map.CreateThing();
				t.Update(type, x, y, z, angle, 0, 0, 1.0f, 1.0f, stringflags, tag, 0, new int[Thing.NUM_ARGS]);
			}

			// Done
			mem.Dispose();
		}

		// This reads the VERTICES from WAD file
		// Returns a lookup table with indices
		private Dictionary<int, Vertex> ReadVertices(MapSet map, int firstindex)
		{
			// Get the lump from wad file
			Lump lump = wad.FindLump("VERTEXES", firstindex);
			if(lump == null) throw new Exception("Could not find required lump VERTEXES!");

			// Prepare to read the items
			MemoryStream mem = new MemoryStream(lump.Stream.ReadAllBytes());
			int num = (int)lump.Stream.Length / VERTEX_SIZE;
			BinaryReader reader = new BinaryReader(mem);

			// Create lookup table
			Dictionary<int, Vertex> link = new Dictionary<int, Vertex>(num);

			// Read items from the lump
			map.SetCapacity(map.Vertices.Count + num, 0, 0, 0, 0);
			for(int i = 0; i < num; i++)
			{
				// Read properties from stream (16.16 fixed point)
				float x = reader.ReadInt32() / 65536f;
				float y = reader.ReadInt32() / 65536f;

				// Create new item
				Vertex v = map.CreateVertex(new Vector2D(x, y));

				// Add it to the lookup table
				link.Add(i, v);
			}

			// Done
			mem.Dispose();

			// Return lookup table
			return link;
		}

		// This reads the LIGHTS from WAD file
		private Lights[] ReadLights(int firstindex)
		{
			// Get the lump from wad file
			Lump lump = wad.FindLump("LIGHTS", firstindex);
			if(lump == null) throw new Exception("Could not find required lump LIGHTS!");

			// Prepare to read the items
			MemoryStream mem = new MemoryStream(lump.Stream.ReadAllBytes());
			int num = (int)lump.Stream.Length / LIGHT_SIZE;
			BinaryReader reader = new BinaryReader(mem);

			Lights[] lights = new Lights[num];
			for(int i = 0; i < num; i++)
			{
				lights[i].color.r = reader.ReadByte();
				lights[i].color.g = reader.ReadByte();
				lights[i].color.b = reader.ReadByte();
				lights[i].color.a = reader.ReadByte();
				lights[i].tag = reader.ReadUInt16();

				// styd: remember which physical LIGHTS-lump slot this entry came from, so
				// AddLightGetIndex() can restore the exact original sharing on save
				lights[i].originalIndex = i;
				lights[i].hasOriginalIndex = true;
			}

			// Done
			mem.Dispose();
			return lights;
		}

		// This reads the SECTORS from WAD file
		// Returns a lookup table with indices
		private Dictionary<int, Sector> ReadSectors(MapSet map, int firstindex)
		{
			// Get the lump from wad file
			Lump lump = wad.FindLump("SECTORS", firstindex);
			if(lump == null) throw new Exception("Could not find required lump SECTORS!");

			// Get the light table
			Lights[] lights = ReadLights(firstindex);

			// Prepare to read the items
			MemoryStream mem = new MemoryStream(lump.Stream.ReadAllBytes());
			int num = (int)lump.Stream.Length / SECTOR_SIZE;
			BinaryReader reader = new BinaryReader(mem);
			int[] colors = new int[Sector.NUM_COLORS];

			// Create lookup table
			Dictionary<int, Sector> link = new Dictionary<int, Sector>(num);

			// Read items from the lump
			map.SetCapacity(0, 0, 0, map.Sectors.Count + num, 0);
			for(int i = 0; i < num; i++)
			{
				// Read properties from stream
				int hfloor = reader.ReadInt16();
				int hceil = reader.ReadInt16();
				uint tfloor = reader.ReadUInt16();
				uint tceil = reader.ReadUInt16();
				for(int c = 0; c < Sector.NUM_COLORS; c++) colors[c] = reader.ReadUInt16();
				int special = reader.ReadUInt16();
				int tag = reader.ReadUInt16();
				uint flags = reader.ReadUInt16();

				// Make string flags
				Dictionary<string, bool> stringflags = MakeStringFlags(flags, manager.Config.SectorFlags.Keys);

				// Create new item. Doom 64 has no sector brightness, the colors are all there is.
				Sector s = map.CreateSector();
				s.Update(hfloor, hceil, MakeHashName(tfloor), MakeHashName(tceil), special, stringflags, new List<int> { tag }, 255, 0, new Vector3D(), 0, new Vector3D());
				s.FloorColor = GetLight(colors[0], lights, i);
				s.CeilColor = GetLight(colors[1], lights, i);
				s.ThingColor = GetLight(colors[2], lights, i);
				s.TopColor = GetLight(colors[3], lights, i);
				s.LowerColor = GetLight(colors[4], lights, i);

				// Add it to the lookup table
				link.Add(i, s);
			}

			// Done
			mem.Dispose();

			// Return lookup table
			return link;
		}

		// This reads the LINEDEFS and SIDEDEFS from WAD file
		private void ReadLinedefs(MapSet map, int firstindex,
			Dictionary<int, Vertex> vertexlink, Dictionary<int, Sector> sectorlink)
		{
			// Get the linedefs lump from wad file
			Lump linedefslump = wad.FindLump("LINEDEFS", firstindex);
			if(linedefslump == null) throw new Exception("Could not find required lump LINEDEFS!");

			// Get the sidedefs lump from wad file
			Lump sidedefslump = wad.FindLump("SIDEDEFS", firstindex);
			if(sidedefslump == null) throw new Exception("Could not find required lump SIDEDEFS!");

			// Prepare to read the items
			MemoryStream linedefsmem = new MemoryStream(linedefslump.Stream.ReadAllBytes());
			MemoryStream sidedefsmem = new MemoryStream(sidedefslump.Stream.ReadAllBytes());
			int num = (int)linedefslump.Stream.Length / LINEDEF_SIZE;
			int numsides = (int)sidedefslump.Stream.Length / SIDEDEF_SIZE;
			BinaryReader readline = new BinaryReader(linedefsmem);
			BinaryReader readside = new BinaryReader(sidedefsmem);

			// Read items from the lump
			map.SetCapacity(0, map.Linedefs.Count + num, map.Sidedefs.Count + numsides, 0, 0);
			for(int i = 0; i < num; i++)
			{
				// Read properties from stream
				int v1 = readline.ReadUInt16();
				int v2 = readline.ReadUInt16();
				uint flags = readline.ReadUInt32();
				int special = readline.ReadUInt16();
				int tag = readline.ReadUInt16();
				int s1 = readline.ReadUInt16();
				int s2 = readline.ReadUInt16();

				// The switch setup bits are not flags
				int switchmask = (int)(flags & SWITCH_MASK);

				// Make string flags
				Dictionary<string, bool> stringflags = MakeStringFlags(flags & ~(uint)SWITCH_MASK, manager.Config.SortedLinedefFlags);
				foreach(string f in manager.Config.SortedLinedefFlags)
				{
					// A switch bit that the game configuration lists as a flag is not one
					uint fnum;
					if(uint.TryParse(f, NumberStyles.Integer, CultureInfo.InvariantCulture, out fnum) && ((fnum & SWITCH_MASK) != 0)) stringflags.Remove(f);
				}

				// Create new linedef
				if(vertexlink.ContainsKey(v1) && vertexlink.ContainsKey(v2))
				{
					// Check if not zero-length
					if(Vector2D.ManhattanDistance(vertexlink[v1].Position, vertexlink[v2].Position) > 0.0001f)
					{
						Linedef l = map.CreateLinedef(vertexlink[v1], vertexlink[v2]);
						l.Update(stringflags, (special & ~ACTION_MASK), new List<int> { tag }, (special & ACTION_MASK), new int[Linedef.NUM_ARGS]);
						l.SwitchMask = switchmask;
						l.UpdateCache();

						// Line has a front side?
						if(s1 != ushort.MaxValue) ReadSidedef(map, readside, sidedefsmem, l, true, s1, i, sectorlink);

						// Line has a back side?
						if(s2 != ushort.MaxValue) ReadSidedef(map, readside, sidedefsmem, l, false, s2, i, sectorlink);
					}
					else
					{
						General.ErrorLogger.Add(ErrorType.Warning, "Linedef " + i + " is zero-length. Linedef has been removed.");
					}
				}
				else
				{
					General.ErrorLogger.Add(ErrorType.Warning, "Linedef " + i + " references one or more invalid vertices. Linedef has been removed.");
				}
			}

			// Done
			linedefsmem.Dispose();
			sidedefsmem.Dispose();
		}

		// This reads a single sidedef
		private static void ReadSidedef(MapSet map, BinaryReader readside, MemoryStream sidedefsmem, Linedef l, bool front,
			int index, int linedefindex, Dictionary<int, Sector> sectorlink)
		{
			if((index * (long)SIDEDEF_SIZE) <= (sidedefsmem.Length - SIDEDEF_SIZE))
			{
				sidedefsmem.Seek(index * SIDEDEF_SIZE, SeekOrigin.Begin);
				int offsetx = readside.ReadInt16();
				int offsety = readside.ReadInt16();
				uint thigh = readside.ReadUInt16();
				uint tlow = readside.ReadUInt16();
				uint tmid = readside.ReadUInt16();
				int sc = readside.ReadUInt16();

				// Create sidedef
				if(sectorlink.ContainsKey(sc))
				{
					Sidedef s = map.CreateSidedef(l, front, sectorlink[sc]);
					s.Update(offsetx, offsety, MakeHashName(thigh), MakeHashName(tmid), MakeHashName(tlow));
				}
				else
				{
					General.ErrorLogger.Add(ErrorType.Warning, "Sidedef " + index + " references invalid sector " + sc + ". Sidedef has been removed.");
				}
			}
			else
			{
				General.ErrorLogger.Add(ErrorType.Warning, "Linedef " + linedefindex + " references invalid sidedef " + index + ". Sidedef has been removed.");
			}
		}

		#endregion

		#region ================== Writing

		// This writes a MapSet to the file
		public override void Write(MapSet map, string mapname, int position)
		{
			Dictionary<Vertex, int> vertexids = new Dictionary<Vertex, int>();
			Dictionary<Sidedef, int> sidedefids = new Dictionary<Sidedef, int>();
			Dictionary<Sector, int> sectorids = new Dictionary<Sector, int>();

			// First index everything
			foreach(Vertex v in map.Vertices) vertexids.Add(v, vertexids.Count);
			foreach(Sidedef sd in map.Sidedefs) sidedefids.Add(sd, sidedefids.Count);
			foreach(Sector s in map.Sectors) sectorids.Add(s, sectorids.Count);

			// Write lumps to wad (note the backwards order because they
			// are all inserted at position+1 when not found)
			WriteLights(map, position, manager.Config.MapLumps);
			WriteSectors(map, position, manager.Config.MapLumps);
			WriteVertices(map, position, manager.Config.MapLumps);
			WriteSidedefs(map, position, manager.Config.MapLumps, sectorids);
			WriteLinedefs(map, position, manager.Config.MapLumps, sidedefids, vertexids);
			WriteThings(map, position, manager.Config.MapLumps);
		}

		// This replaces a map lump
		private void WriteLump(string lumpname, MemoryStream mem, int position, Dictionary<string, MapLumpInfo> maplumps)
		{
			// Find insert position and remove old lump
			int insertpos = MapManager.RemoveSpecificLump(wad, lumpname, position, MapManager.TEMP_MAP_HEADER, maplumps);
			if(insertpos == -1) insertpos = position + 1;
			if(insertpos > wad.Lumps.Count) insertpos = wad.Lumps.Count;

			// Create the lump from memory
			Lump lump = wad.Insert(lumpname, insertpos, (int)mem.Length);
			lump.Stream.Seek(0, SeekOrigin.Begin);
			mem.WriteTo(lump.Stream);
			mem.Flush();
		}

		// This writes the THINGS to WAD file
		private void WriteThings(MapSet map, int position, Dictionary<string, MapLumpInfo> maplumps)
		{
			// Create memory to write to
			MemoryStream mem = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(mem, WAD.ENCODING);

			// Go for all things
			foreach(Thing t in map.Things)
			{
				// Write properties to stream
				writer.Write((Int16)Math.Round(t.Position.x));
				writer.Write((Int16)Math.Round(t.Position.y));
				writer.Write((Int16)Math.Round(t.Position.z));
				writer.Write((Int16)t.AngleDoom);
				writer.Write((UInt16)t.Type);
				writer.Write((UInt16)MakeFlags(t.Flags));
				writer.Write((UInt16)t.Tag);
			}

			WriteLump("THINGS", mem, position, maplumps);
		}

		// This writes the VERTEXES to WAD file
		private void WriteVertices(MapSet map, int position, Dictionary<string, MapLumpInfo> maplumps)
		{
			// Create memory to write to
			MemoryStream mem = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(mem, WAD.ENCODING);

			// Go for all vertices
			foreach(Vertex v in map.Vertices)
			{
				// Write properties to stream (16.16 fixed point)
				writer.Write((int)Math.Round(v.Position.x * 65536.0));
				writer.Write((int)Math.Round(v.Position.y * 65536.0));
			}

			WriteLump("VERTEXES", mem, position, maplumps);
		}

		// This writes the LINEDEFS to WAD file
		private void WriteLinedefs(MapSet map, int position, Dictionary<string, MapLumpInfo> maplumps, IDictionary<Sidedef, int> sidedefids, IDictionary<Vertex, int> vertexids)
		{
			// Create memory to write to
			MemoryStream mem = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(mem, WAD.ENCODING);

			// Go for all lines
			foreach(Linedef l in map.Linedefs)
			{
				// Convert flags
				uint flags = (MakeFlags(l.Flags) & ~(uint)SWITCH_MASK) | (uint)(l.SwitchMask & SWITCH_MASK);

				// Write properties to stream
				writer.Write((UInt16)vertexids[l.Start]);
				writer.Write((UInt16)vertexids[l.End]);
				writer.Write(flags);
				writer.Write((UInt16)((l.Action & ACTION_MASK) | (l.Activate & ~ACTION_MASK)));
				writer.Write((UInt16)l.Tag);

				// Front sidedef
				ushort sid = (l.Front == null ? ushort.MaxValue : (UInt16)sidedefids[l.Front]);
				writer.Write(sid);

				// Back sidedef
				sid = (l.Back == null ? ushort.MaxValue : (UInt16)sidedefids[l.Back]);
				writer.Write(sid);
			}

			WriteLump("LINEDEFS", mem, position, maplumps);
		}

		// This writes the SIDEDEFS to WAD file
		private void WriteSidedefs(MapSet map, int position, Dictionary<string, MapLumpInfo> maplumps, IDictionary<Sector, int> sectorids)
		{
			// Create memory to write to
			MemoryStream mem = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(mem, WAD.ENCODING);

			// Go for all sidedefs
			foreach(Sidedef sd in map.Sidedefs)
			{
				// Write properties to stream
				writer.Write((Int16)sd.OffsetX);
				writer.Write((Int16)sd.OffsetY);
				writer.Write(GetTextureHash(sd.HighTexture));
				writer.Write(GetTextureHash(sd.LowTexture));
				writer.Write(GetTextureHash(sd.MiddleTexture));
				writer.Write((UInt16)sectorids[sd.Sector]);
			}

			WriteLump("SIDEDEFS", mem, position, maplumps);
		}

		// This writes the LIGHTS to WAD file
		private void WriteLights(MapSet map, int position, Dictionary<string, MapLumpInfo> maplumps)
		{
			// Create memory to write to
			MemoryStream mem = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(mem, WAD.ENCODING);

			light = new List<Lights>();
			lightOriginalIndex = new List<int>();
			sectorWrittenIndices = new Dictionary<Sector, int[]>();

			// styd: construct indices properly - each slot = unique entry
			foreach(Sector s in map.Sectors)
			{
				int[] indices = new int[Sector.NUM_COLORS];
				indices[0] = AddLightGetIndex(s.FloorColor);
				indices[1] = AddLightGetIndex(s.CeilColor);
				indices[2] = AddLightGetIndex(s.ThingColor);
				indices[3] = AddLightGetIndex(s.TopColor);
				indices[4] = AddLightGetIndex(s.LowerColor);
				sectorWrittenIndices[s] = indices;
			}

			// styd: reorder the collected LIGHTS entries to match their original physical
			// position in the source WAD when known, so re-saving an unmodified map
			// reproduces the exact original LIGHTS lump content AND order - not just the
			// same set of colors at different offsets. Entries without a known original
			// position (freshly created sectors, or colors manually re-pointed via the
			// Index field) are placed after all the ones that do have one, keeping their
			// relative creation order among themselves.
			int[] order = new int[light.Count];
			for(int i = 0; i < order.Length; i++) order[i] = i;

			Array.Sort(order, delegate(int a, int b)
			{
				int oa = lightOriginalIndex[a];
				int ob = lightOriginalIndex[b];
				if(oa == -1 && ob == -1) return a.CompareTo(b);
				if(oa == -1) return 1;
				if(ob == -1) return -1;
				if(oa == ob) return a.CompareTo(b);
				return oa.CompareTo(ob);
			});

			int[] remap = new int[light.Count];
			List<Lights> sortedlight = new List<Lights>(light.Count);
			for(int newindex = 0; newindex < order.Length; newindex++)
			{
				int oldindex = order[newindex];
				remap[oldindex] = newindex;
				sortedlight.Add(light[oldindex]);
			}
			light = sortedlight;

			foreach(Sector s in map.Sectors)
			{
				int[] indices = sectorWrittenIndices[s];
				for(int k = 0; k < indices.Length; k++)
				{
					if(indices[k] >= Lights.FIRST_LIGHTS_INDEX)
						indices[k] = Lights.FIRST_LIGHTS_INDEX + remap[indices[k] - Lights.FIRST_LIGHTS_INDEX];
				}
			}

			foreach(Lights l in light)
			{
				// Write properties to stream
				writer.Write(l.color.r);
				writer.Write(l.color.g);
				writer.Write(l.color.b);
				writer.Write((byte)0);
				writer.Write(l.tag);
			}

			WriteLump("LIGHTS", mem, position, maplumps);
		}

		// This writes the SECTORS to WAD file
		private void WriteSectors(MapSet map, int position, Dictionary<string, MapLumpInfo> maplumps)
		{
			// Create memory to write to
			MemoryStream mem = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(mem, WAD.ENCODING);

			// Go for all sectors
			foreach(Sector s in map.Sectors)
			{
				// Write properties to stream
				writer.Write((Int16)s.FloorHeight);
				writer.Write((Int16)s.CeilHeight);
				writer.Write(GetTextureHash(s.FloorTexture));
				writer.Write(GetTextureHash(s.CeilTexture));

				// styd: use the indices actually written in LIGHTS
				int[] indices = sectorWrittenIndices[s];
				for(int c = 0; c < Sector.NUM_COLORS; c++) writer.Write((UInt16)indices[c]);

				writer.Write((UInt16)s.Effect);
				writer.Write((UInt16)s.Tag);
				writer.Write((UInt16)MakeFlags(s.Flags));
			}

			WriteLump("SECTORS", mem, position, maplumps);
		}

		#endregion
	}
}
