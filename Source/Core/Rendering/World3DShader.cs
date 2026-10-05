
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
using SlimDX.Direct3D9;
using SlimDX;

#endregion

namespace CodeImp.DoomBuilder.Rendering
{
	internal sealed class World3DShader : D3DShader
	{
		#region ================== Variables

		// Property handlers
		private readonly EffectHandle texture1;
		private readonly EffectHandle texture2; // styd
		private readonly EffectHandle worldviewproj;
		private readonly EffectHandle minfiltersettings;
		private readonly EffectHandle magfiltersettings;
		private readonly EffectHandle mipfiltersettings;
		private readonly EffectHandle maxanisotropysetting;
		private readonly EffectHandle highlightcolor;

		//mxd
		private readonly EffectHandle vertexColorHandle;
		private readonly EffectHandle lightPositionAndRadiusHandle; //lights
        private readonly EffectHandle lightOrientationHandle;
        private readonly EffectHandle light2RadiusHandle;
		private readonly EffectHandle lightColorHandle;
        private readonly EffectHandle desaturationHandle;
        private readonly EffectHandle ignoreNormalsHandle;
        private readonly EffectHandle spotLightHandle;
		private readonly EffectHandle world;
        private readonly EffectHandle modelnormal;
        private readonly EffectHandle camPosHandle; //used for fog rendering
		private readonly EffectHandle doom64fogHandle; // styd
		private readonly EffectHandle doom64viewHandle; // styd
		private readonly EffectHandle doom64skyHandle; // styd
		private readonly EffectHandle doom64skypicHandle; // styd
		private readonly EffectHandle doom64skymodeHandle; // styd
		private readonly EffectHandle doom64skytopHandle; // styd
		private readonly EffectHandle doom64skybottomHandle; // styd
		private readonly EffectHandle doom64skybaseHandle; // styd

        // [ZZ]
        private readonly EffectHandle stencilColorHandle;
		
		#endregion

		#region ================== Properties

		private Matrix wwp;
		public Matrix WorldViewProj
		{
			set
			{
				if(wwp != value)
				{
					effect.SetValue(worldviewproj, value);
					wwp = value;
					settingschanged = true;
				}
			}
		}

		public BaseTexture Texture1 { set { effect.SetTexture(texture1, value); settingschanged = true; } }

		// styd. The texture that Doom 64 draws over the texture of a liquid floor
		public BaseTexture Texture2 { set { effect.SetTexture(texture2, value); settingschanged = true; } }

		//mxd
		private Color4 vertexcolor;
		public Color4 VertexColor
		{
			set 
			{
				if(vertexcolor != value)
				{
					effect.SetValue(vertexColorHandle, value);
					vertexcolor = value;
					settingschanged = true; 
				}
			} 
		}

        // [ZZ]
        private Color4 stencilcolor;
        public Color4 StencilColor
        {
            set
            {
                if (stencilcolor != value)
                {
                    effect.SetValue(stencilColorHandle, value);
                    stencilcolor = value;
                    settingschanged = true;
                }
            }
        }
		
		//lights
		private Color4 lightcolor;
		public Color4 LightColor
		{
			set
			{
				if(lightcolor != value)
				{
					effect.SetValue(lightColorHandle, value);
					lightcolor = value;
					settingschanged = true;
				}
			}
		}

        // [ZZ] desaturation!
        private float desaturation;
        public float Desaturation
        {
            set
            {
                if (desaturation != value)
                {
                    effect.SetValue(desaturationHandle, value);
                    desaturation = value;
                    settingschanged = true;
                }
            }
        }

        // [ZZ] emulating broken gz lights
        private bool ignorenormals;
        public bool IgnoreNormals
        {
            set
            {
                if (ignorenormals != value)
                {
                    effect.SetValue(ignoreNormalsHandle, value ? 1f : 0f);
                    ignorenormals = value;
                    settingschanged = true;
                }
            }
        }

        private bool spotlight;
        public bool SpotLight
        {
            set
            {
                if (spotlight != value)
                {
                    effect.SetValue(spotLightHandle, value ? 1f : 0f);
                    spotlight = value;
                    settingschanged = true;
                }
            }
        }

		private Vector4 lightpos;
		public Vector4 LightPositionAndRadius
		{
			set
			{
				if(lightpos != value)
				{
					effect.SetValue(lightPositionAndRadiusHandle, value);
					lightpos = value;
					settingschanged = true;
				} 
			}
		}

