
#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using CodeImp.DoomBuilder.Rendering;
using SlimDX;
using SlimDX.Direct3D9;

#endregion

namespace CodeImp.DoomBuilder.Data
{
	// styd. One sky of the SKYDEFS lump of Doom 64. A ceiling or a floor with the flat of a sky
	// shows that sky.
	internal sealed class Doom64SkyDef
	{
		public string Flat = string.Empty;		// The flat that stands for this sky
		public string Pic = string.Empty;		// Picture of the sky, or the texture of its clouds
		public string BackPic = string.Empty;	// Picture in front of the sky, such as mountains
		public PixelColor FogColor = new PixelColor(255, 0, 0, 0);
		public PixelColor BaseColor = new PixelColor(255, 0, 0, 0);	// Color of the clouds or of the void
		public PixelColor HighColor = new PixelColor(255, 0, 0, 0);	// Color at the top of the sky
		public PixelColor LowColor = new PixelColor(255, 0, 0, 0);	// Color at the horizon
		public int FogFactor = Doom64Sky.DEFAULT_FOG_NEAR;	// Depth where the fog begins, on a scale where the far plane is 1000
		public bool Cloud;
		public bool Thunder;
		public bool Fire;
		public bool Void;
		public bool FadeInBackground;	// The back picture only shows when a line special asks for it
	}

	// styd. One layer of a Doom 64 sky, as the visual mode draws it: a picture flat on the screen
	// of the game, or the clouds of the game. The layers are drawn one over the other.
	internal sealed class Doom64SkyLayer
	{
		public Texture Texture;
		public bool Clouds;			// The clouds of the game; a picture otherwise
		public bool Smooth;			// The picture is filtered, as the fire of the game is
		public bool Back;			// The layer at the back: it is black where it has no picture
		public bool Mirrored;		// Above its top the picture goes on mirrored; otherwise its top row does
		public float Width;			// Width of the picture, in screen pixels of the game
		public float Height;		// Height of the picture, in screen rows of the game
		public float Top;			// Screen row of the game at the top of the picture
		public float HalfRow = 0.5f;	// Half a row of the texture, in the height of the texture
		public Color4 TopColor;		// Color at the top of the layer
		public Color4 BottomColor;	// Color at the bottom of the layer
		public Color4 BaseColor;	// Color of the clouds

		// What the animation of the sky changes
		public bool Thunder;		// Lightning flashes in these clouds
		public byte[] Fire;			// The pixels of the fire, when this is a fire that can burn
		public float ScrollS;		// How far the clouds have drifted, in widths of their texture
		public float ScrollT;		// The same in depth
		public float Flash;			// What a flash of lightning adds to the colors of the clouds
	}

	// styd. What moves in a Doom 64 sky: the clouds drift, lightning flashes in them and the fire
	// burns. This plays the steps of the game, which draws 30 images in a second.
	internal sealed class Doom64SkyAnimation
	{
		private const float STEP_TIME = 1000f / 30f;	// Milliseconds of a step
		private const int MAX_STEPS = 8;				// Steps in one go, after a long wait
		private const int VBLS_IN_STEP = 2;				// Sixtieths of a second in a step
		private const int CLOUD_WRAP = 16384;			// The offsets of the clouds go around
		private const float CLOUD_S = 0.75f / 2048f;	// Widths of the cloud texture in one unit of its offset: gSPTexture(768 << 6, ...) on 64 pixels of 32 units
		private const float CLOUD_T = 0.5f / 2048f;		// The same in depth: gSPTexture(..., 512 << 6)
		private const int THUNDER_START = 180;			// Sixtieths of a second before the first lightning
		private const int FLASHES = 6;					// Times the light goes on or off in one lightning
		private const float FLASH = 17f / 255f;			// What a flash adds to red, green and blue
		private const int FIRE_SIZE = 64;
		private const int FIRE_COOLING = 16;

