// 3D world rendering shader
// Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com

// Vertex input data
struct VertexData
{
	float3 pos		  : POSITION;
	float4 color	  : COLOR0;
	float2 uv		    : TEXCOORD0;
	float3 normal   : NORMAL; //mxd
};

// Pixel input data
struct PixelData
{
	float4 pos		: POSITION;
	float4 color	: COLOR0;
	float2 uv		  : TEXCOORD0;
};

//mxd. Vertex input data for sky rendering
struct SkyVertexData
{
	float3 pos		: POSITION;
	float2 uv			: TEXCOORD0;
};

//mxd. Pixel input data for sky rendering
struct SkyPixelData
{
	float4 pos		: POSITION;
	float3 tex		: TEXCOORD0;
};

//mxd. Pixel input data for light pass
struct LitPixelData
{
	float4 pos		  : POSITION;
	float4 color	  : COLOR0;
	float2 uv		    : TEXCOORD0;
	float3 pos_w    : TEXCOORD1; //mxd. pixel position in world space
	float3 normal   : TEXCOORD2; //mxd. normal
};

// Highlight color
float4 highlightcolor;

// Matrix for final transformation
const float4x4 worldviewproj;

//mxd
float4x4 world;
float4x4 modelnormal;
float4 vertexColor;
// [ZZ]
float4 stencilColor;

//light
float4 lightPosAndRadius;
float3 lightOrientation; // this is a vector that points in light's direction
float2 light2Radius; // this is used with spotlights
float4 lightColor; //also used as fog color
float desaturation;
float ignoreNormals; // ignore normals in lighting equation. used for non-attenuated lights on models.
float spotLight; // use lightOrientation

//fog
const float4 campos;  //w is set to fade factor (distance, at wich fog color completely overrides pixel color)

// styd. Fog of Doom 64. The part of fog in a color is (x - y / depth of the pixel in the view);
// z is 1 when this fog is used instead of the one above, 0 otherwise.
float4 doom64fog;
// styd. Direction of the view, to get the depth of a pixel
float4 doom64view;

// styd. Sky of Doom 64, drawn flat on the screen as the game draws it. In doom64sky, x and y are
// the screen pixels of the game in one unit of the view, from its middle; z is how many of these
// pixels the middle of the view is above the horizon; w is how far the layer has scrolled.
float4 doom64sky;
// A picture of the sky: x is its width and z its height in screen pixels of the game, y is the
// screen row of the game at its top, w is half a row of its texture.
float4 doom64skypic;
// x is 1 for the layer at the back, which is black where it has no picture and goes on above its
// top; y is 1 when it goes on mirrored, 0 when its top row goes on; z is how far the clouds have
// scrolled in depth.
float4 doom64skymode;
// The colors at the top and at the bottom of a layer, and the color of the clouds
float4 doom64skytop;
float4 doom64skybottom;
float4 doom64skybase;

//sky
static const float4 skynormal = float4(0.0f, 1.0f, 0.0f, 0.0f);

// Texture input
const texture texture1;

// Filter settings
const dword minfiltersettings;
const dword magfiltersettings;
const dword mipfiltersettings;
const float maxanisotropysetting;

// Texture sampler settings
sampler2D texturesamp = sampler_state
{
	Texture = <texture1>;
	MagFilter = magfiltersettings;
	MinFilter = minfiltersettings;
	MipFilter = mipfiltersettings;
	MipMapLodBias = 0.0f;
	MaxAnisotropy = maxanisotropysetting;
};

//mxd. Skybox texture sampler settings
samplerCUBE skysamp = sampler_state
{
	Texture = <texture1>;
	MagFilter = magfiltersettings;
	MinFilter = minfiltersettings;
	MipFilter = mipfiltersettings;
	MipMapLodBias = 0.0f;
	MaxAnisotropy = maxanisotropysetting;
};

// styd. Samplers of the sky of Doom 64: the game shows the pixels of its sky pictures as they
// are, and filters its clouds and its fire
sampler2D doom64skypicsamp = sampler_state
{
	Texture = <texture1>;
	MagFilter = Point;
	MinFilter = Point;
	MipFilter = None;
};

sampler2D doom64skysmoothsamp = sampler_state
{
	Texture = <texture1>;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = None;
};

