#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Drawing;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.VisualModes;

#endregion

namespace CodeImp.DoomBuilder.BuilderModes
{
	// styd: reproduces the occlusion of the original Doom 64 engine (solidcols in R_AddLine, r_phase1.c).
	// A line without another side hides everything that is behind it over the whole height of the
	// screen, also what rises above its wall or lies below it. This part goes on above and below
	// the wall. It is drawn as sky, which looks the same wherever it is drawn, so that it cannot be
	// seen; but unlike the sky of a ceiling it hides what is behind it.
	internal sealed class VisualWallOcclusion : BaseVisualGeometrySidedef
	{
		#region ================== Constants

		// How far this part goes on above the ceiling and below the floor
		private const float EXTENT = 16384f;

		#endregion

		#region ================== Variables

		// The wall is between these heights, this part is above and below them
		private float ceilz;
		private float floorz;

		#endregion

		#region ================== Constructor / Setup

		// Constructor
		public VisualWallOcclusion(BaseVisualMode mode, VisualSector vs, Sidedef s) : base(mode, vs, s)
		{
			geometrytype = VisualGeometryType.WALL_OCCLUSION;
			renderassky = true;

			// We have no destructor
			GC.SuppressFinalize(this);
		}

		// This tells if a sidedef without another side hides what is behind it.
		// The lines with "Render Mid-Texture" or "No Occlusion" do not (ML_DRAWMASKED, ML_DONTOCCLUDE).
		public static bool IsNeeded(Sidedef sd)
		{
			return General.Map.DOOM64 && !sd.Line.IsFlagSet("512") && !sd.Line.IsFlagSet("1024");
		}

		// This builds the geometry. Returns false when no geometry created.
		public override bool Setup()
		{
			if(!IsNeeded(Sidedef) || (Sidedef.Line.Length < 0.0001f))
			{
				base.SetVertices(null);
				return false;
			}

			// This is never drawn with a texture, but geometry without one is not drawn at all
			base.Texture = General.Map.Data.BlackTexture;
			fogfactor = 0f;

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

			ceilz = Sidedef.Sector.CeilHeight;
			floorz = Sidedef.Sector.FloorHeight;
			float topz = ceilz + EXTENT;
			float bottomz = floorz - EXTENT;
			const int color = PixelColor.INT_WHITE;

			List<WorldVertex> verts = new List<WorldVertex>(12);

			// Above the wall
			verts.Add(new WorldVertex(vl.x, vl.y, ceilz, color, 0f, 1f));
			verts.Add(new WorldVertex(vl.x, vl.y, topz, color, 0f, 0f));
			verts.Add(new WorldVertex(vr.x, vr.y, topz, color, 1f, 0f));
			verts.Add(verts[0]);
			verts.Add(verts[2]);
			verts.Add(new WorldVertex(vr.x, vr.y, ceilz, color, 1f, 1f));

			// Below the wall
			verts.Add(new WorldVertex(vl.x, vl.y, bottomz, color, 0f, 1f));
			verts.Add(new WorldVertex(vl.x, vl.y, floorz, color, 0f, 0f));
			verts.Add(new WorldVertex(vr.x, vr.y, floorz, color, 1f, 0f));
			verts.Add(verts[6]);
			verts.Add(verts[8]);
			verts.Add(new WorldVertex(vr.x, vr.y, bottomz, color, 1f, 1f));

			// Keep top and bottom planes for intersection testing
			top = new Plane(new Vector3D(0, 0, -1), topz);
			bottom = new Plane(new Vector3D(0, 0, 1), -bottomz);

			base.SetVertices(verts);
			return true;
		}

		#endregion

		#region ================== Methods

		// This performs a fast test in object picking. This part is not where the wall itself is.
		public override bool PickFastReject(Vector3D from, Vector3D to, Vector3D dir)
		{
			return (pickintersect.z > ceilz) || (pickintersect.z < floorz);
		}

		// Unused
		protected override void SetTextureOffsetX(int x) { }
		protected override void SetTextureOffsetY(int y) { }
		protected override void MoveTextureOffset(int offsetx, int offsety) { }
		protected override Point GetTextureOffset() { return Point.Empty; }

		#endregion
	}
}
