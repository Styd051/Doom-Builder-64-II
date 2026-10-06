
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
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.GZBuilder.Data; //mxd
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using SlimDX;

#endregion

namespace CodeImp.DoomBuilder.VisualModes
{
	public abstract class VisualGeometry : IVisualPickable
	{
		#region ================== Constants

		private const float FOG_DENSITY_SCALER = -1.442692f / 512000f; //mxd. It's -1.442692f / 64000f in GZDoom...;
		private const int FADE_MULTIPLIER = 4; //mxd

		#endregion

		#region ================== Constants

		// styd. The sector flag of Doom 64 that makes a floor liquid
		private const string DOOM64_LIQUID_FLAG = "4";

		// styd. The sector flags of Doom 64 that scroll a floor or a ceiling: the ceiling scrolls, the
		// floor scrolls, three times as fast, and to the left, to the right, up and down
		private const string DOOM64_SCROLL_CEILING_FLAG = "1024";
		private const string DOOM64_SCROLL_FLOOR_FLAG = "2048";
		private const string DOOM64_SCROLL_FAST_FLAG = "16";
		private const string DOOM64_SCROLL_LEFT_FLAG = "4096";
		private const string DOOM64_SCROLL_RIGHT_FLAG = "8192";
		private const string DOOM64_SCROLL_UP_FLAG = "16384";
		private const string DOOM64_SCROLL_DOWN_FLAG = "32768";

		#endregion

		#region ================== Variables

		// Texture
		private ImageData texture;
		
		// Vertices
		private WorldVertex[] vertices;
		private int triangles;
		
		// Selected?
		protected bool selected;
		
		// Elements that this geometry is bound to
		// Only the sector is required, sidedef is only for walls
		private VisualSector sector;
		private Sidedef sidedef;

		/// <summary>
		/// Absolute intersecting coordinates are set during object picking. This is not set if the geometry is not bound to a sidedef.
		/// </summary>
		protected Vector3D pickintersect;

		/// <summary>
		/// Distance unit along the object picking ray is set during object picking. (0.0 is at camera, 1.0f is at far plane) This is not set if the geometry is not bound to a sidedef.
		/// </summary>
		protected float pickrayu;
		
		// Rendering
		private RenderPass renderpass = RenderPass.Solid;
		protected float fogfactor;
		
		// Sector buffer info
		private int vertexoffset;

		//mxd
		private Vector3D[] boundingBox;
		protected VisualGeometryType geometrytype;
		protected string partname; //UDMF part name
		protected bool renderassky;
		protected bool renderskybehind; // styd
		private ImageData doom64liquidtexture; // styd
		private Vector2D doom64scrollflow; // styd
		
		#endregion

		#region ================== Properties
		
		// Internal properties
		public WorldVertex[] Vertices { get { return vertices; } } //mxd
		internal int VertexOffset { get { return vertexoffset; } set { vertexoffset = value; } }
		public int Triangles { get { return triangles; } }

		//mxd
		public Vector3D[] BoundingBox { get { return boundingBox; } }
		public VisualGeometryType GeometryType { get { return geometrytype; } }
		public float FogFactor { get { return fogfactor; } set { fogfactor = value; } }
		public bool RenderAsSky { get { return renderassky; } }

		// styd. In Doom 64 the sky is drawn behind the map: geometry that is drawn as sky shows what is
		// behind it. Only a wall without another side hides what is behind it, as it does in the game.
		public bool RenderAsSkyBackground { get { return renderassky && General.Map.DOOM64 && (geometrytype != VisualGeometryType.WALL_MIDDLE) && (geometrytype != VisualGeometryType.WALL_OCCLUSION); } }

		// styd. In Doom 64 a texture with transparent pixels shows what is behind it through them.
		// Where there is nothing behind, that is the sky: it is drawn behind such geometry.
		public bool RenderSkyBehind { get { return renderskybehind && General.Map.DOOM64; } }

		// styd. Doom 64 draws a liquid floor with two textures: its own, and this one over it
		public ImageData Doom64LiquidTexture { get { return doom64liquidtexture; } }

		// styd. How far the sector of a floor or of a ceiling of Doom 64 scrolls it in a tic of the
		// game, in map units to the left and up
		public Vector2D Doom64ScrollFlow { get { return doom64scrollflow; } }

		/// <summary>
		/// Render pass in which this geometry must be rendered. Default is Solid.
		/// </summary>
		public RenderPass RenderPass { get { return renderpass; } set { renderpass = value; } }

		/// <summary>
		/// Image to use as texture on this geometry.
		/// </summary>
		public ImageData Texture { get { return texture; } set { texture = value; } }

