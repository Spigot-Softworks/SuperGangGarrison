#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Character rim light, drawn as a second pass over a sprite (premultiplied alpha).
// A solid pixel gets rim when the space next to it, toward the light, is empty: the
// silhouette's light-facing edge. Rim pixels are painted one flat, opaque colour (never
// blended with the sprite's own pixels, so there is no colour mixing along the edge), and
// the rest of the body can be darkened and (de)saturated. All directions are in texture space (the caller undoes
// mirroring and rotation), and sampling is clamped to the frame's rectangle so atlas
// neighbours never count as part of the sprite.

sampler2D TextureSampler : register(s0);

float2 LightDirection;   // unit vector toward the light, texture space
float2 TexelSize;        // 1 / texture size
float2 RectMin;          // frame rectangle in UV
float2 RectMax;
float3 RimColor;         // the rim's final flat colour (strength already applied); 0 = no rim
float RimWidth;          // 1..3 texels
float RimWrap;           // 0..1: from 1/3 the rim reaches round the sides, from 2/3 most of the way round
float BodyShade;         // 0..1, darkens the body toward black
float BodySaturation;    // 0..2, 1 = unchanged
float BodyAdjust;        // 1 when the body is shaded or re-saturated, else 0 (body left untouched)

float AlphaAt(float2 uv)
{
    float inside = step(RectMin.x, uv.x) * step(RectMin.y, uv.y) * step(uv.x, RectMax.x) * step(uv.y, RectMax.y);
    return tex2D(TextureSampler, uv).a * inside;
}

// Whole-texel step k along a direction (rounded), so neighbours are always whole sprite
// pixels: a diagonal light walks the grid like a pixel-art line.
float2 TexelStep(float2 direction, float k)
{
    return floor((direction * k) + 0.5) * TexelSize;
}

// 1 when there is open space within RimWidth texels along the direction. `center` must be
// the centre of a sprite texel; a fractional offset would let different screen pixels of
// the same (scaled-up) sprite pixel see different neighbours and leave thin slivers of rim.
float OpenToward(float2 center, float2 direction)
{
    float open = 1.0 - AlphaAt(center + TexelStep(direction, 1.0));
    open = max(open, (1.0 - AlphaAt(center + TexelStep(direction, 2.0))) * saturate(RimWidth - 1.0));
    open = max(open, (1.0 - AlphaAt(center + TexelStep(direction, 3.0))) * saturate(RimWidth - 2.0));
    return open;
}

float2 Rotate(float2 v, float c, float s)
{
    return float2((v.x * c) - (v.y * s), (v.x * s) + (v.y * c));
}

float4 RimPixel(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 tex = tex2D(TextureSampler, texCoord);
    // Only solid pixels take rim (kept branch-free so ps_3_0 never has to unroll a return).
    float solid = step(0.5, tex.a);

    // Every screen pixel inside one sprite pixel decides from that sprite pixel's centre, so
    // the rim always covers whole sprite pixels.
    float2 center = (floor(texCoord / TexelSize) + 0.5) * TexelSize;
    float rim = OpenToward(center, LightDirection);

    // Wrap: edges 60 degrees off the light catch it first, then edges 120 degrees off.
    float nearWrap = max(
        OpenToward(center, Rotate(LightDirection, 0.5, 0.866)),
        OpenToward(center, Rotate(LightDirection, 0.5, -0.866)));
    float farWrap = max(
        OpenToward(center, Rotate(LightDirection, -0.5, 0.866)),
        OpenToward(center, Rotate(LightDirection, -0.5, -0.866)));
    float wrap = max(nearWrap * step(0.333, RimWrap), farWrap * step(0.666, RimWrap));

    // All or nothing per pixel, so every rim pixel ends up exactly the same colour.
    float hasRim = step(0.001, dot(RimColor, float3(1.0, 1.0, 1.0)));
    float rimMask = step(0.5, max(rim, wrap)) * solid * hasRim;

    // The body: the sprite's own colour (with the draw tint), re-saturated, then darkened.
    float3 tint = color.rgb / max(color.a, 0.004);
    float3 straight = (tex.rgb / max(tex.a, 0.004)) * tint;
    float luma = dot(straight, float3(0.299, 0.587, 0.114));
    float3 body = saturate(luma + ((straight - luma) * BodySaturation)) * (1.0 - BodyShade);
    float bodyMask = solid * BodyAdjust;

    // Drawn with premultiplied alpha blending (src + dst * (1 - src.a)) over the sprite
    // already drawn: opaque pixels replace it, alpha 0 leaves it untouched.
    float alpha = max(rimMask, bodyMask) * color.a;
    float3 rgb = lerp(body, RimColor, rimMask);
    return float4(rgb * alpha, alpha);
}

technique RimLight
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL RimPixel();
    }
}
