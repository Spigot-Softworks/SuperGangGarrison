#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

texture SourceTexture;

sampler2D PointSourceSampler : register(s0) = sampler_state
{
    Texture = <SourceTexture>;
    MinFilter = Point;
    MagFilter = Point;
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
float GlowStrength;

#include "CrtCommon.fxh"

float HighlightRollOff(float value)
{
    if (value <= 0.82)
    {
        return value;
    }

    return 1.0 - 0.18 * exp(-(value - 0.82) / 0.18);
}

float4 PixelShaderFunction(float4 position : SV_Position, float4 color : COLOR0,
    float2 texCoord : TEXCOORD0) : COLOR0
{
    float2 sourceUv;
    float2 sourceFootprint;
    float2 tubeSpace;
    float2 tubeFootprint;
    float surfaceCoverage;
    float3 beam = ReconstructBeam(texCoord, sourceUv, sourceFootprint,
        tubeSpace, tubeFootprint, surfaceCoverage);

    float peakDrive = max(beam.r, max(beam.g, beam.b));
    float luminousCellFill = GlowStrength * 8.0 * saturate((peakDrive - 0.35) / 0.65);
    float3 linearOutput = ApplyMask(beam, tubeSpace, tubeFootprint, luminousCellFill) * Brightness;
    linearOutput += max(linearOutput - 0.72, 0.0) * GlowStrength;
    linearOutput = float3(HighlightRollOff(linearOutput.r), HighlightRollOff(linearOutput.g), HighlightRollOff(linearOutput.b));
    return float4(EncodeSrgb(linearOutput), 1.0) * color;
}

float4 PrepareSignalShader(float4 position : SV_Position, float4 color : COLOR0,
    float2 texCoord : TEXCOORD0) : COLOR0
{
    float2 targetSize = floor((SourceSize + 1.0) * 0.5);
    float2 firstTexel = floor(texCoord * targetSize) * 2.0;
    firstTexel = min(firstTexel, SourceSize - 1.0);
    float2 secondTexel = min(firstTexel + 1.0, SourceSize - 1.0);
    float2 topLeftUv = (firstTexel + 0.5) / SourceSize;
    float2 topRightUv = float2((secondTexel.x + 0.5) / SourceSize.x, topLeftUv.y);
    float2 bottomLeftUv = float2(topLeftUv.x, (secondTexel.y + 0.5) / SourceSize.y);
    float2 bottomRightUv = (secondTexel + 0.5) / SourceSize;

    float3 topLeft = FetchDecodedPoint(topLeftUv);
    float3 topRight = FetchDecodedPoint(topRightUv);
    float3 bottomLeft = FetchDecodedPoint(bottomLeftUv);
    float3 bottomRight = FetchDecodedPoint(bottomRightUv);
    float3 linearAverage = 0.25 * (topLeft + topRight + bottomLeft + bottomRight);
    return float4(EncodeSrgb(linearAverage), 1.0) * color;
}

technique Balanced
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}

technique PrepareSignal
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL PrepareSignalShader();
    }
}