		/// <summary>
		/// Returns the VisualSector this geometry has been added to.
		/// </summary>
		public VisualSector Sector { get { return sector; } internal set { sector = value; } }
		
		/// <summary>
		/// Returns the Sidedef that this geometry is created for. Null for geometry that is sector-wide.
		/// </summary>
		public Sidedef Sidedef { get { return sidedef; } }

		/// <summary>
		/// Selected or not? This is only used by the core to determine what color to draw it with.
		/// </summary>
		public bool Selected { get { return selected; } set { selected = value; } }

		#endregion

		#region ================== Constructor / Destructor
		
		/// <summary>
		/// This creates sector-global visual geometry. This geometry is always visible when any of the sector is visible.
		/// </summary>
		protected VisualGeometry(VisualSector vs)
		{
			this.sector = vs;
			this.geometrytype = VisualGeometryType.UNKNOWN; //mxd
		}

		/// <summary>
		/// This creates visual geometry that is bound to a sidedef. This geometry is only visible when the sidedef is visible. It is automatically back-face culled during rendering and automatically XY intersection tested as well as back-face culled during object picking.
		/// </summary>
		protected VisualGeometry(VisualSector vs, Sidedef sd)
		{
			this.sector = vs;
			this.sidedef = sd;
			this.geometrytype = VisualGeometryType.UNKNOWN; //mxd
		}

		#endregion

		#region ================== Methods
		
		// styd. Doom 64 does not draw the transparent pixels of a texture, on a floor or a ceiling
		// no more than on a wall. This sets how geometry with such a texture is drawn: with its
		// holes, and with the sky behind it. Geometry that is drawn as sky is not concerned.
		protected void SetDoom64TextureHoles()
		{
			if(!General.Map.DOOM64) return;

			bool holes = !renderassky && (texture != null) && texture.IsImageLoaded && (texture.IsMasked || texture.IsTranslucent);
			renderpass = (holes ? (texture.IsTranslucent ? RenderPass.Alpha : RenderPass.Mask) : RenderPass.Solid);
			renderskybehind = holes;
		}

		// styd. Doom 64 draws the floor of a sector with the liquid flag with two textures: the one of
		// the floor, and the next one of the textures of the game over it. This looks for that second
		// texture. A floor that is drawn as sky has none.
		protected void SetDoom64Liquid(Sector s)
		{
			doom64liquidtexture = null;
			if(!General.Map.DOOM64 || renderassky || !s.IsFlagSet(DOOM64_LIQUID_FLAG)) return;

			doom64liquidtexture = General.Map.Data.GetDoom64LiquidTexture(s.FloorTexture);
		}

		// styd. A sector of Doom 64 scrolls its floor, or its ceiling, when it has the flag for it: a
		// unit in a tic, or three, to the left or to the right and up or down. The two textures of a
		// liquid floor scroll with it. A floor or a ceiling that is drawn as sky does not scroll.
		protected void SetDoom64Scroll(Sector s, bool ceiling)
		{
			doom64scrollflow = new Vector2D();
			if(!General.Map.DOOM64 || renderassky || !s.IsFlagSet(ceiling ? DOOM64_SCROLL_CEILING_FLAG : DOOM64_SCROLL_FLOOR_FLAG)) return;

			float speed = (s.IsFlagSet(DOOM64_SCROLL_FAST_FLAG) ? 3f : 1f);
			doom64scrollflow = new Vector2D(
				speed * ((s.IsFlagSet(DOOM64_SCROLL_LEFT_FLAG) ? 1f : 0f) - (s.IsFlagSet(DOOM64_SCROLL_RIGHT_FLAG) ? 1f : 0f)),
				speed * ((s.IsFlagSet(DOOM64_SCROLL_UP_FLAG) ? 1f : 0f) - (s.IsFlagSet(DOOM64_SCROLL_DOWN_FLAG) ? 1f : 0f)));
		}

		// This sets the vertices for this geometry
		protected void SetVertices(ICollection<WorldVertex> verts)
		{
			// Copy vertices
			if(verts != null) //mxd
			{ 
				vertices = new WorldVertex[verts.Count];
				verts.CopyTo(vertices, 0);
				triangles = vertices.Length / 3;

				CalculateNormals(); //mxd
				PerformAutoSelection(); //mxd
			} 
			else 
			{
				vertices = null;
				triangles = 0;
			}

			if(sector != null) sector.NeedsUpdateGeo = true;
		}