// Vertex shader
PixelData vs_main(VertexData vd) 
{
	PixelData pd;
	
	// Fill pixel data input
	pd.pos = mul(float4(vd.pos, 1.0f), worldviewproj);
	pd.color = vd.color;
	pd.uv = vd.uv;
	
	// Return result
	return pd;
}

//mxd. same as vs_main, but uses vertexColor var instead of actual vertex color. used in models rendering
PixelData vs_customvertexcolor(VertexData vd) 
{
	PixelData pd;
	
	// Fill pixel data input
	pd.pos = mul(float4(vd.pos, 1.0f), worldviewproj);
	pd.color = vertexColor;
	pd.uv = vd.uv;
	
	// Return result
	return pd;
}

LitPixelData vs_customvertexcolor_fog(VertexData vd) 
{
	LitPixelData pd;
	
	// Fill pixel data input
	pd.pos = mul(float4(vd.pos, 1.0f), worldviewproj);
	pd.pos_w = mul(float4(vd.pos, 1.0f), world).xyz;
	pd.color = vertexColor;
	pd.uv = vd.uv;
	pd.normal = normalize(mul(float4(vd.normal, 1.0f), modelnormal).xyz);
	
	// Return result
	return pd;
}

//mxd. light pass vertex shader
LitPixelData vs_lightpass(VertexData vd) 
{
	LitPixelData pd;
	pd.pos = mul(float4(vd.pos, 1.0f), worldviewproj);
	pd.pos_w = mul(float4(vd.pos, 1.0f), world).xyz;
	pd.color = vd.color;
	pd.uv = vd.uv;
	pd.normal = normalize(mul(float4(vd.normal, 1.0f), modelnormal).xyz);

	// Return result
	return pd;
}

// [ZZ] desaturation routine. almost literal quote from GZDoom's GLSL
float4 desaturate(float4 texel)
{
	float gray = (texel.r * 0.3 + texel.g * 0.56 + texel.b * 0.14);	
	return lerp(texel, float4(gray,gray,gray,texel.a), desaturation);
}

// Normal pixel shader
float4 ps_main(PixelData pd) : COLOR
{
	float4 tcolor = tex2D(texturesamp, pd.uv);
	tcolor = lerp(tcolor, float4(stencilColor.rgb, tcolor.a), stencilColor.a);
	return desaturate(tcolor * pd.color);
}

// Full-bright pixel shader
float4 ps_fullbright(PixelData pd) : COLOR
{
	float4 tcolor = tex2D(texturesamp, pd.uv);
	tcolor = lerp(tcolor, float4(stencilColor.rgb, tcolor.a), stencilColor.a);
	tcolor.a *= pd.color.a;
	return tcolor;
}

// Normal pixel shader with highlight
float4 ps_main_highlight(PixelData pd) : COLOR
{
	float4 tcolor = tex2D(texturesamp, pd.uv);
	tcolor = lerp(tcolor, float4(stencilColor.rgb, tcolor.a), stencilColor.a);
	if(tcolor.a == 0) return tcolor;
	
	// Blend texture color and vertex color
	float4 ncolor = desaturate(tcolor * pd.color);
	
	return float4(highlightcolor.rgb * highlightcolor.a + (ncolor.rgb - 0.4f * highlightcolor.a), max(pd.color.a + 0.25f, 0.5f));
}

// Full-bright pixel shader with highlight
float4 ps_fullbright_highlight(PixelData pd) : COLOR
{
	float4 tcolor = tex2D(texturesamp, pd.uv);
	tcolor = lerp(tcolor, float4(stencilColor.rgb, tcolor.a), stencilColor.a);
	if(tcolor.a == 0) return tcolor;
	
	// Blend texture color and vertex color
	float4 ncolor = tcolor * pd.color;
	
	return float4(highlightcolor.rgb * highlightcolor.a + (tcolor.rgb - 0.4f * highlightcolor.a), max(pd.color.a + 0.25f, 0.5f));
}

//mxd. This adds fog color to current pixel color
float4 getFogColor(LitPixelData pd, float4 color)
{
	float fogdist = max(16.0f, distance(pd.pos_w, campos.xyz));
	float fogfactor = exp2(campos.w * fogdist);

	// styd. Doom 64 makes its fog from the depth in the view
	float depth = max(8.0f, dot(pd.pos_w - campos.xyz, doom64view.xyz));
	fogfactor = lerp(fogfactor, 1.0f - saturate(doom64fog.x - doom64fog.y / depth), doom64fog.z);

	color.rgb = lerp(lightColor.rgb, color.rgb, fogfactor);
	return color;
}

