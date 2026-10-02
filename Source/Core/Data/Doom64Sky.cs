
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
		// when the sky has none or when they could not be loaded. The clouds do not move, there is
		// no lightning and the fire does not burn.
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