		//mxd. Normals calculation algorithm taken from OpenGl wiki 
		private void CalculateNormals() 
		{
			if(triangles == 0) return;

			BoundingBoxSizes bbs = new BoundingBoxSizes(vertices[0]);
			for(int i = 0; i < triangles; i++) 
			{
				int startindex = i * 3;
				WorldVertex p1 = vertices[startindex];
				WorldVertex p2 = vertices[startindex + 1];
				WorldVertex p3 = vertices[startindex + 2];

				Vector3 U = new Vector3(p2.x - p1.x, p2.y - p1.y, p2.z - p1.z);
				Vector3 V = new Vector3(p3.x - p1.x, p3.y - p1.y, p3.z - p1.z);

				p1.nx = p2.nx = p3.nx = -(U.Y * V.Z - U.Z * V.Y);
				p1.ny = p2.ny = p3.ny = -(U.Z * V.X - U.X * V.Z);
				p1.nz = p2.nz = p3.nz = -(U.X * V.Y - U.Y * V.X);

				vertices[startindex] = p1;
				vertices[startindex + 1] = p2;
				vertices[startindex + 2] = p3;

				BoundingBoxTools.UpdateBoundingBoxSizes(ref bbs, p1);
				BoundingBoxTools.UpdateBoundingBoxSizes(ref bbs, p2);
				BoundingBoxTools.UpdateBoundingBoxSizes(ref bbs, p3);
			}

			boundingBox = BoundingBoxTools.CalculateBoundingPlane(bbs);
		}

		//mxd. Calculate fogdistance
		//TODO: this doesn't match any GZDoom light mode...
		//GZDoom: gl_renderstate.h, SetFog();
		//GZDoom: gl_lightlevel.cpp gl_SetFog();
		protected float CalculateFogFactor(int brightness) { return CalculateFogFactor(Sector.Sector, brightness); }
		public static float CalculateFogFactor(Sector sector, int brightness)
		{
			float density;
			int fogdensity = (General.Map.UDMF ? General.Clamp(sector.Fields.GetValue("fogdensity", 0), 0, 510) : 0);
			switch(sector.FogMode)
			{
				case SectorFogMode.OUTSIDEFOGDENSITY:
					if(fogdensity < 3) fogdensity = General.Map.Data.MapInfo.OutsideFogDensity;
					density = fogdensity * FADE_MULTIPLIER;
					break;

				case SectorFogMode.FOGDENSITY:
					if(fogdensity < 3) fogdensity = General.Map.Data.MapInfo.FogDensity;
					density = fogdensity * FADE_MULTIPLIER;
					break;

				case SectorFogMode.FADE:
					if(fogdensity < 3) fogdensity = General.Clamp(255 - brightness, 30, 255);
					density = fogdensity * FADE_MULTIPLIER;
					break;

				case SectorFogMode.CLASSIC:
					density = General.Clamp(255 - brightness, 30, 255);
					break;

				case SectorFogMode.NONE:
					density = 0f;
					break;

				default: throw new NotImplementedException("Unknown SectorFogMode!");
			}

			return density * FOG_DENSITY_SCALER;
		}

		//mxd. Used to get proper sector from 3d-floors
		public virtual Sector GetControlSector() 
		{
			return sector.Sector;
		}

		//mxd. Used to get proper linedef from 3d-floors
		public virtual Linedef GetControlLinedef() 
		{
			return sidedef.Line;
		}

		// This keeps the results for a sidedef intersection
		internal void SetPickResults(Vector3D intersect, float u)
		{
			this.pickintersect = intersect;
			this.pickrayu = u;
		}
		
		/// <summary>
		/// This is called when the geometry must be tested for line intersection. This should reject
		/// as fast as possible to rule out all geometry that certainly does not touch the line.
		/// </summary>
		public virtual bool PickFastReject(Vector3D from, Vector3D to, Vector3D dir)
		{
			return false;
		}
		
		/// <summary>
		/// This is called when the geometry must be tested for line intersection. This should perform
		/// accurate hit detection and set u_ray to the position on the ray where this hits the geometry.
		/// </summary>
		public virtual bool PickAccurate(Vector3D from, Vector3D to, Vector3D dir, ref float u_ray)
		{
			return false;
		}

		//mxd
		protected abstract void PerformAutoSelection();

		#endregion
	}

	//mxd
	public enum VisualGeometryType
	{
		FLOOR,
		CEILING,
		WALL_UPPER,
		WALL_MIDDLE,
		WALL_MIDDLE_3D,
		WALL_LOWER,
		FOG_BOUNDARY,
		UNKNOWN,
		WALL_OCCLUSION, // styd. Doom 64: what a wall without another side hides above and below it
	}
}
