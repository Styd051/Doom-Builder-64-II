
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
using CodeImp.DoomBuilder.GZBuilder.Data;
using CodeImp.DoomBuilder.GZBuilder.MD3;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.VisualModes;
using SlimDX;
using SlimDX.Direct3D9;
using CodeImp.DoomBuilder.GZBuilder;

#endregion

namespace CodeImp.DoomBuilder.Rendering
{
	internal sealed class Renderer3D : Renderer, IRenderer3D
	{
		#region ================== Constants

		private const float PROJ_NEAR_PLANE = 1f;
		private const float FOG_RANGE = 0.9f;

		private const int SHADERPASS_LIGHT = 17; //mxd
		private const int SHADERPASS_SKYBOX = 5; //mxd
		
		// styd. Shape of the screen of Doom 64, and the widest view over the width of a window
		private const float DOOM64_SCREEN_WIDTH = 4f;
		private const float DOOM64_SCREEN_HEIGHT = 3f;
		private const float DOOM64_MAX_FOV = 175f * Angle2D.PI / 180f;

		// styd. Shader passes of the sky of Doom 64: a picture, a picture that is filtered, the clouds
		private const int SHADERPASS_DOOM64_SKY_PICTURE = 18;
		private const int SHADERPASS_DOOM64_SKY_SMOOTH = 19;
		private const int SHADERPASS_DOOM64_SKY_CLOUDS = 20;

		// styd. Shader passes of the liquid floors of Doom 64: the passes of the world, this much further
		private const int SHADERPASS_DOOM64_LIQUID = 21;

		#endregion

		#region ================== Variables

		// Matrices
		private Matrix projection;
		private Matrix view3d;
		private Matrix viewproj; //mxd
		private Matrix billboard;
		private Matrix view2d;
		private Matrix world;
		private Vector3D cameraposition;
        private Vector3D cameravector;
		private int shaderpass;
		
		// Window size
		private Size windowsize;
		
		// Frustum
		private ProjectedFrustum2D frustum;
		
		// styd. Doom 64: the field of view over the width of the window, see CreateProjection
		private float doom64fovx;

		// Thing cage
		private bool renderthingcages;
		//mxd
		private VisualVertexHandle vertexhandle;
		private int[] lightOffsets;
		
		// Crosshair
		private FlatVertex[] crosshairverts;
		private bool crosshairbusy;

		// Highlighting
		private IVisualPickable highlighted;
		private float highlightglow;
		private float highlightglowinv;
		private bool showselection;
		private bool showhighlight;
		
		// villsa. This shows the lighting only: geometry is drawn without its textures
		private bool showlightonly;

		// villsa. Doom 64 linedefs can mirror their textures. This is the addressing set on the device.
		private bool mirroru;
		private bool mirrorv;
		
		//mxd. Solid geometry to be rendered. Must be sorted by sector.
		private Dictionary<ImageData, List<VisualGeometry>> solidgeo;

		//mxd. Masked geometry to be rendered. Must be sorted by sector.
		private Dictionary<ImageData, List<VisualGeometry>> maskedgeo;

		//mxd. Translucent geometry to be rendered. Must be sorted by camera distance.
		private List<VisualGeometry> translucentgeo;

		//mxd. Geometry to be rendered as skybox.
		private List<VisualGeometry> skygeo;

		// styd. Doom 64: geometry to be rendered as the sky behind the map, which hides nothing,
		// and whether the walls without another side hide what is above and below them
		private List<VisualGeometry> skybackgeo;
		private bool wallocclusion;
		private bool fogenabled;

		// styd. Doom 64: the fog of the sky of the map is on everything, in this color
		private bool doom64fog;
		private Color4 doom64fogcolor;

		// styd. Doom 64: a texture of one transparent pixel. Drawn over another texture, it leaves it
		// as it is: it is the second texture of what the game has scrolled.
		private Texture doom64cleartexture;

		//mxd. Solid things to be rendered (currently(?) there won't be any). Must be sorted by sector.
		private Dictionary<ImageData, List<VisualThing>> solidthings;

		//mxd. Masked things to be rendered. Must be sorted by sector.
		private Dictionary<ImageData, List<VisualThing>> maskedthings;

		//mxd. Translucent things to be rendered. Must be sorted by camera distance.
		private List<VisualThing> translucentthings;

		//mxd. Things with attached dynamic lights
		private List<VisualThing> lightthings;
		
		//mxd. Things, which should be rendered as models
		private Dictionary<ModelData, List<VisualThing>> maskedmodelthings;

		//mxd. Things, which should be rendered as translucent models
		private List<VisualThing> translucentmodelthings;

		//mxd. All things. Used to render thing cages
		private List<VisualThing> allthings;

		//mxd. Visual vertices
		private List<VisualVertex> visualvertices;

		//mxd. Event lines
		private List<Line3D> eventlines;

        // FPS-related
        private int fps = 0;
        private System.Diagnostics.Stopwatch fpsWatch;
        private TextLabel fpsLabel;

        #endregion

        #region ================== Properties

        public ProjectedFrustum2D Frustum2D { get { return frustum; } }
		public bool DrawThingCages { get { return renderthingcages; } set { renderthingcages = value; } }
		public bool ShowSelection { get { return showselection; } set { showselection = value; } }
		public bool ShowHighlight { get { return showhighlight; } set { showhighlight = value; } }
		public bool ShowLightOnly { get { return showlightonly; } set { showlightonly = value; } } // villsa
		
		#endregion

		#region ================== Constructor / Disposer

		// Constructor
		internal Renderer3D(D3DDevice graphics) : base(graphics)
		{
			// Initialize
			//CreateProjection(); // [ZZ] don't do undefined things once not even ready
			CreateMatrices2D();
			renderthingcages = true;
			showselection = true;
			showhighlight = true;
			eventlines = new List<Line3D>(); //mxd
			
			// Dummy frustum
			frustum = new ProjectedFrustum2D(new Vector2D(), 0.0f, 0.0f, PROJ_NEAR_PLANE,
				General.Settings.ViewDistance, Angle2D.DegToRad(General.Settings.VisualFOV));

            fpsLabel = new TextLabel();
            fpsLabel.AlignX = TextAlignmentX.Left;
            fpsLabel.AlignY = TextAlignmentY.Top;
            fpsLabel.Text = "(FPS unavailable)";
            fpsWatch = new System.Diagnostics.Stopwatch();

            // We have no destructor
            GC.SuppressFinalize(this);
		}

		// Disposer
		public override void Dispose()
		{
			// Not already disposed?
			if(!isdisposed)
			{
				// Clean up
				if(vertexhandle != null) vertexhandle.Dispose(); //mxd
				if(doom64cleartexture != null) doom64cleartexture.Dispose(); // styd
				
				// Done
				base.Dispose();
			}
		}

		#endregion

		#region ================== Management

		// This is called before a device is reset
		// (when resized or display adapter was changed)
		public override void UnloadResource()
		{
			crosshairverts = null;
		}
		
		// This is called resets when the device is reset
		// (when resized or display adapter was changed)
		public override void ReloadResource()
		{
			CreateMatrices2D();
		}

		// This makes screen vertices for display
		private void CreateCrosshairVerts(Size texturesize)
		{
			// Determine coordinates
			float width = windowsize.Width;
			float height = windowsize.Height;
			RectangleF rect = new RectangleF((float)Math.Round((width - texturesize.Width) * 0.5f), (float)Math.Round((height - texturesize.Height) * 0.5f), texturesize.Width, texturesize.Height);
			
			// Make vertices
			crosshairverts = new FlatVertex[4];
			crosshairverts[0].x = rect.Left;
			crosshairverts[0].y = rect.Top;
			crosshairverts[0].c = -1;
			crosshairverts[1].x = rect.Right;
			crosshairverts[1].y = rect.Top;
			crosshairverts[1].c = -1;
			crosshairverts[1].u = 1.0f;
			crosshairverts[2].x = rect.Left;
			crosshairverts[2].y = rect.Bottom;
			crosshairverts[2].c = -1;
			crosshairverts[2].v = 1.0f;
			crosshairverts[3].x = rect.Right;
			crosshairverts[3].y = rect.Bottom;
			crosshairverts[3].c = -1;
			crosshairverts[3].u = 1.0f;
			crosshairverts[3].v = 1.0f;
		}
		
		#endregion

		#region ================== Resources

		//mxd
		internal void UpdateVertexHandle()
		{
			if(vertexhandle != null)
			{
				vertexhandle.UnloadResource();
				vertexhandle.ReloadResource();
			}
		}

		#endregion
		
		#region ================== Presentation

		// This creates the projection
		internal void CreateProjection()
		{
			// Calculate aspect
			float screenheight = General.Map.Graphics.RenderTarget.ClientSize.Height * (General.Settings.GZStretchView ? General.Map.Data.InvertedVerticalViewStretch : 1.0f); //mxd
			float aspect = General.Map.Graphics.RenderTarget.ClientSize.Width / screenheight;
			
			// The DirectX PerspectiveFovRH matrix method calculates the scaling in X and Y as follows:
			// yscale = 1 / tan(fovY / 2)
			// xscale = yscale / aspect
			// The fov specified in the method is the FOV over Y, but we want the user to specify the FOV
			// over X, so calculate what it would be over Y first;
			float fov = Angle2D.DegToRad(General.Settings.VisualFOV);
			float reversefov = 1.0f / (float)Math.Tan(fov / 2.0f);
			float reversefovy = reversefov * aspect;
			float fovy = (float)Math.Atan(1.0f / reversefovy) * 2.0f;
			
			// styd. In Doom 64 the field of view is the one of a screen of 4 by 3, as in the game
			// (guFrustum(-8, 8, -6, 6, ...) in R_Init for 90 degrees). A window of another shape
			// keeps this view over its height and shows more or less of the sides. The game has
			// square pixels; the stretched view of the preferences makes everything taller here
			// as it does in the other map formats, and leaves the width of the view as it is.
			if(General.Map.DOOM64)
			{
				float stretch = (General.Settings.GZStretchView ? General.Map.Data.VerticalViewStretch : 1.0f);
				float tany = (float)Math.Tan(fov / 2.0f) * DOOM64_SCREEN_HEIGHT / DOOM64_SCREEN_WIDTH;
				fovy = (float)Math.Atan(tany / stretch) * 2.0f;
				doom64fovx = Math.Min((float)Math.Atan(tany * aspect / stretch) * 2.0f, DOOM64_MAX_FOV);
			}

			// Make the projection matrix
			projection = Matrix.PerspectiveFovRH(fovy, aspect, PROJ_NEAR_PLANE, General.Settings.ViewDistance);
			viewproj = view3d * projection; //mxd
		}
		
		// This creates matrices for a camera view
		public void PositionAndLookAt(Vector3D pos, Vector3D lookat)
		{
			// Calculate delta vector
			cameraposition = pos;
            Vector3D delta = lookat - pos;
            cameravector = delta.GetNormal();
            float anglexy = delta.GetAngleXY();
			float anglez = delta.GetAngleZ();

			// Create frustum
			// styd. In Doom 64 the width of the view depends on the shape of the window
			float frustumfov = Angle2D.DegToRad(General.Settings.VisualFOV);
			if(General.Map.DOOM64 && (doom64fovx > 0f)) frustumfov = doom64fovx;
			frustum = new ProjectedFrustum2D(pos, anglexy, anglez, PROJ_NEAR_PLANE,
				General.Settings.ViewDistance, frustumfov);
			
			// Make the view matrix
			view3d = Matrix.LookAtRH(D3DDevice.V3(pos), D3DDevice.V3(lookat), new Vector3(0f, 0f, 1f));
			viewproj = view3d * projection; //mxd
			
			// Make the billboard matrix
			billboard = Matrix.RotationZ(anglexy + Angle2D.PI);
		}
		
		// This creates 2D view matrix
		private void CreateMatrices2D()
		{
			windowsize = graphics.RenderTarget.ClientSize;
			Matrix scaling = Matrix.Scaling((1f / windowsize.Width) * 2f, (1f / windowsize.Height) * -2f, 1f);
			Matrix translate = Matrix.Translation(-(float)windowsize.Width * 0.5f, -(float)windowsize.Height * 0.5f, 0f);
			view2d = translate * scaling;
		}
		
		// This applies the matrices
		private void ApplyMatrices3D()
		{
			graphics.Shaders.World3D.WorldViewProj = world * viewproj; //mxd. Multiplication is ~2x faster than "world * view3d * projection";
		}