        private Vector3 lightori;
        public Vector3 LightOrientation
        {
            set
            {
                if (lightori != value)
                {
                    effect.SetValue(lightOrientationHandle, value);
                    lightori = value;
                    settingschanged = true;
                }
            }
        }

        private Vector2 light2rad;
        public Vector2 Light2Radius
        {
            set
            {
                if (light2rad != value)
                {
                    effect.SetValue(light2RadiusHandle, value);
                    light2rad = value;
                    settingschanged = true;
                }
            }
        }
		
		//fog
		private Vector4 campos;
		public Vector4 CameraPosition
		{
			set
			{
				if(campos != value)
				{
					effect.SetValue(camPosHandle, value);
					campos = value;
					settingschanged = true;
				}
			}
		}

		// styd. The fog of Doom 64 and the direction of the view that it needs
		private Vector4 doom64fog;
		public Vector4 Doom64Fog
		{
			set
			{
				if(doom64fog != value)
				{
					effect.SetValue(doom64fogHandle, value);
					doom64fog = value;
					settingschanged = true;
				}
			}
		}

		private Vector4 doom64view;
		public Vector4 Doom64View
		{
			set
			{
				if(doom64view != value)
				{
					effect.SetValue(doom64viewHandle, value);
					doom64view = value;
					settingschanged = true;
				}
			}
		}

		// styd. The sky of Doom 64: where the view is on the screen of the game, the picture of
		// a layer, how the layer is drawn and its colors (see world3d.fx)
		private Vector4 doom64sky;
		public Vector4 Doom64Sky
		{
			set
			{
				if(doom64sky != value)
				{
					effect.SetValue(doom64skyHandle, value);
					doom64sky = value;
					settingschanged = true;
				}
			}
		}

		private Vector4 doom64skypic;
		public Vector4 Doom64SkyPicture
		{
			set
			{
				if(doom64skypic != value)
				{
					effect.SetValue(doom64skypicHandle, value);
					doom64skypic = value;
					settingschanged = true;
				}
			}
		}

		private Vector4 doom64skymode;
		public Vector4 Doom64SkyMode
		{
			set
			{
				if(doom64skymode != value)
				{
					effect.SetValue(doom64skymodeHandle, value);
					doom64skymode = value;
					settingschanged = true;
				}
			}
		}

		public void SetDoom64SkyColors(Color4 top, Color4 bottom, Color4 basecolor)
		{
			effect.SetValue(doom64skytopHandle, top);
			effect.SetValue(doom64skybottomHandle, bottom);
			effect.SetValue(doom64skybaseHandle, basecolor);
			settingschanged = true;
		}

		private Matrix mworld;
		public Matrix World
		{
			set
			{
				if(mworld != value)
				{
					effect.SetValue(world, value);
					mworld = value;
					settingschanged = true;
				}
			}
        }

        private Matrix mmodelnormal;
        public Matrix ModelNormal
        {
            set
            {
                if (mmodelnormal != value)
                {
                    effect.SetValue(modelnormal, value);
                    mmodelnormal = value;
                    settingschanged = true;
                }
            }
        }

        //mxd. This sets the highlight color
        private Color4 hicolor;
		public Color4 HighlightColor
		{
			set
			{
				if(hicolor != value)
				{
					effect.SetValue(highlightcolor, value);
					hicolor = value;
					settingschanged = true;
				}
			}
		}

		#endregion

		#region ================== Constructor / Disposer

