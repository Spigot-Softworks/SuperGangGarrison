#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

texture SourceTexture;
texture BloomTexture;

sampler2D PointSourceSampler : register(s0) = sampler_state
{
    Texture = <SourceTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D LinearSourceSampler : register(s1) = sampler_state
{
    Texture = <SourceTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D BloomSampler : register(s2) = sampler_state
{
    Texture = <BloomTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU = Clamp;
    AddressV = Clamp;
};

float2 SourceSize;
float2 OutputSize;
float2 AspectScale;
float Curvature;
float Brightness;
float BaseBeamSigma;
float BeamEnergy;
int MaskKind;
float MaskStrength;
float MaskAverageTransmission;
float MaskStripeWidth;
float MaskResolution;
float HorizontalFocus;
float MaskCellFillX;
float MaskCellFillY;
float MaskDarkTransmission;
float MaskRowPitchFactor;
float2 BlurDirection;
float BloomStrength;
float TightGlowStrength;
float HighlightKnee;
float HighlightKneeRange;

#include "CrtCommon.fxh"

float4 EmissionShader(float4 position : SV_Position, float4 color : COLOR0,
    float2 texCoord : TEXCOORD0) : COLOR0
{
    float2 sourceUv;
    float2 sourceFootprint;
    float2 tubeSpace;
    float2 tubeFootprint;
    float surfaceCoverage;
    float3 beam = ReconstructBeam(texCoord, sourceUv, sourceFootprint,
        tubeSpace, tubeFootprint, surfaceCoverage);
    float3 emission = ApplyMask(beam, tubeSpace, tubeFootprint, 0.0) * Brightness;
    return float4(emission, 1.0) * color;
}

float4 DownsampleShader(float4 position : SV_Position, float4 color : COLOR0,
    float2 texCoord : TEXCOORD0) : COLOR0
{
    float2 stepSize = 1.0 / SourceSize;
    float2 offsetA = float2(-1.5, -1.5) * stepSize;
    float2 offsetB = float2(-0.5, -1.5) * stepSize;
    float2 offsetC = float2(0.5, -1.5) * stepSize;
    float2 offsetD = float2(1.5, -1.5) * stepSize;
    float2 offsetE = float2(-1.5, -0.5) * stepSize;
    float2 offsetF = float2(-0.5, -0.5) * stepSize;
    float2 offsetG = float2(0.5, -0.5) * stepSize;
    float2 offsetH = float2(1.5, -0.5) * stepSize;
    float2 offsetI = float2(-1.5, 0.5) * stepSize;
    float2 offsetJ = float2(-0.5, 0.5) * stepSize;
    float2 offsetK = float2(0.5, 0.5) * stepSize;
    float2 offsetL = float2(1.5, 0.5) * stepSize;
    float2 offsetM = float2(-1.5, 1.5) * stepSize;
    float2 offsetN = float2(-0.5, 1.5) * stepSize;
    float2 offsetO = float2(0.5, 1.5) * stepSize;
    float2 offsetP = float2(1.5, 1.5) * stepSize;
    float3 sum = tex2D(LinearSourceSampler, texCoord + offsetA).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetB).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetC).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetD).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetE).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetF).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetG).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetH).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetI).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetJ).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetK).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetL).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetM).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetN).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetO).rgb;
    sum += tex2D(LinearSourceSampler, texCoord + offsetP).rgb;
    return float4(sum * 0.0625, 1.0) * color;
}

float4 BlurShader(float4 position : SV_Position, float4 color : COLOR0,
    float2 texCoord : TEXCOORD0) : COLOR0
{
    float3 blur = tex2D(LinearSourceSampler, texCoord).rgb * 0.28;
    blur += tex2D(LinearSourceSampler, texCoord + BlurDirection).rgb * 0.24;
    blur += tex2D(LinearSourceSampler, texCoord - BlurDirection).rgb * 0.24;
    blur += tex2D(LinearSourceSampler, texCoord + BlurDirection * 2.0).rgb * 0.12;
    blur += tex2D(LinearSourceSampler, texCoord - BlurDirection * 2.0).rgb * 0.12;
    return float4(blur, 1.0) * color;
}

float HighlightRollOff(float value)
{
    if (value <= HighlightKnee)
    {
        return value;
    }

    return 1.0 - HighlightKneeRange * exp(-(value - HighlightKnee) / HighlightKneeRange);
}

float3 ExtractPhosphorDrive(float2 uv)
{
    // Per-channel extraction allows saturated red, green, and blue cells to
    // contribute without requiring a white-oriented luma threshold.
    return max(tex2D(LinearSourceSampler, uv).rgb - HighlightKnee, 0.0);
}

float4 CompositeShader(float4 position : SV_Position, float4 color : COLOR0,
    float2 texCoord : TEXCOORD0) : COLOR0
{
    // Direct emission is read once at 1:1. Only bloom is clipped at the surface;
    // no final geometry warp is applied to the already resolved phosphor pattern.
    float2 sourceUv;
    float2 sourceFootprint;
    float2 tubeSpace;
    float2 tubeFootprint;
    MeasureFootprints(texCoord, sourceUv, sourceFootprint, tubeSpace, tubeFootprint);
    float surfaceCoverage = 1.0;
    if (Curvature > 0.0)
    {
        float2 edgeDistance = min(sourceUv * SourceSize, (1.0 - sourceUv) * SourceSize);
        surfaceCoverage = saturate(0.5 + edgeDistance.x / sourceFootprint.x)
            * saturate(0.5 + edgeDistance.y / sourceFootprint.y);
    }

    float3 directEmission = tex2D(LinearSourceSampler, texCoord).rgb;
    float tightRadius = clamp(MaskStripeWidth * 1.50, 0.75, 2.0);
    float2 tightStep = tightRadius / OutputSize;
    float3 tightSpread = ExtractPhosphorDrive(texCoord) * 0.28;
    tightSpread += ExtractPhosphorDrive(texCoord + float2(tightStep.x, 0.0)) * 0.12;
    tightSpread += ExtractPhosphorDrive(texCoord - float2(tightStep.x, 0.0)) * 0.12;
    tightSpread += ExtractPhosphorDrive(texCoord + float2(0.0, tightStep.y)) * 0.12;
    tightSpread += ExtractPhosphorDrive(texCoord - float2(0.0, tightStep.y)) * 0.12;
    tightSpread += ExtractPhosphorDrive(texCoord + tightStep) * 0.06;
    tightSpread += ExtractPhosphorDrive(texCoord - tightStep) * 0.06;
    tightSpread += ExtractPhosphorDrive(texCoord + float2(tightStep.x, -tightStep.y)) * 0.06;
    tightSpread += ExtractPhosphorDrive(texCoord + float2(-tightStep.x, tightStep.y)) * 0.06;
    float3 broadSpread = tex2D(BloomSampler, texCoord).rgb * BloomStrength;
    float3 opticalSpread = (tightSpread * TightGlowStrength + broadSpread) * surfaceCoverage;
    float3 linearColor = directEmission + opticalSpread;
    linearColor = float3(HighlightRollOff(linearColor.r), HighlightRollOff(linearColor.g), HighlightRollOff(linearColor.b));
    return float4(EncodeSrgb(linearColor), 1.0) * color;
}

technique Emission
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL EmissionShader();
    }
}

technique Downsample
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL DownsampleShader();
    }
}

technique BlurHorizontal
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL BlurShader();
    }
}

technique BlurVertical
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL BlurShader();
    }
}

technique Composite
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL CompositeShader();
    }
}