//mxd. Shaders with fog calculation
// Normal pixel shader
float4 ps_main_fog(LitPixelData pd) : COLOR 
{
	float4 tcolor = tex2D(texturesamp, pd.uv);
	tcolor = lerp(tcolor, float4(stencilColor.rgb, tcolor.a), stencilColor.a);
	if(tcolor.a == 0) return tcolor;
	
	return desaturate(getFogColor(pd, tcolor * pd.color));
}

// Normal pixel shader with highlight
float4 ps_main_highlight_fog(LitPixelData pd) : COLOR 
{
	float4 tcolor = tex2D(texturesamp, pd.uv);
	tcolor = lerp(tcolor, float4(stencilColor.rgb, tcolor.a), stencilColor.a);
	if(tcolor.a == 0) return tcolor;
	
	// Blend texture color and vertex color
	float4 ncolor = desaturate(getFogColor(pd, tcolor * pd.color));
	
	return float4(highlightcolor.rgb * highlightcolor.a + (ncolor.rgb - 0.4f * highlightcolor.a), max(ncolor.a + 0.25f, 0.5f));
}

//mxd: used to draw bounding boxes
float4 ps_constant_color(PixelData pd) : COLOR 
{
	return vertexColor;
}

//mxd: used to draw event lines
float4 ps_vertex_color(PixelData pd) : COLOR 
{
	return pd.color;
}

//mxd. dynamic light pixel shader pass, dood!
float4 ps_lightpass(LitPixelData pd) : COLOR
{
	//is face facing away from light source?
	// [ZZ] oddly enough pd.normal is not a proper normal, so using dot on it returns rather unexpected results. wrapped in normalize().
	//      update 01.02.2017: offset the equation by 3px back to try to emulate GZDoom's broken visibility check.
	float diffuseContribution = dot(pd.normal, normalize(lightPosAndRadius.xyz - pd.pos_w + pd.normal*3));
	if (diffuseContribution < 0 && ignoreNormals < 0.5)
		clip(-1);
	diffuseContribution = max(diffuseContribution, 0); // to make sure

	//is pixel in light range?
	float dist = distance(pd.pos_w, lightPosAndRadius.xyz);
	if(dist > lightPosAndRadius.w)
		clip(-1);

	//is pixel tranparent?
	float4 tcolor = tex2D(texturesamp, pd.uv);
	tcolor = lerp(tcolor, float4(stencilColor.rgb, tcolor.a), stencilColor.a);
	if(tcolor.a == 0.0f)
		clip(-1);

	//if it is - calculate color at current pixel
	float4 lightColorMod = float4(0.0f, 0.0f, 0.0f, 1.0f);

	lightColorMod.rgb = lightColor.rgb * max(lightPosAndRadius.w - dist, 0.0f) / lightPosAndRadius.w;
    
    if (spotLight > 0.5)
    {
        float3 lightDirection = normalize(lightPosAndRadius.xyz - pd.pos_w);
        float cosDir = dot(lightDirection, lightOrientation);
        float df = smoothstep(light2Radius.y, light2Radius.x, cosDir);
        lightColorMod.rgb *= df;
    }

	if (lightColor.a > 0.979f && lightColor.a < 0.981f) // attenuated light 98%
		lightColorMod.rgb *= diffuseContribution;
	if (lightColorMod.r > 0.0f || lightColorMod.g > 0.0f || lightColorMod.b > 0.0f)
	{
		lightColorMod.rgb *= lightColor.a;
		if (lightColor.a > 0.4f) //Normal, vavoom or negative light (or attenuated)
			lightColorMod *= tcolor;
		return desaturate(lightColorMod); //Additive light
	}
	clip(-1);
	return lightColorMod; //should never get here
}

//mxd. Vertex skybox shader
SkyPixelData vs_skybox(SkyVertexData vd)
{
	SkyPixelData pd;
	pd.pos = mul(float4(vd.pos, 1.0f), worldviewproj);
	float3 worldpos = mul(float4(vd.pos, 1.0f), world).xyz;
	pd.tex = reflect(worldpos - campos.xyz, normalize(mul(skynormal, world).xyz));
	return pd;
}

