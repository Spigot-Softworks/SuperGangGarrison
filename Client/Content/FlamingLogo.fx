#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

sampler2D TextureSampler : register(s0);

texture FlameRegionTexture;
sampler2D FlameRegionSampler : register(s1) = sampler_state
{
    Texture = <FlameRegionTexture>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

texture FlameOverlayTexture;
sampler2D FlameOverlaySampler : register(s2) = sampler_state
{
    Texture = <FlameOverlayTexture>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

float FlameTime;
float FlameBlend;
float LogoFlashAmount;
float2 LogoTextureSize;
float2 StaticUvOffset;
float2 StaticUvScale;
float2 RegionUvOffset;
float2 RegionUvScale;
float2 OverlayUvOffset;
float2 OverlayUvScale;

float OrderedDither(float2 pixel)
{
    float2 cell = fmod(floor(pixel), 4.0);
    float row0 = cell.x < 1.0 ? 0.0 : (cell.x < 2.0 ? 8.0 : (cell.x < 3.0 ? 2.0 : 10.0));
    float row1 = cell.x < 1.0 ? 12.0 : (cell.x < 2.0 ? 4.0 : (cell.x < 3.0 ? 14.0 : 6.0));
    float row2 = cell.x < 1.0 ? 3.0 : (cell.x < 2.0 ? 11.0 : (cell.x < 3.0 ? 1.0 : 9.0));
    float row3 = cell.x < 1.0 ? 15.0 : (cell.x < 2.0 ? 7.0 : (cell.x < 3.0 ? 13.0 : 5.0));
    float value = cell.y < 1.0 ? row0 : (cell.y < 2.0 ? row1 : (cell.y < 3.0 ? row2 : row3));
    return value / 16.0;
}

// Small, deterministic value-noise helpers. Keeping the noise in logo-pixel
// space gives the flame the chunky, hand-pixelled breakup of the reference
// instead of a smooth screen-space distortion.
float Hash21(float2 position)
{
    float3 value = frac(float3(position.x, position.y, position.x) * 0.1031);
    value += dot(value, value.yzx + 33.33);
    return frac((value.x + value.y) * value.z);
}

float ValueNoise(float2 position)
{
    float2 cell = floor(position);
    float2 fraction = frac(position);
    float2 blend = fraction * fraction * (3.0 - (2.0 * fraction));
    float lowerLeft = Hash21(cell);
    float lowerRight = Hash21(cell + float2(1.0, 0.0));
    float upperLeft = Hash21(cell + float2(0.0, 1.0));
    float upperRight = Hash21(cell + float2(1.0, 1.0));
    return lerp(
        lerp(lowerLeft, lowerRight, blend.x),
        lerp(upperLeft, upperRight, blend.x),
        blend.y);
}

float3 FlamePalette(float heat)
{
    if (heat < 0.11) return float3(0.219608, 0.031373, 0.023529); // #380806
    if (heat < 0.25) return float3(0.607843, 0.121569, 0.000000); // #9B1F00
    if (heat < 0.40) return float3(0.768627, 0.211765, 0.043137); // #C4360B
    if (heat < 0.57) return float3(1.000000, 0.462745, 0.000000); // #FF7600
    if (heat < 0.72) return float3(0.945098, 0.756863, 0.388235); // #F1C163
    if (heat < 0.84) return float3(0.925490, 0.945098, 0.388235); // #ECF163
    if (heat < 0.94) return float3(0.992157, 1.000000, 0.815686); // #FDFFD0
    return float3(1.000000, 1.000000, 1.000000);                 // #FFFFFF
}

float4 PixelShaderFunction(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float2 safeStaticScale = max(StaticUvScale, float2(0.000001, 0.000001));
    float2 localUv = saturate((texCoord - StaticUvOffset) / safeStaticScale);
    float2 regionUv = RegionUvOffset + (localUv * RegionUvScale);
    float2 overlayUv = OverlayUvOffset + (localUv * OverlayUvScale);
    float4 baseLogo = tex2D(TextureSampler, texCoord);
    float4 region = tex2D(FlameRegionSampler, regionUv);
    float4 overlay = tex2D(FlameOverlaySampler, overlayUv);

    float2 logoPixel = floor(localUv * LogoTextureSize);
    float regionSeed = frac(dot(region.rgb, float3(0.1031, 0.11369, 0.13787)) * 19.19);
    float seedOffset = regionSeed * 47.0;

    // Sampling a vertically translated field makes the irregular hot shapes
    // climb through the lettering. A broad field bends those shapes sideways;
    // progressively smaller fields break them into tongues and sparks.
    float2 broadCoordinate = float2(
        (logoPixel.x * 0.045) + seedOffset,
        (logoPixel.y * 0.052) + (FlameTime * 1.30));
    float sidewaysWarp = ValueNoise(
        (broadCoordinate * 0.58) + float2(13.7, FlameTime * 0.18)) - 0.5;
    float2 fireCoordinate = broadCoordinate + float2(sidewaysWarp * 1.85, 0.0);
    float broadFire = ValueNoise(fireCoordinate);
    float mediumFire = ValueNoise(
        (fireCoordinate * 2.07) + float2(31.4, FlameTime * 0.72));
    float fineFire = ValueNoise(
        (fireCoordinate * 4.13) + float2(71.8, FlameTime * 1.65));
    float sparkFire = ValueNoise(float2(
        (logoPixel.x * 0.245) + seedOffset + 9.3,
        (logoPixel.y * 0.205) + (FlameTime * 5.2)));
    float turbulence = (broadFire * 0.52) + (mediumFire * 0.31) + (fineFire * 0.17);

    float verticalHeat = saturate((localUv.y - 0.475) / 0.335);
    float hotPocket = saturate((fineFire - 0.52) * 1.35);
    float pixelFlicker = (sparkFire - 0.5) * 0.12;
    float heat = verticalHeat
        + ((turbulence - 0.5) * 0.43)
        + (hotPocket * 0.13)
        + pixelFlicker;
    heat = saturate(heat + ((OrderedDither(logoPixel) - 0.5) * 0.075));

    float4 flameLayer = float4(FlamePalette(heat) * region.a, region.a);
    float4 flamingLogo = overlay + (flameLayer * (1.0 - overlay.a));
    float4 composite = lerp(baseLogo, flamingLogo, saturate(FlameBlend));
    composite.rgb = lerp(composite.rgb, composite.aaa, saturate(LogoFlashAmount));
    return composite * color;
}

technique FlamingLogo
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}