		// Constructor
		public World3DShader(ShaderManager manager) : base(manager)
		{
			// Load effect from file
			effect = LoadEffect("world3d.fx");

			// Get the property handlers from effect
			if(effect != null)
			{
				worldviewproj = effect.GetParameter(null, "worldviewproj");
				texture1 = effect.GetParameter(null, "texture1");
				texture2 = effect.GetParameter(null, "texture2"); // styd
				minfiltersettings = effect.GetParameter(null, "minfiltersettings");
				magfiltersettings = effect.GetParameter(null, "magfiltersettings");
				mipfiltersettings = effect.GetParameter(null, "mipfiltersettings");
				highlightcolor = effect.GetParameter(null, "highlightcolor");
				maxanisotropysetting = effect.GetParameter(null, "maxanisotropysetting");

				//mxd
				vertexColorHandle = effect.GetParameter(null, "vertexColor");
				//lights
				lightPositionAndRadiusHandle = effect.GetParameter(null, "lightPosAndRadius");
                lightOrientationHandle = effect.GetParameter(null, "lightOrientation");
                light2RadiusHandle = effect.GetParameter(null, "light2Radius");
                lightColorHandle = effect.GetParameter(null, "lightColor");
                desaturationHandle = effect.GetParameter(null, "desaturation");
                ignoreNormalsHandle = effect.GetParameter(null, "ignoreNormals");
                spotLightHandle = effect.GetParameter(null, "spotLight");
                //fog
                camPosHandle = effect.GetParameter(null, "campos");
				doom64fogHandle = effect.GetParameter(null, "doom64fog"); // styd
				doom64viewHandle = effect.GetParameter(null, "doom64view"); // styd
				doom64skyHandle = effect.GetParameter(null, "doom64sky"); // styd
				doom64skypicHandle = effect.GetParameter(null, "doom64skypic"); // styd
				doom64skymodeHandle = effect.GetParameter(null, "doom64skymode"); // styd
				doom64skytopHandle = effect.GetParameter(null, "doom64skytop"); // styd
				doom64skybottomHandle = effect.GetParameter(null, "doom64skybottom"); // styd
				doom64skybaseHandle = effect.GetParameter(null, "doom64skybase"); // styd

                // [ZZ]
                stencilColorHandle = effect.GetParameter(null, "stencilColor");

				world = effect.GetParameter(null, "world");
                modelnormal = effect.GetParameter(null, "modelnormal");
            }

			// Initialize world vertex declaration
			VertexElement[] ve = {
				new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
				new VertexElement(0, 12, DeclarationType.Color, DeclarationMethod.Default, DeclarationUsage.Color, 0),
				new VertexElement(0, 16, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
				new VertexElement(0, 24, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0), //mxd
				VertexElement.VertexDeclarationEnd
			};

			vertexdecl = new VertexDeclaration(General.Map.Graphics.Device, ve);

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
				if(texture1 != null) texture1.Dispose();
				if(texture2 != null) texture2.Dispose(); // styd
				if(worldviewproj != null) worldviewproj.Dispose();
				if(minfiltersettings != null) minfiltersettings.Dispose();
				if(magfiltersettings != null) magfiltersettings.Dispose();
				if(mipfiltersettings != null) mipfiltersettings.Dispose();
				if(highlightcolor != null) highlightcolor.Dispose();
				if(maxanisotropysetting != null) maxanisotropysetting.Dispose();

				//mxd
				if(vertexColorHandle != null) vertexColorHandle.Dispose();
				if(lightColorHandle != null) lightColorHandle.Dispose();
                if(desaturationHandle != null) desaturationHandle.Dispose();
                if(ignoreNormalsHandle != null) ignoreNormalsHandle.Dispose();
                if(light2RadiusHandle != null) light2RadiusHandle.Dispose();
                if(lightOrientationHandle != null) lightOrientationHandle.Dispose();
                if(lightPositionAndRadiusHandle != null) lightPositionAndRadiusHandle.Dispose();
				if(camPosHandle != null) camPosHandle.Dispose();
				if(doom64fogHandle != null) doom64fogHandle.Dispose(); // styd
				if(doom64viewHandle != null) doom64viewHandle.Dispose(); // styd
				if(doom64skyHandle != null) doom64skyHandle.Dispose(); // styd
				if(doom64skypicHandle != null) doom64skypicHandle.Dispose(); // styd
				if(doom64skymodeHandle != null) doom64skymodeHandle.Dispose(); // styd
				if(doom64skytopHandle != null) doom64skytopHandle.Dispose(); // styd
				if(doom64skybottomHandle != null) doom64skybottomHandle.Dispose(); // styd
				if(doom64skybaseHandle != null) doom64skybaseHandle.Dispose(); // styd
                if(stencilColorHandle != null) stencilColorHandle.Dispose();
				if(world != null) world.Dispose();
                if(modelnormal != null) modelnormal.Dispose();

                // Done
                base.Dispose();
			}
		}

		#endregion

		#region ================== Methods

		// This sets the constant settings
		public void SetConstants(bool bilinear, float maxanisotropy)
		{
			//mxd. It's still nice to have anisotropic filtering when texture filtering is disabled
			TextureFilter magminfilter = (bilinear ? TextureFilter.Linear : TextureFilter.Point);
			effect.SetValue(magfiltersettings, magminfilter);
			effect.SetValue(minfiltersettings, (maxanisotropy > 1.0f ? TextureFilter.Anisotropic : magminfilter));
			effect.SetValue(mipfiltersettings, (bilinear ? TextureFilter.Linear : TextureFilter.None)); // [SB] use None, otherwise textures are still filtered
			effect.SetValue(maxanisotropysetting, maxanisotropy);

			settingschanged = true; //mxd
		}
		
		#endregion
	}
}
