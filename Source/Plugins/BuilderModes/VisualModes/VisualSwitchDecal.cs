
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
using System.Drawing;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.VisualModes;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	// styd: reproduces R_RenderSwitch from the original Doom 64 engine (p_switch.c / r_phase3.c).
	// The switch is an independent 32x32 decal, centered on the linedef,
	// and not a texture mapped onto the wall geometry.
	internal sealed class VisualSwitchDecal : BaseVisualGeometrySidedef
	{
		#region ================== Constants

		private const float SWITCH_SIZE = 32f;

		// Slight offset perpendicular to the wall to avoid z-fighting with the wall
		// texture behind it (the +sin/+cos term of R_RenderSwitch)
		private const float SWITCH_NORMAL_OFFSET = 0.1f;

		#endregion

		#region ================== Variables

		// styd: center, direction and half width of the decal along the line. A normal wall part
		// always spans the full length of its linedef, but this decal is only 32 units wide,
		// so object picking must also check the horizontal position (see PickFastReject).
		private Vector2D pickcenter;
		private Vector2D pickdir;
		private float pickhalfwidth;

		#endregion

		#region ================== Constructor / Setup

		// Constructor
		public VisualSwitchDecal(BaseVisualMode mode, VisualSector vs, Sidedef s) : base(mode, vs, s)
		{
			geometrytype = VisualGeometryType.WALL_MIDDLE;
			partname = "mid";

			// Set render pass
			this.RenderPass = RenderPass.Mask;

			// We have no destructor
			GC.SuppressFinalize(this);
		}

		// This tells if a sidedef needs a switch decal
		public static bool IsNeeded(Sidedef sd)
		{
			return General.Map.DOOM64 && ((sd.Line.SwitchMask & Linedef.SWITCH_TEXTURE_MASK) != 0);
		}

		// This returns the texture of the sidedef that holds the switch graphic:
		// 1 = upper, 2 = lower, anything else = middle
		private void GetSidedefTexture(int part, out long longname, out bool given)
		{
			switch(part)
			{
				case 1: longname = Sidedef.LongHighTexture; break;
				case 2: longname = Sidedef.LongLowTexture; break;
				default: longname = Sidedef.LongMiddleTexture; break;
			}

			given = (longname != MapSet.EmptyLongName);
		}

		// This builds the geometry. Returns false when no geometry created.
		public override bool Setup()
		{
			bool hasback = ((Sidedef.Other != null) && Sidedef.Line.IsFlagSet(General.Map.Config.DoubleSidedFlag));
			int texturemask = Sidedef.Line.SwitchMask & Linedef.SWITCH_TEXTURE_MASK;
			bool displayupper = ((Sidedef.Line.SwitchMask & Linedef.SWITCH_DISPLAY_UPPER) != 0);
			bool checkfloorheight = ((Sidedef.Line.SwitchMask & Linedef.SWITCH_CHECK_FLOOR_HEIGHT) != 0);

			long switchtex = 0;
			bool hasswitchtex = false;
			float switchz = 0f;
			bool foundcase = false;

			if(hasback)
			{
				Sector front = Sidedef.Sector;
				Sector back = Sidedef.Other.Sector;

				// Case A: on the upper part (near the ceiling step)
				if((back.CeilHeight < front.CeilHeight) && displayupper && !checkfloorheight)
				{
					GetSidedefTexture((texturemask == Linedef.SWITCH_TEXTURE_LOWER) ? 2 : 0, out switchtex, out hasswitchtex);
					switchz = back.CeilHeight + Sidedef.OffsetY + 48f;
					foundcase = true;
				}

				// Case B: on the lower part (near the floor step)
				if(!foundcase && (front.FloorHeight < back.FloorHeight) && checkfloorheight && !displayupper)
				{
					GetSidedefTexture((texturemask == Linedef.SWITCH_TEXTURE_UPPER) ? 1 : 0, out switchtex, out hasswitchtex);
					switchz = back.FloorHeight + Sidedef.OffsetY - 16f;
					foundcase = true;
				}

				// Case C: on the middle part (needs the "Render Mid-Texture" flag on a double sided line)
				if(!foundcase && Sidedef.Line.IsFlagSet("512") && checkfloorheight && displayupper)
				{
					float mbottom = Math.Max(front.FloorHeight, back.FloorHeight);
					GetSidedefTexture((texturemask == Linedef.SWITCH_TEXTURE_UPPER) ? 1 : 2, out switchtex, out hasswitchtex);
					switchz = mbottom + Sidedef.OffsetY + 48f;
					foundcase = true;
				}
			}
			else
			{
				// Single sided: always case C
				if(checkfloorheight && displayupper)
				{
					GetSidedefTexture((texturemask == Linedef.SWITCH_TEXTURE_UPPER) ? 1 : 2, out switchtex, out hasswitchtex);
					switchz = Sidedef.Sector.FloorHeight + Sidedef.OffsetY + 48f;
					foundcase = true;
				}
			}

			float linelength = Sidedef.Line.Length;
			if(!foundcase || !hasswitchtex || (linelength < 0.0001f))
			{
				base.SetVertices(null);
				return false;
			}

			// Load the switch texture
			base.Texture = General.Map.Data.GetTextureImage(switchtex);
			if(base.Texture == null || base.Texture is UnknownImage)
			{
				base.Texture = General.Map.Data.UnknownTexture3D;
				setuponloadedtexture = switchtex;
			}
			else if(!base.Texture.IsImageLoaded)
			{
				setuponloadedtexture = switchtex;
			}

			float topz = switchz;
			float bottomz = topz - SWITCH_SIZE;

			// Left and right vertices for this sidedef
			Vector2D vl, vr;
			if(Sidedef.IsFront)
			{
				vl = Sidedef.Line.Start.Position;
				vr = Sidedef.Line.End.Position;
			}
			else
			{
				vl = Sidedef.Line.End.Position;
				vr = Sidedef.Line.Start.Position;
			}

			// The decal is centered on the line, a little in front of the wall
			Vector2D dir = (vr - vl) / linelength;
			Vector2D center = (vl + vr) * 0.5f;
			Vector2D normal = new Vector2D(dir.y, -dir.x);
			Vector2D offsetcenter = center + normal * SWITCH_NORMAL_OFFSET;
			float half = SWITCH_SIZE * 0.5f;
			Vector2D p1 = offsetcenter - dir * half;
			Vector2D p2 = offsetcenter + dir * half;

			// A switch has the thing color of the sector
			int color = Sidedef.Sector.ThingColor.GetColor();
			fogfactor = 0f;

			List<WorldVertex> verts = new List<WorldVertex>(6);
			verts.Add(new WorldVertex(p1.x, p1.y, bottomz, color, 0f, 1f));
			verts.Add(new WorldVertex(p1.x, p1.y, topz, color, 0f, 0f));
			verts.Add(new WorldVertex(p2.x, p2.y, topz, color, 1f, 0f));
			verts.Add(verts[0]);
			verts.Add(verts[2]);
			verts.Add(new WorldVertex(p2.x, p2.y, bottomz, color, 1f, 1f));

			// Keep top and bottom planes for intersection testing
			top = new Plane(new Vector3D(0, 0, -1), topz);
			bottom = new Plane(new Vector3D(0, 0, 1), -bottomz);

			// And the horizontal span
			pickcenter = center;
			pickdir = dir;
			pickhalfwidth = half;

			base.SetVertices(verts);
			return true;
		}

		#endregion

		#region ================== Methods

		// This performs a fast test in object picking
		public override bool PickFastReject(Vector3D from, Vector3D to, Vector3D dir)
		{
			// Between top and bottom?
			if(!base.PickFastReject(from, to, dir)) return false;

			// styd: and within the 32 units of the decal? Without this, aiming left or right of the
			// decal (but at its height) would pick the switch instead of the wall beside it.
			float along = Vector2D.DotProduct(new Vector2D(pickintersect.x, pickintersect.y) - pickcenter, pickdir);
			return Math.Abs(along) <= pickhalfwidth;
		}

		// Return texture name
		public override string GetTextureName()
		{
			int texturemask = Sidedef.Line.SwitchMask & Linedef.SWITCH_TEXTURE_MASK;
			if(texturemask == Linedef.SWITCH_TEXTURE_UPPER) return this.Sidedef.HighTexture;
			if(texturemask == Linedef.SWITCH_TEXTURE_LOWER) return this.Sidedef.LowTexture;
			return this.Sidedef.MiddleTexture;
		}

		// This changes the texture
		protected override void SetTexture(string texturename)
		{
			int texturemask = Sidedef.Line.SwitchMask & Linedef.SWITCH_TEXTURE_MASK;
			if(texturemask == Linedef.SWITCH_TEXTURE_UPPER) this.Sidedef.SetTextureHigh(texturename);
			else if(texturemask == Linedef.SWITCH_TEXTURE_LOWER) this.Sidedef.SetTextureLow(texturename);
			else this.Sidedef.SetTextureMid(texturename);

			General.Map.Data.UpdateUsedTextures();
			this.Setup();
		}

		// Doom 64 has no offsets per sidedef part, a switch moves with the offsets of its sidedef
		protected override void SetTextureOffsetX(int x) { Sidedef.OffsetX = x; }
		protected override void SetTextureOffsetY(int y) { Sidedef.OffsetY = y; }

		protected override void MoveTextureOffset(int offsetx, int offsety)
		{
			Sidedef.OffsetX += offsetx;
			Sidedef.OffsetY += offsety;
		}

		protected override Point GetTextureOffset()
		{
			return new Point(Sidedef.OffsetX, Sidedef.OffsetY);
		}

		#endregion
	}
}