		// The table of random numbers of the game
		private static readonly byte[] rndtable =
		{
			0,   8, 109, 220, 222, 241, 149, 107,  75, 248, 254, 140,  16,  66,
			74,  21, 211,  47,  80, 242, 154,  27, 205, 128, 161,  89,  77,  36,
			95, 110,  85,  48, 212, 140, 211, 249,  22,  79, 200,  50,  28, 188,
			52, 140, 202, 120,  68, 145,  62,  70, 184, 190,  91, 197, 152, 224,
			149, 104,  25, 178, 252, 182, 202, 182, 141, 197,   4,  81, 181, 242,
			145,  42,  39, 227, 156, 198, 225, 193, 219,  93, 122, 175, 249,   0,
			175, 143,  70, 239,  46, 246, 163,  53, 163, 109, 168, 135,   2, 235,
			25,  92,  20, 145, 138,  77,  69, 166,  78, 176, 173, 212, 166, 113,
			94, 161,  41,  50, 239,  49, 111, 164,  70,  60,   2,  37, 171,  75,
			136, 156,  11,  56,  42, 146, 138, 229,  73, 146,  77,  61,  98, 196,
			135, 106,  63, 197, 195,  86,  96, 203, 113, 101, 170, 247, 181, 113,
			80, 250, 108,   7, 255, 237, 129, 226,  79, 107, 112, 166, 103, 241,
			24, 223, 239, 120, 198,  58,  60,  82, 128,   3, 184,  66, 143, 224,
			145, 224,  81, 206, 163,  45,  63,  90, 168, 114,  59,  33, 159,  95,
			28, 139, 123,  98, 125, 196,  15,  70, 194, 253,  54,  14, 109, 226,
			71,  17, 161,  93, 186,  87, 244, 138,  20,  52, 123, 251,  26,  36,
			17,  46,  52, 231, 232,  76,  31, 221,  84,  37, 216, 165, 212, 106,
			197, 242,  98,  43,  39, 175, 254, 145, 190,  84, 118, 222, 187, 136,
			120, 163, 236, 249
		};

		private readonly Doom64SkyLayer clouds;		// The layer of the clouds, or null
		private readonly Doom64SkyLayer fire;		// The layer of a fire that can burn, or null
		private readonly byte[] stillfire;			// The pixels of the fire before it burns

		private float time;				// Milliseconds since the last step
		private bool moved;				// Something is not as it was at the start
		private int rndindex;
		private int cloudx, cloudy;		// SkyCloudOffsetX and SkyCloudOffsetY of the game
		private int thundercounter;		// ThunderCounter
		private int lightningcounter;	// LightningCounter
		private int flashes;			// Flashes that are on
		private bool firestep;			// The fire burns in one step out of two

		public bool Moved { get { return moved; } }

		public Doom64SkyAnimation(List<Doom64SkyLayer> layers)
		{
			foreach(Doom64SkyLayer l in layers)
			{
				if(l.Clouds) clouds = l;
				if(l.Fire != null) fire = l;
			}

			if(fire != null) stillfire = (byte[])fire.Fire.Clone();
			thundercounter = THUNDER_START;
		}

		// A random number as the game makes them (M_Random)
		private int Random()
		{
			rndindex = (rndindex + 1) & 0xff;
			return rndtable[rndindex];
		}

		// This lets the time go by. The angle is the direction of the view in the map.
		public void Advance(float milliseconds, float angle)
		{
			if((clouds == null) && (fire == null)) return;
			moved = true;

			// The game takes these from its tables of cosines and sines, in 65536ths
			int viewcos = (int)Math.Floor(Math.Cos(angle) * 65535.0);
			int viewsin = (int)Math.Floor(Math.Sin(angle) * 65535.0);

			time += milliseconds;
			int steps = (int)(time / STEP_TIME);
			time -= steps * STEP_TIME;
			bool burned = false;
			for(int i = 0; i < Math.Min(steps, MAX_STEPS); i++)
			{
				if(clouds != null)
				{
					// R_RenderClouds
					cloudx = (cloudx - (viewcos >> 14)) & (CLOUD_WRAP - 1);
					cloudy = (cloudy + (viewsin >> 13)) & (CLOUD_WRAP - 1);
					if(clouds.Thunder) Thunder();
				}

				// R_RenderFireSky: on one tic of the game out of two
				firestep = !firestep;
				if((fire != null) && firestep) { SpreadFire(); burned = true; }
			}

			if(clouds != null)
			{
				// Between two steps the clouds go on as they will in the next one
				float part = time / STEP_TIME;
				clouds.ScrollS = (cloudx - (viewcos >> 14) * part) * CLOUD_S;
				clouds.ScrollT = (cloudy + (viewsin >> 13) * part) * CLOUD_T;
				clouds.Flash = flashes * FLASH;
			}

			if(burned) Doom64Sky.WriteFire(fire);
		}

