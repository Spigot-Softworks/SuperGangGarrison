// Shared CRT geometry and beam/mask math for CrtBalanced.fx and CrtHigh.fx.
// Input is display-encoded Color data; point samples are decoded before mixing.

float3 DecodeSrgb(float3 encoded)
{
    encoded = max(encoded, 0.0);
    float3 low = encoded / 12.92;
    float3 high = pow((encoded + 0.055) / 1.055, 2.4);
    return lerp(low, high, step(0.04045, encoded));
}

float3 EncodeSrgb(float3 linearColor)
{
    linearColor = max(linearColor, 0.0);
    float3 low = linearColor * 12.92;
    float3 high = 1.055 * pow(linearColor, 1.0 / 2.4) - 0.055;
    return lerp(low, high, step(0.0031308, linearColor));
}

float2 MapOutputToSource(float2 outputUv)
{
    float2 physical = (outputUv * 2.0 - 1.0) * AspectScale;
    float radiusSquared = dot(physical, physical);
    float2 curved = physical * (1.0 + Curvature * radiusSquared);
    return 0.5 + 0.5 * curved / AspectScale;
}

void MeasureFootprints(float2 outputUv, out float2 sourceUv, out float2 sourceFootprint,
    out float2 tubeSpace, out float2 tubeFootprint)
{
    sourceUv = MapOutputToSource(outputUv);
    float2 sourcePixels = sourceUv * SourceSize;
    float2 sourceDeltaX = ddx(sourcePixels);
    float2 sourceDeltaY = ddy(sourcePixels);
    sourceFootprint = max(float2(0.125, 0.125), abs(sourceDeltaX) + abs(sourceDeltaY));

    tubeSpace = sourceUv * OutputSize;
    float2 tubeDeltaX = ddx(tubeSpace);
    float2 tubeDeltaY = ddy(tubeSpace);
    tubeFootprint = max(float2(0.125, 0.125), abs(tubeDeltaX) + abs(tubeDeltaY));
}

float3 FetchDecodedPoint(float2 texelCenterUv)
{
    float3 encodedRgb = tex2D(PointSourceSampler, saturate(texelCenterUv)).rgb;
    float inside = step(0.0, texelCenterUv.x) * step(texelCenterUv.x, 1.0)
        * step(0.0, texelCenterUv.y) * step(texelCenterUv.y, 1.0);
    return DecodeSrgb(encodedRgb) * inside;
}

float WindowedGaussianWeight(float distance, float inverseVariance)
{
    float gaussian = exp(-distance * distance * inverseVariance);
    float edgeWindow = 1.0 - smoothstep(1.0, 1.5, abs(distance));
    return gaussian * edgeWindow;
}

float3 GetHorizontalWeights(float2 sourceUv, float horizontalFootprint, out float centerPixel)
{
    float sourceX = sourceUv.x * SourceSize.x - 0.5;
    centerPixel = floor(sourceX + 0.5);
    float offset = sourceX - centerPixel;
    float sigma = sqrt(HorizontalFocus * HorizontalFocus
        + horizontalFootprint * horizontalFootprint / 12.0);
    sigma = max(0.30, sigma);
    float inverseVariance = 0.5 / (sigma * sigma);
    float leftDistance = offset + 1.0;
    float centerDistance = offset;
    float rightDistance = offset - 1.0;
    float leftWeight = WindowedGaussianWeight(leftDistance, inverseVariance);
    float centerWeight = WindowedGaussianWeight(centerDistance, inverseVariance);
    float rightWeight = WindowedGaussianWeight(rightDistance, inverseVariance);
    float normalization = max(leftWeight + centerWeight + rightWeight, 0.0001);
    return float3(leftWeight, centerWeight, rightWeight) / normalization;
}