//mxd. Pixel skybox shader
float4 ps_skybox(SkyPixelData pd) : COLOR
{
	float4 ncolor = texCUBE(skysamp, pd.tex);
	return float4(highlightcolor.rgb * highlightcolor.a + (ncolor.rgb - 0.4f * highlightcolor.a), 1.0f);
}

// styd. Pixel input data for the sky of Doom 64
struct Doom64SkyPixelData
{
	float4 pos		: POSITION;
	float4 scr		: TEXCOORD0;
};

// styd. Vertex shader of the sky of Doom 64: the pixel shader needs the place on the screen
Doom64SkyPixelData vs_doom64sky(SkyVertexData vd)
{
	Doom64SkyPixelData pd;
	pd.pos = mul(float4(vd.pos, 1.0f), worldviewproj);
	pd.scr = pd.pos;
	return pd;
}

// styd. A color of the sky of Doom 64, with the highlight
float4 doom64skycolor(float3 color, float alpha)
{
	return float4(highlightcolor.rgb * highlightcolor.a + (color - 0.4f * highlightcolor.a), alpha);
}

// styd. A picture of the sky of Doom 64. The game draws it flat on its screen, one pixel of the
// picture on one pixel of its screen of 320x240, and scrolls it with the angle of the view
// (R_RenderSkyPic). Its fire is drawn the same way on the upper half of the screen, in a color
// that goes from its top to its bottom (R_RenderFireSky).
float4 doom64skypicture(Doom64SkyPixelData pd, sampler2D samp)
{
	float2 p = pd.scr.xy / pd.scr.w;
	float column = p.x * doom64sky.x + doom64sky.w;
	float row = 120.0f - p.y * doom64sky.y - doom64sky.z;
	float v = (row - doom64skypic.y) / doom64skypic.z;

	// Above its top the picture is mirrored, or it ends there. The screen of the game ends at the
	// top of its fire, whose top row burns at times: that row must not go on above it.
	float mirrored = 1.0f - abs(frac(v * 0.5f) * 2.0f - 1.0f);
	float tv = clamp(lerp(saturate(v), mirrored, doom64skymode.y), doom64skypic.w, 1.0f - doom64skypic.w);
	float4 texel = tex2D(samp, float2(column / doom64skypic.x, tv));
	float notabove = max(step(0.0f, v), doom64skymode.y);
	float3 color = texel.rgb * lerp(doom64skytop.rgb, doom64skybottom.rgb, saturate(v)) * notabove;

	// Nothing is drawn below the picture. The layer at the back goes on above its top and is
	// black where it has no picture; the other layers only cover what their picture covers.
	float notbelow = step(v, 1.0f);
	float alpha = texel.a * lerp(notbelow * step(0.0f, v), notbelow, doom64skymode.x);
	return doom64skycolor(color * lerp(1.0f, alpha, doom64skymode.x), lerp(alpha, 1.0f, doom64skymode.x));
}

float4 ps_doom64skypicture(Doom64SkyPixelData pd) : COLOR
{
	return doom64skypicture(pd, doom64skypicsamp);
}

float4 ps_doom64skysmoothpicture(Doom64SkyPixelData pd) : COLOR
{
	return doom64skypicture(pd, doom64skysmoothsamp);
}

// styd. The clouds of the sky of Doom 64. The game draws them on a plane that leans over the
// view: it is 160 units away at the top of the screen, 120 pixels above the horizon, and 300 units
// away at the horizon, where it is 600 units wide. The texture goes one and a half times over
// its width and twice over its depth. Its color is the color of the clouds times the texture,
// plus a color that goes from the top of the screen to the horizon (R_RenderClouds).
float4 ps_doom64skyclouds(Doom64SkyPixelData pd) : COLOR
{
	float2 p = pd.scr.xy / pd.scr.w;
	float x = p.x * doom64sky.x;
	float y = p.y * doom64sky.y + doom64sky.z;
	float up = max(y, 0.0f);
	float v = (120.0f - up) / (120.0f + 0.875f * up);
	float u = 0.5f + (1.0f + 0.875f * v) * x / 600.0f;
	float cloud = tex2D(doom64skysmoothsamp, float2(u * 1.5f + doom64sky.w, v * 2.0f + doom64skymode.z)).r;
	float3 color = doom64skybase.rgb * cloud + lerp(doom64skytop.rgb, doom64skybottom.rgb, saturate(1.0f - y / 120.0f));

	// Nothing is drawn below the horizon
	return doom64skycolor(color * step(0.0f, y), 1.0f);
}

