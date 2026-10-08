
#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CodeImp.DoomBuilder.IO;

#endregion

namespace CodeImp.DoomBuilder.Data
{
	// styd. An animated texture of Doom 64, as an "animpic" block of the ANIMDEFS lump tells it
	internal sealed class Doom64AnimDef
	{
		public string Name = string.Empty;	// The texture that is animated
		public int RestartDelay;			// Tics that the animation waits at its end
		public int Frames;					// How many pictures it has
		public int Speed;					// Tics that a picture stays, less one
		public bool Rewind;					// At its end it runs backwards, instead of starting again
		public bool CyclePalettes;			// Its pictures are the palettes of the texture, instead of the textures that follow it
	}

	// styd. An animated texture as it plays
	internal sealed class Doom64Animation
	{
		private readonly Doom64AnimDef def;

		// What the game keeps for an animation
		private bool reverse;
		private int delay;
		private int tic;
		private int frame;

		private int shown;				// The picture that is shown: 0 is the texture itself
		private ImageData[] images;		// The pictures, made when the texture is first drawn
		private bool failed;			// A picture is missing: the texture stands still

		public Doom64AnimDef Def { get { return def; } }
		public int Shown { get { return shown; } }

		public Doom64Animation(Doom64AnimDef def)
		{
			this.def = def;
			Reset();
		}

		// The start of a level
		public void Reset()
		{
			reverse = false;
			delay = 0;
			tic = 0;
			frame = -1;
			shown = 0;
		}

		// A tic of the game, at this time of the level, in tics
		public void Tick(int leveltime)
		{
			if(delay != 0)
			{
				delay--;
				return;
			}

			if(tic >= leveltime) return;

			int last = (reverse ? 0 : def.Frames - 1);
			tic = leveltime + def.Speed;
			frame += (reverse ? -1 : 1);
			shown = frame;

			if(frame == last)
			{
				if(def.RestartDelay != 0) delay = def.RestartDelay;
				if(def.Rewind) reverse = !reverse;
				else frame = -1;
			}
		}

		// The pictures of the animation, or null when one of them is missing. The first one is the
		// texture itself: the picture that was given.
		public ImageData[] GetImages(ImageData texture)
		{
			if(failed) return null;
			if(images != null) return images;

			ImageData[] made = new ImageData[def.Frames];
			made[0] = texture;
			string name = def.Name;
			for(int i = 1; i < def.Frames; i++)
			{
				if(def.CyclePalettes)
				{
					made[i] = new Doom64PaletteImage(def.Name, i);
				}
				else
				{
					// The textures that follow the texture in the resources
					name = General.Map.Data.GetDoom64NextTexture(name);
					made[i] = (string.IsNullOrEmpty(name) ? null : General.Map.Data.GetTextureImage(name));
					if((made[i] == null) || (made[i] is UnknownImage))
					{
						General.ErrorLogger.Add(ErrorType.Warning, "ANIMDEFS: the texture \"" + def.Name + "\" has " + def.Frames + " pictures, but only " + i + " of them are in the resources. This texture is not animated.");
						failed = true;
						return null;
					}
				}

				// The pictures are kept loaded as long as the resources are
				made[i].AddReference();
			}

			images = made;
			return images;
		}

		public void Dispose()
		{
			// The pictures of palettes are made here; the others belong to the resources
			if((images != null) && def.CyclePalettes)
				for(int i = 1; i < images.Length; i++) images[i].Dispose();
			images = null;
		}
	}

	// styd. The animated textures of Doom 64. The game has a list of them in its ANIMDEFS lump: each
	// one is a texture that is drawn with a picture that changes with the time, wherever it is used.
	// The pictures are either the textures that follow it in the resources, or the palettes that its
	// own picture has. This plays them as the remaster of the game does, from the start of a level,
	// 30 tics in a second. The map is never changed.
	internal sealed class Doom64Animations
	{
		#region ================== Constants

		public const string LUMP_NAME = "ANIMDEFS";

		private const float STEP_TIME = 1000f / 30f;	// Milliseconds of a tic
		private const int MAX_STEPS = 8;				// Tics in one go, after a long wait

