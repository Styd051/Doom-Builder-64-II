
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
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Rendering;

#endregion

namespace CodeImp.DoomBuilder.Map
{
	// villsa. Doom 64 colored sector light. A sector has five of these:
	// floor, ceiling, thing, upper wall and lower wall.
	public struct Lights
	{
		#region ================== Constants

		// Doom 64 linedef flags that affect wall coloring
		public const string FLAG_PEG_UPPER_COLOR = "2097152";
		public const string FLAG_PEG_LOWER_COLOR = "4194304";
		public const string FLAG_USE_MULTI_COLORS = "8388608";
		public const string FLAG_FLIP_UPPER_COLOR = "67108864";

		// Indices below this value are plain gray levels, the rest refers to the LIGHTS lump
		public const int FIRST_LIGHTS_INDEX = 256;

		#endregion

		#region ================== Variables

		// Properties
		public PixelColor color;
		public UInt16 tag;
		public bool isDirect;	// styd: true if loaded as a direct index (<256), false if actual LIGHTS input (>=256)

		// styd: raw LIGHTS-lump index this value was loaded from, only meaningful when
		// hasOriginalIndex is true. Populated by Doom64MapSetIO from an existing WAD,
		// or set manually via the "Index" field in the Edit Sector > Lights tab (matching
		// DEX Editor's "Light NNN" field). Used when saving to restore/request exact
		// physical sharing between sectors, regardless of their Sector.Tag.
		public int originalIndex;
		public bool hasOriginalIndex;

		// styd: the fourth byte of the LIGHTS entry that this value was loaded from. No engine uses
		// it (the Nintendo 64 one overwrites it right after loading, Doom64 EX and the remaster do
		// not read it); it is kept so that a saved map has the lump that it was read with.
		public byte unusedByte;

		#endregion

		#region ================== Constructor / Disposer

		public Lights(byte r, byte g, byte b, UInt16 tag)
		{
			this.color.r = r;
			this.color.g = g;
			this.color.b = b;
			this.color.a = 255;
			this.tag = tag;

			// styd: a colored or tagged value can never be a direct gray index
			this.isDirect = (r == g) && (g == b) && (tag == 0);
			this.originalIndex = -1;
			this.hasOriginalIndex = false;
			this.unusedByte = 0;
		}

		#endregion

		#region ================== Methods

		// Serialize / deserialize
		internal void ReadWrite(IReadWriteStream s)
		{
			s.rwByte(ref color.r);
			s.rwByte(ref color.g);
			s.rwByte(ref color.b);
			s.rwByte(ref color.a);
			s.rwUShort(ref tag);
			s.rwBool(ref isDirect);
			s.rwBool(ref hasOriginalIndex);
			s.rwInt(ref originalIndex);
			s.rwByte(ref unusedByte);
		}

		// This returns the color as shown in the editor (with the Light Intensity setting applied)
		public int GetColor()
		{
			return GetFactor(this).ToInt();
		}

		public int GetColor(Lights light)
		{
			return GetFactor(light).ToInt();
		}

		public int GetTopColor(Sidedef s)
		{
			if(s.Line.IsFlagSet(FLAG_FLIP_UPPER_COLOR))
				return GetColor(s.Sector.LowerColor);

			return GetColor(s.Sector.TopColor);
		}

		public int GetLowerColor(Sidedef s)
		{
			return GetColor(s.Sector.LowerColor);
		}

		// This returns the color at the bottom of an upper wall part
		public int UnpegUpperLight(Sidedef s)
		{
			Lights ltop, lbottom;

			if(s.Line.IsFlagSet(FLAG_FLIP_UPPER_COLOR))
			{
				ltop = s.Sector.LowerColor;
				lbottom = s.Sector.TopColor;
			}
			else
			{
				ltop = s.Sector.TopColor;
				lbottom = s.Sector.LowerColor;
			}

			ltop.color = GetFactor(ltop);
			lbottom.color = GetFactor(lbottom);

			if(s.Line.IsFlagSet(FLAG_PEG_UPPER_COLOR))
				return lbottom.color.ToInt();

			Sector front = (s.IsFront ? s.Line.Front.Sector : s.Line.Back.Sector);
			Sector back = (s.IsFront ? s.Line.Back.Sector : s.Line.Front.Sector);

			int height = front.CeilHeight - front.FloorHeight;
			if(height == 0)
				return lbottom.color.ToInt();

			int sh1 = back.CeilHeight - front.FloorHeight;
			int sh2 = front.CeilHeight - back.CeilHeight;

			return BlendByHeight(ltop.color, lbottom.color, height, sh1, sh2);
		}

		// This returns the color at the top of a lower wall part
		public int UnpegLowerLight(Sidedef s)
		{
			Lights ltop = s.Sector.TopColor;
			Lights lbottom = s.Sector.LowerColor;

			ltop.color = GetFactor(ltop);
			lbottom.color = GetFactor(lbottom);

			if(s.Line.IsFlagSet(FLAG_PEG_LOWER_COLOR))
				return ltop.color.ToInt();

			Sector front = (s.IsFront ? s.Line.Front.Sector : s.Line.Back.Sector);
			Sector back = (s.IsFront ? s.Line.Back.Sector : s.Line.Front.Sector);

			int height = front.CeilHeight - front.FloorHeight;
			if(height == 0)
				return ltop.color.ToInt();

			int sh1 = back.FloorHeight - front.FloorHeight;
			int sh2 = front.CeilHeight - back.FloorHeight;

			return BlendByHeight(ltop.color, lbottom.color, height, sh1, sh2);
		}