		// R_CloudThunder: after a wait, the light of the clouds goes on and off three times,
		// each time for 1 to 8 sixtieths of a second, then the next lightning is 1 to 8 seconds away
		private void Thunder()
		{
			thundercounter -= VBLS_IN_STEP;
			if(thundercounter > 0) return;

			if(lightningcounter == 0)
			{
				// The game plays one of its two sounds of thunder here
				Random();
			}
			else if(lightningcounter >= FLASHES)
			{
				int rand = Random() & 7;
				thundercounter = (((rand << 4) - rand) << 2) + 60;
				lightningcounter = 0;
				return;
			}

			if((lightningcounter & 1) == 0) flashes++; else flashes--;
			thundercounter = (Random() & 7) + 1;
			lightningcounter++;
		}

		// R_SpreadFire, for every column from its second row down: a pixel that burns goes one row
		// up, a little to one side, and may cool; a pixel that does not burn puts out the one above
		private void SpreadFire()
		{
			byte[] buffer = fire.Fire;
			int rand = Random() & 0xff;
			for(int x = 0; x < FIRE_SIZE; x++)
			{
				for(int y = 1; y < FIRE_SIZE; y++)
				{
					int pixel = buffer[y * FIRE_SIZE + x];
					if(pixel != 0)
					{
						int r = rndtable[rand];
						rand = (rand + 2) & 0xff;
						int to = (x - (r & 3) + 1) & (FIRE_SIZE - 1);
						buffer[(y - 1) * FIRE_SIZE + to] = (byte)Math.Max(pixel - (r & 1) * FIRE_COOLING, 0);
					}
					else
					{
						buffer[(y - 1) * FIRE_SIZE + x] = 0;
					}
				}
			}
		}

		// This puts the sky back as it was before anything moved
		public void Reset()
		{
			time = 0f;
			rndindex = 0;
			cloudx = cloudy = 0;
			thundercounter = THUNDER_START;
			lightningcounter = 0;
			flashes = 0;
			firestep = false;
			moved = false;

			if(clouds != null)
			{
				clouds.ScrollS = 0f;
				clouds.ScrollT = 0f;
				clouds.Flash = 0f;
			}

			if(fire != null)
			{
				stillfire.CopyTo(fire.Fire, 0);
				Doom64Sky.WriteFire(fire);
			}
		}
	}

	// styd. This reads the skies of Doom 64 and makes the layers that the visual mode draws
	internal static class Doom64Sky
	{
		#region ================== Constants

		// Name of the lump with the fire texture. The game has this name built in.
		public const string FIRE_PIC = "FIRE";

		// Name of the wall texture that the game does not draw: it is its second texture, and the game
		// has that number built in. The sky shows where a wall has this texture.
		public const string BLANK_TEXTURE = "BLANK";

		// The fog of the game, see R_SetupSky and R_RenderPlayerView. Without a sky the fog is black
		// and begins at 985. The game measures the depth with its projection, from 0 at its near
		// plane to 1000 at its far plane: guFrustum(-8, 8, -6, 6, 8, 3808) in R_Init. At a depth d
		// in map units that is FOG_DEPTH_FAR - FOG_DEPTH_SCALE / d, and the part of fog in a color is
		// (that - fognear) / (1000 - fognear).
		public const int DEFAULT_FOG_NEAR = 985;
		public const float FOG_NEAR_PLANE = 8f;
		public const float FOG_FAR_PLANE = 3808f;
		public const float FOG_DEPTH_FAR = 1000f * FOG_FAR_PLANE / (FOG_FAR_PLANE - FOG_NEAR_PLANE);
		public const float FOG_DEPTH_SCALE = 1000f * FOG_FAR_PLANE * FOG_NEAR_PLANE / (FOG_FAR_PLANE - FOG_NEAR_PLANE);

		// The game draws its sky flat on a screen of 320x240 that shows 90 degrees: a screen pixel is
		// 1/160 of the tangent of an angle and the horizon is on row 120. A sky picture is 256 pixels
		// wide and scrolls by its width for a quarter turn; the fire is 64 pixels wide and stands on
		// the upper half of the screen. See R_RenderSkyPic, R_RenderClouds and R_RenderFireSky.
		public const float SCREEN_FOCAL = 160f;
		public const float SCREEN_HORIZON = 120f;
		public const float PIC_WIDTH = 256f;
		public const float CLOUD_TURN = 3f;			// Times the cloud texture scrolls by in a full turn
		private const float PIC_BOTTOM = 128f;		// Screen row under a sky picture
		private const float BACKPIC_BOTTOM = 170f;	// Screen row under a back picture
		private const float SECOND_PIC_BOTTOM = 240f;	// Screen row under the second sky picture of the sky that fades a picture in
		private const float FIRE_WIDTH = 64f;
		private const int FIRE_SIZE = 64;			// Width and height of the fire texture of the game
		private const float EVERYWHERE = 1000000f;	// Rows of a layer of one color