		// This sets the appropriate view matrix
		public void ApplyMatrices2D()
		{
			graphics.Device.SetTransform(TransformState.World, world);
			graphics.Device.SetTransform(TransformState.Projection, Matrix.Identity);
			graphics.Device.SetTransform(TransformState.View, view2d);
		}
		
		#endregion

		#region ================== Start / Finish

		// This starts rendering
		public bool Start()
		{
            if (!fpsWatch.IsRunning)
                fpsWatch.Start();

			// styd. The sky of a Doom 64 map follows its sky ceilings
			if(General.Map.DOOM64) General.Map.Data.UpdateDoom64Sky();

            // Start drawing
            if (graphics.StartRendering(true, General.Colors.Background.ToColorValue(), graphics.BackBuffer, graphics.DepthBuffer))
			{
				// Beginning renderstates
				graphics.Device.SetRenderState(RenderState.CullMode, Cull.None);
				graphics.Device.SetRenderState(RenderState.ZEnable, false);
				graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, false);
				graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
				graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
				graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
				graphics.Device.SetRenderState(RenderState.FogEnable, false);
				fogenabled = false; // styd
				graphics.Device.SetRenderState(RenderState.FogDensity, 1.0f);
				graphics.Device.SetRenderState(RenderState.FogColor, General.Colors.Background.ToInt());
				SetupDoom64Fog(); // styd
				graphics.Device.SetRenderState(RenderState.FogStart, General.Settings.ViewDistance * FOG_RANGE);
				graphics.Device.SetRenderState(RenderState.FogEnd, General.Settings.ViewDistance);
				graphics.Device.SetRenderState(RenderState.FogTableMode, FogMode.Linear);
				graphics.Device.SetRenderState(RenderState.RangeFogEnable, false);
				graphics.Device.SetRenderState(RenderState.TextureFactor, -1);
				graphics.Shaders.World3D.HighlightColor = new Color4(); //mxd

				// Texture addressing
				graphics.Device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Wrap);
				graphics.Device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Wrap);
				graphics.Device.SetSamplerState(0, SamplerState.AddressW, TextureAddress.Wrap);

				// Matrices
				world = Matrix.Identity;
				ApplyMatrices3D();

				// Highlight
				if(General.Settings.AnimateVisualSelection)
				{
					highlightglow = (float)Math.Sin(Clock.CurrentTime / 100.0f) * 0.1f + 0.4f;
					highlightglowinv = -highlightglow + 0.8f;
				}
				else
				{
					highlightglow = 0.4f;
					highlightglowinv = 0.3f;
				}
				
				// Determine shader pass to use
				shaderpass = (fullbrightness ? 1 : 0);

				// Create crosshair vertices
				if(crosshairverts == null)
					CreateCrosshairVerts(new Size(General.Map.Data.Crosshair3D.Width, General.Map.Data.Crosshair3D.Height));

				//mxd. Crate vertex handle
				if(vertexhandle == null) vertexhandle = new VisualVertexHandle();
				