// Technique for shader model 2.0
technique SM20 
{
	// Normal
	pass p0 
	{
		VertexShader = compile vs_2_0 vs_main();
		PixelShader = compile ps_2_0 ps_main();
	}
	
	// Full brightness mode
	pass p1 
	{
		VertexShader = compile vs_2_0 vs_main();
		PixelShader = compile ps_2_0 ps_fullbright();
	}

	// Normal with highlight
	pass p2 
	{
		VertexShader = compile vs_2_0 vs_main();
		PixelShader = compile ps_2_0 ps_main_highlight();
	}
	
	// Full brightness mode with highlight
	pass p3 
	{
		VertexShader = compile vs_2_0 vs_main();
		PixelShader = compile ps_2_0 ps_fullbright_highlight();
	}
	
	//mxd. same as p0-p3, but using vertexColor variable
	// Normal
	pass p4 
	{
		VertexShader = compile vs_2_0 vs_customvertexcolor();
		PixelShader = compile ps_2_0 ps_main();
	}
	
	//mxd. Skybox shader
	pass p5 
	{
		VertexShader = compile vs_2_0 vs_skybox();
		PixelShader  = compile ps_2_0 ps_skybox();
	}
	
	// Normal with highlight
	pass p6 
	{
		VertexShader = compile vs_2_0 vs_customvertexcolor();
		PixelShader = compile ps_2_0 ps_main_highlight();
	}

	pass p7 {} //mxd. need this only to maintain offset
	
	//mxd. same as p0-p3, but with fog calculation
	// Normal
	pass p8 
	{
		VertexShader = compile vs_2_0 vs_lightpass();
		PixelShader = compile ps_2_0 ps_main_fog();
	}
	
	pass p9 {} //mxd. need this only to maintain offset

	// Normal with highlight
	pass p10 
	{
		VertexShader = compile vs_2_0 vs_lightpass();
		PixelShader = compile ps_2_0 ps_main_highlight_fog();
	}

	pass p11 {} //mxd. need this only to maintain offset
	
	//mxd. same as p4-p7, but with fog calculation
	// Normal
	pass p12 
	{
		VertexShader = compile vs_2_0 vs_customvertexcolor_fog();
		PixelShader = compile ps_2_0 ps_main_fog();
	}

	pass p13 {} //mxd. need this only to maintain offset
	
	// Normal with highlight
	pass p14 
	{
		VertexShader = compile vs_2_0 vs_customvertexcolor_fog();
		PixelShader = compile ps_2_0 ps_main_highlight_fog();
	}

	//mxd. Used to render event lines
	pass p15
	{
		VertexShader = compile vs_2_0 vs_main();
		PixelShader  = compile ps_2_0 ps_vertex_color();
	}
	
	//mxd. Just fills everything with vertexColor. Used in ThingCage rendering.
	pass p16 
	{
		VertexShader = compile vs_2_0 vs_customvertexcolor();
		PixelShader  = compile ps_2_0 ps_constant_color();
	}
	
	//mxd. Light pass
	pass p17 
	{
		VertexShader = compile vs_2_0 vs_lightpass();
		PixelShader  = compile ps_2_0 ps_lightpass();
		AlphaBlendEnable = true;
	}

	// styd. Sky of Doom 64: a picture, a picture that is filtered, the clouds
	pass p18
	{
		VertexShader = compile vs_2_0 vs_doom64sky();
		PixelShader  = compile ps_2_0 ps_doom64skypicture();
	}

	pass p19
	{
		VertexShader = compile vs_2_0 vs_doom64sky();
		PixelShader  = compile ps_2_0 ps_doom64skysmoothpicture();
	}

	pass p20
	{
		VertexShader = compile vs_2_0 vs_doom64sky();
		PixelShader  = compile ps_2_0 ps_doom64skyclouds();
	}
}