		private static int BlendByHeight(PixelColor top, PixelColor bottom, int height, int sh1, int sh2)
		{
			float r1 = (((float)top.r / height) * sh1);
			float g1 = (((float)top.g / height) * sh1);
			float b1 = (((float)top.b / height) * sh1);

			float r2 = (((float)bottom.r / height) * sh2);
			float g2 = (((float)bottom.g / height) * sh2);
			float b2 = (((float)bottom.b / height) * sh2);

			PixelColor c = new PixelColor();
			c.r = (byte)Math.Min((int)(r1 + r2), 255);
			c.g = (byte)Math.Min((int)(g1 + g2), 255);
			c.b = (byte)Math.Min((int)(b1 + b2), 255);
			c.a = 255;

			return c.ToInt();
		}

		private static bool PreciseCmp(float f1, float f2)
		{
			const float precision = 0.00001f;
			return (((f1 - precision) < f2) && ((f1 + precision) > f2));
		}

		private static int[] GetHSV(PixelColor color)
		{
			int[] hsv = new int[3];
			byte r = color.r;
			byte g = color.g;
			byte b = color.b;
			int min = r;
			int max = r;
			float j = 0.0f;
			float x = 0.0f;
			float sum = 0.0f;

			if(g < min) min = g;
			if(b < min) min = b;

			if(g > max) max = g;
			if(b > max) max = b;

			float delta = (max / 255.0f);

			if(PreciseCmp(delta, 0.0f))
				delta = 0;
			else
				j = ((delta - (min / 255.0f)) / delta);

			if(!PreciseCmp(j, 0.0f))
			{
				float xr = (r / 255.0f);

				if(!PreciseCmp(xr, delta))
				{
					float xg = (g / 255.0f);

					if(!PreciseCmp(xg, delta))
					{
						float xb = (b / 255.0f);

						if(PreciseCmp(xb, delta))
						{
							sum = ((((delta - xg) / (delta - (min / 255.0f))) + 4.0f) -
								((delta - xr) / (delta - (min / 255.0f))));
						}
					}
					else
					{
						sum = ((((delta - xr) / (delta - (min / 255.0f))) + 2.0f) -
							((delta - (b / 255.0f)) / (delta - (min / 255.0f))));
					}
				}
				else
				{
					sum = (((delta - (b / 255.0f))) / (delta - (min / 255.0f))) -
						((delta - (g / 255.0f)) / (delta - (min / 255.0f)));
				}

				x = (sum * 60.0f);

				if(x < 0)
					x += 360.0f;
			}
			else
			{
				j = 0.0f;
			}

			hsv[0] = (int)((x / 360.0f) * 255.0f);
			hsv[1] = (int)(j * 255.0f);
			hsv[2] = (int)(delta * 255.0f);

			return hsv;
		}

		private static PixelColor GetRGB(int[] hsv)
		{
			float xr = 0.0f;
			float xg = 0.0f;
			float xb = 0.0f;
			int h = hsv[0];
			int s = hsv[1];
			int v = hsv[2];
			PixelColor color = new PixelColor();

			float j = (h / 255.0f) * 360.0f;

			if(360.0f <= j)
				j -= 360.0f;

			float x = (s / 255.0f);
			float i = (v / 255.0f);

			if(!PreciseCmp(x, 0.0f))
			{
				int table = (int)(j / 60.0f);
				if(table < 6)
				{
					float t = (j / 60.0f);
					switch(table)
					{
						case 0:
							xr = i;
							xg = ((1.0f - ((1.0f - (t - table)) * x)) * i);
							xb = ((1.0f - x) * i);
							break;
						case 1:
							xr = ((1.0f - (x * (t - table))) * i);
							xg = i;
							xb = ((1.0f - x) * i);
							break;
						case 2:
							xr = ((1.0f - x) * i);
							xg = i;
							xb = ((1.0f - ((1.0f - (t - table)) * x)) * i);
							break;
						case 3:
							xr = ((1.0f - x) * i);
							xg = ((1.0f - (x * (t - table))) * i);
							xb = i;
							break;
						case 4:
							xr = ((1.0f - ((1.0f - (t - table)) * x)) * i);
							xg = ((1.0f - x) * i);
							xb = i;
							break;
						case 5:
							xr = i;
							xg = ((1.0f - x) * i);
							xb = ((1.0f - (x * (t - table))) * i);
							break;
					}
				}
			}
			else
			{
				xr = xg = xb = i;
			}

			color.r = (byte)(xr * 255.0f);
			color.g = (byte)(xg * 255.0f);
			color.b = (byte)(xb * 255.0f);
			color.a = 255;

			return color;
		}