				// Ready
				return true;
			}
			else
			{
				// Can't render now
				return false;
			}
		}
		
		// styd. This sets the fog of a Doom 64 map up: the fog of its sky is on all its geometry and
		// things (R_RenderPlayerView in the game). The fog rendering of the preferences switches it.
		private void SetupDoom64Fog()
		{
			doom64fog = General.Map.DOOM64 && General.Settings.GZDrawFog && !fullbrightness;
			if(!doom64fog)
			{
				graphics.Shaders.World3D.Doom64Fog = new Vector4(0f, 0f, 0f, 0f);
				return;
			}

			PixelColor color;
			int fognear;
			General.Map.Data.GetDoom64Fog(out color, out fognear);
			float range = Math.Max(1000 - fognear, 1);

			doom64fogcolor = color.ToColorValue();
			graphics.Shaders.World3D.Doom64Fog = new Vector4((Doom64Sky.FOG_DEPTH_FAR - fognear) / range, Doom64Sky.FOG_DEPTH_SCALE / range, 1f, 0f);
			graphics.Shaders.World3D.Doom64View = new Vector4(cameravector.x, cameravector.y, cameravector.z, 0f);

			// At the end of the view distance the geometry goes into the fog, not into the background
			graphics.Device.SetRenderState(RenderState.FogColor, color.ToInt());
		}

		// This begins rendering world geometry
		public void StartGeometry()
		{
			// Make collections
			solidgeo = new Dictionary<ImageData, List<VisualGeometry>>(); //mxd
			maskedgeo = new Dictionary<ImageData, List<VisualGeometry>>(); //mxd
			translucentgeo = new List<VisualGeometry>(); //mxd
			skygeo = new List<VisualGeometry>(); //mxd
			skybackgeo = new List<VisualGeometry>(); // styd
			wallocclusion = General.Settings.GZDrawSky && General.Map.VisualCamera.IsInsideSector; // styd

			solidthings = new Dictionary<ImageData, List<VisualThing>>(); //mxd
			maskedthings = new Dictionary<ImageData, List<VisualThing>>(); //mxd
			translucentthings = new List<VisualThing>(); //mxd
			
			maskedmodelthings = new Dictionary<ModelData, List<VisualThing>>(); //mxd
			translucentmodelthings = new List<VisualThing>(); //mxd
			lightthings = new List<VisualThing>(); //mxd
			allthings = new List<VisualThing>(); //mxd
		}

		// This ends rendering world geometry
		public void FinishGeometry()
		{
			//mxd. Sort lights
			if(General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && lightthings.Count > 0)
				UpdateLights();

			// Initial renderstates
			graphics.Device.SetRenderState(RenderState.CullMode, Cull.Counterclockwise);
			graphics.Device.SetRenderState(RenderState.ZEnable, true);
			graphics.Device.SetRenderState(RenderState.ZWriteEnable, true);
			graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, false);
			graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
			graphics.Device.SetRenderState(RenderState.TextureFactor, -1);
			graphics.Shaders.World3D.Begin();

			// styd. The sky of Doom 64 is the same however far its geometry is: the fog of the view
			// distance is not for it
			bool skywithoutfog = General.Map.DOOM64 && fogenabled;
			if(skywithoutfog) graphics.Device.SetRenderState(RenderState.FogEnable, false);

			// styd. The sky of Doom 64 is behind everything else: it leaves the depth as it is,
			// so that all that is drawn after it shows, however far it is
			if(skybackgeo.Count > 0)
			{
				world = Matrix.Identity;
				ApplyMatrices3D();
				graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
				RenderSky(skybackgeo);
				graphics.Device.SetRenderState(RenderState.ZWriteEnable, true);
			}

			//mxd. SKY PASS
			if(skygeo.Count > 0)
			{
				world = Matrix.Identity;
				ApplyMatrices3D();
				RenderSky(skygeo);
			}

			if(skywithoutfog) graphics.Device.SetRenderState(RenderState.FogEnable, true); // styd

			// SOLID PASS
			world = Matrix.Identity;
			ApplyMatrices3D();
            RenderSinglePass(solidgeo, solidthings);

			//mxd. Render models, without backface culling
			if(maskedmodelthings.Count > 0)
			{
				graphics.Device.SetRenderState(RenderState.AlphaTestEnable, true);
				graphics.Device.SetRenderState(RenderState.CullMode, Cull.None);
				RenderModels(false, false);
                graphics.Device.SetRenderState(RenderState.CullMode, Cull.Counterclockwise);
			}

			// MASK PASS
			if(maskedgeo.Count > 0 || maskedthings.Count > 0)
			{
				world = Matrix.Identity;
				ApplyMatrices3D();
				graphics.Device.SetRenderState(RenderState.AlphaTestEnable, true);
				RenderSinglePass(maskedgeo, maskedthings);
			}

			//mxd. LIGHT PASS
			if(General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && lightthings.Count > 0)
			{
				world = Matrix.Identity;
				ApplyMatrices3D();
				graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
				graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
				graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
				graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.One);
				
				RenderLights(solidgeo, lightthings);
				RenderLights(maskedgeo, lightthings);

                if (maskedmodelthings.Count > 0)
                {
                    graphics.Device.SetRenderState(RenderState.AlphaTestEnable, true);
                    graphics.Device.SetRenderState(RenderState.CullMode, Cull.None);
                    graphics.Shaders.World3D.IgnoreNormals = true;
                    RenderModels(true, false);
                    graphics.Shaders.World3D.IgnoreNormals = false;
                    graphics.Device.SetRenderState(RenderState.CullMode, Cull.Counterclockwise);
                }
            }

			// ALPHA AND ADDITIVE PASS
			if(translucentgeo.Count > 0 || translucentthings.Count > 0)
			{
				world = Matrix.Identity;
				ApplyMatrices3D();
				graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
				graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
				graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
				graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
				RenderTranslucentPass(translucentgeo, translucentthings);
			}

            // [ZZ] LIGHT PASS on ALPHA GEOMETRY (GZDoom does this)
            if(General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && lightthings.Count > 0 && translucentgeo.Count > 0)
            {
                world = Matrix.Identity;
                ApplyMatrices3D();
                graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
                graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
                graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
                graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.One);
                RenderTranslucentLights(translucentgeo, lightthings);
            }

			//mxd. Render translucent models, with backface culling
			if(translucentmodelthings.Count > 0)
			{
				graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
				graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
				graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
				graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
                RenderModels(false, true);
            }

            // [ZZ] light pass on alpha models
            if (General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && lightthings.Count > 0 && translucentmodelthings.Count > 0)
            {
                graphics.Device.SetRenderState(RenderState.AlphaTestEnable, true);
                graphics.Shaders.World3D.IgnoreNormals = true;
                RenderModels(true, true);
                graphics.Shaders.World3D.IgnoreNormals = false;
            }

			// styd. The remaster of Doom 64 makes its whole view brighter once it is drawn.
			// What only the editor shows comes after. At full brightness the textures are shown
			// as they are: the view is left alone.
			if(General.Map.DOOM64 && !fullbrightness) RenderDoom64Brightness();

            // THING CAGES
            if (renderthingcages)
			{
				world = Matrix.Identity;
				ApplyMatrices3D();
				RenderThingCages();
			}

			//mxd. Visual vertices
			RenderVertices();

			//mxd. Event lines
			if(General.Settings.GZShowEventLines) RenderArrows(eventlines);
			
			// Remove references
			graphics.Shaders.World3D.Texture1 = null;
			graphics.Shaders.World3D.Texture2 = null; // styd
			
			// Done
			graphics.Shaders.World3D.End();

			//mxd. Trash collections
			solidgeo = null;
			maskedgeo = null;
			translucentgeo = null;
			skygeo = null;
			skybackgeo = null; // styd

			solidthings = null;
			maskedthings = null;
			translucentthings = null;
			
			allthings = null;
			lightthings = null;
			maskedmodelthings = null;
			translucentmodelthings = null;

			visualvertices = null;

            //
            fps++;
            if (fpsWatch.ElapsedMilliseconds > 1000)
            {
                fpsLabel.Text = string.Format("{0} FPS", fps);
                fps = 0;
                fpsWatch.Restart();
            }
        }

        // [ZZ] black renderer magic here.
        //      todo maybe implement proper frustum culling eventually?
        //      Frustum2D.IntersectCircle doesn't seem to work here.
        private bool CullLight(VisualThing t)
        {
            Vector3D lightToCamera = (cameraposition - t.CenterV3D).GetNormal();
            double angdiff = Vector3D.DotProduct(lightToCamera, cameravector);
            if (angdiff <= 0)
                return true; // light in front of the camera. it's not negative because I don't want to calculate things twice and need the vector to point at camera.
            // otherwise check light size: large lights might have center on the back, but radius in front of the camera.
            Vector3D lightToCameraWithRadius = (cameraposition - (t.CenterV3D + lightToCamera * t.LightRadius)).GetNormal();
            double angdiffWithRadius = Vector3D.DotProduct(lightToCameraWithRadius, cameravector);
            if (angdiffWithRadius <= 0)
                return true; // light's radius extension is in front of the camera.
            return false;
        }

        //mxd
        private void UpdateLights()
		{
			// Calculate distance to camera
			foreach(VisualThing t in lightthings) t.CalculateCameraDistance(cameraposition);

			// Sort by it, closer ones first
			lightthings.Sort((t1, t2) => Math.Sign(t1.CameraDistance - t2.CameraDistance));

            // Gather the closest
            List<VisualThing> tl = new List<VisualThing>(lightthings.Count);
            // Break on either end of things of max dynamic lights reached
            for (int i = 0; i < lightthings.Count && tl.Count < General.Settings.GZMaxDynamicLights; i++)
            {
                // Make sure we can see this light at all
                if (!CullLight(lightthings[i]))
                    continue;
                tl.Add(lightthings[i]);
            }

            // Update the array
			lightthings = tl;

			// Sort things by light render style
			lightthings.Sort((t1, t2) => Math.Sign(t1.LightType.LightRenderStyle - t2.LightType.LightRenderStyle));
			lightOffsets = new int[4];

			foreach(VisualThing t in lightthings) 
			{
				//add light to apropriate array.
				switch(t.LightType.LightRenderStyle) 
				{
					case GZGeneral.LightRenderStyle.NORMAL:
					case GZGeneral.LightRenderStyle.VAVOOM: lightOffsets[0]++; break;
					case GZGeneral.LightRenderStyle.ADDITIVE: lightOffsets[2]++; break;
                    case GZGeneral.LightRenderStyle.SUBTRACTIVE: lightOffsets[3]++; break;
					default: lightOffsets[1]++; break; // attenuated
				}
			}
		}

		//mxd.
		//I never particularly liked old ThingCages, so I wrote this instead.
		//It should render faster and it has fancy arrow! :)
		private void RenderThingCages() 
		{
			graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
			graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
			graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
			graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
			graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.SourceAlpha);

			graphics.Shaders.World3D.BeginPass(16);

			foreach(VisualThing t in allthings)
			{
				// Setup color
				Color4 thingcolor;
				if(t.Selected && showselection) 
				{
					thingcolor = General.Colors.Selection3D.ToColorValue();
				} 
				else
				{
					thingcolor = t.CageColor;
					if(t != highlighted) thingcolor.Alpha = 0.6f;
				}
				graphics.Shaders.World3D.VertexColor = thingcolor;

				//Render cage
				graphics.Shaders.World3D.ApplySettings();
				graphics.Device.SetStreamSource(0, t.CageBuffer, 0, WorldVertex.Stride);
				graphics.Device.DrawPrimitives(PrimitiveType.LineList, 0, t.CageLength);
			}

			// Done
			graphics.Shaders.World3D.EndPass();
			graphics.Device.SetRenderState(RenderState.TextureFactor, -1);
		}

		// styd. A rectangle over the whole view
		private static readonly WorldVertex[] doom64screenverts =
		{
			new WorldVertex(-1f, -1f, 0f, -1, 0f, 0f), new WorldVertex(-1f, 1f, 0f, -1, 0f, 0f),
			new WorldVertex(1f, -1f, 0f, -1, 0f, 0f), new WorldVertex(1f, 1f, 0f, -1, 0f, 0f)
		};

		// styd. The display brightness of the remaster of Doom 64: every color of the view is
		// multiplied by one plus this brightness, which is 1 unless the player changes it. The
		// game does it with a rectangle over its view, in a gray of that brightness, blended as
		// "what is there, times the gray, plus what is there"; a brightness above 1 takes a
		// second rectangle.
		private void RenderDoom64Brightness()
		{
			float brightness = General.Clamp(General.Settings.Doom64DisplayBrightness, 0f, 2f);
			if(brightness <= 0f) return;

			graphics.Device.SetRenderState(RenderState.CullMode, Cull.None);
			graphics.Device.SetRenderState(RenderState.ZEnable, false);
			graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
			graphics.Device.SetRenderState(RenderState.FogEnable, false);
			graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
			graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
			graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.DestinationColor);
			graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.One);

			graphics.Shaders.World3D.WorldViewProj = Matrix.Identity;
			graphics.Shaders.World3D.BeginPass(16);
			foreach(float part in new float[] { Math.Min(brightness, 1f), brightness - 1f })
			{
				// The game keeps this gray in a byte
				float gray = (int)(part * 255f) / 255f;
				if(gray <= 0f) continue;
				graphics.Shaders.World3D.VertexColor = new Color4(1f, gray, gray, gray);
				graphics.Shaders.World3D.ApplySettings();
				graphics.Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 0, 2, doom64screenverts);
			}
			graphics.Shaders.World3D.EndPass();

			// Back to what the rest of the view is drawn with
			graphics.Device.SetRenderState(RenderState.CullMode, Cull.Counterclockwise);
			graphics.Device.SetRenderState(RenderState.ZEnable, true);
			graphics.Device.SetRenderState(RenderState.FogEnable, fogenabled);
			graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
			graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
			world = Matrix.Identity;
			ApplyMatrices3D();
		}

		//mxd
		private void RenderVertices() 
		{
			if(visualvertices == null) return;

			graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
			graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
			graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
			graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
			graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.SourceAlpha);

			graphics.Shaders.World3D.BeginPass(16);

			foreach(VisualVertex v in visualvertices) 
			{
				world = v.Position;
				ApplyMatrices3D();

				// Setup color
				Color4 color;
				if(v.Selected && showselection) 
				{
					color = General.Colors.Selection3D.ToColorValue();
				} 
				else 
				{
					color = v.HaveHeightOffset ? General.Colors.InfoLine.ToColorValue() : General.Colors.Vertices.ToColorValue();
					if(v != highlighted) color.Alpha = 0.6f;
				}
				graphics.Shaders.World3D.VertexColor = color;

				//Commence drawing!!11
				graphics.Shaders.World3D.ApplySettings();
				graphics.Device.SetStreamSource(0, v.CeilingVertex ? vertexhandle.Upper : vertexhandle.Lower, 0, WorldVertex.Stride);
				graphics.Device.DrawPrimitives(PrimitiveType.LineList, 0, 8);
			}

			// Done
			graphics.Shaders.World3D.EndPass();
			graphics.Device.SetRenderState(RenderState.TextureFactor, -1);
		}

		//mxd
		private void RenderArrows(ICollection<Line3D> lines) 
		{
			// Calculate required points count
			if(lines.Count == 0) return;
			int pointscount = 0;
			foreach(Line3D line in lines) pointscount += (line.RenderArrowhead ? 6 : 2); // 4 extra points for the arrowhead
			if(pointscount < 2) return;
			
			//create vertices
			WorldVertex[] verts = new WorldVertex[pointscount];
			const float scaler = 20f;
			pointscount = 0;

			foreach(Line3D line in lines)
			{
				int color = line.Color.ToInt();

				// Add regular points
				verts[pointscount].x = line.Start.x;
				verts[pointscount].y = line.Start.y;
				verts[pointscount].z = line.Start.z;
				verts[pointscount].c = color;
				pointscount++;

				verts[pointscount].x = line.End.x;
				verts[pointscount].y = line.End.y;
				verts[pointscount].z = line.End.z;
				verts[pointscount].c = color;
				pointscount++;

				// Add arrowhead
				if(line.RenderArrowhead)
				{
					float nz = line.GetDelta().GetNormal().z * scaler;
					float angle = line.GetAngle();
					Vector3D a1 = new Vector3D(line.End.x - scaler * (float)Math.Sin(angle - 0.46f), line.End.y + scaler * (float)Math.Cos(angle - 0.46f), line.End.z - nz);
					Vector3D a2 = new Vector3D(line.End.x - scaler * (float)Math.Sin(angle + 0.46f), line.End.y + scaler * (float)Math.Cos(angle + 0.46f), line.End.z - nz);

					verts[pointscount] = verts[pointscount - 1];
					verts[pointscount + 1].x = a1.x;
					verts[pointscount + 1].y = a1.y;
					verts[pointscount + 1].z = a1.z;
					verts[pointscount + 1].c = color;

					verts[pointscount + 2] = verts[pointscount - 1];
					verts[pointscount + 3].x = a2.x;
					verts[pointscount + 3].y = a2.y;
					verts[pointscount + 3].z = a2.z;
					verts[pointscount + 3].c = color;

					pointscount += 4;
				}
			}

			VertexBuffer vb = new VertexBuffer(General.Map.Graphics.Device, WorldVertex.Stride * verts.Length, Usage.WriteOnly | Usage.Dynamic, VertexFormat.None, Pool.Default);
			DataStream s = vb.Lock(0, WorldVertex.Stride * verts.Length, LockFlags.Discard);
			s.WriteRange(verts);
			vb.Unlock();
			s.Dispose();
			
			//begin rendering
			graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
			graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
			graphics.Device.SetRenderState(RenderState.ZWriteEnable, false);
			graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
			graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.SourceAlpha);

			graphics.Shaders.World3D.BeginPass(15);

			world = Matrix.Identity;
			ApplyMatrices3D();

			//render
			graphics.Shaders.World3D.ApplySettings();
			graphics.Device.SetStreamSource(0, vb, 0, WorldVertex.Stride);
			graphics.Device.DrawPrimitives(PrimitiveType.LineList, 0, pointscount / 2);

			// Done
			graphics.Shaders.World3D.EndPass();
			graphics.Device.SetRenderState(RenderState.TextureFactor, -1);
			vb.Dispose();
		}

		// styd. How far the game has scrolled the texture of a floor, a ceiling or a wall of Doom 64,
		// in parts of that texture: so many units in every tic.
		private static Vector2 Doom64ScrollOffset(VisualGeometry g, ImageData texture)
		{
			long tics = General.Map.Data.Doom64LiquidTics;
			int width = Math.Max((int)texture.ScaledWidth, 1), height = Math.Max((int)texture.ScaledHeight, 1);
			Vector2D flow = g.Doom64ScrollFlow;

			// (whole numbers of tics and of units: nothing drifts however long it moves; a texture that
			// a wall mirrors comes back on itself after two times its size)
			return new Vector2((float)((tics * (int)flow.x) % (2 * width)) / width, (float)((tics * (int)flow.y) % (2 * height)) / height);
		}

		// styd. How far the two textures of a liquid floor of Doom 64 have scrolled, in parts of the
		// texture of the floor, as the remaster of the game scrolls them: a counter goes half a unit
		// in every tic, and a sector that scrolls its floor scrolls both textures with it. The
		// texture of the floor goes with the counter along its width; the texture that is drawn over
		// it goes against the counter along its height.
		private static Vector4 Doom64LiquidOffsets(VisualGeometry g, ImageData texture)
		{
			long tics = General.Map.Data.Doom64LiquidTics;
			int width = Math.Max((int)texture.ScaledWidth, 1), height = Math.Max((int)texture.ScaledHeight, 1);
			Vector2 scroll = Doom64ScrollOffset(g, texture);

			// (whole numbers of tics and of units: nothing drifts however long the liquids move)
			float counterx = (float)(tics % (2 * width)) / (2 * width);
			float countery = (float)(tics % (2 * height)) / (2 * height);
			return new Vector4(counterx + scroll.X, scroll.Y, scroll.X, scroll.Y - countery);
		}

		// styd. What the light effect of a sector of Doom 64 adds at this time to the textures of what
		// the sector shows, as the game adds it before it gives them the color of the sector. Nothing
		// at full brightness, where the light of the sectors is not shown.
		private static float Doom64SectorLight(Sector s)
		{
			if(fullbrightness || (s == null) || !General.Map.DOOM64) return 0f;
			return General.Map.Data.GetDoom64SectorLight(s) / 255f;
		}

		// styd. This sets a transparent texture as the second texture of the passes that draw the
		// liquid floors of Doom 64: it leaves their first texture as it is
		private void SetDoom64ClearTexture()
		{
			if((doom64cleartexture == null) || doom64cleartexture.Disposed)
			{
				doom64cleartexture = new Texture(graphics.Device, 1, 1, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
				doom64cleartexture.LockRectangle(0, LockFlags.None).Data.Write(0);
				doom64cleartexture.UnlockRectangle(0);
			}
			graphics.Shaders.World3D.Texture2 = doom64cleartexture;
		}

		// styd. This sets the second texture, the offsets and the light of the passes that draw the
		// liquid floors of Doom 64, what the game has scrolled and what a light effect lights, and
		// returns how far these passes are from the usual ones: 0 for geometry that is drawn as usual.
		// A liquid floor has its second texture. A floor, a ceiling or a wall that has scrolled, or
		// whose sector has a light at this time, has a transparent second texture, which leaves its
		// own texture as it is, where it has scrolled to.
		private int SetDoom64ScrollPass(VisualGeometry g)
		{
			// (not when another texture is drawn in the place of its own, or the lighting alone)
			ImageData texture = g.Texture;
			if(showlightonly || (texture is UnknownImage) || !texture.IsImageLoaded || texture.IsDisposed) return 0;

			float light = Doom64SectorLight((g.Sector != null) ? g.Sector.Sector : null);

			ImageData liquid = g.Doom64LiquidTexture;
			if((liquid != null) && liquid.IsImageLoaded && !liquid.IsDisposed)
			{
				if((liquid.Texture == null) || liquid.Texture.Disposed) liquid.CreateTexture();
				graphics.Shaders.World3D.Texture2 = liquid.Texture;
				graphics.Shaders.World3D.Doom64Liquid = Doom64LiquidOffsets(g, texture);
				graphics.Shaders.World3D.Doom64Light = light;
				return SHADERPASS_DOOM64_LIQUID;
			}

			Vector2 scroll = new Vector2();
			if((g.Doom64ScrollFlow.x != 0f) || (g.Doom64ScrollFlow.y != 0f)) scroll = Doom64ScrollOffset(g, texture);
			if((scroll.X == 0f) && (scroll.Y == 0f) && (light == 0f)) return 0;

			SetDoom64ClearTexture();
			graphics.Shaders.World3D.Doom64Liquid = new Vector4(scroll.X, scroll.Y, 0f, 0f);
			graphics.Shaders.World3D.Doom64Light = light;
			return SHADERPASS_DOOM64_LIQUID;
		}

		// styd. The same for a thing in a sector of Doom 64 that has a light at this time, given the
		// pass that it would be drawn with: the passes 0, 2, 8 and 10 have such a pass.
		private int SetDoom64LightPass(VisualThing t, int wantedshaderpass)
		{
			if(((wantedshaderpass & ~10) != 0) || (t.StencilColor.a != 0)) return 0;

			float light = Doom64SectorLight(t.Thing.Sector);
			if(light == 0f) return 0;

			SetDoom64ClearTexture();
			graphics.Shaders.World3D.Doom64Liquid = new Vector4();
			graphics.Shaders.World3D.Doom64Light = light;
			return SHADERPASS_DOOM64_LIQUID;
		}

		// This performs a single render pass
		private void RenderSinglePass(Dictionary<ImageData, List<VisualGeometry>> geopass, Dictionary<ImageData, List<VisualThing>> thingspass)
		{
			ImageData curtexture;
			int currentshaderpass = shaderpass;
			int highshaderpass = shaderpass + 2;

			// Begin rendering with this shader
			graphics.Shaders.World3D.BeginPass(shaderpass);

			// Render the geometry collected
			foreach(KeyValuePair<ImageData, List<VisualGeometry>> group in geopass)
			{
				// What texture to use?
				if(group.Key is UnknownImage)
					curtexture = General.Map.Data.UnknownTexture3D;
				else if(group.Key.IsImageLoaded && !group.Key.IsDisposed)
					curtexture = group.Key;
				else
					curtexture = General.Map.Data.Hourglass3D;

				// styd. An animated texture of Doom 64 is drawn with its picture of the moment
				if(General.Map.DOOM64) curtexture = General.Map.Data.GetDoom64AnimationFrame(curtexture);

				// Create Direct3D texture if still needed
				if((curtexture.Texture == null) || curtexture.Texture.Disposed)
					curtexture.CreateTexture();

				// Apply texture (villsa: or none, to show the lighting only)
				graphics.Shaders.World3D.Texture1 = (showlightonly ? General.Map.Data.WhiteTexture.Texture : curtexture.Texture);
				
				//mxd. Sort geometry by sector index
				group.Value.Sort((g1, g2) => g1.Sector.Sector.FixedIndex - g2.Sector.Sector.FixedIndex);

				// Go for all geometry that uses this texture
				VisualSector sector = null;
				
				foreach(VisualGeometry g in group.Value)
				{
					// Changing sector?
					if(!object.ReferenceEquals(g.Sector, sector))
					{
						// Update the sector if needed
						if(g.Sector.NeedsUpdateGeo) g.Sector.Update();

						// Only do this sector when a vertexbuffer is created
						//mxd. No Map means that sector was deleted recently, I suppose
						if(g.Sector.GeometryBuffer != null && g.Sector.Sector.Map != null) 
						{
							// Change current sector
							sector = g.Sector;

							// Set stream source
							graphics.Device.SetStreamSource(0, sector.GeometryBuffer, 0, WorldVertex.Stride);
						}
						else
						{
							sector = null;
						}
					}

                    graphics.Shaders.World3D.Desaturation = 0;
                    if (sector != null) 
					{
						// Determine the shader pass we want to use for this object
						int wantedshaderpass = (((g == highlighted) && showhighlight) || (g.Selected && showselection)) ? highshaderpass : shaderpass;

						//mxd. Render fog?
						if(General.Settings.GZDrawFog && !fullbrightness && (doom64fog || sector.Sector.FogMode != SectorFogMode.NONE)) // styd
							wantedshaderpass += 8;

						// styd. A liquid floor of Doom 64 is drawn with its two textures, in passes of its
						// own; a floor, a ceiling or a wall that has scrolled is drawn in these passes too
						wantedshaderpass += SetDoom64ScrollPass(g);

						// Switch shader pass?
						if(currentshaderpass != wantedshaderpass)
						{
							graphics.Shaders.World3D.EndPass();
							graphics.Shaders.World3D.BeginPass(wantedshaderpass);
							currentshaderpass = wantedshaderpass;

							//mxd. Set variables for fog rendering?
							if(wantedshaderpass > 7)
							{
								graphics.Shaders.World3D.World = world;
                                graphics.Shaders.World3D.ModelNormal = Matrix.Identity;
                            }
						}

						//mxd. Set variables for fog rendering?
						if(wantedshaderpass > 7)
						{
							graphics.Shaders.World3D.CameraPosition = new Vector4(cameraposition.x, cameraposition.y, cameraposition.z, g.FogFactor);
							graphics.Shaders.World3D.LightColor = (doom64fog ? doom64fogcolor : sector.Sector.FogColor); // styd
						}
                        
						// Set the colors to use
						graphics.Shaders.World3D.HighlightColor = CalculateHighlightColor((g == highlighted) && showhighlight, (g.Selected && showselection));

                        // [ZZ] include desaturation factor
                        graphics.Shaders.World3D.Desaturation = sector.Sector.Desaturation;

						// villsa. Doom 64 linedefs can mirror their textures
						if(General.Map.DOOM64) SetTextureMirror(g.Sidedef);

						// Apply changes
						graphics.Shaders.World3D.ApplySettings();
						
						// Render!
						graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);
					}
				}
			}

			SetTextureMirror(null); // villsa

			// Get things for this pass
			if(thingspass.Count > 0)
			{
				// Texture addressing
				graphics.Device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Clamp);
				graphics.Device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Clamp);
				graphics.Device.SetSamplerState(0, SamplerState.AddressW, TextureAddress.Clamp);
				graphics.Device.SetRenderState(RenderState.CullMode, Cull.None); //mxd. Disable backside culling, because otherwise sprites with positive ScaleY and negative ScaleX will be facing away from the camera...

				Color4 vertexcolor = new Color4(); //mxd

				// Render things collected
				foreach(KeyValuePair<ImageData, List<VisualThing>> group in thingspass)
				{
					if(group.Key is UnknownImage) continue;
					
					// What texture to use?
					if(!group.Key.IsImageLoaded || group.Key.IsDisposed)
						curtexture = General.Map.Data.Hourglass3D;
					else 
						curtexture = group.Key;

					// Create Direct3D texture if still needed
					if((curtexture.Texture == null) || curtexture.Texture.Disposed)
						curtexture.CreateTexture();

					// Apply texture
					graphics.Shaders.World3D.Texture1 = curtexture.Texture;

					// Render all things with this texture
					foreach(VisualThing t in group.Value)
					{
						// Update buffer if needed
						t.Update();

                        //mxd. Check 3D distance
                        if (t.Info.DistanceCheckSq < int.MaxValue && (t.Thing.Position - cameraposition).GetLengthSq() > t.Info.DistanceCheckSq)
							continue;

						// Only do this sector when a vertexbuffer is created
						if(t.GeometryBuffer != null) 
						{
							// Determine the shader pass we want to use for this object
							int wantedshaderpass = (((t == highlighted) && showhighlight) || (t.Selected && showselection)) ? highshaderpass : shaderpass;

							//mxd. If fog is enagled, switch to shader, which calculates it
							if(General.Settings.GZDrawFog && !fullbrightness && t.Thing.Sector != null && (doom64fog || t.Thing.Sector.FogMode != SectorFogMode.NONE)) // styd
								wantedshaderpass += 8;

							//mxd. Create the matrix for positioning
							world = CreateThingPositionMatrix(t);

							//mxd. If current thing is light - set it's color to light color
							if(t.LightType != null && t.LightType.LightInternal && !fullbrightness)
							{
								wantedshaderpass += 4; // Render using one of passes, which uses World3D.VertexColor
								vertexcolor = t.LightColor;
							}
							//mxd. Check if Thing is affected by dynamic lights and set color accordingly
							else if(General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && lightthings.Count > 0)
							{
								Color4 litcolor = GetLitColorForThing(t);
								if(litcolor.ToArgb() != 0)
								{
									wantedshaderpass += 4; // Render using one of passes, which uses World3D.VertexColor
									vertexcolor = new Color4(t.VertexColor) + litcolor;
								}
							}
							else
							{
								vertexcolor = new Color4();
							}

							// styd. A thing in a sector of Doom 64 that has a light at this time gets it
							wantedshaderpass += SetDoom64LightPass(t, wantedshaderpass);

							// Switch shader pass?
							if(currentshaderpass != wantedshaderpass) 
							{
								graphics.Shaders.World3D.EndPass();
								graphics.Shaders.World3D.BeginPass(wantedshaderpass);
								currentshaderpass = wantedshaderpass;
							}

							//mxd. Set variables for fog rendering?
							if(wantedshaderpass > 7)
							{
								graphics.Shaders.World3D.World = world;
                                graphics.Shaders.World3D.ModelNormal = Matrix.Identity;
                                graphics.Shaders.World3D.CameraPosition = new Vector4(cameraposition.x, cameraposition.y, cameraposition.z, t.FogFactor);
							}

							// Set the colors to use
							if(t.Thing.Sector != null) graphics.Shaders.World3D.LightColor = (doom64fog ? doom64fogcolor : t.Thing.Sector.FogColor); // styd
							graphics.Shaders.World3D.VertexColor = vertexcolor;
							graphics.Shaders.World3D.HighlightColor = CalculateHighlightColor((t == highlighted) && showhighlight, (t.Selected && showselection));

                            // [ZZ] check if we want stencil
                            graphics.Shaders.World3D.StencilColor = t.StencilColor.ToColorValue();

                            // [ZZ] apply desaturation
                            if (t.Thing.Sector != null)
                                graphics.Shaders.World3D.Desaturation = t.Thing.Sector.Desaturation;
                            else graphics.Shaders.World3D.Desaturation = 0;

                            // Apply changes
                            ApplyMatrices3D();
							graphics.Shaders.World3D.ApplySettings();

							// Apply buffer
							graphics.Device.SetStreamSource(0, t.GeometryBuffer, 0, WorldVertex.Stride);

							// Render!
							graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, 0, t.Triangles);
                        }
					}

                    // [ZZ]
                    graphics.Shaders.World3D.StencilColor = new Color4(0f, 1f, 1f, 1f);
                }

                // Texture addressing
                graphics.Device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Wrap);
				graphics.Device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Wrap);
				graphics.Device.SetSamplerState(0, SamplerState.AddressW, TextureAddress.Wrap);
				graphics.Device.SetRenderState(RenderState.CullMode, Cull.Counterclockwise); //mxd
			}

			// Done rendering with this shader
			graphics.Shaders.World3D.EndPass();
		}

		// villsa. This sets the texture addressing for a Doom 64 sidedef: the linedef flags
		// "UV Wrap H Mirror" and "UV Wrap V Mirror" mirror the texture on every other repeat.
		// Call with null to go back to normal wrapping.
		private void SetTextureMirror(Sidedef sd)
		{
			bool wantu = ((sd != null) && sd.Line.IsFlagSet("1073741824"));
			bool wantv = ((sd != null) && sd.Line.IsFlagSet("2147483648"));

			if(wantu != mirroru)
			{
				graphics.Device.SetSamplerState(0, SamplerState.AddressU, (wantu ? TextureAddress.Mirror : TextureAddress.Wrap));
				mirroru = wantu;
			}

			if(wantv != mirrorv)
			{
				graphics.Device.SetSamplerState(0, SamplerState.AddressV, (wantv ? TextureAddress.Mirror : TextureAddress.Wrap));
				mirrorv = wantv;
			}
		}

		//mxd
		private void RenderTranslucentPass(List<VisualGeometry> geopass, List<VisualThing> thingspass)
		{
			int currentshaderpass = shaderpass;
			int highshaderpass = shaderpass + 2;

			// Sort geometry by camera distance. First vertex of the BoundingBox is it's center
            geopass.Sort(delegate(VisualGeometry vg1, VisualGeometry vg2)
			{
                /*if(vg1 == vg2) return 0;
				return (int)((General.Map.VisualCamera.Position - vg2.BoundingBox[0]).GetLengthSq()
					        -(General.Map.VisualCamera.Position - vg1.BoundingBox[0]).GetLengthSq());*/

                // This does not work when you have huge translucent 3D floor combined with small translucent something over it.
                // The huge translucent 3D floor may easily have it's center CLOSER and thus get drawn over everything, which is certainly not expected behavior.

                if (vg1 == vg2)
                    return 0;

                double dist1, dist2;
                Vector3D cameraPos = General.Map.VisualCamera.Position;
                Vector2D cameraPos2 = new Vector2D(cameraPos);

                // if one of the things being compared is a plane, use easier formula. (3d floor compatibility)
                if (vg1.GeometryType == VisualGeometryType.FLOOR || vg1.GeometryType == VisualGeometryType.CEILING ||
                    vg2.GeometryType == VisualGeometryType.FLOOR || vg2.GeometryType == VisualGeometryType.CEILING)
                {
                    // more magic
                    dist1 = Math.Abs(vg1.BoundingBox[0].z - cameraPos.z);
                    dist2 = Math.Abs(vg2.BoundingBox[0].z - cameraPos.z);
                }
                else
                {
                    dist1 = (General.Map.VisualCamera.Position - vg1.BoundingBox[0]).GetLengthSq();
                    dist2 = (General.Map.VisualCamera.Position - vg2.BoundingBox[0]).GetLengthSq();
                }

                return (int)(dist2 - dist1);
			});

			ImageData curtexture;
			VisualSector sector = null;
			RenderPass currentpass = RenderPass.Solid;
			long curtexturename = 0;
			float fogfactor = -1;

			// Begin rendering with this shader
			graphics.Shaders.World3D.BeginPass(shaderpass);

			// Go for all geometry
			foreach(VisualGeometry g in geopass)
			{
				// Change blend mode?
				if(g.RenderPass != currentpass)
				{
					switch(g.RenderPass)
					{
						case RenderPass.Additive:
							graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.One);
							break;

						case RenderPass.Alpha:
							graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
							break;
					}

					currentpass = g.RenderPass;
				}

				// Change texture?
				if(g.Texture.LongName != curtexturename)
				{
					// What texture to use?
					if(g.Texture is UnknownImage)
						curtexture = General.Map.Data.UnknownTexture3D;
					else if(g.Texture.IsImageLoaded && !g.Texture.IsDisposed)
						curtexture = g.Texture;
					else
						curtexture = General.Map.Data.Hourglass3D;

					// styd. An animated texture of Doom 64 is drawn with its picture of the moment
					if(General.Map.DOOM64) curtexture = General.Map.Data.GetDoom64AnimationFrame(curtexture);

					// Create Direct3D texture if still needed
					if((curtexture.Texture == null) || curtexture.Texture.Disposed)
						curtexture.CreateTexture();

					// Apply texture (villsa: or none, to show the lighting only)
					graphics.Shaders.World3D.Texture1 = (showlightonly ? General.Map.Data.WhiteTexture.Texture : curtexture.Texture);
					curtexturename = g.Texture.LongName;
				}

				// Changing sector?
				if(!object.ReferenceEquals(g.Sector, sector))
				{
					// Update the sector if needed
					if(g.Sector.NeedsUpdateGeo) g.Sector.Update();

					// Only do this sector when a vertexbuffer is created
					//mxd. No Map means that sector was deleted recently, I suppose
					if(g.Sector.GeometryBuffer != null && g.Sector.Sector.Map != null)
					{
						// Change current sector
						sector = g.Sector;

						// Set stream source
						graphics.Device.SetStreamSource(0, sector.GeometryBuffer, 0, WorldVertex.Stride);
					}
					else
					{
						sector = null;
					}
				}

                if (sector != null)
                {
                    // Determine the shader pass we want to use for this object
                    int wantedshaderpass = (((g == highlighted) && showhighlight) || (g.Selected && showselection)) ? highshaderpass : shaderpass;

                    //mxd. Render fog?
                    if (General.Settings.GZDrawFog && !fullbrightness && (doom64fog || sector.Sector.FogMode != SectorFogMode.NONE)) // styd
                        wantedshaderpass += 8;

                    // styd. The liquid floors of Doom 64, and what the game has scrolled
                    wantedshaderpass += SetDoom64ScrollPass(g);

                    // Switch shader pass?
                    if (currentshaderpass != wantedshaderpass)
                    {
                        graphics.Shaders.World3D.EndPass();
                        graphics.Shaders.World3D.BeginPass(wantedshaderpass);
                        currentshaderpass = wantedshaderpass;

                        //mxd. Set variables for fog rendering?
                        if (wantedshaderpass > 7)
                        {
                            graphics.Shaders.World3D.World = world;
                            graphics.Shaders.World3D.ModelNormal = Matrix.Identity;
                        }
                    }

                    // Set variables for fog rendering?
                    if (wantedshaderpass > 7 && g.FogFactor != fogfactor)
                    {
                        graphics.Shaders.World3D.CameraPosition = new Vector4(cameraposition.x, cameraposition.y, cameraposition.z, g.FogFactor);
                        fogfactor = g.FogFactor;
                    }

                    //
                    graphics.Shaders.World3D.Desaturation = sector.Sector.Desaturation;

                    // Set the colors to use
                    graphics.Shaders.World3D.LightColor = (doom64fog ? doom64fogcolor : sector.Sector.FogColor); // styd
                    graphics.Shaders.World3D.HighlightColor = CalculateHighlightColor((g == highlighted) && showhighlight, (g.Selected && showselection));

                    // villsa. Doom 64 linedefs can mirror their textures
                    if(General.Map.DOOM64) SetTextureMirror(g.Sidedef);

                    // Apply changes
                    graphics.Shaders.World3D.ApplySettings();

                    // Render!
                    graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);
                }
                else graphics.Shaders.World3D.Desaturation = 0f;
            }

			SetTextureMirror(null); // villsa

			// Get things for this pass
			if(thingspass.Count > 0)
			{
				// Texture addressing
				graphics.Device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Clamp);
				graphics.Device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Clamp);
				graphics.Device.SetSamplerState(0, SamplerState.AddressW, TextureAddress.Clamp);
				graphics.Device.SetRenderState(RenderState.CullMode, Cull.None); //mxd. Disable backside culling, because otherwise sprites with positive ScaleY and negative ScaleX will be facing away from the camera...

				// Sort geometry by camera distance. First vertex of the BoundingBox is it's center
				thingspass.Sort(delegate(VisualThing vt1, VisualThing vt2)
				{
					if(vt1 == vt2) return 0;
					return (int)((General.Map.VisualCamera.Position - vt2.BoundingBox[0]).GetLengthSq()
							   - (General.Map.VisualCamera.Position - vt1.BoundingBox[0]).GetLengthSq());
				});

				// Reset vars
				currentpass = RenderPass.Solid;
				curtexturename = 0;
				Color4 vertexcolor = new Color4();
				fogfactor = -1;
				bool nightmare = false; // styd

				// Render things collected
				foreach(VisualThing t in thingspass)
				{
					// Update buffer if needed
					t.Update();

					//mxd. Check 3D distance
					if(t.Info.DistanceCheckSq < int.MaxValue && (t.Thing.Position - cameraposition).GetLengthSq() > t.Info.DistanceCheckSq)
						continue;
					
					t.UpdateSpriteFrame(); // Set correct texture, geobuffer and triangles count
					if(t.Texture is UnknownImage) continue;
					
					// Change blend mode?
					if(t.RenderPass != currentpass)
					{
						switch(t.RenderPass)
						{
							case RenderPass.Additive:
								graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.One);
								break;

							case RenderPass.Alpha:
								graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
								break;
						}

						currentpass = t.RenderPass;
					}

					// styd. Doom 64: a thing with the Nightmare flag is blended with its own colors,
					// as Doom64 EX does (DL_ProcessDrawList): where its color is dark, what is behind
					// it shows. The pixels that its picture leaves out are not drawn.
					if(t.Doom64Nightmare != nightmare)
					{
						nightmare = t.Doom64Nightmare;
						graphics.Device.SetRenderState(RenderState.SourceBlend, (nightmare ? Blend.SourceColor : Blend.SourceAlpha));
						graphics.Device.SetRenderState(RenderState.DestinationBlend, (nightmare ? Blend.InverseSourceColor
							: (t.RenderPass == RenderPass.Additive ? Blend.One : Blend.InverseSourceAlpha)));
						graphics.Device.SetRenderState(RenderState.AlphaTestEnable, nightmare);
					}

					// Change texture?
					if(t.Texture.LongName != curtexturename)
					{
						// What texture to use?
						if(t.Texture.IsImageLoaded && !t.Texture.IsDisposed)
							curtexture = t.Texture;
						else
							curtexture = General.Map.Data.Hourglass3D;

						// Create Direct3D texture if still needed
						if((curtexture.Texture == null) || curtexture.Texture.Disposed)
							curtexture.CreateTexture();

						// Apply texture
						graphics.Shaders.World3D.Texture1 = curtexture.Texture;
						curtexturename = t.Texture.LongName;
					}

					// Only do this sector when a vertexbuffer is created
					if(t.GeometryBuffer != null)
					{
						// Determine the shader pass we want to use for this object
						int wantedshaderpass = (((t == highlighted) && showhighlight) || (t.Selected && showselection)) ? highshaderpass : shaderpass;

						//mxd. if fog is enagled, switch to shader, which calculates it
						if(General.Settings.GZDrawFog && !fullbrightness && t.Thing.Sector != null && (doom64fog || t.Thing.Sector.FogMode != SectorFogMode.NONE)) // styd
							wantedshaderpass += 8;

						// styd. The green of a Doom 64 thing with the Nightmare flag is not a light:
						// it stays when everything is shown at full brightness
						if(t.Doom64Nightmare && fullbrightness) wantedshaderpass -= 1;

						//mxd. Create the matrix for positioning
						world = CreateThingPositionMatrix(t);

						//mxd. If current thing is light - set it's color to light color
						if(t.LightType != null && t.LightType.LightInternal && !fullbrightness)
						{
							wantedshaderpass += 4; // Render using one of passes, which uses World3D.VertexColor
							vertexcolor = t.LightColor;
						}
						//mxd. Check if Thing is affected by dynamic lights and set color accordingly
						else if(General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && lightthings.Count > 0)
						{
							Color4 litcolor = GetLitColorForThing(t);
							if(litcolor.ToArgb() != 0)
							{
								wantedshaderpass += 4; // Render using one of passes, which uses World3D.VertexColor
								vertexcolor = new Color4(t.VertexColor) + litcolor;
							}
						}
						else
						{
							vertexcolor = new Color4();
						}

						// styd. A thing in a sector of Doom 64 that has a light at this time gets it
						wantedshaderpass += SetDoom64LightPass(t, wantedshaderpass);

						// Switch shader pass?
						if(currentshaderpass != wantedshaderpass)
						{
							graphics.Shaders.World3D.EndPass();
							graphics.Shaders.World3D.BeginPass(wantedshaderpass);
							currentshaderpass = wantedshaderpass;
						}

						//mxd. Set variables for fog rendering?
						if(wantedshaderpass > 7)
						{
							graphics.Shaders.World3D.World = world;
                            graphics.Shaders.World3D.ModelNormal = Matrix.Identity;
                            if (t.FogFactor != fogfactor)
							{
								graphics.Shaders.World3D.CameraPosition = new Vector4(cameraposition.x, cameraposition.y, cameraposition.z, t.FogFactor);
								fogfactor = t.FogFactor;
							}
						}

						// Set the colors to use
						graphics.Shaders.World3D.LightColor = (doom64fog ? doom64fogcolor : t.Thing.Sector.FogColor); // styd
						graphics.Shaders.World3D.VertexColor = vertexcolor;
						graphics.Shaders.World3D.HighlightColor = CalculateHighlightColor((t == highlighted) && showhighlight, (t.Selected && showselection));

                        // [ZZ] check if we want stencil
                        graphics.Shaders.World3D.StencilColor = t.StencilColor.ToColorValue();

                        //
                        graphics.Shaders.World3D.Desaturation = t.Thing.Sector.Desaturation;

                        // Apply changes
                        ApplyMatrices3D();
						graphics.Shaders.World3D.ApplySettings();

						// Apply buffer
						graphics.Device.SetStreamSource(0, t.GeometryBuffer, 0, WorldVertex.Stride);

						// Render!
						graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, 0, t.Triangles);
					}
				}

				// styd. Back to the blending of this pass
				if(nightmare)
				{
					graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
					graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
					graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
				}

                // [ZZ] check if we want stencil
                graphics.Shaders.World3D.StencilColor = new Color4(0f, 1f, 1f, 1f);

                // Texture addressing
                graphics.Device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Wrap);
				graphics.Device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Wrap);
				graphics.Device.SetSamplerState(0, SamplerState.AddressW, TextureAddress.Wrap);
				graphics.Device.SetRenderState(RenderState.CullMode, Cull.Counterclockwise); //mxd
			}

			// Done rendering with this shader
			graphics.Shaders.World3D.EndPass();
		}

		//mxd
		private Matrix CreateThingPositionMatrix(VisualThing t)
		{
			// Use normal ThingRenderMode when model rendering is disabled for this thing
			ThingRenderMode rendermode = t.Thing.RenderMode;
			if((t.Thing.RenderMode == ThingRenderMode.MODEL || t.Thing.RenderMode == ThingRenderMode.VOXEL) &&
			   (General.Settings.GZDrawModelsMode == ModelRenderMode.NONE ||
			   (General.Settings.GZDrawModelsMode == ModelRenderMode.SELECTION && !t.Selected)))
			{
				rendermode = ThingRenderMode.NORMAL;
			}
			
			// Create the matrix for positioning
			switch(rendermode)
			{
				case ThingRenderMode.NORMAL:
					if(t.Info.XYBillboard) // Apply billboarding?
					{
						return Matrix.Translation(0f, 0f, -t.LocalCenterZ)
							* Matrix.RotationX(Angle2D.PI - General.Map.VisualCamera.AngleZ)
							* Matrix.Translation(0f, 0f, t.LocalCenterZ)
							* billboard
							* t.Position;
					}
					return billboard * t.Position;

				case ThingRenderMode.FLATSPRITE:
				case ThingRenderMode.WALLSPRITE:
				case ThingRenderMode.MODEL:
				case ThingRenderMode.VOXEL:
					return t.Position;

				default: throw new NotImplementedException("Unknown ThingRenderMode");
			}
		}

        private float CosDeg(float angle)
        {
            return (float)Math.Cos(Angle2D.DegToRad(angle));
        }

        //mxd. Dynamic lights pass!
        private VisualSector RenderLightsFromGeometryList(List<VisualGeometry> geometrytolit, List<VisualThing> lights, VisualSector sector, bool settexture)
        {
            foreach (VisualGeometry g in geometrytolit)
            {
                // Changing sector?
                if (!object.ReferenceEquals(g.Sector, sector))
                {
                    // Only do this sector when a vertexbuffer is created
                    // mxd. no Map means that sector was deleted recently, I suppose
                    if (g.Sector.GeometryBuffer != null && g.Sector.Sector.Map != null)
                    {
                        // Change current sector
                        sector = g.Sector;

                        // Set stream source
                        graphics.Device.SetStreamSource(0, sector.GeometryBuffer, 0, WorldVertex.Stride);
                    }
                    else
                    {
                        sector = null;
                    }
                }

                if (sector == null) continue;

                graphics.Shaders.World3D.Desaturation = sector.Sector.Desaturation;

                // note: additive geometry doesn't receive lighting
                if (g.RenderPass == RenderPass.Additive)
                    continue;

                if (settexture)
                    graphics.Shaders.World3D.Texture1 = g.Texture.Texture;

                //normal lights
                int count = lightOffsets[0];
                Vector4 lpr;
                if (lightOffsets[0] > 0)
                {
                    graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);

                    for (int i = 0; i < count; i++)
                    {
                        if (BoundingBoxesIntersect(g.BoundingBox, lights[i].BoundingBox))
                        {
                            lpr = new Vector4(lights[i].Center, lights[i].LightRadius);
                            if (lpr.W == 0) continue;
                            graphics.Shaders.World3D.LightColor = lights[i].LightColor;
                            graphics.Shaders.World3D.LightPositionAndRadius = lpr;
                            GZGeneral.LightData ld = lights[i].LightType;
                            if (ld.LightType == GZGeneral.LightType.SPOT)
                            {
                                graphics.Shaders.World3D.SpotLight = true;
                                graphics.Shaders.World3D.LightOrientation = lights[i].VectorLookAt;
                                graphics.Shaders.World3D.Light2Radius = new Vector2(CosDeg(lights[i].LightSpotRadius1), CosDeg(lights[i].LightSpotRadius2));
                            }
                            else graphics.Shaders.World3D.SpotLight = false;
                            graphics.Shaders.World3D.ApplySettings();
                            graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);
                        }
                    }
                }

                //attenuated lights
                if (lightOffsets[1] > 0)
                {
                    count += lightOffsets[1];
                    graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);

                    for (int i = lightOffsets[0]; i < count; i++)
                    {
                        if (BoundingBoxesIntersect(g.BoundingBox, lights[i].BoundingBox))
                        {
                            lpr = new Vector4(lights[i].Center, lights[i].LightRadius);
                            if (lpr.W == 0) continue;
                            graphics.Shaders.World3D.LightColor = lights[i].LightColor;
                            graphics.Shaders.World3D.LightPositionAndRadius = lpr;
                            GZGeneral.LightData ld = lights[i].LightType;
                            if (ld.LightType == GZGeneral.LightType.SPOT)
                            {
                                graphics.Shaders.World3D.SpotLight = true;
                                graphics.Shaders.World3D.LightOrientation = lights[i].VectorLookAt;
                                graphics.Shaders.World3D.Light2Radius = new Vector2(CosDeg(lights[i].LightSpotRadius1), CosDeg(lights[i].LightSpotRadius2));
                            }
                            else graphics.Shaders.World3D.SpotLight = false;
                            graphics.Shaders.World3D.ApplySettings();
                            graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);
                        }
                    }
                }

                //additive lights
                if (lightOffsets[2] > 0)
                {
                    count += lightOffsets[2];
                    graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);

                    for (int i = lightOffsets[0] + lightOffsets[1]; i < count; i++)
                    {
                        if (BoundingBoxesIntersect(g.BoundingBox, lights[i].BoundingBox))
                        {
                            lpr = new Vector4(lights[i].Center, lights[i].LightRadius);
                            if (lpr.W == 0) continue;
                            graphics.Shaders.World3D.LightColor = lights[i].LightColor;
                            graphics.Shaders.World3D.LightPositionAndRadius = lpr;
                            GZGeneral.LightData ld = lights[i].LightType;
                            if (ld.LightType == GZGeneral.LightType.SPOT)
                            {
                                graphics.Shaders.World3D.SpotLight = true;
                                graphics.Shaders.World3D.LightOrientation = lights[i].VectorLookAt;
                                graphics.Shaders.World3D.Light2Radius = new Vector2(CosDeg(lights[i].LightSpotRadius1), CosDeg(lights[i].LightSpotRadius2));
                            }
                            else graphics.Shaders.World3D.SpotLight = false;
                            graphics.Shaders.World3D.ApplySettings();
                            graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);
                        }
                    }
                }

                //negative lights
                if (lightOffsets[3] > 0)
                {
                    count += lightOffsets[3];
                    graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.ReverseSubtract);

                    for (int i = lightOffsets[0] + lightOffsets[1] + lightOffsets[2]; i < count; i++)
                    {
                        if (BoundingBoxesIntersect(g.BoundingBox, lights[i].BoundingBox))
                        {
                            lpr = new Vector4(lights[i].Center, lights[i].LightRadius);
                            if (lpr.W == 0) continue;
                            Color4 lc = lights[i].LightColor;
                            graphics.Shaders.World3D.LightColor = new Color4(lc.Alpha, (lc.Green + lc.Blue) / 2, (lc.Red + lc.Blue) / 2, (lc.Green + lc.Red) / 2);
                            graphics.Shaders.World3D.LightPositionAndRadius = lpr;
                            GZGeneral.LightData ld = lights[i].LightType;
                            if (ld.LightType == GZGeneral.LightType.SPOT)
                            {
                                graphics.Shaders.World3D.SpotLight = true;
                                graphics.Shaders.World3D.LightOrientation = lights[i].VectorLookAt;
                                graphics.Shaders.World3D.Light2Radius = new Vector2(CosDeg(lights[i].LightSpotRadius1), CosDeg(lights[i].LightSpotRadius2));
                            }
                            else graphics.Shaders.World3D.SpotLight = false;
                            graphics.Shaders.World3D.ApplySettings();
                            graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);
                        }
                    }
                }
            }

            return sector;
        }
        
        // [ZZ] split into RenderLights and RenderTranslucentLights
        private void RenderTranslucentLights(List<VisualGeometry> geometrytolit, List<VisualThing> lights)
        {
            if (geometrytolit.Count == 0) return;

            graphics.Shaders.World3D.World = Matrix.Identity;
            graphics.Shaders.World3D.ModelNormal = Matrix.Identity;
            graphics.Shaders.World3D.BeginPass(SHADERPASS_LIGHT);

            VisualSector sector = null;

            graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.One);
            graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.BlendFactor);

            //
            RenderLightsFromGeometryList(geometrytolit, lights, sector, true);

            //
            graphics.Shaders.World3D.EndPass();
            graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);
        }

        //
        private void RenderLights(Dictionary<ImageData, List<VisualGeometry>> geometrytolit, List<VisualThing> lights)
        {
            // Anything to do?
            if (geometrytolit.Count == 0) return;

            graphics.Shaders.World3D.World = Matrix.Identity;
            graphics.Shaders.World3D.ModelNormal = Matrix.Identity;
            graphics.Shaders.World3D.BeginPass(SHADERPASS_LIGHT);

            VisualSector sector = null;

            graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.One);
            graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.BlendFactor);

            foreach (KeyValuePair<ImageData, List<VisualGeometry>> group in geometrytolit)
            {
                if (group.Key.Texture == null) continue;
                graphics.Shaders.World3D.Texture1 = group.Key.Texture;

                sector = RenderLightsFromGeometryList(group.Value, lights, sector, false);
            }

            graphics.Shaders.World3D.EndPass();
            graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);
        }

        //mxd. Render models
        private void RenderModels(bool lightpass, bool trans) 
		{
			int shaderpass = (fullbrightness ? 1 : 4);
			int currentshaderpass = shaderpass;
			int highshaderpass = shaderpass + 2;

            RenderPass currentpass = RenderPass.Solid;

            // Begin rendering with this shader
            if (!lightpass)
            {
                graphics.Shaders.World3D.BeginPass(currentshaderpass);
            }
            else
            {
                graphics.Shaders.World3D.BeginPass(SHADERPASS_LIGHT);
            }

            List<VisualThing> things;
            if (trans)
            {
                // Sort models by camera distance. First vertex of the BoundingBox is it's center
                translucentmodelthings.Sort((vt1, vt2) => (int)((General.Map.VisualCamera.Position - vt2.BoundingBox[0]).GetLengthSq()
                                              - (General.Map.VisualCamera.Position - vt1.BoundingBox[0]).GetLengthSq()));
                things = translucentmodelthings;
            }
            else
            {
                things = new List<VisualThing>();
                foreach (KeyValuePair<ModelData, List<VisualThing>> group in maskedmodelthings)
                    foreach (VisualThing t in group.Value)
                        things.Add(t);
            }
            
			foreach(VisualThing t in things) 
			{
                if (trans)
                {
                    // Change blend mode?
                    if (t.RenderPass != currentpass)
                    {
                        switch (t.RenderPass)
                        {
                            case RenderPass.Additive:
                                graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.One);
                                break;

                            case RenderPass.Alpha:
                                graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
                                break;
                        }

                        currentpass = t.RenderPass;
                    }
                }

				// Update buffer if needed
				t.Update();

				// Check 3D distance
				if(t.Info.DistanceCheckSq < int.MaxValue && (t.Thing.Position - cameraposition).GetLengthSq() > t.Info.DistanceCheckSq)
					continue;
					
				Color4 vertexcolor = new Color4(t.VertexColor);

                // Check if model is affected by dynamic lights and set color accordingly
                graphics.Shaders.World3D.VertexColor = vertexcolor;

				// Determine the shader pass we want to use for this object
				int wantedshaderpass = ((((t == highlighted) && showhighlight) || (t.Selected && showselection)) ? highshaderpass : shaderpass);

				// If fog is enagled, switch to shader, which calculates it
				if (General.Settings.GZDrawFog && !fullbrightness && t.Thing.Sector != null && (doom64fog || t.Thing.Sector.FogMode != SectorFogMode.NONE)) // styd
					wantedshaderpass += 8;

				// Switch shader pass?
				if (!lightpass && currentshaderpass != wantedshaderpass)
				{
					graphics.Shaders.World3D.EndPass();
					graphics.Shaders.World3D.BeginPass(wantedshaderpass);
					currentshaderpass = wantedshaderpass;
				}

				// Set the colors to use
				graphics.Shaders.World3D.HighlightColor = CalculateHighlightColor((t == highlighted) && showhighlight, (t.Selected && showselection));

				// Create the matrix for positioning / rotation
				float sx = t.Thing.ScaleX * t.Thing.ActorScale.Width;
				float sy = t.Thing.ScaleY * t.Thing.ActorScale.Height;
                
				Matrix modelscale = Matrix.Scaling(sx, sx, sy);
				Matrix modelrotation = Matrix.RotationY(-t.Thing.RollRad) * Matrix.RotationX(-t.Thing.PitchRad) * Matrix.RotationZ(t.Thing.Angle);

				world = General.Map.Data.ModeldefEntries[t.Thing.Type].Transform * modelscale * modelrotation * t.Position;
				ApplyMatrices3D();

				// Set variables for fog rendering
				if(wantedshaderpass > 7)
				{
					graphics.Shaders.World3D.World = world;
                    // this is not right...
                    graphics.Shaders.World3D.ModelNormal = General.Map.Data.ModeldefEntries[t.Thing.Type].TransformRotation * modelrotation;
                    if (t.Thing.Sector != null) graphics.Shaders.World3D.LightColor = (doom64fog ? doom64fogcolor : t.Thing.Sector.FogColor); // styd
					graphics.Shaders.World3D.CameraPosition = new Vector4(cameraposition.x, cameraposition.y, cameraposition.z, t.FogFactor);
				}

                if (t.Thing.Sector != null)
                    graphics.Shaders.World3D.Desaturation = t.Thing.Sector.Desaturation;
                else graphics.Shaders.World3D.Desaturation = 0;

                GZModel model = General.Map.Data.ModeldefEntries[t.Thing.Type].Model;
                for (int j = 0; j < model.Meshes.Count; j++)
                {
                    graphics.Shaders.World3D.Texture1 = model.Textures[j];
                    graphics.Shaders.World3D.ApplySettings();

                    if (!lightpass)
                    {
                        // Render!
                        model.Meshes[j].DrawSubset(0);
                    }
                    else if (lightpass && t.RenderPass != RenderPass.Additive) // additive stuff does not get any lighting
                    {
                        List<VisualThing> lights = lightthings;
                        //
                        int count = lightOffsets[0];
                        Vector4 lpr;

                        // normal lights
                        if (lightOffsets[0] > 0)
                        {
                            graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);

                            for (int i = 0; i < count; i++)
                            {
                                if (BoundingBoxesIntersect(t.BoundingBox, lights[i].BoundingBox))
                                {
                                    lpr = new Vector4(lights[i].Center, lights[i].LightRadius);
                                    if (lpr.W == 0) continue;
                                    graphics.Shaders.World3D.LightColor = lights[i].LightColor;
                                    graphics.Shaders.World3D.LightPositionAndRadius = lpr;
                                    GZGeneral.LightData ld = lights[i].LightType;
                                    if (ld.LightType == GZGeneral.LightType.SPOT)
                                    {
                                        graphics.Shaders.World3D.SpotLight = true;
                                        graphics.Shaders.World3D.LightOrientation = lights[i].VectorLookAt;
                                        graphics.Shaders.World3D.Light2Radius = new Vector2(CosDeg(lights[i].LightSpotRadius1), CosDeg(lights[i].LightSpotRadius2));
                                    }
                                    else graphics.Shaders.World3D.SpotLight = false;
                                    graphics.Shaders.World3D.ApplySettings();
                                    model.Meshes[j].DrawSubset(0);
                                }
                            }
                        }

                        //attenuated lights
                        if (lightOffsets[1] > 0)
                        {
                            count += lightOffsets[1];
                            graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);

                            for (int i = lightOffsets[0]; i < count; i++)
                            {
                                if (BoundingBoxesIntersect(t.BoundingBox, lights[i].BoundingBox))
                                {
                                    lpr = new Vector4(lights[i].Center, lights[i].LightRadius);
                                    if (lpr.W == 0) continue;
                                    graphics.Shaders.World3D.LightColor = lights[i].LightColor;
                                    graphics.Shaders.World3D.LightPositionAndRadius = lpr;
                                    GZGeneral.LightData ld = lights[i].LightType;
                                    if (ld.LightType == GZGeneral.LightType.SPOT)
                                    {
                                        graphics.Shaders.World3D.SpotLight = true;
                                        graphics.Shaders.World3D.LightOrientation = lights[i].VectorLookAt;
                                        graphics.Shaders.World3D.Light2Radius = new Vector2(CosDeg(lights[i].LightSpotRadius1), CosDeg(lights[i].LightSpotRadius2));
                                    }
                                    else graphics.Shaders.World3D.SpotLight = false;
                                    graphics.Shaders.World3D.ApplySettings();
                                    model.Meshes[j].DrawSubset(0);
                                }
                            }
                        }

                        //additive lights
                        if (lightOffsets[2] > 0)
                        {
                            count += lightOffsets[2];
                            graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);

                            for (int i = lightOffsets[0] + lightOffsets[1]; i < count; i++)
                            {
                                if (BoundingBoxesIntersect(t.BoundingBox, lights[i].BoundingBox))
                                {
                                    lpr = new Vector4(lights[i].Center, lights[i].LightRadius);
                                    if (lpr.W == 0) continue;
                                    graphics.Shaders.World3D.LightColor = lights[i].LightColor;
                                    graphics.Shaders.World3D.LightPositionAndRadius = lpr;
                                    GZGeneral.LightData ld = lights[i].LightType;
                                    if (ld.LightType == GZGeneral.LightType.SPOT)
                                    {
                                        graphics.Shaders.World3D.SpotLight = true;
                                        graphics.Shaders.World3D.LightOrientation = lights[i].VectorLookAt;
                                        graphics.Shaders.World3D.Light2Radius = new Vector2(CosDeg(lights[i].LightSpotRadius1), CosDeg(lights[i].LightSpotRadius2));
                                    }
                                    else graphics.Shaders.World3D.SpotLight = false;
                                    graphics.Shaders.World3D.ApplySettings();
                                    model.Meshes[j].DrawSubset(0);
                                }
                            }
                        }

                        //negative lights
                        if (lightOffsets[3] > 0)
                        {
                            count += lightOffsets[3];
                            graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.ReverseSubtract);

                            for (int i = lightOffsets[0] + lightOffsets[1] + lightOffsets[2]; i < count; i++)
                            {
                                if (BoundingBoxesIntersect(t.BoundingBox, lights[i].BoundingBox))
                                {
                                    lpr = new Vector4(lights[i].Center, lights[i].LightRadius);
                                    if (lpr.W == 0) continue;
                                    Color4 lc = lights[i].LightColor;
                                    graphics.Shaders.World3D.LightColor = new Color4(lc.Alpha, (lc.Green + lc.Blue) / 2, (lc.Red + lc.Blue) / 2, (lc.Green + lc.Red) / 2);
                                    graphics.Shaders.World3D.LightPositionAndRadius = lpr;
                                    GZGeneral.LightData ld = lights[i].LightType;
                                    if (ld.LightType == GZGeneral.LightType.SPOT)
                                    {
                                        graphics.Shaders.World3D.SpotLight = true;
                                        graphics.Shaders.World3D.LightOrientation = lights[i].VectorLookAt;
                                        graphics.Shaders.World3D.Light2Radius = new Vector2(CosDeg(lights[i].LightSpotRadius1), CosDeg(lights[i].LightSpotRadius2));
                                    }
                                    else graphics.Shaders.World3D.SpotLight = false;
                                    graphics.Shaders.World3D.ApplySettings();
                                    model.Meshes[j].DrawSubset(0);
                                }
                            }
                        }
                    }
                }
			}

			graphics.Shaders.World3D.EndPass();
            if (lightpass) graphics.Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);
        }

		//mxd
		private void RenderSky(IEnumerable<VisualGeometry> geo)
		{
			// styd. The sky of Doom 64 is not a box around the map
			if(General.Map.DOOM64)
			{
				RenderDoom64Sky(geo);
				return;
			}

			VisualSector sector = null;
			
			// Set render settings
			graphics.Shaders.World3D.BeginPass(SHADERPASS_SKYBOX);
			graphics.Shaders.World3D.Texture1 = General.Map.Data.SkyBox;
			graphics.Shaders.World3D.World = world;
			graphics.Shaders.World3D.CameraPosition = new Vector4(cameraposition.x, cameraposition.y, cameraposition.z, 0f);

			foreach(VisualGeometry g in geo)
			{
				// Changing sector?
				if(!object.ReferenceEquals(g.Sector, sector))
				{
					// Update the sector if needed
					if(g.Sector.NeedsUpdateGeo) g.Sector.Update();

					// Only do this sector when a vertexbuffer is created
					//mxd. No Map means that sector was deleted recently, I suppose
					if(g.Sector.GeometryBuffer != null && g.Sector.Sector.Map != null)
					{
						// Change current sector
						sector = g.Sector;

						// Set stream source
						graphics.Device.SetStreamSource(0, sector.GeometryBuffer, 0, WorldVertex.Stride);
					}
					else
					{
						sector = null;
					}
				}

				if(sector != null)
				{
					// Set the colors to use
					graphics.Shaders.World3D.HighlightColor = CalculateHighlightColor((g == highlighted) && showhighlight, (g.Selected && showselection));

					// Apply changes
					graphics.Shaders.World3D.ApplySettings();

					// Render!
					graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);
				}
			}

			graphics.Shaders.World3D.EndPass();
		}

		// styd. This draws the sky of a Doom 64 map where the given geometry is. The game draws its
		// sky flat on its screen before anything else (R_RenderSKY): a picture of the sky is not
		// a place in the map, it only scrolls as the view turns. The layers of the sky are drawn
		// that way here, one over the other. The game cannot look up or down; here the horizon of
		// the sky stays on the horizon of the map.
		private void RenderDoom64Sky(IEnumerable<VisualGeometry> geo)
		{
			List<Doom64SkyLayer> layers = General.Map.Data.Doom64SkyLayers;
			if(layers == null) return;

			// How far the view looks up and how far it has turned
			float level = (float)Math.Sqrt(cameravector.x * cameravector.x + cameravector.y * cameravector.y);
			float pitch = cameravector.z / Math.Max(level, 0.0001f);
			float turn = (float)Math.Atan2(cameravector.y, cameravector.x) / (Angle2D.PI * 2f);

			// Screen pixels of the game in one unit of the view
			float scalex = Doom64Sky.SCREEN_FOCAL / projection.M11;
			float scaley = Doom64Sky.SCREEN_FOCAL / projection.M22;

			foreach(Doom64SkyLayer l in layers)
			{
				int pass = (l.Clouds ? SHADERPASS_DOOM64_SKY_CLOUDS : (l.Smooth ? SHADERPASS_DOOM64_SKY_SMOOTH : SHADERPASS_DOOM64_SKY_PICTURE));

				// A picture scrolls by its width in a quarter turn and starts at the left edge of
				// the view, which is column 160 at the middle of the screen of the game and more on
				// a wider view; the clouds scroll twice in a full turn, and drift
				float scroll = (l.Clouds ? turn * Doom64Sky.CLOUD_TURN - l.ScrollS : scalex - turn * 4f * l.Turn);

				// The colors of the sky behind the clouds, which the lightning makes brighter
				Color4 top = (l.Clouds ? Doom64Sky.Doubled(l.High) : l.TopColor);
				Color4 bottom = (l.Clouds ? Doom64Sky.Doubled(l.Low) : l.BottomColor);

				// A layer in front only covers what its picture covers, as much as its pixels are opaque
				graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, !l.Back);
				graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
				graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);

				graphics.Shaders.World3D.BeginPass(pass);
				graphics.Shaders.World3D.Texture1 = l.Texture;
				graphics.Shaders.World3D.Doom64Sky = new Vector4(scalex, scaley, pitch * Doom64Sky.SCREEN_FOCAL, scroll);
				graphics.Shaders.World3D.Doom64SkyPicture = (l.Clouds ? new Vector4(scalex / scaley, Doom64Sky.CLOUD_OPACITY, 0f, 0f) : new Vector4(l.Width, l.Top, l.Height, l.HalfRow));
				graphics.Shaders.World3D.Doom64SkyMode = new Vector4((l.Back ? 1f : 0f), (l.Mirrored ? 1f : 0f), (l.Clouds ? l.ScrollT : l.VStart), (l.Solid ? 1f : 0f));
				graphics.Shaders.World3D.SetDoom64SkyColors(top, bottom, l.BaseColor);

				VisualSector sector = null;
				foreach(VisualGeometry g in geo)
				{
					// Changing sector?
					if(!object.ReferenceEquals(g.Sector, sector))
					{
						// Update the sector if needed
						if(g.Sector.NeedsUpdateGeo) g.Sector.Update();

						// Only do this sector when a vertexbuffer is created
						if(g.Sector.GeometryBuffer != null && g.Sector.Sector.Map != null)
						{
							sector = g.Sector;
							graphics.Device.SetStreamSource(0, sector.GeometryBuffer, 0, WorldVertex.Stride);
						}
						else
						{
							sector = null;
						}
					}

					if(sector != null)
					{
						// The sky behind a texture with holes is not highlighted: the texture is
						graphics.Shaders.World3D.HighlightColor = (g.RenderAsSky ? CalculateHighlightColor((g == highlighted) && showhighlight, (g.Selected && showselection)) : new Color4());
						graphics.Shaders.World3D.ApplySettings();
						graphics.Device.DrawPrimitives(PrimitiveType.TriangleList, g.VertexOffset, g.Triangles);
					}
				}

				graphics.Shaders.World3D.EndPass();
			}

			graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, false);
		}

        // [ZZ] this is copied from GZDoom
        private float Smoothstep(float edge0, float edge1, float x)
        {
            double t = Math.Min(Math.Max((x - edge0) / (edge1 - edge0), 0.0), 1.0);
            return (float)(t * t * (3.0 - 2.0 * t));
        }

		//mxd. This gets color from dynamic lights based on distance to thing. 
		//thing position must be in absolute cordinates 
		//(thing.Position.Z value is relative to floor of the sector the thing is in)
		private Color4 GetLitColorForThing(VisualThing t) 
		{
			Color4 litColor = new Color4();
			foreach(VisualThing lt in lightthings)
			{
				// Don't light self
				if(General.Map.Data.GldefsEntries.ContainsKey(t.Thing.Type) && General.Map.Data.GldefsEntries[t.Thing.Type].DontLightSelf && t.Thing.Index == lt.Thing.Index)
					continue;

				float distSquared = Vector3.DistanceSquared(lt.Center, t.Center);
                float radiusSquared = lt.LightRadius * lt.LightRadius;
				if(distSquared < radiusSquared) 
				{
                    int sign = (lt.LightType.LightRenderStyle == GZGeneral.LightRenderStyle.SUBTRACTIVE ? -1 : 1);
                    Vector3 L = (t.Center - lt.Center);
                    float dist = L.Length();
                    float scaler = 1 - dist / lt.LightRadius * lt.LightColor.Alpha;

                    if (lt.LightType.LightType == GZGeneral.LightType.SPOT)
                    {
                        Vector3 lookAt = lt.VectorLookAt;
                        L.Normalize();
                        float cosDir = Vector3.Dot(-L, lookAt);
                        scaler *= (float)Smoothstep(CosDeg(lt.LightSpotRadius2), CosDeg(lt.LightSpotRadius1), cosDir);
                    }

                    if (scaler > 0)
                    {
                        litColor.Red += lt.LightColor.Red * scaler * sign;
                        litColor.Green += lt.LightColor.Green * scaler * sign;
                        litColor.Blue += lt.LightColor.Blue * scaler * sign;
                    }
				}
			}

			return litColor;
		}

		// This calculates the highlight/selection color
		private Color4 CalculateHighlightColor(bool ishighlighted, bool isselected)
		{
			if(!ishighlighted && !isselected) return new Color4(); //mxd
			Color4 highlightcolor = isselected ? General.Colors.Selection.ToColorValue() : General.Colors.Highlight.ToColorValue();
			highlightcolor.Alpha = ishighlighted ? highlightglowinv : highlightglow;
			return highlightcolor;
		}
		
		// This finishes rendering
		public void Finish()
		{
			General.Plugins.OnPresentDisplayBegin();

			// Done
			graphics.FinishRendering();
			graphics.Present();
			highlighted = null;
		}
		
		#endregion
		
		#region ================== Rendering
		
		// This sets the highlighted object for the rendering
		public void SetHighlightedObject(IVisualPickable obj)
		{
			highlighted = obj;
		}
		
		// This collects a visual sector's geometry for rendering
		public void AddSectorGeometry(VisualGeometry g)
		{
			// Must have a texture and vertices
			if(g.Texture != null && g.Triangles > 0)
			{
				// styd. Doom 64: the part that goes on above and below a wall without another side only
				// hides what is behind it, and only for a camera that is where a player could be
				if((g.GeometryType == VisualGeometryType.WALL_OCCLUSION) && !wallocclusion) return;

				if(g.RenderAsSky && General.Settings.GZDrawSky)
				{
					if(g.RenderAsSkyBackground) skybackgeo.Add(g); // styd
					else skygeo.Add(g);
				}
				else
				{
					// styd. Doom 64: the sky is drawn first behind geometry whose texture has holes. A wall
					// without another side hides what is behind it, as it does in the game.
					if(g.RenderSkyBehind && General.Settings.GZDrawSky)
					{
						if(g.GeometryType == VisualGeometryType.WALL_MIDDLE) skygeo.Add(g);
						else skybackgeo.Add(g);
					}

					switch(g.RenderPass)
					{
						case RenderPass.Solid:
							if(!solidgeo.ContainsKey(g.Texture))
								solidgeo.Add(g.Texture, new List<VisualGeometry>());
							solidgeo[g.Texture].Add(g);
							break;

						case RenderPass.Mask:
							if(!maskedgeo.ContainsKey(g.Texture))
								maskedgeo.Add(g.Texture, new List<VisualGeometry>());
							maskedgeo[g.Texture].Add(g);
							break;

						case RenderPass.Additive:
						case RenderPass.Alpha:
							translucentgeo.Add(g);
							break;

						default:
							throw new NotImplementedException("Geometry rendering of " + g.RenderPass + " render pass is not implemented!");
					}
				}
			}
		}

		// This collects a visual sector's geometry for rendering
		public void AddThingGeometry(VisualThing t)
		{
			//mxd. Gather lights
			if (General.Settings.GZDrawLightsMode != LightRenderMode.NONE && !fullbrightness && t.LightType != null)
			{
				t.UpdateLightRadius();
                if (t.LightRadius > 0)
				{
                    if (t.LightType != null && t.LightType.LightAnimated)
                        t.UpdateBoundingBox();
					lightthings.Add(t);
				}
			}

			//mxd. Gather models
			if((t.Thing.RenderMode == ThingRenderMode.MODEL || t.Thing.RenderMode == ThingRenderMode.VOXEL) && 
				(General.Settings.GZDrawModelsMode == ModelRenderMode.ALL ||
				 General.Settings.GZDrawModelsMode == ModelRenderMode.ACTIVE_THINGS_FILTER ||
				(General.Settings.GZDrawModelsMode == ModelRenderMode.SELECTION && t.Selected))) 
			{
                if (t.RenderPass == RenderPass.Mask ||
                    t.RenderPass == RenderPass.Solid ||
                    (t.RenderPass == RenderPass.Alpha && (t.VertexColor & 0xFF000000) == 0xFF000000))
                {
                    ModelData mde = General.Map.Data.ModeldefEntries[t.Thing.Type];
                    if (!maskedmodelthings.ContainsKey(mde)) maskedmodelthings.Add(mde, new List<VisualThing>());
                    maskedmodelthings[mde].Add(t);
                }
                else if (t.RenderPass == RenderPass.Alpha || t.RenderPass == RenderPass.Additive)
                {
                    translucentmodelthings.Add(t);
                }
                else
                {
                    throw new NotImplementedException("Thing model rendering of " + t.RenderPass + " render pass is not implemented!");
                }
			}
			// Gather regular things
			else 
			{
				//mxd. Set correct texture, geobuffer and triangles count
				t.UpdateSpriteFrame();

				//Must have a texture!
				if(t.Texture != null)
				{
					//mxd
					switch(t.RenderPass)
					{
						case RenderPass.Solid:
							if(!solidthings.ContainsKey(t.Texture)) solidthings.Add(t.Texture, new List<VisualThing>());
							solidthings[t.Texture].Add(t);
							break;

						case RenderPass.Mask:
							if(!maskedthings.ContainsKey(t.Texture)) maskedthings.Add(t.Texture, new List<VisualThing>());
							maskedthings[t.Texture].Add(t);
							break;

						case RenderPass.Additive:
						case RenderPass.Alpha:
							translucentthings.Add(t);
							break;

						default:
							throw new NotImplementedException("Thing rendering of " + t.RenderPass + " render pass is not implemented!");
					}
				}
			}

			//mxd. Add to the plain list
			allthings.Add(t);
		}

		//mxd
		public void SetVisualVertices(List<VisualVertex> verts) { visualvertices = verts; }

		//mxd
		public void SetEventLines(List<Line3D> lines) { eventlines = lines; }

		//mxd
		private static bool BoundingBoxesIntersect(Vector3D[] bbox1, Vector3D[] bbox2) 
		{
			Vector3D dist = bbox1[0] - bbox2[0];

			Vector3D halfSize1 = bbox1[0] - bbox1[1];
			Vector3D halfSize2 = bbox2[0] - bbox2[1];

			return (halfSize1.x + halfSize2.x >= Math.Abs(dist.x) && halfSize1.y + halfSize2.y >= Math.Abs(dist.y) && halfSize1.z + halfSize2.z >= Math.Abs(dist.z));
		}

		// This renders the crosshair
		public void RenderCrosshair()
		{
			//mxd
			world = Matrix.Identity;
			ApplyMatrices3D();
			
			// Set renderstates
			graphics.Device.SetRenderState(RenderState.CullMode, Cull.None);
			graphics.Device.SetRenderState(RenderState.ZEnable, false);
			graphics.Device.SetRenderState(RenderState.AlphaBlendEnable, true);
			graphics.Device.SetRenderState(RenderState.AlphaTestEnable, false);
			graphics.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
			graphics.Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
			graphics.Device.SetRenderState(RenderState.TextureFactor, -1);
			graphics.Device.SetTransform(TransformState.World, Matrix.Identity);
			graphics.Device.SetTransform(TransformState.Projection, Matrix.Identity);
			ApplyMatrices2D();
			
			// Texture
			if(crosshairbusy)
			{
				if(General.Map.Data.CrosshairBusy3D.Texture == null) General.Map.Data.CrosshairBusy3D.CreateTexture();
				graphics.Shaders.Display2D.Texture1 = General.Map.Data.CrosshairBusy3D.Texture;
			}
			else
			{
				if(General.Map.Data.Crosshair3D.Texture == null) General.Map.Data.Crosshair3D.CreateTexture();
				graphics.Shaders.Display2D.Texture1 = General.Map.Data.Crosshair3D.Texture;
			}
			
			// Draw
			graphics.Shaders.Display2D.Begin();
			graphics.Shaders.Display2D.SetSettings(1.0f, 1.0f, 0.0f, 1.0f, true);
			graphics.Shaders.Display2D.BeginPass(1);
			graphics.Device.DrawUserPrimitives(PrimitiveType.TriangleStrip, 0, 2, crosshairverts);
			graphics.Shaders.Display2D.EndPass();
			graphics.Shaders.Display2D.End();

            General.Map.Renderer2D.RenderText(fpsLabel);
        }

		// This switches fog on and off
		public void SetFogMode(bool usefog)
		{
			graphics.Device.SetRenderState(RenderState.FogEnable, usefog);
			fogenabled = usefog; // styd
		}

		// This siwtches crosshair busy icon on and off
		public void SetCrosshairBusy(bool busy)
		{
			crosshairbusy = busy;
		}
		
		#endregion
	}
}