float3 SampleDecodedRow(float2 sourceUv, float sourceRow, float centerPixel,
    float3 horizontalWeights)
{
    float sourceY = (sourceRow + 0.5) / SourceSize.y;
    float2 leftUv = float2((centerPixel - 0.5) / SourceSize.x, sourceY);
    float2 centerUv = float2((centerPixel + 0.5) / SourceSize.x, sourceY);
    float2 rightUv = float2((centerPixel + 1.5) / SourceSize.x, sourceY);
    float3 leftColor = FetchDecodedPoint(leftUv);
    float3 centerColor = FetchDecodedPoint(centerUv);
    float3 rightColor = FetchDecodedPoint(rightUv);
    return leftColor * horizontalWeights.x
        + centerColor * horizontalWeights.y
        + rightColor * horizontalWeights.z;
}

float3 EvaluateBeamRowColor(float2 sourceUv, float sourceLine, float sourceLineFootprint,
    float row, float lineModulation, float centerPixel, float3 horizontalWeights)
{
    float3 rowColor = SampleDecodedRow(sourceUv, row, centerPixel, horizontalWeights);
    float luma = saturate(dot(rowColor, float3(0.2126, 0.7152, 0.0722)));
    float brightnessSigma = BaseBeamSigma * (0.82 + 0.38 * sqrt(luma));
    // At less than two output pixels per source line, broaden the beam toward
    // the unresolved line limit to suppress unstable scanline beat patterns.
    float resolvedSigma = lerp(max(brightnessSigma, 0.40), brightnessSigma, lineModulation);
    float sigma = sqrt(resolvedSigma * resolvedSigma
        + sourceLineFootprint * sourceLineFootprint / 12.0);
    float distance = sourceLine - row;
    float gaussian = exp(-0.5 * distance * distance / (sigma * sigma));
    return rowColor * gaussian / (2.506628 * sigma);
}

float3 ReconstructBeam(float2 outputUv, out float2 sourceUv, out float2 sourceFootprint,
    out float2 tubeSpace, out float2 tubeFootprint, out float surfaceCoverage)
{
    MeasureFootprints(outputUv, sourceUv, sourceFootprint, tubeSpace, tubeFootprint);
    float sourceLine = sourceUv.y * SourceSize.y - 0.5;
    float sourceLineFootprint = sourceFootprint.y;
    float linePitchInOutputPixels = 1.0 / max(sourceLineFootprint, 0.0001);
    float lineModulation = smoothstep(1.0, 2.0, linePitchInOutputPixels);
    float centerPixel;
    float3 horizontalWeights = GetHorizontalWeights(sourceUv, sourceFootprint.x, centerPixel);
    float nearestLine = floor(sourceLine + 0.5);

    float3 beam = EvaluateBeamRowColor(sourceUv, sourceLine, sourceLineFootprint,
        nearestLine - 2.0, lineModulation, centerPixel, horizontalWeights);
    beam += EvaluateBeamRowColor(sourceUv, sourceLine, sourceLineFootprint,
        nearestLine - 1.0, lineModulation, centerPixel, horizontalWeights);
    beam += EvaluateBeamRowColor(sourceUv, sourceLine, sourceLineFootprint,
        nearestLine, lineModulation, centerPixel, horizontalWeights);
    beam += EvaluateBeamRowColor(sourceUv, sourceLine, sourceLineFootprint,
        nearestLine + 1.0, lineModulation, centerPixel, horizontalWeights);
    beam += EvaluateBeamRowColor(sourceUv, sourceLine, sourceLineFootprint,
        nearestLine + 2.0, lineModulation, centerPixel, horizontalWeights);
    beam *= BeamEnergy;

    if (Curvature > 0.0)
    {
        float2 edgeDistance = min(sourceUv * SourceSize, (1.0 - sourceUv) * SourceSize);
        float coverageX = saturate(0.5 + edgeDistance.x / sourceFootprint.x);
        float coverageY = saturate(0.5 + edgeDistance.y / sourceFootprint.y);
        surfaceCoverage = coverageX * coverageY;
    }
    else
    {
        surfaceCoverage = 1.0;
    }

    return beam * surfaceCoverage;
}