		// A picture of the game with several palettes has them one after the other, each of this
		// many colors, and the game knows this many of them
		public const int PALETTE_COLORS = 16;
		public const int MAX_PALETTES = 17;

		#endregion

		#region ================== Variables

		private readonly List<Doom64Animation> animations;
		private readonly Dictionary<long, Doom64Animation> bytexture;	// By the long name of the texture
		private int leveltime;		// Tics since the start
		private float time;			// Milliseconds since the last tic

		#endregion

		#region ================== Properties

		public List<Doom64Animation> Animations { get { return animations; } }

		#endregion

		#region ================== Constructor / Disposer

		public Doom64Animations(List<Doom64AnimDef> defs)
		{
			animations = new List<Doom64Animation>();
			bytexture = new Dictionary<long, Doom64Animation>();

			foreach(Doom64AnimDef def in defs)
			{
				// One picture does not move. A texture has one animation: the last one that is given for it.
				if(def.Frames < 2) continue;
				long longname = Lump.MakeLongName(def.Name);
				Doom64Animation a = new Doom64Animation(def);
				if(bytexture.ContainsKey(longname)) animations.Remove(bytexture[longname]);
				bytexture[longname] = a;
				animations.Add(a);
			}
		}

		public void Dispose()
		{
			foreach(Doom64Animation a in animations) a.Dispose();
		}

		#endregion

		#region ================== Playing

		// The start of a level
		public void Reset()
		{
			if((leveltime == 0) && (time == 0f)) return;
			foreach(Doom64Animation a in animations) a.Reset();
			leveltime = 0;
			time = 0f;
		}

		// A tic of the game
		public void Tick()
		{
			foreach(Doom64Animation a in animations) a.Tick(leveltime);
			leveltime++;
		}

		// This lets the time go by
		public void Advance(float milliseconds)
		{
			time += milliseconds;
			int steps = (int)(time / STEP_TIME);
			time -= steps * STEP_TIME;
			for(int i = 0; (i < steps) && (i < MAX_STEPS); i++) Tick();
		}

		// The picture that a texture is drawn with at this time: the texture itself when it is not
		// animated, or as long as its picture of the moment is not loaded
		public ImageData GetFrame(ImageData texture)
		{
			Doom64Animation a;
			if(!bytexture.TryGetValue(texture.LongName, out a)) return texture;

			ImageData[] images = a.GetImages(texture);
			if((images == null) || (a.Shown <= 0) || (a.Shown >= images.Length)) return texture;

			ImageData image = images[a.Shown];
			return ((image.IsImageLoaded && !image.IsDisposed && !image.LoadFailed) ? image : texture);
		}

		#endregion

		#region ================== Reading

		// This reads the animations of an ANIMDEFS lump: blocks as
		//   animpic "NAME" { restartdelay = 15  frames = 4  speed = 7  rewind  cyclepalettes }
		public static List<Doom64AnimDef> Parse(string text, string source)
		{
			List<Doom64AnimDef> defs = new List<Doom64AnimDef>();
			List<string> tokens = Doom64Sky.Tokenize(text);
			int pos = 0;

			while(pos < tokens.Count)
			{
				string token = tokens[pos++];
				if(string.Compare(token, "animpic", StringComparison.OrdinalIgnoreCase) != 0)
				{
					Warn(source, "unexpected \"" + token + "\" outside of an animpic");
					continue;
				}

				if((pos + 1 >= tokens.Count) || (tokens[pos + 1] != "{"))
				{
					Warn(source, "an animpic needs a name and a block");
					continue;
				}

				Doom64AnimDef def = new Doom64AnimDef();
				def.Name = tokens[pos].ToUpperInvariant();
				pos += 2;

				while((pos < tokens.Count) && (tokens[pos] != "}"))
				{
					string key = tokens[pos++].ToLowerInvariant();
					switch(key)
					{
						case "restartdelay": ReadNumber(tokens, ref pos, source, key, def, ref def.RestartDelay); break;
						case "frames": ReadNumber(tokens, ref pos, source, key, def, ref def.Frames); break;
						case "speed": ReadNumber(tokens, ref pos, source, key, def, ref def.Speed); break;
						case "rewind": def.Rewind = true; break;
						case "cyclepalettes": def.CyclePalettes = true; break;

						default:
							Warn(source, "unknown \"" + key + "\" in animpic \"" + def.Name + "\"");
							break;
					}
				}

				// Skip the end of the block
				pos++;

				if(def.CyclePalettes && (def.Frames > MAX_PALETTES))
				{
					Warn(source, "animpic \"" + def.Name + "\" asks for " + def.Frames + " palettes, the game knows " + MAX_PALETTES);
					def.Frames = MAX_PALETTES;
				}

				defs.Add(def);
			}

			return defs;
		}