		// This applies the Light Intensity setting from the preferences. styd. It is the brightness of the
		// remaster divided by 10 (g_brightness), and it is applied as the remaster does (0x140160a00): a gray
		// of the table (written as an index below 256) is that index times the factor; a color of the
		// LIGHTS lump goes to hue, saturation and value kept as fractions, its value is multiplied by the
		// factor and kept at 1 at most, and it comes back to red, green and blue. The Nintendo 64 rounds
		// hue, saturation and value to bytes on the way (LightGetHSV), which puts most colors a few
		// levels off: the remaster does not.
		private static PixelColor GetFactor(Lights light)
		{
			float factor = (General.Settings.LightIntensity * 10 + 100) / 100f;
			PixelColor c = light.color;
			if(light.isDirect && (c.r == c.g) && (c.r == c.b) && (light.tag == 0))
			{
				byte gray = (byte)Math.Min((int)(float)(c.r * factor), 255);
				return new PixelColor(255, gray, gray, gray);
			}

			float h, s, v;
			GetHSVFraction(c, out h, out s, out v);
			v = (float)(v * factor);
			if(v >= 1f) v = 1f;
			return GetRGBFraction(h, s, v);
		}

		// styd. Hue, saturation and value of a color, as fractions, as the remaster has them (0x14006f460)
		private static void GetHSVFraction(PixelColor color, out float h, out float s, out float v)
		{
			const float epsilon = 0.00001f;
			float min = Math.Min(color.r, Math.Min(color.g, color.b)) / 255f;
			float max = Math.Max(color.r, Math.Max(color.g, color.b)) / 255f;
			float r = color.r / 255f, g = color.g / 255f, b = color.b / 255f;
			h = 0f; s = 0f; v = max;
			if(Math.Abs(max) >= epsilon) s = (float)((float)(max - min) / max);
			else v = 0f;
			if(Math.Abs(s) < epsilon)
			{
				s = 0f;
				return;
			}

			float range = (float)(max - min);
			float hue = 0f;
			if(Math.Abs((float)(r - max)) < epsilon)
				hue = (float)((float)((float)(max - b) / range) - (float)((float)(max - g) / range));
			else if(Math.Abs((float)(g - max)) < epsilon)
				hue = (float)((float)((float)((float)(max - r) / range) + 2f) - (float)((float)(max - b) / range));
			else if(Math.Abs((float)(b - max)) < epsilon)
				hue = (float)((float)((float)((float)(max - g) / range) + 4f) - (float)((float)(max - r) / range));
			hue = (float)(hue * 60f);
			if(hue < 0f) hue = (float)(hue + 360f);
			h = (float)(hue / 360f);
		}

		// styd. A color from hue, saturation and value as fractions, as the remaster makes it (0x14006f6a0)
		private static PixelColor GetRGBFraction(float h, float s, float v)
		{
			float r = v, g = v, b = v;
			float t = (float)(h * 360f);
			if(t >= 360f) t = (float)(t - 360f);
			if(Math.Abs(s) >= 0.00001f)
			{
				t = (float)(t / 60f);
				r = g = b = 0f;
				switch((int)t)
				{
					case 0: r = v; g = (float)((float)(1f - (float)((float)(1f - t) * s)) * v); b = (float)((float)(1f - s) * v); break;
					case 1: r = (float)((float)(1f - (float)((float)(t - 1f) * s)) * v); g = v; b = (float)((float)(1f - s) * v); break;
					case 2: r = (float)((float)(1f - s) * v); g = v; b = (float)((float)(1f - (float)((float)(1f - (float)(t - 2f)) * s)) * v); break;
					case 3: r = (float)((float)(1f - s) * v); g = (float)((float)(1f - (float)((float)(t - 3f) * s)) * v); b = v; break;
					case 4: r = (float)((float)(1f - (float)((float)(1f - (float)(t - 4f)) * s)) * v); g = (float)((float)(1f - s) * v); b = v; break;
					case 5: r = v; g = (float)((float)(1f - s) * v); b = (float)((float)(1f - (float)((float)(t - 5f) * s)) * v); break;
				}
			}
			return new PixelColor(255, (byte)(int)(float)(r * 255f), (byte)(int)(float)(g * 255f), (byte)(int)(float)(b * 255f));
		}

		public void SetIntensity(float value)
		{
			float factor = 1.0f + value;
			int[] hsv = GetHSV(this.color);
			hsv[2] = Math.Min((int)(hsv[2] * factor), 255);
			this.color = GetRGB(hsv);
		}

		// styd: computes the raw LIGHTS-lump index to display for a color slot, matching
		// how DEX Editor shows/edits this value directly (e.g. "Light 258"). Returns "" when
		// there's no known physical slot yet (a freshly-created, non-gray color that will get
		// a fresh entry assigned automatically on save).
		public static string GetDisplayIndex(Lights l)
		{
			if(l.hasOriginalIndex) return (FIRST_LIGHTS_INDEX + l.originalIndex).ToString();
			if(l.isDirect) return l.color.r.ToString();
			return "";
		}

		#endregion
	}
}
