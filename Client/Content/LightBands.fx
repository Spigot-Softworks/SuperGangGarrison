#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Retro light bands: steps a smooth light map's brightness into flat bands, keeping each
// cell's hue. Runs on the small light map (one texel per light cell), so band edges are
// crisp cell-aligned contours however the lights overlap. Output alpha is 0: the light map
// renderer uses destination alpha afterwards to clip bands to the map art.

sampler2D TextureSampler : register(s0);

float BandCount;   // bands across the light map's 0..1 range (0.5 = unlit world)

float4 BandPixel(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float3 light = tex2D(TextureSampler, texCoord).rgb;
    float peak = max(light.r, max(light.g, light.b));
    float count = max(BandCount, 1.0);
    // Levels are anchored on 0.5 (the unlit world), so every band count keeps that exact.
    float stepped = saturate(0.5 + (floor(((peak - 0.5) * count) + 0.5) / count));
    float3 banded = peak > 0.0001 ? light * (stepped / peak) : float3(0.0, 0.0, 0.0);
    return float4(banded, 0.0);
}

technique LightBands
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL BandPixel();
    }
}
