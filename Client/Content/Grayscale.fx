#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

sampler2D TextureSampler : register(s0);

float4 PixelShaderFunction(float4 position : SV_Position, float4 color : COLOR0, float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 tex = tex2D(TextureSampler, texCoord);
    
    // Convert to grayscale using Rec. 709 luma coefficients
    // These are perception-based weights that match human brightness sensitivity
    float gray = dot(tex.rgb, float3(0.2126, 0.7152, 0.0722));
    
    // Apply subtle sepia tone that fades with brightness
    // Darker areas get mild sepia, lighter areas fade to white/desaturated
    float sepiaMix = 1.0 - (gray * 0.7); // Less sepia in bright areas
    float3 sepiaColor = float3(gray * (1.0 + sepiaMix * 0.08), gray, gray * (1.0 - sepiaMix * 0.12));
    
    // Apply contrast adjustment to the final sepia result
    sepiaColor = ((sepiaColor - 0.5) * 1.15) + 0.5;
    sepiaColor = saturate(sepiaColor); // Clamp to [0, 1]
    
    return float4(sepiaColor, tex.a) * color;
}

technique Grayscale
{
    pass Pass1
    {
        PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}