		#endregion

		#region ================== Reading

		// This reads the sky definitions of a SKYDEFS lump. A sky that is already known is replaced.
		public static void Parse(string text, string source, Dictionary<string, Doom64SkyDef> skies)
		{
			List<string> tokens = Tokenize(text);
			int pos = 0;

			while(pos < tokens.Count)
			{
				string token = tokens[pos++];
				if(string.Compare(token, "sky", StringComparison.OrdinalIgnoreCase) != 0)
				{
					Warn(source, "unexpected \"" + token + "\" outside of a sky");
					continue;
				}

				if((pos + 1 >= tokens.Count) || (tokens[pos + 1] != "{"))
				{
					Warn(source, "a sky needs a name and a block");
					continue;
				}

				Doom64SkyDef sky = new Doom64SkyDef();
				sky.Flat = tokens[pos].ToUpperInvariant();
				pos += 2;

				while((pos < tokens.Count) && (tokens[pos] != "}"))
				{
					string key = tokens[pos++].ToLowerInvariant();
					switch(key)
					{
						case "pic": ReadName(tokens, ref pos, source, key, ref sky.Pic); break;
						case "backpic": ReadName(tokens, ref pos, source, key, ref sky.BackPic); break;
						case "fogcolor": ReadColor(tokens, ref pos, source, key, ref sky.FogColor); break;
						case "basecolor": ReadColor(tokens, ref pos, source, key, ref sky.BaseColor); break;
						case "highcolor": ReadColor(tokens, ref pos, source, key, ref sky.HighColor); break;
						case "lowcolor": ReadColor(tokens, ref pos, source, key, ref sky.LowColor); break;

						case "fogfactor":
							if(Assigned(tokens, ref pos, 1) && int.TryParse(tokens[pos], NumberStyles.Integer, CultureInfo.InvariantCulture, out sky.FogFactor)) pos++;
							else Warn(source, "\"" + key + "\" of sky \"" + sky.Flat + "\" needs a number");
							break;

						case "cloud": sky.Cloud = true; break;
						case "thunder": sky.Thunder = true; break;
						case "fire": sky.Fire = true; break;
						case "void": sky.Void = true; break;
						case "fadeinbackground": sky.FadeInBackground = true; break;

						default:
							Warn(source, "unknown \"" + key + "\" in sky \"" + sky.Flat + "\"");
							break;
					}
				}

				// Skip the end of the block
				pos++;

				skies[sky.Flat] = sky;
			}
		}

		// This checks for "= value", with the given number of value tokens, and moves to the first value
		private static bool Assigned(List<string> tokens, ref int pos, int values)
		{
			if((pos + values >= tokens.Count) || (tokens[pos] != "=")) return false;
			for(int i = 1; i <= values; i++)
				if((tokens[pos + i] == "{") || (tokens[pos + i] == "}") || (tokens[pos + i] == "=")) return false;
			pos++;
			return true;
		}

		private static void ReadName(List<string> tokens, ref int pos, string source, string key, ref string name)
		{
			if(Assigned(tokens, ref pos, 1)) name = tokens[pos++].ToUpperInvariant();
			else Warn(source, "\"" + key + "\" needs a name");
		}

