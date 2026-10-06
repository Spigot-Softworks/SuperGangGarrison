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
float2 TexelSize;        // one ART pixel in UV: (art scale) / texture size. Sprites stored
                         // upscaled (each art pixel a 2x2 texel block) pass 2 / size, so
                         // the rim follows the art's pixel grid, not the texture's.
float2 RectMin;          // frame rectangle in UV
float2 RectMax;
float3 RimColor;         // the rim's final flat colour (strength already applied); 0 = no rim
float RimWidth;          // 1..3 texels
float CardinalLimit;     // a straight (up/down/left/right) neighbour counts when cos(angle to light) >= this
float DiagonalLimit;     // likewise for a diagonal neighbour (stricter, so flat edges do not catch side light)
float BlendMode;         // 0 normal, 1 add, 2 screen, 3 multiply, 4 overlay
float RimOpacity;        // 0..1, how strongly the blended rim replaces the pixel
float GrazeBand;         // how far the limits relax toward the light-facing end of the sprite
float BodyShade;         // 0..1, darkens the body toward black
float BodySaturation;    // 0..2, 1 = unchanged
float BodyAdjust;        // 1 when the body is shaded or re-saturated, else 0 (body left untouched)

float AlphaAt(float2 uv)
{
    float inside = step(RectMin.x, uv.x) * step(RectMin.y, uv.y) * step(uv.x, RectMax.x) * step(uv.y, RectMax.y);
    return tex2D(TextureSampler, uv).a * inside;
}

// 1 when there is open space within RimWidth grid pixels along a whole-pixel step.
// `center` must be the centre of a grid pixel; a fractional offset would let different
// screen pixels of the same (scaled-up) sprite pixel see different neighbours.
float OpenAlong(float2 center, float2 stepUv)
{
    float open = 1.0 - AlphaAt(center + stepUv);
    open = max(open, (1.0 - AlphaAt(center + (stepUv * 2.0))) * saturate(RimWidth - 1.0));
    open = max(open, (1.0 - AlphaAt(center + (stepUv * 3.0))) * saturate(RimWidth - 2.0));
    return open;
}

// Rim from one of the 8 grid neighbours: it counts when that side faces the light closely
// enough. `relax` lowers the limit for pixels toward the light-facing end of the sprite, so
// as the light swings past a side, that side lights up (or goes dark) a pixel or two at a
// time from the end nearest the light, instead of the whole edge popping at once.
float LitFrom(float2 center, float2 neighbour, float limit, float relax)
{
    float facing = dot(normalize(neighbour), LightDirection);
    return OpenAlong(center, neighbour * TexelSize) * step(limit - relax, facing);
}

float4 RimPixel(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 tex = tex2D(TextureSampler, texCoord);
    // Only solid pixels take rim (kept branch-free so ps_3_0 never has to unroll a return).
    float solid = step(0.5, tex.a);

    // Every screen pixel inside one sprite pixel decides from that sprite pixel's centre, so
    // the rim always covers whole sprite pixels.
    // The art grid starts at the frame's corner (frames in an atlas need not sit on it).
    float2 center = RectMin + ((floor((texCoord - RectMin) / TexelSize) + 0.5) * TexelSize);
    // Where this pixel sits along the light, -1 (far end) .. 1 (end nearest the light).
    float2 rectCenter = (RectMin + RectMax) * 0.5;
    float2 fromCenter = (center - rectCenter) / TexelSize;
    float2 halfSize = (RectMax - RectMin) * 0.5 / TexelSize;
    float along = clamp(dot(fromCenter, LightDirection) / max(max(halfSize.x, halfSize.y), 1.0), -1.0, 1.0);
    float relax = GrazeBand * along;

    float straight4 = max(
        max(LitFrom(center, float2(1.0, 0.0), CardinalLimit, relax), LitFrom(center, float2(-1.0, 0.0), CardinalLimit, relax)),
        max(LitFrom(center, float2(0.0, 1.0), CardinalLimit, relax), LitFrom(center, float2(0.0, -1.0), CardinalLimit, relax)));
    float diagonal4 = max(
        max(LitFrom(center, float2(1.0, 1.0), DiagonalLimit, relax), LitFrom(center, float2(-1.0, 1.0), DiagonalLimit, relax)),
        max(LitFrom(center, float2(1.0, -1.0), DiagonalLimit, relax), LitFrom(center, float2(-1.0, -1.0), DiagonalLimit, relax)));
    float rim = max(straight4, diagonal4);
    float wrap = 0.0;

    // All or nothing per pixel, so every rim pixel ends up exactly the same colour.
    float hasRim = step(0.001, dot(RimColor, float3(1.0, 1.0, 1.0)));
    float rimMask = step(0.5, max(rim, wrap)) * solid * hasRim;

    // The body: the sprite's own colour (with the draw tint), re-saturated, then darkened.
    float3 tint = color.rgb / max(color.a, 0.004);
    float3 straight = (tex.rgb / max(tex.a, 0.004)) * tint;
    float luma = dot(straight, float3(0.299, 0.587, 0.114));
    float3 body = saturate(luma + ((straight - luma) * BodySaturation)) * (1.0 - BodyShade);
    float bodyMask = solid * BodyAdjust;

    // The rim combined with the pixel it sits on (the body as shaded, else the sprite as
    // drawn), like a layer mode; the result is still one colour wherever the art is one colour.
    float3 under = lerp(straight, body, BodyAdjust);
    float3 overlayLow = 2.0 * under * RimColor;
    float3 overlayHigh = 1.0 - (2.0 * (1.0 - under) * (1.0 - RimColor));
    float3 blended =
        (RimColor * (1.0 - saturate(abs(BlendMode))))
        + (saturate(under + RimColor) * (1.0 - saturate(abs(BlendMode - 1.0))))
        + ((1.0 - ((1.0 - under) * (1.0 - RimColor))) * (1.0 - saturate(abs(BlendMode - 2.0))))
        + ((under * RimColor) * (1.0 - saturate(abs(BlendMode - 3.0))))
        + (lerp(overlayLow, overlayHigh, step(0.5, under)) * (1.0 - saturate(abs(BlendMode - 4.0))));
    float3 rimRgb = lerp(under, blended, RimOpacity);

    // Drawn with premultiplied alpha blending (src + dst * (1 - src.a)) over the sprite
    // already drawn: opaque pixels replace it, alpha 0 leaves it untouched.
    float alpha = max(rimMask, bodyMask) * color.a;
    float3 rgb = lerp(body, rimRgb, rimMask);
    return float4(rgb * alpha, alpha);
}

technique RimLight
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL RimPixel();
    }
}