		private static void ReadNumber(List<string> tokens, ref int pos, string source, string key, Doom64AnimDef def, ref int number)
		{
			if((pos + 1 < tokens.Count) && (tokens[pos] == "=") && int.TryParse(tokens[pos + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) pos += 2;
			else Warn(source, "\"" + key + "\" of animpic \"" + def.Name + "\" needs a number");
		}

		private static void Warn(string source, string message)
		{
			General.ErrorLogger.Add(ErrorType.Warning, LUMP_NAME + " of \"" + source + "\": " + message + ".");
		}

		#endregion

		#region ================== Palettes

		// This gives the data of a PNG picture with another of its palettes, the way the game takes
		// it: a picture of 16 colors has its palettes one after the other, and one that it does not
		// have is its first one; a picture of 256 colors takes them from the lump that is named
		// after it ("PAL", four letters of its name and the number), or else gets its 16 first colors
		// from that place of its own. Other data is given back as it is.
		public static byte[] WithPalette(byte[] png, string texturename, int palette)
		{
			const int HEADER = 8 + 8 + 13 + 4;	// Signature and the first chunk, which tells the kind of picture
			if((palette <= 0) || (png.Length < HEADER) || (png[1] != 'P') || (png[2] != 'N') || (png[3] != 'G')) return png;

			int bits = png[24];
			if(png[25] != 3) return png;	// Not a picture with a palette

			// Find the palette
			int pos = 8, length = 0;
			bool found = false;
			while(pos + 12 <= png.Length)
			{
				length = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
				if((length < 0) || (pos + 12 + length > png.Length)) return png;
				string type = "" + (char)png[pos + 4] + (char)png[pos + 5] + (char)png[pos + 6] + (char)png[pos + 7];
				if(type == "PLTE") { found = true; break; }
				if((type == "IDAT") || (type == "IEND")) break;
				pos += 12 + length;
			}
			if(!found) return png;

			int colors = length / 3;
			int first = pos + 8;
			byte[] wanted;

			if(bits == 4)
			{
				int start = palette * PALETTE_COLORS;
				if(colors <= start) start = 0;
				wanted = new byte[PALETTE_COLORS * 3];
				for(int i = 0; (i < PALETTE_COLORS) && (start + i < colors); i++)
					Array.Copy(png, first + (start + i) * 3, wanted, i * 3, 3);
			}
			else if((bits >= 8) && (palette < PALETTE_COLORS))
			{
				Playpal lump = General.Map.Data.GetThingPalette("PAL" + (texturename.Length > 4 ? texturename.Substring(0, 4) : texturename) + palette.ToString(CultureInfo.InvariantCulture));
				if(lump != null)
				{
					wanted = new byte[256 * 3];
					for(int i = 0; i < 256; i++)
					{
						wanted[i * 3] = lump[i].r;
						wanted[i * 3 + 1] = lump[i].g;
						wanted[i * 3 + 2] = lump[i].b;
					}
				}
				else
				{
					wanted = new byte[colors * 3];
					Array.Copy(png, first, wanted, 0, wanted.Length);
					int start = palette * PALETTE_COLORS;
					for(int i = 0; (i < PALETTE_COLORS) && (i < colors) && (start + i < colors); i++)
						Array.Copy(png, first + (start + i) * 3, wanted, i * 3, 3);
				}
			}
			else
			{
				return png;
			}

			// The same picture with this palette in place of the one it has
			byte[] chunk = new byte[12 + wanted.Length];
			chunk[0] = (byte)(wanted.Length >> 24);
			chunk[1] = (byte)(wanted.Length >> 16);
			chunk[2] = (byte)(wanted.Length >> 8);
			chunk[3] = (byte)wanted.Length;
			chunk[4] = (byte)'P'; chunk[5] = (byte)'L'; chunk[6] = (byte)'T'; chunk[7] = (byte)'E';
			Array.Copy(wanted, 0, chunk, 8, wanted.Length);
			uint crc = Crc(chunk, 4, 4 + wanted.Length);
			chunk[8 + wanted.Length] = (byte)(crc >> 24);
			chunk[9 + wanted.Length] = (byte)(crc >> 16);
			chunk[10 + wanted.Length] = (byte)(crc >> 8);
			chunk[11 + wanted.Length] = (byte)crc;

			int after = pos + 12 + length;
			byte[] result = new byte[pos + chunk.Length + (png.Length - after)];
			Array.Copy(png, 0, result, 0, pos);
			Array.Copy(chunk, 0, result, pos, chunk.Length);
			Array.Copy(png, after, result, pos + chunk.Length, png.Length - after);
			return result;
		}

		// The checksum that a chunk of a PNG picture ends with
		private static uint Crc(byte[] bytes, int start, int count)
		{
			uint crc = 0xFFFFFFFF;
			for(int i = start; i < start + count; i++)
			{
				crc ^= bytes[i];
				for(int k = 0; k < 8; k++) crc = ((crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1);
			}
			return ~crc;
		}

		#endregion
	}

	// styd. The picture of a Doom 64 texture with another of its palettes
	internal sealed class Doom64PaletteImage : ImageData
	{
		private readonly string lumpname;
		private readonly int palette;

		public Doom64PaletteImage(string texturename, int palette)
		{
			this.lumpname = texturename;
			this.palette = palette;
			this.scale.x = General.Map.Config.DefaultTextureScale;
			this.scale.y = General.Map.Config.DefaultTextureScale;
			SetName(texturename + "#" + palette.ToString(CultureInfo.InvariantCulture));

			// We have no destructor
			GC.SuppressFinalize(this);
		}

		// This loads the image
		protected override void LocalLoadImage()
		{
			// Checks
			if(this.IsImageLoaded) return;

			lock(this)
			{
				if(bitmap != null) bitmap.Dispose(); bitmap = null;
				string location = string.Empty;
				Stream lump = General.Map.Data.GetTextureData(lumpname, false, ref location);
				if(lump != null)
				{
					byte[] bytes = new byte[(int)lump.Length];
					lock(lump)
					{
						lump.Seek(0, SeekOrigin.Begin);
						lump.Read(bytes, 0, bytes.Length);
					}

					MemoryStream mem = new MemoryStream(Doom64Animations.WithPalette(bytes, lumpname, palette));
					IImageReader reader = ImageDataFormat.GetImageReader(mem, ImageDataFormat.DOOMPICTURE, General.Map.Data.Palette);
					if(!(reader is UnknownImageReader))
					{
						mem.Seek(0, SeekOrigin.Begin);
						try { bitmap = reader.ReadAsBitmap(mem); }
						catch(InvalidDataException) { bitmap = null; }
					}

					if(bitmap == null)
					{
						General.ErrorLogger.Add(ErrorType.Error, "Image lump \"" + Path.Combine(location, lumpname) + "\" could not be read with its palette " + palette + ".");
						loadfailed = true;
					}
					else
					{
						width = bitmap.Size.Width;
						height = bitmap.Size.Height;
					}

					mem.Dispose();
				}
				else
				{
					General.ErrorLogger.Add(ErrorType.Error, "Image lump \"" + lumpname + "\" could not be found, while loading its palette " + palette + ".");
					loadfailed = true;
				}

				// Pass on to base
				base.LocalLoadImage();
			}
		}
	}
}