		// A color is three bytes in hexadecimal: red, green and blue
		private static void ReadColor(List<string> tokens, ref int pos, string source, string key, ref PixelColor color)
		{
			byte r = 0, g = 0, b = 0;
			if(Assigned(tokens, ref pos, 3)
				&& byte.TryParse(tokens[pos], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
				&& byte.TryParse(tokens[pos + 1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
				&& byte.TryParse(tokens[pos + 2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
			{
				color = new PixelColor(255, r, g, b);
				pos += 3;
			}
			else Warn(source, "\"" + key + "\" needs three hexadecimal bytes");
		}

		private static void Warn(string source, string message)
		{
			General.ErrorLogger.Add(ErrorType.Warning, "SKYDEFS of \"" + source + "\": " + message + ".");
		}

		// This splits the text in words, quoted names, braces and equal signs. Comments are left out.
		private static List<string> Tokenize(string text)
		{
			List<string> tokens = new List<string>();
			int i = 0;

			while(i < text.Length)
			{
				char c = text[i];
				if(char.IsWhiteSpace(c)) { i++; continue; }

				// Comment until the end of the line
				if((c == '/') && (i + 1 < text.Length) && (text[i + 1] == '/'))
				{
					while((i < text.Length) && (text[i] != '\n')) i++;
					continue;
				}

				if((c == '{') || (c == '}') || (c == '='))
				{
					tokens.Add(c.ToString());
					i++;
					continue;
				}

				if(c == '"')
				{
					int end = text.IndexOf('"', i + 1);
					if(end < 0) end = text.Length;
					tokens.Add(text.Substring(i + 1, end - i - 1));
					i = end + 1;
					continue;
				}

				int start = i;
				while((i < text.Length) && !char.IsWhiteSpace(text[i]) && (text[i] != '{') && (text[i] != '}') && (text[i] != '=') && (text[i] != '"')) i++;
				tokens.Add(text.Substring(start, i - start));
			}

			return tokens;
		}

		#endregion

		#region ================== Layers

		// This makes the layers of a sky, from the back to the front. The pictures may be null
		// when the sky has none or when they could not be loaded. The layers are still: see
		// Doom64SkyAnimation for what moves.
		public static List<Doom64SkyLayer> MakeLayers(Device device, Doom64SkyDef sky, Bitmap pic, Bitmap backpic, Bitmap fire)
		{
			List<Doom64SkyLayer> layers = new List<Doom64SkyLayer>();
			Color4 white = new Color4(1f, 1f, 1f, 1f);
			Color4 black = new Color4(1f, 0f, 0f, 0f);

			// The back of the sky: one color (R_RenderVoidSky), the clouds over the colors of
			// the sky (R_RenderClouds), a picture down to row 128 (R_RenderSpaceSky), or nothing
			if(sky.Void)
			{
				layers.Add(OneColor(device, sky.BaseColor.ToColorValue()));
			}
			else if(sky.Cloud)
			{
				Doom64SkyLayer l = new Doom64SkyLayer();
				l.Texture = (pic != null ? MakeTexture(device, pic) : MakeTexture(device, 0));
				l.Clouds = true;
				l.Thunder = sky.Thunder;
				l.Back = true;
				l.TopColor = sky.HighColor.ToColorValue();
				l.BottomColor = sky.LowColor.ToColorValue();
				l.BaseColor = sky.BaseColor.ToColorValue();
				layers.Add(l);
			}
			else if(pic != null)
			{
				Doom64SkyLayer l = Picture(device, pic, PIC_BOTTOM);
				l.Back = true;
				l.Mirrored = true;
				layers.Add(l);
			}
			else if((fire == null) || !sky.Fire)
			{
				layers.Add(OneColor(device, black));
			}

			// The fire burns on the upper half of the screen, in the high color at its top and the
			// low color at its foot (R_RenderFireSky)
			if((fire != null) && sky.Fire)
			{
				Doom64SkyLayer l = new Doom64SkyLayer();
				l.Texture = MakeTexture(device, fire);
				l.Fire = ReadFire(fire);
				lock(fire) { l.HalfRow = 0.5f / fire.Height; }
				l.Smooth = true;
				l.Back = (layers.Count == 0);
				l.Width = FIRE_WIDTH;
				l.Height = SCREEN_HORIZON;
				l.TopColor = sky.HighColor.ToColorValue();
				l.BottomColor = sky.LowColor.ToColorValue();
				layers.Add(l);
			}

			if(sky.FadeInBackground)
			{
				// The back picture only shows when a line special asks for it. Until then the game
				// fills its screen with a second sky picture (R_RenderEvilSky).
				if((pic != null) && !sky.Cloud && !sky.Void) layers.Add(Picture(device, pic, SECOND_PIC_BOTTOM));
			}
			else if(backpic != null)
			{
				// A back picture stands in front of the sky, down to row 170. The sky shows through
				// its transparent pixels.
				layers.Add(Picture(device, backpic, BACKPIC_BOTTOM));
			}

			foreach(Doom64SkyLayer l in layers)
			{
				l.TopColor.Alpha = 1f;
				l.BottomColor.Alpha = 1f;
				l.BaseColor.Alpha = 1f;
			}

			return layers;
		}

		// A picture that stands on a screen row of the game. It is as wide as a sky picture of
		// the game whatever its own size.
		private static Doom64SkyLayer Picture(Device device, Bitmap picture, float bottom)
		{
			Doom64SkyLayer l = new Doom64SkyLayer();
			l.Texture = MakeTexture(device, picture);
			l.Width = PIC_WIDTH;
			lock(picture)
			{
				l.Height = picture.Height * PIC_WIDTH / picture.Width;
				l.HalfRow = 0.5f / picture.Height;
			}
			l.Top = bottom - l.Height;
			l.TopColor = new Color4(1f, 1f, 1f, 1f);
			l.BottomColor = l.TopColor;
			return l;
		}

		// A layer of one color over the whole sky
		private static Doom64SkyLayer OneColor(Device device, Color4 color)
		{
			Doom64SkyLayer l = new Doom64SkyLayer();
			l.Texture = MakeTexture(device, 255);
			l.Back = true;
			l.Width = PIC_WIDTH;
			l.Height = EVERYWHERE * 2f;
			l.Top = -EVERYWHERE;
			l.TopColor = color;
			l.BottomColor = color;
			return l;
		}

		// This makes a texture from a picture
		private static Texture MakeTexture(Device device, Bitmap picture)
		{
			lock(picture)
			{
				// A copy in a known pixel format
				using(Bitmap copy = new Bitmap(picture))
				{
					Texture texture = new Texture(device, copy.Width, copy.Height, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
					DataRectangle rect = texture.LockRectangle(0, LockFlags.None);
					BitmapData data = copy.LockBits(new Rectangle(0, 0, copy.Width, copy.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
					for(int y = 0; y < copy.Height; y++)
					{
						rect.Data.Seek((long)y * rect.Pitch, SeekOrigin.Begin);
						rect.Data.WriteRange(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), copy.Width * 4);
					}
					copy.UnlockBits(data);
					texture.UnlockRectangle(0);
					return texture;
				}
			}
		}

		// This makes a texture of one pixel of a gray
		private static Texture MakeTexture(Device device, int gray)
		{
			using(Bitmap pixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
			{
				pixel.SetPixel(0, 0, Color.FromArgb(255, gray, gray, gray));
				return MakeTexture(device, pixel);
			}
		}

		// This gives the pixels of the fire texture of the game, which is 64 by 64 pixels of one
		// brightness each. A picture of another size is a fire that does not burn.
		private static byte[] ReadFire(Bitmap picture)
		{
			lock(picture)
			{
				if((picture.Width != FIRE_SIZE) || (picture.Height != FIRE_SIZE)) return null;
				byte[] fire = new byte[FIRE_SIZE * FIRE_SIZE];
				using(Bitmap copy = new Bitmap(picture))
				{
					BitmapData data = copy.LockBits(new Rectangle(0, 0, FIRE_SIZE, FIRE_SIZE), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
					int[] pixels = new int[FIRE_SIZE * FIRE_SIZE];
					for(int y = 0; y < FIRE_SIZE; y++)
						System.Runtime.InteropServices.Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), pixels, y * FIRE_SIZE, FIRE_SIZE);
					copy.UnlockBits(data);
					for(int i = 0; i < fire.Length; i++) fire[i] = (byte)((pixels[i] >> 16) & 0xFF);
				}
				return fire;
			}
		}

		// This writes the pixels of a fire to its texture
		public static void WriteFire(Doom64SkyLayer layer)
		{
			if((layer.Fire == null) || (layer.Texture == null)) return;
			int[] pixels = new int[FIRE_SIZE];
			DataRectangle rect = layer.Texture.LockRectangle(0, LockFlags.None);
			for(int y = 0; y < FIRE_SIZE; y++)
			{
				for(int x = 0; x < FIRE_SIZE; x++)
				{
					int v = layer.Fire[y * FIRE_SIZE + x];
					pixels[x] = (255 << 24) | (v << 16) | (v << 8) | v;
				}
				rect.Data.Seek((long)y * rect.Pitch, SeekOrigin.Begin);
				rect.Data.WriteRange(pixels, 0, FIRE_SIZE);
			}
			layer.Texture.UnlockRectangle(0);
		}

		// This disposes the textures of the layers of a sky
		public static void DisposeLayers(List<Doom64SkyLayer> layers)
		{
			if(layers == null) return;
			foreach(Doom64SkyLayer l in layers)
			{
				if(l.Texture != null) l.Texture.Dispose();
				l.Texture = null;
			}
		}

		#endregion
	}
}