float PeriodicPulseIntegral(float coordinate, float period, float activeWidth, float phase)
{
    float shifted = coordinate - phase;
    float periodIndex = floor(shifted / period);
    float positionInPeriod = shifted - periodIndex * period;
    return periodIndex * activeWidth + min(positionInPeriod, activeWidth);
}

float AveragePeriodicPulse(float center, float halfFootprint, float period,
    float activeWidth, float phase)
{
    float left = center - halfFootprint;
    float right = center + halfFootprint;
    float span = max(0.0001, right - left);
    return saturate((PeriodicPulseIntegral(right, period, activeWidth, phase)
        - PeriodicPulseIntegral(left, period, activeWidth, phase)) / span);
}

float AverageStripePulse(float center, float halfFootprint, float channel)
{
    return AveragePeriodicPulse(center - channel, halfFootprint, 3.0, MaskCellFillX, 0.0);
}

float3 TriadPattern(float stripeCoordinate, float halfFootprint)
{
    float redPulse = AverageStripePulse(stripeCoordinate, halfFootprint, 0.0);
    float greenPulse = AverageStripePulse(stripeCoordinate, halfFootprint, 1.0);
    float bluePulse = AverageStripePulse(stripeCoordinate, halfFootprint, 2.0);
    float3 primary = float3(redPulse, greenPulse, bluePulse);
    return MaskDarkTransmission + (1.0 - MaskDarkTransmission) * primary;
}

float3 MaskPattern(float2 tubeSpace, float2 tubeFootprint)
{
    float stripeWidth = max(0.25, MaskStripeWidth);
    float rowPeriod = max(0.5, stripeWidth * MaskRowPitchFactor);
    float rowCoordinate = tubeSpace.y / rowPeriod;
    float rowHalfFootprint = 0.5 * tubeFootprint.y / rowPeriod;
    float stripeCoordinate = tubeSpace.x / stripeWidth;
    float halfStripeFootprint = 0.5 * tubeFootprint.x / stripeWidth;
    float3 unStaggered = TriadPattern(stripeCoordinate, halfStripeFootprint);

    if (MaskKind == 2)
    {
        return unStaggered;
    }

    float rowPhase = MaskKind == 4 ? 0.75 : 1.5;
    float3 staggered = TriadPattern(stripeCoordinate + rowPhase, halfStripeFootprint);
    float evenCells = AveragePeriodicPulse(rowCoordinate, rowHalfFootprint,
        2.0, MaskCellFillY, 0.0);
    float oddCells = AveragePeriodicPulse(rowCoordinate, rowHalfFootprint,
        2.0, MaskCellFillY, 1.0);
    return MaskDarkTransmission
        + (unStaggered - MaskDarkTransmission) * evenCells
        + (staggered - MaskDarkTransmission) * oddCells;
}

float3 ApplyMask(float3 emission, float2 tubeSpace, float2 tubeFootprint, float luminousCellFill)
{
    float stripeWidth = max(0.25, MaskStripeWidth);
    float stripeFootprint = tubeFootprint.x / stripeWidth;
    float rowFootprint = MaskKind == 2 ? 0.0
        : tubeFootprint.y / max(0.25, stripeWidth * MaskRowPitchFactor);
    float localFootprint = max(stripeFootprint, rowFootprint);
    float localResolution = 1.0 - smoothstep(1.4, 2.4, localFootprint);
    float effectiveStrength = saturate(MaskStrength * MaskResolution * localResolution
        * (1.0 - saturate(luminousCellFill)));
    float3 pattern = MaskPattern(tubeSpace, tubeFootprint);
    float3 transmission = lerp(1.0, pattern, effectiveStrength);
    float meanTransmission = max(0.35,
        1.0 - effectiveStrength * (1.0 - MaskAverageTransmission));
    // The SDR shoulder below provides controlled highlight headroom for bright
    // phosphor cells. Compensation never adds a black pedestal.
    float compensation = min(1.90, 1.0 / meanTransmission);
    return emission * transmission * compensation;
}
