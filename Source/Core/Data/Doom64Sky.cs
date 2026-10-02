
#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using CodeImp.DoomBuilder.Rendering;

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

	// styd. This returns the color of a sky in a direction of the map (z is up)
	internal delegate int Doom64SkySampler(float x, float y, float z);

	// styd. This reads the skies of Doom 64 and makes the images of a sky box from them
	internal static class Doom64Sky
	{
		#region ================== Constants

		// Size of a face of the sky box
		public const int FACE_SIZE = 512;

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

		// The game draws a sky on a screen of 320x240 that shows 90 degrees: a pixel is 1/160 of the
		// tangent of an angle, and the horizon is on row 120. A sky picture is 256 pixels wide for a
		// quarter turn. See R_RenderSkyPic, R_RenderClouds and R_RenderFireSky in the game.
		private const float SCREEN_FOCAL = 160f;
		private const float SCREEN_HORIZON = 120f;
		private const float SCREEN_TOP = 0.75f;		// Tangent of the angle at the top of the screen
		private const float PIC_WIDTH = 256f;
		private const float PIC_BOTTOM = 128f;		// Screen row under a sky picture
		private const float BACKPIC_BOTTOM = 170f;	// Screen row under a back picture
		private const float FIRE_TILES = 16f;		// Times the fire texture goes around
		private const float CLOUD_TILE = 5.2f;		// Size of the cloud texture, in heights of the cloud layer

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

		#region ================== Sky box

		// This makes the six faces of a sky box, in the order of the faces of a cube texture.
		// The sampler gives the color of the sky in a direction of the map.
		public static Bitmap[] MakeFaces(int size, Doom64SkySampler sampler)
		{
			Bitmap[] faces = new Bitmap[6];
			int[] pixels = new int[size * size];

			for(int face = 0; face < 6; face++)
			{
				for(int row = 0; row < size; row++)
				{
					float t = (row + 0.5f) / size * 2f - 1f;
					for(int col = 0; col < size; col++)
					{
						float s = (col + 0.5f) / size * 2f - 1f;
						float x, y, z;

						// Direction of this pixel in the cube texture
						switch(face)
						{
							case 0: x = 1f; y = -t; z = -s; break;		// Positive X
							case 1: x = -1f; y = -t; z = s; break;		// Negative X
							case 2: x = s; y = 1f; z = t; break;		// Positive Y
							case 3: x = s; y = -1f; z = -t; break;		// Negative Y
							case 4: x = s; y = -t; z = 1f; break;		// Positive Z
							default: x = -s; y = -t; z = -1f; break;	// Negative Z
						}

						// The sky shader mirrors the Y axis of the map
						pixels[row * size + col] = sampler(x, -y, z);
					}
				}

				Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
				BitmapData data = bmp.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
				Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
				bmp.UnlockBits(data);
				faces[face] = bmp;
			}

			return faces;
		}

		// This makes the sampler of a sky. The pictures may be null when the sky has none
		// or when they could not be loaded.
		public static Doom64SkySampler MakeSampler(Doom64SkyDef sky, Bitmap pic, Bitmap backpic, Bitmap fire, int facesize)
		{
			return new Painter(sky, pic, backpic, fire, facesize).Sample;
		}

		#endregion

		#region ================== Painter

		// The pixels of a picture
		private sealed class Picture
		{
			public readonly int Width;
			public readonly int Height;
			private readonly int[] pixels;

			public Picture(Bitmap source)
			{
				lock(source)
				{
					// A copy in a known pixel format
					using(Bitmap copy = new Bitmap(source))
					{
						Width = copy.Width;
						Height = copy.Height;
						pixels = new int[Width * Height];
						BitmapData data = copy.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
						Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
						copy.UnlockBits(data);
					}
				}
			}

			// This gives a pixel. The picture repeats horizontally, the rows are clamped.
			private int Pixel(int x, int y)
			{
				x %= Width;
				if(x < 0) x += Width;
				if(y < 0) y = 0; else if(y >= Height) y = Height - 1;
				return pixels[y * Width + x];
			}

			// This gives the filtered color at a position, with the colors weighted by their alpha.
			// With tiled set, the picture also repeats vertically.
			public void Sample(float x, float y, bool tiled, out float r, out float g, out float b, out float a)
			{
				x -= 0.5f;
				y -= 0.5f;
				int x0 = (int)Math.Floor(x);
				int y0 = (int)Math.Floor(y);
				float fx = x - x0;
				float fy = y - y0;
				int y1 = y0 + 1;

				if(tiled)
				{
					y0 %= Height; if(y0 < 0) y0 += Height;
					y1 %= Height; if(y1 < 0) y1 += Height;
				}

				r = g = b = a = 0f;
				Add(Pixel(x0, y0), (1f - fx) * (1f - fy), ref r, ref g, ref b, ref a);
				Add(Pixel(x0 + 1, y0), fx * (1f - fy), ref r, ref g, ref b, ref a);
				Add(Pixel(x0, y1), (1f - fx) * fy, ref r, ref g, ref b, ref a);
				Add(Pixel(x0 + 1, y1), fx * fy, ref r, ref g, ref b, ref a);

				if(a > 0f) { r /= a; g /= a; b /= a; }
			}

			private static void Add(int pixel, float weight, ref float r, ref float g, ref float b, ref float a)
			{
				float alpha = ((pixel >> 24) & 0xFF) / 255f * weight;
				r += ((pixel >> 16) & 0xFF) * alpha;
				g += ((pixel >> 8) & 0xFF) * alpha;
				b += (pixel & 0xFF) * alpha;
				a += alpha;
			}

			// This gives the average color of some rows
			public void Average(int firstrow, int rows, out float r, out float g, out float b)
			{
				r = g = b = 0f;
				int count = 0;
				for(int y = Math.Max(firstrow, 0); (y < firstrow + rows) && (y < Height); y++)
				{
					for(int x = 0; x < Width; x++)
					{
						int pixel = pixels[y * Width + x];
						r += (pixel >> 16) & 0xFF;
						g += (pixel >> 8) & 0xFF;
						b += pixel & 0xFF;
						count++;
					}
				}

				if(count > 0) { r /= count; g /= count; b /= count; }
			}
		}

		// This paints a sky the way the game does, as far as a picture that never changes can:
		// the clouds do not move, there is no lightning and the fire does not burn.
		private sealed class Painter
		{
			private const int EDGE_ROWS = 8;	// Rows of a picture that give the color beyond it

			private readonly Doom64SkyDef sky;
			private readonly Picture pic;
			private readonly Picture backpic;
			private readonly Picture fire;
			private readonly float cloudmean;		// Average brightness of the cloud texture
			private readonly float cloudfootprint;	// How fast the cloud texture gets too fine for the sky box
			private readonly float topr, topg, topb;			// Color above the sky picture
			private readonly float bottomr, bottomg, bottomb;	// Color below the sky picture
			private readonly float groundr, groundg, groundb;	// Color below the back picture

			public Painter(Doom64SkyDef sky, Bitmap pic, Bitmap backpic, Bitmap fire, int facesize)
			{
				this.sky = sky;
				if(pic != null) this.pic = new Picture(pic);
				if((backpic != null) && !sky.FadeInBackground)
				{
					this.backpic = new Picture(backpic);
					this.backpic.Average(this.backpic.Height - EDGE_ROWS, EDGE_ROWS, out groundr, out groundg, out groundb);
				}
				if((fire != null) && sky.Fire) this.fire = new Picture(fire);

				if(this.pic != null)
				{
					float r, g, b;
					this.pic.Average(0, this.pic.Height, out r, out g, out b);
					cloudmean = r / 255f;
					cloudfootprint = (this.pic.Width / CLOUD_TILE) * ((float)Math.PI / 2f) / facesize;
					this.pic.Average(0, EDGE_ROWS, out topr, out topg, out topb);
					this.pic.Average(this.pic.Height - EDGE_ROWS, EDGE_ROWS, out bottomr, out bottomg, out bottomb);
				}
			}

			public int Sample(float x, float y, float z)
			{
				float flat = (float)Math.Sqrt(x * x + y * y);
				float length = (float)Math.Sqrt(x * x + y * y + z * z);

				// Tangent of the angle above the horizon, and the screen row of the game that shows it
				float tangent = z / Math.Max(flat, 0.0001f);
				float screenrow = SCREEN_HORIZON - SCREEN_FOCAL * tangent;

				// Position around the horizon, in pixels of a sky picture. A picture goes clockwise.
				float around = -(float)Math.Atan2(y, x) / ((float)Math.PI / 2f) * PIC_WIDTH;

				float r = 0f, g = 0f, b = 0f;

				if(sky.Void)
				{
					r = sky.BaseColor.r; g = sky.BaseColor.g; b = sky.BaseColor.b;
				}
				else if(sky.Cloud)
				{
					Clouds(x, y, z, length, tangent, ref r, ref g, ref b);
				}
				else if(pic != null)
				{
					SkyPicture(around, screenrow, ref r, ref g, ref b);
				}

				if(fire != null) Fire(around, tangent, ref r, ref g, ref b);
				if(backpic != null) BackPicture(around, screenrow, ref r, ref g, ref b);

				return (255 << 24) | (ToByte(r) << 16) | (ToByte(g) << 8) | ToByte(b);
			}

			// A sky picture stands on the horizon. Beyond its edges, where the game never looks, it is
			// mirrored and fades to the color of its edge.
			private void SkyPicture(float around, float screenrow, ref float r, ref float g, ref float b)
			{
				float scale = pic.Width / PIC_WIDTH;
				float row = (screenrow - (PIC_BOTTOM - pic.Height)) * scale;
				float column = (around + SCREEN_FOCAL) * scale;
				float a;

				if(row < 0f)
				{
					pic.Sample(column, Math.Min(-row, pic.Height), false, out r, out g, out b, out a);
					float fade = Smooth(0f, pic.Height, -row);
					r += (topr - r) * fade; g += (topg - g) * fade; b += (topb - b) * fade;
				}
				else if(row > pic.Height)
				{
					float over = row - pic.Height;
					pic.Sample(column, Math.Max(pic.Height - over, 0f), false, out r, out g, out b, out a);
					float fade = Smooth(0f, pic.Height, over);
					r += (bottomr - r) * fade; g += (bottomg - g) * fade; b += (bottomb - b) * fade;
				}
				else
				{
					pic.Sample(column, row, false, out r, out g, out b, out a);
				}
			}

			// The clouds are a layer above the map, lit by the base color, over a sky that goes from
			// the low color at the horizon to the high color at the top of the screen of the game.
			private void Clouds(float x, float y, float z, float length, float tangent, ref float r, ref float g, ref float b)
			{
				float high = Clamp(tangent / SCREEN_TOP);
				r = sky.LowColor.r + (sky.HighColor.r - sky.LowColor.r) * high;
				g = sky.LowColor.g + (sky.HighColor.g - sky.LowColor.g) * high;
				b = sky.LowColor.b + (sky.HighColor.b - sky.LowColor.b) * high;
				if(pic == null) return;

				float brightness = cloudmean;
				if(z > 0f)
				{
					// Far away the texture is finer than the sky box can show: it blends into its average
					float sine = z / length;
					float detail = Clamp(sine * sine / cloudfootprint);
					if(detail > 0f)
					{
						float cr, cg, cb, ca;
						float scale = pic.Width / CLOUD_TILE / z;
						pic.Sample(x * scale, y * scale, true, out cr, out cg, out cb, out ca);
						brightness += (cr / 255f - cloudmean) * detail;
					}
				}

				r += sky.BaseColor.r * brightness;
				g += sky.BaseColor.g * brightness;
				b += sky.BaseColor.b * brightness;
			}

			// The fire burns from the horizon to the top of the screen of the game, in the low color
			// at its foot and the high color at its top
			private void Fire(float around, float tangent, ref float r, ref float g, ref float b)
			{
				float row = (SCREEN_TOP - tangent) / SCREEN_TOP * fire.Height;
				float column = around / (4f * PIC_WIDTH) * FIRE_TILES * fire.Width;
				float fr, fg, fb, fa;
				fire.Sample(column, Math.Max(row, 0f), false, out fr, out fg, out fb, out fa);

				float brightness = fr / 255f;
				if(row < 0f) brightness *= 1f - Smooth(0f, EDGE_ROWS, -row);
				if(tangent < 0f) brightness *= 1f - Smooth(0f, 0.2f, -tangent);

				float low = Clamp(row / fire.Height);
				r += (sky.HighColor.r + (sky.LowColor.r - sky.HighColor.r) * low) * brightness;
				g += (sky.HighColor.g + (sky.LowColor.g - sky.HighColor.g) * low) * brightness;
				b += (sky.HighColor.b + (sky.LowColor.b - sky.HighColor.b) * low) * brightness;
			}

			// A back picture stands in front of the sky. The sky shows through its transparent pixels.
			// Below it, where the game never looks, is the color of its foot.
			private void BackPicture(float around, float screenrow, ref float r, ref float g, ref float b)
			{
				float scale = backpic.Width / PIC_WIDTH;
				float row = (screenrow - (BACKPIC_BOTTOM - backpic.Height)) * scale;
				if(row <= 0f) return;

				float pr, pg, pb, pa;
				backpic.Sample((around + SCREEN_FOCAL) * scale, Math.Max(row, 0.5f), false, out pr, out pg, out pb, out pa);
				pa *= Clamp(row);

				if(row > backpic.Height)
				{
					float fade = Smooth(0f, EDGE_ROWS, row - backpic.Height);
					pr += (groundr - pr) * fade; pg += (groundg - pg) * fade; pb += (groundb - pb) * fade;
					pa += (1f - pa) * fade;
				}

				r += (pr - r) * pa; g += (pg - g) * pa; b += (pb - b) * pa;
			}

			private static float Clamp(float v) { return (v < 0f ? 0f : (v > 1f ? 1f : v)); }

			private static float Smooth(float from, float to, float v)
			{
				float t = Clamp((v - from) / (to - from));
				return t * t * (3f - 2f * t);
			}

			private static int ToByte(float v) { return (v <= 0f ? 0 : (v >= 255f ? 255 : (int)(v + 0.5f))); }
		}

		#endregion
	}
}
